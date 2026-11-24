using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLimon.MainMenu
{
    public sealed class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.035f;
        [SerializeField] private float speed = 12f;

        private Vector3 _baseScale;
        private bool _hovered;
        private bool _pressed;

        private void Awake() => _baseScale = transform.localScale;

        private void Update()
        {
            float target = _pressed ? 0.98f : (_hovered ? hoverScale : 1f);
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * target, Time.unscaledDeltaTime * speed);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData) { _hovered = false; _pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
    }
}
