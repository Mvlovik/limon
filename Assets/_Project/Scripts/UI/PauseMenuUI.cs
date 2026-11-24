using GameLimon.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameLimon.UI
{
    /// <summary>
    /// Меню паузы по ESC во время игры. Живёт на GameRoot в Boot, поэтому
    /// работает во всех игровых сценах и не появляется в главном меню.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuUI : MonoBehaviour
    {
        private const string FontResource = "LegacyRuntime.ttf";
        private const int CanvasOrder = 3000;

        private CanvasGroup _group;
        private bool _open;

        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInteractor _interactor;
        private bool _motorWasLocked;
        private bool _lookWasLocked;
        private bool _interactorWasEnabled;
        private bool _captured;

        private void Awake()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (_open)
                Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            // В главном меню пауза не нужна (компонент живёт только в Boot).
            if (SceneManager.GetActiveScene().name == "MainMenu")
                return;

            Toggle();
        }

        public void Toggle()
        {
            if (_open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (_open)
                return;

            CapturePlayer();
            _open = true;

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_motor != null) _motor.SetControlLocked(true);
            if (_look != null) _look.PoseLocked = true;
            if (_interactor != null) _interactor.enabled = false;

            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            _group.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (!_open)
                return;

            _open = false;
            Time.timeScale = 1f;

            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (_interactor != null) _interactor.enabled = _interactorWasEnabled;
            if (_motor != null) _motor.SetControlLocked(_motorWasLocked);

            if (_look != null)
            {
                _look.PoseLocked = _lookWasLocked;
                if (!_lookWasLocked)
                {
                    _look.ResyncPose();
                    _look.LockCursor(true);
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            _captured = false;
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CapturePlayer()
        {
            _captured = true;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                _motor = null;
                _look = null;
                _interactor = null;
                return;
            }

            _motor = player.GetComponent<PlayerMotor>();
            _look = player.GetComponent<PlayerLook>();
            _interactor = player.GetComponent<PlayerInteractor>();
            _motorWasLocked = _motor != null && _motor.ControlLocked;
            _lookWasLocked = _look != null && _look.PoseLocked;
            _interactorWasEnabled = _interactor != null && _interactor.enabled;
        }

        private void Build()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("PauseCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var panelObject = new GameObject("PausePanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);

            Image backdrop = panelObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.78f);
            backdrop.raycastTarget = true;

            _group = panelObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            RectTransform panelRect = (RectTransform)panelObject.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Text title = CreateText(panelObject.transform, "Title", "ПАУЗА", 64, new Vector2(0f, 210f));
            title.color = new Color(0.92f, 0.9f, 0.84f, 1f);

            CreateButton(panelObject.transform, "Продолжить", new Vector2(0f, 70f), Close);
            CreateButton(panelObject.transform, "В главное меню", new Vector2(0f, -30f), GoToMainMenu);
            CreateButton(panelObject.transform, "Выйти из игры", new Vector2(0f, -130f), QuitGame);

            Text hint = CreateText(panelObject.transform, "Hint", "ESC — закрыть меню", 22, new Vector2(0f, -260f));
            hint.color = new Color(0.75f, 0.75f, 0.72f, 0.9f);
        }

        private static Text CreateText(Transform parent, string objectName, string value, int size, Vector2 position)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>(FontResource);
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 80f);
            rect.anchoredPosition = position;
            return text;
        }

        private static void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 72f);
            rect.anchoredPosition = position;

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.17f, 0.21f, 1f);
            colors.highlightedColor = new Color(0.32f, 0.25f, 0.15f, 1f);
            colors.pressedColor = new Color(0.55f, 0.36f, 0.12f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(go.transform, false);

            Text text = labelObject.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>(FontResource);
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.93f, 0.92f, 0.88f, 1f);
            text.raycastTarget = false;

            RectTransform labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemObject = new GameObject("[PauseEventSystem]");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
