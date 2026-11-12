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
    /// Receives PITACO 2.4 optical JSON through the existing serial event.
    /// Bootstrapped persistently by PitacoConnectionManager (no extra COM).
    /// EXPERIMENTAL: optical contact and G1 are diagnostics only in this branch.
    /// The mathematical/continuity checks still guard candidate estimates.
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
        private sealed class SqiPayload
        {
            public bool contact;
        }

        [Serializable]
        private sealed class SampleFrame
        {
            public string type;
            public int seq;
            public long t_ms;
            public PpgPayload ppg;
            public SqiPayload sqi;
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
        private long receivedSampleFrames;
        private long jsonCallbacksReceived;
        private long nonSampleJsonFrames;
        private long invalidJsonFrames;
        private long missingContactFrames;
        private long invalidSampleFrames;
        private string lastJsonType = "nenhum";
        private const uint RecalculationPeriodMs = 400;

        public PpgMeasurement LastMeasurement { get; private set; }
        public bool OpticalContactConfirmed { get; private set; }
        // Distinguish JSON-looking frames, callbacks, recognized sample frames
        // and actual valid PPG. These counters do not change the signal path.
        public long JsonCallbacksReceived => jsonCallbacksReceived;
        public long ReceivedSampleFrames => receivedSampleFrames;
        public event Action<PpgMeasurement> OnMeasurement;
        // Independent of 8-second processing: one contact observation per
        // sample. A missing sqi object is NOT treated as proof of finger-off.
        public event Action<bool> OnOpticalContactSample;

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

            // Diagnostico de toda a cadeia: recepcao, contato, janela e
            // estimativas. Antes so havia log apos um calculo bem-sucedido.
            if (logDiagnostics && Time.unscaledTime >= nextLogTime)
            {
                nextLogTime = Time.unscaledTime + Math.Max(1f, logIntervalSeconds);
                var measurement = LastMeasurement;
                Debug.Log("[PPG C# diagnostico] JSON serial=" +
                    (serialController != null ? serialController.JsonMessagesReceived : 0) +
                    " | callbacks=" + jsonCallbacksReceived +
                    " | samples=" + receivedSampleFrames +
                    " | outroTipo=" + nonSampleJsonFrames +
                    " | JSON invalido=" + invalidJsonFrames +
                    " | semContato=" + missingContactFrames +
                    " | amostraInvalida=" + invalidSampleFrames +
                    " | ultimoTipo=" + lastJsonType +
                    " | gateContato=DESATIVADO_TESTE" +
                    " | contato=" + OpticalContactConfirmed +
                    " | buffer=" + buffer.Count +
                    " | estado=" + (measurement != null ? measurement.Status : "sem resultado") +
                    " | BPM=" + (measurement != null && measurement.BpmEstimate.HasValue
                        ? measurement.BpmEstimate.Value.ToString("F1") : "--") +
                    " | SpO2 DEMO=" + (measurement != null && measurement.SpO2DemoPercent.HasValue
                        ? measurement.SpO2DemoPercent.Value.ToString("F1") : "--"));
            }
        }

        private void AttachIfAvailable()
        {
            // The persistent manager owns the real serial controller. Prefer
            // its colocated component to avoid subscribing to a scene duplicate.
            if (serialController == null)
                serialController = GetComponent<SerialControllerPitaco24>();
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
            OpticalContactConfirmed = false;
            hasPreviousProcessingTime = false;
            lastSampleReceivedAt = -1f;
            LastMeasurement = new PpgMeasurement(0, 0, null, null, null, 0, status);
        }

        private void ReceiveJson(string json)
        {
            // G1 frames are intentionally ignored in this first, SQI-free prototype.
            if (string.IsNullOrEmpty(json)) return;
            jsonCallbacksReceived++;

            SampleFrame frame;
            try { frame = JsonUtility.FromJson<SampleFrame>(json); }
            catch (ArgumentException)
            {
                invalidJsonFrames++;
                lastJsonType = "JSON-malformado";
                return;
            }

            if (frame == null)
            {
                invalidJsonFrames++;
                lastJsonType = "nulo";
                return;
            }

            lastJsonType = string.IsNullOrEmpty(frame.type) ? "sem-type" : frame.type;
            if (frame.type != "sample" || frame.ppg == null)
            {
                nonSampleJsonFrames++;
                return;
            }

            receivedSampleFrames++;

            // EXPERIMENTAL WITHOUT CONTACT GATE: the firmware provides
            // sqi.contact, but it does NOT approve/reject the signal here.
            // Keep contact as diagnostic metadata and always continue with
            // structural and mathematical checks on RED/IR.
            // G1 is also diagnostic only, as it already was in Marco 0.5.
            bool hasContact = frame.sqi != null && frame.sqi.contact;
            OpticalContactConfirmed = hasContact;
            if (frame.sqi != null)
                OnOpticalContactSample?.Invoke(hasContact);
            if (!hasContact)
                missingContactFrames++;

            PpgSample sample;
            if (!PpgSample.TryCreate(frame.seq, frame.t_ms,
                frame.ppg.red, frame.ppg.ir, out sample))
            {
                invalidSampleFrames++;
                LastMeasurement = new PpgMeasurement(0, 0, null, null,
                    null, 0, "Amostra PPG invalida: seq/t_ms/RED/IR");
                return;
            }

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
                autocorrelation, hasContact
                    ? "CANDIDATO EXPERIMENTAL - CONTATO REPORTADO"
                    : "CANDIDATO EXPERIMENTAL - SEM CONTATO");
            OnMeasurement?.Invoke(LastMeasurement);


        }
    }
}
