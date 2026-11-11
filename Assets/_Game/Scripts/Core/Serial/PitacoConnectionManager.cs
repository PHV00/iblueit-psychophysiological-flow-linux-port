// Unico proprietario da porta serial USB do PITACO (2.4 com fallback 2.0).
// Os controladores existentes continuam responsaveis por eventos/protocolos.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ibit.Core.Oximetry;

namespace Ibit.Core.Serial
{
    [DefaultExecutionOrder(-20000)]
    public sealed class PitacoConnectionManager : MonoBehaviour
    {
        public enum Version { None, Pitaco20, Pitaco24 }
        private enum PacketKind { Connected, Disconnected, Line, Warning }

        private sealed class Packet
        {
            public PacketKind Kind;
            public Version DeviceVersion;
            public string Text;
        }

        public static PitacoConnectionManager Active { get; private set; }

        [Header("USB PITACO — 2.4 primeiro, 2.0 como fallback")]
        [SerializeField] private int baudRate = 115200;
        [SerializeField] private int arduinoOpenTimeMs = 3000;
        [SerializeField] private int handshakeTimeoutMs = 3000;
        [SerializeField] private int scanRetryMs = 5000;
        [SerializeField] private int maxPendingLines = 2048;
        [SerializeField] private int maxLinesPerFrame = 256;
        [SerializeField] private string preferredPort = "";

        private readonly object inboundLock = new object();
        private readonly Queue<Packet> inbound = new Queue<Packet>();
        private readonly object outboundLock = new object();
        private readonly Queue<string> outbound = new Queue<string>();
        private volatile bool stopRequested;
        private Thread worker;
        private SerialControllerPitaco legacy;
        private SerialControllerPitaco24 modern;
        private Version activeVersion;
        private int pendingLines;
        private long droppedLines;

        public Version ConnectedVersion => activeVersion;
        public long DroppedLines => Interlocked.Read(ref droppedLines);

