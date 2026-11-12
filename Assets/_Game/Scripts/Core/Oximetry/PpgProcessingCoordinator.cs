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
    /// Optical contact and the latest fresh firmware G1 PASS are both required
    /// before publishing experimental estimates. G1 runs every 5 seconds;
    /// this coordinator never reimplements its signal-quality calculations.
    /// The placement monitor keeps receiving every optical-contact observation.
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
            // Frame-specific G1 reason is useful for detecting firmware resets.
            // Do not deserialize sample.sqi.g1 here: it can be JSON null.
            public int reason;
        }

        // Only type="g1" frames contain a non-null boolean sqi.g1.
        // A separate type avoids treating sample.sqi.g1=null as false.
        [Serializable]
        private sealed class Gate01SqiPayload
        {
            public bool g1;
            public int reason;
            public int samples;
        }

        [Serializable]
        private sealed class Gate01Frame
        {
            public string type;
            public long t_ms;
            public Gate01SqiPayload sqi;
        }

        private enum Gate01State
        {
            Waiting,
            Passed,
            Failed,
            Stale
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
        // 5-second G1 cadence plus 1.5 s of delivery tolerance.
        // Experimental parameter; reevaluate with real hardware logs.
        private const uint Gate01MaxAgeMs = 6500;
        private const float Gate01MaxAgeSeconds = 6.5f;
        private const int Gate01MinimumSamples = 50;

        private Gate01State gate01State = Gate01State.Waiting;
        private bool gate01Received;
        private uint lastGate01TimeMs;
        private float lastGate01ArrivalAt = -1f;
        private int lastGate01Reason = -1;
        private int lastGate01Samples;
        private long gate01Frames;
        private long rejectedGate01Frames;
        private long blockedByGate01;

        public string Gate01Status => gate01State.ToString();
        public int LastGate01Reason => lastGate01Reason;
        public int LastGate01Samples => lastGate01Samples;
        public bool Gate01Approved => gate01State == Gate01State.Passed &&
            gate01Received && lastGate01ArrivalAt >= 0f &&
            Time.unscaledTime - lastGate01ArrivalAt <= Gate01MaxAgeSeconds;

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

            if (gate01State == Gate01State.Passed &&
                lastGate01ArrivalAt >= 0f &&
                Time.unscaledTime - lastGate01ArrivalAt > Gate01MaxAgeSeconds)
                MarkGate01Stale();

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
                    " | gateContato=ATIVADO" +
                    " | G1=" + gate01State +
                    " | G1_reason=" + lastGate01Reason +
                    " | G1_samples=" + lastGate01Samples +
                    " | G1_frames=" + gate01Frames +
                    " | G1_invalidos=" + rejectedGate01Frames +
                    " | G1_amostras_bloqueadas=" + blockedByGate01 +
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

        private void InvalidatePpgWindow(string status)
        {
            buffer.Clear();
            hasPreviousProcessingTime = false;
            LastMeasurement = new PpgMeasurement(0, 0, null, null, null, 0, status);
        }

        private void ResetGate01()
        {
            gate01State = Gate01State.Waiting;
            gate01Received = false;
            lastGate01TimeMs = 0;
            lastGate01ArrivalAt = -1f;
            lastGate01Reason = -1;
            lastGate01Samples = 0;
        }

        private void ResetMeasurements(string status)
        {
            InvalidatePpgWindow(status);
            OpticalContactConfirmed = false;
            lastSampleReceivedAt = -1f;
            ResetGate01();
        }

        private void MarkGate01Stale()
        {
            if (gate01State != Gate01State.Passed) return;
            gate01State = Gate01State.Stale;
            // Suspend publication but retain collected samples until next G1
            // decision; no stale measurement may remain visible in the HUD.
            hasPreviousProcessingTime = false;
            LastMeasurement = new PpgMeasurement(0, 0, null, null, null, 0,
                "G1 expirado: aguardando nova avaliacao");
        }

        private void ReceiveGate01(string json)
        {
            Gate01Frame result;
            try { result = JsonUtility.FromJson<Gate01Frame>(json); }
            catch (ArgumentException)
            {
                rejectedGate01Frames++;
                return;
            }
            if (result == null || result.sqi == null || result.t_ms < 0 ||
                result.t_ms > uint.MaxValue)
            {
                rejectedGate01Frames++;
                return;
            }

            uint deviceMs = (uint)result.t_ms;
            // Compare modulo 2^32 to handle millis() wrap-around without
            // accepting replayed/out-of-order results from this device session.
            if (gate01Received &&
                unchecked((int)(deviceMs - lastGate01TimeMs)) <= 0)
            {
                rejectedGate01Frames++;
                return;
            }

            gate01Received = true;
            lastGate01TimeMs = deviceMs;
            lastGate01ArrivalAt = Time.unscaledTime;
            lastGate01Reason = result.sqi.reason;
            lastGate01Samples = result.sqi.samples;
            gate01Frames++;

            bool approved = result.sqi.g1 && result.sqi.reason == 0 &&
                result.sqi.samples >= Gate01MinimumSamples;
            gate01State = approved ? Gate01State.Passed : Gate01State.Failed;
            if (!approved)
            {
                // A failed 5-second period must never leave the previous
                // 8-second result on the player's HUD.
                InvalidatePpgWindow("G1 reprovado: reason=" + lastGate01Reason);
            }
        }

        private bool IsGate01ApprovedForSample(uint sampleTimeMs)
        {
            if (gate01State != Gate01State.Passed || !gate01Received)
                return false;
            if (lastGate01ArrivalAt < 0f ||
                Time.unscaledTime - lastGate01ArrivalAt > Gate01MaxAgeSeconds ||
                unchecked(sampleTimeMs - lastGate01TimeMs) > Gate01MaxAgeMs)
            {
                MarkGate01Stale();
                return false;
            }
            return true;
        }

        private void ReceiveJson(string json)
        {
            // Handle firmware G1 window events and optical sample frames
            // independently; no second USB stream or software G1 calculation.
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
            if (frame.type == "g1")
            {
                ReceiveGate01(json);
                return;
            }
            if (frame.type != "sample" || frame.ppg == null)
            {
                nonSampleJsonFrames++;
                return;
            }

            receivedSampleFrames++;

            // Restore Marco 0.5 optical contact gate. This is NOT G1 SQI
            // approval and cannot validate physiological quality.
            // Emit the contact observation first so FingerPlacementMonitor
            // continues progressing even when measurements are suppressed.
            bool hasContact = frame.sqi != null && frame.sqi.contact;
            if (frame.sqi != null)
                OnOpticalContactSample?.Invoke(hasContact);
            if (!hasContact)
            {
                missingContactFrames++;
                if (OpticalContactConfirmed || LastMeasurement == null ||
                    LastMeasurement.Status != "Sem contato optico")
                    ResetMeasurements("Sem contato optico");
                return;
            }
            OpticalContactConfirmed = true;

            // Firmware resets the current G1 evaluation to reason=1 on a
            // new sampling session. Revoke an older PASS even if USB stayed
            // connected. Never treat the null per-sample g1 as a boolean.
            if (gate01State == Gate01State.Passed &&
                frame.sqi != null && frame.sqi.reason != 0)
            {
                ResetGate01();
                InvalidatePpgWindow("G1 reiniciado/invalidado pelo firmware");
            }

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

            if (!IsGate01ApprovedForSample(sample.TimeMs))
            {
                blockedByGate01++;
                string gateBlockReason = gate01State == Gate01State.Waiting
                    ? "Aguardando aprovacao G1"
                    : gate01State == Gate01State.Failed
                        ? "G1 reprovado: reason=" + lastGate01Reason
                        : "G1 expirado: aguardando nova avaliacao";
                LastMeasurement = new PpgMeasurement(sample.TimeMs, 0,
                    null, null, null, 0, gateBlockReason);
                return;
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


        }
    }
}
