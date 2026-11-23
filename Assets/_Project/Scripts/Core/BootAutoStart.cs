using System.Collections.Generic;
using GameLimon.MainMenu;
using GameLimon.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.Core
{
    /// <summary>
    /// Точка входа Boot-сцены.
    /// Пока автозапуск не нужен: без отладочной сцены Boot открывает главное меню.
    /// Запуск конкретной сцены из меню идёт через MenuLaunchRequest (MenuBootIntegration),
    /// а отладочный запуск из редактора — через явно заданную debugStartScene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BootAutoStart : MonoBehaviour
    {
        private const string MenuSceneName = "MainMenu";

        [SerializeField] private bool allowInBuilds;

        [Header("Отладка (только в редакторе)")]
        [Tooltip("С какой сцены начать игру. Пусто — открыть главное меню.")]
        [SerializeField] private SceneDefinition debugStartScene;

        [Tooltip("Флаги, которые нужно выставить до старта. Например, флаги предыдущих сцен, " +
                 "чтобы квесты не считали прогресс потерянным.")]
        [SerializeField] private List<string> debugFlags = new List<string>();

        private void Start()
        {
            if (!Application.isEditor && !allowInBuilds)
                return;

            // Запуск через главное меню: MenuBootIntegration погасит этот
            // компонент и сам запустит запрошенную сцену.
            if (MenuLaunchRequest.HasPending)
                return;

            // Отладочный запуск конкретной сцены — только если она явно задана.
            if (debugStartScene != null)
            {
                GameFlow flow = GameRoot.Instance != null ? GameRoot.Instance.Flow : GetComponent<GameFlow>();
                if (flow == null)
                {
                    Debug.LogWarning("[BootAutoStart] GameFlow не найден");
                    return;
                }

                int startIndex = 0;
                if (flow.Sequence != null)
                {
                    int index = flow.Sequence.IndexOf(debugStartScene.Id);
                    if (index >= 0)
                        startIndex = index;
                    else
                        Debug.LogWarning($"[BootAutoStart] Сцена '{debugStartScene.Id}' не найдена в последовательности. Начинаю с первой.");
                }

                ApplyDebugFlags();
                flow.StartGame(startIndex);
                return;
            }

            // Обычный вход в игру — через главное меню.
            if (!SceneAvailability.CanLoad(MenuSceneName))
            {
                Debug.LogError($"[BootAutoStart] Сцена \"{MenuSceneName}\" отсутствует в Build Settings — меню не открыть.");
                return;
            }

            // Игрок в Boot захватил курсор (PlayerLook). Блокировка курсора
            // переживает смену сцены — в меню возвращаем видимость и кликабельность.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(MenuSceneName, LoadSceneMode.Single);
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
