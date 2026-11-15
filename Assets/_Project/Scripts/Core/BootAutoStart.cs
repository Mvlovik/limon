using System.Collections.Generic;
using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// !!!!!!!!!!!!!!!!!! Временный автозапуск игры из Boot
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootAutoStart : MonoBehaviour
    {
        [SerializeField] private bool allowInBuilds;

        [Header("Отладка (только в редакторе)")]
        [Tooltip("С какой сцены начать игру. Пусто - с первой сцены последовательности.")]
        [SerializeField] private SceneDefinition debugStartScene;

        [Tooltip("Флаги, которые нужно выставить до старта. Например, флаги предыдущих сцен, " +
                 "чтобы квесты не считали прогресс потерянным.")]
        [SerializeField] private List<string> debugFlags = new List<string>();

        private void Start()
        {
            if (!Application.isEditor && !allowInBuilds)
                return;

            GameFlow flow = GameRoot.Instance != null ? GameRoot.Instance.Flow : GetComponent<GameFlow>();
            if (flow == null)
            {
                Debug.LogWarning("[BootAutoStart] GameFlow не найден");
                return;
            }

            ApplyDebugFlags();

            int startIndex = 0;
            if (debugStartScene != null && flow.Sequence != null)
            {
                int index = flow.Sequence.IndexOf(debugStartScene.Id);
                if (index >= 0)
                    startIndex = index;
                else
                    Debug.LogWarning($"[BootAutoStart] Сцена '{debugStartScene.Id}' не найдена в последовательности. Начинаю с первой.");
            }

            flow.StartGame(startIndex);
        }

        private void ApplyDebugFlags()
        {
            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (state == null)
                return;

            foreach (string flag in debugFlags)
            {
                if (!string.IsNullOrWhiteSpace(flag))
                    state.SetFlag(flag, true);
            }
        }
    }
}
