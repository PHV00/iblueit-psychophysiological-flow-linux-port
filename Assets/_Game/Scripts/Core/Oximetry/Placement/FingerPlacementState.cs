using System;

namespace Ibit.Core.Oximetry.Placement
{
    // These states describe sensor positioning/telemetry, never clinical safety.
    public enum FingerPlacementState
    {
        Inactive,
        WaitingForData,
        Normal,
        Warning,
        Critical,
        PauseRequired,
        Recovering,
        TelemetryLost,
        DeviceDisconnected
    }

    // Pure state engine: deterministic, testable without the Unity player or sensor.
    public sealed class FingerPlacementStateMachine
    {
        private float warningSeconds = 1f;
        private float criticalSeconds = 3f;
        private float pauseSeconds = 6f;
        private float recoverySeconds = 2f;
        private float timeoutSeconds = 2f;

        private bool active;
        private bool connected;
        private bool received;
        private bool contact;
        private bool recoveryRequired;
        private bool pauseWasRequired;
        private float absentSince = -1f;
        private float restoredSince = -1f;
        private float lastSampleAt = -1f;

        public FingerPlacementState State { get; private set; } = FingerPlacementState.Inactive;
        public bool MonitoringRequired { get; private set; }
        public bool SessionActive => active;
        public bool HasContact => connected && received && contact;
        public bool EverConnected { get; private set; }
        public bool RecoveryReady => pauseWasRequired && State == FingerPlacementState.Normal;

        public void Configure(float warning, float critical, float pause,
            float recovery, float timeout)
        {
            if (warning <= 0 || critical <= warning || pause <= critical ||
                recovery <= 0 || timeout <= 0)
                throw new ArgumentException("Invalid finger placement timing configuration");
            warningSeconds = warning;
            criticalSeconds = critical;
            pauseSeconds = pause;
            recoverySeconds = recovery;
            timeoutSeconds = timeout;
        }

        public void Begin(float now, bool required)
        {
            active = true;
            MonitoringRequired = required;
            connected = false;
            received = false;
            contact = false;
            recoveryRequired = false;
            pauseWasRequired = false;
            EverConnected = false;
            absentSince = restoredSince = lastSampleAt = -1f;
            State = FingerPlacementState.WaitingForData;
        }

        public void End()
        {
            active = false;
            connected = received = contact = false;
            recoveryRequired = pauseWasRequired = false;
            State = FingerPlacementState.Inactive;
        }

        public void SetMonitoringRequired(bool required)
        {
            MonitoringRequired = required;
        }

        public void SetConnected(bool value)
        {
            if (!active || connected == value) return;
            connected = value;
            if (value)
            {
                EverConnected = true;
            }
            else
            {
                received = contact = false;
                absentSince = restoredSince = lastSampleAt = -1f;
                recoveryRequired = true; // Require a new stable contact after reconnection.
            }
        }

        public void ObserveContact(float now, bool present)
        {
            if (!active || !connected) return;

            if (!received || contact != present)
            {
                if (present)
                {
                    restoredSince = now;
                    absentSince = -1f;
                    recoveryRequired |= pauseWasRequired ||
                        State == FingerPlacementState.Warning ||
                        State == FingerPlacementState.Critical ||
                        State == FingerPlacementState.PauseRequired ||
                        State == FingerPlacementState.TelemetryLost;
                }
                else
                {
                    absentSince = now;
                    restoredSince = -1f;
                }
            }

            received = true;
            contact = present;
            lastSampleAt = now;
        }

        public FingerPlacementState Tick(float now)
        {
            if (!active)
                return State = FingerPlacementState.Inactive;
            if (!connected)
                return State = FingerPlacementState.DeviceDisconnected;
            if (!received)
                return State == FingerPlacementState.TelemetryLost
                    ? State : (State = FingerPlacementState.WaitingForData);
            if (now - lastSampleAt > timeoutSeconds)
            {
                // A missing JSON stream is not proof the user removed the finger.
                // Restart loss counting on the first sample after telemetry resumes.
                contact = false;
                received = false;
                absentSince = restoredSince = -1f;
                recoveryRequired = true;
                return State = FingerPlacementState.TelemetryLost;
            }

            if (contact)
            {
                if (recoveryRequired && now - restoredSince < recoverySeconds)
                    return State = FingerPlacementState.Recovering;
                recoveryRequired = false;
                return State = FingerPlacementState.Normal;
            }

            float absentFor = now - absentSince;
            if (absentFor < warningSeconds)
                return State = FingerPlacementState.Normal;
            if (absentFor < criticalSeconds)
                return State = FingerPlacementState.Warning;
            if (!MonitoringRequired || absentFor < pauseSeconds)
                return State = FingerPlacementState.Critical;

            pauseWasRequired = true;
            return State = FingerPlacementState.PauseRequired;
        }

        // Called only after a user acknowledges the placement pause.
        public void AcknowledgeResume()
        {
            if (RecoveryReady)
                pauseWasRequired = false;
        }
    }
}
