using System;
using GameLimon.Core;
using GameLimon.UI;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Ведёт линейный квест. Активен всегда один квест и ровно один текущий шаг.
    /// Флаг текущего шага - единственный, который может продвинуть квест вперёд.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestDirector : MonoBehaviour
    {
        public static QuestDirector Instance { get; private set; }

        [Tooltip("Строгий режим: флаг будущего шага откатывается и не может перескочить квест " +
                 "Интерактивы и триггеры дополнительно закрываются через requiredStep.")]
        [SerializeField] private bool enforceOrder = true;

        private QuestDefinition _quest;
        private int _stepIndex = -1;
        private GameState _state;

        public bool HasActiveQuest => _quest != null && _stepIndex >= 0;
        public QuestDefinition ActiveQuest => _quest;
        public int CurrentStepIndex => _stepIndex;
        public QuestStep CurrentStep => HasActiveQuest ? _quest.GetStep(_stepIndex) : null;

        /// <summary>Любой активный квест запрещает завершать сцену, пока не выполнен последний шаг</summary>
        public bool BlocksSceneExit => HasActiveQuest;

        public event Action<QuestDefinition> QuestStarted;
        public event Action<QuestDefinition, QuestStep, int> StepStarted;
        public event Action<QuestDefinition, QuestStep, int> StepCompleted;
        public event Action<QuestDefinition> QuestCompleted;

        public static QuestDirector Ensure() {
            if (Instance != null)
                return Instance;

            var host = new GameObject("[QuestDirector]");
            host.AddComponent<QuestDirector>();
            DontDestroyOnLoad(host);
            return Instance;
        }

        /// <summary>
        /// Разрешено ли действие, завершающее шаг с указанным флагом.
        /// </summary>
        public static bool IsStepActive(string completionFlag) {
            if (string.IsNullOrEmpty(completionFlag))
                return true;

            return Instance != null && Instance.IsWaitingFor(completionFlag);
        }

        public bool IsWaitingFor(string completionFlag) {
            if (!HasActiveQuest || string.IsNullOrEmpty(completionFlag))
                return false;

            QuestStep step = CurrentStep;
            return step != null && step.CompletionFlag == completionFlag;
        }

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() {
            Unsubscribe();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Запустить квест. Продолжаем с невыполненного</summary>
        public void StartQuest(QuestDefinition quest) {
            if (quest == null) {
                StopQuest();
                return;
            }

            if (!quest.IsValid(out string error)) {
                Debug.LogError($"[QuestDirector] {error}", quest);
                StopQuest();
                return;
            }

            StopQuest();
            _quest = quest;
            ResolveState();

            if (_state == null) {
                Debug.LogError("[QuestDirector] GameState не найден - квест не сможет продвигаться.", this);
                _quest = null;
                return;
            }

            _state.FlagChanged += OnFlagChanged;
            QuestStarted?.Invoke(quest);

            int firstIncomplete = FindFirstIncomplete();
            if (firstIncomplete < 0) {
                Debug.Log($"[QuestDirector] Квест '{quest.DisplayName}' уже выполнен - повторный запуск пропущен.", quest);
                _state.FlagChanged -= OnFlagChanged;
                _quest = null;
                _stepIndex = -1;
                ObjectiveUI.Instance?.Hide();
                return;
            }

            BeginStep(firstIncomplete);
        }

        /// <summary>Прервать квест без выполнения onCompleted</summary>
        public void StopQuest() {
            Unsubscribe();
            _quest = null;
            _stepIndex = -1;
            ObjectiveUI.Instance?.Hide();
        }

        private void ResolveState() {
            if (_state == null && GameRoot.Instance != null)
                _state = GameRoot.Instance.State;
        }

        private void Unsubscribe()
        {
            if (_state != null)
                _state.FlagChanged -= OnFlagChanged;
        }

        private int FindFirstIncomplete()
        {
            for (int i = 0; i < _quest.Count; i++)
            {
                QuestStep step = _quest.GetStep(i);
                if (step != null && !HasFlag(step.CompletionFlag))
                    return i;
            }

            return -1;
        }

        private bool HasFlag(string flag) {
            return _state != null && !string.IsNullOrEmpty(flag) && _state.GetFlag(flag);
        }

        private void BeginStep(int index) {
            while (index < _quest.Count) {
                QuestStep step = _quest.GetStep(index);
                if (step == null || !HasFlag(step.CompletionFlag))
                    break;

                Debug.Log($"[QuestDirector] Шаг '{step.Title}' уже отмечен флагом {step.CompletionFlag} - засчитан.", _quest);
                StepCompleted?.Invoke(_quest, step, index);
                index++;
            }

            if (index >= _quest.Count) {
                CompleteQuest();
                return;
            }

            _stepIndex = index;
            QuestStep current = _quest.GetStep(index);

            ObjectiveUI.Ensure()?.Show(current.Icon, current.ObjectiveText);

            StepStarted?.Invoke(_quest, current, index);

            if (current.OnStart != null && current.OnStart.Count > 0)
                StoryDirector.Ensure().Run(current.OnStart);
        }

        private void OnFlagChanged(string flag, bool value) {
            if (!value || !HasActiveQuest) return;
            int index = _quest.IndexOfFlag(flag);
            if (index < 0) return;

            if (index == _stepIndex) {
                Advance();
                return;
            }

            if (index < _stepIndex) return;

            if (enforceOrder) {
                Debug.LogWarning(
                    $"[QuestDirector] Попытка пропустить шаг: флаг '{flag}' относится к шагу {index + 1}, " +
                    $"а сейчас активен шаг {_stepIndex + 1}. Флаг откатан.",
                    _quest);

                _state?.SetFlag(flag, false);
            } else {
                Debug.LogWarning(
                    $"[QuestDirector] Флаг будущего шага '{flag}' выставлен заранее. " +
                    "Квест не сдвинется, пока не дойдёт до этого шага.",
                    _quest);
            }
        }

        private void Advance()
        {
            QuestStep completed = CurrentStep;
            int completedIndex = _stepIndex;

            StepCompleted?.Invoke(_quest, completed, completedIndex);

            int next = _stepIndex + 1;
            if (next >= _quest.Count) {
                CompleteQuest();
                return;
            }

            BeginStep(next);
        }

        private void CompleteQuest() {
            QuestDefinition completed = _quest;
            if (completed == null)
                return;

            Unsubscribe();
            _quest = null;
            _stepIndex = -1;

            ObjectiveUI.Instance?.Hide();

            if (completed.OnCompleted != null && completed.OnCompleted.Count > 0)
                StoryDirector.Ensure().Run(completed.OnCompleted);

            QuestCompleted?.Invoke(completed);
            Debug.Log($"[QuestDirector] Квест завершён: {completed.DisplayName}", completed);
        }

        [ContextMenu("Debug/Log Active Quest")]
        private void LogActiveQuest() {
            if (!HasActiveQuest) {
                Debug.Log("[QuestDirector] Активного квеста нет.", this);
                return;
            }

            QuestStep step = CurrentStep;
            Debug.Log(
                $"[QuestDirector] '{_quest.DisplayName}', шаг {_stepIndex + 1}/{_quest.Count}: " +
                $"'{step.Title}', ждём флаг {step.CompletionFlag}.",
                this);
        }
    }
}
