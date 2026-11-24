using System.Collections;
using GameLimon.Companions;
using GameLimon.Core;
using GameLimon.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Stages the under-bridge sequence, including its interactive choice and shared ending.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RustamBridgeCutscene : SceneCutscene
    {
        private const string PlayerTag = "Player";

        [Header("Staging marks")]
        [SerializeField] private Transform playerIntroMark;
        [SerializeField] private Transform playerFinalMark;
        [SerializeField] private Transform joeIntroMark;

        [Header("Standing cast")]
        [SerializeField] private GameObject rustamStanding;
        [SerializeField] private GameObject thugAStanding;
        [SerializeField] private GameObject thugBStanding;
        [SerializeField] private GameObject banditStanding;
        [SerializeField] private GameObject pistolInThugHand;

        [Header("Aftermath bodies")]
        [SerializeField] private GameObject rustamBody;
        [SerializeField] private GameObject thugABody;
        [SerializeField] private GameObject thugBBody;
        [SerializeField] private GameObject[] aftermathBlood;

        [Header("Finale")]
        [SerializeField] private GameObject finaleProps;
        [SerializeField] private GameObject handPrefab;
        [SerializeField] private GameObject fireEffectPrefab;
        [SerializeField] private Material childSleeveMaterial;

        [Header("Objects hidden for the duration")]
        [SerializeField] private GameObject[] hideDuringCutscene;

        [Header("Dialogue")]
        [SerializeField] private BridgeDialogueTalk talk;

        [Header("Idle animations (before the shootout)")]
        [SerializeField] private BridgeIdleActor[] idleActors;

        [Header("Audio")]
        [SerializeField] private AudioSource cutsceneAudio;

        [Header("Dialogue speech")]
        [SerializeField] private AudioClip speechClip;
        [SerializeField, Range(0f, 1f)] private float speechVolume = 0.32f;

        [Header("Shootout and aftermath audio")]
        [SerializeField] private AudioClip shootClip;
        [SerializeField] private AudioClip ringingClip;
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField] private AudioClip sadTheme;
        [SerializeField] private AudioClip strainedVoice;
        [SerializeField, Range(0f, 1f)] private float shootVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float ringingVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.7f;

        private CutsceneOverlayUI _ui;
        private GameObject _player;
        private FirstPersonBody _playerBody;
        private GameObject _joe;
        private CompanionActor _joeCompanion;
        private AudioSource _shootSourceA;
        private AudioSource _shootSourceB;
        private AudioSource _shootSourceC;
        private AudioSource _ringingSource;
        private AudioSource _heartbeatSource;
        private AudioSource _speechSource;
        private Coroutine _speechRoutine;
        private AudioSource _musicSource;
        private GameObject _heroPistol;
        private Camera _shot;
        private CinemachineBrain _brain;
        private bool _brainWasEnabled;
        private Vector2 _originalLens;
        private Reach _captorHold;
        private GameObject _campfire;
        private Vector3 _bloodScale;

        private GameObject[] _fallActors;
        private Vector3[] _fallPositions;
        private Quaternion[] _fallRotations;

        private void Awake()
        {
            EnsureAudio();
        }

        protected override void PrepareCutscene()
        {
            base.PrepareCutscene();

            _ui = CutsceneOverlayUI.Create();
            _ui.HideAll();
            GameRoot.Instance?.Audio?.StopMusic(.2f);

            _player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (_player != null)
            {
                _playerBody = _player.GetComponentInChildren<FirstPersonBody>();
                if (_playerBody != null)
                    _playerBody.enabled = false;

                // FirstPersonBody normally hides the head for the FPS view. The
                // cutscene needs the real head for the final close-up. Only the
                // active body variant matters — other bodies stay disabled.
                Transform body = _playerBody != null ? _playerBody.transform : _player.transform;
                RestoreHeadScale(body);
                StopLocomotion(body);

                if (playerIntroMark != null)
                    StagePlayer(playerIntroMark.position, playerIntroMark.rotation);
            }

            _joe = FindObjectByName("Joe");
            if (_joe != null)
            {
                _joeCompanion = _joe.GetComponent<CompanionActor>();
                if (_joeCompanion != null)
                    _joeCompanion.enabled = false;

                StopLocomotion(_joe.transform);

                if (joeIntroMark != null)
                    _joe.transform.rotation = joeIntroMark.rotation;
            }

            RestoreStandingPoses();
            CaptureStandingPoses();
            SetStandingCast(true);
            SetAftermath(false, true);
            SetHiddenState(true);

            if (finaleProps != null)
                finaleProps.SetActive(false);

            _shot = GetComponent<Camera>();
            _brain = GetComponent<CinemachineBrain>();
            _brainWasEnabled = _brain != null && _brain.enabled;
            if (_brain != null) _brain.enabled = false;
            _originalLens = new Vector2(_shot.fieldOfView, _shot.nearClipPlane);
            _shot.nearClipPlane = .035f;

            Vector3 eye = FindObjectByName("CM_Shot_Approach").transform.position;
            SetShot(LookAt(eye, ObservationAim()));
            _shot.fieldOfView = 58f;
            Debug.Log("[BridgeCutscene] prepared");
        }

        protected override IEnumerator PlayContent()
        {
            Vector3 audience = ObservationAim();
            Vector3 behindCorner = FindObjectByName("CM_Shot_Approach").transform.position;
            Vector3 peekEye = FindObjectByName("CM_Shot_Nook").transform.position;
            Pose peek = LookAt(peekEye, audience);
            Pose cover = LookAt(behindCorner, audience);
            _shot.fieldOfView = 58f;
            SetShot(cover);
            SetPlayerVisible(false);
            SetIdleActors(true);
            Transform guard = ResolveThugA().transform;
            Transform speaker = ResolveBandit().transform;
            Vector3 hostagePosition = rustamStanding.transform.position;
            Vector3 towardCover = Vector3.ProjectOnPlane(peekEye - hostagePosition, Vector3.up).normalized;
            Vector3 screenRight = Vector3.Cross(towardCover, Vector3.up);
            Vector3 guardPosition = hostagePosition + screenRight * .62f + towardCover * .22f;
            guardPosition.y = GroundAt(guardPosition);
            guard.SetPositionAndRotation(guardPosition, FaceDirection(hostagePosition - guardPosition));
            Vector3 speakerPosition = hostagePosition - screenRight * .88f + towardCover * .60f;
            speakerPosition.y = GroundAt(speakerPosition);
            speaker.SetPositionAndRotation(speakerPosition, FaceDirection(hostagePosition - speakerPosition));
            _captorHold = Arm(guard.gameObject, "Left");
            Transform captiveArm = Bone(rustamStanding, "RightArm");
            if (captiveArm != null) _captorHold.Target = () => captiveArm.position - Vector3.up * .14f;
            var fire = FindObjectByName("bonfire");
            if (fire != null && fireEffectPrefab != null && _campfire == null)
            {
                _campfire = Instantiate(fireEffectPrefab, fire.transform.position + Vector3.up * .20f,
                    Quaternion.identity, transform.parent);
                _campfire.name = "Bridge cinematic fire";
                var fireLight = _campfire.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1f, .39f, .13f);
                fireLight.range = 4.3f;
                fireLight.intensity = 2.25f;
            }
            yield return HoldCamera(cover, .42f, .21f);
            yield return MoveCamera(peek, .80f, .36f);
            Debug.Log("[BridgeCutscene] peek framed");
            yield return HoldCamera(peek, 2.2f, .35f);

            // Entry belongs to the player. Stage Joe behind the camera before he is revealed.
            if (Vector3.Distance(_joe.transform.position, joeIntroMark.position) > .35f)
                _joe.transform.SetPositionAndRotation(joeIntroMark.position, joeIntroMark.rotation);
            Vector3 joeEye = Face(_joe, joeIntroMark.position);
            Vector3 listenEye = playerIntroMark.position;
            Pose listen = ListenPose(listenEye, joeEye);
            yield return MoveCamera(cover, .58f, .30f);
            Quaternion towardJoe = FaceDirection(joeIntroMark.position - listenEye);
            Quaternion oldYaw = _player.transform.rotation;
            Quaternion joeYaw = _joe.transform.rotation;
            Quaternion towardHero = FaceDirection(listenEye - _joe.transform.position);
            yield return MoveCamera(listen, .82f, .18f, progress =>
            {
                _player.transform.rotation = Quaternion.Slerp(oldYaw, towardJoe, progress);
                _joe.transform.rotation = Quaternion.Slerp(joeYaw, towardHero, progress);
                _shot.fieldOfView = Mathf.Lerp(58f, 46f, progress);
            },
                () => SetPlayerVisible(true));

            _player.GetComponent<PlayerLook>()?.LockCursor(true);
            Debug.Log("[BridgeCutscene] listening framed");
            _speechSource.transform.position = audience;
            _speechSource.spatialBlend = .77f;
            yield return PlayOffscreenLine("Рустам", "Я вам все рассказал еще тогда! Оставьте меня в покое!", .68f);
            yield return PlayOffscreenLine("Бандит", "Не все, старик. Ты знаешь, кого мы ищем. Где он?", .82f);

            CutsceneChoice choice = CutsceneChoice.Wait;
            yield return _ui.ShowChoice(10f, selected => choice = selected);
            StagePlayer(listenEye, towardJoe);
            SetShot(listen);
            Debug.Log($"[BridgeCutscene] choice: {choice}");

            if (choice == CutsceneChoice.Act)
                yield return PlayActBranch(cover, peek);
            else
                yield return PlayWaitBranch(cover, peek, audience);

            yield return GunfireAndFade(peek, choice);
            yield return PlayAftermath(peek);
            yield return PlayFinale();

            _ui.HideAll();
            _ui.DestroyOverlay();
            _ui = null;
            StopAftermathAudio();
            Debug.Log("[BridgeCutscene] content finished");
        }

        private Vector3 ObservationAim()
        {
            Vector3 center = Vector3.Lerp(rustamStanding.transform.position, ResolveBandit().transform.position, .5f);
            return center + Vector3.up * 1.03f;
        }

        private IEnumerator PlayWaitBranch(Pose cover, Pose peek, Vector3 audience)
        {
            yield return PlayTalkLine("Дима", "Хорошо, будь на готове.", 52f, .56f);
            SetPlayerVisible(false);
            yield return MoveCamera(peek, 1.18f, .38f, progress => _shot.fieldOfView = Mathf.Lerp(46f, 58f, progress), null,
                cover.position);
            _speechSource.transform.position = audience;
            _speechSource.spatialBlend = .22f;
            yield return PlayTalkLine("Бандит", "У меня нет времени. Говори, или я те колено прострелю!", 48f, .60f, 1.5f);
            if (strainedVoice != null)
            {
                StopSpeech();
                _speechSource.volume = Mathf.Clamp01(speechVolume * 2f);
                _speechSource.PlayOneShot(strainedVoice);
                yield return _ui.PlayLine("Рустам", "А-а!", 48f, .72f);
                _speechSource.Stop();
            }
            else
            {
                yield return PlayTalkLine("Рустам", "А-а!", 48f, .72f, 1.65f);
            }
            _speechSource.spatialBlend = 0f;
            yield return PlayTalkLine("Джо", "Сейчас!", 48f, .39f);
        }

        private IEnumerator PlayActBranch(Pose cover, Pose peek)
        {
            SetPlayerVisible(false);
            PrepareHeroPistol();
            if (_heroPistol != null) _heroPistol.SetActive(true);
            _shot.fieldOfView = 58f;
            yield return MoveCamera(peek, .64f, 1f, null, null, cover.position);
        }

        private IEnumerator GunfireAndFade(Pose resume, CutsceneChoice choice)
        {
            yield return Fade(1f, .18f);
            SetIdleActors(false);
            _captorHold = default;
            if (_heroPistol != null) _heroPistol.SetActive(false);
            int shots = choice == CutsceneChoice.Wait ? 4 : 3;
            for (int i = 0; i < shots; i++)
            {
                PlayShoot(i);
                yield return new WaitForSecondsRealtime(i == 1 ? .32f : .23f);
            }

            StartCoroutine(FadeLoop(_ringingSource, ringingVolume, .34f));
            StartCoroutine(FadeLoop(_heartbeatSource, heartbeatVolume, .46f));
            LayDownAfterShot();
            if (pistolInThugHand != null) pistolInThugHand.SetActive(false);
            SetShot(resume);
            yield return Fade(0f, .75f);
            StartCoroutine(FadeLoop(_ringingSource, ringingVolume * .22f, 2.9f));
            yield return new WaitForSecondsRealtime(.44f);
            Debug.Log($"[BridgeCutscene] black-screen shots: {shots}");
        }

        private IEnumerator PlayAftermath(Pose peek)
        {
            Transform rustam = rustamStanding.transform;
            Transform nearMark = FindObjectByName("CM_Shot_Death").transform;
            Vector3 viewer = Vector3.ProjectOnPlane(nearMark.position - rustam.position, Vector3.up).normalized;
            Quaternion rollLeft = rustam.rotation * Quaternion.Euler(0f, -64f, 0f);
            Quaternion rollRight = rustam.rotation * Quaternion.Euler(0f, 64f, 0f);
            rustam.rotation = Vector3.Dot(rollLeft * Vector3.forward, viewer) < Vector3.Dot(rollRight * Vector3.forward, viewer)
                ? rollLeft : rollRight;
            Transform firstShoulder = Bone(rustamStanding, "LeftArm");
            Transform secondShoulder = Bone(rustamStanding, "RightArm");
            Vector3 shoulder = firstShoulder != null && secondShoulder != null
                ? Vector3.Distance(nearMark.position, firstShoulder.position) < Vector3.Distance(nearMark.position, secondShoulder.position)
                    ? firstShoulder.position : secondShoulder.position
                : Face(rustamStanding, rustamBody.transform.position) - rustam.up * .24f;
            Vector3 towardCamera = Vector3.ProjectOnPlane(nearMark.position - shoulder, Vector3.up).normalized;
            Vector3 lowEye = shoulder + towardCamera * .55f + Vector3.up * .42f;
            Pose knees = LookAt(lowEye, shoulder);
            Pose running = LookAt(knees.position + Vector3.up * .70f, shoulder);
            yield return MoveCamera(running, 1.48f, 2.30f, via: peek.position + Vector3.forward * 1.65f);
            yield return MoveCamera(knees, .43f, .56f);

            Debug.Log("[BridgeCutscene] shoulder framed");
            yield return PlayTalkLine("Рустам", "Слушай… тумбочка… у кровати… там подарок от твоего отца… посмотри…", 38f, 2.15f, animateSpeaker: false);
            Vector3 callFrom = joeIntroMark.position + Vector3.up * 1.48f;
            _speechSource.transform.position = callFrom;
            _speechSource.spatialBlend = .72f;
            if (_musicSource != null) StartCoroutine(FadeLoop(_musicSource, .18f, .62f));
            yield return PlayTalkLine("Джо", "Дэн… Дэн! Иди сюда… Быстрее!", 46f, .8f);
            yield return MoveCamera(LookAt(knees.position, callFrom), .43f, .70f);
            yield return Fade(1f, .28f);
        }

        private IEnumerator PlayFinale()
        {
            SetPlayerHeadVisible(true);
            SetPlayerVisible(true);
            SetLightEnabled("RimLight_Warm", false);

            Vector3 feet = playerFinalMark.position;
            Vector3 forward = Vector3.ProjectOnPlane(joeIntroMark.position - feet, Vector3.up).normalized;
            feet += Vector3.Cross(forward, Vector3.up) * 1.15f;
            feet.y = GroundAt(feet);
            StagePlayer(feet, FaceDirection(forward));
            StopLocomotion(_player.transform);
            Vector3 originalBodyPosition = _playerBody != null ? _playerBody.transform.localPosition : Vector3.zero;
            AlignBootsToGround(feet.y);
            Vector3 face = Face(_player, feet);
            _shot.fieldOfView = 49f;
            Vector3 startingEye = feet + forward * 1.40f + Vector3.up * .12f;
            Pose low = new Pose(startingEye, Quaternion.LookRotation(-forward) * Quaternion.Euler(-18f, 0f, 0f));
            Pose stop = LookAt(feet + forward * .56f + Vector3.up * (face.y - feet.y), face);
            SetShot(low);
            StartCoroutine(FadeLoop(_ringingSource, ringingVolume * 1.35f, 5.5f));
            StartCoroutine(Fade(0f, .48f));
            yield return MoveCamera(stop, 2.35f, .22f);
            Debug.Log("[BridgeCutscene] close-up framed");
            yield return new WaitForSecondsRealtime(4f);

            Transform toy = finaleProps != null ? finaleProps.transform.Find("ChildToy") : null;
            Vector3 toyPoint = toy != null ? toy.position : stop.position + stop.rotation * Vector3.left;
            toyPoint.y = GroundAt(toyPoint);
            Pose floor = LookAt(stop.position, toyPoint + Vector3.up * .04f);
            PrepareFinaleProps(floor, toyPoint);
            if (finaleProps != null) finaleProps.SetActive(true);
            yield return MoveCamera(floor, .47f, 0f);
            SetPlayerVisible(false);
            if (_playerBody != null) _playerBody.transform.localPosition = originalBodyPosition;
            yield return SpreadBlood(1.25f);

            GameRoot.Instance?.UI?.SetFade(1f);
            Debug.Log("[BridgeCutscene] final cut to black");
            yield return new WaitForSecondsRealtime(.48f);
        }

        private void AlignBootsToGround(float groundHeight)
        {
            if (_playerBody == null) return;
            var skin = _playerBody.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null || skin.sharedMesh == null) return;

            var pose = new Mesh();
            skin.BakeMesh(pose);
            float soleHeight = float.PositiveInfinity;
            foreach (Vector3 vertex in pose.vertices)
                soleHeight = Mathf.Min(soleHeight, skin.transform.TransformPoint(vertex).y);
            Destroy(pose);

            if (!float.IsPositiveInfinity(soleHeight))
                _playerBody.transform.position += Vector3.up * (groundHeight - soleHeight);
        }

        private void PrepareHeroPistol()
        {
            if (pistolInThugHand == null || _heroPistol != null) return;
            _heroPistol = Instantiate(pistolInThugHand, _shot.transform, false);
            _heroPistol.name = "Bridge cinematic pistol";
            _heroPistol.transform.SetLocalPositionAndRotation(new Vector3(.22f, -.20f, .46f),
                Quaternion.Euler(-9f, 59f, 5f));
            foreach (Collider shape in _heroPistol.GetComponentsInChildren<Collider>(true))
                shape.enabled = false;
            foreach (Renderer part in _heroPistol.GetComponentsInChildren<Renderer>(true))
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Pose ListenPose(Vector3 hero, Vector3 joeEye)
        {
            Vector3 heroEye = Face(_player, hero);
            Vector3 midpoint = (heroEye + joeEye) * .5f;
            Vector3 lookAcross = Vector3.ProjectOnPlane(joeEye - heroEye, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, lookAcross);
            if (Vector3.Dot(side, Vector3.forward) < 0f) side = -side;
            return LookAt(midpoint + side * 1.54f - Vector3.up * .04f, midpoint + Vector3.up * .015f);
        }

        private void StagePlayer(Vector3 position, Quaternion rotation)
        {
            var controller = _player.GetComponent<CharacterController>();
            bool enabled = controller != null && controller.enabled;
            if (enabled) controller.enabled = false;
            _player.transform.SetPositionAndRotation(position, rotation);
            if (enabled) controller.enabled = true;
        }

        private void SetPlayerVisible(bool visible)
        {
            if (_playerBody != null) _playerBody.SetExternalView(!visible);
        }

        private void SetPlayerHeadVisible(bool visible)
        {
            if (_playerBody == null) return;
            if (visible)
            {
                RestoreHeadScale(_playerBody.transform);
                return;
            }
            var neck = Bone(_playerBody.gameObject, "Neck");
            if (neck != null) neck.localScale = Vector3.one * .001f;
        }

        private static Transform Bone(GameObject actor, string name)
        {
            if (actor == null) return null;
            var animator = actor.GetComponentInChildren<Animator>();
            Transform root = animator != null ? animator.transform : actor.transform;
            foreach (Transform part in root.GetComponentsInChildren<Transform>())
                if (part.name == name || part.name.EndsWith(":" + name, System.StringComparison.Ordinal))
                    return part;
            return null;
        }

        private static Vector3 Face(GameObject actor, Vector3 fallback)
        {
            Transform head = Bone(actor, "Head");
            return head != null ? head.position + actor.transform.up * .08f : fallback + Vector3.up * 1.62f;
        }

        private static Quaternion FaceDirection(Vector3 direction)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            return flat.sqrMagnitude > .0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        }

        private IEnumerator Fade(float alpha, float seconds)
        {
            var ui = GameRoot.Instance?.UI;
            if (ui != null) yield return ui.FadeTo(alpha, seconds);
            else yield return new WaitForSecondsRealtime(seconds);
        }

        private static Pose LookAt(Vector3 eye, Vector3 at) => new Pose(eye, Quaternion.LookRotation(at - eye));
        private static float Ease(float t) => t * t * (3f - 2f * t);
        private Pose CurrentShot => new Pose(_shot.transform.position, _shot.transform.rotation);
        private void SetShot(Pose pose) => _shot.transform.SetPositionAndRotation(pose.position, pose.rotation);

        private IEnumerator MoveCamera(Pose end, float duration, float handHeld, System.Action<float> update = null,
            System.Action midpoint = null, Vector3? via = null)
        {
            Pose start = CurrentShot;
            bool moved = false;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float u = Ease(t / duration);
                if (!moved && u > .55f) { midpoint?.Invoke(); moved = true; }
                update?.Invoke(u);
                Vector3 point = via.HasValue
                    ? Vector3.Lerp(Vector3.Lerp(start.position, via.Value, u), Vector3.Lerp(via.Value, end.position, u), u)
                    : Vector3.Lerp(start.position, end.position, u);
                Pose pose = new Pose(point, Quaternion.Slerp(start.rotation, end.rotation, u));
                SetShot(Shake(pose, t, handHeld * Mathf.Sin(Mathf.PI * t / duration)));
                yield return null;
            }
            if (!moved) midpoint?.Invoke();
            update?.Invoke(1f);
            SetShot(end);
        }

        private IEnumerator HoldCamera(Pose basePose, float duration, float handHeld)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetShot(Shake(basePose, t, handHeld));
                yield return null;
            }
            SetShot(basePose);
        }

        private static Pose Shake(Pose pose, float t, float intensity)
        {
            float bob = Mathf.Sin(t * 17f) + .32f * Mathf.Sin(t * 23.9f + .32f);
            float sway = Mathf.Sin(t * 8.9f + .6f);
            var drift = new Vector3(.016f * sway, .022f * bob, .006f * Mathf.Sin(t * 10f)) * intensity;
            return new Pose(pose.position + pose.rotation * drift,
                pose.rotation * Quaternion.Euler(intensity * new Vector3(.75f * bob, .49f * sway, .85f * sway)));
        }

        private struct Reach
        {
            public Transform Upper, Lower, Hand;
            public System.Func<Vector3> Target;
            public bool Valid => Upper != null && Lower != null && Hand != null;

            public void Apply()
            {
                if (!Valid || Target == null) return;
                Vector3 destination = Target();
                Vector3 toward = destination - Upper.position;
                float upperLength = Vector3.Distance(Upper.position, Lower.position);
                float lowerLength = Vector3.Distance(Lower.position, Hand.position);
                if (toward.sqrMagnitude < .0001f || upperLength < .01f || lowerLength < .01f) return;
                Vector3 axis = toward.normalized;
                float reach = Mathf.Clamp(toward.magnitude, Mathf.Abs(upperLength - lowerLength) + .006f,
                    upperLength + lowerLength - .006f);
                float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2f * reach);
                float bend = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
                Vector3 elbow = Upper.position + axis * along +
                    Vector3.ProjectOnPlane(Vector3.down + .3f * Upper.right, axis).normalized * bend;
                Upper.rotation = Quaternion.FromToRotation(Lower.position - Upper.position, elbow - Upper.position) * Upper.rotation;
                Lower.rotation = Quaternion.FromToRotation(Hand.position - Lower.position, destination - Lower.position) * Lower.rotation;
            }
        }

        private static Reach Arm(GameObject actor, string side) => new Reach
        {
            Upper = Bone(actor, side + "Arm"),
            Lower = Bone(actor, side + "ForeArm"),
            Hand = Bone(actor, side + "Hand")
        };

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            _captorHold.Apply();
        }

        private static float GroundAt(Vector3 point) =>
            TryGetGround(point, out Vector3 hit, out _) ? hit.y : point.y;

        private void PrepareFinaleProps(Pose floor, Vector3 toyPoint)
        {
            if (finaleProps == null) return;
            Transform hand = finaleProps.transform.Find("ChildHand");
            Transform toy = finaleProps.transform.Find("ChildToy");
            Transform mark = finaleProps.transform.Find("Blood_Finale");
            Transform light = finaleProps.transform.Find("FinaleRevealLight");
            Vector3 handPoint = hand != null ? hand.position : toyPoint;
            handPoint.y = GroundAt(handPoint);
            if (toy != null) toy.position = toyPoint + Vector3.up * .014f;
            if (hand != null)
            {
                Vector3 towardToy = Vector3.ProjectOnPlane(toyPoint - handPoint, Vector3.up).normalized;
                hand.SetPositionAndRotation(handPoint + Vector3.up * .022f, Quaternion.LookRotation(Vector3.up, towardToy));
                if (hand.GetComponentInChildren<Renderer>(true) == null && handPrefab != null)
                {
                    GameObject visual = Instantiate(handPrefab, hand);
                    visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    visual.transform.localScale *= 1.15f;
                    AttachSleeve(hand, new Vector3(-.01f, -.21f, .055f), new Vector3(.056f, .215f, .054f), "Child sleeve cuff");
                    foreach (Collider body in visual.GetComponentsInChildren<Collider>(true)) body.enabled = false;
                }
            }
            if (mark != null)
            {
                if (_bloodScale == Vector3.zero) _bloodScale = mark.localScale;
                mark.position = Vector3.Lerp(handPoint, toyPoint, .025f) + Vector3.up * .019f;
                mark.localScale = new Vector3(_bloodScale.x * .18f, _bloodScale.y, _bloodScale.z * .18f);
            }
            if (light != null)
                light.SetPositionAndRotation(Vector3.Lerp(handPoint, toyPoint, .52f) + Vector3.up * .43f, floor.rotation);
        }

        private void AttachSleeve(Transform wrist, Vector3 center, Vector3 size, string label)
        {
            if (childSleeveMaterial == null) return;
            GameObject sleeve = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sleeve.name = label;
            sleeve.transform.SetParent(wrist, false);
            sleeve.transform.localPosition = center;
            sleeve.transform.localScale = size;
            sleeve.GetComponent<Collider>().enabled = false;
            sleeve.GetComponent<Renderer>().sharedMaterial = childSleeveMaterial;
        }

        private IEnumerator SpreadBlood(float duration)
        {
            Transform mark = finaleProps != null ? finaleProps.transform.Find("Blood_Finale") : null;
            if (mark == null) { yield return new WaitForSecondsRealtime(duration); yield break; }
            Vector3 final = _bloodScale;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float size = Mathf.Lerp(.18f, .86f, Mathf.Sqrt(t / duration));
                mark.localScale = new Vector3(final.x * size, final.y, final.z * size);
                yield return null;
            }
            mark.localScale = new Vector3(final.x * .86f, final.y, final.z * .86f);
        }

        private IEnumerator PlayOffscreenLine(string speaker, string text, float gain)
        {
            float seconds = EstimateLineDuration(text, 40f, .60f);
            PlaySpeech(speaker, seconds, gain);
            yield return new WaitForSecondsRealtime(seconds + .16f);
        }


        private static void SetLightEnabled(string lightName, bool enabled)
        {
            var go = GameObject.Find(lightName);
            var light = go != null ? go.GetComponent<Light>() : null;
            if (light != null) light.enabled = enabled;
        }

        private GameObject ResolveActor(GameObject assigned, string fallbackName)
        {
            if (assigned != null)
                return assigned;

            return FindObjectByName(fallbackName);
        }

        private GameObject ResolveBandit() => ResolveActor(banditStanding, "Modern Rebel Soldier Character");

        private GameObject ResolveThugA() => ResolveActor(thugAStanding, "ThugA_Standing");

        /// <summary>Запоминает исходные позы стоящих персонажей перед катсценой.</summary>
        private void CaptureStandingPoses()
        {
            _fallActors = new[] { rustamStanding, ResolveThugA(), ResolveBandit() };
            _fallPositions = new Vector3[_fallActors.Length];
            _fallRotations = new Quaternion[_fallActors.Length];

            for (int i = 0; i < _fallActors.Length; i++)
            {
                if (_fallActors[i] == null)
                    continue;

                _fallPositions[i] = _fallActors[i].transform.position;
                _fallRotations[i] = _fallActors[i].transform.rotation;
            }
        }

        /// <summary>Возвращает персонажей в исходную позу (при повторном запуске катсцены).</summary>
        private void RestoreStandingPoses()
        {
            if (_fallActors == null || _fallPositions == null || _fallRotations == null)
                return;

            for (int i = 0; i < _fallActors.Length; i++)
            {
                if (_fallActors[i] == null)
                    continue;

                _fallActors[i].transform.SetPositionAndRotation(_fallPositions[i], _fallRotations[i]);
            }
        }

        /// <summary>
        /// После выстрела укладывает тех же персонажей на землю: берём точку и
        /// поворот у соответствующих *_Body, затем прижимаем модель к поверхности.
        /// </summary>
        private void LayDownAfterShot()
        {
            LayDown(rustamStanding, rustamBody);
            LayDown(ResolveThugA(), thugABody);
            LayDown(ResolveBandit(), thugBBody);

            SetAftermath(false, true);
            PlaceBloodOnGround();
        }

        private static void LayDown(GameObject actor, GameObject bodyMark)
        {
            if (actor == null)
                return;

            actor.SetActive(true);
            if (bodyMark == null)
                return;

            Transform tr = actor.transform;

            // Маркеры тел повёрнуты «лицом вниз» (X = 90). Для трупа нужен
            // поворот «лицом вверх» (X = -90), иначе руки в A-позе уходят
            // под землю, а торс висит в воздухе.
            float yaw = bodyMark.transform.eulerAngles.y;
            tr.SetPositionAndRotation(bodyMark.transform.position, Quaternion.Euler(-90f, yaw, 0f));

            if (!TryGetGround(tr.position, out Vector3 groundPoint, out _))
                return;

            // У разных FBX разные пивоты (у части моделей — ступни, у части —
            // бёдра). Выравниваем по кости бёдер: её центр должен оказаться
            // примерно на половине толщины тела над землёй.
            Transform hips = FindHips(actor);
            if (hips == null)
                return;

            tr.position += Vector3.up * (groundPoint.y + 0.17f - hips.position.y);
        }

        private static Transform FindHips(GameObject actor)
        {
            foreach (Transform part in actor.GetComponentsInChildren<Transform>(true))
                if (part.name == "mixamorig:Hips")
                    return part;

            return null;
        }

        /// <summary>
        /// Включает пятна крови и укладывает их на землю: позиция — верхняя поверхность
        /// под точкой, поворот — по нормали (с случайным поворотом вокруг нормали).
        /// </summary>
        private void PlaceBloodOnGround()
        {
            if (aftermathBlood == null)
                return;

            foreach (GameObject target in aftermathBlood)
            {
                if (target == null)
                    continue;

                Transform mark = target.transform;
                target.SetActive(true);

                if (!TryGetGround(mark.position, out Vector3 groundPoint, out Vector3 normal))
                    continue;

                // Пятно — плоская Plane-декаль с нормалью +Y. Поворачиваем
                // только вокруг нормали, чтобы не поставить её на ребро.
                mark.SetPositionAndRotation(
                    groundPoint + normal * 0.02f,
                    Quaternion.FromToRotation(Vector3.up, normal)
                    * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        /// <summary>
        /// Ищет поверхность пола под точкой (коллайдеры, затем террейн).
        /// Луч стартует чуть выше маркера и игнорирует геометрию над ним
        /// (например, коллайдер моста), иначе тела и кровь «улетают» наверх.
        /// </summary>
        private static bool TryGetGround(Vector3 worldPosition, out Vector3 point, out Vector3 normal)
        {
            point = worldPosition;
            normal = Vector3.up;
            bool found = false;
            float bestY = float.NegativeInfinity;

            // Маркеры тел/крови стоят на 2–50 см выше пола. Всё, что выше
            // этого допуска, — потолок/мост/декор над персонажем.
            float maxFloorY = worldPosition.y + 0.35f;

            RaycastHit[] hits = Physics.RaycastAll(
                worldPosition + Vector3.up * 2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.point.y > maxFloorY)
                    continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    point = hit.point;
                    normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : Vector3.up;
                    found = true;
                }
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
                if (y <= maxFloorY && (!found || y > bestY))
                {
                    point = new Vector3(worldPosition.x, y, worldPosition.z);
                    normal = Vector3.up;
                    found = true;
                }
            }

            return found;
        }

        private void SetStandingCast(bool active)
        {
            if (rustamStanding != null) rustamStanding.SetActive(active);
            if (thugAStanding != null) thugAStanding.SetActive(active);
            if (thugBStanding != null) thugBStanding.SetActive(active);

            // В сцене третий бандит лежит отдельным объектом с именем
            // "Modern Rebel Soldier Character"; ссылка может быть не задана.
            GameObject bandit = ResolveBandit();
            if (bandit != null) bandit.SetActive(active);

            if (pistolInThugHand != null) pistolInThugHand.SetActive(active);
        }

        private void SetIdleActors(bool active)
        {
            if (idleActors == null)
                return;

            foreach (BridgeIdleActor idle in idleActors)
            {
                if (idle == null)
                    continue;

                if (active)
                    idle.Play();
                else
                    idle.Stop();
            }
        }

        private IEnumerator PlayTalkLine(string speaker, string text, float speed, float hold, float gain = 1f, bool animateSpeaker = true)
        {
            float lineDuration = EstimateLineDuration(text, speed, hold);

            if (animateSpeaker && talk != null)
                talk.Speak(speaker, lineDuration);

            PlaySpeech(speaker, lineDuration, gain);

            yield return _ui.PlayLine(speaker, text, speed, hold);

            if (talk != null)
                talk.Silence();
        }

        private void PlaySpeech(string speaker, float duration, float gain)
        {
            if (_speechSource == null || _speechSource.clip == null)
                return;

            StopSpeech();
            _speechRoutine = StartCoroutine(SpeechRoutine(speaker, duration, Mathf.Clamp01(gain * speechVolume)));
        }

        private IEnumerator SpeechRoutine(string speaker, float duration, float targetVolume)
        {
            _speechSource.pitch = SpeechPitch(speaker);
            float length = _speechSource.clip.length;
            _speechSource.time = Random.Range(0f, Mathf.Max(0.01f, length - 0.05f));
            _speechSource.volume = 0f;
            _speechSource.Play();

            const float fadeIn = 0.15f;
            const float fadeOut = 0.25f;
            float holdEnd = Mathf.Max(fadeIn, duration - fadeOut);

            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                _speechSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeIn);
                yield return null;
            }

            _speechSource.volume = targetVolume;

            float elapsed = fadeIn;
            while (elapsed < holdEnd)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                _speechSource.volume = Mathf.Lerp(targetVolume, 0f, t / fadeOut);
                yield return null;
            }

            _speechSource.volume = 0f;
            _speechSource.Stop();
            _speechRoutine = null;
        }

        private void StopSpeech()
        {
            if (_speechRoutine != null)
            {
                StopCoroutine(_speechRoutine);
                _speechRoutine = null;
            }

            if (_speechSource != null)
            {
                _speechSource.Stop();
                _speechSource.volume = 0f;
            }
        }

        private static float SpeechPitch(string speaker)
        {
            switch (speaker)
            {
                case "\u0420\u0443\u0441\u0442\u0430\u043c": return 0.82f;
                case "\u0411\u0430\u043d\u0434\u0438\u0442": return 1.14f;
                case "\u0414\u0438\u043C\u0430 \u0438 \u0414\u0436\u043e": return 1.06f;
                default: return 1f;
            }
        }

        private static float EstimateLineDuration(string text, float speed, float hold) =>
            Mathf.Max(0.4f, text.Length / Mathf.Max(1f, speed)) + hold;

        private void SetAftermath(bool active, bool includeThugs)
        {
            if (rustamBody != null) rustamBody.SetActive(active);
            if (thugABody != null) thugABody.SetActive(active && includeThugs);
            if (thugBBody != null) thugBBody.SetActive(active && includeThugs);

            if (aftermathBlood != null)
            {
                foreach (GameObject target in aftermathBlood)
                    if (target != null) target.SetActive(active);
            }
        }

        private void SetHiddenState(bool hidden)
        {
            if (hideDuringCutscene == null)
                return;

            foreach (GameObject target in hideDuringCutscene)
                if (target != null) target.SetActive(!hidden);
        }

        protected override void RestorePlayerState()
        {
            SetLightEnabled("RimLight_Warm", true);
            SetHiddenState(false);

            if (_playerBody != null)
            {
                SetPlayerVisible(true);
                SetPlayerHeadVisible(true);
                _playerBody.enabled = true;
            }

            if (_brain != null) _brain.enabled = _brainWasEnabled;
            if (_shot != null)
            {
                _shot.fieldOfView = _originalLens.x;
                _shot.nearClipPlane = _originalLens.y;
            }

            if (_joeCompanion != null)
                _joeCompanion.enabled = true;

            base.RestorePlayerState();
        }

        private void OnDisable()
        {
            SetIdleActors(false);
            StopAftermathAudio();
            StopSpeech();

            if (_ui != null)
                _ui.DestroyOverlay();
            _ui = null;
        }

        private void EnsureAudio()
        {
            if (cutsceneAudio == null)
                cutsceneAudio = GetComponent<AudioSource>();

            if (cutsceneAudio == null)
                cutsceneAudio = gameObject.AddComponent<AudioSource>();

            cutsceneAudio.playOnAwake = false;
            cutsceneAudio.loop = false;
            cutsceneAudio.spatialBlend = 0f;
            cutsceneAudio.volume = 1f;

            if (_shootSourceA == null) _shootSourceA = CreateAudioSource("ShootA", 0.93f, false, null, 1f);
            if (_shootSourceB == null) _shootSourceB = CreateAudioSource("ShootB", 1f, false, null, 1f);
            if (_shootSourceC == null) _shootSourceC = CreateAudioSource("ShootC", 1.07f, false, null, 1f);
            if (_ringingSource == null) _ringingSource = CreateAudioSource("EarRinging", 1f, true, ringingClip, 0f);
            if (_heartbeatSource == null) _heartbeatSource = CreateAudioSource("Heartbeat", 1f, true, heartbeatClip, 0f);
            if (_speechSource == null) _speechSource = CreateAudioSource("DialogueSpeech", 1f, true, speechClip, 0f);
            if (_musicSource == null) _musicSource = CreateAudioSource("Bridge sad theme", 1f, false, sadTheme, 0f);
        }

        private AudioSource CreateAudioSource(string name, float pitch, bool loop, AudioClip clip, float volume)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);

            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.pitch = pitch;
            source.clip = clip;
            source.volume = volume;
            return source;
        }

        private void PlayShoot(int index)
        {
            AudioSource source;
            if (index % 3 == 0) source = _shootSourceA;
            else if (index % 3 == 1) source = _shootSourceB;
            else source = _shootSourceC;

            if (source == null || shootClip == null)
                return;

            source.PlayOneShot(shootClip, shootVolume * Random.Range(0.7f, 1f));
        }

        private static IEnumerator FadeLoop(AudioSource source, float target, float duration)
        {
            if (source == null)
                yield break;

            if (target > 0.001f && !source.isPlaying && source.clip != null)
                source.Play();

            float start = source.volume;

            if (duration <= 0f)
            {
                source.volume = target;
            }
            else
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    source.volume = Mathf.Lerp(start, target, t / duration);
                    yield return null;
                }

                source.volume = target;
            }

            if (target <= 0.001f)
                source.Stop();
        }

        private void StopAftermathAudio()
        {
            if (_musicSource != null) _musicSource.Stop();
            if (_campfire != null) Destroy(_campfire);
            if (_ringingSource != null)
            {
                _ringingSource.Stop();
                _ringingSource.volume = 0f;
            }

            if (_heartbeatSource != null)
            {
                _heartbeatSource.Stop();
                _heartbeatSource.volume = 0f;
            }
        }

        private static void StopLocomotion(Transform root)
        {
            var animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
            if (animator == null) return;
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("PlaybackSpeed", 1f);
        }

        private static void RestoreHeadScale(Transform player)
        {
            // FirstPersonBody скрывает и голову, и шею — для катсцены
            // возвращаем обе кости, иначе в крупном плане не будет головы.
            Transform[] all = player.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in all)
            {
                if (candidate.name == "mixamorig:Head" || candidate.name.EndsWith(":Head") ||
                    candidate.name == "mixamorig:Neck" || candidate.name.EndsWith(":Neck"))
                {
                    candidate.localScale = Vector3.one;
                }
            }
        }

        private static GameObject FindObjectByName(string targetName)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in all)
                    if (candidate.name == targetName)
                        return candidate.gameObject;
            }

            return null;
        }
    }
}
