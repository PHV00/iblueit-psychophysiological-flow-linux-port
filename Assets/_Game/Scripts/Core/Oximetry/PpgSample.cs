namespace Ibit.Core.Oximetry
{
    /// <summary>One raw RED/IR pair from PITACO 2.4; no SDK BPM or SpO2 is used.</summary>
    public struct PpgSample
    {
        public const int AdcMaximum = 262143;
        public readonly ushort Sequence;
        public readonly uint TimeMs;
        public readonly int Red;
        public readonly int Ir;
        // 0-2 rejected contact observations immediately before this real sample.
        public readonly byte SkippedContactSamplesBefore;

        public PpgSample(ushort sequence, uint timeMs, int red, int ir,
            byte skippedContactSamplesBefore = 0)
        {
            Sequence = sequence;
            TimeMs = timeMs;
            Red = red;
            Ir = ir;
            SkippedContactSamplesBefore = skippedContactSamplesBefore;
        }

        public static bool TryCreate(int sequence, long timeMs, int red, int ir, out PpgSample sample)
        {
            sample = default(PpgSample);
            if (sequence < 0 || sequence > ushort.MaxValue ||
                timeMs < 0 || timeMs > uint.MaxValue ||
                red < 0 || ir < 0 || red > AdcMaximum || ir > AdcMaximum)
                return false;

            sample = new PpgSample((ushort)sequence, (uint)timeMs, red, ir);
            return true;
        }
    }
}
