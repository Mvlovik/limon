namespace GameLimon.Interaction
{
    /// <summary>
    /// Всё, с чем игрок может взаимодействовать по клавише E, реализует этот интерфейс.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Текст подсказки</summary>
        string InteractionPrompt { get; }

        /// <summary>Можно ли взаимодействовать прямо сейчас.</summary>
        bool CanInteract { get; }

        /// <summary>Вызывается игроком.</summary>
        void Interact(UnityEngine.GameObject interactor);
    }
}
