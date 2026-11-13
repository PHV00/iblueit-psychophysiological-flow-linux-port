using System;
using System.Collections.Generic;
using System.Reflection;
using Ibit.Core.Oximetry;
using Ibit.Plataform;
using NUnit.Framework;
using UnityEngine;

public class PpgProcessingTests
{
    private static List<PpgSample> MakeWave(double bpm, bool fifoBatched = false)
    {
        var data = new List<PpgSample>();
        for (int i = 0; i < 210; i++)
        {
            double t = i * 0.040;
            double wave = Math.Sin(2.0 * Math.PI * bpm * t / 60.0);
            int red = (int)Math.Round(60000.0 + 500.0 * wave);
            int ir = (int)Math.Round(80000.0 + 1000.0 * wave);
            uint timeMs = fifoBatched ? (uint)((i / 4) * 160) : (uint)(i * 40);
            data.Add(new PpgSample((ushort)i, timeMs, red, ir));
        }
        return data;
    }

    [Test]
    public void KnownSynthetic75BpmIsRecoveredAndRatioHasExpectedDemo()
    {
        var source = MakeWave(75);
        PpgProcessedWindow processed;
        string reason;
        Assert.IsTrue(PpgSignalProcessor.TryProcess(source, out processed, out reason), reason);
        double bpm, correlation;
        Assert.IsTrue(BpmEstimator.TryEstimate(processed, out bpm, out correlation));
        Assert.AreEqual(75.0, bpm, 2.0);
        Assert.Greater(correlation, 0.57);

        double spo2Demo, ratio;
        Assert.IsTrue(Spo2Estimator.TryEstimateDemo(processed, out spo2Demo, out ratio));
        Assert.AreEqual(2.0 / 3.0, ratio, 0.04);
        Assert.That(spo2Demo, Is.InRange(94.0, 96.5));
    }

    [Test]
    public void RepeatedFifoReadTimestampsDoNotInvalidateSequentialSamples()
    {
        PpgProcessedWindow processed;
        string reason;
        Assert.IsTrue(PpgSignalProcessor.TryProcess(MakeWave(75, true),
            out processed, out reason), reason);
        double bpm, correlation;
        Assert.IsTrue(BpmEstimator.TryEstimate(processed, out bpm, out correlation));
        Assert.AreEqual(75.0, bpm, 3.0);
    }

    [Test]
    public void ConstantOpticalSignalProducesNeitherBpmNorDemoSpo2()
    {
        var source = new List<PpgSample>();
        for (int i = 0; i < 210; i++)
            source.Add(new PpgSample((ushort)i, (uint)(i * 40), 60000, 80000));

        PpgProcessedWindow processed;
        string reason;
        Assert.IsTrue(PpgSignalProcessor.TryProcess(source, out processed, out reason), reason);
        double bpm, correlation, spo2Demo, ratio;
        Assert.IsFalse(BpmEstimator.TryEstimate(processed, out bpm, out correlation));
        Assert.IsFalse(Spo2Estimator.TryEstimateDemo(processed, out spo2Demo, out ratio));
    }

    [Test]
    public void BeforeEightSecondsNoEstimateIsProduced()
    {
        var source = MakeWave(75);
        // 200 samples at 25 Hz span only 7960 ms: not a full 8-second window.
        source.RemoveRange(200, source.Count - 200);
        PpgProcessedWindow processed;
        string reason;
        Assert.IsFalse(PpgSignalProcessor.TryProcess(source, out processed, out reason));
        Assert.IsNull(processed);
    }

    [Test]
    public void AtEightSecondsEstimationBecomesAvailable()
    {
        var source = MakeWave(75);
        source.RemoveRange(201, source.Count - 201); // 201 samples, 0..8000 ms.
        PpgProcessedWindow processed;
        string reason;
        Assert.IsTrue(PpgSignalProcessor.TryProcess(source, out processed, out reason), reason);
        double bpm, correlation;
        Assert.IsTrue(BpmEstimator.TryEstimate(processed, out bpm, out correlation));
        Assert.AreEqual(75.0, bpm, 3.0);
    }

