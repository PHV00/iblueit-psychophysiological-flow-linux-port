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
            { // Se o oxímetro estiver conectado, exibe seus valores
                // A fonte P24 possui HR experimental, mas nao SpO2 calibrada.
                var spo2 = plr.Pitaco24ComboBpmSource ? "-- (não disponível)" :
                    $"{plr.SPO2Value} %";
                GetComponent<Text>().text = $"HR: {plr.HRValue} bpm\nSPO2: {spo2}";
            } else {
                GetComponent<Text>().text = "";
            }
        }
    }
}