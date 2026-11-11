using System;
using Ibit.Core.Database;
using Ibit.Core.Serial;
using UnityEngine;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// Immutable, source-attributed experimental estimate. Reserved for
    /// display/research, not a clinical measurement or a safety decision.
    /// </summary>
    public sealed class OximetryReading
    {
        public const string Pitaco24Source = "PITACO24_MAX30105";
        public const string Algorithm = "ppg-csharp-mvp-rms-demo";

        public readonly string SourceId;
        public readonly string AlgorithmVersion;
        public readonly DateTime ReceivedAtUtc;
        public readonly uint DeviceTimeMs;
        public readonly double? Bpm;
        public readonly double? SpO2DemoPercent;
        public readonly double? RatioOfRatios;
        public readonly double SamplingHz;
        public readonly double Autocorrelation;
        public readonly string Status;

        public bool HasMeasurement { get { return Bpm.HasValue || SpO2DemoPercent.HasValue; } }

        public OximetryReading(uint deviceTimeMs, DateTime receivedAtUtc, double? bpm,
            double? spo2DemoPercent, double? ratio, double samplingHz,
            double autocorrelation, string status)
        {
            SourceId = Pitaco24Source;
            AlgorithmVersion = Algorithm;
            ReceivedAtUtc = receivedAtUtc;
            DeviceTimeMs = deviceTimeMs;
            Bpm = bpm;
            SpO2DemoPercent = spo2DemoPercent;
            RatioOfRatios = ratio;
            SamplingHz = samplingHz;
            Autocorrelation = autocorrelation;
            Status = status ?? "Indisponivel";
        }

        public static OximetryReading FromEstimate(PpgMeasurement estimate)
        {
            if (estimate == null) return null;
            return new OximetryReading(estimate.DeviceTimeMs, DateTime.UtcNow,
                estimate.BpmEstimate, estimate.SpO2DemoPercent, estimate.RatioOfRatios,
                estimate.SamplingHz, estimate.Autocorrelation, estimate.Status);
        }
    }

    /// <summary>
    /// Single central source for future HUD, Mixer and persistence consumers.
    /// Phase 1 only publishes to HUD; no connection to Player static fields,
    /// patient warnings, rewards, ML-Agents or database.
    /// </summary>
    public sealed class OximetryIntegrationHub : MonoBehaviour
    {
        public static OximetryIntegrationHub Instance { get; private set; }

        [SerializeField] private SerialControllerPitaco24 serialController;
        [SerializeField] private PpgProcessingCoordinator processor;

        private float nextSearchTime;
        private float nextDisplayTime;
        private float displayIntervalSeconds = 6f;
        private PpgProcessingCoordinator subscribedProcessor;

        // Fast stream of calculated estimates for future research persistence.
        public event Action<OximetryReading> OnEstimate;
        // O;10 publication for the current game's visual presentation.
        public event Action<OximetryReading> OnDisplayReading;

        public OximetryReading LatestEstimate { get; private set; }
        public OximetryReading DisplayReading { get; private set; }
        public float DisplayIntervalSeconds { get { return displayIntervalSeconds; } }

        public bool IsPitaco24Connected
        {
            get { return serialController != null && serialController.IsConnected; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            int readingsPerMinute = SignalTreatmentDb.LoadSignalParameters("O");
            displayIntervalSeconds = readingsPerMinute > 0
                ? 60f / readingsPerMinute : 6f;
        }

        private void OnEnable()
        {
            BindSources();
        }

        private void BindSources()
        {
            if (serialController == null)
                serialController = GetComponent<SerialControllerPitaco24>();
            if (serialController == null)
                serialController = FindObjectOfType<SerialControllerPitaco24>();
            if (processor == null)
                processor = GetComponent<PpgProcessingCoordinator>();
            if (processor == null)
                processor = FindObjectOfType<PpgProcessingCoordinator>();

            if (subscribedProcessor == processor) return;
            if (subscribedProcessor != null)
                subscribedProcessor.OnMeasurement -= ReceiveEstimate;
            subscribedProcessor = processor;
            if (subscribedProcessor != null)
                subscribedProcessor.OnMeasurement += ReceiveEstimate;
        }

        private void ReceiveEstimate(PpgMeasurement measurement)
        {
            if (!IsPitaco24Connected || processor == null ||
                !processor.OpticalContactConfirmed)
                return;

            LatestEstimate = OximetryReading.FromEstimate(measurement);
            OnEstimate?.Invoke(LatestEstimate);
        }

        private void Update()
        {
            if ((serialController == null || processor == null) &&
                Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 1f;
                BindSources();
            }

            if (!IsPitaco24Connected)
            {
                LatestEstimate = null;
                Invalidate("PITACO 2.4 desconectado");
                return;
            }

            if (processor == null || !processor.OpticalContactConfirmed)
            {
                LatestEstimate = null;
                Invalidate(processor != null && processor.LastMeasurement != null
                    ? processor.LastMeasurement.Status : "Aguardando contato");
                return;
            }

            var estimate = processor.LastMeasurement;
            if (estimate == null ||
                (!estimate.BpmEstimate.HasValue && !estimate.SpO2DemoPercent.HasValue))
            {
                Invalidate(estimate != null ? estimate.Status : "Aguardando janela");
                return;
            }

            // Missing channels must disappear immediately, not after the next
            // 6-second numeric publication, to avoid showing stale values.
            if (DisplayReading != null && DisplayReading.HasMeasurement &&
                ((DisplayReading.Bpm.HasValue && !estimate.BpmEstimate.HasValue) ||
                 (DisplayReading.SpO2DemoPercent.HasValue &&
                  !estimate.SpO2DemoPercent.HasValue)))
            {
                DisplayReading = new OximetryReading(estimate.DeviceTimeMs,
                    DateTime.UtcNow,
                    estimate.BpmEstimate.HasValue ? DisplayReading.Bpm : null,
                    estimate.SpO2DemoPercent.HasValue
                        ? DisplayReading.SpO2DemoPercent : null,
                    estimate.RatioOfRatios, estimate.SamplingHz,
                    estimate.Autocorrelation, "Estimativa parcial");
                OnDisplayReading?.Invoke(DisplayReading);
            }

            if (DisplayReading == null || !DisplayReading.HasMeasurement ||
                Time.unscaledTime >= nextDisplayTime)
            {
                DisplayReading = OximetryReading.FromEstimate(estimate);
                nextDisplayTime = Time.unscaledTime + displayIntervalSeconds;
                OnDisplayReading?.Invoke(DisplayReading);
            }
        }

        private void Invalidate(string status)
        {
            nextDisplayTime = 0f;
            if (DisplayReading != null && !DisplayReading.HasMeasurement &&
                DisplayReading.Status == status)
                return;

            DisplayReading = new OximetryReading(0, DateTime.UtcNow,
                null, null, null, 0, 0, status);
            OnDisplayReading?.Invoke(DisplayReading);
        }

        private void OnDisable()
        {
            if (subscribedProcessor != null)
                subscribedProcessor.OnMeasurement -= ReceiveEstimate;
            subscribedProcessor = null;
            LatestEstimate = null;
            DisplayReading = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
