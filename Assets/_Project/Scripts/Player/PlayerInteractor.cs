using System;
using GameLimon.Core;
using GameLimon.Interaction;
using UnityEngine;

namespace GameLimon.Player
{
    /// <summary>
    /// Взаимодействие с объектами. Трассировка лучей.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Откуда пускать луч")]
        [SerializeField] private Transform view;

        [Tooltip("Дальность луча")]
        [SerializeField] private float distance = 2.5f;

        [Tooltip("Радиус сферического луча")]
        [SerializeField] private float aimRadius = 0.08f;

        [Tooltip("Сколько цель должна продержаться в прицеле")]
        [SerializeField] private float focusStability = 0.12f;

        [Tooltip("Слои, которые участвуют в поиске")]
        [SerializeField] private LayerMask mask = ~0;

        [Tooltip("Дебаг")]
        [SerializeField] private bool logFocusChanges;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private InputReader _input;
        private IInteractable _pending;
        private float _pendingSince;

        /// <summary>Текущий объект в прицеле или null</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Вызывается при смене объекта в прицеле</summary>
        public event Action<IInteractable> FocusChanged;

        private void Start()
        {
            if (view == null && Camera.main != null)
                view = Camera.main.transform;

            _input = GameRoot.Instance != null ? GameRoot.Instance.Input : null;

            if (_input == null)
                _input = GetComponent<InputReader>();

            if (_input != null)
                _input.InteractPressed += OnInteractPressed;
        }

        private void OnDestroy()
        {
            if (_input != null)
                _input.InteractPressed -= OnInteractPressed;
        }

        private void Update()
        {
            IInteractable next = FindInteractable();

            if (ReferenceEquals(next, Current))
            {
                _pending = null;
                return;
            }

            if (!ReferenceEquals(next, _pending))
            {
                _pending = next;
                _pendingSince = Time.unscaledTime;
            }

            if (Time.unscaledTime - _pendingSince >= focusStability)
                SetFocus(next);
        }

        private void OnInteractPressed()
        {
            if (Current != null && Current.CanInteract)
                Current.Interact(gameObject);
        }

        private IInteractable FindInteractable()
        {
            if (view == null) return null;

            int count = aimRadius > 0f
                ? Physics.SphereCastNonAlloc(view.position, aimRadius, view.forward, _hits, distance, mask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(view.position, view.forward, _hits, distance, mask, QueryTriggerInteraction.Ignore);

            RaycastHit closest = default;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];

                if (hit.collider == null) continue;

                if (hit.collider.transform.IsChildOf(transform)) continue;

                if (!found || hit.distance < closest.distance)
                {
                    closest = hit;
                    found = true;
                }
            }

            if (!found) return null;

          
            foreach (MonoBehaviour behaviour in closest.collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IInteractable interactable && interactable.CanInteract)
                    return interactable;
            }

            return null;
        }

        private void SetFocus(IInteractable next)
        {
            if (ReferenceEquals(Current, next)) return;

            Current = next;

            if (logFocusChanges)
                Debug.Log($"[Interactor] Фокус: {(next != null ? next.GetType().Name : "нет")}", this);

            FocusChanged?.Invoke(next);
        }
    }
}
