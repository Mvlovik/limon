using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Порядок прохождения.
    /// GameFlow читает его и решает, что грузить дальше. Ассеты: SEQ_Act1 и т.д.
    /// </summary>
    [CreateAssetMenu(fileName = "SEQ_NewSequence", menuName = "GameLimon/Story/Story Sequence")]
    public sealed class StorySequence : ScriptableObject
    {
        [SerializeField] private List<SceneDefinition> scenes = new List<SceneDefinition>();

        public int Count => scenes.Count;

        public SceneDefinition GetScene(int index)
        {
            if (index < 0 || index >= scenes.Count)
                return null;

            return scenes[index];
        }

        /// <summary>Индекс сцены по её id, или -1.</summary>
        public int IndexOf(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
                return -1;

            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i] != null && scenes[i].Id == sceneId)
                    return i;

            return -1;
        }
    }
}
