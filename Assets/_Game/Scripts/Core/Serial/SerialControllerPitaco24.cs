// Receptor C do PITACO 2.4: identificacao exclusiva + JSON de RED/IR.
// Nao modifica o PITACO 2.0 (echop) nem o oximetro legado (echox).
using System;
using System.Collections;
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
        public const string EchoCommand = "e";
        public const string EchoResponse = "echo2.4p";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SerialControllerPitaco24>() != null) return;
            var go = new GameObject("PITACO 2.4 - Receptor C");
            DontDestroyOnLoad(go);
            go.AddComponent<SerialControllerPitaco24>();
        }

        [SerializeField] private int baudRate = 115200;
        [SerializeField] private int probeTimeoutMs = 1500;
        [SerializeField] private int reconnectSeconds = 5;
        [SerializeField] private int maxQueuedMessages = 512;
        [SerializeField] private int maxMessagesPerFrame = 64;

        public static SerialControllerPitaco24 Instance { get; private set; }
        public bool IsConnected { get; private set; }
        public string ConnectedPort { get; private set; }
        public event Action<string> OnJsonReceived;
        public event Action OnSerialConnected;
        public event Action OnSerialDisconnected;

        private SerialThreadPitaco worker;
        private Thread thread;
        private Coroutine retry;
        private bool shuttingDown;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Connect();
        }

        private static string[] Ports()
        {
            var p = Application.platform;
            if (p == RuntimePlatform.LinuxEditor || p == RuntimePlatform.LinuxPlayer ||
                p == RuntimePlatform.OSXEditor || p == RuntimePlatform.OSXPlayer)
                return Directory.GetFiles("/dev/").Where(x => x.StartsWith("/dev/ttyACM") ||
                    x.StartsWith("/dev/ttyUSB") || x.StartsWith("/dev/tty.usb")).ToArray();
            return SerialPort.GetPortNames();
        }

        private string Discover()
        {
            foreach (string name in Ports())
            {
                try
                {
                    using (var port = new SerialPort(name, baudRate))
                    {
                        port.ReadTimeout = 150;
                        port.WriteTimeout = 500;
                        port.DtrEnable = true;
                        port.RtsEnable = true;
                        port.Open();
                        port.DiscardInBuffer();
                        port.Write(EchoCommand);
                        var until = DateTime.UtcNow.AddMilliseconds(Math.Max(300, probeTimeoutMs));
                        while (DateTime.UtcNow < until)
                        {
                            try
                            {
                                if (string.Equals(port.ReadLine().Trim(), EchoResponse,
                                    StringComparison.Ordinal)) return name;
                            }
                            catch (TimeoutException) { }
                        }
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                    e is InvalidOperationException || e is TimeoutException ||
                    e is ArgumentException) { }
            }
            return null;
        }

        private void Connect()
        {
            if (shuttingDown || thread != null) return;
            string name = Discover();
            if (name == null)
            {
                if (retry == null) retry = StartCoroutine(Retry());
                return;
            }
            ConnectedPort = name;
            // Reusa o transporte existente, mas com uma fila apropriada a PPG.
            worker = new SerialThreadPitaco(name, baudRate, 1000, Math.Max(256, maxQueuedMessages));
            thread = new Thread(worker.RunForever) { IsBackground = true, Name = "Pitaco24Reader" };
            thread.Start();
            Debug.Log("[PITACO 2.4] Receptor C identificado em " + name);
        }

        private IEnumerator Retry()
        {
            while (!shuttingDown && thread == null)
            {
                yield return new WaitForSeconds(Math.Max(1, reconnectSeconds));
                Connect();
            }
            retry = null;
        }

        public void SendCommand(string command)
        {
            if (IsConnected && worker != null && !string.IsNullOrEmpty(command))
                worker.SendSerialMessage(command);
        }

        private void Update()
        {
            if (worker == null) return;
            for (int i = 0; i < Math.Max(1, maxMessagesPerFrame); i++)
            {
                string line = worker.ReadSerialMessage();
                if (line == null) break;
                if (line == SerialControllerPitaco.SERIAL_DEVICE_CONNECTED)
                {
                    if (!IsConnected)
                    {
                        IsConnected = true;
                        OnSerialConnected?.Invoke();
                        SendCommand("r");
                    }
                    continue;
                }
                if (line == SerialControllerPitaco.SERIAL_DEVICE_DISCONNECTED)
                {
                    if (IsConnected)
                    {
                        IsConnected = false;
                        OnSerialDisconnected?.Invoke();
                    }
                    continue;
                }
                if (!IsConnected || line.Length > 1024) continue;
                try
                {
                    var obj = JObject.Parse(line);
                    string type = (string)obj["type"];
                    if (type == "sample" || type == "g1")
                        OnJsonReceived?.Invoke(line);
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
        }

        private void OnDestroy()
        {
            shuttingDown = true;
            if (retry != null) StopCoroutine(retry);
            if (IsConnected) SendCommand("f");
            worker?.RequestStop();
            if (thread != null && thread.IsAlive) thread.Join(1500);
            worker = null;
            thread = null;
            IsConnected = false;
            if (Instance == this) Instance = null;
        }
    }
}
