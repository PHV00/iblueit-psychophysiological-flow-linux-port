using Ibit.Core.Serial;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Ibit.MainMenu.UI
{
    [RequireComponent(typeof(Image))]
    public class Pitaco24Status : MonoBehaviour
    {
        [SerializeField] private Sprite offline;
        [SerializeField] private Sprite online;

        private Image statusImage;

        private void Awake()
        {
            statusImage = GetComponent<Image>();
        }

        private void FixedUpdate()
        {
            if (statusImage == null)
                return;

            // A versao fisica vem do manager que possui a unica porta COM.
            // Nao consultar uma possivel instancia de controlador de cena.
            var manager = PitacoConnectionManager.Active;
            bool connected = manager != null &&
                manager.ConnectedVersion == PitacoConnectionManager.Version.Pitaco24;

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
