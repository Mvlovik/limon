using GameLimon.Core;
using GameLimon.Story;
using UnityEngine;
using UnityEngine.Events;

namespace GameLimon.Companions
{
    /// <summary>
    /// Триггер маршрута напарника: когда игрок входит в зону, напарник идёт
    /// к привязанной <see cref="CompanionWaypoint"/>.
    ///
    /// Зона проверяется каждый кадр по отрезку «прошлая позиция игрока →
    /// текущая». Поэтому триггер не пропускает зоны, пройденные на бегу,
    /// и корректно определяет, какую из нескольких зон игрок прошёл позже.
    ///
    /// Новый триггер сразу перехватывает цель; уже активированная точка
    /// повторно не принимается, поэтому вернуться назад нельзя.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class CompanionWaypointTrigger : MonoBehaviour
    {
        [Header("Напарник")]
        [Tooltip("Кого вести. Если пусто — берётся первый CompanionActor в сцене.")]
        [SerializeField] private CompanionActor actor;

        [Tooltip("Точка, к которой должен прийти напарник.")]
        [SerializeField] private CompanionWaypoint waypoint;

        [Tooltip("Порядковый номер точки — для порядка в инспекторе и отладки. Переключение цели от него не зависит.")]
        [Min(0)]
        [SerializeField] private int order = 1;

        [Tooltip("Сработать только один раз за загрузку сцены.")]
        [SerializeField] private bool once = true;

        [Tooltip("Ловить проскок зоны за один кадр (свип-проверка отрезка).")]
        [SerializeField] private bool detectFastMovement = true;

        [Header("Условия")]
        [Tooltip("Разрешить срабатывание только пока квест ждёт этот флаг.")]
        [QuestStep]
        [SerializeField] private string requiredStep;

        [Tooltip("Скипать, если этот флаг уже стоит.")]
        [SerializeField] private string skipIfFlag;

        [Tooltip("Флаг, который ставится после срабатывания.")]
        [SerializeField] private string completedFlag;

        [Header("События")]
        [Tooltip("Вызывается после того, как напарник принял точку.")]
        [SerializeField] private UnityEvent onActivated = new UnityEvent();

        [Tooltip("Тег игрока.")]
        [SerializeField] private string playerTag = "Player";

        private const float PlayerSearchInterval = 0.5f;

        private Collider _volume;
        private bool _fired;

        private Transform _player;
        private Collider _playerCollider;
        private float _playerSearchTimer;

        private bool _hasSample;
        private bool _inside;
        private Vector3 _lastSample;

        public int Order => order;
        public CompanionWaypoint Waypoint => waypoint;
        public CompanionActor Actor => actor;

        private void Awake()
        {
            _volume = GetComponent<Collider>();
        }

        private void Reset()
        {
            Collider volume = GetComponent<Collider>();
            if (volume != null)
                volume.isTrigger = true;
        }

        private void Update()
        {
            if (once && _fired)
                return;

            Transform player = ResolvePlayer();
            if (player == null)
                return;

            Vector3 sample = SamplePosition(player);
            int frame = Time.frameCount;
            bool crossed = false;
            float progress = 1f;

            if (_hasSample && detectFastMovement)
                crossed = SegmentIntersects(_lastSample, sample, out progress);

            bool inside = Contains(sample);

            if (!_hasSample)
            {
                _hasSample = true;
                _lastSample = sample;

                if (inside)
                    _inside = Activate(player.gameObject, frame, 1f);

                return;
            }

            bool wasInside = _inside;
            _lastSample = sample;

            if (crossed || (inside && !wasInside))
            {
                bool activated = Activate(player.gameObject, frame, crossed ? progress : 1f);
                _inside = activated && inside;
                return;
            }

            _inside = inside;
        }

        /// <summary>Активировать вручную — из кода, UnityEvent или отладочной кнопки.</summary>
        public bool Activate(GameObject source)
        {
            return Activate(source, Time.frameCount, 1f);
        }

        private bool Activate(GameObject source, int frame, float progress)
        {
            if (once && _fired)
                return false;

            if (source == null || !IsPlayer(source))
                return false;

            if (!string.IsNullOrEmpty(requiredStep) && !QuestDirector.IsStepActive(requiredStep))
                return false;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;

            if (state != null && !string.IsNullOrEmpty(skipIfFlag) && state.GetFlag(skipIfFlag))
                return false;

            CompanionActor target = ResolveActor();
            if (target == null || waypoint == null)
                return false;

            if (!target.MoveTo(waypoint, order, frame, progress))
                return false;

            _fired = true;

            if (state != null && !string.IsNullOrEmpty(completedFlag))
                state.SetFlag(completedFlag, true);

            onActivated.Invoke();
            return true;
        }

        private CompanionActor ResolveActor()
        {
            if (actor == null)
                actor = FindAnyObjectByType<CompanionActor>();

            return actor;
        }

        private bool IsPlayer(GameObject candidate)
        {
            return !string.IsNullOrEmpty(playerTag) && candidate.CompareTag(playerTag);
        }

        private Transform ResolvePlayer()
        {
            if (_player != null)
                return _player;

            CompanionActor companion = ResolveActor();
            if (companion != null && companion.Player != null)
            {
                _player = companion.Player;
                return _player;
            }

            _playerSearchTimer -= Time.deltaTime;
            if (_playerSearchTimer > 0f)
                return null;

            _playerSearchTimer = PlayerSearchInterval;

            if (string.IsNullOrEmpty(playerTag))
                return null;

            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null)
                _player = found.transform;

            return _player;
        }

