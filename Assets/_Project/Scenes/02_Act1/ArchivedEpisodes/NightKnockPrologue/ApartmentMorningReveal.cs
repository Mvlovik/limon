using System.Collections;
using GameLimon.Core;
using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Staging
{
    // Local visual handoff from the nightmare to the apartment's existing wake interaction.
    public sealed class ApartmentMorningReveal : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private BedWakeUp bedWakeUp;
        [SerializeField, Range(0f, 1f)] private float sleepingDarkness = 0.92f;
        [SerializeField, Min(0.01f)] private float revealDuration = 2f;
        [SerializeField, Min(0.01f)] private float brightenDuration = 2.4f;
        [SerializeField, Min(0f)] private float blackoutDuration = 3f;

        private static float blackoutEndsAt;
        private bool sawLying;
        private bool waking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBlackout() => blackoutEndsAt = 0f;

        // Called by the nightmare clip exactly when the bulb bursts.
        public void MarkNightBlackout() => blackoutEndsAt = Time.unscaledTime + blackoutDuration;

        private void Awake()
        {
            if (overlay == null) return; // The nightmare instance only supplies the animation event.
            overlay.alpha = 1f;
            overlay.blocksRaycasts = false;
            overlay.interactable = false;
            if (overlayCanvas != null) overlayCanvas.sortingOrder = 2000;
        }

        private void Update()
        {
            if (overlay == null || bedWakeUp == null || waking) return;
            if (bedWakeUp.CanInteract) sawLying = true;
            else if (sawLying) waking = true; // E was accepted by the existing BedWakeUp component.
        }

        private IEnumerator Start()
        {
            if (overlay == null) yield break;
            yield return null;
            yield return null;

            if (GameRoot.Instance != null && GameRoot.Instance.State.GetFlag("A1_S01_WokeUp"))
            {
                Finish();
                yield break;
            }

            while (Time.unscaledTime < blackoutEndsAt) yield return null;
            if (overlayCanvas != null) overlayCanvas.sortingOrder = 900;

            float elapsed = 0f;
            while (elapsed < revealDuration && !waking)
            {
                elapsed += Time.unscaledDeltaTime;
                overlay.alpha = Mathf.Lerp(1f, sleepingDarkness,
                    Mathf.SmoothStep(0f, 1f, elapsed / revealDuration));
                yield return null;
            }

            while (!waking) yield return null;
            float from = overlay.alpha;
            elapsed = 0f;
            while (elapsed < brightenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlay.alpha = Mathf.Lerp(from, 0f,
                    Mathf.SmoothStep(0f, 1f, elapsed / brightenDuration));
                yield return null;
            }
            Finish();
        }

        private void Finish()
        {
            overlay.alpha = 0f;
            if (overlayCanvas != null) overlayCanvas.enabled = false;
        }
    }
}
