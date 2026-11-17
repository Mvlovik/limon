using UnityEngine;

namespace GameLimon.Companions
{
    /// <summary>
    /// Точка маршрута напарника. Ставится в сцене и привязывается к
    /// <see cref="CompanionWaypointTrigger"/>; сам напарник идёт к ней по порядку.
    /// </summary>
    public sealed class CompanionWaypoint : MonoBehaviour
    {
        [Tooltip("Имя точки — для отладки и сюжетных условий.")]
        [SerializeField] private string id = "point";

        [Tooltip("Радиус, в котором напарник считается пришедшим в точку, м.")]
        [Min(0.05f)]
        [SerializeField] private float arriveRadius = 0.35f;

        [Tooltip("Сколько секунд стоять в точке, прежде чем снова пойти за игроком.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 0.75f;

        public string Id => id;
        public float ArriveRadius => Mathf.Max(0.05f, arriveRadius);
        public float HoldSeconds => Mathf.Max(0f, holdSeconds);
        public Vector3 Position => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            bool selected = UnityEditor.Selection.activeGameObject == gameObject;

            Gizmos.color = selected ? new Color(0.35f, 0.95f, 1f, 1f) : new Color(0.35f, 0.95f, 1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, ArriveRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);

            if (selected)
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.1f, "Companion point: " + id);
        }
#endif
    }
}
