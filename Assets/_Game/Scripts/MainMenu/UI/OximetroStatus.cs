using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;

namespace Ibit.MainMenu.UI
{
    public class OximetroStatus : MonoBehaviour
    {
        [SerializeField]
        private SerialControllerOximetro serialController;
        private SerialControllerPitaco pitaco24Controller;

        [SerializeField]
        private Sprite offline, online;

        private void Awake()
        {
            if (serialController == null)
                serialController = FindObjectOfType<SerialControllerOximetro>();

            if (pitaco24Controller == null)
                pitaco24Controller = FindObjectOfType<SerialControllerPitaco>();

            if (serialController == null && pitaco24Controller == null)
                Debug.LogWarning("Nenhum receptor de biossinais encontrado.");
        }

        private void FixedUpdate()
        {
            bool legacy = serialController != null && serialController.IsConnected;
            bool combo = pitaco24Controller != null && pitaco24Controller.HasRecentPitaco24Combo;
            GetComponent<Image>().sprite = (legacy || combo) ? online : offline;
        }

        public void Reboot() => UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); // Descobre o índice da scene atual e a reinicia pra tentar reconectar o Oxímetro.
    }
}