using GameLimon.Core;
using UnityEngine;

namespace GameLimon.Interaction
{
    /// <summary>
    /// Универсальный объект осмотреть / включить: ставит флаг в GameState.
    /// </summary>
    public sealed class FlagInteractable : InteractableBase
    {
        [Tooltip("Флаг, который выставляется при взаимодействии. Пример: кофемашина.")]
        [SerializeField] private string flag;

        [Tooltip("Сообщение в консоль - временная замена реплике/озвучке.")]
        [SerializeField] private string debugMessage;

        protected override void OnInteract(GameObject interactor)
        {
            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;

            if (state != null && !string.IsNullOrEmpty(flag))
                state.SetFlag(flag, true);

            if (!string.IsNullOrEmpty(debugMessage))
                Debug.Log($"[Interact] {debugMessage} (flag: {flag})", this);
        }
    }
}
