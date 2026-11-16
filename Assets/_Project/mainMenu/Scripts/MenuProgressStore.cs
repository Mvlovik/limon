using System.IO;
using GameLimon.Save;
using UnityEngine;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Прогресс для меню: какие сцены уже пройдены и есть ли сохранение.
    /// Пройденные сцены хранятся в PlayerPrefs, дополнительно учитывается
    /// текущая сцена из файла сохранения (всё, что было до неё, считается пройденным).
    /// </summary>
    public static class MenuProgressStore
    {
        private const string CompletedPrefix = "GameLimon.Menu.Completed.";

        public static bool HasSave()
        {
            return File.Exists(SaveService.GetSavePath(0));
        }

        public static bool IsCompleted(MenuSceneCatalog catalog, int index)
        {
            MenuSceneEntry entry = catalog != null ? catalog.Get(index) : null;
            if (entry == null)
                return false;

            if (PlayerPrefs.GetInt(GetCompletedKey(entry), 0) == 1)
                return true;

            int savedIndex = GetSavedSceneIndex(catalog);
            return savedIndex >= 0 && index < savedIndex;
        }

        public static void MarkCompleted(MenuSceneEntry entry)
        {
            if (entry == null)
                return;

            PlayerPrefs.SetInt(GetCompletedKey(entry), 1);
            PlayerPrefs.Save();
        }

        public static void ResetProgress(MenuSceneCatalog catalog)
        {
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    MenuSceneEntry entry = catalog.Get(i);
                    if (entry != null)
                        PlayerPrefs.DeleteKey(GetCompletedKey(entry));
                }
            }

            PlayerPrefs.Save();
        }

        public static void DeleteSave()
        {
            string path = SaveService.GetSavePath(0);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static int GetSavedSceneIndex(MenuSceneCatalog catalog)
        {
            if (catalog == null || !HasSave())
                return -1;

            try
            {
                string json = File.ReadAllText(SaveService.GetSavePath(0));
                GameStateSnapshot snapshot = JsonUtility.FromJson<GameStateSnapshot>(json);
                if (snapshot == null)
                    return -1;

                return catalog.IndexOfSceneId(snapshot.sceneId);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[MainMenu] Не удалось прочитать сохранение: {exception.Message}");
                return -1;
            }
        }

        private static string GetCompletedKey(MenuSceneEntry entry)
        {
            string id = string.IsNullOrEmpty(entry.sceneId) ? entry.sceneName : entry.sceneId;
            return CompletedPrefix + id;
        }
    }
}
