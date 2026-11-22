using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLimon.Companions
{
    /// <summary>
    /// Универсальный напарник (например, Джо): идёт к точкам маршрута по
    /// команде сценовых триггеров. Преследование игрока — необязательный
    /// режим, по умолчанию выключен.
    ///
    /// Двигается кинематически через Transform, поэтому не требует NavMesh,
    /// Rigidbody или CharacterController и подходит для любой сцены.
    /// Каждая точка активируется один раз: новый триггер сразу перехватывает
    /// цель, а прошлая точка больше не принимается — назад напарник не идёт.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionActor : MonoBehaviour
    {
        [Header("Игрок")]
        [Tooltip("Опционально: догонять игрока, когда нет активной точки маршрута. По умолчанию выключено.")]
        [SerializeField] private bool followPlayer;

        [Tooltip("Тег игрока. Используется, если не задан Player Override.")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Необязательная явная ссылка на игрока — если тег не подходит.")]
        [SerializeField] private Transform playerOverride;

        [Tooltip("Начинать движение, когда игрок дальше этого расстояния, м.")]
        [Min(0f)]
        [SerializeField] private float followDistance = 2.8f;

        [Tooltip("Останавливаться, когда игрок ближе этого расстояния, м.")]
        [Min(0f)]
        [SerializeField] private float followStopDistance = 1.9f;

        [Tooltip("Смотреть на игрока, когда напарник стоит.")]
        [SerializeField] private bool facePlayerWhenIdle = true;

        [Header("Скорость")]
        [Tooltip("Обычная скорость ходьбы, м/с.")]
        [Min(0f)]
        [SerializeField] private float walkSpeed = 2.4f;

        [Tooltip("Скорость, когда нужно догонять игрока, м/с.")]
        [Min(0f)]
        [SerializeField] private float catchUpSpeed = 5.6f;

        [Tooltip("С какого расстояния включается полная скорость догоняния, м.")]
        [Min(0f)]
        [SerializeField] private float catchUpDistance = 8f;

        [Tooltip("За сколько метров до точки напарник начинает замедляться.")]
        [Min(0.05f)]
        [SerializeField] private float settleDistance = 1.2f;

        [Tooltip("Ускорение и торможение, м/с².")]
        [Min(0.01f)]
        [SerializeField] private float acceleration = 12f;

        [Tooltip("Скорость поворота, градусов в секунду.")]
        [Min(0f)]
        [SerializeField] private float turnSpeed = 420f;

        [Header("Земля")]
        [Tooltip("Прижимать напарника к земле лучом, чтобы он не проваливался и не парил.")]
        [SerializeField] private bool snapToGround = true;

        [Tooltip("Слои, которые считаются землёй.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("На сколько поднять начало луча над напарником, м.")]
        [Min(0f)]
        [SerializeField] private float groundProbeUp = 1.2f;

        [Tooltip("Как далеко вниз искать землю, м.")]
        [Min(0.1f)]
        [SerializeField] private float groundProbeDown = 4f;

        [Tooltip("Смещение над поверхностью земли, м.")]
        [SerializeField] private float groundOffset = 0f;

        [Tooltip("Гравитация, если земля под ногами не найдена, м/с².")]
        [SerializeField] private float gravity = -20f;

        [Header("Анимация (необязательно)")]
        [Tooltip("Animator напарника. Если пусто — ищется в детях.")]
        [SerializeField] private Animator animator;

        [Tooltip("Имя float-параметра Animator, куда пишется нормализованная скорость.")]
        [SerializeField] private string animatorSpeedParameter = "Speed";

        [Tooltip("Имя float-параметра Animator для скорости проигрывания шага. Пусто — не используется.")]
        [SerializeField] private string animatorPlaybackParameter = "PlaybackSpeed";

        private const float PlayerSearchInterval = 0.5f;
        private static readonly RaycastHit[] s_groundHits = new RaycastHit[16];

        private Transform _player;
        private float _playerSearchTimer;

        private Vector3 _velocity;
        private float _verticalVelocity;

        private CompanionWaypoint _activeWaypoint;
        private int _activeOrder;
        private int _highestOrder = int.MinValue;

        private readonly HashSet<CompanionWaypoint> _visitedWaypoints = new HashSet<CompanionWaypoint>();

        private int _lastActivationFrame = int.MinValue;
        private float _lastActivationProgress = float.NegativeInfinity;

        private float _holdTimer;
        private bool _isFollowing;

        /// <summary>Наибольший порядок среди принятых точек — только для отладки.</summary>
        public int HighestOrder => _highestOrder;

        /// <summary>Текущая точка маршрута, к которой идёт напарник. null — идёт за игроком.</summary>
        public CompanionWaypoint ActiveWaypoint => _activeWaypoint;

        public int ActiveOrder => _activeOrder;

        public bool IsMovingToPoint => _activeWaypoint != null;

        /// <summary>Текущая горизонтальная скорость, м/с — для анимации ходьбы.</summary>
        public float CurrentSpeed => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

        public bool IsMoving => CurrentSpeed > 0.05f;

        /// <summary>Трансформ игрока, найденный по тегу или заданный явно. Может быть null.</summary>
        public Transform Player => _player;

        public bool FollowEnabled => followPlayer;

        /// <summary>Точка принята и напарник направляется к ней.</summary>
        public event Action<CompanionWaypoint, int> WaypointAccepted;

        /// <summary>Напарник дошёл до точки.</summary>
        public event Action<CompanionWaypoint> WaypointReached;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            ResolvePlayer(force: true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            ResolvePlayer(force: false);
            UpdateMotion(dt);
            UpdateAnimator(dt);
        }

        /// <summary>Точка уже активировалась ранее — возвращаться к ней нельзя.</summary>
        public bool IsWaypointVisited(CompanionWaypoint waypoint) =>
            waypoint != null && _visitedWaypoints.Contains(waypoint);

        /// <summary>
        /// Направить напарника к точке по обычной активации (вне покадрового свипа).
        /// </summary>
        public bool MoveTo(CompanionWaypoint waypoint, int order) =>
            MoveTo(waypoint, order, Time.frameCount, 1f);

        /// <summary>
        /// Направить напарника к точке. Принимается любая новая точка,
        /// а уже активированная — отклоняется. Дополнительно проверяется
        /// порядок активации внутри кадра: если игрок проскочил несколько зон
        /// за один кадр, побеждает та, которую он прошёл позже (больше progress).
        /// </summary>
        public bool MoveTo(CompanionWaypoint waypoint, int order, int activationFrame, float activationProgress)
        {
            if (waypoint == null || _visitedWaypoints.Contains(waypoint))
                return false;

            if (activationFrame < _lastActivationFrame)
                return false;

            if (activationFrame == _lastActivationFrame &&
                activationProgress < _lastActivationProgress - 0.0001f)
                return false;

            _visitedWaypoints.Add(waypoint);
            _activeWaypoint = waypoint;
            _activeOrder = order;
            _lastActivationFrame = activationFrame;
            _lastActivationProgress = activationProgress;

            if (order > _highestOrder)
                _highestOrder = order;
            _holdTimer = 0f;
            _isFollowing = false;

            WaypointAccepted?.Invoke(waypoint, order);
            return true;
        }

        /// <summary>Включить или выключить преследование игрока.</summary>
        public void SetFollowEnabled(bool enabled) => followPlayer = enabled;

        /// <summary>Сбросить историю активированных точек (новый чекпоинт, перезапуск маршрута).</summary>
        public void ResetVisitedWaypoints()
        {
            _visitedWaypoints.Clear();
            _highestOrder = int.MinValue;
            _lastActivationFrame = int.MinValue;
            _lastActivationProgress = float.NegativeInfinity;
        }

        /// <summary>Мгновенно перенести напарника (смена сцены, чекпоинт).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _velocity = Vector3.zero;
            _verticalVelocity = 0f;
            transform.position = position;
            transform.rotation = rotation;
        }

        private void UpdateMotion(float dt)
        {
            Vector3 desiredVelocity = Vector3.zero;
            bool lookAtPlayer = false;

            if (_activeWaypoint != null)
            {
                Vector3 offset = _activeWaypoint.Position - transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;

                if (distance <= _activeWaypoint.ArriveRadius)
                {
                    CompleteWaypoint();
                }
                else
                {
                    float speed = ChaseSpeed(distance) * Mathf.Clamp01(distance / settleDistance);
                    desiredVelocity = offset / distance * speed;
                }
            }
            else if (_holdTimer > 0f)
            {
                _holdTimer -= dt;
                lookAtPlayer = facePlayerWhenIdle;
            }
            else if (followPlayer && _player != null)
            {
                Vector3 offset = _player.position - transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;

                if (!_isFollowing && distance > followDistance)
                    _isFollowing = true;
                else if (_isFollowing && distance < followStopDistance)
                    _isFollowing = false;

                if (_isFollowing && distance > 0.001f)
                    desiredVelocity = offset / distance * ChaseSpeed(distance);
                else
                    lookAtPlayer = facePlayerWhenIdle;
            }

            _velocity = Vector3.MoveTowards(_velocity, desiredVelocity, acceleration * dt);

            Vector3 position = transform.position + _velocity * dt;

            if (snapToGround)
            {
                float groundY = position.y;

                if (TrySampleGround(position, out groundY))
                {
                    position.y = groundY + groundOffset;
                    _verticalVelocity = 0f;
                }
                else
                {
                    _verticalVelocity += gravity * dt;
                    position.y += _verticalVelocity * dt;
                }
            }

            transform.position = position;

            Vector3 flatVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
            if (flatVelocity.sqrMagnitude > 0.02f)
            {
                RotateTowards(flatVelocity);
            }
            else if (lookAtPlayer && _player != null)
            {
                Vector3 toPlayer = _player.position - transform.position;
                toPlayer.y = 0f;

                if (toPlayer.sqrMagnitude > 0.01f)
                    RotateTowards(toPlayer);
            }
        }

        private float ChaseSpeed(float distance)
        {
            if (distance <= followDistance)
                return walkSpeed;

            float t = Mathf.InverseLerp(followDistance, Mathf.Max(followDistance + 0.01f, catchUpDistance), distance);
            return Mathf.Lerp(walkSpeed, catchUpSpeed, t);
        }

        private void RotateTowards(Vector3 direction)
        {
            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);

            if (turnSpeed <= 0f)
            {
                transform.rotation = target;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private void CompleteWaypoint()
        {
            CompanionWaypoint waypoint = _activeWaypoint;
            _activeWaypoint = null;
            _activeOrder = 0;
            _holdTimer = waypoint != null ? waypoint.HoldSeconds : 0f;
            _velocity = Vector3.zero;
            _isFollowing = false;

            // Встаём в позу точки, чтобы следующая катсцена/диалог не
            // разворачивали NPC рывком.
            if (waypoint != null)
            {
                transform.rotation = waypoint.transform.rotation;
                WaypointReached?.Invoke(waypoint);
            }
        }

        private void ResolvePlayer(bool force)
        {
            if (playerOverride != null)
            {
                _player = playerOverride;
                return;
            }

            if (!force && _player != null)
                return;

            _playerSearchTimer -= Time.deltaTime;
            if (!force && _playerSearchTimer > 0f)
                return;

            _playerSearchTimer = PlayerSearchInterval;

            if (string.IsNullOrEmpty(playerTag))
                return;

            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            _player = found != null ? found.transform : null;
        }

        private bool TrySampleGround(Vector3 position, out float groundY)
        {
            groundY = position.y;

            if (!snapToGround)
                return false;

            Vector3 origin = position + Vector3.up * groundProbeUp;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                s_groundHits,
                groundProbeUp + groundProbeDown,
                groundMask,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_groundHits[i];
                if (hit.collider == null)
                    continue;

                // Не считаем землёй собственное тело напарника и самого игрока.
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                if (_player != null && (hit.transform == _player || hit.transform.IsChildOf(_player)))
                    continue;

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
            }

            return found;
        }

        private void UpdateAnimator(float dt)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            if (string.IsNullOrEmpty(animatorSpeedParameter))
                return;

            float speed = new Vector3(_velocity.x, 0f, _velocity.z).magnitude;
            float normalized = walkSpeed > 0.01f ? Mathf.Clamp01(speed / walkSpeed) : 0f;

            animator.SetFloat(animatorSpeedParameter, normalized, 0.1f, dt);

            if (!string.IsNullOrEmpty(animatorPlaybackParameter))
            {
                float playback = Mathf.Clamp(speed / Mathf.Max(0.01f, walkSpeed), 0.55f, 2.2f);
                animator.SetFloat(animatorPlaybackParameter, playback, 0.1f, dt);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, followDistance);

            if (_activeWaypoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _activeWaypoint.Position);
            }
        }
#endif
    }
}
