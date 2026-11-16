using GameLimon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Если игру запустили из меню, после загрузки Boot сразу стартуем выбранную сцену,
    /// не давая BootAutoStart запустить первую сцену по умолчанию.
    /// </summary>
    internal static class MenuBootIntegration
    {
        private const string BootSceneName = "Boot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!MenuLaunchRequest.HasPending || scene.name != BootSceneName)
                return;

            string sceneId = MenuLaunchRequest.Consume();

            BootAutoStart autoStart = Object.FindAnyObjectByType<BootAutoStart>();
            if (autoStart != null)
                autoStart.enabled = false;

            GameRoot root = GameRoot.Instance;
            GameFlow flow = root != null ? root.Flow : null;
            if (flow == null || flow.Sequence == null)
            {
                Debug.LogError("[MainMenu] Boot загружен, но GameFlow не найден.");
                return;
            }

            int index = flow.Sequence.IndexOf(sceneId);
            if (index < 0)
            {
                Debug.LogError($"[MainMenu] Сцена '{sceneId}' не найдена в StorySequence.");
                return;
            }

            flow.StartGame(index);
        }
    }
}
