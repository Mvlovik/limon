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
            {
                button.onClick.AddListener(HandleClick);
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors;
                colors.normalColor = new Color(0.12f, 0.15f, 0.2f, 0.96f);
                colors.highlightedColor = new Color(0.22f, 0.3f, 0.38f, 0.98f);
                colors.pressedColor = new Color(0.32f, 0.42f, 0.5f, 1f);
                colors.disabledColor = new Color(0.08f, 0.09f, 0.12f, 0.6f);
                colors.fadeDuration = 0.12f;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
                MenuButtonHover hover = button.GetComponent<MenuButtonHover>();
                if (hover == null)
                    button.gameObject.AddComponent<MenuButtonHover>();
                Image buttonImage = button.GetComponent<Image>();
                if (buttonImage != null)
                    buttonImage.color = new Color(0.055f, 0.1f, 0.15f, 0.98f);
                if (button.GetComponent<Shadow>() == null)
                {
                    var shadow = button.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                    shadow.effectDistance = new Vector2(0f, -4f);
                }
            }
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
            {
                button.interactable = unlocked;
                Outline outline = button.GetComponent<Outline>();
                if (outline == null)
                    outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = completed
                    ? new Color(0.35f, 0.95f, 0.72f, 0.9f)
                    : new Color(0.95f, 0.78f, 0.38f, 0.75f);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
                outline.useGraphicAlpha = true;
            }
        }

        private void HandleClick()
        {
            _onClick?.Invoke();
        }
    }
}
