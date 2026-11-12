using Ibit.Core.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ibit.Core.Oximetry.Placement
{
    // Builds an overlay in active game scenes; no copied Canvas or prefab GUID
    // dependencies. This UI never owns the game pause or the USB connection.
    public sealed class FingerPlacementFeedback : MonoBehaviour
    {
        private FingerPlacementMonitor monitor;
        private FingerPlacementPauseBridge pause;
        private GameObject root;
        private GameObject banner;
        private GameObject modal;
        private Image bannerBackground;
        private RectTransform bannerRect;
        private Text bannerTitle;
        private Text bannerDetail;
        private Text modalTitle;
        private Text modalDetail;
        private Button resumeButton;
        private Text resumeText;
        private Font font;

        private void OnEnable()
        {
            monitor = GetComponent<FingerPlacementMonitor>();
            pause = GetComponent<FingerPlacementPauseBridge>();
            if (monitor != null) monitor.OnStateChanged += OnPlacementChanged;
            if (pause != null) pause.OnPauseOwnershipChanged += Refresh;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            CreateOverlayIfNeeded(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (monitor != null) monitor.OnStateChanged -= OnPlacementChanged;
            if (pause != null) pause.OnPauseOwnershipChanged -= Refresh;
            if (root != null) Destroy(root);
            root = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (root != null) Destroy(root);
            root = null;
            CreateOverlayIfNeeded(scene);
        }

        private void OnPlacementChanged(FingerPlacementState state)
        {
            Refresh();
        }

        private void Update()
        {
            // Covers initialization when the persistent bootstrap is executed
            // after the active scene has already been loaded in the Editor.
            if (root == null && monitor != null && monitor.SessionActive)
                CreateOverlayIfNeeded(SceneManager.GetActiveScene());
        }

        private void CreateOverlayIfNeeded(Scene scene)
        {
            if (monitor == null || !monitor.SessionActive) return;
            if (scene.name != "Plataform" && scene.name != "WaterGame" &&
                scene.name != "CakeGame") return;
            if (root != null) return;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            root = new GameObject("Finger Placement HUD (runtime)",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 31000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            banner = NewUi("Finger placement notification", root.transform);
            bannerRect = banner.GetComponent<RectTransform>();
            bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.87f);
            bannerRect.sizeDelta = new Vector2(770f, 112f);
            bannerRect.anchoredPosition = Vector2.zero;
            bannerBackground = banner.AddComponent<Image>();
            bannerBackground.raycastTarget = false;
            bannerTitle = Label("Title", banner.transform, new Vector2(0.04f, 0.54f),
                new Vector2(0.96f, 0.96f), 32, FontStyle.Bold, TextAnchor.MiddleCenter);
            bannerDetail = Label("Instruction", banner.transform, new Vector2(0.03f, 0.05f),
                new Vector2(0.97f, 0.52f), 23, FontStyle.Normal, TextAnchor.MiddleCenter);

            modal = NewUi("Finger placement pause overlay", root.transform);
            var modalRect = modal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = modalRect.offsetMax = Vector2.zero;
            var shade = modal.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);
            shade.raycastTarget = true;

            var card = NewUi("Position finger card", modal.transform);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(850f, 390f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.13f, 0.17f, 0.22f, 0.98f);

            modalTitle = Label("Paused title", card.transform,
                new Vector2(0.06f, 0.71f), new Vector2(0.94f, 0.93f),
                38, FontStyle.Bold, TextAnchor.MiddleCenter);
            modalDetail = Label("Paused instruction", card.transform,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.69f),
                25, FontStyle.Normal, TextAnchor.MiddleCenter);

            var buttonObject = NewUi("Continue after contact", card.transform);
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.18f);
            buttonRect.sizeDelta = new Vector2(320f, 72f);
            buttonRect.anchoredPosition = Vector2.zero;
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.56f, 0.42f, 1f);
            resumeButton = buttonObject.AddComponent<Button>();
            resumeButton.targetGraphic = image;
            resumeButton.onClick.AddListener(OnResumeClicked);
            resumeText = Label("Continue label", buttonObject.transform,
                Vector2.zero, Vector2.one, 27, FontStyle.Bold, TextAnchor.MiddleCenter);
            resumeText.text = "Retomar jogo";
            resumeText.color = Color.white;

            Refresh();
        }

        private static GameObject NewUi(string title, Transform parent)
        {
            var go = new GameObject(title, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private Text Label(string title, Transform parent, Vector2 min,
            Vector2 max, int size, FontStyle style, TextAnchor alignment)
        {
            var go = NewUi(title, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void OnResumeClicked()
        {
            if (pause != null) pause.TryResume();
            Refresh();
        }

        private void Refresh()
        {
            if (banner == null || modal == null || monitor == null) return;

            var state = monitor.State;
            bool ownsPause = pause != null && pause.OwnsPause;
            bool otherPause = GameManager.HasLegacyPauseRequest ||
                (GameManager.GameIsPaused && !ownsPause);

            // Clinical/manual dialogs must remain visible above this optional
            // positioning dialog, even if our token was acquired first.
            bool showingModal = monitor.SessionActive && ownsPause && !otherPause;
            modal.SetActive(showingModal);
            banner.SetActive(false);

            if (showingModal)
            {
                bool ready = pause.CanResume;
                resumeButton.interactable = ready;
                resumeText.text = ready ? "Retomar jogo" : "Aguardando contato";
                modalTitle.text = "Exercicio pausado: posicione o dedo";
                modalDetail.text = ready
                    ? "Contato estabilizado. Clique para continuar."
                    : state == FingerPlacementState.TelemetryLost ||
                      state == FingerPlacementState.DeviceDisconnected
                        ? "Sem dados opticos. Confira a conexao do PITACO."
                        : "Ajuste o dedo sobre o sensor e mantenha-o parado por 2 s.";
                return;
            }

            if (!monitor.SessionActive || otherPause) return;
            string title = null;
            string detail = "";
            bool red = false;
            bool large = false;

            switch (state)
            {
                case FingerPlacementState.Warning:
                    title = "Reposicione o dedo";
                    detail = "Cubra corretamente a area optica do sensor.";
                    break;
                case FingerPlacementState.Critical:
                case FingerPlacementState.PauseRequired:
                    title = "Atencao: verifique o sensor!";
                    detail = "Nao ha contato optico. Ajuste o dedo e mantenha-o imovel.";
                    red = large = true;
                    break;
                case FingerPlacementState.Recovering:
                    title = "Confirmando contato...";
                    detail = "Mantenha o dedo estavel por 2 segundos.";
                    break;
                case FingerPlacementState.TelemetryLost:
                    title = "Sem dados opticos recentes";
                    detail = "Verifique o PITACO e a conexao USB.";
                    red = true;
                    break;
                case FingerPlacementState.DeviceDisconnected:
                    if (!monitor.HasEverConnected) break;
                    title = "PITACO 2.4 desconectado";
                    detail = "Verifique o cabo e aguarde a reconexao.";
                    red = true;
                    break;
            }

            if (title == null) return;
            banner.SetActive(true);
            bannerRect.sizeDelta = new Vector2(large ? 1000f : 770f,
                large ? 165f : 112f);
            bannerBackground.color = red
                ? new Color(0.78f, 0.16f, 0.13f, 0.96f)
                : new Color(0.98f, 0.78f, 0.23f, 0.96f);
            bannerTitle.color = bannerDetail.color =
                red ? Color.white : new Color(0.16f, 0.12f, 0.05f);
            bannerTitle.fontSize = large ? 40 : 32;
            bannerTitle.text = title;
            bannerDetail.text = detail;
        }
    }
}
