using UnityEngine;
using UnityEngine.UI;

namespace Ibit.WaterGame
{
    public class OximeterMeasuresWaterGame : MonoBehaviour
    {
        private Player plr;
        private void Awake()
        {
            plr = FindObjectOfType<Player>();
        }

        private void Update()
        {
            if (plr.oxiActive)
            {
                GetComponent<Text>().text = plr.OximetryHudText;
            }
            else
            {
                GetComponent<Text>().text = "";
            }
        }
    }
}
