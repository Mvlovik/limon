using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Помечает строковое поле как флаг шага квеста. В инспекторе показывает
    /// выпадающий список шагов из всех QuestDefinition проекта.
    /// </summary>
    public sealed class QuestStepAttribute : PropertyAttribute
    {
        public string EmptyLabel { get; }

        public QuestStepAttribute(string emptyLabel = "---ЛЮБОЙ---")
        {
            EmptyLabel = emptyLabel;
        }
    }
}
