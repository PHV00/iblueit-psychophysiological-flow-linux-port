using UnityEngine;
using UnityEngine.UI;

namespace Ibit.CakeGame
{
    public class OximeterMeasuresCakeGame : MonoBehaviour
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
