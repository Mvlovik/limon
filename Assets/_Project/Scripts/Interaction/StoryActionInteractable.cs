using System.Collections.Generic;
using GameLimon.Story;
using UnityEngine;

namespace GameLimon.Interaction
{
    /// <summary>
    /// Интерактив, который проигрывает список StoryAction:
    /// мысль, флаг, звук, затемнение, завершение сцены.
    /// </summary>
    public sealed class StoryActionInteractable : InteractableBase
    {
        [Tooltip("Действия по нажатию E, по порядку.")]
        [SerializeField] private List<StoryAction> actions = new List<StoryAction>();

        public IReadOnlyList<StoryAction> Actions => actions;

        protected override void OnInteract(GameObject interactor)
        {
            StoryDirector.Ensure().Run(actions, transform.position);
        }
    }
}
