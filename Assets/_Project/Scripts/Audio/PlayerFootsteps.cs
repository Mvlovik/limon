using System.Collections;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Audio
{
    /// <summary>
    /// Scene-level footsteps for the persistent player.
    /// The source clip contains several recorded steps. Instead of looping the
    /// whole clip (which sounds repetitive), every step starts at a randomly
    /// selected step offset with a small jitter. Only one voice is used, so
    /// steps never overlap. Stopping fades the current step out smoothly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip walkClip;
        [SerializeField] private AudioClip sprintClip;

        [Header("Cadence")]
        [SerializeField] private float walkInterval = 0.55f;
        [SerializeField] private float sprintInterval = 0.36f;
        [SerializeField] private float minSpeed = 0.15f;

        [Header("Randomization")]
        [Tooltip("Start times of individual footstep transients inside the clip.")]
        [SerializeField] private float[] stepOffsets = { 0.10f, 0.86f, 1.56f, 2.23f };
        [Tooltip("Random offset added to the selected step start, in seconds.")]
        [SerializeField] private float offsetJitter = 0.02f;
        [Tooltip("Random pitch variation per step.")]
        [SerializeField] private float pitchJitter = 0.015f;

        [Header("Pitch")]
        [SerializeField] private float walkPitch = 1f;
        [SerializeField] private float sprintPitch = 1.35f;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        private PlayerMotor _motor;
        private Coroutine _fade;
        private bool _playing;
        private float _stepTimer;

        private void Awake()
        {
            if (source == null)
                source = GetComponent<AudioSource>();

            if (source != null)
            {
                source.loop = false;
                source.playOnAwake = false;
                source.volume = 0f;
            }
        }

        private void OnDisable()
        {
            StopFade();
            if (source != null)
                source.Stop();
            _playing = false;
        }

        private void Update()
        {
            if (_motor == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    _motor = player.GetComponent<PlayerMotor>();
            }

            if (_motor == null || source == null)
                return;

            bool shouldPlay = !_motor.ControlLocked &&
                              _motor.IsGrounded &&
                              _motor.Speed01 >= minSpeed;

            if (!shouldPlay)
            {
                if (_playing)
                {
                    _playing = false;
                    FadeOutAndStop();
                }
                return;
            }

            bool sprinting = _motor.IsSprinting;

            if (!_playing)
            {
                _playing = true;
                _stepTimer = 0f;
                PlayStep(sprinting);
                return;
            }

            float interval = Mathf.Max(0.1f, sprinting ? sprintInterval : walkInterval);
            _stepTimer += Time.deltaTime;
            if (_stepTimer < interval)
                return;

            _stepTimer = 0f;
            PlayStep(sprinting);
        }

        private void PlayStep(bool sprinting)
        {
            AudioClip clip = sprinting && sprintClip != null ? sprintClip : walkClip;
            if (clip == null || source == null)
                return;

            StopFade();
            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.time = PickOffset(clip.length);
            source.pitch = (sprinting ? sprintPitch : walkPitch) +
                           Random.Range(-pitchJitter, pitchJitter);
            source.volume = volume;
            source.Play();
        }

        private float PickOffset(float clipLength)
        {
            float offset = 0f;

            if (stepOffsets != null && stepOffsets.Length > 0)
            {
                offset = stepOffsets[Random.Range(0, stepOffsets.Length)];
            }
            else
            {
                offset = Random.Range(0f, Mathf.Max(0f, clipLength - 0.1f));
            }

            offset += Random.Range(-offsetJitter, offsetJitter);
            return Mathf.Clamp(offset, 0f, Mathf.Max(0f, clipLength - 0.01f));
        }

        private void FadeOutAndStop()
        {
            if (source == null)
                return;

            StopFade();
            _fade = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            float startVolume = source.volume;
            float duration = Mathf.Max(0.01f, fadeOutDuration);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            source.volume = 0f;
            source.Stop();
            _fade = null;
        }

        private void StopFade()
        {
            if (_fade == null)
                return;

            StopCoroutine(_fade);
            _fade = null;
        }
    }
}
