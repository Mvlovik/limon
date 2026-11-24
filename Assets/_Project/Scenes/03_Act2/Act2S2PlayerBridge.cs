using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Staging
{
    /// <summary>
    /// Вторая сцена второго акта собрана вокруг собственного сценового игрока
    /// (превью-версия). В игре игрок живёт в Boot, поэтому этот мост:
    /// — выключает сценового дублёра;
    /// — держит настоящего игрока в «сонном» состоянии до конца вступительного
    ///   таймлайна и возвращает управление по сигналу;
    /// — принимает сигналы таймлайнов вместо удалённых объектов первого акта.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Act2S2PlayerBridge : MonoBehaviour
    {
        [Header("Сценовые телепорты")]
        [SerializeField] private TeleportInteractable cleanupStart;
        [SerializeField] private TeleportInteractable stopAfterSteppingBack;
        [SerializeField] private TeleportInteractable apartmentToStreet;

        [Header("Переход на улицу")]
        [Tooltip("Корень улицы — включается при выходе из квартиры.")]
        [SerializeField] private GameObject streetRoot;

        [Tooltip("Флаг, после которого игрока переносит из квартиры на улицу.")]
        [SerializeField] private string streetExitFlag = "A2_S02_Exit";

        private GameObject _player;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInteractor _interactor;
        private FirstPersonBody _body;
        private CharacterController _controller;
        private readonly List<Camera> _cameras = new List<Camera>();
        private readonly List<AudioListener> _listeners = new List<AudioListener>();
        private bool _bound;
        private bool _wentOutside;
        private bool _advancedToSceneThree;

        private void Start()
        {
            GameObject bootPlayer = FindBootPlayer();
            if (bootPlayer == null)
                return; // сцена запущена напрямую — работает её превью-игрок

            // Сценовый дублёр игрока в игре не нужен: он дублирует контроллер и тело.
            foreach (var motor in FindObjectsByType<PlayerMotor>(FindObjectsInactive.Include))
            {
                if (motor != null && motor.gameObject != bootPlayer &&
                    motor.gameObject.scene == gameObject.scene)
                {
                    motor.gameObject.SetActive(false);
                }
            }

            _player = bootPlayer;
            _motor = _player.GetComponent<PlayerMotor>();
            _look = _player.GetComponent<PlayerLook>();
            _interactor = _player.GetComponent<PlayerInteractor>();
            _controller = _player.GetComponent<CharacterController>();
            _body = _player.GetComponentInChildren<FirstPersonBody>(true);
            _bound = true;

            _cameras.Clear();
            foreach (Camera camera in _player.GetComponentsInChildren<Camera>(true))
                if (camera != null && camera.enabled)
                    _cameras.Add(camera);

            _listeners.Clear();
            foreach (AudioListener listener in _player.GetComponentsInChildren<AudioListener>(true))
                if (listener != null && listener.enabled)
                    _listeners.Add(listener);

            // Вступление идёт через сценовые камеры: игрок заморожен и не виден.
            SetCameraEnabled(false);
            SetListenerEnabled(false);
            SetControlLocked(true);
            SetPoseLocked(true);
            SetInteractorEnabled(false);
            _body?.SetExternalView(true);
        }

        private void Update()
        {
            if (!_bound)
                return;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (state == null)
                return;

            if (!_wentOutside && state.GetFlag(streetExitFlag))
            {
                _wentOutside = true;
                if (streetRoot != null)
                    streetRoot.SetActive(true);
                Teleport(apartmentToStreet);
            }

            if (!_advancedToSceneThree && state.GetFlag("A2_S02_Ride"))
            {
                _advancedToSceneThree = true;
                GameRoot.Instance?.Flow?.CompleteCurrentScene();
            }
        }

        /// <summary>Сигнал 3.78 с: вступление закончилось, игроку возвращают управление.</summary>
        public void ResumeGameplay()
        {
            if (!_bound)
                return;

            SetCameraEnabled(true);
            SetListenerEnabled(true);
            SetControlLocked(false);
            SetPoseLocked(false);
            SetInteractorEnabled(true);
            LockCursor(true);
            ResyncPose();
            _body?.SetExternalView(false);
        }

        public void SetCameraEnabled(bool value)
        {
            foreach (Camera camera in _cameras)
                if (camera != null) camera.enabled = value;
        }

        public void SetListenerEnabled(bool value)
        {
            foreach (AudioListener listener in _listeners)
                if (listener != null) listener.enabled = value;
        }

        public void SetControlLocked(bool value) => _motor?.SetControlLocked(value);

        public void SetPoseLocked(bool value)
        {
            if (_look != null) _look.PoseLocked = value;
        }

        public void SetInteractorEnabled(bool value)
        {
            if (_interactor != null) _interactor.enabled = value;
        }

        public void LockCursor(bool value) => _look?.LockCursor(value);

        public void ResyncPose() => _look?.ResyncPose();

        public void LookAt(Transform target)
        {
            if (target == null || _player == null)
                return;

            Vector3 flat = target.position - _player.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return;

            _player.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            _look?.ResyncPose();
        }

        public void SetFade(float alpha)
        {
            GameRoot root = GameRoot.Instance;
            if (root != null && root.UI != null)
                root.UI.SetFade(alpha);
        }

        public void TeleportCleanupStart() => Teleport(cleanupStart);

        public void TeleportStopAfterSteppingBack() => Teleport(stopAfterSteppingBack);

        /// <summary>Совместимость со старым сигналом «PlayerCamera Animator.enabled».</summary>
        public void SetCameraAnimatorEnabled(bool value) { }

        private void Teleport(TeleportInteractable teleport)
        {
            if (teleport == null || _player == null)
                return;

            teleport.Interact(_player);
        }

        private GameObject FindBootPlayer()
        {
            foreach (GameObject candidate in GameObject.FindGameObjectsWithTag("Player"))
            {
                if (candidate != null && candidate.scene != gameObject.scene)
                    return candidate;
            }

            return null;
        }
    }
}
