using Ibit.Core.Oximetry.UI;
using UnityEngine;

namespace Ibit.WaterGame
{
    /// <summary>
    /// Preserves this scene's serialized MonoBehaviour reference.
    /// The shared presenter owns PITACO 2.4 status and UI rendering;
    /// this adapter only reads the scene's existing legacy Player.
    /// </summary>
    public class OximeterMeasuresWaterGame : OximetryHudPresenter
    {
        private Player player;

        protected override string ReadPlayerText(bool pitaco24Connected)
        {
            if (player == null)
                player = FindObjectOfType<Player>();
            if (player == null)
                return null;
            return pitaco24Connected || player.oxiActive
                ? player.OximetryHudText
                : string.Empty;
        }

        protected override void Update()
        {
            base.Update();
        }
    }
}
