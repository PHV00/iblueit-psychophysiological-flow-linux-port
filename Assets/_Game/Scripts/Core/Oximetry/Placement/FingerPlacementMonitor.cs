using System;
using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ibit.Core.Oximetry.Placement
{
    // Persistent supervisor; intentionally independent of BPM/SpO2 validity.
    public sealed class FingerPlacementMonitor : MonoBehaviour
    {
        [Header("Progressive UI thresholds, seconds")]
        [SerializeField] private float warningAfterSeconds = 1f;
        [SerializeField] private float criticalAfterSeconds = 3f;
        [SerializeField] private float pauseAfterSeconds = 6f;
        [SerializeField] private float recoverySeconds = 2f;
        [SerializeField] private float telemetryTimeoutSeconds = 2f;

        [Header("Optical monitoring is optional by default")]
        [SerializeField] private bool monitoringRequiredByDefault = false;

        private readonly FingerPlacementStateMachine engine =
            new FingerPlacementStateMachine();
        private PpgProcessingCoordinator coordinator;
        private bool initialized;

        public FingerPlacementState State => engine.State;
        public bool SessionActive => engine.SessionActive;
        public bool MonitoringRequired => engine.MonitoringRequired;
        public bool RecoveryReady => engine.RecoveryReady;
        public bool HasEverConnected => engine.EverConnected;

        public event Action<FingerPlacementState> OnStateChanged;
        public event Action OnPauseRequested;

        private void OnEnable()
        {
            coordinator = GetComponent<PpgProcessingCoordinator>();
            if (coordinator != null)
                coordinator.OnOpticalContactSample += ObserveContact;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            if (!initialized)
                SetupScene(SceneManager.GetActiveScene());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupScene(scene);
        }

        private void SetupScene(Scene scene)
        {
            initialized = true;
            bool gameplay = scene.name == "Plataform" ||
                scene.name == "WaterGame" || scene.name == "CakeGame";

            if (gameplay)
            {
                engine.Configure(warningAfterSeconds, criticalAfterSeconds,
                    pauseAfterSeconds, recoverySeconds, telemetryTimeoutSeconds);
                engine.Begin(Time.unscaledTime, monitoringRequiredByDefault);
                bool connected = PitacoConnectionManager.Active != null &&
                    PitacoConnectionManager.Active.ConnectedVersion ==
                        PitacoConnectionManager.Version.Pitaco24;
                engine.SetConnected(connected);
            }
            else
            {
                engine.End();
            }
            OnStateChanged?.Invoke(engine.State);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (coordinator != null)
                coordinator.OnOpticalContactSample -= ObserveContact;
            engine.End();
            OnStateChanged?.Invoke(engine.State);
        }

        private void ObserveContact(bool hasContact)
        {
            if (!engine.SessionActive) return;
            var manager = PitacoConnectionManager.Active;
            engine.SetConnected(manager != null &&
                manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco24);
            engine.ObserveContact(Time.unscaledTime, hasContact);
        }

        public void SetMonitoringRequired(bool required)
        {
            engine.SetMonitoringRequired(required);
            Debug.Log("[FingerPlacement] Monitoramento obrigatorio: " + required);
        }

        public void AcknowledgeResume()
        {
            engine.AcknowledgeResume();
        }

        private void Update()
        {
            if (!engine.SessionActive) return;

#if UNITY_EDITOR
            // Local test convenience; never shipped in a player build.
            if (Input.GetKeyDown(KeyCode.F9))
                SetMonitoringRequired(!engine.MonitoringRequired);
#endif

            var manager = PitacoConnectionManager.Active;
            engine.SetConnected(manager != null &&
                manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco24);

            var previous = engine.State;
            var current = engine.Tick(Time.unscaledTime);
            if (current == previous) return;

            Debug.Log("[FingerPlacement] " + previous + " -> " + current);
            OnStateChanged?.Invoke(current);
            if (current == FingerPlacementState.PauseRequired)
                OnPauseRequested?.Invoke();
        }
    }
}
