using System.Collections.Generic;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// Rolling raw PPG window of at least 8 seconds. Timestamp is the FIFO read time,
    /// not ADC conversion time: equal timestamps are allowed for contiguous sequences.
    /// A small retention margin avoids permanently missing an exact 8000-ms span
    /// when FIFO read timestamps are irregular or grouped.
    /// </summary>
    public sealed class PpgSampleBuffer
    {
        public const uint WindowMs = 8000;
        private const uint RetentionMarginMs = 200;
        private const uint MaxReadGapMs = 750;
        private readonly List<PpgSample> samples = new List<PpgSample>(256);
        public IReadOnlyList<PpgSample> Samples { get { return samples; } }
        public int Count { get { return samples.Count; } }

        public void Clear() { samples.Clear(); }

        /// <returns>False when continuity was broken and the window restarted.</returns>
        public bool Add(PpgSample sample)
        {
            bool continuous = true;
            if (samples.Count > 0)
            {
                PpgSample previous = samples[samples.Count - 1];
                // Only known 1-2 contact gaps may skip sequence slots.
                // Any other lost sample still restarts the rolling window.
                int gap = sample.SkippedContactSamplesBefore;
                int expected = (previous.Sequence + 1 + gap) & 0xFFFF;
                uint elapsed = unchecked(sample.TimeMs - previous.TimeMs);
                if (gap > 2 || sample.Sequence != expected || elapsed > MaxReadGapMs)
                {
                    Clear();
                    continuous = false;
                }
            }

            samples.Add(sample);
            while (samples.Count > 0 &&
                   unchecked(sample.TimeMs - samples[0].TimeMs) > WindowMs + RetentionMarginMs)
                samples.RemoveAt(0);

            // Defensive bound if several FIFO samples share the same timestamp.
            while (samples.Count > 256)
                samples.RemoveAt(0);
            return continuous;
        }
    }
}