    [Test]
    public void IrregularFortyOneMsReadsStillPermitFullEightSecondWindow()
    {
        var buffer = new PpgSampleBuffer();
        // An exact 8000-ms timestamp is never present at a 41-ms cadence.
        // The buffer must retain enough margin for the full window to exist.
        for (int i = 0; i < 210; i++)
        {
            double phase = 2.0 * Math.PI * 75.0 * (i * 0.041) / 60.0;
            int ir = (int)Math.Round(80000.0 + 1000.0 * Math.Sin(phase));
            buffer.Add(new PpgSample((ushort)i, (uint)(i * 41), 60000, ir));
        }
        PpgProcessedWindow processed;
        string reason;
        Assert.IsTrue(PpgSignalProcessor.TryProcess(buffer.Samples, out processed, out reason),
            reason);
        Assert.GreaterOrEqual(
            unchecked(buffer.Samples[buffer.Count - 1].TimeMs - buffer.Samples[0].TimeMs),
            PpgSampleBuffer.WindowMs);
    }

    [Test]
    public void PlayerFormatsBothOpticalValuesAsExperimental()
    {
        var text = Player.FormatPitaco24Oximetry(75.1, 95.0);
        StringAssert.Contains("HR [2.4]: 75.1 bpm", text);
        StringAssert.Contains("SpO2 DEMO: 95.0 %", text);
        StringAssert.DoesNotContain("75.1,95.0", text);
    }

    [Test]
    public void PlayerFormatsMissingOpticalValuesAsDashes()
    {
        var text = Player.FormatPitaco24Oximetry(null, null);
        StringAssert.Contains("HR [2.4]: -- bpm", text);
        StringAssert.Contains("SpO2 DEMO: -- %", text);
    }

    [Test]
    public void SharedPresenterFormatsPitaco24NumbersWithoutChangingLegacyContract()
    {
        var text = Ibit.Core.Oximetry.UI.OximetryHudPresenter.FormatPitaco24Values(
            75.1, null);
        StringAssert.Contains("HR [2.4]: 75.1 bpm", text);
        StringAssert.Contains("SpO2 DEMO: -- %", text);
        Assert.AreEqual(Ibit.Plataform.Player.FormatPitaco24Oximetry(75.1, null),
            text);
    }

    [Test]
    public void SharedPresenterUsesPlacementStateOnlyForInformativeNoContactText()
    {
        string waiting = Ibit.Core.Oximetry.UI.OximetryHudPresenter
            .FormatNoContactStatus(Ibit.Core.Oximetry.Placement.FingerPlacementState.WaitingForData);
        string warning = Ibit.Core.Oximetry.UI.OximetryHudPresenter
            .FormatNoContactStatus(Ibit.Core.Oximetry.Placement.FingerPlacementState.Warning);
        string lost = Ibit.Core.Oximetry.UI.OximetryHudPresenter
            .FormatNoContactStatus(Ibit.Core.Oximetry.Placement.FingerPlacementState.TelemetryLost);
        StringAssert.Contains("sem contato optico", waiting);
        StringAssert.Contains("reposicione o dedo", warning);
        StringAssert.Contains("telemetria ausente", lost);
    }

    [Test]
    public void PlayerPreservesBpmWhenDemoSpo2Unavailable()
    {
        var text = Player.FormatPitaco24Oximetry(75, null);
        StringAssert.Contains("75.0 bpm", text);
        StringAssert.Contains("SpO2 DEMO: -- %", text);
    }

    [Test]
    public void AllThreePlayerTypesExposeLegacyOximetryHudContract()
    {
        Assert.IsNotNull(typeof(Ibit.Plataform.Player).GetProperty("OximetryHudText"));
        Assert.IsNotNull(typeof(Ibit.WaterGame.Player).GetProperty("OximetryHudText"));
        Assert.IsNotNull(typeof(Ibit.CakeGame.Player).GetProperty("OximetryHudText"));
    }

