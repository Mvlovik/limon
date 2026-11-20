using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// Global chromatic aberration settings. Lives on the persistent GameRoot so the
    /// effect is applied in every scene. The renderer feature reads the static values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChromaticAberrationSettings : MonoBehaviour
    {
        [Header("Хроматическая аберрация")]
        [Tooltip("Включает полноэкранную хроматическую аберрацию.")]
        [SerializeField] private bool enableChromaticAberration = false;

        [Tooltip("Сила смещения цветовых каналов. 0 = выключено, 1 = заметное, 2 = сильное.")]
        [SerializeField, Range(0f, 2f)] private float intensity = 0.5f;

        /// <summary>Whether the chromatic aberration pass should run this frame.</summary>
        public static bool Enabled { get; private set; }

        /// <summary>Strength of the color channel offset.</summary>
        public static float Intensity { get; private set; }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void OnDisable()
        {
            Enabled = false;
            Intensity = 0f;
        }

        private void Apply()
        {
            Intensity = Mathf.Clamp(intensity, 0f, 4f);
            Enabled = enableChromaticAberration && Intensity > 0f;
        }
    }
}
