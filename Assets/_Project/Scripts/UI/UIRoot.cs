using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameLimon.UI
{
    /// <summary>
    /// UI, живущий поверх Boot. 
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float defaultFadeDuration = 0.5f;

        private Coroutine _fadeRoutine;
        private CanvasGroup _loadingGroup;
        private Text _loadingLabel;

        public bool IsFading => _fadeRoutine != null;

        /// <summary>Корневой Canvas (подсказки, HUD)</summary>
        public Canvas Canvas => canvas;

        private void Awake()
        {
            if (canvas == null || fadeOverlay == null)
            {
                DefaultCanvas built = BuildDefaultCanvas(transform);
                canvas = built.Canvas;
                fadeOverlay = built.FadeOverlay;
            }

            BuildLoadingPanel();
        }

        /// <summary>Назначает ссылки на канвас</summary>
        public void AssignReferences(Canvas builtCanvas, CanvasGroup builtFadeOverlay)
        {
            canvas = builtCanvas;
            fadeOverlay = builtFadeOverlay;
        }

        /// <summary>Мгновенно выставить зашторку: 0 - прозрачно, 1 - чёрный экран.</summary>
        public void SetFade(float alpha)
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (fadeOverlay != null)
                fadeOverlay.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>Плавно увести зашторку к целевой прозрачности.</summary>
        public Coroutine FadeTo(float alpha, float duration)
        {
            alpha = Mathf.Clamp01(alpha);

            if (duration <= 0f || fadeOverlay == null)
            {
                SetFade(alpha);
                return null;
            }

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(FadeRoutine(alpha, duration));
            return _fadeRoutine;
        }

        public Coroutine FadeOut() => FadeTo(1f, defaultFadeDuration);
        public Coroutine FadeIn() => FadeTo(0f, defaultFadeDuration);

        public void ShowLoading(string message = "Загрузка...")
        {
            BuildLoadingPanel();
            if (_loadingLabel != null)
                _loadingLabel.text = message;
            if (_loadingGroup != null)
            {
                _loadingGroup.alpha = 1f;
                _loadingGroup.blocksRaycasts = true;
                _loadingGroup.interactable = true;
                _loadingGroup.transform.SetAsLastSibling();
            }
        }

        public void HideLoading()
        {
            if (_loadingGroup == null)
                return;
            _loadingGroup.alpha = 0f;
            _loadingGroup.blocksRaycasts = false;
            _loadingGroup.interactable = false;
        }

        private void BuildLoadingPanel()
        {
            if (canvas == null || _loadingGroup != null)
                return;

            GameObject panel = new GameObject("LoadingPanel");
            panel.transform.SetParent(canvas.transform, false);
            Image background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.98f);
            background.raycastTarget = true;
            _loadingGroup = panel.AddComponent<CanvasGroup>();
            _loadingGroup.alpha = 0f;
            _loadingGroup.blocksRaycasts = false;
            _loadingGroup.interactable = false;
            StretchToParent(panel.transform as RectTransform);

            GameObject labelObject = new GameObject("LoadingLabel");
            labelObject.transform.SetParent(panel.transform, false);
            _loadingLabel = labelObject.AddComponent<Text>();
            _loadingLabel.text = "Загрузка...";
            _loadingLabel.alignment = TextAnchor.MiddleCenter;
            _loadingLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _loadingLabel.fontSize = 42;
            _loadingLabel.fontStyle = FontStyle.Bold;
            _loadingLabel.color = new Color(0.78f, 0.96f, 0.94f, 1f);
            _loadingLabel.raycastTarget = false;
            StretchToParent(labelObject.transform as RectTransform);
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            float startAlpha = fadeOverlay.alpha;
            float progress = 0f;

            while (progress < 1f)
            {
                progress += Time.unscaledDeltaTime / duration;
                fadeOverlay.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                yield return null;
            }

            fadeOverlay.alpha = targetAlpha;
            _fadeRoutine = null;
        }

        /// <summary>
        /// Минимальный канвас с чёрной зашторкой.
        /// </summary>
        public static DefaultCanvas BuildDefaultCanvas(Transform parent)
        {
            var canvasObject = new GameObject("UI");
            canvasObject.transform.SetParent(parent, false);

            Canvas builtCanvas = canvasObject.AddComponent<Canvas>();
            builtCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            builtCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var overlayObject = new GameObject("FadeOverlay");
            overlayObject.transform.SetParent(canvasObject.transform, false);

            Image image = overlayObject.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            CanvasGroup group = overlayObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            StretchToParent(overlayObject.transform as RectTransform);

            return new DefaultCanvas
            {
                Canvas = builtCanvas,
                FadeOverlay = group
            };
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public struct DefaultCanvas
        {
            public Canvas Canvas;
            public CanvasGroup FadeOverlay;
        }
    }
}
