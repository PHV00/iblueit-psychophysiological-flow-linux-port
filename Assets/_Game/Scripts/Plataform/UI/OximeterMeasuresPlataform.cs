using Ibit.Core.Oximetry;
using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;

namespace Ibit.Plataform.UI
{
    public class OximeterMeasuresPlataform : MonoBehaviour
    {
        private Player plr;
        private Text hud;

        private void Awake()
        {
            plr = FindObjectOfType<Player>();
            hud = GetComponent<Text>();
        }

        private void Update()
        {
            if (hud == null)
                return;

            if (plr == null)
                plr = FindObjectOfType<Player>();

            var manager = PitacoConnectionManager.Active;
            bool pitaco24Connected = manager != null &&
                manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco24;

            // No PITACO 2.4, a conexao USB pode existir antes de haver
            // uma janela PPG valida: nao deixar o HUD simplesmente sumir.
            if (pitaco24Connected)
            {
                var coordinator = manager.GetComponent<PpgProcessingCoordinator>();
                if (coordinator == null)
                {
                    hud.text = "PITACO 2.4: conectado\nPPG: processamento ausente";
                    return;
                }

                if (coordinator.JsonCallbacksReceived == 0)
                {
                    var serial = manager.GetComponent<SerialControllerPitaco24>();
                    bool rawJsonPresent = serial != null && serial.JsonMessagesReceived > 0;
                    hud.text = rawJsonPresent
                        ? "PITACO 2.4: conectado\nPPG: JSON nao encaminhado"
                        : "PITACO 2.4: conectado\nPPG: aguardando JSON";
                    return;
                }

                if (coordinator.ReceivedSampleFrames == 0)
                {
                    hud.text = "PITACO 2.4: conectado\nPPG: JSON sem amostras RED/IR";
                    return;
                }

                if (plr == null)
                {
                    hud.text = "PITACO 2.4: conectado\nPPG: aguardando Player";
                    return;
                }

                hud.text = plr.OximetryHudText;
                return;
            }

            // Mantem a apresentacao e a visibilidade do oximetro original.
            hud.text = plr != null && plr.oxiActive ? plr.OximetryHudText : "";
        }
    }
}
