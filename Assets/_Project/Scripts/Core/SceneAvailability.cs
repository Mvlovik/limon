using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.Core
{
    /// <summary>
    /// Надёжная проверка «сцену можно загрузить».
    /// Application.CanStreamedLevelBeLoaded в редакторе может не видеть сцены,
    /// добавленные в Build Settings во время сессии, поэтому дополнительно
    /// сверяемся со списком сцен сборки через SceneUtility.
    /// </summary>
    public static class SceneAvailability
    {
        public static bool CanLoad(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            if (Application.CanStreamedLevelBeLoaded(sceneName))
                return true;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (path == sceneName)
                    return true;

                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    return true;
            }

            return false;
        }
    }
}
