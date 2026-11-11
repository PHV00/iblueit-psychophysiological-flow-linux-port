using UnityEngine;
using UnityEngine.UI;

namespace Ibit.Plataform.UI
{
    public class OximeterMeasuresPlataform : MonoBehaviour
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
