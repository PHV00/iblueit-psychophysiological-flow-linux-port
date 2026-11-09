using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Ibit.MainMenu.UI
{
    [RequireComponent(typeof(Image))]
    public class Pitaco24Status : MonoBehaviour
    {
        [SerializeField] private SerialControllerPitaco24 serialController;
        [SerializeField] private Sprite offline;
        [SerializeField] private Sprite online;

        private Image statusImage;

        private void Awake()
        {
            statusImage = GetComponent<Image>();
            if (serialController == null)
                serialController = FindObjectOfType<SerialControllerPitaco24>();

            if (serialController == null)
                Debug.LogWarning("[PITACO 2.4] SerialControllerPitaco24 nao encontrado na cena.");
        }

        private void FixedUpdate()
        {
            if (statusImage == null)
                return;

            bool connected = serialController != null && serialController.IsConnected;
            Sprite target = connected ? online : offline;
            if (target != null && statusImage.sprite != target)
                statusImage.sprite = target;
        }

        public void Reboot()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
