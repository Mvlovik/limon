using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameLimon.MainMenu
{
    /// <summary>
    /// Карточка одной сцены на экране выбора. Вся вёрстка задаётся в префабе.
    /// </summary>
    public sealed class SceneCardView : MonoBehaviour
    {
        [SerializeField] private Image preview;
        [SerializeField] private Text actTitle;
        [SerializeField] private Text sceneName;
        [SerializeField] private Text state;
        [SerializeField] private Button button;

        private Action _onClick;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(MenuSceneEntry entry, bool completed, bool unlocked, Material lockedMaterial, Action onClick)
        {
            _onClick = onClick;

            if (preview != null)
            {
                preview.sprite = entry.preview;
                preview.material = completed ? null : lockedMaterial;
            }

            if (actTitle != null)
                actTitle.text = entry.actTitle;

            if (sceneName != null)
                sceneName.text = entry.displayName;

            if (state != null)
            {
                state.text = completed ? "ПРОЙДЕНО" : "НЕ ПРОЙДЕНО";
                state.color = completed
                    ? new Color(0.52f, 0.86f, 0.55f, 1f)
                    : new Color(0.72f, 0.74f, 0.78f, 1f);
            }

            if (button != null)
                button.interactable = unlocked;
        }

        private void HandleClick()
        {
            _onClick?.Invoke();
        }
    }
}
