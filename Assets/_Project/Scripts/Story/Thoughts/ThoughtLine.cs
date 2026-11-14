using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>  Субтитры Assets/_Project/Data/Dialogues/THT_* </summary>
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

        /// <summary>Длительность показа с учётом скорости чтения и длины озвучки</summary>
        public float ResolveDuration(float charsPerSecond)
        {
            float fromText = duration > 0.01f
                ? duration
                : Mathf.Max(1.2f, (string.IsNullOrEmpty(text) ? 0 : text.Length) / Mathf.Max(1f, charsPerSecond));

            if (voice != null)
                fromText = Mathf.Max(fromText, voice.length + 0.2f);

            return fromText;
        }
    }
}
