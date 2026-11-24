using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameLimon.Story
{
    public enum CutsceneChoice
    {
        Wait = 0,
        Act = 1
    }

    /// <summary>
    /// Self-contained cinematic overlay for scripted scenes: letterbox, subtitles,
    /// and the two-option choice with a timeout.
    /// The overlay is built in code so a cutscene only needs one component reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutsceneOverlayUI : MonoBehaviour
    {
        private const string FontResource = "LegacyRuntime.ttf";
        private const int CanvasSortingOrder = 50;

        private static CutsceneOverlayUI _instance;

        private Canvas _canvas;
        private RectTransform _letterboxTop;
        private RectTransform _letterboxBottom;
        private CanvasGroup _subtitleGroup;
        private Text _subtitleText;
        private CanvasGroup _choiceGroup;
        private Text _choiceTimer;
        private Image _flash;

        private int _choiceIndex = -1;

        public static CutsceneOverlayUI Create()
        {
            if (_instance != null)
            {
                _instance.gameObject.SetActive(true);
                return _instance;
            }

            var host = new GameObject("[CutsceneOverlay]");
            DontDestroyOnLoad(host);
            CutsceneOverlayUI overlay = host.AddComponent<CutsceneOverlayUI>();
            overlay.Build();
            _instance = overlay;
            return overlay;
        }

        private void Awake()
        {
            if (_instance == null)
                _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Build()
        {

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildLetterbox(canvasObject.transform);
            BuildSubtitle(canvasObject.transform);
            BuildChoice(canvasObject.transform);
            BuildFlash(canvasObject.transform);
        }

        private void BuildLetterbox(Transform parent)
        {
            _letterboxTop = CreatePanel(parent, "LetterboxTop", new Color(0f, 0f, 0f, 1f));
            _letterboxTop.anchorMin = new Vector2(0f, 1f);
            _letterboxTop.anchorMax = new Vector2(1f, 1f);
            _letterboxTop.pivot = new Vector2(0.5f, 1f);
            _letterboxTop.sizeDelta = new Vector2(0f, 0f);
            _letterboxTop.anchoredPosition = Vector2.zero;

            _letterboxBottom = CreatePanel(parent, "LetterboxBottom", new Color(0f, 0f, 0f, 1f));
            _letterboxBottom.anchorMin = new Vector2(0f, 0f);
            _letterboxBottom.anchorMax = new Vector2(1f, 0f);
            _letterboxBottom.pivot = new Vector2(0.5f, 0f);
            _letterboxBottom.sizeDelta = new Vector2(0f, 0f);
            _letterboxBottom.anchoredPosition = Vector2.zero;

            SetBarHeight(_letterboxTop, 0f);
            SetBarHeight(_letterboxBottom, 0f);
        }

        private void BuildSubtitle(Transform parent)
        {
            _subtitleGroup = CreateCaptionGroup(parent, "Subtitle");
            _subtitleText = CreateCaption(_subtitleGroup.transform, "Line");
            var rect = _subtitleText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BuildChoice(Transform parent)
        {
            _choiceGroup = CreateCaptionGroup(parent, "Choice");
            CreateChoiceLabel(_choiceGroup.transform, "WaitOption", "[1] Ждать", .5f);
            CreateChoiceLabel(_choiceGroup.transform, "ActOption", "[2] Действовать", 0f);

            _choiceTimer = CreateCaption(_choiceGroup.transform, "Timer");
            _choiceTimer.color = new Color(.68f, .68f, .68f, 1f);
            var rect = _choiceTimer.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 36f);
            rect.anchoredPosition = new Vector2(0f, 4f);
        }

        private static CanvasGroup CreateCaptionGroup(Transform parent, string name)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            host.transform.SetParent(parent, false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(.135f, 0f);
            rect.anchorMax = new Vector2(.865f, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 108f);
            rect.anchoredPosition = new Vector2(0f, 11f);

            var group = host.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private void BuildFlash(Transform parent)
        {
            RectTransform flashRect = CreatePanel(parent, "Flash", new Color(1f, 1f, 1f, 0f));
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            _flash = flashRect.GetComponent<Image>();
            _flash.raycastTarget = false;
        }

        private static RectTransform CreatePanel(Transform parent, string objectName, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text CreateText(Transform parent, string objectName, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(FontResource);
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static Text CreateCaption(Transform parent, string name)
        {
            Text label = CreateText(parent, name, 30, TextAnchor.MiddleCenter, Color.white);
            ThoughtUI.StyleCaption(label);
            return label;
        }

        private static void CreateChoiceLabel(Transform parent, string name, string title, float row)
        {
            Text label = CreateCaption(parent, name);
            label.text = title;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(.28f, row);
            rect.anchorMax = new Vector2(.72f, row + .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetBarHeight(RectTransform bar, float height)
        {
            if (bar == null) return;
            bar.sizeDelta = new Vector2(0f, height);
        }

        public IEnumerator SetLetterbox(bool visible, float duration = 0.5f)
        {
            float target = visible ? 120f : 0f;
            float startTop = _letterboxTop != null ? _letterboxTop.sizeDelta.y : 0f;
            float startBottom = _letterboxBottom != null ? _letterboxBottom.sizeDelta.y : 0f;
            float progress = 0f;

            if (duration <= 0f)
            {
                SetBarHeight(_letterboxTop, target);
                SetBarHeight(_letterboxBottom, target);
                yield break;
            }

            while (progress < 1f)
            {
                progress += Time.unscaledDeltaTime / duration;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
                SetBarHeight(_letterboxTop, Mathf.Lerp(startTop, target, eased));
                SetBarHeight(_letterboxBottom, Mathf.Lerp(startBottom, target, eased));
                yield return null;
            }

            SetBarHeight(_letterboxTop, target);
            SetBarHeight(_letterboxBottom, target);
        }

        public IEnumerator PlayLine(string speaker, string text, float charsPerSecond = 42f, float hold = 1.1f)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            string prefix = string.IsNullOrEmpty(speaker) ? string.Empty : $"{speaker}: ";
            _subtitleText.text = string.Empty;
            _subtitleGroup.alpha = 1f;

            float reveal = Mathf.Max(0.18f, text.Length / Mathf.Max(1f, charsPerSecond));
            float elapsed = 0f;

            while (elapsed < reveal)
            {
                elapsed += Time.unscaledDeltaTime;
                int count = Mathf.Clamp(Mathf.CeilToInt(text.Length * Mathf.Clamp01(elapsed / reveal)), 0, text.Length);
                _subtitleText.text = prefix + text.Substring(0, count);
                yield return null;
            }

            _subtitleText.text = prefix + text;
            yield return new WaitForSecondsRealtime(hold);

            float fade = 0.22f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                _subtitleGroup.alpha = 1f - Mathf.Clamp01(t / fade);
                yield return null;
            }

            _subtitleGroup.alpha = 0f;
            _subtitleText.text = string.Empty;
        }

        public IEnumerator ShowChoice(float timeout, Action<CutsceneChoice> onComplete)
        {
            _choiceIndex = -1;
            _choiceGroup.alpha = 1f;

            float remaining = Mathf.Max(0.1f, timeout);
            while (remaining > 0f && _choiceIndex < 0)
            {
                _choiceTimer.text = $"{Mathf.CeilToInt(remaining)} с";
                ReadChoiceKeys();
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            CutsceneChoice choice = _choiceIndex == 1 ? CutsceneChoice.Act : CutsceneChoice.Wait;
            _choiceGroup.alpha = 0f;
            onComplete?.Invoke(choice);
        }

        private void ReadChoiceKeys()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            if ((keyboard != null && (keyboard.digit1Key.wasPressedThisFrame ||
                 keyboard.numpad1Key.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.dpad.up.wasPressedThisFrame))
                _choiceIndex = 0;
            else if ((keyboard != null && (keyboard.digit2Key.wasPressedThisFrame ||
                      keyboard.numpad2Key.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)) ||
                     (gamepad != null && gamepad.dpad.down.wasPressedThisFrame))
                _choiceIndex = 1;
        }

        public IEnumerator Flash(float duration = 0.35f)
        {
            if (_flash == null) yield break;

            float startAlpha = 0.95f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                _flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, 0f, t / duration));
                yield return null;
            }

            _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        public void HideAll()
        {
            StopAllCoroutines();

            if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
            if (_subtitleText != null) _subtitleText.text = string.Empty;
            if (_choiceGroup != null) _choiceGroup.alpha = 0f;
            if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        public void DestroyOverlay()
        {
            HideAll();
            if (_instance == this)
                _instance = null;
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            HideAll();
        }
    }
}
