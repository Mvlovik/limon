using GameLimon.Core;
using UnityEngine;

namespace GameLimon.Staging
{
    /// <summary>
    /// Служебный корень сцены-«превью» (собственные GameRoot, UI, игрок)
    /// нужен только при прямом запуске сцены из редактора.
    /// Когда сцена грузится аддитивно поверх Boot, эти службы дублируют
    /// постоянные — такой корень удаляет себя до инициализации остальных
    /// компонентов, чтобы не перехватывать GameState/сейвы/ввод.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class PreviewSceneServicesGuard : MonoBehaviour
    {
        private void Awake()
        {
            GameRoot root = GameRoot.Instance;

            // Прямой запуск сцены: GameRoot ещё не существует — превью-службы работают.
            if (root == null)
                return;

            // Мы и есть действующий GameRoot (например, сцена загружена первой).
            if (root.transform.root == transform.root)
                return;

            Debug.Log("[PreviewSceneServicesGuard] Boot-службы уже активны — превью-службы отключены.", this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
