using System;
using UnityEngine;

namespace Ibit.Core.Serial
{
    /// <summary>
    /// Snapshot leve da telemetria optica do PITACO 2.4.
    /// O manager chama AcceptLine no thread principal, inclusive apos trocar de cena.
    /// Nao substitui o oximetro legado nem gera alarmes/decisoes terapeuticas.
    /// </summary>
    public static class Pitaco24Telemetry
    {
        private const float StaleAfterSeconds = 3f;

        [Serializable]
        private sealed class Frame
        {
            public string type;
            public Ppg ppg;
            public Sqi sqi;
        }

        [Serializable]
        private sealed class Ppg
        {
            public float bpm;
            public bool bpm_valid;
        }

        [Serializable]
        private sealed class Sqi
        {
            public bool contact;
            public bool g1;
            public int reason;
            public int samples;
        }

        private static bool hasSample;
        private static float lastSampleAt;
        private static bool hasContact;
        private static bool bpmValid;
        private static float bpm;
        private static bool hasGateResult;
        private static bool g1Passed;

        public static bool HasFreshSample =>
            hasSample && Time.realtimeSinceStartup - lastSampleAt <= StaleAfterSeconds;

        public static bool HasContact => HasFreshSample && hasContact;

        public static bool HasValidBpm => HasFreshSample && hasContact && bpmValid;

        public static float Bpm => HasValidBpm ? bpm : 0f;

        public static bool HasGateResult => HasFreshSample && hasContact && hasGateResult;

        public static bool G1Passed => HasGateResult && g1Passed;

        public static void Reset()
        {
            hasSample = false;
            hasContact = false;
            bpmValid = false;
            bpm = 0f;
            hasGateResult = false;
            g1Passed = false;
            lastSampleAt = 0f;
        }

        public static void AcceptLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            line = line.Trim();
            if (!line.StartsWith("{", StringComparison.Ordinal) ||
                !line.EndsWith("}", StringComparison.Ordinal))
                return;

            Frame frame;
            try
            {
                frame = JsonUtility.FromJson<Frame>(line);
            }
            catch (Exception)
            {
                // Mensagens malformadas nao devem interromper o jogo.
                return;
            }

            if (frame == null || frame.sqi == null)
                return;

            if (string.Equals(frame.type, "sample", StringComparison.Ordinal))
            {
                if (frame.ppg == null)
                    return;

                hasSample = true;
                lastSampleAt = Time.realtimeSinceStartup;
                hasContact = frame.sqi.contact;
                bpmValid = hasContact && frame.ppg.bpm_valid &&
                           frame.ppg.bpm > 0f &&
                           !float.IsNaN(frame.ppg.bpm) &&
                           !float.IsInfinity(frame.ppg.bpm);
                bpm = bpmValid ? frame.ppg.bpm : 0f;

                // Nao reaproveitar um PASS anterior apos perder contato.
                if (!hasContact)
                {
                    hasGateResult = false;
                    g1Passed = false;
                }
            }
            else if (string.Equals(frame.type, "g1", StringComparison.Ordinal))
            {
                // G1 e emitido pelo firmware ao final da janela de avaliacao.
                if (hasSample && hasContact)
                {
                    hasGateResult = true;
                    g1Passed = frame.sqi.g1;
                }
            }
        }
    }
}
