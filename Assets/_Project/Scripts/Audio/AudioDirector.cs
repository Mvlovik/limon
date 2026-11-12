using System.Collections;
using UnityEngine;

namespace GameLimon.Audio
{
    /// <summary>
    /// Общий звук: музыка и амбиент, кроссфейды, громкости.
    /// Точечные звуки и амбиент-зоны живут в сценах, но используют общий микс.
    /// !!! НООО Assets/_Project/Audio/Mixers громкости через AudioMixer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float ambienceVolume = 1f;
        [SerializeField] private float defaultFadeDuration = 1.5f;

        private AudioSource _music;
        private AudioSource _ambience;
        private Coroutine _musicFade;
        private Coroutine _ambienceFade;

        private void Awake()
        {
            _music = gameObject.AddComponent<AudioSource>();
            _ambience = gameObject.AddComponent<AudioSource>();

            foreach (AudioSource source in new[] { _music, _ambience })
            {
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f; // 2D: музыка и общий амбиент
                source.volume = 0f;
            }
        }

        public void PlayMusic(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip == null)
                return;
            if (_music.clip == clip && _music.isPlaying)
                return;

            if (_musicFade != null)
                StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(FadeInRoutine(_music, clip, musicVolume, ResolveFade(fadeDuration)));
        }

        public void StopMusic(float fadeDuration = -1f)
        {
            if (_musicFade != null)
                StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(FadeOutRoutine(_music, ResolveFade(fadeDuration)));
        }

        public void PlayAmbience(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip == null)
                return;
            if (_ambience.clip == clip && _ambience.isPlaying)
                return;

            if (_ambienceFade != null)
                StopCoroutine(_ambienceFade);
            _ambienceFade = StartCoroutine(FadeInRoutine(_ambience, clip, ambienceVolume, ResolveFade(fadeDuration)));
        }

        public void StopAmbience(float fadeDuration = -1f)
        {
            if (_ambienceFade != null)
                StopCoroutine(_ambienceFade);
            _ambienceFade = StartCoroutine(FadeOutRoutine(_ambience, ResolveFade(fadeDuration)));
        }

        public void SetMusicVolume(float value01)
        {
            musicVolume = Mathf.Clamp01(value01);
            if (_musicFade == null)
                _music.volume = musicVolume;
        }

        public void SetAmbienceVolume(float value01)
        {
            ambienceVolume = Mathf.Clamp01(value01);
            if (_ambienceFade == null)
                _ambience.volume = ambienceVolume;
        }

        private float ResolveFade(float fadeDuration) => fadeDuration >= 0f ? fadeDuration : defaultFadeDuration;

        private IEnumerator FadeInRoutine(AudioSource source, AudioClip clip, float targetVolume, float fadeDuration)
        {
            // сначала тихо сводим прошлую тему, потом заводим новую
            yield return FadeOutRoutine(source, fadeDuration);

            source.clip = clip;
            source.volume = 0f;
            source.Play();

            if (fadeDuration <= 0f)
            {
                source.volume = targetVolume;
                yield break;
            }

            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.unscaledDeltaTime / fadeDuration;
                source.volume = Mathf.Lerp(0f, targetVolume, progress);
                yield return null;
            }

            source.volume = targetVolume;
        }

        private IEnumerator FadeOutRoutine(AudioSource source, float fadeDuration)
        {
            if (!source.isPlaying && source.volume <= 0f)
                yield break;

            float startVolume = source.volume;

            if (fadeDuration <= 0f)
            {
                source.volume = 0f;
                source.Stop();
                yield break;
            }

            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.unscaledDeltaTime / fadeDuration;
                source.volume = Mathf.Lerp(startVolume, 0f, progress);
                yield return null;
            }

            source.volume = 0f;
            source.Stop();
        }
    }
}
