using System.Collections;
using GameLimon.Core;
using GameLimon.Player;
using UnityEngine;

namespace GameLimon.Staging
{
    // Scene-specific staging. BedWakeUp still owns E, getting up and the story flag.
    public sealed class ApartmentOpening : MonoBehaviour
    {
        [SerializeField] private Animation windowAnimation;
        [SerializeField] private Camera openingCamera;
        [SerializeField] private Transform player;

        public bool HasPlayed { get; private set; }
        public bool IsLookingAtWindow { get; private set; }

        private void Update()
        {
            var state = GameRoot.Instance != null ? GameRoot.Instance.State : null;
            if (!HasPlayed && state != null && state.CurrentSceneId == "A1_S01" &&
                state.GetFlag("A1_S01_WokeUp") && player != null &&
                windowAnimation != null && windowAnimation.clip != null && openingCamera != null)
            {
                HasPlayed = true;
                StartCoroutine(LookAtWindow());
            }
        }

        private IEnumerator LookAtWindow()
        {
            var motor = player.GetComponent<PlayerMotor>();
            var look = player.GetComponent<PlayerLook>();
            var controller = player.GetComponent<CharacterController>();
            var playerCamera = player.GetComponentInChildren<Camera>(true);
            var listener = playerCamera != null ? playerCamera.GetComponent<AudioListener>() : null;
            bool wasControlLocked = motor != null && motor.ControlLocked;
            bool wasPoseLocked = look != null && look.PoseLocked;
            bool controllerWasEnabled = controller != null && controller.enabled;
            bool cameraWasEnabled = playerCamera != null && playerCamera.enabled;
            bool listenerWasEnabled = listener != null && listener.enabled;

            IsLookingAtWindow = true;
            if (motor != null) motor.SetControlLocked(true);
            if (look != null) look.PoseLocked = true;
            if (controller != null) controller.enabled = false;
            if (playerCamera != null) playerCamera.enabled = false;
            if (listener != null) listener.enabled = false;
            openingCamera.gameObject.SetActive(true);

            try
            {
                if (windowAnimation.Play())
                {
                    yield return new WaitForSeconds(windowAnimation.clip.length);
                    // A cut back into the apartment, now facing the hallway.
                    player.SetPositionAndRotation(new Vector3(-2.65f, 15.05f, 3.35f),
                        Quaternion.Euler(0f, 180f, 0f));
                    if (motor != null) motor.ResetMomentum();
                    if (playerCamera != null)
                    {
                        playerCamera.transform.localPosition = new Vector3(0f,
                            motor != null ? motor.StandEyeHeight : 1.62f, 0f);
                        playerCamera.transform.localRotation = Quaternion.identity;
                    }
                    if (look != null) look.ResyncPose();
                }
            }
            finally
            {
                if (windowAnimation != null) windowAnimation.Stop();
                if (openingCamera != null) openingCamera.gameObject.SetActive(false);
                if (playerCamera != null) playerCamera.enabled = cameraWasEnabled;
                if (listener != null) listener.enabled = listenerWasEnabled;
                if (controller != null) controller.enabled = controllerWasEnabled;
                if (look != null) look.PoseLocked = wasPoseLocked;
                if (motor != null) motor.SetControlLocked(wasControlLocked);
                IsLookingAtWindow = false;
            }
        }
    }
}
