using System.Collections;
using GameLimon.Companions;
using GameLimon.Core;
using GameLimon.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

namespace GameLimon.Story
{
    /// <summary>
    /// Scene 4 cinematic: trigger -> dialogue -> Wait/Act choice -> branch -> shared finale.
    /// The scene composition lives in A1_S04_UnderBridge; this component only drives
    /// the flow and swaps staging groups. Timeline/Cinemachine handle all camera work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RustamBridgeCutscene : SceneCutscene
    {
        private const string PlayerTag = "Player";

        [Header("Timelines")]
        [SerializeField] private PlayableDirector introDirector;
        [SerializeField] private PlayableDirector branchADirector;
        [SerializeField] private PlayableDirector aftermathDirector;
        [SerializeField] private PlayableDirector finaleDirector;

        [Header("Staging marks")]
        [SerializeField] private Transform playerIntroMark;
        [SerializeField] private Transform playerFinalMark;
        [SerializeField] private Transform joeIntroMark;

        [Header("Standing cast")]
        [SerializeField] private GameObject rustamStanding;
        [SerializeField] private GameObject thugAStanding;
        [SerializeField] private GameObject thugBStanding;
        [SerializeField] private GameObject pistolInThugHand;

        [Header("Aftermath bodies")]
        [SerializeField] private GameObject rustamBody;
        [SerializeField] private GameObject thugABody;
        [SerializeField] private GameObject thugBBody;
        [SerializeField] private GameObject[] aftermathBlood;

        [Header("Finale")]
        [SerializeField] private GameObject finaleProps;

        [Header("Objects hidden for the duration")]
        [SerializeField] private GameObject[] hideDuringCutscene;

        [Header("Audio")]
        [SerializeField] private AudioSource cutsceneAudio;
        [SerializeField, Range(0f, 1f)] private float gunshotVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float ringingVolume = 0.35f;

        [Header("Pacing")]
        [SerializeField] private float choiceTimeout = 5f;
        [SerializeField] private int qtePressesRequired = 9;
        [SerializeField] private float qteDuration = 4f;

        [Header("Debug")]
        [SerializeField] private bool debugForceAct;

        private CutsceneOverlayUI _ui;
        private GameObject _player;
        private FirstPersonBody _playerBody;
        private GameObject _joe;
        private CompanionActor _joeCompanion;
        private AudioClip _gunshot;
        private AudioClip _ringing;
        private AudioClip _running;

        private void Awake()
        {
            EnsureAudio();
        }

        protected override void PrepareCutscene()
        {
            base.PrepareCutscene();

            _ui = CutsceneOverlayUI.Create();
            _ui.HideAll();

            _player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (_player != null)
            {
                _playerBody = _player.GetComponentInChildren<FirstPersonBody>(true);
                if (_playerBody != null)
                    _playerBody.enabled = false;

                // FirstPersonBody normally hides the head for the FPS view. The
                // cutscene needs the real head for the final close-up.
                RestoreHeadScale(_player.transform);
                StopLocomotion(_player.transform);

                if (playerIntroMark != null)
                    _player.transform.SetPositionAndRotation(playerIntroMark.position, playerIntroMark.rotation);
            }

            _joe = FindObjectByName("Joe");
            if (_joe != null)
            {
                _joeCompanion = _joe.GetComponent<CompanionActor>();
                if (_joeCompanion != null)
                    _joeCompanion.enabled = false;

                StopLocomotion(_joe.transform);

                if (joeIntroMark != null)
                    _joe.transform.SetPositionAndRotation(joeIntroMark.position, joeIntroMark.rotation);
            }

            SetStandingCast(true);
            SetAftermath(false, true);
            SetHiddenState(true);

            if (finaleProps != null)
                finaleProps.SetActive(false);

            Debug.Log("[BridgeCutscene] prepared");
        }

        protected override IEnumerator PlayContent()
        {
            StartCoroutine(EnsureLetterbox());
            yield return new WaitForSecondsRealtime(0.2f);

            yield return PlayIntro();

            CutsceneChoice choice = debugForceAct ? CutsceneChoice.Act : CutsceneChoice.Wait;
            yield return _ui.ShowChoice("Что делает ГГ?", choiceTimeout, value =>
            {
                if (!debugForceAct)
                    choice = value;
            });
            Debug.Log($"[BridgeCutscene] choice={choice}");

            if (choice == CutsceneChoice.Wait)
                yield return BranchWait();
            else
                yield return BranchAct();

            yield return PlayFinale();

            _ui.HideAll();
            _ui.DestroyOverlay();
            _ui = null;
            Debug.Log("[BridgeCutscene] content finished");
        }

        private IEnumerator EnsureLetterbox()
        {
            if (_ui == null) yield break;
            yield return _ui.SetLetterbox(true, 0.5f);
        }

        private IEnumerator PlayIntro()
        {
            FocusCamera("CM_Shot_Approach");
            PlayTimeline(introDirector);

            yield return _ui.PlayLine("Джо", "Тихо. Смотри туда.", 40f, 1.2f);
            yield return _ui.PlayLine("Рустам", "Я вам все рассказал еще тогда! Оставьте меня в покое!", 45f, 0.9f);
            yield return _ui.PlayLine("Бандит", "Не все, старик. Ты знаешь, кого мы ищем. Где он?", 45f, 0.9f);

            yield return WaitForTimeline(introDirector);
            FocusCamera("CM_Shot_Rustam");
            yield return _ui.PlayLine("Джо", "Ждем. Узнаем, что им нужно.", 40f, 1.3f);
            Debug.Log("[BridgeCutscene] intro finished");
        }

        private IEnumerator BranchWait()
        {
            FocusCamera("CM_Shot_Wait");
            PlayTimeline(branchADirector);
            yield return _ui.PlayLine("Джо", "Спокойно. Пусть выговорятся.", 40f, 0.9f);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return _ui.PlayLine("Бандит", "У меня нет времени. Говори, или я те колено прострелю!", 48f, 0.7f);
            yield return _ui.PlayLine("Рустам", "Не-ет! Подожди! Я не знаю...", 42f, 0.5f);
            yield return _ui.PlayLine("Джо", "Сейчас!", 40f, 0.3f);
            yield return _ui.PlayLine("ГГ и Джо", "ПОЛИЦИЯ! БРОСАЙТЕ ОРУЖИЕ!", 65f, 0.3f);

            yield return GunfireAndFade(keepOnlyRustam: false);
            yield return PlayAftermath();

            yield return _ui.PlayLine("Рустам", "Слушай... тумбочка... у кровати... там подарок от твоего отца... посмотри...", 42f, 1.4f);
            yield return _ui.PlayLine("Джо", "ГГ! Сюда! Быстрее!", 40f, 1.0f);
        }

        private IEnumerator BranchAct()
        {
            FocusCamera("CM_Shot_Rustam");
            yield return _ui.PlayLine("Джо", "Стой, придурок! У них ствол!", 42f, 0.7f);

            bool brokeFree = false;
            yield return _ui.RunQte("Вырваться из хвата Джо", qteDuration, qtePressesRequired, result => brokeFree = result);

            if (!brokeFree)
                yield return _ui.PlayLine("Джо", "Чёрт, я тебя не удержу...", 40f, 0.3f);

            yield return _ui.PlayLine("ГГ", "ПОЛИЦИЯ! ОТПУСТИ ЕГО!", 60f, 0.3f);

            yield return GunfireAndFade(keepOnlyRustam: true);
            yield return PlayAftermath();

            yield return _ui.PlayLine("Рустам", "Слушай... тумбочка... у кровати... там подарок от твоего отца... посмотри...", 42f, 1.4f);
            yield return _ui.PlayLine("Джо", "Твою мать! Они ушли! Ты их спугнул!", 44f, 0.7f);
            yield return _ui.PlayLine("Джо", "ГГ... ГГ, иди сюда... Быстрее", 40f, 1.0f);
        }

        private IEnumerator GunfireAndFade(bool keepOnlyRustam)
        {
            Debug.Log("[BridgeCutscene] gunfire");
            StopDirector(introDirector);
            StopDirector(branchADirector);
            PlayGunshot();
            yield return _ui.Flash(0.35f);
            yield return new WaitForSecondsRealtime(0.35f);

            GameRoot root = GameRoot.Instance;
            if (root != null && root.UI != null)
                yield return root.UI.FadeTo(1f, 0.55f);

            PlayRinging();
            PlayRunning();

            yield return new WaitForSecondsRealtime(2.0f);

            SetStandingCast(false);
            SetAftermath(true, !keepOnlyRustam);

            if (pistolInThugHand != null)
                pistolInThugHand.SetActive(false);

            if (root != null && root.UI != null)
                yield return root.UI.FadeTo(0f, 1.0f);

            yield return new WaitForSecondsRealtime(0.4f);
        }

        private IEnumerator PlayAftermath()
        {
            if (aftermathDirector != null)
            {
                FocusCamera("CM_Shot_Aftermath");
                PlayTimeline(aftermathDirector);
                yield return new WaitForSecondsRealtime(1.0f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.0f);
            }
        }

        private IEnumerator PlayFinale()
        {
            if (finaleProps != null)
                finaleProps.SetActive(true);

            if (_player != null && playerFinalMark != null)
                _player.transform.SetPositionAndRotation(playerFinalMark.position, playerFinalMark.rotation);

            if (_joe != null && playerFinalMark != null)
            {
                Vector3 behind = playerFinalMark.position - playerFinalMark.forward * 2.4f + playerFinalMark.right * 1.8f;
                _joe.transform.SetPositionAndRotation(behind, Quaternion.LookRotation(-playerFinalMark.forward, Vector3.up));
            }

            Debug.Log("[BridgeCutscene] finale");
            SetLightEnabled("RimLight_Warm", false);
            FocusCamera("CM_Shot_Finale");
            PlayTimeline(finaleDirector);
            PlayRinging();
            yield return WaitForTimeline(finaleDirector, 9.0f);
            yield return _ui.SetLetterbox(false, 0.6f);
        }

        private void PlayTimeline(PlayableDirector director)
        {
            if (director == null)
                return;

            StopDirector(introDirector);
            StopDirector(branchADirector);
            StopDirector(aftermathDirector);
            StopDirector(finaleDirector);

            director.time = 0;
            director.Play();
        }

        private static void StopDirector(PlayableDirector director)
        {
            if (director != null && director.state == PlayState.Playing)
                director.Stop();
        }

        /// <summary>
        /// Keep the last shot alive after a Timeline stops. Without this the
        /// brain falls back to the highest-priority camera, which used to be
        /// the opening shot.
        /// </summary>
        private static void FocusCamera(string cameraName)
        {
            CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach (CinemachineCamera camera in cameras)
                if (camera != null)
                    camera.Priority = camera.name == cameraName ? 100 : 0;
        }

        private static void SetLightEnabled(string lightName, bool enabled)
        {
            var go = GameObject.Find(lightName);
            var light = go != null ? go.GetComponent<Light>() : null;
            if (light != null) light.enabled = enabled;
        }

        private IEnumerator WaitForTimeline(PlayableDirector director, float fallbackHold = 0f)
        {
            if (director == null)
            {
                if (fallbackHold > 0f)
                    yield return new WaitForSecondsRealtime(fallbackHold);
                yield break;
            }

            while (director.state == PlayState.Playing)
                yield return null;

            yield return new WaitForSecondsRealtime(0.15f);
        }

        private void SetStandingCast(bool active)
        {
            if (rustamStanding != null) rustamStanding.SetActive(active);
            if (thugAStanding != null) thugAStanding.SetActive(active);
            if (thugBStanding != null) thugBStanding.SetActive(active);
            if (pistolInThugHand != null) pistolInThugHand.SetActive(active);
        }

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
                _playerBody.enabled = true;

            if (_joeCompanion != null)
                _joeCompanion.enabled = true;

            base.RestorePlayerState();
        }

