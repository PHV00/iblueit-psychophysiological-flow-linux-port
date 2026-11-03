using System;
using Ibit.Core.Serial;

static class Test
{
    static int count;
    static void Check(bool expected, string name)
    {
        if (!expected) throw new Exception("FAIL: " + name);
        count++;
    }

    public static void Main()
    {
        Pitaco24ComboFrame sample;
        Check(Pitaco24ComboFrame.TryParse("P24,-23.15,72.5,,1,1\\r\\n".Replace("\\r", "\r").Replace("\\n", "\n"), out sample),
            "valid frame");
        Check(sample != null && sample.RespiratoryValue == -23.15f &&
              sample.HasValidBpm && sample.Bpm == 72.5f &&
              sample.G1Passed && sample.ContactPresent, "fields");
        Check(Pitaco24ComboFrame.TryParse("P24,20.00,,,1,1", out sample) &&
              !sample.HasValidBpm, "optional BPM");
        Check(Pitaco24ComboFrame.TryParse("P24,0.00,,,0,0", out sample) &&
              !sample.ContactPresent, "absent contact");
        Check(!Pitaco24ComboFrame.TryParse("P24,20.00,72,,0,1", out sample),
              "reject BPM with bad gate");
        Check(!Pitaco24ComboFrame.TryParse("P24,20.00,72,97,1,1", out sample),
              "reject demo SpO2");
        Check(!Pitaco24ComboFrame.TryParse("P24,nan,72,,1,1", out sample),
              "reject non numeric breathing");
        Check(!Pitaco24ComboFrame.TryParse("P24,20.00,999,,1,1", out sample),
              "reject BPM outside range");
        Check(!Pitaco24ComboFrame.TryParse("P24,20.00,72,,1", out sample),
              "reject missing field");
        Check(!Pitaco24ComboFrame.TryParse("32.2", out sample),
              "legacy numeric line is not a P24 frame");
        Console.WriteLine("PASS: " + count + " testes C# do frame P24");
    }
}
