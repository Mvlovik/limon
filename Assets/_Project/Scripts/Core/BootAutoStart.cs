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

            flow.StartGame();
        }
    }
}
