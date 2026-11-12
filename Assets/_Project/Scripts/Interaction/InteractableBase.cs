using UnityEngine;

namespace GameLimon.Interaction
{
    /// <summary>хранит подсказку и признак </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Tooltip("Текст подсказки на экране. Например: \"E - заварить кофе\".")]
        [SerializeField] private string prompt = "E — взаимодействовать";

        [Tooltip("Если включено, объект сработает только один раз за загрузку сцены.")]
        [SerializeField] private bool oneShot;

        protected bool Used { get; private set; }
        public string InteractionPrompt => prompt;
        public virtual bool CanInteract => !Used || !oneShot;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;

            OnInteract(interactor);

            if (oneShot) Used = true;
        }

        protected abstract void OnInteract(GameObject interactor);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(prompt))
                prompt = "E — взаимодействовать";
        }
#endif
    }
}
