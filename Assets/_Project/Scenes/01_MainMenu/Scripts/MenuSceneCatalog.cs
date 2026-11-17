using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Настраиваемый список сцен, которые показываются в меню "Продолжить".
    /// </summary>
    [CreateAssetMenu(fileName = "MENU_SceneCatalog", menuName = "GameLimon/Main Menu/Scene Catalog")]
    public sealed class MenuSceneCatalog : ScriptableObject
    {
        [SerializeField] private List<MenuSceneEntry> entries = new List<MenuSceneEntry>();

        public IReadOnlyList<MenuSceneEntry> Entries => entries;

        public int Count => entries.Count;

        public MenuSceneEntry Get(int index)
        {
            if (index < 0 || index >= entries.Count)
                return null;

            return entries[index];
        }

        public int IndexOfSceneId(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
                return -1;

            for (int i = 0; i < entries.Count; i++)
            {
                MenuSceneEntry entry = entries[i];
                if (entry == null)
                    continue;

                if (entry.sceneId == sceneId || entry.sceneName == sceneId)
                    return i;
            }

            return -1;
        }

#if UNITY_EDITOR
        public void SetEntries(List<MenuSceneEntry> value)
        {
            entries = value ?? new List<MenuSceneEntry>();
        }
#endif
    }
}
