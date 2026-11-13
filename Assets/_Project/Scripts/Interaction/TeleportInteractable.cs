using GameLimon.Core;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Interaction
{
    /// <summary>
    /// Переносит игрока в заданную точку (например, из квартиры на улицу).
    /// </summary>
    public sealed class TeleportInteractable : InteractableBase
    {
        [Tooltip("Точка, в которую переносится игрок. Поворот точки задаёт взгляд игрока.")]
        [SerializeField] private Transform destination;

        [Tooltip("Необязательный флаг, выставляемый при телепортации.")]
        [SerializeField] private string flag;

        [Tooltip("Сообщение в консоль - временная замена реплике/озвучке.")]
        [SerializeField] private string debugMessage;

        protected override void OnInteract(GameObject interactor)
        {
            if (destination == null)
            {
                Debug.LogWarning("[TeleportInteractable] Не задана точка телепортации.", this);
                return;
            }

            if (interactor == null)
                return;

            CharacterController controller = interactor.GetComponent<CharacterController>();
            Vector3 position = destination.position;
            Quaternion rotation = Quaternion.Euler(0f, destination.rotation.eulerAngles.y, 0f);

            if (controller != null)
                controller.enabled = false;

            interactor.transform.SetPositionAndRotation(position, rotation);

            if (controller != null)
                controller.enabled = true;

            // Гасим инерцию, чтобы после телепорта игрок не «доехал» по прошлой скорости.
            interactor.GetComponent<PlayerMotor>()?.ResetMomentum();

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;

            if (state != null && !string.IsNullOrEmpty(flag))
                state.SetFlag(flag, true);

            if (!string.IsNullOrEmpty(debugMessage))
                Debug.Log($"[Interact] {debugMessage}", this);
        }
    }
}
