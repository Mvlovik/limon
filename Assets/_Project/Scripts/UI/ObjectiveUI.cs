using System.Collections;
using GameLimon.Core;
using GameLimon.Story;
using UnityEngine;
using UnityEngine.UI;

namespace GameLimon.UI
{
    /// <summary>
    /// Панель указания в правом верхнем углу: PNG-метка + текст задания.
    /// Создаётся автоматически на канвасе UIRoot, как и панель мыслей.
    /// </summary>
    public sealed class ObjectiveUI : MonoBehaviour
    {
        private const string FontResource = "LegacyRuntime.ttf";
        private const float FadeDuration = 0.2f;

        public static ObjectiveUI Instance { get; private set; }

        private CanvasGroup _group;
        private Image _icon;
        private Text _label;
        private Coroutine _fade;

        public bool IsVisible => _group != null && _group.alpha > 0.001f;

        /// <summary>Найти или создать панель на Canvas из UIRoot.</summary>
        public static ObjectiveUI Ensure()
        {
            if (Instance != null)
                return Instance;

            UIRoot ui = GameRoot.Instance != null ? GameRoot.Instance.UI : null;
            Canvas canvas = ui != null ? ui.Canvas : null;

            if (canvas == null)
            {
                Debug.LogWarning("[ObjectiveUI] Не найден Canvas UIRoot.", null);
                return null;
            }

            ObjectiveUI existing = canvas.GetComponentInChildren<ObjectiveUI>(true);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            Instance = Create(canvas);
            return Instance;
        }

        /// <summary>Показать указание из ассета.</summary>
        public void Show(ObjectiveDefinition definition)
        {
            if (definition == null)
            {
                Hide();
                return;
            }

            Show(definition.Icon, definition.Text);
        }

        /// <summary>Показать указание с произвольной иконкой и текстом.</summary>
        public void Show(Sprite icon, string text)
        {
            if (_label == null)
                return;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            _label.text = text ?? string.Empty;

            if (!IsVisible)
                FadeTo(1f);
        }

        /// <summary>Скрыть указание.</summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            FadeTo(0f);
        }

        private void FadeTo(float target)
        {
            if (_group == null)
                return;

            if (_fade != null)
                StopCoroutine(_fade);

            _fade = StartCoroutine(FadeRoutine(target));
        }

        private IEnumerator FadeRoutine(float target)
        {
            float start = _group.alpha;
            float progress = 0f;

            while (progress < 1f)
            {
                progress += Time.unscaledDeltaTime / FadeDuration;
                _group.alpha = Mathf.Lerp(start, target, progress);
                yield return null;
            }

            _group.alpha = target;
            _fade = null;

            if (target <= 0f && _label != null)
                _label.text = string.Empty;
        }

        private static ObjectiveUI Create(Canvas canvas)
        {
            if (canvas == null)
                return null;

            var host = new GameObject("ObjectivePanel", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);
            host.transform.SetAsFirstSibling(); // под затемнением UIRoot, но над игрой

            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(560f, 132f);
            rect.anchoredPosition = new Vector2(-28f, -28f);

            CanvasGroup group = host.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image background = host.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
            background.raycastTarget = false;

            // Тонкая акцентная полоса слева.
            var accentObject = new GameObject("Accent", typeof(RectTransform));
            accentObject.transform.SetParent(host.transform, false);
            var accentRect = (RectTransform)accentObject.transform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(6f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
            Image accent = accentObject.AddComponent<Image>();
            accent.color = new Color(1f, 0.76f, 0.3f, 1f);
            accent.raycastTarget = false;

            // Иконка-метка.
            var iconObject = new GameObject("Marker", typeof(RectTransform));
            iconObject.transform.SetParent(host.transform, false);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(72f, 72f);
            iconRect.anchoredPosition = new Vector2(24f, 0f);

            Image icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // Заголовок.
            var titleObject = new GameObject("Title", typeof(RectTransform));
            titleObject.transform.SetParent(host.transform, false);
            var titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(116f, -38f);
            titleRect.offsetMax = new Vector2(-20f, -12f);

            Text title = titleObject.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>(FontResource);
            title.fontSize = 16;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(1f, 0.76f, 0.3f, 1f);
            title.text = "ЗАДАНИЕ";
            title.raycastTarget = false;

            // Основной текст указания.
            var labelObject = new GameObject("ObjectiveText", typeof(RectTransform));
            labelObject.transform.SetParent(host.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(116f, 14f);
            labelRect.offsetMax = new Vector2(-20f, -40f);

            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(FontResource);
            label.fontSize = 26;
            label.alignment = TextAnchor.UpperLeft;
            label.color = new Color(0.95f, 0.93f, 0.88f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            var shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            var panel = host.AddComponent<ObjectiveUI>();
            panel._group = group;
            panel._icon = icon;
            panel._label = label;
            panel._label.text = string.Empty;
            return panel;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
