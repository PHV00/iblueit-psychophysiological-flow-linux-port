/*
 * Adaptacao para PITACO 2.4 do padrao SerialCommUnity usado no I Blue It.
 * Base original: Daniel Wilches <dwilches@gmail.com>, licenca CC BY 2.0.
 * Modulos do legado preservados; esta versao adiciona handshake especifico
 * e recepcao continua para telemetria JSON do PITACO 2.4.
 */

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace Ibit.Core.Serial
{
    // Adaptado do SerialThreadPitaco original.
    // Thread de I/O separada: nenhuma API de GameObject ou UI aqui.
    public sealed class SerialThreadPitaco24
    {
        private const int READ_TIMEOUT_MS = 150;
        private const int WRITE_TIMEOUT_MS = 1000;

        private readonly string portName;
        private readonly int baudRate;
        private readonly int delayBeforeReconnecting;
        private readonly int maxUnreadMessages;
        private readonly int reopenSettleTimeMs;

        private readonly Queue<string> inputQueue = new Queue<string>();
        private readonly Queue<string> statusQueue = new Queue<string>();
        private readonly Queue<string> outputQueue = new Queue<string>();
        private readonly object inputLock = new object();
        private readonly object statusLock = new object();
        private readonly object outputLock = new object();

        private SerialPort serialPort;  // Acessado somente pela thread de I/O.
        private volatile bool stopRequested;
        private long droppedMessages;
        private string lastError;

        public long DroppedMessages
        {
            get { return Interlocked.Read(ref droppedMessages); }
        }

        public string LastError
        {
            get { return lastError; }
        }

        public SerialThreadPitaco24(string portName, int baudRate,
                                    int delayBeforeReconnecting,
                                    int maxUnreadMessages,
                                    int reopenSettleTimeMs)
        {
            this.portName = portName;
            this.baudRate = baudRate;
            this.delayBeforeReconnecting = Math.Max(100, delayBeforeReconnecting);
            this.maxUnreadMessages = Math.Max(8, maxUnreadMessages);
            this.reopenSettleTimeMs = Math.Max(0, reopenSettleTimeMs);
        }

        // Chamado no Update() da thread principal do Unity.
        public string ReadSerialMessage()
        {
            // Eventos de conexao nao competem com a fila de telemetria.
            lock (statusLock)
            {
                if (statusQueue.Count > 0)
                    return statusQueue.Dequeue();
            }

            lock (inputLock)
            {
                if (inputQueue.Count == 0)
                    return null;
                return inputQueue.Dequeue();
            }
        }

        // Chamado pelo Controller/Commands; gravacao ocorre nesta thread.
        public void SendSerialMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            lock (outputLock)
                outputQueue.Enqueue(message);
        }

        public void RequestStop()
        {
            stopRequested = true;
        }

        public void RunForever()
        {
            while (!stopRequested)
            {
                try
                {
                    AttemptConnection();
                    lastError = null;
                    ClearInputQueue();
                    EnqueueStatus(SerialControllerPitaco24.SERIAL_DEVICE_CONNECTED);

                    while (!stopRequested)
                    {
                        WritePendingMessages();
                        try
                        {
                            string line = serialPort.ReadLine();
                            if (!string.IsNullOrEmpty(line))
                                EnqueueData(line.Trim());
                        }
                        catch (TimeoutException)
                        {
                            // Porta sem linha completa: comportamento normal.
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.GetType().Name + ": " + ex.Message;
                    if (!stopRequested)
                    {
                        ClearInputQueue();
                        ClearOutputQueue();
                        EnqueueStatus(SerialControllerPitaco24.SERIAL_DEVICE_DISCONNECTED);
                    }
                }
                finally
                {
                    if (stopRequested)
                    {
                        // Tenta encaminhar o 'f' enfileirado antes de encerrar.
                        try { WritePendingMessages(); }
                        catch (Exception) { }
                    }
                    CloseDevice();
                }

                if (!stopRequested)
                    Thread.Sleep(delayBeforeReconnecting);
            }
        }

        private void AttemptConnection()
        {
            serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = READ_TIMEOUT_MS,
                WriteTimeout = WRITE_TIMEOUT_MS,
                DtrEnable = true,
                RtsEnable = true,
                Handshake = Handshake.None,
                NewLine = "\n"
            };

            serialPort.Open();

            // Abertura da USB fisica apos o probe pode reiniciar o ESP32-C3.
            if (reopenSettleTimeMs > 0)
                Thread.Sleep(reopenSettleTimeMs);

            if (stopRequested)
                return;

            // Descarta apenas as mensagens de boot anteriores a esta conexao.
            serialPort.DiscardInBuffer();
        }

        private void WritePendingMessages()
        {
            if (serialPort == null || !serialPort.IsOpen)
                return;

            while (true)
            {
                string command;
                lock (outputLock)
                {
                    if (outputQueue.Count == 0)
                        return;
                    command = outputQueue.Dequeue();
                }

                // Comandos do firmware sao caracteres individuais.
                // Nao adiciona '\n' depois de 'r', 'f', 'c', 'z' ou 'i'.
                serialPort.Write(command);
            }
        }

        private void EnqueueData(string line)
        {
            lock (inputLock)
            {
                if (inputQueue.Count >= maxUnreadMessages)
                {
                    // Para graficos em tempo real, mantem os dados mais novos.
                    // Todo descarte e contabilizado, nunca silencioso.
                    inputQueue.Dequeue();
                    Interlocked.Increment(ref droppedMessages);
                }
                inputQueue.Enqueue(line);
            }
        }

        private void EnqueueStatus(string status)
        {
            lock (statusLock)
                statusQueue.Enqueue(status);
        }

        private void ClearInputQueue()
        {
            lock (inputLock)
                inputQueue.Clear();
        }

        private void ClearOutputQueue()
        {
            lock (outputLock)
                outputQueue.Clear();
        }

        private void CloseDevice()
        {
            if (serialPort == null)
                return;

            try { serialPort.Close(); }
            catch (Exception) { }
            try { serialPort.Dispose(); }
            catch (Exception) { }
            serialPort = null;
        }
    }
}
