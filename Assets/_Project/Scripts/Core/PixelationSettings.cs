using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// Global pixelation settings. Lives on the persistent GameRoot so the effect
    /// is applied in every scene. Renderer features read the static properties.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelationSettings : MonoBehaviour
    {
        [Header("Пикселизация")]
        [Tooltip("Включает полноэкранную пикселизацию.")]
        [SerializeField] private bool enablePixelation = false;

        [Tooltip("Размер пикселя в пикселях экрана. 1 = без эффекта, 4 = заметная пикселизация.")]
        [SerializeField, Range(1f, 32f)] private float pixelSize = 4f;

        private const float DefaultPixelSize = 4f;

        /// <summary>Whether the pixelation pass should run this frame.</summary>
        public static bool Enabled { get; private set; }

        /// <summary>Pixel block size in screen pixels.</summary>
        public static float PixelSize { get; private set; } = DefaultPixelSize;

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void OnDisable()
        {
            Enabled = false;
            PixelSize = DefaultPixelSize;
        }

        private void Apply()
        {
            Enabled = enablePixelation;
            PixelSize = Mathf.Clamp(pixelSize, 1f, 64f);
        }
    }
}
