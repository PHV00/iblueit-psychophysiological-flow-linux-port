// PITACO 2.4: receptor C conectado DIRETAMENTE ao ESP32-C3 por USB.
// Nenhuma dependencia da Bridge Python. Identificacao: i -> echo2.4p.
// Os dispositivos legados continuam com e -> echop / echox.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ibit.Core.Serial
{
    public sealed class SerialControllerPitaco24 : MonoBehaviour
    {
        public const string EchoCommand = "i";
        public const string EchoResponse = "echo2.4p";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SerialControllerPitaco24>() != null)
                return;
            var go = new GameObject("PITACO 2.4 - Receptor C (USB)");
            DontDestroyOnLoad(go);
            go.AddComponent<SerialControllerPitaco24>();
        }

        [SerializeField] private int baudRate = 115200;
        [SerializeField] private int identificationTimeoutMs = 3500;
        [SerializeField] private int reconnectSeconds = 5;
        [SerializeField] private int maxQueuedLines = 1024;
        [SerializeField] private int maxLinesPerFrame = 128;

        public static SerialControllerPitaco24 Instance { get; private set; }
        public bool IsConnected { get; private set; }
        public string ConnectedPort { get; private set; }
        public long SampleCount { get; private set; }
        public long G1Count { get; private set; }
        public long DroppedLines { get; private set; }
        public long SequenceGaps { get; private set; }

        public event Action<string> OnJsonReceived;
        public event Action OnSerialConnected;
        public event Action OnSerialDisconnected;

        private readonly Queue<string> pending = new Queue<string>();
        private readonly object pendingLock = new object();
        private SerialPort serialPort;
        private Thread reader;
        private Coroutine reconnectCoroutine;
        private volatile bool stopRequested;
        private volatile bool readFailed;
        private bool shuttingDown;
        private int? previousSequence;
        private long lastReportedSamples;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            AttemptConnect();
        }

        private static string[] GetPortNames()
        {
            var platform = Application.platform;
            if (platform == RuntimePlatform.LinuxEditor ||
                platform == RuntimePlatform.LinuxPlayer ||
                platform == RuntimePlatform.OSXEditor ||
                platform == RuntimePlatform.OSXPlayer)
            {
                return Directory.GetFiles("/dev/")
                    .Where(p => p.StartsWith("/dev/ttyACM") ||
                                p.StartsWith("/dev/ttyUSB") ||
                                p.StartsWith("/dev/tty.usb")).ToArray();
            }
            return SerialPort.GetPortNames();
        }

        // Retorna a porta AINDA ABERTA. Nao ha intervalo entre identificar e
        // adquirir o dispositivo, evitando disputa de COM com os receptores A/B.
        private SerialPort ProbeUsbPort()
        {
            foreach (string name in GetPortNames())
            {
                SerialPort candidate = null;
                bool identified = false;
                try
                {
                    candidate = new SerialPort(name, baudRate)
                    {
                        ReadTimeout = 120,
                        WriteTimeout = 500,
                        DtrEnable = true,
                        RtsEnable = true,
                        Handshake = Handshake.None
                    };
                    candidate.Open();
                    candidate.DiscardInBuffer();

                    var until = DateTime.UtcNow.AddMilliseconds(
                        Math.Max(500, identificationTimeoutMs));
                    var nextProbe = DateTime.MinValue;
                    while (DateTime.UtcNow < until)
                    {
                        if (DateTime.UtcNow >= nextProbe)
                        {
                            candidate.Write(EchoCommand);
                            nextProbe = DateTime.UtcNow.AddMilliseconds(500);
                        }
                        try
                        {
                            // Pode haver status e amostras antigos no buffer.
                            string line = candidate.ReadLine().Trim();
                            if (string.Equals(line, EchoResponse,
                                StringComparison.Ordinal))
                            {
                                identified = true;
                                return candidate;
                            }
                        }
                        catch (TimeoutException)
                        {
                            // Aguarda o reset/boot do ESP32 ate o limite.
                        }
                    }
                }
                catch (Exception e) when (e is IOException ||
                                           e is UnauthorizedAccessException ||
                                           e is InvalidOperationException ||
                                           e is ArgumentException ||
                                           e is TimeoutException)
                {
                    // Porta ocupada, dispositivo legado ou sem resposta.
                }
                finally
                {
                    if (!identified && candidate != null)
                        candidate.Dispose();
                }
            }
            return null;
        }

        private void AttemptConnect()
        {
            if (shuttingDown || serialPort != null)
                return;

            var matchedPort = ProbeUsbPort();
            if (matchedPort == null)
            {
                if (reconnectCoroutine == null)
                    reconnectCoroutine = StartCoroutine(Reconnect());
                return;
            }

            serialPort = matchedPort;
            ConnectedPort = matchedPort.PortName;
            stopRequested = false;
            readFailed = false;
            previousSequence = null;
            lock (pendingLock)
                pending.Clear();

            reader = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "Pitaco24UsbReader"
            };
            reader.Start();
            IsConnected = true;
            Debug.Log("[PITACO 2.4] echo2.4p reconhecido diretamente em " +
                      ConnectedPort);
            OnSerialConnected?.Invoke();
            // Mantem contrato original de inicio da sessao respiratoria/PPG.
            SendCommand("r");
        }

        private IEnumerator Reconnect()
        {
            while (!shuttingDown && serialPort == null)
            {
                yield return new WaitForSeconds(Math.Max(1, reconnectSeconds));
                AttemptConnect();
            }
            reconnectCoroutine = null;
        }

        private void ReadLoop()
        {
            try
            {
                while (!stopRequested)
                {
                    string line;
                    try
                    {
                        line = serialPort.ReadLine();
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(line))
                        continue;

                    lock (pendingLock)
                    {
                        if (pending.Count >= Math.Max(256, maxQueuedLines))
                            DroppedLines++;
                        else
                            pending.Enqueue(line);
                    }
                }
            }
            catch (Exception e) when (e is IOException ||
                                       e is InvalidOperationException ||
                                       e is ObjectDisposedException ||
                                       e is UnauthorizedAccessException)
            {
                if (!stopRequested)
                    readFailed = true;
            }
        }

        public void SendCommand(string command)
        {
            if (!IsConnected || serialPort == null ||
                string.IsNullOrEmpty(command))
                return;
            try
            {
                // Comandos do firmware sao caracteres, sem CRLF obrigatorio.
                serialPort.Write(command);
            }
            catch (Exception e) when (e is IOException ||
                                       e is InvalidOperationException ||
                                       e is ObjectDisposedException ||
                                       e is TimeoutException)
            {
                readFailed = true;
            }
        }

        private void Update()
        {
            if (readFailed)
            {
                CloseCurrentConnection();
                if (!shuttingDown && reconnectCoroutine == null)
                    reconnectCoroutine = StartCoroutine(Reconnect());
                return;
            }

            for (int i = 0; i < Math.Max(1, maxLinesPerFrame); ++i)
            {
                string line;
                lock (pendingLock)
                {
                    if (pending.Count == 0)
                        break;
                    line = pending.Dequeue();
                }

                if (line.Length > 1024)
                    continue;

                try
                {
                    var frame = JObject.Parse(line);
                    string type = (string)frame["type"];
                    if (type == "sample")
                    {
                        // Somente verificar campos RAW e continuidade.
                        // SQI, BPM e SpO2 ficam para o futuro integrador.
                        var ppg = frame["ppg"];
                        var red = ppg == null ? null : ppg["red"];
                        var ir = ppg == null ? null : ppg["ir"];
                        var seq = (int?)frame["seq"];
                        if (red == null || ir == null || !seq.HasValue)
                            continue;

                        if (previousSequence.HasValue &&
                            ((seq.Value - previousSequence.Value + 65536) % 65536) != 1)
                            SequenceGaps++;
                        previousSequence = seq.Value;
                        SampleCount++;
                    }
                    else if (type == "g1")
                    {
                        G1Count++;
                    }
                    else
                        continue;

                    OnJsonReceived?.Invoke(line);
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    // Ignora diagnosticos/quadros parcialmente recebidos.
                }
            }

            if (SampleCount - lastReportedSamples >= 250)
            {
                lastReportedSamples = SampleCount;
                Debug.Log("[PITACO 2.4] JSON USB: " + SampleCount +
                    " amostras, G1=" + G1Count +
                    ", lacunas=" + SequenceGaps +
                    ", fila descartada=" + DroppedLines);
            }
        }

        private void CloseCurrentConnection()
        {
            if (IsConnected)
            {
                IsConnected = false;
                OnSerialDisconnected?.Invoke();
            }

            stopRequested = true;
            try { serialPort?.Close(); }
            catch (Exception) { }
            if (reader != null && reader.IsAlive)
                reader.Join(700);
            reader = null;
            try { serialPort?.Dispose(); }
            catch (Exception) { }
            serialPort = null;
            ConnectedPort = null;
            readFailed = false;
            previousSequence = null;
            lock (pendingLock)
                pending.Clear();
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            if (reconnectCoroutine != null)
                StopCoroutine(reconnectCoroutine);
            if (IsConnected)
                SendCommand("f");
            CloseCurrentConnection();
            if (Instance == this)
                Instance = null;
        }
    }
}
