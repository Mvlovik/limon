using GameLimon.Core;
using GameLimon.Interaction;
using UnityEngine;

namespace GameLimon.Examples
{
    /// <summary>
    /// Пример собственного интерактива на E.
    /// Наследуемся от InteractableBase и получаем:
    /// - подсказку "E - ...";
    /// - oneShot;
    /// - requiredStep - объект сам не сработает вне нужного шага квеста.
    /// В OnInteract делает всё, что нужно сцене: ставит флаг, включает объект,
    /// запускает анимацию. Флаг завершения шага двигает квест дальше.
    /// </summary>
    public sealed class ExampleQuestInteractable : InteractableBase
    {
        [Tooltip("Флаг, завершающий шаг квеста. Должен совпадать с completionFlag шага.")]
        [SerializeField] private string completionFlag;

        [Tooltip("Необязательный объект, который включится после взаимодействия.")]
        [SerializeField] private GameObject reveal;

        protected override void OnInteract(GameObject interactor)
        {
            GameState state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            
            if (state != null && !string.IsNullOrEmpty(completionFlag))
                state.SetFlag(completionFlag, true);

            if (reveal != null)
                reveal.SetActive(true);
        }
    }
}
