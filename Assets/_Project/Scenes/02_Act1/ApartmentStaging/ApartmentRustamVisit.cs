using System.Collections;
using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Staging
{
    // Apartment-only staging. Existing wake, input, thoughts, quests and exit components remain owners.
    public sealed class ApartmentRustamVisit : InteractableBase
    {
        public enum VisitPhase { WaitingForWindow, FirstThought, Knocking, WaitingAtDoor, Conversation, Complete }

        [SerializeField] private Transform doorHinge;
        [SerializeField] private Transform doorway;
        [SerializeField] private GameObject rustam;
        [SerializeField] private AudioSource knocks;
        [SerializeField] private ThoughtSequence firstThought;
        [SerializeField] private ThoughtSequence conversation;
        [SerializeField] private FixedThirdPersonView fixedView;
        [SerializeField] private CoffeeExitBarrier coffeeBarrier;
        [SerializeField] private Transform returnPosition;
        [SerializeField] private float thoughtBeforeKnock = 1.8f;
        [SerializeField] private float stopKnockingDistance = 1.8f;
        [SerializeField] private float doorOpenDuration = 0.9f;

        private GameObject player;
        private PlayerMotor motor;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private Camera playerCamera;
        private Quaternion closedDoor;
        private Transform body, chest, head, leftArm, rightArm, leftElbow, rightElbow;
        private Quaternion chestRest, leftElbowRest, rightElbowRest;
        private SkinnedMeshRenderer face;
        private int mouthShape = -1;
        private bool rustamSpeaking;
        private float lineStartedAt, speakingWeight;
        private float mouthStartedAt, mouthDuration;
        private int conversationLine;
        private bool capturedControl;
        private bool oldMotorLock, oldPoseLock, oldInteractorEnabled;

        public VisitPhase Phase { get; private set; }
        public bool IsKnocking => knocks != null && knocks.isPlaying;
        public override bool CanInteract => base.CanInteract &&
            (Phase == VisitPhase.Knocking || Phase == VisitPhase.WaitingAtDoor);

        private void Awake()
        {
            closedDoor = doorHinge.localRotation;
            rustam.SetActive(false);
            knocks.playOnAwake = false;
            knocks.Stop();
            if (coffeeBarrier != null)
            {
                coffeeBarrier.enabled = false;
                coffeeBarrier.GetComponent<BoxCollider>().enabled = false;
            }
            body = rustam.transform.Find("Body");
            var visual = rustam.transform.Find("CharacterVisual");
            if (visual != null) face = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (face != null)
            {
                mouthShape = face.sharedMesh.GetBlendShapeIndex("DialogueMouth");
                chest = System.Array.Find(face.bones, bone => bone != null && bone.name == "mixamorig:Spine2");
                if (chest != null) chestRest = chest.localRotation;
            }
            if (body != null)
            {
                head = body.Find("HeadPivot");
                leftArm = body.Find("LeftArm");
                rightArm = body.Find("RightArm");
                leftElbow = body.Find("LeftArm/Elbow");
                rightElbow = body.Find("RightArm/Elbow");
                if (leftElbow != null) leftElbowRest = leftElbow.localRotation;
                if (rightElbow != null) rightElbowRest = rightElbow.localRotation;
            }
        }

        private IEnumerator Start()
        {
            ApartmentOpening opening = null;
            while (player == null || opening == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                opening = FindFirstObjectByType<ApartmentOpening>();
                yield return null;
            }
            motor = player.GetComponent<PlayerMotor>();
            look = player.GetComponent<PlayerLook>();
            interactor = player.GetComponent<PlayerInteractor>();
            playerCamera = player.GetComponentInChildren<Camera>(true);
            while (!opening.HasPlayed || opening.IsLookingAtWindow) yield return null;

            if (GameRoot.Instance.State.GetFlag("A1_S01_RustamVisited"))
            {
                Phase = VisitPhase.Complete;
                RestoreBarrier();
                fixedView.ActivateView();
                yield break;
            }
            Phase = VisitPhase.FirstThought;
            StoryDirector.Ensure().Play(firstThought);
            yield return new WaitForSeconds(thoughtBeforeKnock);
            StoryDirector.Ensure().Stop();
            Phase = VisitPhase.Knocking;
            knocks.Play();
        }

        private void Update()
        {
            if (Phase != VisitPhase.Knocking || player == null) return;
            Vector3 offset = player.transform.position - doorway.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= stopKnockingDistance * stopKnockingDistance)
            {
                knocks.Stop();
                Phase = VisitPhase.WaitingAtDoor;
            }
        }

        private void LateUpdate()
        {
            if (!rustam.activeInHierarchy || body == null) return;
            float breath = Mathf.Sin(Time.time * 1.85f);
            float elapsed = Time.time - lineStartedAt;
            speakingWeight = Mathf.MoveTowards(speakingWeight, rustamSpeaking ? 1f : 0f, Time.deltaTime * 4f);

            float accent = Pulse(elapsed, 0.22f, 0.85f) + 0.55f * Pulse(elapsed, 1.3f, 1.1f);
            float nod = speakingWeight * accent;
            float leftGesture = conversationLine == 4 ? nod : nod * 0.24f;
            float rightGesture = conversationLine == 4 ? nod * 0.24f : nod;
            float listeningNod = (1f - speakingWeight) * Pulse(elapsed, 0.65f, 0.75f);
            float weightShift = Mathf.Sin(Time.time * 0.95f);

            if (chest != null)
                chest.localRotation = chestRest * Quaternion.Euler(1.2f * breath, 0f, 1.7f * weightShift);
            if (head != null) head.localRotation = Quaternion.Euler(6f * nod + 4f * listeningNod,
                2.4f * Mathf.Sin(Time.time * 1.25f) - 3f * leftGesture, -1.5f * weightShift);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-12f * leftGesture, 2f * leftGesture, -3f - 4f * leftGesture);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-15f * rightGesture, -3f * rightGesture, 3f + 5f * rightGesture);
            if (leftElbow != null) leftElbow.localRotation = leftElbowRest * Quaternion.Euler(-18f * leftGesture, 0f, 0f);
            if (rightElbow != null) rightElbow.localRotation = rightElbowRest * Quaternion.Euler(-21f * rightGesture, 0f, 0f);
            UpdateMouth();
        }

        private void UpdateMouth()
        {
            if (mouthShape < 0) return;
            float elapsed = Time.unscaledTime - mouthStartedAt;
            float opening = 0f;
            if (rustamSpeaking && elapsed > 0f && elapsed < mouthDuration)
            {
                float attack = Mathf.SmoothStep(0f, 1f, elapsed / 0.12f);
                float release = Mathf.SmoothStep(0f, 1f, (mouthDuration - elapsed) / 0.18f);
                float syllable = Mathf.Sin(25f * elapsed + 0.55f * Mathf.Sin(6f * elapsed));
                float emphasis = 0.72f + 0.28f * Mathf.Sin(8f * elapsed);
                opening = 66f * attack * release * Mathf.Max(0f, syllable) * emphasis;
            }
            face.SetBlendShapeWeight(mouthShape, opening);
        }

        private static float Pulse(float elapsed, float start, float duration)
        {
            float progress = Mathf.Clamp01((elapsed - start) / duration);
            return Mathf.Sin(progress * Mathf.PI);
        }

        protected override void OnInteract(GameObject actor)
        {
            Phase = VisitPhase.Conversation;
            knocks.Stop();
            StartCoroutine(Visit());
        }

        private IEnumerator Visit()
        {
            CaptureControl();
            StoryDirector.Ensure().Stop();
            GameLimon.UI.ObjectiveUI.Instance?.Hide();
            lineStartedAt = Time.time;
            rustam.SetActive(true);
            Quaternion cameraFrom = playerCamera.transform.rotation;
            Vector3 face = rustam.transform.position + Vector3.up * 1.66f;
            Quaternion cameraTo = Quaternion.LookRotation(face - playerCamera.transform.position);
            for (float t = 0f; t < doorOpenDuration; t += Time.deltaTime)
            {
                float f = Mathf.SmoothStep(0f, 1f, t / doorOpenDuration);
                doorHinge.localRotation = closedDoor * Quaternion.Euler(0f, -95f * f, 0f);
                playerCamera.transform.rotation = Quaternion.Slerp(cameraFrom, cameraTo, f);
                yield return null;
            }
            doorHinge.localRotation = closedDoor * Quaternion.Euler(0f, -95f, 0f);
            playerCamera.transform.rotation = cameraTo;

            for (int i = 0; i < conversation.Count; i++)
            {
                conversationLine = i;
                lineStartedAt = Time.time;
                var line = conversation.GetLine(i);
                const string speaker = "Рустам:";
                rustamSpeaking = line != null && line.Text != null &&
                    line.Text.StartsWith(speaker, System.StringComparison.Ordinal);
                if (rustamSpeaking)
                {
                    mouthStartedAt = Time.unscaledTime + (line.Voice == null ? 0.18f : 0f);
                    mouthDuration = line.Voice != null ? line.Voice.length :
                        Mathf.Max(0.35f, (line.Text.Length - speaker.Length) / 23f);
                }
                var director = StoryDirector.Ensure();
                director.PlayLine(line);
                while (director.IsBusy) yield return null;
                rustamSpeaking = false;
            }
            yield return GameRoot.Instance.UI.FadeTo(1f, 0.55f);
            rustam.SetActive(false);
            doorHinge.localRotation = closedDoor;
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(returnPosition.position, returnPosition.rotation);
            if (controller != null) controller.enabled = wasEnabled;
            motor.ResetMomentum();
            playerCamera.transform.localRotation = Quaternion.identity;
            look.ResyncPose();
            GameRoot.Instance.State.SetFlag("A1_S01_RustamVisited", true);
            RestoreBarrier();
            RestoreControl();
            Phase = VisitPhase.Complete;
            fixedView.ActivateView();
            yield return new WaitForSeconds(0.2f);
            yield return GameRoot.Instance.UI.FadeTo(0f, 0.65f);
        }

        private void CaptureControl()
        {
            oldMotorLock = motor.ControlLocked;
            oldPoseLock = look.PoseLocked;
            oldInteractorEnabled = interactor.enabled;
            capturedControl = true;
            motor.ResetMomentum();
            motor.SetControlLocked(true);
            look.PoseLocked = true;
            // Keep focus updates running so the accepted door prompt clears during the conversation.
            // All quest interactions are unavailable until the visit flag is set.
        }

        private void RestoreControl()
        {
            if (!capturedControl) return;
            capturedControl = false;
            if (motor != null) motor.SetControlLocked(oldMotorLock);
            if (look != null) look.PoseLocked = oldPoseLock;
            if (interactor != null) interactor.enabled = oldInteractorEnabled;
        }

        private void RestoreBarrier()
        {
            if (coffeeBarrier != null) coffeeBarrier.enabled = true;
        }

        private void OnDestroy()
        {
            if (knocks != null) knocks.Stop();
            RestoreControl();
        }
    }
}
