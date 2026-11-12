using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    ///  переносит в PlayerSpawnPoint единственного игрока.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnService : MonoBehaviour
    {
        [Tooltip("Необязательно. Если пусто — игрок ищется по тегу Player.")]
        [SerializeField] private GameObject player;

        private CharacterController _controller;

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

        private void OnSceneLoaded(string sceneName) => MovePlayerToSpawnPoint();

        /// <summary>Перенести игрока в точку появления текущей загруженной сцены</summary>
        public void MovePlayerToSpawnPoint()
        {
            if (player == null)
                ResolvePlayer();

            if (player == null)
                return;

            PlayerSpawnPoint spawn = FindSpawnPoint();
            if (spawn == null)
            {
                Debug.LogWarning("[PlayerSpawnService] В загруженной сцене нет PlayerSpawnPoint — игрок остался на месте.");
                return;
            }

            if (_controller != null)
                _controller.enabled = false;

       
            Quaternion flatRotation = Quaternion.Euler(0f, spawn.Rotation.eulerAngles.y, 0f);
            player.transform.SetPositionAndRotation(spawn.Position, flatRotation);

            if (_controller != null)
                _controller.enabled = true;
        }

        private void ResolvePlayer()
        {
            if (player == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged;
            }

            if (player != null)
                _controller = player.GetComponent<CharacterController>();
        }

        private static PlayerSpawnPoint FindSpawnPoint()
        {
            PlayerSpawnPoint[] points = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);

            foreach (PlayerSpawnPoint point in points)
            {
                if (point != null && point.gameObject.scene.isLoaded)
                    return point;
            }

            return null;
        }
    }
}