        private void OnDisable()
        {
            if (_ui != null)
                _ui.HideAll();
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

            if (_gunshot == null) _gunshot = BuildGunshot();
            if (_ringing == null) _ringing = BuildRinging();
            if (_running == null) _running = BuildRunning();
        }

        private void PlayGunshot()
        {
            if (cutsceneAudio == null || _gunshot == null) return;
            cutsceneAudio.PlayOneShot(_gunshot, gunshotVolume);
        }

        private void PlayRinging()
        {
            if (cutsceneAudio == null || _ringing == null) return;
            cutsceneAudio.PlayOneShot(_ringing, ringingVolume);
        }

        private void PlayRunning()
        {
            if (cutsceneAudio == null || _running == null) return;
            cutsceneAudio.PlayOneShot(_running, 0.5f);
        }

        private static AudioClip BuildGunshot()
        {
            const int rate = 44100;
            const float duration = 0.38f;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];
            var rng = new System.Random(1701);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float decay = Mathf.Exp(-t * 13f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float crack = Mathf.Sin(2f * Mathf.PI * (90f - 40f * t) * t);
                samples[i] = Mathf.Clamp((noise * 0.9f + crack * 0.6f) * decay, -1f, 1f);
            }

            var clip = AudioClip.Create("SFX_CutsceneGunshot_Placeholder", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildRinging()
        {
            const int rate = 44100;
            const float duration = 3.8f;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float envelope = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((duration - t) / 1.2f);
                float tone = Mathf.Sin(2f * Mathf.PI * 3900f * t) * 0.5f
                             + Mathf.Sin(2f * Mathf.PI * 4400f * t) * 0.25f;
                samples[i] = tone * envelope * 0.35f;
            }

            var clip = AudioClip.Create("SFX_CutsceneRinging_Placeholder", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildRunning()
        {
            const int rate = 44100;
            const float duration = 1.6f;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];
            var rng = new System.Random(9021);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float step = Mathf.Repeat(t * 4.5f, 1f);
                float envelope = Mathf.Exp(-step * 7f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                samples[i] = noise * envelope * 0.4f;
            }

            var clip = AudioClip.Create("SFX_CutsceneRunning_Placeholder", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
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
            Transform[] all = player.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in all)
            {
                if (candidate.name == "mixamorig:Head" || candidate.name.EndsWith(":Head"))
                {
                    candidate.localScale = Vector3.one;
                    return;
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
