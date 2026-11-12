using Ibit.Core.Oximetry.Placement;
using Ibit.Core.Game;
using NUnit.Framework;

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
            Assert.IsFalse(GameManager.TryPauseForFingerPlacement());
            Assert.IsTrue(GameManager.GameIsPaused);
            GameManager.UnPauseGame();
            Assert.IsFalse(GameManager.GameIsPaused);

            Assert.IsTrue(GameManager.TryPauseForFingerPlacement());
            GameManager.PauseGame(); // Security/manual pause while placement owns it.
            GameManager.ReleaseFingerPlacementPause();
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
