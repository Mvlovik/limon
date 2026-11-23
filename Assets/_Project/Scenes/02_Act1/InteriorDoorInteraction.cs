using System.Collections;
using GameLimon.Interaction;
using UnityEngine;

namespace GameLimon.Staging
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class InteriorDoorInteraction : InteractableBase, IInteractable
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 90f;
        [SerializeField, Min(0.01f)] private float openDuration = 0.55f;
        [SerializeField] private AudioSource openingSound;
        [SerializeField] private AudioClip closingSound;

        private BoxCollider doorCollider;
        private CharacterController overlappingPlayer;
        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private Coroutine swing;

        private Quaternion OpenRotation => closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        // Read the hinge because a cutscene can also move this door.
        private bool IsOpen => hinge != null &&
            Quaternion.Angle(hinge.localRotation, OpenRotation) <
            Quaternion.Angle(hinge.localRotation, closedRotation);

        string IInteractable.InteractionPrompt => IsOpen ? "E — закрыть дверь" : InteractionPrompt;

        public override bool CanInteract =>
            isActiveAndEnabled && hinge != null && swing == null && base.CanInteract;

        private void Awake()
        {
            doorCollider = GetComponent<BoxCollider>();
            if (hinge != null)
                closedRotation = hinge.localRotation;
        }

        protected override void OnInteract(GameObject interactor)
        {
            bool opening = !IsOpen;
            targetRotation = opening ? OpenRotation : closedRotation;
            RestorePlayerCollision();
            doorCollider.enabled = false;

            if (openingSound != null)
            {
                openingSound.Stop();
                if (opening)
                    openingSound.Play();
                else if (closingSound != null)
                    openingSound.PlayOneShot(closingSound);
            }

            swing = StartCoroutine(SwingDoor(interactor.GetComponent<CharacterController>()));
        }

        private IEnumerator SwingDoor(CharacterController actor)
        {
            Quaternion startRotation = hinge.localRotation;
            float duration = Mathf.Max(0.01f, openDuration);

            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, time / duration);
                hinge.localRotation = Quaternion.Slerp(startRotation, targetRotation, progress);
                yield return null;
            }

            hinge.localRotation = targetRotation;
            doorCollider.enabled = true;
            swing = null;

            // An actor still in the final pose can step out before the door blocks that pair again.
            if (actor != null && actor.enabled && Overlaps(actor))
            {
                Physics.IgnoreCollision(doorCollider, actor, true);
                overlappingPlayer = actor;
            }
        }

        private bool Overlaps(CharacterController actor)
        {
            return actor.gameObject.activeInHierarchy && Physics.ComputePenetration(
                doorCollider, doorCollider.transform.position, doorCollider.transform.rotation,
                actor, actor.transform.position, actor.transform.rotation, out _, out _);
        }

        private void FixedUpdate()
        {
            if (overlappingPlayer != null && (!overlappingPlayer.enabled || !Overlaps(overlappingPlayer)))
                RestorePlayerCollision();
        }

        private void RestorePlayerCollision()
        {
            if (overlappingPlayer != null && overlappingPlayer.enabled && overlappingPlayer.gameObject.activeInHierarchy &&
                doorCollider.enabled && doorCollider.gameObject.activeInHierarchy)
                Physics.IgnoreCollision(doorCollider, overlappingPlayer, false);
            overlappingPlayer = null;
        }

        private void OnDisable()
        {
            if (swing != null)
            {
                StopCoroutine(swing);
                swing = null;
                if (hinge != null)
                    hinge.localRotation = targetRotation;
                doorCollider.enabled = true;
            }
            RestorePlayerCollision();
        }
    }
}
