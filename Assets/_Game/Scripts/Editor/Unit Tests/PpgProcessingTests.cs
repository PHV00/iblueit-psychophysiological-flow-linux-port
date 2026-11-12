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
    public void ContactFalseDoesNotBlockCandidateEstimatorsInExperimentalBranch()
    {
        // A synthetic pulsatile signal deliberately reports NO finger contact.
        // This test asserts separation of math from firmware quality policy,
        // NOT measurement accuracy or clinical permission to display it.
        var gameObject = new GameObject("PPG contact-independent test");
        try
        {
            var coordinator = gameObject.AddComponent<PpgProcessingCoordinator>();
            var receive = typeof(PpgProcessingCoordinator).GetMethod(
                "ReceiveJson", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(receive);
            for (int i = 0; i < 210; ++i)
            {
                double t = i * 0.040;
                double wave = Math.Sin(2.0 * Math.PI * 75.0 * t / 60.0);
                int red = (int)Math.Round(60000.0 + 500.0 * wave);
                int ir = (int)Math.Round(80000.0 + 1000.0 * wave);
                string json = "{\"type\":\"sample\",\"seq\":" + i +
                    ",\"t_ms\":" + (i * 40) +
                    ",\"ppg\":{\"red\":" + red + ",\"ir\":" + ir +
                    "},\"sqi\":{\"contact\":false}}";
                receive.Invoke(coordinator, new object[] { json });
            }
            Assert.IsFalse(coordinator.OpticalContactConfirmed);
            Assert.IsNotNull(coordinator.LastMeasurement);
            Assert.IsTrue(coordinator.LastMeasurement.BpmEstimate.HasValue);
            Assert.IsTrue(coordinator.LastMeasurement.SpO2DemoPercent.HasValue);
            StringAssert.Contains("SEM CONTATO", coordinator.LastMeasurement.Status);
            StringAssert.Contains("CAND.", Player.FormatPitaco24Oximetry(
                coordinator.LastMeasurement.BpmEstimate,
                coordinator.LastMeasurement.SpO2DemoPercent, false));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void WithoutContactHudLabelsNumbersAsUnverifiedCandidates()
    {
        string text = Player.FormatPitaco24Oximetry(75.1, 95.0, false);
        StringAssert.Contains("BPM CAND. S/CONTATO: 75.1", text);
        StringAssert.Contains("SpO2 DEMO CAND.: 95.0 %", text);
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
}
