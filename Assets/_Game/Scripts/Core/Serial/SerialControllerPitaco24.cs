/*
 * Adaptacao para PITACO 2.4 do padrao SerialCommUnity usado no I Blue It.
 * Base original: Daniel Wilches <dwilches@gmail.com>, licenca CC BY 2.0.
 * Modulos do legado preservados; esta versao adiciona handshake especifico
 * e recepcao continua para telemetria JSON do PITACO 2.4.
 */

using System;
using System.Collections;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace Ibit.Core.Serial
{
    // Adaptado do SerialControllerPitaco original.
    // Fase 1: identifica PITACO 2.4, recebe linhas e repassa JSON bruto.
    // Nao interfere com SerialControllerPitaco, Mano, Cinta ou Oximetro.
    public partial class SerialControllerPitaco24 : MonoBehaviour
    {
        public const string SERIAL_DEVICE_CONNECTED = "__Connected__";
        public const string SERIAL_DEVICE_DISCONNECTED = "__Disconnected__";

        private const string ID_COMMAND = "i";
        private const string ID_RESPONSE = "echo2.4p";

        [Header("Identificacao USB do PITACO 2.4")]
        [SerializeField] private int arduinoOpenTime = 3000;
        [SerializeField] private int baudRate = 115200;
        [Tooltip("Deixe vazio para procurar todas as portas COM/tty.")]
        [SerializeField] private string preferredPort = "";
        [SerializeField] private int handshakeTimeoutMs = 3000;

        [Header("Recepcao serial")]
        [SerializeField] private int maxUnreadMessages = 256;
        [SerializeField] private int maxMessagesPerFrame = 128;
        [SerializeField] private int reconnectionDelay = 1000;
        [SerializeField] private int reopenSettleTimeMs = 1200;
        [SerializeField] private bool startSamplingOnConnect = true;
        [SerializeField] private int logEveryNJsonMessages = 100;

        private SerialThreadPitaco24 serialThread;
        private Thread thread;
        private Coroutine reconnectCoroutine;

        public bool IsConnected { get; private set; }
        public long JsonMessagesReceived { get; private set; }
        public long DroppedMessages
        {
            get { return serialThread != null ? serialThread.DroppedMessages : 0; }
        }

        public event Action OnSerialConnected;
        public event Action OnSerialDisconnected;
        public event Action<string> OnSerialMessageReceived;
        public event Action<string> OnJsonReceived;

        private void Awake()
        {
            if (PitacoConnectionManager.Active == null)
                Connect();
        }

        // Mesmo criterio do controlador original para descobrir portas.
        private static string[] GetPortNames()
        {
            if (Application.platform == RuntimePlatform.LinuxPlayer ||
                Application.platform == RuntimePlatform.LinuxEditor ||
                Application.platform == RuntimePlatform.OSXPlayer ||
                Application.platform == RuntimePlatform.OSXEditor)
            {
                return Directory.GetFiles("/dev/")
                    .Where(p => p.StartsWith("/dev/ttyACM") ||
                                p.StartsWith("/dev/ttyUSB") ||
                                p.StartsWith("/dev/tty.usb"))
                    .ToArray();
            }

            return SerialPort.GetPortNames();
        }

        private string AutoConnect()
        {
            string[] ports = string.IsNullOrEmpty(preferredPort)
                ? GetPortNames()
                : new[] { preferredPort };

            foreach (string port in ports)
            {
                try
                {
                    using (var sp = new SerialPort(port, baudRate))
                    {
                        sp.ReadTimeout = 200;
                        sp.WriteTimeout = 1000;
                        sp.DtrEnable = true;
                        sp.RtsEnable = true;
                        sp.Handshake = Handshake.None;
                        sp.NewLine = "\n";
                        sp.Open();

                        // A abertura da USB nativa pode reiniciar o ESP32-C3.
                        Thread.Sleep(Math.Max(0, arduinoOpenTime));
                        sp.DiscardInBuffer();
                        sp.Write(ID_COMMAND);

                        DateTime deadline = DateTime.UtcNow.AddMilliseconds(
                            Math.Max(500, handshakeTimeoutMs));

                        // O firmware pode enviar linhas X,... antes da resposta.
                        while (DateTime.UtcNow < deadline)
                        {
                            try
                            {
                                string response = sp.ReadLine().Trim();
                                if (string.Equals(response, ID_RESPONSE,
                                                  StringComparison.Ordinal))
                                {
                                    Debug.Log("[PITACO 2.4] Handshake confirmado em " +
                                              port + ": " + response);
                                    return port;
                                }
                            }
                            catch (TimeoutException)
                            {
                                // Continua ate o prazo limite.
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PITACO 2.4] Falha ao testar " + port +
                                     ": " + ex.Message);
                }
            }

            return null;
        }

        public void Connect()
        {
            if (PitacoConnectionManager.Active != null)
                return;
            if (serialThread != null)
                return;

            string portName = AutoConnect();
            if (string.IsNullOrEmpty(portName))
            {
                Debug.LogWarning("[PITACO 2.4] Nao identificado. Nova tentativa em 10 s.");
                if (reconnectCoroutine == null)
                    reconnectCoroutine = StartCoroutine(Reconnect());
                return;
            }

            serialThread = new SerialThreadPitaco24(
                portName,
                baudRate,
                reconnectionDelay,
                maxUnreadMessages,
                reopenSettleTimeMs);

            thread = new Thread(serialThread.RunForever);
            thread.Name = "SerialThreadPitaco24";
            thread.IsBackground = true;
            thread.Start();
        }

        private IEnumerator Reconnect()
        {
            yield return new WaitForSeconds(10f);
            reconnectCoroutine = null;
            Connect();
        }

        public void SendSerialMessage(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(message))
                return;

            if (PitacoConnectionManager.Active != null)
            {
                PitacoConnectionManager.Active.SendToPitaco24(message);
                return;
            }
            if (serialThread != null)
                serialThread.SendSerialMessage(message);
        }

        internal void ManagedConnected()
        {
            if (IsConnected) return;
            IsConnected = true;
            Debug.Log("[PITACO 2.4] Porta serial ativa (manager).");
            OnSerialConnected?.Invoke();
            if (startSamplingOnConnect)
                StartSampling();
        }

        internal void ManagedDisconnected()
        {
            if (!IsConnected) return;
            IsConnected = false;
            OnSerialDisconnected?.Invoke();
        }

        internal void ManagedLine(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(message)) return;
            OnSerialMessageReceived?.Invoke(message);
            string line = message.Trim();
            if (!line.StartsWith("{", StringComparison.Ordinal) ||
                !line.EndsWith("}", StringComparison.Ordinal))
                return;
            JsonMessagesReceived++;
            OnJsonReceived?.Invoke(line);
            if (logEveryNJsonMessages > 0 && JsonMessagesReceived % logEveryNJsonMessages == 0)
                Debug.Log("[PITACO 2.4] JSON: " + JsonMessagesReceived +
                          " | descartes da fila: " + PitacoConnectionManager.Active.DroppedLines);
        }

        private void Update()
        {
            if (PitacoConnectionManager.Active != null)
                return;
            if (serialThread == null)
                return;

            // Drena multiplas mensagens por frame. A fila do legado = 1
            // pode descartar grande parte do JSON optico do 2.4.
            int limit = Math.Max(1, maxMessagesPerFrame);
            for (int i = 0; i < limit; i++)
            {
                string message = serialThread.ReadSerialMessage();
                if (message == null)
                    break;

                if (message == SERIAL_DEVICE_CONNECTED)
                {
                    if (!IsConnected)
                    {
                        IsConnected = true;
                        Debug.Log("[PITACO 2.4] Porta serial ativa.");
                        OnSerialConnected?.Invoke();
                        if (startSamplingOnConnect)
                            StartSampling();
                    }
                }
                else if (message == SERIAL_DEVICE_DISCONNECTED)
                {
                    if (IsConnected)
                    {
                        IsConnected = false;
                        Debug.LogWarning("[PITACO 2.4] Conexao perdida: " +
                                         serialThread.LastError);
                        OnSerialDisconnected?.Invoke();
                    }
                }
                else if (IsConnected)
                {
                    // Contrato identico ao legado: repassa a linha bruta.
                    OnSerialMessageReceived?.Invoke(message);

                    // Somente identifica linhas com aparencia de JSON.
                    // Validacao de esquema/SQI e uma etapa posterior.
                    string line = message.Trim();
                    if (line.StartsWith("{", StringComparison.Ordinal) &&
                        line.EndsWith("}", StringComparison.Ordinal))
                    {
                        JsonMessagesReceived++;
                        OnJsonReceived?.Invoke(line);

                        if (logEveryNJsonMessages > 0 &&
                            JsonMessagesReceived % logEveryNJsonMessages == 0)
                        {
                            Debug.Log("[PITACO 2.4] JSON: " + JsonMessagesReceived +
                                      " | descartes da fila: " + DroppedMessages);
                        }
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (PitacoConnectionManager.Active != null)
            {
                ManagedDisconnected();
                return; // A COM permanece aberta na troca de cena.
            }
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }

            if (serialThread != null)
            {
                if (IsConnected)
                    serialThread.SendSerialMessage("f");

                serialThread.RequestStop();
            }

            if (thread != null && thread.IsAlive)
                thread.Join(2000);

            serialThread = null;
            thread = null;
            IsConnected = false;
        }
    }
}
