using GameLimon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.MainMenu
{

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
                Debug.LogError("[MainMenu] GameFlow не найден");
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
