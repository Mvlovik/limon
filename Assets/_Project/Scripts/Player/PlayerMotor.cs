using GameLimon.Core;
using UnityEngine;

namespace GameLimon.Player
{
    /// <summary>
    /// Движение от первого лица на CharacterController.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость")]
        [SerializeField] private float walkSpeed = 2.2f;

        [Tooltip("Скорость ускорения / торможения.")]
        [SerializeField] private float acceleration = 14f;

        [Header("Приседание")]
        [Tooltip("Скорость в приседе")]
        [SerializeField] private float crouchSpeed = 1.6f;

        [Tooltip("Высота капсулы стоя")]
        [SerializeField] private float standHeight = 1.8f;

        [Tooltip("Высота капсулы в приседе")]
        [SerializeField] private float crouchHeight = 1.2f;

        [Tooltip("Как быстро тело переходит между высотами")]
        [SerializeField] private float crouchTransition = 10f;

        [Tooltip("Высота глаз стоя (локальная Y камеры).")]
        [SerializeField] private float standEyeHeight = 1.62f;

        [Tooltip("Высота глаз в приседе")]
        [SerializeField] private float crouchEyeHeight = 1.05f;

        [Tooltip("Что мешает встать под низким потолком")]
        [SerializeField] private LayerMask headroomMask = ~0;

        [Header("Спринт и прыжок")]
        [Tooltip("Скорость спринта")]
        [SerializeField] private float sprintSpeed = 5.2f;

        [Tooltip("Высота прыжка, м.")]
        [SerializeField] private float jumpHeight = 0.7f;

        [Header("Гравитация")]
        [SerializeField] private float gravity = -22f;

        private readonly Collider[] _headroomOverlaps = new Collider[8];

        private CharacterController _controller;
        private InputReader _input;
        private InputReader _subscribedInput;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _crouchAmount;
        private bool _isJumping;


        public float CrouchAmount => _crouchAmount;
        public bool IsSprinting { get; private set; }
        public float EyeHeight => Mathf.Lerp(standEyeHeight, crouchEyeHeight, _crouchAmount);
        public float Speed01 => walkSpeed > 0.01f ? _horizontalVelocity.magnitude / walkSpeed : 0f;
        public float NormalizedSpeed => Mathf.Clamp01(Speed01);

        public bool IsGrounded => _controller != null && _controller.isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.height = standHeight;
            _controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
        }

        private void Start()
        {
            ResolveInput();
        }

        private void Update()
        {
            if (_input == null)
                ResolveInput();

            UpdateCrouch();

            // В прыжке WASD заблокирован, но инерция сохраняется:
            // горизонтальную скорость не изменяем до приземления.
            if (!_isJumping)
            {
                Vector2 move = _input != null ? _input.Move : Vector2.zero;

                Vector3 desired = transform.right * move.x + transform.forward * move.y;
                if (desired.sqrMagnitude > 1f)
                    desired.Normalize();

                IsSprinting = _input != null && _input.SprintHeld && move.sqrMagnitude > 0.01f
                    && _crouchAmount < 0.5f;

                float targetSpeed = Mathf.Lerp(walkSpeed, crouchSpeed, _crouchAmount);
                if (IsSprinting)
                    targetSpeed = sprintSpeed;

                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    desired * targetSpeed,
                    acceleration * Time.deltaTime);
            }
            else
            {
                IsSprinting = false;
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f; // прижимаем к полу
                _isJumping = false;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            CollisionFlags collisionFlags = _controller.Move(velocity * Time.deltaTime);

            // Упёрлись головой в потолок во время прыжка — не ждём конца дуги, сразу падаем.
            if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;
        }

        private void UpdateCrouch()
        {
            bool wantsCrouch = _input != null && _input.CrouchHeld;

           
            if (!wantsCrouch && _crouchAmount > 0.01f && !HasHeadroom())
                wantsCrouch = true;

            _crouchAmount = Mathf.MoveTowards(
                _crouchAmount,
                wantsCrouch ? 1f : 0f,
                crouchTransition * Time.deltaTime);

            float height = Mathf.Lerp(standHeight, crouchHeight, _crouchAmount);

            if (Mathf.Abs(_controller.height - height) > 0.0001f)
            {
                _controller.height = height;
                _controller.center = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        private bool HasHeadroom()
        {
            float radius = Mathf.Max(0.05f, _controller.radius - 0.02f);
            Vector3 bottom = transform.position + Vector3.up * (_controller.radius + 0.01f);
            Vector3 top = transform.position + Vector3.up * (standHeight - _controller.radius - 0.01f);

            int count = Physics.OverlapCapsuleNonAlloc(
                bottom, top, radius, _headroomOverlaps, headroomMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = _headroomOverlaps[i];

                if (other == null || other.transform.IsChildOf(transform))
                    continue;

                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            if (_subscribedInput != null)
                _subscribedInput.JumpPressed -= OnJumpPressed;
        }

        private void OnJumpPressed()
        {
            if (_controller == null || !_controller.isGrounded)
                return;

            _verticalVelocity = Mathf.Sqrt(2f * Mathf.Max(0f, jumpHeight) * Mathf.Abs(gravity));
            _isJumping = true;
        }

        private void ResolveInput()
        {
            InputReader resolved = GameRoot.Instance != null ? GameRoot.Instance.Input : null;

            if (resolved == null)
                resolved = GetComponent<InputReader>();

            if (resolved == _subscribedInput)
            {
                _input = resolved;
                return;
            }

            if (_subscribedInput != null)
                _subscribedInput.JumpPressed -= OnJumpPressed;

            _subscribedInput = resolved;
            _input = resolved;

            if (_subscribedInput != null)
                _subscribedInput.JumpPressed += OnJumpPressed;
        }
    }
}
