using System.Collections.Generic;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// Bounded 8-second raw window. Timestamp is the FIFO read time, not ADC conversion
    /// time: equal timestamps are permitted if sequence numbers are contiguous.
    /// </summary>
    public sealed class PpgSampleBuffer
    {
        public const uint WindowMs = 8000;
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
                int expected = (previous.Sequence + 1) & 0xFFFF;
                uint elapsed = unchecked(sample.TimeMs - previous.TimeMs);
                if (sample.Sequence != expected || elapsed > MaxReadGapMs)
                {
                    Clear();
                    continuous = false;
                }
            }

            samples.Add(sample);
            while (samples.Count > 0 &&
                   unchecked(sample.TimeMs - samples[0].TimeMs) > WindowMs)
                samples.RemoveAt(0);

            // Defensive bound if several FIFO samples share the same timestamp.
            while (samples.Count > 256)
                samples.RemoveAt(0);
            return continuous;
        }
    }
}
