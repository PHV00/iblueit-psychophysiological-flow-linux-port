using System;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// New IR autocorrelation BPM estimator, ported from the PITACO Python
    /// experimental analyzer. Does not consume the firmware checkForBeat BPM.
    /// </summary>
    public static class BpmEstimator
    {
        public const double MinimumBpm = 35.0;
        public const double MaximumBpm = 200.0;
        private const double MinimumAutocorrelation = 0.57;
        private const double PeakTolerance = 0.075;

        public static bool TryEstimate(PpgProcessedWindow input,
            out double bpm, out double correlationPeak)
        {
            bpm = 0.0;
            correlationPeak = 0.0;
            if (input == null || input.IrAc == null || input.IrAc.Length < 70)
                return false;

            double[] data = input.IrAc;
            int n = data.Length;
            double average = 0.0;
            for (int i = 0; i < n; i++) average += data[i];
            average /= n;

            var centered = new double[n];
            for (int i = 0; i < n; i++) centered[i] = data[i] - average;

            int minLag = Math.Max(2, (int)(input.SamplingHz * 60.0 / MaximumBpm));
            int maxLag = Math.Min(n / 3, (int)(input.SamplingHz * 60.0 / MinimumBpm) + 1);
            if (minLag > maxLag) return false;

            var scores = new double[maxLag + 2];
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double cross = 0.0, leftPower = 0.0, rightPower = 0.0;
                for (int i = 0; i < n - lag; i++)
                {
                    double left = centered[i], right = centered[i + lag];
                    cross += left * right;
                    leftPower += left * left;
                    rightPower += right * right;
                }
                double denominator = Math.Sqrt(leftPower * rightPower);
                scores[lag] = denominator > 0.0 ? cross / denominator : 0.0;
                correlationPeak = Math.Max(correlationPeak, scores[lag]);
            }
            if (correlationPeak < MinimumAutocorrelation) return false;

            int chosenLag = -1;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double left = lag > minLag ? scores[lag - 1] : -1.0;
                double right = lag < maxLag ? scores[lag + 1] : -1.0;
                if (scores[lag] >= correlationPeak - PeakTolerance &&
                    scores[lag] >= left && scores[lag] >= right)
                {
                    chosenLag = lag; // shortest strong local peak; avoids doubled period
                    break;
                }
            }
            if (chosenLag < 0) return false;

            double refinedLag = chosenLag;
            if (chosenLag > minLag && chosenLag < maxLag)
            {
                double left = scores[chosenLag - 1];
                double center = scores[chosenLag];
                double right = scores[chosenLag + 1];
                double curvature = left - 2.0 * center + right;
                if (curvature < -1e-8)
                {
                    double offset = 0.5 * (left - right) / curvature;
                    refinedLag += Math.Max(-0.5, Math.Min(0.5, offset));
                }
            }

            double estimate = 60.0 * input.SamplingHz / refinedLag;
            if (estimate < MinimumBpm || estimate > MaximumBpm ||
                double.IsNaN(estimate) || double.IsInfinity(estimate))
                return false;

            bpm = Math.Round(estimate, 1);
            return true;
        }
    }
}
