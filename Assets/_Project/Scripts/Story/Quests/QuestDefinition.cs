using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Линейный квест: шаги выполняются строго по порядку.
    /// Прогресс хранится во флагах GameState, поэтому отдельное состояние квеста
    /// </summary>
    [CreateAssetMenu(fileName = "QST_NewQuest", menuName = "GameLimon/Story/Quest")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [Tooltip("Стабильный id квеста для логов")]
        [SerializeField] private string id;

        [Tooltip("Название для редактора")]
        [SerializeField] private string displayName;

        [Tooltip("Шаги выполняются строго сверху вниз")]
        [SerializeField] private List<QuestStep> steps = new List<QuestStep>();


        [Tooltip("Действия после завершения последнего шага")]
        [SerializeField] private List<StoryAction> onCompleted = new List<StoryAction>();

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? Id : displayName;
        public IReadOnlyList<QuestStep> Steps => steps;
        public IReadOnlyList<StoryAction> OnCompleted => onCompleted;
        public int Count => steps != null ? steps.Count : 0;

        public QuestStep GetStep(int index)
        {
            if (steps == null || index < 0 || index >= steps.Count)
                return null;
            return steps[index];
        }

        /// <summary>Индекс шага с указанным флагом или -1</summary>
        public int IndexOfFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || steps == null)
                return -1;

            for (int i = 0; i < steps.Count; i++)
            {
                QuestStep step = steps[i];
                if (step != null && step.CompletionFlag == flag)
                    return i;
            }

            return -1;
        }

        /// <summary>Проверка данных квеста</summary>
        public bool IsValid(out string error)
        {
            error = null;

            if (Count == 0) {
                error = $"Квест '{name}' не содержит шагов.";
                return false;
            }

            var usedFlags = new HashSet<string>();

            for (int i = 0; i < Count; i++) {
                QuestStep step = steps[i];

                if (step == null) {
                    error = $"Квест '{name}': шаг {i + 1} пустой.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(step.CompletionFlag)) {
                    error = $"Квест '{name}': у шага {i + 1} ('{step.Title}') не задан completionFlag.";
                    return false;
                }

                if (!usedFlags.Add(step.CompletionFlag)) {
                    error = $"Квест '{name}': флаг '{step.CompletionFlag}' повторяется в нескольких шагах.";
                    return false;
                }
            }
            return true;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) id = name;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = id;
            if (IsValid(out string error)) return;

            Debug.LogWarning($"[QuestDefinition] {error}", this);
        }
    }
}
