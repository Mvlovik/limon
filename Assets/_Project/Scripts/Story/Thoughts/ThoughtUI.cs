using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameLimon.Story
{
    /// <summary> Панель мыслей/субтитров внизу экрана </summary>
    public sealed class ThoughtUI : MonoBehaviour
    {
        private const string FontResource = "LegacyRuntime.ttf";

        private CanvasGroup _group;
        private Text _label;

        public bool IsVisible => _group != null && _group.alpha > 0.001f;

        public static ThoughtUI Create(Canvas canvas)
        {
            if (canvas == null) return null;
            var host = new GameObject("ThoughtPanel", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1400f, 170f);
            rect.anchoredPosition = new Vector2(0f, 260f);

            CanvasGroup group = host.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var labelObject = new GameObject("ThoughtText", typeof(RectTransform));
            labelObject.transform.SetParent(host.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(FontResource);
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.9f, 0.86f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            var shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            var panel = host.AddComponent<ThoughtUI>();
            panel._group = group;
            panel._label = label;
            panel._label.text = string.Empty;
            return panel;
        }

        /// <summary>Чтоб пичатала па буквом</summary>
        public IEnumerator Play(ThoughtLine line, float charsPerSecond, float fadeDuration)
        {
            if (line == null || _label == null || string.IsNullOrEmpty(line.Text))
                yield break;

            string text = line.Text;
            float revealDuration = Mathf.Max(0.2f, text.Length / Mathf.Max(1f, charsPerSecond));
            float hold = line.ResolveDuration(charsPerSecond);

            _label.text = string.Empty;
            yield return FadeTo(1f, fadeDuration);

            float revealed = 0f;
            while (revealed < revealDuration) {
                revealed += Time.unscaledDeltaTime;
                int count = Mathf.Clamp(
                    Mathf.CeilToInt(text.Length * Mathf.Clamp01(revealed / revealDuration)),
                    0,
                    text.Length);
                _label.text = text.Substring(0, count);
                yield return null;
            }

            _label.text = text;

            float held = 0f;
            while (held < hold) {
                held += 2 * Mathf.Sin( Time.unscaledDeltaTime);
                yield return null;
            }

            yield return FadeTo(0f, fadeDuration);
            _label.text = string.Empty;
        }
        public void Hide()
        {
            StopAllCoroutines();
            if (_label != null)
                _label.text = string.Empty;
            if (_group != null)
                _group.alpha = 0f;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (_group == null)
                yield break;

            if (duration <= 0f)
            {
                _group.alpha = target;
                yield break;
            }

            float start = _group.alpha;
            float progress = 0f;

            while (progress < 1f)
            {
                progress += Time.unscaledDeltaTime / duration;
                _group.alpha = Mathf.Lerp(start, target, progress);
                yield return null;
            }

            _group.alpha = target;
        }
    }
}
