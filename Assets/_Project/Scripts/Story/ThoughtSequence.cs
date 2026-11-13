using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Последовательность мыслей героя Assets/_Project/Data/Dialogues/SEQ_*.
    /// </summary>
    [CreateAssetMenu(fileName = "THS_NewSequence", menuName = "GameLimon/Story/Thought Sequence")]
    public sealed class ThoughtSequence : ScriptableObject
    {
        [SerializeField] private List<ThoughtLine> lines = new List<ThoughtLine>();

        [Header("Музыка")]
        [Tooltip("Музыка, которая запускается вместе с первой репликой.")]
        [SerializeField] private AudioClip music;

        [Tooltip("Остановить музыку после последней реплики.")]
        [SerializeField] private bool stopMusicAfter;

        [Header("Указание игроку")]
        [Tooltip("Указание, которое появится справа сверху вместе с первой репликой.")]
        [SerializeField] private ObjectiveDefinition objective;

        public int Count => lines != null ? lines.Count : 0;
        public AudioClip Music => music;
        public bool StopMusicAfter => stopMusicAfter;
        public ObjectiveDefinition Objective => objective;

        public ThoughtLine GetLine(int index)
        {
            if (lines == null || index < 0 || index >= lines.Count) return null;
            return lines[index];
        }

        /// <summary>Заполнить послдовательность из кода</summary>
        public void SetLines(IEnumerable<ThoughtLine> source)
        {
            lines = new List<ThoughtLine>();
            if (source == null) return;

            foreach (ThoughtLine line in source)
                lines.Add(line);
        }
    }
}
