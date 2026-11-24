using System.Collections;
using GameLimon.Core;
using UnityEngine;
using UnityEngine.Playables;

namespace GameLimon.Staging
{
    /// <summary>
    /// Завершает первую сцену второго акта после телефонного разговора:
    /// оба варианта ответа (промолчать / возразить) доигрывают таймлайн
    /// с затемнением, затем компонент гасит экран и просит GameFlow
    /// загрузить следующую сцену (улицу).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Act2SceneAdvance : MonoBehaviour
    {
        [Tooltip("Сколько подождать после сигнала конца разговора, чтобы доиграло затемнение таймлайна.")]
        [Min(0f)]
        [SerializeField] private float pauseBeforeFade = 1.2f;

        [Tooltip("Длительность финального затемнения перед переходом.")]
        [Min(0f)]
        [SerializeField] private float fadeDuration = 1f;

        private bool _finishing;

        /// <summary>Вызывается последним сигналом таймлайнов «Промолчать» и «Возразить».</summary>
        public void FinishScene()
        {
            if (_finishing)
                return;

            GameRoot root = GameRoot.Instance;
            if (root == null || root.Flow == null)
            {
                Debug.LogWarning("[Act2SceneAdvance] GameFlow не найден — переход пропущен.");
                return;
            }

            _finishing = true;
            StartCoroutine(FinishRoutine(root));
        }

        private IEnumerator FinishRoutine(GameRoot root)
        {
            if (pauseBeforeFade > 0f)
                yield return new WaitForSecondsRealtime(pauseBeforeFade);

            if (root.UI != null && fadeDuration > 0f)
                yield return root.UI.FadeTo(1f, fadeDuration);
            else if (root.UI != null)
                root.UI.SetFade(1f);

            root.Flow.CompleteCurrentScene();
        }

        /// <summary>Проверка для отладки, чтобы сигнал не сработал дважды.</summary>
        public bool IsFinishing => _finishing;
    }
}
