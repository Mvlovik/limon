using System;
using System.Collections;
using GameLimon.Audio;
using GameLimon.Save;
using GameLimon.Story;
using GameLimon.UI;
using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// Читает StorySequence и решает, какая сцена следующая
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlow : MonoBehaviour
    {
        [Tooltip("Порядок сцен. Задаётся в Data/Story/SEQ_*")]
        [SerializeField] private StorySequence sequence;

        [Tooltip("Сохранять чекпоинт на старте каждой сцены")]
        [SerializeField] private bool saveOnSceneStart = true;

        [Tooltip("Время затемнения при переходах")]
        [SerializeField] private float fadeDuration = 0.5f;

        private int _currentIndex = -1;
        private string _currentSceneName;
        private bool _isTransitioning;

        public event Action GameFinished;

        public StorySequence Sequence => sequence;
        public int CurrentIndex => _currentIndex;
        public bool IsTransitioning => _isTransitioning;

   
        public void StartGame()
        {
            if (!ValidateSequence())
                return;

            _currentIndex = 0;
            StartCoroutine(TransitionRoutine(null, sequence.GetScene(0)));
        }

        /// <summary>Текущая сцена закончилась. Что грузить дальше решает GameFlow</summary>
        public void CompleteCurrentScene()
        {
            if (QuestDirector.Instance != null && QuestDirector.Instance.BlocksSceneExit)
            {
                Debug.LogWarning(
                    $"[GameFlow] Нельзя завершить сцену: квест '{QuestDirector.Instance.ActiveQuest.DisplayName}' " +
                    $"ещё на шаге {QuestDirector.Instance.CurrentStepIndex + 1}",
                    QuestDirector.Instance.ActiveQuest);
                return;
            }

            if (!ValidateSequence() || _currentIndex < 0)
            {
                Debug.LogWarning("[GameFlow] StartGame не запускался");
                return;
            }

            int nextIndex = _currentIndex + 1;
            if (nextIndex >= sequence.Count)
            {
                Debug.Log("[GameFlow] Последняя сцена завершена");
                _currentIndex = -1;
                _currentSceneName = null;
                GameFinished?.Invoke();
                return;
            }

            _currentIndex = nextIndex;
            StartCoroutine(TransitionRoutine(_currentSceneName, sequence.GetScene(nextIndex)));
        }

        /// <summary>Перезапустить текгщую сцену</summary>
        public void RestartCurrentScene()
        {
            if (!ValidateSequence() || _currentIndex < 0) return;
            StartCoroutine(TransitionRoutine(_currentSceneName, sequence.GetScene(_currentIndex)));
        }

        /// <summary>Восстановить состояние и грузить сцену из сейва</summary>
        public void ContinueFromSave(GameStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[GameFlow] ContinueFromSave: сейв пуст.");
                return;
            }

            if (!ValidateSequence())
                return;

            int index = sequence.IndexOf(snapshot.sceneId);
            if (index < 0)
            {
                Debug.LogWarning($"[GameFlow] Сцена {snapshot.sceneId} из сейва не найдена в {sequence.name}.");
                return;
            }

            _currentIndex = index;
            GameRoot.Instance.State.ApplySnapshot(snapshot);
            StartCoroutine(TransitionRoutine(null, sequence.GetScene(index)));
        }

        private bool ValidateSequence()
        {
            if (sequence != null && sequence.Count > 0) return true;
            Debug.LogWarning("[GameFlow] StorySequence не назначена или пуста");
            return false;
        }

        private IEnumerator TransitionRoutine(string sceneToUnload, SceneDefinition next)
        {
            if (next == null)
            {
                Debug.LogWarning("[GameFlow] Пустая SceneDefinition");
                yield break;
            }

            if (_isTransitioning)
            {
                Debug.LogWarning("[GameFlow] Переход уже идёт");
                yield break;
            }

            _isTransitioning = true;


            // Не выгружаем текущую сцену, если следующей нет в Build Settings.
            if (!Application.CanStreamedLevelBeLoaded(next.SceneName))
            {
                Debug.LogError($"[GameFlow] Сцена \"{next.SceneName}\" ({next.Id}) отсутствует в Build Settings - перехода нет(");
                _isTransitioning = false;
                yield break;
            }

            GameRoot root = GameRoot.Instance;
            UIRoot ui = root != null ? root.UI : null;
            SceneLoader loader = root != null ? root.Loader : null;
            GameState state = root != null ? root.State : null;
            AudioDirector audio = root != null ? root.Audio : null;

            if (ui != null) yield return ui.FadeTo(1f, fadeDuration);

            // Это чтоб музыка прерывалась при переходу на другую сцену
            if (audio != null)
                audio.StopMusic(fadeDuration);

            if (loader == null)
            {
                Debug.LogError("[GameFlow] SceneLoader не найден на GameRoot");
                _isTransitioning = false;
                yield break;
            }

            if (!string.IsNullOrEmpty(sceneToUnload)) 
                yield return loader.Unload(sceneToUnload);

            yield return loader.LoadAdditive(next.SceneName);

            if (!loader.IsLoaded(next.SceneName))
            {
                Debug.LogError($"[GameFlow] Сцена {next.SceneName} не загрузилась");
                if (ui != null)
                    yield return ui.FadeTo(0f, fadeDuration);
                _isTransitioning = false;
                yield break;
            }

            _currentSceneName = next.SceneName;

            if (state != null)
            {
                state.SetCurrentScene(next.Id);
                state.SetCheckpoint(next.CheckpointId);

                if (saveOnSceneStart && root != null && root.Save != null)
                    root.Save.Save(state.CaptureSnapshot());
            }

            if (next.Quest != null)
                QuestDirector.Ensure().StartQuest(next.Quest);
            else if (QuestDirector.Instance != null)
                QuestDirector.Instance.StopQuest();

            if (ui != null)
                yield return ui.FadeTo(0f, fadeDuration);

            _isTransitioning = false;
        }
    }
}
