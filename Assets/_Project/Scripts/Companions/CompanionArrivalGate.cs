using GameLimon.Core;
using UnityEngine;

namespace GameLimon.Companions
{
    /// <summary>
    /// Не даёт сценовому триггеру сработать раньше, чем напарник реально
    /// дошёл до нужной точки. Пока игрок находится в зоне, компонент
    /// отправляет напарника к точке (если он ещё не идёт) и ставит
    /// GameState-флаг только после прибытия. У TriggerVolume этот флаг
    /// задаётся в поле Required Flag.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CompanionArrivalGate : MonoBehaviour
    {
        [Header("Напарник")]
        [SerializeField] private CompanionActor actor;
        [SerializeField] private CompanionWaypoint waypoint;

        [Tooltip("Если пусто — используется позиция точки маршрута.")]
        [SerializeField] private Transform arrivalPoint;

        [Tooltip("Радиус, в котором напарник считается пришедшим.")]
        [Min(0.05f)]
        [SerializeField] private float arrivalRadius = 0.55f;

        [Header("Флаг")]
        [Tooltip("Флаг, который ставится после прибытия напарника.")]
        [SerializeField] private string arrivalFlag = "companion_ready";

        [Header("Игрок")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Если напарник уже стоит в зоне, сразу отправить его к точке.")]
        [SerializeField] private bool pushToWaypoint = true;

        [Tooltip("Ждать реального прихода напарника перед установкой флага. " +
                 "Если выключено — флаг ставится сразу при входе игрока, а напарник " +
                 "доходит до точки уже во время катсцены (без задержки старта).")]
        [SerializeField] private bool waitForArrival = false;

        private bool _playerInside;
        private bool _arrived;

        private Vector3 ArrivalPosition =>
            arrivalPoint != null ? arrivalPoint.position : waypoint != null ? waypoint.Position : transform.position;

        private void Reset()
        {
            Collider volume = GetComponent<Collider>();
            if (volume != null)
                volume.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
            {
                _playerInside = true;
                Tick();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsPlayer(other))
            {
                _playerInside = true;
                Tick();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
                _playerInside = false;
        }

        private void Update()
        {
            if (_playerInside)
                Tick();
        }

        private void Tick()
        {
            if (actor == null)
                return;

            if (!_arrived)
            {
                if (!waitForArrival)
                {
                    // Катсцена стартует сразу: напарник доходит до точки уже
                    // внутри интро, а не блокирует её начало своим походом.
                    _arrived = true;
                    SetFlag(arrivalFlag);
                }
                else
                {
                    float distance = Vector3.Distance(actor.transform.position, ArrivalPosition);

                    // Ждём именно завершения движения: пока ActiveWaypoint != null,
                    // напарник ещё бежит и может быть в паре метров от финальной позы.
                    if (distance <= arrivalRadius && actor.ActiveWaypoint == null)
                    {
                        _arrived = true;
                        SetFlag(arrivalFlag);
                        return;
                    }
                }
            }

            if (!pushToWaypoint || waypoint == null)
                return;

            if (actor.ActiveWaypoint == waypoint || actor.IsWaypointVisited(waypoint))
                return;

            actor.MoveTo(waypoint, 999);
        }

        private static void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (state != null)
                state.SetFlag(flag, true);
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && !string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.9f);
            Gizmos.DrawWireSphere(ArrivalPosition, arrivalRadius);
        }
#endif
    }
}
