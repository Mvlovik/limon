using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace GameLimon.Staging
{
    /// <summary>Fixed third-person cameras, zone switching and optional placeholder animation.
    /// Assign roomCamera, hallwayCamera and secondaryCameraZone in the Inspector.
    /// Disable waitForApartmentOpening for other scenes, or call ActivateView from a UnityEvent.
    /// Existing PlayerMotor and PlayerInteractor continue to handle movement and E.</summary>
    [MovedFrom(true, "GameLimon.Staging", "Assembly-CSharp", "ApartmentFixedView")]
    [AddComponentMenu("GameLimon/Staging/Fixed Third Person View")]
    [DisallowMultipleComponent]
    public sealed class FixedThirdPersonView : MonoBehaviour
    {
        [Tooltip("Wait for the existing apartment opening. Disable in other scenes.")]
        [SerializeField] private bool waitForApartmentOpening = true;
        [Tooltip("Automatically activate when ready. Otherwise call ActivateView().")]
        [SerializeField] private bool activateAutomatically = true;
        [SerializeField] private Camera roomCamera;
        [SerializeField] private Camera hallwayCamera;
        [SerializeField] private Transform hallwayDoorway;
        [SerializeField] private BoxCollider secondaryCameraZone;
        [SerializeField] private float doorwayHysteresis = 0.08f;
        [SerializeField] private Transform standIn;
        [SerializeField] private Collider[] interactionTargets;
        [SerializeField] private float interactionRange = 2.35f;

        public bool IsActive { get; private set; }
        public bool IsInHallway { get; private set; }
        public string FocusedTargetName { get; private set; }

        private ApartmentOpening _opening;
        private Transform _player;
        private PlayerLook _look;
        private PlayerMotor _motor;
        private Camera _playerCamera;
        private AudioListener _playerListener;
        private bool _used, _oldPoseLocked, _oldCameraEnabled, _oldListenerEnabled;
        private bool _activationRequested;
        private Vector3 _lastPosition;
        private float _movementYaw;
        private Transform _body, _head, _leftArm, _rightArm, _leftLeg, _rightLeg, _leftKnee, _rightKnee;
        private float _stepPhase, _walkBlend, _idleTime;
        private readonly RaycastHit[] _interactionHits = new RaycastHit[16];

        private void Awake()
        {
            if (standIn != null)
            {
                _body = standIn.Find("Body");
                _head = standIn.Find("Body/HeadPivot");
                _leftArm = standIn.Find("Body/LeftArm");
                _rightArm = standIn.Find("Body/RightArm");
                _leftLeg = standIn.Find("Body/LeftLeg");
                _rightLeg = standIn.Find("Body/RightLeg");
                _leftKnee = standIn.Find("Body/LeftLeg/Knee");
                _rightKnee = standIn.Find("Body/RightLeg/Knee");
            }
            if (roomCamera != null) roomCamera.gameObject.SetActive(false);
            if (hallwayCamera != null) hallwayCamera.gameObject.SetActive(false);
            if (standIn != null) standIn.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_player == null)
            {
                var root = GameRoot.Instance;
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (root == null || playerObject == null) return;
                _opening = root.GetComponentInParent<ApartmentOpening>();
                _player = playerObject.transform;
                _look = playerObject.GetComponent<PlayerLook>();
                _motor = playerObject.GetComponent<PlayerMotor>();
                _playerCamera = playerObject.GetComponentInChildren<Camera>(true);
                _playerListener = _playerCamera != null ? _playerCamera.GetComponent<AudioListener>() : null;
            }

            if (!_used && (_activationRequested || (activateAutomatically &&
                (!waitForApartmentOpening || (_opening != null && _opening.HasPlayed && !_opening.IsLookingAtWindow)))) &&
                roomCamera != null && _playerCamera != null)
                EnterFixedView();
        }

        public void ActivateView()
        {
            if (IsActive) return;
            _used = false;
            _activationRequested = true;
            if (!IsActive && _player != null && roomCamera != null && _playerCamera != null)
                EnterFixedView();
        }

        private void EnterFixedView()
        {
            _used = true;
            _activationRequested = false;
            _oldPoseLocked = _look != null && _look.PoseLocked;
            _oldCameraEnabled = _playerCamera.enabled;
            _oldListenerEnabled = _playerListener != null && _playerListener.enabled;
            if (_look != null) _look.PoseLocked = true;
            _playerCamera.enabled = false;
            if (_playerListener != null) _playerListener.enabled = false;
            roomCamera.gameObject.SetActive(true);
            _movementYaw = roomCamera.transform.eulerAngles.y;
            if (standIn != null) standIn.gameObject.SetActive(true);
            if (standIn != null) standIn.rotation = _player.rotation;
            _lastPosition = _player.position;
            IsActive = true;
        }

        private void LateUpdate()
        {
            if (!IsActive || _player == null) return;

            UpdateRoomView();
            var activeCamera = IsInHallway ? hallwayCamera : roomCamera;
            var input = GameRoot.Instance != null ? GameRoot.Instance.Input : null;
            // Preserve the held walking direction across a cut; use the new view after release.
            if (input == null || input.Move.sqrMagnitude < 0.0001f)
                _movementYaw = activeCamera.transform.eulerAngles.y;
            _player.rotation = Quaternion.Euler(0f, _movementYaw, 0f);
            if (_look != null) _look.PoseLocked = true;
            _playerCamera.transform.localPosition = new Vector3(0f, _motor != null ? _motor.EyeHeight : 1.62f, 0f);
            AimInteractionView();

            if (standIn != null)
            {
                standIn.position = _player.position;
                standIn.localScale = new Vector3(1f, _motor != null ? 1f - 0.35f * _motor.CrouchAmount : 1f, 1f);
                var movement = _player.position - _lastPosition;
                movement.y = 0f;
                if (movement.sqrMagnitude > 0.00001f)
                    standIn.rotation = Quaternion.Slerp(standIn.rotation, Quaternion.LookRotation(movement), 12f * Time.deltaTime);
                AnimateCharacter(movement);
            }
            _lastPosition = _player.position;
        }

        private void AnimateCharacter(Vector3 movement)
        {
            if (_body == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float travel = movement.magnitude;
            // Teleports and scene transitions must not produce a burst of steps.
            if (travel > 0.5f) travel = 0f;
            float speed = travel / dt;
            _walkBlend = Mathf.MoveTowards(_walkBlend, Mathf.Clamp01(speed / 1.6f), 6f * dt);
            _stepPhase += travel * (Mathf.PI * 2f / 1.35f);
            _idleTime += dt;
            float step = Mathf.Sin(_stepPhase);
            float stride = step * _walkBlend;
            float breath = Mathf.Sin(_idleTime * 1.8f);
            _body.localPosition = new Vector3(0f,
                0.012f * (1f - Mathf.Cos(_stepPhase * 2f)) * _walkBlend + 0.003f * breath * (1f - _walkBlend), 0f);
            _body.localRotation = Quaternion.Euler(2f * _walkBlend, 1.5f * stride, 1.2f * stride);
            if (_head != null) _head.localRotation = Quaternion.Euler(0.5f * breath * (1f - _walkBlend), -stride, -0.6f * stride);
            if (_leftArm != null) _leftArm.localRotation = Quaternion.Euler(16f * stride, 0f, -4f);
            if (_rightArm != null) _rightArm.localRotation = Quaternion.Euler(-16f * stride, 0f, 4f);
            if (_leftLeg != null) _leftLeg.localRotation = Quaternion.Euler(-22f * stride, 0f, 0f);
            if (_rightLeg != null) _rightLeg.localRotation = Quaternion.Euler(22f * stride, 0f, 0f);
            if (_leftKnee != null) _leftKnee.localRotation = Quaternion.Euler(26f * Mathf.Max(0f, stride), 0f, 0f);
            if (_rightKnee != null) _rightKnee.localRotation = Quaternion.Euler(26f * Mathf.Max(0f, -stride), 0f, 0f);
        }

        private void UpdateRoomView()
        {
            if (hallwayCamera == null) return;
            bool hallway = IsInHallway;
            if (secondaryCameraZone != null)
            {
                // Test the player's position inside the room, not the capsule touching the doorway.
                Vector3 point = secondaryCameraZone.transform.InverseTransformPoint(_player.position + Vector3.up * 0.9f);
                Bounds zone = new Bounds(secondaryCameraZone.center, secondaryCameraZone.size);
                var scale = secondaryCameraZone.transform.lossyScale;
                float margin = Mathf.Max(0f, doorwayHysteresis) * (IsInHallway ? 2f : -2f);
                zone.Expand(new Vector3(margin / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                    margin / Mathf.Max(0.001f, Mathf.Abs(scale.y)), margin / Mathf.Max(0.001f, Mathf.Abs(scale.z))));
                hallway = secondaryCameraZone.enabled && secondaryCameraZone.gameObject.activeInHierarchy &&
                    zone.Contains(point);
            }
            else if (hallwayDoorway != null)
            {
                float side = hallwayDoorway.InverseTransformPoint(_player.position).z;
                if (side < -doorwayHysteresis) hallway = true;
                else if (side > doorwayHysteresis) hallway = false;
            }
            else return;
            if (hallway == IsInHallway) return;
            IsInHallway = hallway;
            // Only one camera and its audio listener are active at a time.
            roomCamera.gameObject.SetActive(!hallway);
            hallwayCamera.gameObject.SetActive(hallway);
        }

        private void AimInteractionView()
        {
            var view = _playerCamera.transform;
            float nearest = interactionRange * interactionRange;
            Collider selected = null;
            if (interactionTargets != null)
                foreach (var target in interactionTargets)
                {
                    if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                    var interaction = target.GetComponent<InteractableBase>();
                    if (interaction == null || !interaction.isActiveAndEnabled || !interaction.CanInteract) continue;
                    var offset = target.bounds.center - view.position;
                    if (offset.sqrMagnitude >= nearest) continue;
                    int count = Physics.RaycastNonAlloc(view.position, offset.normalized, _interactionHits,
                        offset.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore);
                    Collider first = null;
                    float firstDistance = float.PositiveInfinity;
                    for (int i = 0; i < count; i++)
                    {
                        var hit = _interactionHits[i];
                        if (hit.collider == null || hit.collider.transform.IsChildOf(_player)) continue;
                        if (hit.distance < firstDistance)
                        {
                            firstDistance = hit.distance;
                            first = hit.collider;
                        }
                    }
                    if (first != target) continue;
                    nearest = offset.sqrMagnitude;
                    selected = target;
                }

            // Keep the original raycast/E/prompt flow; only aim its invisible first-person view.
            view.rotation = selected != null
                ? Quaternion.LookRotation(selected.bounds.center - view.position)
                : Quaternion.Euler(-90f, 0f, 0f);
            FocusedTargetName = selected != null ? selected.name : null;
        }

        public void DeactivateView()
        {
            _activationRequested = false;
            _used = true;
            if (!IsActive) return;
            IsActive = false;
            if (roomCamera != null) roomCamera.gameObject.SetActive(false);
            if (hallwayCamera != null) hallwayCamera.gameObject.SetActive(false);
            if (standIn != null) standIn.gameObject.SetActive(false);
            if (_playerCamera != null)
            {
                _playerCamera.transform.localRotation = Quaternion.identity;
                _playerCamera.enabled = _oldCameraEnabled;
            }
            if (_playerListener != null) _playerListener.enabled = _oldListenerEnabled;
            if (_look != null)
            {
                _look.ResyncPose();
                _look.PoseLocked = _oldPoseLocked;
            }
            IsInHallway = false;
        }

        private void OnDisable()
        {
            DeactivateView();
            _used = false;
            IsInHallway = false;
        }
    }
}
