using System;
using UnityEngine;

namespace GameLimon.Story
{
    public enum StoryActionType
    {
        Wait,
        SetFlag,
        ClearFlag,
        PlayThought,
        PlaySfx,
        Fade,
        CompleteScene,
        RestartScene,
        Log,
        HideObjective
    }

    /// <summary>
    /// Одно действие сюжета: флаг, мысль, звук, затемнение, переход.
    /// </summary>
    [Serializable]
    public sealed class StoryAction
    {
        [SerializeField] private StoryActionType type;

        [Tooltip("Флаг для SetFlag / ClearFlag")]
        [SerializeField] private string flag;

        [Tooltip("Мысль для PlayThought")]
        [SerializeField] private ThoughtSequence thought;

        [Tooltip("Звук для PlaySfx")]
        [SerializeField] private AudioClip clip;

        [Range(0f, 1f)]
        [Tooltip("Громкость звука.")]
        [SerializeField] private float volume = 1f;

        [Tooltip("3D-звук: играть из позиции объекта")]
        [SerializeField] private bool spatial;

        [Tooltip("Wait - пауза. Fade - длительность затемнения")]
        [SerializeField] private float duration;

        [Range(0f, 1f)]
        [Tooltip("Целевая прозрачность затемнения для Fade: 1 - чёрный экран, 0 - прозрачно.")]
        [SerializeField] private float fadeAlpha = 1f;

        [Tooltip("Текст для Log (отладка)")]
        [SerializeField] private string message;

        public StoryActionType Type => type;
        public string Flag => flag;
        public ThoughtSequence Thought => thought;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public bool Spatial => spatial;
        public float Duration => duration;
        public float FadeAlpha => fadeAlpha;
        public string Message => message;
    }
}
