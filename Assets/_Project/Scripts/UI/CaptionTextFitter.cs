using UnityEngine;
using UnityEngine.UI;

namespace GameLimon.UI
{
    /// <summary>
    /// Держит размер шрифта субтитров/мыслей пропорциональным фактической высоте
    /// канваса, чтобы текст был читаем и не «терялся» ни при каком разрешении
    /// или соотношении сторон окна.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class CaptionTextFitter : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float referenceHeight = 1080f;
        [SerializeField, Min(1)] private int referenceFontSize = 30;
        [SerializeField, Min(1)] private int minFontSize = 12;
        [SerializeField, Min(1)] private int maxFontSize = 72;

        private Text _text;
        private int _appliedSize;

        public void Setup(int fontSize, int minSize, int maxSize = 72, float refHeight = 1080f)
        {
            referenceFontSize = Mathf.Max(1, fontSize);
            minFontSize = Mathf.Max(1, minSize);
            maxFontSize = Mathf.Max(minFontSize, maxSize);
            referenceHeight = Mathf.Max(1f, refHeight);
            _appliedSize = 0;
            Apply();
        }

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_text == null) _text = GetComponent<Text>();
            if (_text == null) return;

            Canvas canvas = _text.canvas;
            float height = 0f;
            if (canvas != null)
            {
                var canvasRect = canvas.transform as RectTransform;
                if (canvasRect != null) height = canvasRect.rect.height;
            }
            if (height < 1f) height = referenceHeight;

            int size = Mathf.Clamp(
                Mathf.RoundToInt(referenceFontSize * height / referenceHeight),
                minFontSize, maxFontSize);
            if (size != _appliedSize)
            {
                _appliedSize = size;
                _text.fontSize = size;
            }
        }
    }
}
