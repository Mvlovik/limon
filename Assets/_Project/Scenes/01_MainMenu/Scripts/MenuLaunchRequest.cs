using UnityEngine;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Передаёт выбранную в меню сцену через загрузку Boot-сцены.
    /// </summary>
    public static class MenuLaunchRequest
    {
        private static string _sceneId;

        public static bool HasPending => !string.IsNullOrEmpty(_sceneId);

        public static void Request(string sceneId)
        {
            _sceneId = sceneId;
        }

        public static string Consume()
        {
            string sceneId = _sceneId;
            _sceneId = null;
            return sceneId;
        }
    }
}
