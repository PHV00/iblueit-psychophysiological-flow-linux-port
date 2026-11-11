using UnityEngine;
using UnityEngine.UI;
using Ibit.Core.Oximetry;

namespace Ibit.WaterGame
{
    public class OximeterMeasuresWaterGame : MonoBehaviour
    {
        private Player plr;
        private Text label;
        private void Awake()
        {
            plr = FindObjectOfType<Player>();
            label = GetComponent<Text>();
        }

        private void Update()
        {
            if (label == null) return;

            // PITACO 2.4 gets priority when connected. Only the HUD is changed:
            // do not write Player.sensorValueOxiSPO/HR or invoke Mixer/DeepDDA.
            string experimentalText;
            if (OximetryHudFormatter.TryFormatPitaco24(out experimentalText))
            {
                label.text = experimentalText;
                return;
            }

            // Preserve the existing serial oximeter fallback unchanged.
            label.text = plr != null && plr.oxiActive
                ? $"HR: {plr.HRValue} bpm\nSPO2: {plr.SPO2Value} %"
                : "";
        }
    }
}