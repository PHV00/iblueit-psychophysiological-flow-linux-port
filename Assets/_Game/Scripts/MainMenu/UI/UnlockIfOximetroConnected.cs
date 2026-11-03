using Ibit.Core.Data;
using UnityEngine;
using UnityEngine.UI;
using Ibit.Core.Serial;

namespace Ibit.MainMenu.UI
{
    public class UnlockIfOximetroConnected : MonoBehaviour
    {

        [SerializeField]
        private SerialControllerOximetro serialControllerOximetro;
        [SerializeField] private SerialControllerPitaco serialControllerPitaco;
   

        private bool OximetroConnected = false;
   

        private void OnEnable()
        {

            if (serialControllerOximetro == null)
                serialControllerOximetro = FindObjectOfType<SerialControllerOximetro>();
            if (serialControllerPitaco == null)
                serialControllerPitaco = FindObjectOfType<SerialControllerPitaco>();
            RefreshAvailability();
        }

        private void Update()
        {
            // O primeiro P24 pode chegar depois de OnEnable, quando a COM
            // ja esta conectada. Tambem invalida se a telemetria parar.
            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            OximetroConnected =
                (serialControllerOximetro != null && serialControllerOximetro.IsConnected) ||
                (serialControllerPitaco != null && serialControllerPitaco.HasRecentPitaco24Combo);
            this.GetComponent<Button>().interactable =
                Pacient.Loaded != null && OximetroConnected;
        }
    }
}