using System;
using Ibit.Core.Serial;
using UnityEngine;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// Diagnostic results ONLY. SpO2DemoPercent is an uncalibrated demonstration
    /// and must not be connected to patient alerts, rewards, or medical decisions.
    /// </summary>
    public sealed class PpgMeasurement
    {
        public readonly uint DeviceTimeMs;
        public readonly double SamplingHz;
        public readonly double? BpmEstimate;
        public readonly double? SpO2DemoPercent;
        public readonly double? RatioOfRatios;
        public readonly double Autocorrelation;
        public readonly string Status;

        public PpgMeasurement(uint timeMs, double samplingHz, double? bpm,
            double? spo2Demo, double? ratio, double autocorrelation, string status)
        {
            DeviceTimeMs = timeMs;
            SamplingHz = samplingHz;
            BpmEstimate = bpm;
            SpO2DemoPercent = spo2Demo;
            RatioOfRatios = ratio;
            Autocorrelation = autocorrelation;
            Status = status;
        }
    }

    /// <summary>
    /// Optional Unity component: attach in the Inspector to a GameObject in a scene
    /// that also has SerialControllerPitaco24 (or assign that controller). No changes
    /// to existing scenes, Player, HUD, DeepDDA, or USB COM ownership are required.
    /// </summary>
    public sealed class PpgProcessingCoordinator : MonoBehaviour
    {
        [Serializable]
        private sealed class PpgPayload
        {
            public int red;
            public int ir;
        }

        [Serializable]
        private sealed class SampleFrame
        {
            public string type;
            public int seq;
            public long t_ms;
            public PpgPayload ppg;
        }

        [SerializeField] private SerialControllerPitaco24 serialController;
        [SerializeField] private bool logDiagnostics = true;
        [SerializeField] private float logIntervalSeconds = 5f;

        private readonly PpgSampleBuffer buffer = new PpgSampleBuffer();
        private bool hasPreviousProcessingTime;
        private uint previousProcessingMs;
        private float nextSearchTime;
        private float lastSampleReceivedAt = -1f;
        private float nextLogTime;
        private const uint RecalculationPeriodMs = 400;

        public PpgMeasurement LastMeasurement { get; private set; }
        public event Action<PpgMeasurement> OnMeasurement;

        private void OnEnable()
        {
            ResetMeasurements("Aguardando amostras RED/IR");
            AttachIfAvailable();
        }

        private void Update()
        {
            if (serialController == null && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 1f;
                AttachIfAvailable();
            }

            if (lastSampleReceivedAt >= 0f &&
                Time.unscaledTime - lastSampleReceivedAt > 3f)
            {
                ResetMeasurements("Sem amostras PPG recentes");
            }
        }

        private void AttachIfAvailable()
        {
            if (serialController == null)
                serialController = FindObjectOfType<SerialControllerPitaco24>();
            if (serialController == null) return;

            serialController.OnJsonReceived -= ReceiveJson;
            serialController.OnSerialDisconnected -= OnDeviceDisconnected;
            serialController.OnJsonReceived += ReceiveJson;
            serialController.OnSerialDisconnected += OnDeviceDisconnected;
        }

        private void OnDisable()
        {
            if (serialController != null)
            {
                serialController.OnJsonReceived -= ReceiveJson;
                serialController.OnSerialDisconnected -= OnDeviceDisconnected;
            }
            ResetMeasurements("Processamento suspenso");
        }

        private void OnDeviceDisconnected()
        {
            ResetMeasurements("PITACO 2.4 desconectado");
        }

        private void ResetMeasurements(string status)
        {
            buffer.Clear();
            hasPreviousProcessingTime = false;
            lastSampleReceivedAt = -1f;
            LastMeasurement = new PpgMeasurement(0, 0, null, null, null, 0, status);
        }

        private void ReceiveJson(string json)
        {
            // G1 frames are intentionally ignored in this first, SQI-free prototype.
            if (string.IsNullOrEmpty(json)) return;

            SampleFrame frame;
            try { frame = JsonUtility.FromJson<SampleFrame>(json); }
            catch (ArgumentException) { return; }

            if (frame == null || frame.type != "sample" || frame.ppg == null)
                return;

            PpgSample sample;
            if (!PpgSample.TryCreate(frame.seq, frame.t_ms,
                frame.ppg.red, frame.ppg.ir, out sample))
                return;

            bool continuous = buffer.Add(sample);
            lastSampleReceivedAt = Time.unscaledTime;
            if (!continuous)
            {
                hasPreviousProcessingTime = false;
                LastMeasurement = new PpgMeasurement(sample.TimeMs, 0,
                    null, null, null, 0, "Janela reiniciada: sequencia/timestamp");
            }

            if (hasPreviousProcessingTime &&
                unchecked(sample.TimeMs - previousProcessingMs) < RecalculationPeriodMs)
                return;

            PpgProcessedWindow processed;
            string reason;
            if (!PpgSignalProcessor.TryProcess(buffer.Samples, out processed, out reason))
            {
                LastMeasurement = new PpgMeasurement(sample.TimeMs, 0,
                    null, null, null, 0, reason);
                return;
            }

            previousProcessingMs = sample.TimeMs;
            hasPreviousProcessingTime = true;
            double bpm, autocorrelation, spo2Demo, ratio;
            bool hasBpm = BpmEstimator.TryEstimate(processed, out bpm, out autocorrelation);
            bool hasSpO2 = Spo2Estimator.TryEstimateDemo(processed, out spo2Demo, out ratio);
            LastMeasurement = new PpgMeasurement(sample.TimeMs, processed.SamplingHz,
                hasBpm ? (double?)bpm : null,
                hasSpO2 ? (double?)spo2Demo : null,
                hasSpO2 ? (double?)ratio : null,
                autocorrelation, "EXPERIMENTAL - NAO CALIBRADO");
            OnMeasurement?.Invoke(LastMeasurement);

            if (logDiagnostics && Time.unscaledTime >= nextLogTime)
            {
                nextLogTime = Time.unscaledTime + Math.Max(1f, logIntervalSeconds);
                Debug.Log("[PPG C# experimental] BPM=" +
                    (hasBpm ? bpm.ToString("F1") : "--") +
                    " | SpO2 DEMO=" + (hasSpO2 ? spo2Demo.ToString("F1") : "--") +
                    " | fs=" + processed.SamplingHz.ToString("F1") +
                    " Hz | ACFr=" + autocorrelation.ToString("F2") +
                    " | NAO USAR PARA DECISOES CLINICAS");
            }
        }
    }
}
