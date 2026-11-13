using System.Globalization;
using Ibit.Core.Oximetry;
using Ibit.Core.Oximetry.Placement;
using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;

namespace Ibit.Core.Oximetry.UI
{
    /// <summary>
    /// Shared text presenter for the existing StatusDevices/measures UI.
    /// Reads measurement/placement state, but never changes the G1 policy,
    /// PPG buffer, serial connection or the finger-placement pause.
    /// Scene-specific OximeterMeasures scripts adapt their legacy Player only.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public abstract class OximetryHudPresenter : MonoBehaviour
    {
        private const string ConnectedPrefix = "PITACO 2.4: conectado\n";
        private Text hud;
        private PitacoConnectionManager cachedManager;
        private PpgProcessingCoordinator coordinator;
        private SerialControllerPitaco24 serial;
        private FingerPlacementMonitor placement;

        // Retain current scene Player semantics; no second publication clock.
        // Numeric values are already refreshed every 1 s by the Player.
        protected abstract string ReadPlayerText(bool pitaco24Connected);

        protected virtual void Update()
        {
            if (hud == null)
                hud = GetComponent<Text>();
            if (hud == null)
                return;

            var manager = PitacoConnectionManager.Active;
            bool isPitaco24 = manager != null &&
                manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco24;

            if (!isPitaco24)
            {
                // The old independent oximeter still owns its original HUD path.
                SetText(ReadPlayerText(false) ?? string.Empty);
                return;
            }

            if (manager != cachedManager || coordinator == null)
            {
                cachedManager = manager;
                coordinator = manager.GetComponent<PpgProcessingCoordinator>();
                serial = manager.GetComponent<SerialControllerPitaco24>();
                placement = manager.GetComponent<FingerPlacementMonitor>();
            }

            if (coordinator == null)
            {
                SetText(ConnectedPrefix + "PPG: processamento ausente");
                return;
            }

            if (coordinator.JsonCallbacksReceived == 0)
            {
                SetText(ConnectedPrefix + (serial != null && serial.JsonMessagesReceived > 0
                    ? "PPG: JSON nao encaminhado"
                    : "PPG: aguardando JSON"));
                return;
            }

            if (coordinator.ReceivedSampleFrames == 0)
            {
                SetText(ConnectedPrefix + "PPG: JSON sem amostras RED/IR");
                return;
            }

            // The coordinator alone authorizes estimates. PlacementState only
            // selects an informative message when that coordinator has no contact.
            if (!coordinator.OpticalContactConfirmed)
            {
                SetText(FormatNoContactStatus(placement != null && placement.SessionActive
                    ? placement.State
                    : FingerPlacementState.WaitingForData));
                return;
            }

            string playerText = ReadPlayerText(true);
            SetText(playerText ?? ConnectedPrefix + "PPG: aguardando Player");
        }

        private void SetText(string value)
        {
            if (hud.text != value)
                hud.text = value;
        }

        public static string FormatNoContactStatus(FingerPlacementState state)
        {
            switch (state)
            {
                case FingerPlacementState.Warning:
                case FingerPlacementState.Critical:
                case FingerPlacementState.PauseRequired:
                    return ConnectedPrefix + "PPG: reposicione o dedo";
                case FingerPlacementState.Recovering:
                    return ConnectedPrefix + "PPG: estabilizando contato";
                case FingerPlacementState.TelemetryLost:
                    return ConnectedPrefix + "PPG: telemetria ausente";
                case FingerPlacementState.DeviceDisconnected:
                    return ConnectedPrefix + "PPG: aguardando reconexao";
                default:
                    return ConnectedPrefix + "PPG: sem contato optico";
            }
        }

        public static string FormatPitaco24Values(double? bpm, double? spo2Demo)
        {
            string hr = bpm.HasValue
                ? bpm.Value.ToString("F1", CultureInfo.InvariantCulture) : "--";
            string spo2 = spo2Demo.HasValue
                ? spo2Demo.Value.ToString("F1", CultureInfo.InvariantCulture) : "--";
            return $"HR [2.4]: {hr} bpm\nSpO2 DEMO: {spo2} %";
        }
    }
}
