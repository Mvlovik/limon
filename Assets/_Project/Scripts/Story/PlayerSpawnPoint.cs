using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Точка появления игрока в сцене
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [Tooltip("Необязательное имя, чтобы различать точки в одной сцене.")]
        [SerializeField] private string id = "default";

        public string Id => id;

        public Vector3 Position => transform.position;

        public Quaternion Rotation => transform.rotation;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.2f);
        }
#endif
    }
}
