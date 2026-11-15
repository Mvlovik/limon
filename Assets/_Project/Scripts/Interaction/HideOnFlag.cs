using GameLimon.Core;
using UnityEngine;

namespace GameLimon.Interaction
{
    /// <summary>
    /// Скрывает или удаляет объект, когда в GameState появляется указанный флаг.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HideOnFlag : MonoBehaviour
    {
        [Tooltip("Флаг, при появлении которого объект скрывается.")]
        [SerializeField] private string flag;

        [Tooltip("Задержка перед скрытием, секунды. Можно использовать для звука или анимации.")]
        [SerializeField] private float delay;

        [Tooltip("Удалить объект полностью. Если выключено — просто выключить GameObject.")]
        [SerializeField] private bool destroyGameObject;

        [Tooltip("Скрыть сразу при старте, если флаг уже стоит (например, после загрузки сейва).")]
        [SerializeField] private bool hideIfFlagAlreadySet = true;

        private GameState _state;
        private Coroutine _hideRoutine;

        private void OnEnable()
        {
            ResolveState();
        }

        private void Start()
        {
            // GameRoot может появиться чуть позже OnEnable при аддитивной загрузке.
            if (_state == null) ResolveState();
        }

        private void OnDisable()
        {
            if (_state != null)
                _state.FlagChanged -= OnFlagChanged;
            _state = null;
        }

        private void ResolveState()
        {
            if (_state != null)
                return;

            if (string.IsNullOrWhiteSpace(flag))
            {
                Debug.LogWarning("[HideOnFlag] Не задан флаг — компонент отключён.", this);
                return;
            }

            _state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (_state == null)
                return;

            _state.FlagChanged += OnFlagChanged;

            if (hideIfFlagAlreadySet && _state.GetFlag(flag))
                Hide();
        }

        private void OnFlagChanged(string changedFlag, bool value)
        {
            if (value && changedFlag == flag)
                Hide();
        }

        private void Hide()
        {
            if (_hideRoutine != null)
                return;

            if (delay <= 0f)
            {
                HideNow();
                return;
            }

            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private System.Collections.IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(delay);
            _hideRoutine = null;
            HideNow();
        }

        private void HideNow()
        {
            if (_state != null)
                _state.FlagChanged -= OnFlagChanged;

            _state = null;
            
            if (destroyGameObject) Destroy(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
