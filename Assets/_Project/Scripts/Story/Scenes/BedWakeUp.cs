using System.Collections;
using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Пробуждение в начале игры
    /// </summary>
    public sealed class BedWakeUp : InteractableBase
    {
        [Tooltip("Коллайдер кровати. Если пусто — ищется объект Sofa_Seat.")]
        [SerializeField] private Collider bed;

        [Tooltip("Флаг, выставляемый после пробуждения")]
        [SerializeField] private string flag = "A1_S01_WokeUp";

        [Tooltip("Сообщение в консоль - временная замена реплике/озвучке.")]
        [SerializeField] private string debugMessage = "Игрок проснулся и встал.";

        [Header("Реплика при пробуждении")]
        [Tooltip("Первая реплика (текст и/или озвучка в поле voice). Играет в момент пробуждения, если не задана последовательность.")]
        [SerializeField] private ThoughtLine wakeLine;

        [Tooltip("Последовательность мыслей: субтитры, озвучка, музыка и указание игроку — всё в одном ассете.")]
        [SerializeField] private ThoughtSequence wakeSequence;

        [Header("Тайминги анимации")]
        [Tooltip("Фаза 1: сесть на кровати, сек.")]
        [SerializeField] private float sitUpDuration = 1.0f;

        [Tooltip("Фаза 2: встать с кровати, сек.")]
        [SerializeField] private float standUpDuration = 1.2f;

        [SerializeField] private Transform standPoint;
        [SerializeField] private CanvasGroup wakeShade;
        [SerializeField, Range(0f, 1f)] private float closedEyesOpacity = 0.84f;
        [SerializeField, Min(0.01f)] private float brightenDuration = 1.4f;
        [SerializeField] private bool hideFirstPersonBodyDuringWake;
        [SerializeField, Range(0f, 1f)] private float wakeCameraSway;
        [SerializeField, Min(0f)] private float wakeBreathDuration;

        private const float LieEyeLocalY = 0.15f;
        private const float SitEyeLocalY = 0.55f;

        private const float LiePitch = -90f;
        private const float SitPitch = -15f;
        private const float EndPitch = 0f;
        private const float SitRoll = -6f;

        private Transform _player;
        private Transform _camera;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private CharacterController _controller;
        private FirstPersonBody _hiddenBody;

        private bool _lying;
        private bool _waking;
        private Vector3 _standPosition;
        private float _standYaw = 90f;

        public override bool CanInteract => base.CanInteract && _lying && !_waking;

        private void Awake()
        {
            SceneLoader loader = GameRoot.Instance != null ? GameRoot.Instance.Loader : null;
            if (loader != null)
                loader.SceneLoaded += OnSceneLoaded;
        }

        private void Start() => StartCoroutine(PlaceWhenReady());

        private void OnDestroy()
        {
            RestoreFirstPersonBody();
            SceneLoader loader = GameRoot.Instance != null ? GameRoot.Instance.Loader : null;
            if (loader != null)
                loader.SceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(string sceneName)
        {
            if (sceneName != gameObject.scene.name)
                return;

            StartCoroutine(PlaceWhenReady());
        }

        private IEnumerator PlaceWhenReady()
        {
            yield return null;
            yield return null;
            PlaceLying();
        }

        private void PlaceLying()
        {
            if (_lying || _waking)
                return;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (state != null && !string.IsNullOrEmpty(flag) && state.GetFlag(flag))
            {
                if (wakeShade != null) wakeShade.alpha = 0f;
                return; // уже просыпались — повторно не укладываем
            }

            ResolvePlayer();
            Collider bedCollider = ResolveBed();

            if (_player == null || bedCollider == null || _camera == null)
            {
                Debug.LogWarning("[BedWakeUp] Не найден игрок или кровать", this);
                return;
            }

            Bounds bounds = bedCollider.bounds;
          
            float rootY = bounds.max.y + 0.10f - LieEyeLocalY;

            _player.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, rootY, bounds.center.z),
                Quaternion.Euler(0f, _standYaw, 0f));


            float floorY = bounds.min.y;
            _standPosition = standPoint != null ? standPoint.position
                : new Vector3(bounds.max.x + 0.45f, floorY + 0.05f, bounds.center.z);

            if (wakeShade != null) wakeShade.alpha = closedEyesOpacity;

            if (hideFirstPersonBodyDuringWake)
            {
                _hiddenBody = _player.GetComponentInChildren<FirstPersonBody>();
                _hiddenBody?.SetExternalView(true);
            }

            if (_controller != null)
                _controller.enabled = false; 

            _camera.localPosition = new Vector3(0f, LieEyeLocalY, 0f);
            _camera.localRotation = Quaternion.Euler(LiePitch, 0f, 0f);

            if (_motor != null)
                _motor.SetControlLocked(true);

            if (_look != null)
                _look.PoseLocked = true;

            _lying = true;
            Debug.Log("[BedWakeUp] Нажмите E, чтобы проснуться.", this);
        }

        protected override void OnInteract(GameObject interactor)
        {
            if (_lying && !_waking)
                StartCoroutine(WakeUpSequence());
        }

        private IEnumerator WakeUpSequence()
        {
            _waking = true;

            if (wakeSequence != null)
                StoryDirector.Ensure().Play(wakeSequence);
            else if (wakeLine != null)
                StoryDirector.Ensure().PlayLine(wakeLine);

            Vector3 lyingPosition = _player.transform.position;
            Quaternion lyingRotation = _player.transform.rotation;
            Vector3 seatEdge = Vector3.Lerp(lyingPosition, _standPosition, 0.72f);
            seatEdge.y = lyingPosition.y;

            if (wakeBreathDuration > 0f)
            {
                yield return Animate(wakeBreathDuration, t =>
                {
                    float breath = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(t * 5f);
                    _camera.localPosition = new Vector3(breath * 0.005f,
                        LieEyeLocalY + breath * 0.012f, 0f);
                    _camera.localRotation = Quaternion.Euler(LiePitch + breath * 0.65f,
                        0f, breath * 0.28f);
                    Brighten(wakeBreathDuration * t);
                });
            }

            // Сначала поворачиваемся и садимся на край кровати.
            yield return Animate(sitUpDuration, t =>
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                if (standPoint != null)
                {
                    float slide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1f, t));
                    _player.transform.SetPositionAndRotation(
                        Vector3.Lerp(lyingPosition, seatEdge, slide),
                        Quaternion.Slerp(lyingRotation, standPoint.rotation, e));
                }
                float elapsed = sitUpDuration * t;
                ApplyWakeCamera(Mathf.Lerp(LieEyeLocalY, SitEyeLocalY, e),
                    Mathf.Lerp(LiePitch, SitPitch, e), Mathf.Lerp(0f, SitRoll, e), elapsed);
                Brighten(wakeBreathDuration + elapsed);
            });

            // Фаза 2 встаём
            Vector3 fromRoot = _player.transform.position;
            float standEye = _motor != null ? _motor.StandEyeHeight : 1.62f;

            yield return Animate(standUpDuration, t =>
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                _player.transform.position = Vector3.Lerp(fromRoot, _standPosition, e);
                float elapsed = sitUpDuration + standUpDuration * t;
                ApplyWakeCamera(Mathf.Lerp(SitEyeLocalY, standEye, e),
                    Mathf.Lerp(SitPitch, EndPitch, e), Mathf.Lerp(SitRoll, 0f, e), elapsed);
                Brighten(wakeBreathDuration + elapsed);
            });

            // контроллер включаем уже в позе стоя.
            _player.transform.position = _standPosition;
            if (wakeShade != null) wakeShade.alpha = 0f;

            if (_controller != null)
                _controller.enabled = true;

            if (_look != null)
            {
                _look.PoseLocked = false;
                _look.ResyncPose();
            }

            if (_motor != null)
                _motor.SetControlLocked(false);

            RestoreFirstPersonBody();
            _lying = false;
            _waking = false;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (state != null && !string.IsNullOrEmpty(flag))
                state.SetFlag(flag, true);

            if (!string.IsNullOrEmpty(debugMessage))
                Debug.Log($"[Interact] {debugMessage}", this);
        }

        private void RestoreFirstPersonBody()
        {
            if (_hiddenBody == null) return;
            _hiddenBody.SetExternalView(false);
            _hiddenBody = null;
        }

        private void ApplyWakeCamera(float eyeHeight, float pitch, float roll, float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, sitUpDuration + standUpDuration));
            float envelope = wakeCameraSway * Mathf.Sin(progress * Mathf.PI);
            float shift = Mathf.Sin(elapsed * 7.4f + 0.34f * Mathf.Sin(elapsed * 3.1f));
            float balance = Mathf.Sin(elapsed * 4.6f + 1.1f);
            float lift = Mathf.Sin(elapsed * 10.2f + 0.48f * Mathf.Sin(elapsed * 3.6f));
            _camera.localPosition = new Vector3(envelope * (0.026f * shift + 0.012f * balance),
                eyeHeight + envelope * (0.027f * lift + 0.011f * balance), 0f);
            _camera.localRotation = Quaternion.Euler(pitch, envelope * 1.25f * balance,
                roll + envelope * (2.65f * shift + 0.85f * balance));
        }

        private void Brighten(float elapsed)
        {
            if (wakeShade == null) return;
            float duration = Mathf.Min(brightenDuration, wakeBreathDuration + sitUpDuration + standUpDuration);
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            wakeShade.alpha = Mathf.Lerp(closedEyesOpacity, 0f, Mathf.SmoothStep(0f, 1f, progress));
        }

        private IEnumerator Animate(float duration, System.Action<float> apply)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duration))
            {
                apply(Mathf.Clamp01(t));
                yield return null;
            }

            apply(1f);
        }

        private void ResolvePlayer()
        {
            if (_player != null)
                return;

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged == null)
                return;

            _player = tagged.transform;
            Camera camera = tagged.GetComponentInChildren<Camera>();
            _camera = camera != null ? camera.transform : null;
            _motor = tagged.GetComponent<PlayerMotor>();
            _look = tagged.GetComponent<PlayerLook>();
            _controller = tagged.GetComponent<CharacterController>();
        }

        private Collider ResolveBed()
        {
            if (bed != null)
                return bed;

            GameObject sofa = GameObject.Find("Sofa_Seat");
            return sofa != null ? sofa.GetComponent<Collider>() : null;
        }
    }
}
