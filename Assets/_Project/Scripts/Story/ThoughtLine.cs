using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>  Субтитры Assets/_Project/Data/Dialogues/THT_*. </summary>
    [CreateAssetMenu(fileName = "THT_NewLine", menuName = "GameLimon/Story/Thought Line")]
    public sealed class ThoughtLine : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("Текст мысли")]
        [SerializeField] private string text;

        [Tooltip("Сколько держать на экране")]
        [SerializeField] private float duration;

        [Tooltip("Озвучка мысли")]
        [SerializeField] private AudioClip voice;




        public string Text => text;
        public float Duration => duration;
        public AudioClip Voice => voice;

        /// <summary>Длительность показа с учётом скорости чтения</summary>
        public float ResolveDuration(float charsPerSecond)
        {
            if (duration > 0.01f) return duration;
            int length = string.IsNullOrEmpty(text) ? 0 : text.Length;
            return Mathf.Max(1.2f, length / Mathf.Max(1f, charsPerSecond));
        }
    }
}
