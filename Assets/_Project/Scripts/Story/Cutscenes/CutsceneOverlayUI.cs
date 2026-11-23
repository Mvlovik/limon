using System;
using System.Collections;
using GameLimon.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
    /// the two-option choice with a timeout and the "break free" QTE.
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
        private Text _subtitleSpeaker;
        private Text _subtitleText;
        private CanvasGroup _choiceGroup;
        private RectTransform _choiceRect;
        private Text _choiceTitle;
        private Text _choiceHint;
        private Text _choiceTimer;
        private Button _waitButton;
        private Button _actButton;
        private CanvasGroup _waitGroup;
        private CanvasGroup _actGroup;
        private Vector2 _waitBasePos;
        private Vector2 _actBasePos;
        private CanvasGroup _qteGroup;
        private Text _qteText;
        private Image _qteFill;
        private Image _flash;

        private int _choiceIndex = -1;
        private int _qtePresses;

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

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("[CutsceneEventSystem]");
            eventSystem.transform.SetParent(transform, false);
            eventSystem.AddComponent<EventSystem>();
            var inputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private void Build()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup rootGroup = canvasObject.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;

            BuildLetterbox(canvasObject.transform);
            BuildSubtitle(canvasObject.transform);
            BuildChoice(canvasObject.transform);
            BuildQte(canvasObject.transform);
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
            var groupObject = new GameObject("Subtitle", typeof(RectTransform));
            groupObject.transform.SetParent(parent, false);

            var rect = (RectTransform)groupObject.transform;
            // Процентные якоря: панель всегда внутри экрана при любом разрешении
            // и соотношении сторон, и всегда выше нижнего letterbox-бара (120/1080 ≈ 11%).
            rect.anchorMin = new Vector2(0.08f, 0.14f);
            rect.anchorMax = new Vector2(0.92f, 0.34f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f);

            _subtitleGroup = groupObject.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;
            _subtitleGroup.blocksRaycasts = false;
            _subtitleGroup.interactable = false;

            _subtitleSpeaker = CreateText(groupObject.transform, "Speaker", 24, TextAnchor.UpperCenter, new Color(0.92f, 0.78f, 0.45f, 1f));
            RectTransform speakerRect = _subtitleSpeaker.rectTransform;
            speakerRect.anchorMin = new Vector2(0f, 1f);
            speakerRect.anchorMax = new Vector2(1f, 1f);
            speakerRect.pivot = new Vector2(0.5f, 1f);
            speakerRect.sizeDelta = new Vector2(0f, 36f);
            speakerRect.anchoredPosition = Vector2.zero;

            _subtitleText = CreateText(groupObject.transform, "Line", 32, TextAnchor.MiddleCenter, new Color(0.96f, 0.94f, 0.9f, 1f));
            RectTransform lineRect = _subtitleText.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 0f);
            lineRect.anchorMax = new Vector2(1f, 1f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.offsetMin = new Vector2(0f, 0f);
            lineRect.offsetMax = new Vector2(0f, -38f);

            var shadow = _subtitleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            // Шрифт следует за высотой канваса — субтитры читаемы на любом разрешении.
            var speakerFitter = _subtitleSpeaker.GetComponent<CaptionTextFitter>();
            if (speakerFitter == null) speakerFitter = _subtitleSpeaker.gameObject.AddComponent<CaptionTextFitter>();
            speakerFitter.Setup(24, 12, 56);

            var lineFitter = _subtitleText.GetComponent<CaptionTextFitter>();
            if (lineFitter == null) lineFitter = _subtitleText.gameObject.AddComponent<CaptionTextFitter>();
            lineFitter.Setup(32, 14, 64);
        }

        private void BuildChoice(Transform parent)
        {
            var groupObject = new GameObject("Choice", typeof(RectTransform));
            groupObject.transform.SetParent(parent, false);

            var rect = (RectTransform)groupObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(860f, 400f);
            rect.anchoredPosition = new Vector2(0f, -60f);
            _choiceRect = rect;

            _choiceGroup = groupObject.AddComponent<CanvasGroup>();
            _choiceGroup.alpha = 0f;
            _choiceGroup.blocksRaycasts = false;
            _choiceGroup.interactable = false;

            // Затемняющая подложка: текст читаем поверх игры и зоны не «слипаются».
            RectTransform backdrop = CreatePanel(groupObject.transform, "Backdrop", new Color(0f, 0f, 0f, 0.62f));
            backdrop.gameObject.GetComponent<Image>().raycastTarget = true;

            // Зона заголовка — верхняя треть панели, кнопки её не касаются.
            _choiceTitle = CreateText(groupObject.transform, "Prompt", 28, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.88f, 1f));
            RectTransform titleRect = _choiceTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.72f);
            titleRect.anchorMax = new Vector2(1f, 0.97f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-24f, 0f);

            // Кнопки — по центру, с подписью клавиши прямо на кнопке.
            _waitButton = CreateButton(groupObject.transform, "WaitButton", "[1] Ждать", Vector2.zero, out Text waitLabel);
            _actButton = CreateButton(groupObject.transform, "ActButton", "[2] Действовать", Vector2.zero, out Text actLabel);

            // Нельзя использовать ?? с UnityEngine.Object: GetComponent возвращает
            // «фальшивый null», который оператор ?? считает валидным объектом.
            // Из-за этого CanvasGroup не добавлялся, и Build() падал с
            // MissingComponentException — катсцена не доходила до контента.
            _waitGroup = _waitButton.GetComponent<CanvasGroup>();
            if (_waitGroup == null)
                _waitGroup = _waitButton.gameObject.AddComponent<CanvasGroup>();

            _actGroup = _actButton.GetComponent<CanvasGroup>();
            if (_actGroup == null)
                _actGroup = _actButton.gameObject.AddComponent<CanvasGroup>();
            _waitBasePos = new Vector2(-200f, 10f);
            _actBasePos = new Vector2(200f, 10f);
            _waitButton.transform.localScale = Vector3.one;
            _actButton.transform.localScale = Vector3.one;
            _waitButton.onClick.AddListener(() => _choiceIndex = 0);
            _actButton.onClick.AddListener(() => _choiceIndex = 1);

            waitLabel.fontSize = 28;
            actLabel.fontSize = 28;
            waitLabel.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            actLabel.color = new Color(0.92f, 0.92f, 0.88f, 1f);

            // Подсказка под кнопками.
            _choiceHint = CreateText(groupObject.transform, "Hint", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.72f, 0.9f));
            RectTransform hintRect = _choiceHint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0.2f);
            hintRect.anchorMax = new Vector2(1f, 0.3f);
            hintRect.offsetMin = new Vector2(24f, 0f);
            hintRect.offsetMax = new Vector2(-24f, 0f);

            // Таймер — самая нижняя полоса.
            _choiceTimer = CreateText(groupObject.transform, "Timer", 22, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.72f, 1f));
            RectTransform timerRect = _choiceTimer.rectTransform;
            timerRect.anchorMin = new Vector2(0f, 0.02f);
            timerRect.anchorMax = new Vector2(1f, 0.14f);
            timerRect.offsetMin = new Vector2(24f, 0f);
            timerRect.offsetMax = new Vector2(-24f, 0f);

            _waitGroup.alpha = 0f;
            _actGroup.alpha = 0f;
        }

        private void BuildQte(Transform parent)
        {
            var groupObject = new GameObject("QTE", typeof(RectTransform));
            groupObject.transform.SetParent(parent, false);

            var rect = (RectTransform)groupObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(880f, 170f);
            rect.anchoredPosition = new Vector2(0f, -80f);

            _qteGroup = groupObject.AddComponent<CanvasGroup>();
            _qteGroup.alpha = 0f;
            _qteGroup.blocksRaycasts = false;
            _qteGroup.interactable = false;

            _qteText = CreateText(groupObject.transform, "Prompt", 26, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.86f, 1f));
            RectTransform textRect = _qteText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0.45f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            RectTransform barBack = CreatePanel(groupObject.transform, "BarBack", new Color(0f, 0f, 0f, 0.75f));
            barBack.anchorMin = new Vector2(0.1f, 0.12f);
            barBack.anchorMax = new Vector2(0.9f, 0.34f);
            barBack.offsetMin = Vector2.zero;
            barBack.offsetMax = Vector2.zero;

            RectTransform fillObject = CreatePanel(barBack, "BarFill", new Color(0.86f, 0.3f, 0.18f, 1f));
            fillObject.anchorMin = new Vector2(0f, 0f);
            fillObject.anchorMax = new Vector2(0f, 1f);
            fillObject.pivot = new Vector2(0f, 0.5f);
            fillObject.offsetMin = new Vector2(4f, 4f);
            fillObject.offsetMax = new Vector2(4f, -4f);
            _qteFill = fillObject.GetComponent<Image>();
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

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 position, out Text labelText)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 76f);
            rect.anchoredPosition = position;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.11f, 0.9f);

            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.13f, 0.14f, 0.17f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.23f, 0.14f, 1f);
            colors.pressedColor = new Color(0.55f, 0.36f, 0.12f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            labelText = CreateText(go.transform, "Label", 26, TextAnchor.MiddleCenter, Color.white);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
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

            _subtitleSpeaker.text = speaker ?? string.Empty;
            _subtitleText.text = string.Empty;
            _subtitleGroup.alpha = 1f;

            float reveal = Mathf.Max(0.18f, text.Length / Mathf.Max(1f, charsPerSecond));
            float elapsed = 0f;

            while (elapsed < reveal)
            {
                elapsed += Time.unscaledDeltaTime;
                int count = Mathf.Clamp(Mathf.CeilToInt(text.Length * Mathf.Clamp01(elapsed / reveal)), 0, text.Length);
                _subtitleText.text = text.Substring(0, count);
                yield return null;
            }

            _subtitleText.text = text;
            yield return new WaitForSecondsRealtime(hold);

            float fade = 0.22f;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                _subtitleGroup.alpha = 1f - Mathf.Clamp01(t / fade);
                yield return null;
            }

            _subtitleGroup.alpha = 0f;
            _subtitleText.text = string.Empty;
            _subtitleSpeaker.text = string.Empty;
        }

        public IEnumerator ShowChoice(string prompt, float timeout, Action<CutsceneChoice> onComplete)
        {
            _choiceIndex = -1;
            _choiceTitle.text = prompt;
            _choiceTimer.text = string.Empty;
            if (_choiceHint != null) _choiceHint.text = "Выбери [1] или [2] — или щёлкни кнопку";
            _choiceGroup.blocksRaycasts = true;
            _choiceGroup.interactable = true;

            yield return ChoiceEnterAnimation();

            float remaining = Mathf.Max(0.1f, timeout);
            while (remaining > 0f && _choiceIndex < 0)
            {
                ReadChoiceKeys();
                remaining -= Time.unscaledDeltaTime;
                _choiceTimer.text = $"Автовыбор через {Mathf.Max(0, Mathf.CeilToInt(remaining))} с";

                // Пульс обеих кнопок, пока игрок думает.
                float pulse = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 5.5f);
                _waitGroup.alpha = pulse;
                _actGroup.alpha = pulse;

                yield return null;
            }

            CutsceneChoice choice = _choiceIndex == 1 ? CutsceneChoice.Act : CutsceneChoice.Wait;
            _choiceGroup.blocksRaycasts = false;
            _choiceGroup.interactable = false;
            yield return ChoiceExitAnimation();
            onComplete?.Invoke(choice);
        }

        /// <summary>Въезд панели: затемнение + масштаб, затем поочерёдное появление кнопок.</summary>
        private IEnumerator ChoiceEnterAnimation()
        {
            _choiceGroup.alpha = 0f;
            _choiceRect.localScale = Vector3.one * 0.88f;
            _waitGroup.alpha = 0f;
            _actGroup.alpha = 0f;
            ((RectTransform)_waitButton.transform).anchoredPosition = _waitBasePos + new Vector2(0f, -16f);
            ((RectTransform)_actButton.transform).anchoredPosition = _actBasePos + new Vector2(0f, -16f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.18f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                _choiceGroup.alpha = k;
                _choiceRect.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, k);
                yield return null;
            }
            _choiceGroup.alpha = 1f;
            _choiceRect.localScale = Vector3.one;

            yield return ChoiceButtonSlideIn(_waitButton.transform, _waitBasePos, _waitGroup);
            yield return ChoiceButtonSlideIn(_actButton.transform, _actBasePos, _actGroup);
        }

        private IEnumerator ChoiceButtonSlideIn(Transform button, Vector2 basePos, CanvasGroup group)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.16f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                group.alpha = k;
                ((RectTransform)button).anchoredPosition = Vector2.Lerp(basePos + new Vector2(0f, -16f), basePos, k);
                yield return null;
            }
            group.alpha = 1f;
            ((RectTransform)button).anchoredPosition = basePos;
        }

        private IEnumerator ChoiceExitAnimation()
        {
            _waitGroup.alpha = 1f;
            _actGroup.alpha = 1f;
            float start = _choiceGroup.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.16f;
                _choiceGroup.alpha = Mathf.Lerp(start, 0f, t);
                _choiceRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, t);
                yield return null;
            }
            _choiceGroup.alpha = 0f;
            _choiceRect.localScale = Vector3.one;
            _choiceTimer.text = string.Empty;
        }

        private void ReadChoiceKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                _choiceIndex = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                _choiceIndex = 1;
        }

        public IEnumerator RunQte(string prompt, float duration, int requiredPresses, Action<bool> onComplete)
        {
            _qtePresses = 0;
            _qteText.text = $"{prompt}\n<size=22>Жми E / Пробел</size>";
            _qteGroup.alpha = 1f;
            _qteFill.rectTransform.anchorMax = new Vector2(0f, 1f);

            float elapsed = 0f;
            bool success = false;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (WasQtePressed())
                    _qtePresses++;

                float progress = Mathf.Clamp01((float)_qtePresses / Mathf.Max(1, requiredPresses));
                _qteFill.rectTransform.anchorMax = new Vector2(progress, 1f);

                if (_qtePresses >= requiredPresses)
                {
                    success = true;
                    break;
                }

                yield return null;
            }

            _qteGroup.alpha = 0f;
            onComplete?.Invoke(success);
        }

        private static bool WasQtePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                return true;

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
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
            if (_subtitleSpeaker != null) _subtitleSpeaker.text = string.Empty;
            if (_choiceGroup != null)
            {
                _choiceGroup.alpha = 0f;
                _choiceGroup.blocksRaycasts = false;
                _choiceGroup.interactable = false;
            }
            if (_qteGroup != null) _qteGroup.alpha = 0f;
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
