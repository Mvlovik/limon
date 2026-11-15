using UnityEngine;
using UnityEngine.Events;

namespace GameLimon.Story
{
    /// <summary>
    /// Универсальный слушатель шага квеста. Позволяет навешивать любые действия
    /// из инспектора на начало и завершение выбранного шага, не меняя QuestDirector.
    ///
    /// Пример: включить свет, запустить звук, открыть дверь, активировать объект.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestStepListener : MonoBehaviour
    {
        [Tooltip("Шаг, за которым следит компонент")]
        [QuestStep]
        [SerializeField] private string stepFlag;

        [Tooltip("Вызвать onStepStarted сразу, если шаг уже активен при включении объекта")]
        [SerializeField] private bool invokeIfAlreadyActive = true;

        [Tooltip("Вызывается, когда квест вошёл в выбранный шаг")]
        [SerializeField] private UnityEvent onStepStarted = new UnityEvent();

        [Tooltip("Вызывается, когда выбранный шаг завершился")]
        [SerializeField] private UnityEvent onStepCompleted = new UnityEvent();

        private QuestDirector _director;

        private void OnEnable() {
            _director = QuestDirector.Ensure();
            _director.StepStarted += HandleStepStarted;
            _director.StepCompleted += HandleStepCompleted;

            if (invokeIfAlreadyActive && IsCurrentStep())
                onStepStarted.Invoke();
        }

        private void OnDisable() {
            if (_director == null) return;

            _director.StepStarted -= HandleStepStarted;
            _director.StepCompleted -= HandleStepCompleted;
            _director = null;
        }

        private void HandleStepStarted(QuestDefinition quest, QuestStep step, int index) {
            if (step != null && step.CompletionFlag == stepFlag)
                onStepStarted.Invoke();
        }

        private void HandleStepCompleted(QuestDefinition quest, QuestStep step, int index) {
            if (step != null && step.CompletionFlag == stepFlag)
                onStepCompleted.Invoke();
        }

        private bool IsCurrentStep() {
            return _director != null && _director.IsWaitingFor(stepFlag);
        }
    }
}
