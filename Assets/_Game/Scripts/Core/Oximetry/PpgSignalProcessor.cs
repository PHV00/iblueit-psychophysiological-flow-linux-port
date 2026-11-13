using System;
using System.Collections.Generic;

namespace Ibit.Core.Oximetry
{
    public sealed class PpgProcessedWindow
    {
        public readonly double SamplingHz;
        public readonly double RedDc;
        public readonly double IrDc;
        public readonly double RedAcRms;
        public readonly double IrAcRms;
        public readonly double[] RedAc;
        public readonly double[] IrAc;
        // Reconstructed points are not real optical measurements.
        public readonly int ReconstructedSamples;

        internal PpgProcessedWindow(double samplingHz, double redDc, double irDc,
            double[] redAc, double[] irAc, double redAcRms, double irAcRms,
            int reconstructedSamples)
        {
            SamplingHz = samplingHz;
            RedDc = redDc;
            IrDc = irDc;
            RedAc = redAc;
            IrAc = irAc;
            RedAcRms = redAcRms;
            IrAcRms = irAcRms;
            ReconstructedSamples = reconstructedSamples;
        }
    }

    /// <summary>
    /// Mirrors the moving-mean removal and three-point smoothing from the
    /// PITACO Python experimental analyzer. No SQI gate or contact state is used.
    /// </summary>
    public static class PpgSignalProcessor
    {
        public const int MinimumSamples = 70;
        // Require a complete window before producing any BPM/SpO2 estimate.
        public const uint MinimumWindowMs = PpgSampleBuffer.WindowMs;

        public static bool TryProcess(IReadOnlyList<PpgSample> samples,
            out PpgProcessedWindow result, out string reason)
        {
            result = null;
            reason = "Waiting for a full 8-second PPG window";
            if (samples == null || samples.Count < MinimumSamples)
                return false;

            int n = samples.Count;
            uint duration = unchecked(samples[n - 1].TimeMs - samples[0].TimeMs);
            if (duration < MinimumWindowMs)
                return false;

            // Validate raw sequence and distinguish known contact gaps from
            // unexpected serial/FIFO loss. Never accept invalid ADC values.
            int reconstructed = 0;
            for (int i = 0; i < n; i++)
            {
                PpgSample s = samples[i];
                if (s.Red < 0 || s.Ir < 0 || s.Red > PpgSample.AdcMaximum ||
                    s.Ir > PpgSample.AdcMaximum)
                {
                    reason = "ADC values outside 18-bit range";
                    return false;
                }
                if (i > 0)
                {
                    PpgSample previous = samples[i - 1];
                    int missing = s.SkippedContactSamplesBefore;
                    if (missing > 2 ||
                        s.Sequence != ((previous.Sequence + 1 + missing) & 0xFFFF) ||
                        unchecked(s.TimeMs - previous.TimeMs) > 750)
                    {
                        reason = "Sequence or FIFO read time discontinuity";
                        return false;
                    }
                    reconstructed += missing;
                }
            }

            int effectiveCount = n + reconstructed;
            // Experimental 2% ceiling: repetitive contact failures must not
            // cause unbounded signal synthesis. Validate with device logs.
            if (reconstructed * 100 > effectiveCount * 2)
            {
                reason = "Too many short optical-contact gaps (>2%)";
                return false;
            }

            // Timestamp belongs to FIFO read; use the expected sample slots,
            // including explicitly quarantined contact gaps, for effective fs.
            double fs = (effectiveCount - 1) * 1000.0 / duration;
            if (fs < 20.0 || fs > 30.0)
            {
                reason = "Effective sample rate outside 20-30 Hz";
                return false;
            }

            var red = new double[effectiveCount];
            var ir = new double[effectiveCount];
            double redSum = 0.0, irSum = 0.0;
            int position = 0;
            for (int i = 0; i < n; i++)
            {
                PpgSample current = samples[i];
                if (i > 0)
                {
                    PpgSample previous = samples[i - 1];
                    int missing = current.SkippedContactSamplesBefore;
                    // Only 1-2 missing slots, interpolated for experimental BPM.
                    // Suspicious RED/IR values are NOT used.
                    for (int j = 1; j <= missing; j++)
                    {
                        double fraction = j / (missing + 1.0);
                        red[position] = previous.Red +
                            (current.Red - previous.Red) * fraction;
                        ir[position] = previous.Ir +
                            (current.Ir - previous.Ir) * fraction;
                        redSum += red[position];
                        irSum += ir[position];
                        ++position;
                    }
                }
                red[position] = current.Red;
                ir[position] = current.Ir;
                redSum += red[position];
                irSum += ir[position];
                ++position;
            }

            double redDc = redSum / effectiveCount;
            double irDc = irSum / effectiveCount;
            if (redDc <= 0.0 || irDc <= 0.0)
            {
                reason = "Zero optical DC: ratio undefined";
                return false;
            }

            int halfWidth = Math.Max(3, (int)Math.Round(fs / 2.0));
            double[] redAc = RemoveSlowComponent(red, halfWidth);
            double[] irAc = RemoveSlowComponent(ir, halfWidth);
            result = new PpgProcessedWindow(fs, redDc, irDc,
                redAc, irAc, RootMeanSquare(redAc), RootMeanSquare(irAc),
                reconstructed);
            reason = reconstructed == 0 ? "Window processed (experimental)" :
                "Window processed (experimental, short gaps reconstructed)";
            return true;
        }

        private static double[] RemoveSlowComponent(double[] values, int halfWidth)
        {
            int n = values.Length;
            var prefix = new double[n + 1];
            for (int i = 0; i < n; i++)
                prefix[i + 1] = prefix[i] + values[i];

            var high = new double[n];
            for (int i = 0; i < n; i++)
            {
                int lo = Math.Max(0, i - halfWidth);
                int hi = Math.Min(n, i + halfWidth + 1);
                high[i] = values[i] - (prefix[hi] - prefix[lo]) / (hi - lo);
            }

            var smooth = new double[n];
            for (int i = 0; i < n; i++)
                smooth[i] = (high[Math.Max(0, i - 1)] + high[i] +
                             high[Math.Min(n - 1, i + 1)]) / 3.0;
            return smooth;
        }

        private static double RootMeanSquare(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i] * values[i];
            return Math.Sqrt(sum / values.Length);
        }
    }
}
