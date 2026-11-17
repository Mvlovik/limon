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

        private readonly List<SceneCardView> _cards = new List<SceneCardView>();
        private RenderTexture _backgroundTexture;

        private void Awake()
        {
            BuildSceneCards();
            RefreshState();
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
