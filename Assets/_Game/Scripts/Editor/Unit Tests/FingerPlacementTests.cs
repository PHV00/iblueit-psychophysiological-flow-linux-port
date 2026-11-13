using System.Reflection;
using Ibit.Core.Oximetry;
using Ibit.Core.Oximetry.Placement;
using Ibit.Core.Game;
using NUnit.Framework;
using UnityEngine;

public class FingerPlacementTests
{
    private static FingerPlacementStateMachine Start(bool required)
    {
        var state = new FingerPlacementStateMachine();
        state.Configure(1f, 3f, 6f, 2f, 2f);
        state.Begin(0f, required);
        state.SetConnected(true);
        return state;
    }

    [Test]
    public void NoFingerShowsProgressiveWarningsAtConfiguredTimes()
    {
        var state = Start(false);
        state.ObserveContact(0f, false);
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(0.5f));
        state.ObserveContact(1f, false);
        Assert.AreEqual(FingerPlacementState.Warning, state.Tick(1f));
        state.ObserveContact(3f, false);
        Assert.AreEqual(FingerPlacementState.Critical, state.Tick(3f));
        state.ObserveContact(6f, false);
        Assert.AreEqual(FingerPlacementState.Critical, state.Tick(6f));
        Assert.IsFalse(state.RecoveryReady);
    }

    [Test]
    public void RequiredMonitoringEscalatesAndRequiresTwoStableSeconds()
    {
        var state = Start(true);
        state.ObserveContact(0f, false);
        state.ObserveContact(6f, false);
        Assert.AreEqual(FingerPlacementState.PauseRequired, state.Tick(6f));
        state.ObserveContact(6.1f, true);
        Assert.AreEqual(FingerPlacementState.Recovering, state.Tick(6.1f));
        state.ObserveContact(7.1f, true);
        Assert.AreEqual(FingerPlacementState.Recovering, state.Tick(8.09f));
        state.ObserveContact(8.11f, true);
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(8.11f));
        Assert.IsTrue(state.RecoveryReady);
        // A brief new loss must immediately disable resuming, even before
        // it is long enough to raise the yellow placement warning.
        state.ObserveContact(8.2f, false);
        Assert.IsFalse(state.RecoveryReady);
        state.ObserveContact(8.3f, true);
        Assert.AreEqual(FingerPlacementState.Recovering, state.Tick(8.3f));
        state.ObserveContact(10.31f, true);
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(10.31f));
        Assert.IsTrue(state.RecoveryReady);
        state.AcknowledgeResume();
        Assert.IsFalse(state.RecoveryReady);
    }

    [Test]
    public void ContactRecoveryBeforeWarningDoesNotShowAnAlert()
    {
        var state = Start(true);
        state.ObserveContact(0f, false);
        state.Tick(0.5f);
        state.ObserveContact(0.6f, true);
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(0.6f));
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(1f));
    }

    [Test]
    public void ShortFingerRepositionResetsTheContinuousLossTimer()
    {
        var state = Start(false);
        state.ObserveContact(0f, false);
        Assert.AreEqual(FingerPlacementState.Warning, state.Tick(1.1f));
        state.ObserveContact(1.2f, true);
        Assert.AreEqual(FingerPlacementState.Recovering, state.Tick(1.3f));
        state.ObserveContact(1.4f, false);
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(1.6f));
        Assert.AreEqual(FingerPlacementState.Warning, state.Tick(2.5f));
    }

    [Test]
    public void MissingTelemetryIsNotClassifiedAsFingerAbsent()
    {
        var state = Start(true);
        state.ObserveContact(0f, true);
        Assert.AreEqual(FingerPlacementState.Normal, state.Tick(0f));
        Assert.AreEqual(FingerPlacementState.TelemetryLost, state.Tick(2.1f));
        state.ObserveContact(2.2f, true);
        Assert.AreEqual(FingerPlacementState.Recovering, state.Tick(2.2f));
    }

    [Test]
    public void PauseOwnershipDoesNotReleaseAnExistingLegacyPause()
    {
        GameManager.ReleaseFingerPlacementPause();
        GameManager.UnPauseGame();
        try
        {
            GameManager.PauseGame();
            Assert.IsTrue(GameManager.HasLegacyPauseRequest);
            Assert.IsFalse(GameManager.TryPauseForFingerPlacement());
            Assert.IsTrue(GameManager.GameIsPaused);
            GameManager.UnPauseGame();
            Assert.IsFalse(GameManager.GameIsPaused);

            Assert.IsTrue(GameManager.TryPauseForFingerPlacement());
            GameManager.PauseGame(); // Security/manual pause while placement owns it.
            GameManager.ReleaseFingerPlacementPause();
            Assert.IsTrue(GameManager.HasLegacyPauseRequest);
            Assert.IsTrue(GameManager.GameIsPaused); // Legacy still owns the pause.
            GameManager.UnPauseGame();
            Assert.IsFalse(GameManager.GameIsPaused);
        }
        finally
        {
            GameManager.ReleaseFingerPlacementPause();
            GameManager.UnPauseGame();
        }
    }


    [Test]
    public void CoordinatorPublishesContactBeforeAnEightSecondEstimateExists()
    {
        var go = new GameObject("PPG contact event test");
        try
        {
            var coordinator = go.AddComponent<PpgProcessingCoordinator>();
            var method = typeof(PpgProcessingCoordinator).GetMethod(
                "ReceiveJson", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            int events = 0;
            bool lastContact = true;
            coordinator.OnOpticalContactSample += value =>
            {
                events++;
                lastContact = value;
            };

            method.Invoke(coordinator, new object[]
            {
                "{\"type\":\"sample\",\"seq\":1,\"t_ms\":40," +
                "\"ppg\":{\"red\":50000,\"ir\":60000}," +
                "\"sqi\":{\"contact\":false}}"
            });
            Assert.AreEqual(1, events);
            Assert.IsFalse(lastContact);

            method.Invoke(coordinator, new object[]
            {
                "{\"type\":\"sample\",\"seq\":2,\"t_ms\":80," +
                "\"ppg\":{\"red\":50000,\"ir\":60000}," +
                "\"sqi\":{\"contact\":true}}"
            });
            Assert.AreEqual(2, events);
            Assert.IsTrue(lastContact);
            Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void DisconnectAndSceneEndResetTheMonitor()
    {
        var state = Start(true);
        state.ObserveContact(0f, false);
        state.ObserveContact(6f, false);
        state.Tick(6f);
        state.SetConnected(false);
        Assert.AreEqual(FingerPlacementState.DeviceDisconnected, state.Tick(6.1f));
        state.End();
        Assert.AreEqual(FingerPlacementState.Inactive, state.Tick(6.2f));
    }
}
