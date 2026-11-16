using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Story;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Главное меню: «Продолжить», «Новая игра», «Настройки», «Выход».
    /// UI строится кодом, чтобы сцена меню оставалась максимально простой.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string ContinueLabel = "Продолжить";
        private const string NewGameLabel = "Новая игра";
        private const string SettingsLabel = "Настройки";
        private const string ExitLabel = "Выход";
        private const string BackLabel = "Назад";

        [Header("Данные")]
        [Tooltip("Список сцен для экрана выбора.")]
        [SerializeField] private MenuSceneCatalog catalog;

        [Tooltip("Первая сцена новой игры. Если пусто — берётся первая сцена каталога.")]
        [SerializeField] private string newGameSceneName = "A1_S01_Apartment";

        [Tooltip("Boot-сцена, через которую запускается игра (в ней лежат GameRoot и игрок).")]
        [SerializeField] private string bootSceneName = "Boot";

        [Tooltip("Режим разработчика: разрешить выбор любой сцены. Если выключено — только пройденные сцены.")]
        [SerializeField] private bool developerUnlockAllScenes = true;

        [Header("Внешний вид")]
        [Tooltip("Материал с шейдером GameLimon/UI/Grayscale для непройденных сцен.")]
        [SerializeField] private Material lockedPreviewMaterial;

        [SerializeField] private Color backgroundColor = new Color(0.055f, 0.067f, 0.082f, 1f);
        [SerializeField] private Color panelColor = new Color(0.10f, 0.12f, 0.145f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.14f, 0.17f, 0.21f, 1f);
        [SerializeField] private Color buttonHighlightColor = new Color(0.19f, 0.23f, 0.29f, 1f);
        [SerializeField] private Color accentColor = new Color(0.95f, 0.78f, 0.28f, 1f);

        private Font _font;
        private Canvas _canvas;
        private GameObject _mainPanel;
        private GameObject _scenePanel;
        private Button _continueButton;
        private Material _runtimeLockedMaterial;

        private void Awake()
        {
            _font = LoadFont();
            BuildUi();
            RefreshState();
        }

        // ------------------------------------------------------------------
        // Состояние и действия
        // ------------------------------------------------------------------

        private void RefreshState()
        {
            bool hasSave = MenuProgressStore.HasSave();
            if (_continueButton != null)
                _continueButton.gameObject.SetActive(hasSave);
        }

        private void OnContinueClicked()
        {
            if (!MenuProgressStore.HasSave())
            {
                Debug.Log("[MainMenu] Сохранений нет — «Продолжить» скрыто.");
                RefreshState();
                return;
            }

            ShowSceneSelection();
        }

        private void OnNewGameClicked()
        {
            MenuProgressStore.ResetProgress(catalog);
            MenuProgressStore.DeleteSave();

            int startIndex = 0;
            if (catalog != null && !string.IsNullOrEmpty(newGameSceneName))
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    MenuSceneEntry entry = catalog.Get(i);
                    if (entry != null && entry.sceneName == newGameSceneName)
                    {
                        startIndex = i;
                        break;
                    }
                }
            }

            StartScene(startIndex);
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MainMenu] Настройки пока не реализованы.");
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnSceneSelected(int index)
        {
            MenuSceneEntry entry = catalog != null ? catalog.Get(index) : null;
            if (entry == null)
                return;

            bool unlocked = developerUnlockAllScenes || MenuProgressStore.IsCompleted(catalog, index);
            if (!unlocked)
            {
                Debug.Log($"[MainMenu] Сцена «{entry.displayName}» ещё не пройдена.");
                return;
            }

            StartScene(index);
        }

        private void StartScene(int index)
        {
            MenuSceneEntry entry = catalog != null ? catalog.Get(index) : null;
            if (entry == null)
            {
                Debug.LogWarning("[MainMenu] В каталоге нет сцен для запуска.");
                return;
            }

            GameRoot root = GameRoot.Instance;
            GameFlow flow = root != null ? root.Flow : null;

            if (flow != null && flow.Sequence != null)
            {
                int sequenceIndex = flow.Sequence.IndexOf(entry.sceneId);
                if (sequenceIndex < 0)
                {
                    Debug.LogError($"[MainMenu] Сцена '{entry.sceneId}' не найдена в StorySequence.");
                    return;
                }

                HideMenu();
                flow.StartGame(sequenceIndex);
                return;
            }

            if (string.IsNullOrEmpty(entry.sceneName) || !Application.CanStreamedLevelBeLoaded(entry.sceneName))
            {
                Debug.LogError($"[MainMenu] Сцена '{entry.sceneName}' отсутствует в Build Settings.");
                return;
            }

            if (string.IsNullOrEmpty(bootSceneName) || !Application.CanStreamedLevelBeLoaded(bootSceneName))
            {
                Debug.LogError($"[MainMenu] Boot-сцена '{bootSceneName}' отсутствует в Build Settings.");
                return;
            }

            // Запускаем через Boot, иначе в игровой сцене не будет GameRoot, игрока и UI.
            MenuLaunchRequest.Request(entry.sceneId);
            SceneManager.LoadScene(bootSceneName, LoadSceneMode.Single);
        }

        private void ShowSceneSelection()
        {
            _mainPanel.SetActive(false);
            _scenePanel.SetActive(true);
        }

        private void ShowMainPanel()
        {
            _scenePanel.SetActive(false);
            _mainPanel.SetActive(true);
            RefreshState();
        }

        private void HideMenu()
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Построение UI
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var background = CreateImage(canvasObject.transform, "Background", backgroundColor);
            Stretch(background.rectTransform, 0, 0, 0, 0);

            var stripe = CreateImage(canvasObject.transform, "AccentStripe", accentColor);
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.anchoredPosition = new Vector2(64f, 0f);
            stripe.rectTransform.sizeDelta = new Vector2(4f, -180f);

            _mainPanel = BuildMainPanel(canvasObject.transform);
            _scenePanel = BuildScenePanel(canvasObject.transform);
            _scenePanel.SetActive(false);
        }

        private GameObject BuildMainPanel(Transform parent)
        {
            GameObject panel = NewUiObject("MainPanel", parent);
            Stretch(panel.GetComponent<RectTransform>(), 0, 0, 0, 0);

            Text title = CreateText(panel.transform, "Title", "ЛИМОН", 78, FontStyle.Bold,
                TextAnchor.UpperLeft, Color.white);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(120f, -84f), new Vector2(800f, 96f));

            Text subtitle = CreateText(panel.transform, "Subtitle", "ГЛАВНОЕ МЕНЮ", 24, FontStyle.Normal,
                TextAnchor.UpperLeft, new Color(0.66f, 0.70f, 0.76f, 1f));
            SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(124f, -176f), new Vector2(800f, 36f));

            GameObject buttons = NewUiObject("Buttons", panel.transform);
            var buttonsRect = buttons.GetComponent<RectTransform>();
            SetRect(buttonsRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(120f, -30f), new Vector2(440f, 420f));

            var layout = buttons.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _continueButton = CreateMenuButton(buttons.transform, ContinueLabel, OnContinueClicked);
            CreateMenuButton(buttons.transform, NewGameLabel, OnNewGameClicked);
            CreateMenuButton(buttons.transform, SettingsLabel, OnSettingsClicked);
            CreateMenuButton(buttons.transform, ExitLabel, OnExitClicked);

            Text footer = CreateText(panel.transform, "Footer", "GameLimon", 20, FontStyle.Normal,
                TextAnchor.LowerRight, new Color(0.45f, 0.49f, 0.55f, 1f));
            SetRect(footer.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-64f, 42f), new Vector2(400f, 30f));

            return panel;
        }

        private GameObject BuildScenePanel(Transform parent)
        {
            GameObject panel = NewUiObject("SceneSelectPanel", parent);
            Stretch(panel.GetComponent<RectTransform>(), 0, 0, 0, 0);

            Button back = CreateSmallButton(panel.transform, BackLabel, ShowMainPanel);
            SetRect(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(64f, -56f), new Vector2(170f, 54f));

            Text title = CreateText(panel.transform, "Title", "ВЫБОР СЦЕНЫ", 44, FontStyle.Bold,
                TextAnchor.UpperCenter, Color.white);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(900f, 60f));

            Text hint = CreateText(panel.transform, "Hint",
                developerUnlockAllScenes
                    ? "Режим разработчика: доступны все сцены"
                    : "Доступны только пройденные сцены",
                20, FontStyle.Normal, TextAnchor.UpperCenter, new Color(0.62f, 0.66f, 0.72f, 1f));
            SetRect(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -116f), new Vector2(900f, 30f));

            GameObject scrollObject = NewUiObject("Scroll", panel.transform);
            Stretch(scrollObject.GetComponent<RectTransform>(), 110f, 60f, 110f, 170f);

            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            GameObject viewport = NewUiObject("Viewport", scrollObject.transform);
            Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 0, 0);
            viewport.AddComponent<RectMask2D>();

            GameObject content = NewUiObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(400f, 280f);
            grid.spacing = new Vector2(28f, 28f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            if (catalog == null || catalog.Count == 0)
            {
                Text empty = CreateText(content.transform, "Empty", "Каталог сцен не настроен", 26,
                    FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.6f, 0.64f, 0.7f, 1f));
                SetRect(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 60f));
            }
            else
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    MenuSceneEntry entry = catalog.Get(i);
                    if (entry != null)
                        BuildSceneCard(content.transform, entry, i);
                }
            }

            return panel;
        }

        private void BuildSceneCard(Transform parent, MenuSceneEntry entry, int index)
        {
            bool completed = MenuProgressStore.IsCompleted(catalog, index);
            bool unlocked = developerUnlockAllScenes || completed;

            GameObject card = NewUiObject("Card_" + entry.sceneId, parent);
            var cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.12f, 0.14f, 0.17f, 1f);

            var button = card.AddComponent<Button>();
            button.targetGraphic = cardImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            button.colors = colors;
            button.interactable = unlocked;
            button.onClick.AddListener(() => OnSceneSelected(index));

            var previewObject = NewUiObject("Preview", card.transform);
            Stretch(previewObject.GetComponent<RectTransform>(), 0, 0, 0, 0);
            var previewImage = previewObject.AddComponent<Image>();
            previewImage.sprite = entry.preview;
            previewImage.preserveAspect = false;
            previewImage.raycastTarget = false;
            if (!completed)
                previewImage.material = GetLockedMaterial();

            var topOverlay = CreateImage(card.transform, "TopOverlay", new Color(0f, 0f, 0f, 0.55f));
            var topRect = topOverlay.rectTransform;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 78f);

            var bottomOverlay = CreateImage(card.transform, "BottomOverlay", new Color(0f, 0f, 0f, 0.68f));
            var bottomRect = bottomOverlay.rectTransform;
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 96f);

            Text act = CreateText(card.transform, "Act", entry.actTitle, 28, FontStyle.Bold,
                TextAnchor.MiddleLeft, accentColor);
            SetRect(act.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -16f), new Vector2(240f, 44f));

            Text state = CreateText(card.transform, "State", completed ? "ПРОЙДЕНО" : "НЕ ПРОЙДЕНО", 17,
                FontStyle.Bold, TextAnchor.MiddleRight,
                completed ? new Color(0.52f, 0.86f, 0.55f, 1f) : new Color(0.72f, 0.74f, 0.78f, 1f));
            SetRect(state.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(200f, 32f));

            Text name = CreateText(card.transform, "Name", entry.displayName, 30, FontStyle.Bold,
                TextAnchor.MiddleLeft, Color.white);
            SetRect(name.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 18f), new Vector2(360f, 64f));
        }

        // ------------------------------------------------------------------
        // Помощники UI
        // ------------------------------------------------------------------

        private Button CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = NewUiObject("Button_" + label, parent);
            var image = buttonObject.AddComponent<Image>();
            image.color = buttonColor;

            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 70f;
            layoutElement.minHeight = 70f;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            var highlight = CreateImage(buttonObject.transform, "Highlight", buttonHighlightColor);
            var highlightRect = highlight.rectTransform;
            highlightRect.anchorMin = new Vector2(0f, 0f);
            highlightRect.anchorMax = new Vector2(0f, 1f);
            highlightRect.pivot = new Vector2(0f, 0.5f);
            highlightRect.anchoredPosition = new Vector2(0f, 0f);
            highlightRect.sizeDelta = new Vector2(6f, 0f);
            highlight.raycastTarget = false;

            Text text = CreateText(buttonObject.transform, "Label", label, 28, FontStyle.Normal,
                TextAnchor.MiddleLeft, Color.white);
            Stretch(text.rectTransform, 32f, 0f, 24f, 0f);

            return button;
        }

        private Button CreateSmallButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = NewUiObject("Button_" + label, parent);
            var image = buttonObject.AddComponent<Image>();
            image.color = buttonColor;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;
            button.onClick.AddListener(onClick);

            Text text = CreateText(buttonObject.transform, "Label", label, 22, FontStyle.Normal,
                TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

            return button;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = NewUiObject(name, parent);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            TextAnchor alignment, Color color)
        {
            GameObject textObject = NewUiObject(name, parent);
            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private Material GetLockedMaterial()
        {
            if (lockedPreviewMaterial != null)
                return lockedPreviewMaterial;

            if (_runtimeLockedMaterial != null)
                return _runtimeLockedMaterial;

            Shader shader = Shader.Find("GameLimon/UI/Grayscale");
            if (shader == null)
            {
                Debug.LogWarning("[MainMenu] Шейдер GameLimon/UI/Grayscale не найден.");
                return null;
            }

            _runtimeLockedMaterial = new Material(shader);
            return _runtimeLockedMaterial;
        }

        private Font LoadFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return font;
        }

        private static GameObject NewUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
