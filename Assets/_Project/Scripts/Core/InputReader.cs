using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLimon.Core
{
    /// <summary>
    /// Единая точка чтения ввода (Input System). Никто больше не лезет в Input напрямую.
    /// Базовый набор действий создаётся кодом; позже можно перевести
    /// на InputSystem_Actions.inputactions (он переедет в Data/Config).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InputReader : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _look;
        private InputAction _interact;
        private InputAction _pause;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }

        public event Action InteractPressed;
        public event Action PausePressed;

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            _move.AddBinding("<Keyboard>/wasd");

            _look = new InputAction("Look", InputActionType.Value, "<Gamepad>/rightStick");
            _look.AddBinding("<Mouse>/delta");

            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _interact.AddBinding("<Gamepad>/buttonSouth");

            _pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");

            _move.performed += OnMove;
            _move.cancelled += OnMoveCancelled;
            _look.performed += OnLook;
            _look.cancelled += OnLookCancelled;
            _interact.performed += OnInteract;
            _pause.performed += OnPause;
        }

        private void OnEnable()
        {
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void OnDestroy()
        {
            _move?.Dispose();
            _look?.Dispose();
            _interact?.Dispose();
            _pause?.Dispose();
        }

        /// <summary>Включить чтение ввода (вызывать при выходе из паузы/катсцены).</summary>
        public void EnableActions()
        {
            _move?.Enable();
            _look?.Enable();
            _interact?.Enable();
            _pause?.Enable();
        }

        /// <summary>Выключить чтение ввода (пауза, катсцена, UI).</summary>
        public void DisableActions()
        {
            _move?.Disable();
            _look?.Disable();
            _interact?.Disable();
            _pause?.Disable();
        }

        private void OnMove(InputAction.CallbackContext context) => Move = context.ReadValue<Vector2>();

        private void OnMoveCancelled(InputAction.CallbackContext context) => Move = Vector2.zero;

        private void OnLook(InputAction.CallbackContext context) => Look = context.ReadValue<Vector2>();

        private void OnLookCancelled(InputAction.CallbackContext context) => Look = Vector2.zero;

        private void OnInteract(InputAction.CallbackContext context) => InteractPressed?.Invoke();

        private void OnPause(InputAction.CallbackContext context) => PausePressed?.Invoke();
    }
}
