using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLimon.Core
{
    [DisallowMultipleComponent]
    public sealed class InputReader : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _look;
        private InputAction _interact;
        private InputAction _pause;
        private InputAction _crouch;
        private InputAction _sprint;
        private InputAction _jump;

        private Vector2 _lookValue;

        public Vector2 Move { get; private set; }

        /// <summary>Накопленное смещение мыши/стика с прошлого кадра.</summary>
        public Vector2 Look => _lookValue;

        /// <summary>Удерживается ли Ctrl/кнопка приседа.</summary>
        public bool CrouchHeld { get; private set; }

        /// <summary>Удерживается ли Shift/кнопка спринта.</summary>
        public bool SprintHeld { get; private set; }

        public event Action InteractPressed;
        public event Action PausePressed;
        public event Action JumpPressed;

        private void Awake()
        {
            // "<Keyboard>/wasd" — не существует как control path: нужен явный 2D-композит.
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _look = new InputAction("Look", InputActionType.Value, "<Gamepad>/rightStick");
            _look.AddBinding("<Mouse>/delta");

            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _interact.AddBinding("<Gamepad>/buttonWest");

            _pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");

            // leftCtrl/rightCtrl — валидные пути; "<Keyboard>/ctrl" не существует.
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _crouch.AddBinding("<Keyboard>/leftCtrl");
            _crouch.AddBinding("<Keyboard>/rightCtrl");
            _crouch.AddBinding("<Gamepad>/buttonEast");

            _sprint = new InputAction("Sprint", InputActionType.Button);
            _sprint.AddBinding("<Keyboard>/leftShift");
            _sprint.AddBinding("<Keyboard>/rightShift");
            _sprint.AddBinding("<Gamepad>/leftStickPress");

            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _move.performed += OnMove;
            _move.canceled += OnMoveCancelled;
            _look.performed += OnLook;
            _interact.performed += OnInteract;
            _pause.performed += OnPause;
            _crouch.performed += OnCrouch;
            _crouch.canceled += OnCrouchReleased;
            _sprint.performed += OnSprint;
            _sprint.canceled += OnSprintReleased;
            _jump.performed += OnJump;


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
            _crouch?.Dispose();
            _sprint?.Dispose();
            _jump?.Dispose();
        }

        /// <summary>Включить чтение ввода (вызывать при выходе из паузы/катсцены).</summary>
        public void EnableActions()
        {
            _move?.Enable();
            _look?.Enable();
            _interact?.Enable();
            _pause?.Enable();
            _crouch?.Enable();
            _sprint?.Enable();
            _jump?.Enable();
        }

        /// <summary>Выключить чтение ввода (пауза, катсцена, UI).</summary>
        public void DisableActions()
        {
            _move?.Disable();
            _look?.Disable();
            _interact?.Disable();
            _pause?.Disable();
            _crouch?.Disable();
            _sprint?.Disable();
            _jump?.Disable();
            CrouchHeld = false;
            SprintHeld = false;
        }

        private void OnMove(InputAction.CallbackContext context) => Move = context.ReadValue<Vector2>();

        private void OnMoveCancelled(InputAction.CallbackContext context) => Move = Vector2.zero;

        private void OnLook(InputAction.CallbackContext context) => _lookValue += context.ReadValue<Vector2>();

        public Vector2 ConsumeLook()
        {
            Vector2 value = _lookValue;
            _lookValue = Vector2.zero;
            return value;
        }

        private void OnInteract(InputAction.CallbackContext context) => InteractPressed?.Invoke();

        private void OnPause(InputAction.CallbackContext context) => PausePressed?.Invoke();

        private void OnCrouch(InputAction.CallbackContext context) => CrouchHeld = true;

        private void OnCrouchReleased(InputAction.CallbackContext context) => CrouchHeld = false;

        private void OnSprint(InputAction.CallbackContext context) => SprintHeld = true;

        private void OnSprintReleased(InputAction.CallbackContext context) => SprintHeld = false;

        private void OnJump(InputAction.CallbackContext context) => JumpPressed?.Invoke();
    }
}
