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
