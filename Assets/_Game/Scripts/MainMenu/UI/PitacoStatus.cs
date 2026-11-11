using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;

namespace Ibit.MainMenu.UI
{
    public class PitacoStatus : MonoBehaviour
    {
        [SerializeField]
        private SerialControllerPitaco serialController;

        [SerializeField]
        private Sprite offline, online;

        private void Awake()
        {
            if (serialController == null)
                serialController = FindObjectOfType<SerialControllerPitaco>();

            if (serialController == null)
                Debug.LogWarning("Serial Controller instance not found!");
        }

        private void FixedUpdate()
        {
            // Este indicador representa apenas o dispositivo fisico PITACO 2.0.
            // O PITACO 2.4 pode reutilizar a API respiratoria legada,
            // mas possui seu proprio indicador Pitaco24Status.
            var manager = PitacoConnectionManager.Active;
            bool connected = manager != null
                ? manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco20
                : serialController != null && serialController.IsConnected;

            var image = GetComponent<Image>();
            var target = connected ? online : offline;
            if (target != null && image.sprite != target)
                image.sprite = target;
        }

        public void Reboot() => UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); // Descobre o índice da scene atual e a reinicia pra tentar reconectar o Pitaco.
    }
}