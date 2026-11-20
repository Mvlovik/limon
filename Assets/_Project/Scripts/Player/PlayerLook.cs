using GameLimon.Core;
using UnityEngine;

namespace GameLimon.Player
{
    /// <summary>
    /// Камера от первого лица
    /// </summary>
    public sealed class PlayerLook : MonoBehaviour
    {
        [Tooltip("Трансформ камеры-ребёнка")]
        [SerializeField] private Transform cameraTransform;

        [Header("Камера")]
        [Tooltip("Вертикальный FOV игровой камеры. Меняется здесь, а не в компоненте Camera.")]
        [SerializeField, Range(0f, 360f)] private float fieldOfView = 90f;

        [Header("Сенса")]
        [Tooltip("Множитель для мыши и стика.")]
        [SerializeField] private float sensitivity = 0.11f;

        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;


        [Header("Покачивание")]
        [Tooltip("Частота шага на полной скорости, циклов в секунду")]
        [SerializeField] private float bobFrequency = 1.0f;

        [Tooltip("Амплитуда покачивания по V")]
        [SerializeField] private float bobVertical = 0.012f;

        [Tooltip("Амплитуда покачивания по H")]
        [SerializeField] private float bobHorizontal = 0.008f;

        [Tooltip("Во сколько раз сильнее качает на спринте")]
        [SerializeField] private float bobSprintAmplitude = 1.7f;

        [Tooltip("Во сколько раз чаще шаг на спринте")]
        [SerializeField] private float bobSprintFrequency = 1.25f;

        [Tooltip("Как быстро покачивание набирается и затухает")]
        [SerializeField] private float bobSmoothing = 1.2f;

        [Tooltip("Сглаживание высоты глаз при приседе.")]
        [SerializeField] private float eyeSmoothing = 8f;

        [Header("Курсор")]
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField] private bool relockOnClick = true;

        private InputReader _input;
        private PlayerMotor _motor;
        private Camera _camera;

        private float _pitch;
        private float _bobPhase;
        private float _bobWeight;
        private float _smoothedEyeHeight;

        /// <summary>
        /// Поза камеры под внешним управлением (катсцены, пробуждение на кровати):
        /// обзор мышью и запись позы камеры приостановлены.
        /// </summary>
        public bool PoseLocked { get; set; }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            _camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            ApplyFieldOfView();

            _motor = GetComponent<PlayerMotor>();

            if (cameraTransform != null)
                _smoothedEyeHeight = cameraTransform.localPosition.y;

            ResolveInput();

            if (lockCursorOnStart)
                LockCursor(true);
        }

        private void Update()
        {
            if (_input == null)
                ResolveInput();

            if (_input == null || cameraTransform == null)
                return;

            if (PoseLocked)
            {
                // Съедаем накопленный дельта-ввод мыши, чтобы после разблокировки не было рывка.
                _input.ConsumeLook();
                return;
            }

            bool justLocked = false;

            if (relockOnClick && Cursor.lockState != CursorLockMode.Locked &&
                UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor(true);
                justLocked = true;
            }

            Vector2 look = _input.ConsumeLook() * sensitivity;

            // В кадр захвата курсора мышь может прыгнуть в центр окна — не крутим камеру этим скачком.
            if (!justLocked)
            {
                float yaw = transform.localEulerAngles.y + look.x;
                transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);
                cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            UpdateCameraPose();
        }

        /// <summary>
        /// Высота глаз (присед) + процедурное покачивание при ходьбе.
        /// Работает только с локальной позицией камеры, повороты не трогает.
        /// </summary>
        private void UpdateCameraPose()
        {
            if (_motor == null)
                _motor = GetComponent<PlayerMotor>();

            float crouch = _motor != null ? _motor.CrouchAmount : 0f;
            float speed01 = _motor != null ? _motor.Speed01 : 0f;
            bool sprinting = _motor != null && _motor.IsSprinting;
            bool grounded = _motor == null || _motor.IsGrounded;

            float targetWeight = grounded && speed01 > 0.02f ? Mathf.Clamp01(speed01) : 0f;
            _bobWeight = Mathf.MoveTowards(_bobWeight, targetWeight, bobSmoothing * Time.deltaTime);

            if (!grounded)
            {
                // В прыжке и падении тряски нет вообще — не ждём плавного затухания.
                _bobWeight = 0f;
            }
            else
            {
                float frequencyMultiplier = sprinting ? bobSprintFrequency : 1f;
                _bobPhase += Time.deltaTime * Mathf.PI * 2f * bobFrequency * frequencyMultiplier *
                             Mathf.Clamp(speed01, 0.4f, 1.8f);
                _bobPhase = Mathf.Repeat(_bobPhase, Mathf.PI * 2f);
            }

            // В приседе шаг короче — покачивание слабее; на спринте — сильнее.
            float crouchScale = 1f - 0.6f * crouch;
            float amplitudeScale = crouchScale * (sprinting ? bobSprintAmplitude : 1f);

            float vertical = Mathf.Sin(_bobPhase * 2f) * bobVertical * _bobWeight * amplitudeScale;
            float horizontal = Mathf.Cos(_bobPhase) * bobHorizontal * _bobWeight * amplitudeScale;

            float targetEyeHeight = _motor != null ? _motor.EyeHeight : _smoothedEyeHeight;
            _smoothedEyeHeight = Mathf.Lerp(
                _smoothedEyeHeight,
                targetEyeHeight,
                1f - Mathf.Exp(-eyeSmoothing * Time.deltaTime));

            cameraTransform.localPosition = new Vector3(horizontal, _smoothedEyeHeight + vertical, 0f);
        }

        /// <summary>Захват курсора</summary>
        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// Синхронизировать внутреннее состояние с текущей позой камеры
        /// (после катсцен, когда камеру двигали снаружи).
        /// </summary>
        public void ResyncPose()
        {
            if (cameraTransform == null)
                return;

            float pitch = cameraTransform.localEulerAngles.x;
            _pitch = pitch > 180f ? pitch - 360f : pitch;
            _smoothedEyeHeight = cameraTransform.localPosition.y;
        }

        private void OnValidate()
        {
            if (_camera == null && cameraTransform != null)
                _camera = cameraTransform.GetComponent<Camera>();

            if (_camera == null)
                _camera = Camera.main;

            ApplyFieldOfView();
        }

        private void ApplyFieldOfView()
        {
            if (_camera == null)
                return;

            _camera.fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        }

        private void ResolveInput()
        {
            _input = GameRoot.Instance != null ? GameRoot.Instance.Input : null;

            if (_input == null)
                _input = GetComponent<InputReader>();
        }
    }
}
