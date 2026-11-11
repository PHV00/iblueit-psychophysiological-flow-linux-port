using System;
using System.Globalization;

namespace Ibit.Core.Oximetry
{
    /// <summary>
    /// Reuses the legacy two-line Text HUD, without writing to Player,
    /// MixerManager, DeepDDA, or the legacy SerialControllerOximetro.
    /// </summary>
    public static class OximetryHudFormatter
    {
        public static bool TryFormatPitaco24(out string text)
        {
            var hub = OximetryIntegrationHub.Instance;
            if (hub == null || !hub.IsPitaco24Connected)
            {
                text = null;
                return false;
            }

            text = Format(hub.DisplayReading);
            return true;
        }

        public static string Format(OximetryReading reading)
        {
            if (reading == null || !reading.HasMeasurement)
            {
                string status = reading == null ? "aguardando sinal"
                    : ShortStatus(reading.Status);
                return "PITACO 2.4: " + status + "\nHR: -- | SpO2 DEMO: --";
            }

            string bpm = reading.Bpm.HasValue
                ? reading.Bpm.Value.ToString("F1", CultureInfo.InvariantCulture) : "--";
            string spo2 = reading.SpO2DemoPercent.HasValue
                ? reading.SpO2DemoPercent.Value.ToString("F1",
                    CultureInfo.InvariantCulture) : "--";
            return "HR [2.4]: " + bpm + " bpm\nSpO2 DEMO: " + spo2 + " %";
        }

        private static string ShortStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return "aguardando sinal";
            if (status.IndexOf("contato", StringComparison.OrdinalIgnoreCase) >= 0)
                return "sem contato";
            if (status.IndexOf("desconectado", StringComparison.OrdinalIgnoreCase) >= 0)
                return "desconectado";
            if (status.IndexOf("recentes", StringComparison.OrdinalIgnoreCase) >= 0)
                return "sinal interrompido";
            return "aguardando 8 s";
        }
    }
}