    [Test]
    public void NoContactSuppressesSyntheticBpmAndSpo2UntilEightSecondsOfContact()
    {
        var gameObject = new GameObject("PPG restored contact gate test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            var receive = typeof(PpgProcessingCoordinator).GetMethod(
                "ReceiveJson", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(receive);
            int contactEvents = 0;
            coordinator.OnOpticalContactSample += value => contactEvents++;
            // 8.4 s of perfectly periodic optical samples, but NO finger.
            for (int i = 0; i < 210; i++)
            {
                double phase = 2.0 * Math.PI * 75.0 * i * 0.040 / 60.0;
                int red = (int)Math.Round(60000 + 500 * Math.Sin(phase));
                int ir = (int)Math.Round(80000 + 1000 * Math.Sin(phase));
                string json = "{\"type\":\"sample\",\"seq\":" + i +
                    ",\"t_ms\":" + (i * 40) +
                    ",\"ppg\":{\"red\":" + red + ",\"ir\":" + ir +
                    "},\"sqi\":{\"contact\":false}}";
                receive.Invoke(coordinator, new object[] { json });
            }
            Assert.AreEqual(210, contactEvents, "Placement monitor must still receive events.");
            Assert.IsFalse(coordinator.OpticalContactConfirmed);
            Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            Assert.AreEqual("Sem contato optico", coordinator.LastMeasurement.Status);

            // A fresh, uninterrupted 8-second contact window is required.
            for (int i = 210; i < 420; i++)
            {
                double phase = 2.0 * Math.PI * 75.0 * i * 0.040 / 60.0;
                int red = (int)Math.Round(60000 + 500 * Math.Sin(phase));
                int ir = (int)Math.Round(80000 + 1000 * Math.Sin(phase));
                string json = "{\"type\":\"sample\",\"seq\":" + i +
                    ",\"t_ms\":" + (i * 40) +
                    ",\"ppg\":{\"red\":" + red + ",\"ir\":" + ir +
                    "},\"sqi\":{\"contact\":true}}";
                receive.Invoke(coordinator, new object[] { json });
                // New continuous-contact period after the initial no-finger
                // interval: firmware G1 first evaluates this window at 13.4 s.
                if (i == 335)
                    ReceiveG1TestJson(coordinator,
                        MakeGate01TestJson(13400, true, 0));
            }
            Assert.IsTrue(coordinator.OpticalContactConfirmed);
            Assert.AreEqual("Passed", coordinator.Gate01Status);
            Assert.IsTrue(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsTrue(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);

            // The first two contact failures quarantine the raw samples:
            // they suspend old numbers but preserve G1 and the 8-second buffer.
            for (int i = 420; i < 422; i++)
            {
                receive.Invoke(coordinator, new object[]
                {
                    "{\"type\":\"sample\",\"seq\":" + i +
                    ",\"t_ms\":" + (i * 40) +
                    ",\"ppg\":{\"red\":60000,\"ir\":80000}," +
                    "\"sqi\":{\"contact\":false}}"
                });
                Assert.IsTrue(coordinator.OpticalContactConfirmed);
                Assert.AreEqual("Passed", coordinator.Gate01Status);
                Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
                Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            }

            // Third consecutive failure confirms removal and clears the window.
            receive.Invoke(coordinator, new object[]
            {
                "{\"type\":\"sample\",\"seq\":422,\"t_ms\":16880," +
                "\"ppg\":{\"red\":60000,\"ir\":80000}," +
                "\"sqi\":{\"contact\":false}}"
            });
            Assert.IsFalse(coordinator.OpticalContactConfirmed);
            Assert.AreEqual("Waiting", coordinator.Gate01Status);
            Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            Assert.AreEqual(423, contactEvents);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    // Gate 01 is calculated by the ESP32; the Unity coordinator only accepts
    // fresh, positive G1 window decisions. These tests exercise actual JSON.
    private static void ReceiveG1TestJson(PpgProcessingCoordinator coordinator, string json)
    {
        var receive = typeof(PpgProcessingCoordinator).GetMethod(
            "ReceiveJson", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(receive);
        receive.Invoke(coordinator, new object[] { json });
    }

    private static string MakeGate01TestJson(uint deviceMs, bool passed,
        int reason, int sampleCount = 125)
    {
        return "{\"type\":\"g1\",\"t_ms\":" + deviceMs +
            ",\"window\":1,\"sqi\":{\"g1\":" +
            (passed ? "true" : "false") + ",\"reason\":" + reason +
            ",\"samples\":" + sampleCount + "}}";
    }

    private static string MakeGate01TestSample(int seq, bool contact, int reason = 0)
    {
        double phase = 2.0 * Math.PI * 75.0 * seq * 0.040 / 60.0;
        int red = (int)Math.Round(60000.0 + 500.0 * Math.Sin(phase));
        int ir = (int)Math.Round(80000.0 + 1000.0 * Math.Sin(phase));
        return "{\"type\":\"sample\",\"seq\":" + seq +
            ",\"t_ms\":" + (seq * 40) +
            ",\"ppg\":{\"red\":" + red + ",\"ir\":" + ir +
            "},\"sqi\":{\"g1\":null,\"reason\":" + reason +
            ",\"contact\":" + (contact ? "true" : "false") + "}}";
    }

    [Test]
    public void Gate01WaitingBlocksEstimatesDespiteEightSecondsOfPulses()
    {
        var gameObject = new GameObject("PPG G1 pending test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            for (int i = 0; i < 210; i++)
                ReceiveG1TestJson(coordinator, MakeGate01TestSample(i, true));

            Assert.AreEqual("Waiting", coordinator.Gate01Status);
            Assert.IsTrue(coordinator.OpticalContactConfirmed);
            Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            StringAssert.Contains("Aguardando aprovacao G1",
                coordinator.LastMeasurement.Status);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void Gate01PassAtFiveSecondsAllowsEightSecondPpgWindow()
    {
        var gameObject = new GameObject("PPG G1 pass test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            for (int i = 0; i < 210; i++)
            {
                ReceiveG1TestJson(coordinator, MakeGate01TestSample(i, true));
                if (i == 125)
                    ReceiveG1TestJson(coordinator,
                        MakeGate01TestJson(5000, true, 0));
            }
            Assert.AreEqual("Passed", coordinator.Gate01Status);
            Assert.IsTrue(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsTrue(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void Gate01FailureInvalidatesPreviousEstimatesAndRetainsContact()
    {
        var gameObject = new GameObject("PPG G1 failure test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            for (int i = 0; i < 250; i++)
            {
                ReceiveG1TestJson(coordinator, MakeGate01TestSample(i, true));
                if (i == 125)
                    ReceiveG1TestJson(coordinator,
                        MakeGate01TestJson(5000, true, 0));
            }
            Assert.IsTrue(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            // Firmware emits the failed five-second result after sample 249.
            ReceiveG1TestJson(coordinator,
                MakeGate01TestJson(10000, false, 4));
            Assert.AreEqual("Failed", coordinator.Gate01Status);
            Assert.AreEqual(4, coordinator.LastGate01Reason);
            Assert.IsTrue(coordinator.OpticalContactConfirmed);
            Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);

            // Buffer was reset: a fresh PASS can restore later measurements.
            for (int i = 250; i < 460; i++)
            {
                ReceiveG1TestJson(coordinator,
                    MakeGate01TestSample(i, true, i <= 375 ? 4 : 0));
                if (i == 375)
                    ReceiveG1TestJson(coordinator,
                        MakeGate01TestJson(15000, true, 0));
            }
            Assert.AreEqual("Passed", coordinator.Gate01Status);
            // A complete new 8-second PPG window followed the failed G1.
            Assert.IsTrue(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void Gate01OldPassExpiresAndCannotAuthorizeNewSample()
    {
        var gameObject = new GameObject("PPG G1 stale test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            ReceiveG1TestJson(coordinator, MakeGate01TestJson(0, true, 0));
            for (int i = 0; i < 210; i++)
                ReceiveG1TestJson(coordinator, MakeGate01TestSample(i, true));
            Assert.AreEqual("Stale", coordinator.Gate01Status);
            Assert.IsFalse(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void Gate01SessionResetOrContactLossRevokesPreviousPass()
    {
        var gameObject = new GameObject("PPG G1 reset test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            for (int i = 0; i <= 125; i++)
                ReceiveG1TestJson(coordinator, MakeGate01TestSample(i, true));
            ReceiveG1TestJson(coordinator, MakeGate01TestJson(5000, true, 0));
            Assert.AreEqual("Passed", coordinator.Gate01Status);

            // Firmware g1=null, reason=1 after restarting collection.
            ReceiveG1TestJson(coordinator, MakeGate01TestSample(126, true, 1));
            Assert.AreEqual("Waiting", coordinator.Gate01Status);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);

            ReceiveG1TestJson(coordinator, MakeGate01TestJson(10000, true, 0));
            Assert.AreEqual("Passed", coordinator.Gate01Status);
            ReceiveG1TestJson(coordinator, MakeGate01TestSample(127, false, 10));
            ReceiveG1TestJson(coordinator, MakeGate01TestSample(128, false, 10));
            Assert.AreEqual("Passed", coordinator.Gate01Status);
            Assert.IsTrue(coordinator.OpticalContactConfirmed);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            ReceiveG1TestJson(coordinator, MakeGate01TestSample(129, false, 10));
            Assert.AreEqual("Waiting", coordinator.Gate01Status);
            Assert.IsFalse(coordinator.OpticalContactConfirmed);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void Gate01OutOfOrderResultCannotOverrideLatestFailure()
    {
        var gameObject = new GameObject("PPG G1 ordering test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            ReceiveG1TestJson(coordinator, MakeGate01TestJson(10000, false, 4));
            ReceiveG1TestJson(coordinator, MakeGate01TestJson(5000, true, 0));
            Assert.AreEqual("Failed", coordinator.Gate01Status);
            Assert.AreEqual(4, coordinator.LastGate01Reason);
            Assert.IsFalse(coordinator.Gate01Approved);
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void SequenceGapRestartsTheBuffer()
    {
        var buffer = new PpgSampleBuffer();
        Assert.IsTrue(buffer.Add(new PpgSample(1, 0, 50000, 60000)));
        Assert.IsTrue(buffer.Add(new PpgSample(2, 40, 50000, 60000)));
        Assert.IsFalse(buffer.Add(new PpgSample(4, 80, 50000, 60000)));
        Assert.AreEqual(1, buffer.Count);
    }

    [Test]
    public void CounterWraparoundDoesNotCauseAFalseGap()
    {
        var buffer = new PpgSampleBuffer();
        Assert.IsTrue(buffer.Add(new PpgSample(65535, uint.MaxValue - 20, 50000, 60000)));
        Assert.IsTrue(buffer.Add(new PpgSample(0, 20, 50000, 60000)));
        Assert.AreEqual(2, buffer.Count);
    }

    [Test]
    public void MissingDcOrInsufficientSamplesDoesNotGenerateOutput()
    {
        var insufficient = new List<PpgSample> { new PpgSample(0, 100, 0, 0) };
        PpgProcessedWindow processed;
        string reason;
        Assert.IsFalse(PpgSignalProcessor.TryProcess(insufficient, out processed, out reason));
        Assert.IsNull(processed);

        var data = new List<PpgSample>();
        for (int i = 0; i < 210; i++)
            data.Add(new PpgSample((ushort)i, (uint)(i * 40), 0, 80000));
        Assert.IsFalse(PpgSignalProcessor.TryProcess(data, out processed, out reason));
    }

    [Test]
    public void TwoIsolatedContactFailuresPreserveWindowAndOnlyAllowExperimentalBpm()
    {
        var gameObject = new GameObject("PPG two short contact failures");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            int receivedEvents = 0;
            coordinator.OnOpticalContactSample += ignored => ++receivedEvents;
            for (int i = 0; i < 210; i++)
            {
                bool contact = i != 140 && i != 141;
                ReceiveG1TestJson(coordinator,
                    MakeGate01TestSample(i, contact, contact ? 0 : 10));
                if (i == 125)
                    ReceiveG1TestJson(coordinator,
                        MakeGate01TestJson(5000, true, 0));
            }

            Assert.AreEqual(210, receivedEvents,
                "Placement state machine still receives raw contact events.");
            Assert.IsTrue(coordinator.OpticalContactConfirmed);
            Assert.AreEqual("Passed", coordinator.Gate01Status);
            var field = typeof(PpgProcessingCoordinator).GetField(
                "buffer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var buffer = field.GetValue(coordinator) as PpgSampleBuffer;
            Assert.IsNotNull(buffer);
            Assert.Greater(buffer.Count, 190,
                "The prior rolling window must not be discarded.");
            Assert.IsTrue(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsFalse(coordinator.LastMeasurement.SpO2DemoPercent.HasValue,
                "A reconstructed RED/IR gap is not acceptable for SpO2.");
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }

    [Test]
    public void MarkedOneOrTwoSampleGapsRetainSequenceContinuity()
    {
        var buffer = new PpgSampleBuffer();
        Assert.IsTrue(buffer.Add(new PpgSample(100, 4000, 60000, 80000)));
        Assert.IsTrue(buffer.Add(new PpgSample(103, 4120, 60010, 80020, 2)));
        Assert.AreEqual(2, buffer.Count);
        // Unknown sequence gaps must still reset: only explicitly marked
        // optical contact gaps are permitted.
        Assert.IsFalse(buffer.Add(new PpgSample(105, 4200, 60020, 80040)));
        Assert.AreEqual(1, buffer.Count);
    }

    [Test]
    public void ExcessiveNonConsecutiveContactGapsRejectProcessing()
    {
        var samples = new List<PpgSample>();
        for (int i = 0; i < 210; i++)
        {
            if (i > 0 && i % 20 == 0) continue;
            double phase = 2.0 * Math.PI * 75.0 * (i * 0.040) / 60.0;
            int red = (int)Math.Round(60000.0 + 500.0 * Math.Sin(phase));
            int ir = (int)Math.Round(80000.0 + 1000.0 * Math.Sin(phase));
            byte previousGap = (byte)(i > 1 && (i - 1) % 20 == 0 ? 1 : 0);
            samples.Add(new PpgSample((ushort)i, (uint)(i * 40),
                red, ir, previousGap));
        }
        PpgProcessedWindow processed;
        string reason;
        Assert.IsFalse(PpgSignalProcessor.TryProcess(samples, out processed, out reason));
        StringAssert.Contains("Too many short optical-contact gaps", reason);
    }

}
