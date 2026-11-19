using GameLimon.Player;
using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// Переносит в PlayerSpawnPoint единственного игрока.
    /// Игрок живёт в Boot, поэтому служба слушает SceneLoader.SceneLoaded
    /// и выбирает точку именно из только что загруженной сцены.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnService : MonoBehaviour
    {
        [Tooltip("Необязательно. Если пусто — игрок ищется по тегу Player.")]
        [SerializeField] private GameObject player;

        private CharacterController _controller;
        private PlayerMotor _motor;

        private void Start()
        {
            ResolvePlayer();

            SceneLoader loader = GameRoot.Instance != null ? GameRoot.Instance.Loader : null;
            if (loader != null)
                loader.SceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneLoader loader = GameRoot.Instance != null ? GameRoot.Instance.Loader : null;
            if (loader != null)
                loader.SceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(string sceneName) => MovePlayerToSpawnPoint(sceneName);

        /// <summary>Перенести игрока в точку появления текущей загруженной сцены</summary>
        public void MovePlayerToSpawnPoint() => MovePlayerToSpawnPoint(null);

        /// <summary>Перенести игрока в точку появления конкретной сцены.</summary>
        public void MovePlayerToSpawnPoint(string sceneName)
        {
            ResolvePlayer();

            if (player == null)
            {
                Debug.LogError("[PlayerSpawnService] Игрок не найден (тег Player). Спавн в PlayerSpawnPoint не выполнен.");
                return;
            }

            // Ищем точку в только что загруженной сцене: иначе при нескольких
            // открытых сценах можно уехать в PlayerSpawnPoint чужой сцены.
            PlayerSpawnPoint spawn = FindSpawnPoint(sceneName);

            if (spawn == null)
            {
                Debug.LogWarning("[PlayerSpawnService] В загруженной сцене нет PlayerSpawnPoint — игрок остался на месте.");
                return;
            }

            if (_controller != null && _controller.enabled)
                _controller.enabled = false;

            Quaternion flatRotation = Quaternion.Euler(0f, spawn.Rotation.eulerAngles.y, 0f);
            player.transform.SetPositionAndRotation(spawn.Position, flatRotation);

            if (_motor != null)
                _motor.ResetMomentum();

            if (_controller != null)
                _controller.enabled = true;

            Debug.Log($"[PlayerSpawnService] Игрок перемещён в {spawn.gameObject.scene.name}/{spawn.name} " +
                      $"({spawn.Position.x:0.00}, {spawn.Position.y:0.00}, {spawn.Position.z:0.00}).", spawn);
        }

        private void ResolvePlayer()
        {
            if (player == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged;
            }

            // Страховка, если у префаба потерялся тег Player.
            if (player == null)
            {
                PlayerMotor foundMotor = Object.FindFirstObjectByType<PlayerMotor>();
                if (foundMotor != null)
                    player = foundMotor.gameObject;
            }

            if (player != null)
            {
                _controller = player.GetComponent<CharacterController>();
                _motor = player.GetComponent<PlayerMotor>();
            }
        }

        private static PlayerSpawnPoint FindSpawnPoint(string sceneName)
        {
            PlayerSpawnPoint[] points = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);

            foreach (PlayerSpawnPoint point in points)
            {
                if (point == null || !point.gameObject.scene.isLoaded)
                    continue;

                if (string.IsNullOrEmpty(sceneName) || point.gameObject.scene.name == sceneName)
                    return point;
            }

            return null;
        }
    }
}