        // A instancia e criada ANTES dos Awake dos controladores em qualquer cena.
        // Nao e necessario editar prefabs ou duplicar gerenciadores entre cenas.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Active != null) return;
            var go = new GameObject("Pitaco Connection Manager");
            go.AddComponent<PitacoConnectionManager>();
            DontDestroyOnLoad(go);
            // One persistent USB connection and one optical pipeline across scenes.
            // The modern serial controller forwards data; it does NOT open another COM.
            go.AddComponent<SerialControllerPitaco24>();
            go.AddComponent<PpgProcessingCoordinator>();
            go.AddComponent<OximetryIntegrationHub>();
        }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }
            Active = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            BindControllers();
            worker = new Thread(ScanAndRead) { IsBackground = true, Name = "PitacoSerialManager" };
            worker.Start();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindControllers();
        }

        private void BindControllers()
        {
            var nextLegacy = FindObjectOfType<SerialControllerPitaco>();
            var nextModern = GetComponent<SerialControllerPitaco24>();
            if (nextModern == null)
                nextModern = FindObjectOfType<SerialControllerPitaco24>();

            if (legacy != nextLegacy)
            {
                legacy = nextLegacy;
                // O 2.4 tambem publica pressao numerica legada. Apenas a API
                // respiratoria e espelhada, NAO existe uma segunda conexao.
                if (legacy != null && activeVersion != Version.None)
                    legacy.ManagedConnected();
            }
            if (modern != nextModern)
            {
                modern = nextModern;
                if (modern != null && activeVersion == Version.Pitaco24)
                    modern.ManagedConnected();
            }
        }

        // Chamados exclusivamente pelo thread principal dos controladores.
        public void SendToLegacy(string command)
        {
            if (legacy != null && activeVersion != Version.None)
                EnqueueCommand(command);
        }

        public void SendToPitaco24(string command)
        {
            if (modern != null && activeVersion == Version.Pitaco24)
                EnqueueCommand(command);
        }

        private void EnqueueCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            lock (outboundLock) outbound.Enqueue(command);
        }

        private void Update()
        {
            // Objetos de cena ja sao vinculados em sceneLoaded. Verificacao
            // eventual cobre instanciacao dinamica sem buscar a cada frame.
            if (Time.frameCount % 60 == 0)
                BindControllers();
            int count = Math.Max(1, maxLinesPerFrame);
            for (int i = 0; i < count; ++i)
            {
                Packet packet;
                lock (inboundLock)
                {
                    if (inbound.Count == 0) break;
                    packet = inbound.Dequeue();
                    if (packet.Kind == PacketKind.Line) pendingLines--;
                }

                switch (packet.Kind)
                {
                    case PacketKind.Connected:
                        activeVersion = packet.DeviceVersion;
                        Debug.Log("[PITACO Manager] " + activeVersion + " identificado em " +
                                  packet.Text + " (COM exclusiva).");
                        if (legacy != null) legacy.ManagedConnected();
                        if (modern != null && activeVersion == Version.Pitaco24)
                            modern.ManagedConnected();
                        break;
                    case PacketKind.Disconnected:
                        if (modern != null) modern.ManagedDisconnected();
                        if (legacy != null) legacy.ManagedDisconnected();
                        activeVersion = Version.None;
                        Debug.LogWarning("[PITACO Manager] Conexao perdida. Redescobrindo COM...");
                        break;
                    case PacketKind.Line:
                        if (activeVersion == Version.Pitaco24 && modern != null)
                            modern.ManagedLine(packet.Text);
                        if (legacy != null &&
                            (activeVersion == Version.Pitaco20 || IsNumericPressure(packet.Text)))
                            legacy.ManagedLine(packet.Text);
                        break;
                    case PacketKind.Warning:
                        Debug.LogWarning("[PITACO Manager] " + packet.Text);
                        break;
                }
            }
        }

        private static bool IsNumericPressure(string line)
        {
            double value;
            return !string.IsNullOrEmpty(line) &&
                   double.TryParse(line.Trim(), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, out value) &&
                   !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void Push(PacketKind kind, Version version, string text)
        {
            lock (inboundLock)
            {
                if (kind == PacketKind.Line)
                {
                    if (pendingLines >= Math.Max(64, maxPendingLines))
                    {
                        Interlocked.Increment(ref droppedLines);
                        return;
                    }
                    pendingLines++;
                }
                inbound.Enqueue(new Packet { Kind = kind, DeviceVersion = version, Text = text });
            }
        }

        private void FlushWrites(SerialPort port)
        {
            while (true)
            {
                string command;
                lock (outboundLock)
                {
                    if (outbound.Count == 0) break;
                    command = outbound.Dequeue();
                }
                // Firmware interpreta caracteres individuais; nunca acrescentar LF.
                port.Write(command);
            }
        }

        private void ClearWrites()
        {
            lock (outboundLock) outbound.Clear();
        }

        private bool WaitFor(SerialPort port, string expected, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(300, timeoutMs));
            while (!stopRequested && DateTime.UtcNow < deadline)
            {
                try
                {
                    string line = port.ReadLine().Trim();
                    if (string.Equals(line, expected, StringComparison.Ordinal))
                        return true;
                }
                catch (TimeoutException) { }
            }
            return false;
        }

        // Cada fase testa UM protocolo em todas as portas; fallback so
        // acontece depois de esgotar a descoberta global do PITACO 2.4.
        private bool Identify(SerialPort port, Version version)
        {
            Thread.Sleep(Math.Max(0, arduinoOpenTimeMs));
            if (stopRequested) return false;
            port.DiscardInBuffer();
            bool is24 = version == Version.Pitaco24;
            port.Write(is24 ? "i" : "e");
            return WaitFor(port, is24 ? "echo2.4p" : "echop", handshakeTimeoutMs);
        }

        private void ScanAndRead()
        {
            bool warnedNoDevice = false;
            while (!stopRequested)
            {
                string[] ports;
                try { ports = SerialPort.GetPortNames(); }
                catch (Exception ex)
                {
                    Push(PacketKind.Warning, Version.None, "Falha ao listar COM: " + ex.Message);
                    Thread.Sleep(Math.Max(500, scanRetryMs));
                    continue;
                }

                if (!string.IsNullOrEmpty(preferredPort))
                    ports = new[] { preferredPort.Trim() };

                // Prioriza a COM fisica no exame inicial; CNCB/CNCA
                // permanecem disponiveis para compatibilidade com com0com.
                Array.Sort(ports, (a, b) =>
                {
                    bool aVirtual = a.StartsWith("CNC", StringComparison.OrdinalIgnoreCase);
                    bool bVirtual = b.StartsWith("CNC", StringComparison.OrdinalIgnoreCase);
                    if (aVirtual != bVirtual) return aVirtual ? 1 : -1;
                    return StringComparer.OrdinalIgnoreCase.Compare(a, b);
                });

                bool hadConnection = false;
                // Fase 1: todas as COMs com i/echo2.4p.
                // Fase 2: somente se nao ha 2.4, todas com e/echop.
                for (int phase = 0; phase < 2 && !stopRequested && !hadConnection; phase++)
                {
                    Version target = phase == 0 ? Version.Pitaco24 : Version.Pitaco20;
                    foreach (string portName in ports)
                    {
                    if (stopRequested) break;
                    bool connected = false;
                    Version found = target;
                    try
                    {
                        using (var port = new SerialPort(portName, baudRate))
                        {
                            port.ReadTimeout = 150;
                            port.WriteTimeout = 1000;
                            port.DtrEnable = true;
                            port.RtsEnable = true;
                            port.Handshake = Handshake.None;
                            port.NewLine = "\n";
                            port.Open();
                            if (!Identify(port, found)) continue;

                            connected = true;
                            hadConnection = true;
                            Push(PacketKind.Connected, found, portName);
                            if (found == Version.Pitaco20)
                                Push(PacketKind.Warning, found,
                                     "Fallback PITACO 2.0 em " + portName +
                                     ": nenhuma COM respondeu echo2.4p.");
                            try
                            {
                                while (!stopRequested)
                                {
                                    FlushWrites(port);
                                    try
                                    {
                                        string line = port.ReadLine();
                                        if (!string.IsNullOrWhiteSpace(line))
                                            Push(PacketKind.Line, found, line.Trim());
                                    }
                                    catch (TimeoutException) { }
                                }
                            }
                            finally
                            {
                                // Encerra a amostragem somente ao finalizar a sessao fisica.
                                try { port.Write("f"); } catch (Exception) { }
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Outra aplicacao ja possui a COM: testar a proxima.
                    }
                    catch (IOException ex)
                    {
                        if (connected)
                            Push(PacketKind.Warning, found, portName + ": " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        if (connected)
                            Push(PacketKind.Warning, found, portName + ": " + ex.Message);
                    }
                    finally
                    {
                        if (connected)
                        {
                            ClearWrites();
                            Push(PacketKind.Disconnected, found, portName);
                        }
                    }
                    if (hadConnection) break;
                    }
                }

                if (!hadConnection && !warnedNoDevice && !stopRequested)
                {
                    Push(PacketKind.Warning, Version.None,
                         "Nenhum PITACO identificado. Verifique a COM e o firmware; nova busca automatica.");
                    warnedNoDevice = true;
                }
                if (hadConnection) warnedNoDevice = false;
                if (!stopRequested)
                    Thread.Sleep(Math.Max(500, scanRetryMs));
            }
        }

        private void OnDestroy()
        {
            if (Active != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            stopRequested = true;
            if (worker != null && worker.IsAlive)
                worker.Join(2000);
            Active = null;
        }
    }
}
