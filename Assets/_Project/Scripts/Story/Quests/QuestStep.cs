using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Один шаг линейного квеста. Ключ шага - completionFlag, двигает квест вперёд
    /// </summary>
    [Serializable]
    public sealed class QuestStep
    {
        [Tooltip("Название шага для редактора")]
        [SerializeField] private string title;

        [TextArea(2, 4)]
        [Tooltip("Текст цели на экране")]
        [SerializeField] private string objectiveText;

        [Tooltip("Иконка цели")]
        [SerializeField] private Sprite icon;

        [Tooltip("Флаг, завершающий шаг")]
        [SerializeField] private string completionFlag;

        [Tooltip("Действия в начале шага: мысль, звук, реплика, катсцена.")]
        [SerializeField] private List<StoryAction> onStart = new List<StoryAction>();

        public string Title => string.IsNullOrEmpty(title) ? completionFlag : title;
        public string ObjectiveText => string.IsNullOrEmpty(objectiveText) ? Title : objectiveText;
        public Sprite Icon => icon;
        public string CompletionFlag => completionFlag;
        public IReadOnlyList<StoryAction> OnStart => onStart;
    }
}
