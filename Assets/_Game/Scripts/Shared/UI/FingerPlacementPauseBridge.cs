using System;
using Ibit.Core.Game;
using UnityEngine;

namespace Ibit.Core.Oximetry.Placement
{
    // Only controls a pause token owned by this feature; never releases
    // manual, security-panel, or other gameplay pauses.
    public sealed class FingerPlacementPauseBridge : MonoBehaviour
    {
        private FingerPlacementMonitor monitor;
        private bool ownsPause;

        public bool OwnsPause => ownsPause;
        public bool CanResume => ownsPause && monitor != null &&
            monitor.RecoveryReady;

        public event Action OnPauseOwnershipChanged;

        private void OnEnable()
        {
            monitor = GetComponent<FingerPlacementMonitor>();
            if (monitor == null) return;
            monitor.OnPauseRequested += TryAcquirePause;
            monitor.OnStateChanged += OnPlacementStateChanged;
        }

        private void OnDisable()
        {
            if (monitor != null)
            {
                monitor.OnPauseRequested -= TryAcquirePause;
                monitor.OnStateChanged -= OnPlacementStateChanged;
            }
            ReleaseOwnPause();
        }

        private void Update()
        {
            if (monitor == null) return;

            if (!monitor.SessionActive || !monitor.MonitoringRequired)
            {
                // Turning the optional feature off cannot leave the game stuck.
                ReleaseOwnPause();
                return;
            }

            // If a legacy security/manual pause preempted this request, do NOT
            // steal it. Only try again if that pause has ended and contact is
            // still absent past the configured pause threshold.
            if (!ownsPause && monitor.State == FingerPlacementState.PauseRequired &&
                !GameManager.GameIsPaused)
                TryAcquirePause();
        }

        private void OnPlacementStateChanged(FingerPlacementState next)
        {
            if (next == FingerPlacementState.Inactive)
                ReleaseOwnPause();
            else if (next == FingerPlacementState.PauseRequired)
                TryAcquirePause();
        }

        private void TryAcquirePause()
        {
            if (ownsPause || monitor == null || !monitor.SessionActive ||
                !monitor.MonitoringRequired ||
                monitor.State != FingerPlacementState.PauseRequired)
                return;

            ownsPause = GameManager.TryPauseForFingerPlacement();
            if (ownsPause)
            {
                Debug.Log("[FingerPlacement] Pausa orientativa adquirida.");
                OnPauseOwnershipChanged?.Invoke();
            }
        }

        public void TryResume()
        {
            if (!CanResume) return; // Require a stable 2-second contact interval.
            monitor.AcknowledgeResume();
            ReleaseOwnPause();
        }

        private void ReleaseOwnPause()
        {
            if (!ownsPause) return;
            ownsPause = false;
            GameManager.ReleaseFingerPlacementPause();
            Debug.Log("[FingerPlacement] Pausa orientativa liberada.");
            OnPauseOwnershipChanged?.Invoke();
        }
    }
}
