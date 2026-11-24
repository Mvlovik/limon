using System.Collections;
using System.Collections.Generic;
using GameLimon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Главное меню. Вёрстка лежит в сцене и префабе карточки, контроллер только
    /// переключает панели и заполняет список сцен.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Данные")]
        [Tooltip("Список сцен для экрана выбора.")]
        [SerializeField] private MenuSceneCatalog catalog;

        [Tooltip("Первая сцена новой игры. Если пусто — берётся первая сцена каталога.")]
        [SerializeField] private string newGameSceneName = "A1_S01_Apartment";

        [Tooltip("Boot-сцена, через которую запускается игра (в ней лежат GameRoot и игрок).")]
        [SerializeField] private string bootSceneName = "Boot";

        [Tooltip("Ваша 3D-сцена фона. Загружается аддитивно под UI меню.")]
        [SerializeField] private string backgroundSceneName = "MENU_Background";

        [Tooltip("Режим разработчика: разрешить выбор любой сцены. Если выключено — только пройденные сцены.")]
        [SerializeField] private bool developerUnlockAllScenes = true;

        [Tooltip("Материал с шейдером GameLimon/UI/Grayscale для непройденных сцен.")]
        [SerializeField] private Material lockedPreviewMaterial;

        [Header("UI")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject sceneSelectPanel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Transform sceneGrid;
        [SerializeField] private SceneCardView sceneCardPrefab;
        [SerializeField] private Text sceneHint;
        [SerializeField] private RawImage backgroundImage;

        [Tooltip("Статичная картинка фона меню. Если задана — 3D-сцена фона не загружается.")]
        [SerializeField] private Texture2D backgroundTexture;

        private readonly List<SceneCardView> _cards = new List<SceneCardView>();
        private RenderTexture _backgroundTexture;

        private void Awake()
        {
            // Меню должно быть кликабельным при любом пути входа (из Boot,
            // билда или напрямую): захват курсора игроком переживает смену сцены.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildSceneCards();
            ApplyMenuStyle();
            RefreshState();

            if (backgroundTexture != null && backgroundImage != null)
                backgroundImage.texture = backgroundTexture;
            else
                StartCoroutine(LoadBackgroundRoutine());
        }

        private void OnDestroy()
        {
            if (_backgroundTexture != null)
            {
                _backgroundTexture.Release();
                Destroy(_backgroundTexture);
                _backgroundTexture = null;
            }
        }

        // ------------------------------------------------------------------
        // Кнопки (события назначены в сцене)
        // ------------------------------------------------------------------

        public void OnContinueClicked()
        {
            if (!MenuProgressStore.HasSave())
            {
                RefreshState();
                return;
            }

            ShowSceneSelection();
        }

        public void OnNewGameClicked()
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

        public void OnSettingsClicked()
        {
            Debug.Log("[MainMenu] Настройки пока не реализованы.");
        }

        public void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void ShowSceneSelection()
        {
            if (mainPanel != null)
                mainPanel.SetActive(false);

            if (sceneSelectPanel != null)
                sceneSelectPanel.SetActive(true);

            RefreshState();
        }

        public void ShowMainPanel()
        {
            if (sceneSelectPanel != null)
                sceneSelectPanel.SetActive(false);

            if (mainPanel != null)
                mainPanel.SetActive(true);

            RefreshState();
        }

        // ------------------------------------------------------------------
        // Внутренняя логика
        // ------------------------------------------------------------------

        private void RefreshState()
        {
            bool hasSave = MenuProgressStore.HasSave();
            if (continueButton != null)
                continueButton.gameObject.SetActive(hasSave);

            if (sceneHint != null)
            {
                sceneHint.text = developerUnlockAllScenes
                    ? "Режим разработчика: доступны все сцены"
                    : "Доступны только пройденные сцены";
            }
        }

        private void BuildSceneCards()
        {
            if (sceneCardPrefab == null || sceneGrid == null || catalog == null)
                return;

            for (int i = 0; i < catalog.Count; i++)
            {
                MenuSceneEntry entry = catalog.Get(i);
                if (entry == null)
                    continue;

                SceneCardView card = Instantiate(sceneCardPrefab, sceneGrid);
                bool completed = MenuProgressStore.IsCompleted(catalog, i);
                bool unlocked = developerUnlockAllScenes || completed;
                int index = i;

                card.Bind(entry, completed, unlocked, lockedPreviewMaterial, () => OnSceneSelected(index));
                _cards.Add(card);
            }
        }

        private void ApplyMenuStyle()
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.08f, 0.11f, 0.16f, 0.96f);
                colors.highlightedColor = new Color(0.18f, 0.34f, 0.42f, 1f);
                colors.pressedColor = new Color(0.28f, 0.55f, 0.56f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.05f, 0.06f, 0.09f, 0.55f);
                colors.fadeDuration = 0.12f;
                button.colors = colors;

                Image image = button.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.035f, 0.075f, 0.12f, 0.98f);

                button.transition = Selectable.Transition.ColorTint;
                MenuButtonHover hover = button.GetComponent<MenuButtonHover>();
                if (hover == null)
                    button.gameObject.AddComponent<MenuButtonHover>();

                Outline outline = button.GetComponent<Outline>();
                if (outline == null) outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.36f, 0.86f, 0.82f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);

                Shadow shadow = button.GetComponent<Shadow>();
                if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(0f, -5f);

                Text text = button.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.fontStyle = FontStyle.Bold;
                    text.fontSize = Mathf.Max(text.fontSize, 24);
                    text.color = new Color(0.92f, 0.98f, 0.97f, 1f);
                }
            }
        }

        private void OnSceneSelected(int index)
        {
            MenuSceneEntry entry = catalog != null ? catalog.Get(index) : null;
            if (entry == null)
                return;

            bool unlocked = developerUnlockAllScenes || MenuProgressStore.IsCompleted(catalog, index);
            if (!unlocked)
                return;

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

                gameObject.SetActive(false);
                flow.StartGame(sequenceIndex);
                return;
            }

            if (string.IsNullOrEmpty(entry.sceneName) || !SceneAvailability.CanLoad(entry.sceneName))
            {
                Debug.LogError($"[MainMenu] Сцена '{entry.sceneName}' отсутствует в Build Settings.");
                return;
            }

            if (string.IsNullOrEmpty(bootSceneName) || !SceneAvailability.CanLoad(bootSceneName))
            {
                Debug.LogError($"[MainMenu] Boot-сцена '{bootSceneName}' отсутствует в Build Settings.");
                return;
            }

            // Запускаем через Boot, иначе в игровой сцене не будет GameRoot, игрока и UI.
            MenuLaunchRequest.Request(entry.sceneId);
            if (GameRoot.Instance != null && GameRoot.Instance.UI != null)
                GameRoot.Instance.UI.ShowLoading("Загрузка...");
            SceneManager.LoadScene(bootSceneName, LoadSceneMode.Single);
        }

        private IEnumerator LoadBackgroundRoutine()
        {
            if (string.IsNullOrEmpty(backgroundSceneName))
                yield break;

            Scene background = SceneManager.GetSceneByName(backgroundSceneName);
            if (!background.isLoaded)
            {
                if (!SceneAvailability.CanLoad(backgroundSceneName))
                {
                    Debug.LogWarning($"[MainMenu] Фоновая сцена '{backgroundSceneName}' отсутствует в Build Settings.");
                    yield break;
                }

                yield return SceneManager.LoadSceneAsync(backgroundSceneName, LoadSceneMode.Additive);
                background = SceneManager.GetSceneByName(backgroundSceneName);
            }

            Camera backgroundCamera = null;
            foreach (GameObject root in background.GetRootGameObjects())
            {
                backgroundCamera = root.GetComponentInChildren<Camera>();
                if (backgroundCamera != null)
                    break;
            }

            if (backgroundCamera == null)
            {
                Debug.LogWarning($"[MainMenu] В фоновой сцене '{backgroundSceneName}' нет камеры.");
                yield break;
            }

            // UI рисуется этой же камерой после 3D-сцены, иначе фон может перекрывать кнопки.
            Canvas menuCanvas = GetComponentInChildren<Canvas>(true);
            if (menuCanvas != null)
            {
                menuCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                menuCanvas.worldCamera = backgroundCamera;
                menuCanvas.planeDistance = 1f;
            }

            if (backgroundImage == null)
                yield break;

            _backgroundTexture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24,
                RenderTextureFormat.ARGB32);
            _backgroundTexture.Create();
            backgroundCamera.targetTexture = _backgroundTexture;
            backgroundImage.texture = _backgroundTexture;
        }
    }
}
