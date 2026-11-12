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

        [Header("Тайминги анимации")]
        [Tooltip("Фаза 1: сесть на кровати, сек.")]
        [SerializeField] private float sitUpDuration = 1.0f;

        [Tooltip("Фаза 2: встать с кровати, сек.")]
        [SerializeField] private float standUpDuration = 1.2f;

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
                return; // уже просыпались — повторно не укладываем

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
            _standPosition = new Vector3(bounds.max.x + 0.45f, floorY + 0.05f, bounds.center.z);

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

            // Фаза 1 садимся.
            yield return Animate(sitUpDuration, t =>
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                _camera.localPosition = new Vector3(0f, Mathf.Lerp(LieEyeLocalY, SitEyeLocalY, e), 0f);
                _camera.localRotation = Quaternion.Euler(
                    Mathf.Lerp(LiePitch, SitPitch, e), 0f, Mathf.Lerp(0f, SitRoll, e));
            });

            // Фаза 2 встаём
            Vector3 fromRoot = _player.transform.position;
            float standEye = _motor != null ? _motor.StandEyeHeight : 1.62f;

            yield return Animate(standUpDuration, t =>
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                _player.transform.position = Vector3.Lerp(fromRoot, _standPosition, e);
                _camera.localPosition = new Vector3(0f, Mathf.Lerp(SitEyeLocalY, standEye, e), 0f);
                _camera.localRotation = Quaternion.Euler(
                    Mathf.Lerp(SitPitch, EndPitch, e), 0f, Mathf.Lerp(SitRoll, 0f, e));
            });

            // контроллер включаем уже в позе стоя.
            _player.transform.position = _standPosition;

            if (_controller != null)
                _controller.enabled = true;

            if (_look != null)
            {
                _look.PoseLocked = false;
                _look.ResyncPose();
            }

            if (_motor != null)
                _motor.SetControlLocked(false);

            _lying = false;
            _waking = false;

            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (state != null && !string.IsNullOrEmpty(flag))
                state.SetFlag(flag, true);

            if (!string.IsNullOrEmpty(debugMessage))
                Debug.Log($"[Interact] {debugMessage}", this);
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
