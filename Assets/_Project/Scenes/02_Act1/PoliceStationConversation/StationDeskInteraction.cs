using System.Collections;
using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using GameLimon.Story;
using GameLimon.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameLimon.Staging
{
    // Scene-local handoff from the existing player to the existing station clip.
    public sealed class StationDeskInteraction : InteractableBase
    {
        [SerializeField] private Animation conversation;
        [SerializeField] private GameObject firstPerson;
        [SerializeField] private Camera seatedCamera;
        [SerializeField] private GameObject joe;
        [SerializeField] private AudioSource conversationAudio;
        [SerializeField] private AudioSource handsetAudio;
        [SerializeField] private AudioSource stationAmbience;
        [SerializeField] private AudioSource speechAudio;
        [SerializeField] private AudioClip ritaSpeech;
        [SerializeField] private Transform spawn;
        [SerializeField] private GameObject editorPlayerPrefab;
        [SerializeField] private GameObject editorServicesPrefab;
        [SerializeField, Min(0f)] private float seatingFadeDuration = 0.32f;

        [Header("Cutscene")]
        [Tooltip("Общий сценарий катсцены. Если назначен, используется вместо встроенной логики.")]
        [SerializeField] private SceneCutscene cutscene;

        [Tooltip("После конца разговора затемнить экран и перейти к следующей сцене")]
        [SerializeField] private bool completeSceneWhenFinished = true;

        [Tooltip("Пауза перед затемнением после конца разговора, сек")]
        [SerializeField] private float endHoldDuration = 0.6f;

        private const float SeatReach = 1.35f;
        private const float FloorTolerance = 0.16f;
        private const float FloorContactTolerance = 0.06f;
        private const float RingStart = 21.65f;
        private const float HandsetPickup = 22.52f;
        private const float DialogueAmbienceGain = .38f;
        private const float PhoneAnswered = 23.65f;
        private const float PhoneReturn = 46.5f;
        private const float PhoneLoopStart = 30f;
        private const string FirstChoice = "А с Мишкой что?";
        private const string SecondChoice = "А с последним что?";

        private static readonly float[] SpeechOffsets = { .01f, .49f, .24f, .76f, .12f, .62f, .36f, .87f };

        private bool started;
        private MaleDialoguePlayback maleSpeech;
        private int ritaLine;
        private bool dialogueActive;
        private float normalAmbienceVolume;
        private ThoughtUI captions;
        private GameObject player;
        private PlayerMotor motor;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private CharacterController playerController;
        private FirstPersonBody hiddenPlayerBody;
        private GameObject interactionPrompt;
        private readonly List<Camera> playerCameras = new List<Camera>();
        private readonly List<AudioListener> playerListeners = new List<AudioListener>();

        public override bool CanInteract => isActiveAndEnabled && !started && base.CanInteract && IsAtChair();

        private bool IsAtChair()
        {
            if (playerController == null || !playerController.enabled)
                return false;

            Bounds playerBounds = playerController.bounds;
            Vector3 feet = playerBounds.center;
            feet.y = playerBounds.min.y;
            if (Mathf.Abs(feet.y - transform.position.y) > FloorTolerance)
                return false;

            Vector3 offset = feet - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > SeatReach * SeatReach)
                return false;

            // Use the support under the capsule; isGrounded can flicker between Move calls.
            Vector3 probe = feet + Vector3.up * FloorContactTolerance;
            return Physics.Raycast(probe, Vector3.down, out RaycastHit floor, 2f * FloorContactTolerance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && floor.normal.y >= 0.7f;
        }

        private void Awake()
        {
            maleSpeech = new MaleDialoguePlayback(speechAudio);
            if (conversation != null)
            {
                conversation.playAutomatically = false;
                conversation.Stop();
            }
            if (conversationAudio != null)
            {
                conversationAudio.playOnAwake = false;
                conversationAudio.Stop();
            }
            if (stationAmbience != null)
                normalAmbienceVolume = stationAmbience.volume;

            firstPerson.SetActive(false);
            joe.SetActive(false);

            // Opening this scene directly in the editor uses the same project prefabs.
            if (Application.isEditor && GameRoot.Instance == null && editorServicesPrefab != null)
            {
                var services = Instantiate(editorServicesPrefab);
                var autoStart = services.GetComponent<BootAutoStart>();
                if (autoStart != null) autoStart.enabled = false;
            }
            if (Application.isEditor && GameObject.FindGameObjectWithTag("Player") == null && editorPlayerPrefab != null)
                Instantiate(editorPlayerPrefab);
        }

        private IEnumerator Start()
        {
            yield return null; // Let the normal additive scene/spawn handoff finish.
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null && Application.isEditor && editorPlayerPrefab != null)
                player = Instantiate(editorPlayerPrefab);
            if (player == null) yield break;

            playerController = player.GetComponent<CharacterController>();
            if (spawn == null) yield break;

            if (playerController != null) playerController.enabled = false;
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (playerController != null) playerController.enabled = true;
            player.GetComponent<PlayerMotor>()?.ResetMomentum();
            player.GetComponent<PlayerMotor>()?.SetControlLocked(false);
            var playerLook = player.GetComponent<PlayerLook>();
            if (playerLook != null) playerLook.PoseLocked = false;
            ObjectiveUI.Instance?.Hide();
        }

        private void Update()
        {
            maleSpeech.UpdateVolume();
            if (stationAmbience == null)
                return;

            float target = dialogueActive ? normalAmbienceVolume * DialogueAmbienceGain : normalAmbienceVolume;
            stationAmbience.volume = Mathf.MoveTowards(stationAmbience.volume, target, Time.unscaledDeltaTime * .8f);
        }

        protected override void OnInteract(GameObject interactorObject)
        {
            if (cutscene != null)
            {
                started = true;
                player = interactorObject;
                cutscene.Play();
                return;
            }

            if (conversation == null || conversation.clip == null || seatedCamera == null) return;
            started = true;
            player = interactorObject;
            StartCoroutine(SitAndPlay());
        }

        private IEnumerator SitAndPlay()
        {
            motor = player.GetComponent<PlayerMotor>();
            look = player.GetComponent<PlayerLook>();
            interactor = player.GetComponent<PlayerInteractor>();
            motor?.ResetMomentum();
            motor?.SetControlLocked(true);
            if (look != null) look.PoseLocked = true;
            if (interactor != null) interactor.enabled = false;
            StoryDirector.Instance?.Stop();
            ObjectiveUI.Instance?.Hide();
            var ui = GameRoot.Instance != null ? GameRoot.Instance.UI : null;
            var canvas = ui != null ? ui.Canvas : null;
            var promptTransform = canvas != null ? canvas.transform.Find("InteractionPrompt") : null;
            if (promptTransform != null && promptTransform.gameObject.activeSelf)
            {
                interactionPrompt = promptTransform.gameObject;
                interactionPrompt.SetActive(false);
            }

            if (ui != null) yield return ui.FadeTo(1f, seatingFadeDuration);

            foreach (var camera in player.GetComponentsInChildren<Camera>())
            {
                if (!camera.enabled) continue;
                playerCameras.Add(camera);
                camera.enabled = false;
            }
            foreach (var listener in player.GetComponentsInChildren<AudioListener>())
            {
                if (!listener.enabled) continue;
                playerListeners.Add(listener);
                listener.enabled = false;
            }

            hiddenPlayerBody = player.GetComponentInChildren<FirstPersonBody>(true);
            hiddenPlayerBody?.HideForCutscene();
            firstPerson.SetActive(true);
            joe.SetActive(true);
            conversation.clip.SampleAnimation(conversation.gameObject, 0f);
            DisableBakedCaptions();
            captions = ThoughtUI.GetOrCreate(canvas);
            if (ui != null) yield return ui.FadeTo(0f, seatingFadeDuration);

            conversation.wrapMode = WrapMode.ClampForever;
            conversation.Play();
            var state = conversation[conversation.clip.name];
            if (state != null)
            {
                state.wrapMode = WrapMode.ClampForever;
                StartCoroutine(PlayPhoneSounds(state));
            }
            yield return PlayConversationCaptions(state);
            if (completeSceneWhenFinished)
                yield return FinishAndAdvance();
        }

        private IEnumerator PlayPhoneSounds(AnimationState state)
        {
            yield return WaitForClip(state, RingStart);
            if (conversationAudio != null)
                conversationAudio.Play();

            yield return WaitForClip(state, HandsetPickup);
            if (conversationAudio != null)
                conversationAudio.Stop();
            if (handsetAudio != null)
                handsetAudio.Play();
        }

        private void DisableBakedCaptions()
        {
            var overlay = conversation.transform.Find("UI");
            if (overlay == null) return;
            foreach (Transform item in overlay)
            {
                if (item.name == "SubtitlePlate" || item.name == "RingCaption" ||
                    item.name.StartsWith("Line", System.StringComparison.Ordinal))
                    item.gameObject.SetActive(false);
            }
        }

        private IEnumerator PlayConversationCaptions(AnimationState state)
        {
            if (state == null) yield break;
            if (captions == null)
            {
                yield return WaitForClip(state, state.length);
                yield break;
            }

            yield return WaitForClip(state, 3.22f);
            dialogueActive = true;
            yield return SpeakUntil(state, "Джо: Привет, Дима. Ты как?", 5.16f);
            yield return WaitForClip(state, 5.25f);
            yield return SpeakUntil(state, "Дима: Привет Джо, все хорошо, кстати я пришел первым, с тебя пончики.", 10.43f);
            yield return WaitForClip(state, 10.75f);
            yield return SpeakUntil(state, "Джо: Ахах, думал забудешь, ну ничего, они уже в машине", 14.43f);
            yield return WaitForClip(state, 14.75f);
            yield return SpeakUntil(state, "Дима: Раз ты пришел, давай сразу за дело, поехали в патруль, сегодня у нас Меридиан-", RingStart);
            yield return WaitForClip(state, RingStart);
            captions.ShowImmediate("[Звонит телефон]");
            yield return WaitForClip(state, PhoneAnswered);
            captions.Hide();

            // Keep the existing handset hold animated while the player chooses and reads.
            var hold = StartCoroutine(HoldHandset(state));
            yield return Speak("Рита: Дима, это ты, только трубку не бросай, ладно?");
            yield return new WaitForSecondsRealtime(.55f);
            yield return Speak("Дима: Рита.");
            yield return Speak("Рита: Они его забрали. Рустама. Прямо сейчас. Он даже куртку не взял.");
            yield return Speak("Дима: Кто забрал. Рита, кто?!");
            yield return Speak("Рита: Наши. Четверо… нет, трое. Молодые совсем. Один сказал: \"Дядя Рустам, пошли, поговорить надо\". Как будто они его знают, двое с ним пошло.");

            int choice = 0;
            captions.ShowImmediate($"[1] {FirstChoice}\n[2] {SecondChoice}");
            while (choice == 0)
            {
                var keyboard = Keyboard.current;
                var gamepad = Gamepad.current;
                if ((keyboard != null && (keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed)) ||
                    (gamepad != null && gamepad.dpad.up.isPressed))
                    choice = 1;
                else if ((keyboard != null && (keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed)) ||
                         (gamepad != null && gamepad.dpad.down.isPressed))
                    choice = 2;
                else
                    yield return null;
            }

            captions.Hide();
            GameRoot.Instance?.State.SetChoice("A1_S03_RitaQuestion", choice == 1 ? "Mishka" : "LastMan");
            yield return Speak("Дима: " + (choice == 1 ? FirstChoice : SecondChoice));
            if (choice == 1)
            {
                yield return Speak("Рита: …Мишка? Мишка, стой! Я кому сказала — в комнату!..");
                yield return Speak("Рита: В комнате он, сам знаешь, любопытный, глаз да глаз");
            }
            else
            {
                yield return Speak("Рита: Остался. Внизу на лавке сидит, курит. Сказал, что подождёт. Кого он ждёт, Дима?");
            }

            yield return Speak("Рита: Они не били его, Дима. Они смеялись. И он пошёл. Сам пошёл, понимаешь?");
            yield return Speak("Дима: Я понял кто это и знаю где собираются... Запрись... На цепочку... Не открывай — даже если это я. Едем");
            StopCoroutine(hold);
            dialogueActive = false;
            state.time = PhoneReturn;
            yield return WaitForClip(state, state.length);
        }

        private IEnumerator SpeakUntil(AnimationState state, string text, float end)
        {
            float start = state.time;
            PlaySpeech(text, (end - start) / Mathf.Max(.01f, state.speed * Time.timeScale));
            int shown = 0;
            while (state.time < end - .015f)
            {
                var length = Mathf.Min(text.Length, Mathf.FloorToInt((state.time - start) * 40f));
                if (length > shown)
                {
                    shown = length;
                    captions.ShowImmediate(text.Substring(0, shown));
                }
                yield return null;
            }
            captions.Hide();
        }

        private IEnumerator Speak(string text, float hold = 2.5f)
        {
            float voiceDuration = PlaySpeech(text, float.PositiveInfinity, text.Length / 40f + hold * .5f);
            float reveal = Mathf.Max(.2f, text.Length / 40f);
            hold = Mathf.Max(hold, 2f * (voiceDuration - reveal - .13f));
            yield return captions.PlayText(text, 40f, hold, .13f);
            while (maleSpeech.IsPlaying)
                yield return null;
        }

        private float PlaySpeech(string text, float available, float ritaLimit = float.PositiveInfinity)
        {
            if (speechAudio == null || available <= 0f)
                return 0f;

            bool isRita = text.StartsWith("Рита:", System.StringComparison.Ordinal) && ritaSpeech != null;
            if (!isRita)
                return maleSpeech.Play(text, available);

            maleSpeech.ReleaseVolume();

            float offset = SpeechOffsets[ritaLine++ % SpeechOffsets.Length];
            int prefix = text.IndexOf(':') + 1;
            float duration = Mathf.Min(Mathf.Min(available, ritaLimit), Mathf.Max(.38f, (text.Length - prefix) / 27f));
            double start = AudioSettings.dspTime + .025;
            speechAudio.Stop();
            speechAudio.clip = ritaSpeech;
            speechAudio.loop = true;
            speechAudio.timeSamples = Mathf.FloorToInt(ritaSpeech.samples * offset);
            speechAudio.PlayScheduled(start);
            speechAudio.SetScheduledEndTime(start + duration);
            return 0f;
        }

        private static IEnumerator WaitForClip(AnimationState state, float time)
        {
            // A clamped legacy clip can land just below length on its last frame.
            while (state.time < time - .015f)
                yield return null;
        }

        private static IEnumerator HoldHandset(AnimationState state)
        {
            while (true)
            {
                if (state.time >= PhoneReturn - .12f)
                    state.time = PhoneLoopStart;
                yield return null;
            }
        }

        private IEnumerator FinishAndAdvance()
        {
            if (endHoldDuration > 0f)
                yield return new WaitForSeconds(endHoldDuration);

            GameRoot root = GameRoot.Instance;
            if (root != null && root.UI != null)
                yield return root.UI.FadeTo(1f, 1f);

            if (root != null && root.Flow != null)
                root.Flow.CompleteCurrentScene();
        }

        private void OnDisable()
        {
            if (!started) return;
            StopAllCoroutines();
            if (hiddenPlayerBody != null)
                hiddenPlayerBody.RestoreAfterCutscene();
            hiddenPlayerBody = null;
            if (firstPerson != null) firstPerson.SetActive(false);
            if (joe != null) joe.SetActive(false);
            if (captions != null) captions.Hide();
            if (motor != null) motor.SetControlLocked(false);
            if (look != null) look.PoseLocked = false;
            if (interactor != null) interactor.enabled = true;
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
            foreach (var camera in playerCameras) if (camera != null) camera.enabled = true;
            foreach (var listener in playerListeners) if (listener != null) listener.enabled = true;
        }

#if UNITY_EDITOR
        [ContextMenu("Preview seated conversation (Play Mode)")]
        private void PreviewConversation()
        {
            if (!Application.isPlaying || started || !base.CanInteract) return;
            var actor = GameObject.FindGameObjectWithTag("Player");
            if (actor != null) OnInteract(actor);
        }
#endif
    }
}