        private Vector3 SamplePosition(Transform player)
        {
            if (_player != player)
            {
                _player = player;
                _playerCollider = player.GetComponent<Collider>();

                if (_playerCollider == null)
                    _playerCollider = player.GetComponentInChildren<Collider>();
            }

            if (_playerCollider != null)
                return _playerCollider.bounds.center;

            return player.position + Vector3.up * 0.9f;
        }

        private bool Contains(Vector3 worldPoint)
        {
            if (_volume == null)
                _volume = GetComponent<Collider>();

            if (_volume == null)
                return false;

            if (_volume is BoxCollider box)
            {
                Vector3 local = box.transform.InverseTransformPoint(worldPoint);
                return new Bounds(box.center, box.size).Contains(local);
            }

            return _volume.bounds.Contains(worldPoint);
        }

        private bool SegmentIntersects(Vector3 from, Vector3 to, out float progress)
        {
            if (_volume == null)
                _volume = GetComponent<Collider>();

            if (_volume == null)
            {
                progress = 0f;
                return false;
            }

            if (_volume is BoxCollider box)
            {
                Vector3 localFrom = box.transform.InverseTransformPoint(from);
                Vector3 localTo = box.transform.InverseTransformPoint(to);
                return SegmentIntersectsBounds(localFrom, localTo, new Bounds(box.center, box.size), out progress);
            }

            return SegmentIntersectsBounds(from, to, _volume.bounds, out progress);
        }

        private static bool SegmentIntersectsBounds(Vector3 from, Vector3 to, Bounds bounds, out float progress)
        {
            Vector3 direction = to - from;
            float tMin = 0f;
            float tMax = 1f;

            for (int axis = 0; axis < 3; axis++)
            {
                float start = from[axis];
                float delta = direction[axis];
                float min = bounds.min[axis];
                float max = bounds.max[axis];

                if (Mathf.Abs(delta) < 0.00001f)
                {
                    if (start < min || start > max)
                    {
                        progress = 0f;
                        return false;
                    }

                    continue;
                }

                float inverse = 1f / delta;
                float t1 = (min - start) * inverse;
                float t2 = (max - start) * inverse;

                if (t1 > t2)
                {
                    float swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);

                if (tMin > tMax)
                {
                    progress = 0f;
                    return false;
                }
            }

            progress = tMin;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            bool selected = UnityEditor.Selection.activeGameObject == gameObject;

            Gizmos.color = selected ? new Color(1f, 0.55f, 0.15f, 1f) : new Color(1f, 0.55f, 0.15f, 0.55f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.25f);

            if (waypoint != null)
            {
                Gizmos.DrawLine(transform.position, waypoint.Position);
                Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.85f);
                Gizmos.DrawWireCube(waypoint.Position + Vector3.up * 0.05f, new Vector3(0.3f, 0.1f, 0.3f));
            }

            if (selected)
            {
                string label = "Companion trigger #" + order;
                if (waypoint != null)
                    label += " → " + waypoint.Id;

                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, label);
            }
        }
#endif
    }
}
