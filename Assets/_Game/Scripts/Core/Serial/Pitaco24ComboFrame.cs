// PITACO 2.4: frame combinado produzido pelo middleware Python (independente
// de como ele recebe os sensores: USB ou BLE). SpO2 e reservada, nao usada.
using System;
using System.Globalization;

namespace Ibit.Core.Serial
{
    public sealed class Pitaco24ComboFrame
    {
        public float RespiratoryValue { get; private set; }
        public float Bpm { get; private set; }
        public bool HasValidBpm { get; private set; }
        public bool G1Passed { get; private set; }
        public bool ContactPresent { get; private set; }

        private Pitaco24ComboFrame() { }

        public static bool TryParse(string line, out Pitaco24ComboFrame sample)
        {
            sample = null;
            if (string.IsNullOrEmpty(line))
                return false;

            // P24,<pressao_Pa>,<bpm_ou_vazio>,<spo2_vazio>,<g1>,<contato>
            var fields = line.Trim().Split(',');
            if (fields.Length != 6 || fields[0] != "P24" ||
                !string.IsNullOrEmpty(fields[3]))  // SpO2 nao calibrada: rejeitar.
                return false;

            float respiratory;
            if (!float.TryParse(fields[1], NumberStyles.Float,
                CultureInfo.InvariantCulture, out respiratory) ||
                float.IsNaN(respiratory) || float.IsInfinity(respiratory))
                return false;

            if ((fields[4] != "0" && fields[4] != "1") ||
                (fields[5] != "0" && fields[5] != "1"))
                return false;

            bool g1 = fields[4] == "1";
            bool contact = fields[5] == "1";
            float bpm = 0f;
            bool valid = !string.IsNullOrEmpty(fields[2]);
            if (valid && (!g1 || !contact ||
                !float.TryParse(fields[2], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out bpm) ||
                float.IsNaN(bpm) || float.IsInfinity(bpm) ||
                bpm < 35f || bpm > 200f))
                return false;

            sample = new Pitaco24ComboFrame
            {
                RespiratoryValue = respiratory,
                Bpm = bpm,
                HasValidBpm = valid,
                G1Passed = g1,
                ContactPresent = contact
            };
            return true;
        }
    }
}
