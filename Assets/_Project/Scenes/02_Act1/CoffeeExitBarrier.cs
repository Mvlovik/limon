using GameLimon.Core;
using GameLimon.Interaction;
using UnityEngine;

namespace GameLimon.Staging
{
    // Local apartment staging: the existing coffee interaction owns the flag.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CoffeeExitBarrier : InteractableBase
    {
        private BoxCollider _barrier;

        public bool IsBlocked => GameRoot.Instance == null || GameRoot.Instance.State == null ||
            !GameRoot.Instance.State.GetFlag("A1_S01_CoffeeStarted");

        public override bool CanInteract => base.CanInteract && IsBlocked;

        private void Awake()
        {
            _barrier = GetComponent<BoxCollider>();
            _barrier.isTrigger = false;
        }

        private void Update() => _barrier.enabled = IsBlocked;

        protected override void OnInteract(GameObject interactor)
        {
            Debug.Log("[Apartment] Сначала нужно приготовить кофе.", this);
        }
    }
}
