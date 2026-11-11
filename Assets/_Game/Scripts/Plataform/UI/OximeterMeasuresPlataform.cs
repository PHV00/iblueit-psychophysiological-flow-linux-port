using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;

namespace Ibit.Plataform.UI
{
    // Reutiliza o HUD de medidas ja existente na Plataforma.
    // Um oximetro dedicado continua tendo prioridade sobre dados experimentais.
    [RequireComponent(typeof(Text))]
    public class OximeterMeasuresPlataform : MonoBehaviour
    {
        private Player plr;
        private Text label;
        private float nextRefresh;

        private void Awake()
        {
            plr = FindObjectOfType<Player>();
            label = GetComponent<Text>();
        }

        private void Update()
        {
            if (label == null || Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + 0.2f;

            if (plr != null && plr.oxiActive)
            {
                // Preserva a apresentacao e a prioridade do oximetro original.
                label.text = $"HR: {plr.HRValue} bpm\nSPO2: {plr.SPO2Value} %";
                return;
            }

            var manager = PitacoConnectionManager.Active;
            if (manager == null ||
                manager.ConnectedVersion != PitacoConnectionManager.Version.Pitaco24)
            {
                label.text = "";
                return;
            }

            if (!Pitaco24Telemetry.HasFreshSample)
            {
                label.text = "PITACO 2.4 (experimental)\nPPG: aguardando sinal\nSpO2: indisponivel";
                return;
            }

            string bpm = Pitaco24Telemetry.HasValidBpm
                ? Pitaco24Telemetry.Bpm.ToString("0") + " bpm"
                : "-- bpm";
            string contact = Pitaco24Telemetry.HasContact ? "sim" : "nao";
            string g1 = !Pitaco24Telemetry.HasGateResult
                ? "em analise"
                : Pitaco24Telemetry.G1Passed ? "aprovado" : "reprovado";

            // SpO2 do MAX30105 nao esta calibrada/validada: nunca mostrar
            // 0%, estimativas ou limiares clinicos como valores reais.
            label.text = $"PITACO 2.4 (experimental)\nFC: {bpm}\n" +
                         $"Contato: {contact} | SQI G1: {g1}\nSpO2: indisponivel";
        }
    }
}
