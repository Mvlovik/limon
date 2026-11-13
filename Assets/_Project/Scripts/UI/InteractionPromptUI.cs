using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameLimon.UI
{
    /// <summary>
    /// Подсказка "E - ...". Только отображение: слушает PlayerInteractor.
    /// </summary>
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private float fontSize = 28f;
        [SerializeField] private Color textColor = new Color(0.95f, 0.93f, 0.88f, 1f);

        private PlayerInteractor _interactor;
        private Text _label;

        private void Start()
        {
            _interactor = GetComponent<PlayerInteractor>();

            GameObject host = BuildLabel();
            if (host == null) return;

            _label = host.GetComponent<Text>();
            _label.text = string.Empty;

            if (_interactor != null)
                _interactor.FocusChanged += OnFocusChanged;
        }

        private void OnDestroy()
        {
            if (_interactor != null)
                _interactor.FocusChanged -= OnFocusChanged;
        }

        private void OnFocusChanged(IInteractable interactable)
        {
            if (_label == null) return;

            bool visible = interactable != null && interactable.CanInteract;
            _label.text = visible ? interactable.InteractionPrompt : string.Empty;
        }

        private GameObject BuildLabel()
        {
            UIRoot ui = GameRoot.Instance != null ? GameRoot.Instance.UI : null;
            Canvas canvas = ui != null ? ui.Canvas : null;

            if (canvas == null)
            {
                Debug.LogWarning("[InteractionPromptUI] Не найден Canvas UIRoot", this);
                return null;
            }

            var host = new GameObject("InteractionPrompt", typeof(RectTransform));
            host.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(900f, 60f);
            rect.anchoredPosition = new Vector2(0f, 140f);

            Text label = host.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = Mathf.RoundToInt(fontSize);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = textColor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            var shadow = host.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return host;
        }
    }
}
