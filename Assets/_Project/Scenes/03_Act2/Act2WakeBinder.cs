using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Player;
using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Staging
{
    /// <summary>
    /// Первая сцена второго акта начинается со сновидения: игрок ещё «спит»,
    /// камеру держат сценовые камеры, а управление и тело игрока не должны
    /// участвовать. Игрок живёт в Boot (как и в первом акте), поэтому этот
    /// компонент берёт именно его и возвращает в кадр по сигналу пробуждения
    /// из таймлайна.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Act2WakeBinder : MonoBehaviour
    {
        [Tooltip("Точка, в которой игрок просыпается. Если пусто — берётся PlayerSpawnPoint этой сцены.")]
        [SerializeField] private PlayerSpawnPoint wakingSpawn;

        [Tooltip("Глубина камеры игрока после пробуждения, чтобы она перекрыла сценовые камеры.")]
        [SerializeField] private float playerCameraDepth = 10f;

        private GameObject _player;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private CharacterController _controller;
        private FirstPersonBody _body;
        private readonly List<Camera> _playerCameras = new List<Camera>();
        private readonly List<AudioListener> _playerListeners = new List<AudioListener>();
        private bool _bound;

        private void Start()
        {
            // Сцена запущена напрямую из редактора — работает собственный превью-игрок.
            if (GameRoot.Instance == null)
                return;

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged == null || tagged.scene == gameObject.scene)
                return;

            _player = tagged;
            _motor = _player.GetComponent<PlayerMotor>();
            _look = _player.GetComponent<PlayerLook>();
            _controller = _player.GetComponent<CharacterController>();
            _body = _player.GetComponentInChildren<FirstPersonBody>(true);
            _bound = true;

            // Сон: управление забрано, камера и слушатель игрока не участвуют —
            // картинку и звук дают камеры сцены. Тело тоже прячем: игрок может
            // оказаться в кадре сновидения.
            _motor?.SetControlLocked(true);
            if (_look != null)
                _look.PoseLocked = true;

            _body?.SetExternalView(true);

            foreach (Camera camera in _player.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null) continue;
                _playerCameras.Add(camera);
                camera.enabled = false;
            }

            foreach (AudioListener listener in _player.GetComponentsInChildren<AudioListener>(true))
            {
                if (listener == null) continue;
                _playerListeners.Add(listener);
                listener.enabled = false;
            }
        }

        /// <summary>Вызывается сигналом таймлайна в момент пробуждения.</summary>
        public void WakePlayer()
        {
            if (!_bound || _player == null)
                return;

            PlayerSpawnPoint spawn = wakingSpawn != null ? wakingSpawn : FindSpawnInThisScene();
            if (spawn != null)
            {
                Quaternion flat = Quaternion.Euler(0f, spawn.Rotation.eulerAngles.y, 0f);

                // CharacterController нельзя телепортировать при включённом компоненте.
                if (_controller != null && _controller.enabled)
                    _controller.enabled = false;

                _player.transform.SetPositionAndRotation(spawn.Position, flat);

                if (_controller != null)
                    _controller.enabled = true;
            }

            foreach (Camera camera in _playerCameras)
            {
                if (camera == null) continue;
                camera.enabled = true;
                camera.depth = playerCameraDepth;
            }

            foreach (AudioListener listener in _playerListeners)
                if (listener != null) listener.enabled = true;

            // После пробуждения в кадре и в звуке остаётся только игрок —
            // сценовые камеры сновидения больше не нужны.
            DisableSceneCameras();

            if (_motor != null)
            {
                _motor.SetControlLocked(false);
                _motor.ResetMomentum();
            }

            if (_look != null)
            {
                _look.PoseLocked = false;
                _look.ResyncPose();
                _look.LockCursor(true);
            }

            _body?.SetExternalView(false);
        }

        /// <summary>Для сигналов, которые раньше вызывали PlayerLook.LockCursor у сценового игрока.</summary>
        public void LockCursor()
        {
            if (_look != null) _look.LockCursor(true);
        }

        /// <summary>Для сигналов, которые раньше вызывали PlayerLook.ResyncPose у сценового игрока.</summary>
        public void ResyncPose()
        {
            if (_look != null) _look.ResyncPose();
        }

        private void DisableSceneCameras()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (camera == null || camera.gameObject.scene != gameObject.scene)
                    continue;
                if (_playerCameras.Contains(camera))
                    continue;

                camera.enabled = false;
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        private PlayerSpawnPoint FindSpawnInThisScene()
        {
            foreach (PlayerSpawnPoint spawn in Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude))
            {
                if (spawn != null && spawn.gameObject.scene == gameObject.scene)
                    return spawn;
            }

            return null;
        }
    }
}
