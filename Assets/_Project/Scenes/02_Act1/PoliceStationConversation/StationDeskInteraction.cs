using System.Collections;
using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using GameLimon.Story;
using GameLimon.UI;
using UnityEngine;

namespace GameLimon.Staging
{
    // Scene-local handoff from the existing player to the existing station clip.
    public sealed class StationDeskInteraction : InteractableBase
    {
        [SerializeField] private Animation conversation;
        [SerializeField] private GameObject firstPerson;
        [SerializeField] private Camera seatedCamera;
        [SerializeField] private GameObject joe;
        [SerializeField] private AudioSource conversationAudio;
        [SerializeField] private Transform spawn;
        [SerializeField] private GameObject editorPlayerPrefab;
        [SerializeField] private GameObject editorServicesPrefab;
        [SerializeField] private float sitDuration = 0.65f;

        private bool started;
        private GameObject player;
        private PlayerMotor motor;
        private PlayerLook look;
        private PlayerInteractor interactor;
        private GameObject interactionPrompt;
        private readonly List<Camera> playerCameras = new List<Camera>();
        private readonly List<AudioListener> playerListeners = new List<AudioListener>();

        public override bool CanInteract => !started && base.CanInteract;

        private void Awake()
        {
            if (conversation != null)
            {
                conversation.playAutomatically = false;
                conversation.Stop();
            }
            if (conversationAudio != null)
            {
                conversationAudio.playOnAwake = false;
                conversationAudio.Stop();
            }
            firstPerson.SetActive(false);
            joe.SetActive(false);

            // Opening this scene directly in the editor uses the same project prefabs.
            if (Application.isEditor && GameRoot.Instance == null && editorServicesPrefab != null)
            {
                var services = Instantiate(editorServicesPrefab);
                var autoStart = services.GetComponent<BootAutoStart>();
                if (autoStart != null) autoStart.enabled = false;
            }
            if (Application.isEditor && GameObject.FindGameObjectWithTag("Player") == null && editorPlayerPrefab != null)
                Instantiate(editorPlayerPrefab);
        }

        private IEnumerator Start()
        {
            yield return null; // Let the normal additive scene/spawn handoff finish.
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null && Application.isEditor && editorPlayerPrefab != null)
                player = Instantiate(editorPlayerPrefab);
            if (player == null || spawn == null) yield break;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            if (controller != null) controller.enabled = true;
            player.GetComponent<PlayerMotor>()?.ResetMomentum();
            player.GetComponent<PlayerMotor>()?.SetControlLocked(false);
            var playerLook = player.GetComponent<PlayerLook>();
            if (playerLook != null) playerLook.PoseLocked = false;
            ObjectiveUI.Instance?.Hide();
        }

        protected override void OnInteract(GameObject interactorObject)
        {
            if (conversation == null || conversation.clip == null || seatedCamera == null) return;
            started = true;
            player = interactorObject;
            StartCoroutine(SitAndPlay());
        }

        private IEnumerator SitAndPlay()
        {
            motor = player.GetComponent<PlayerMotor>();
            look = player.GetComponent<PlayerLook>();
            interactor = player.GetComponent<PlayerInteractor>();
            motor?.ResetMomentum();
            motor?.SetControlLocked(true);
            if (look != null) look.PoseLocked = true;
            if (interactor != null) interactor.enabled = false;
            StoryDirector.Instance?.Stop();
            ObjectiveUI.Instance?.Hide();
            var canvas = GameRoot.Instance != null ? GameRoot.Instance.UI?.Canvas : null;
            var promptTransform = canvas != null ? canvas.transform.Find("InteractionPrompt") : null;
            if (promptTransform != null && promptTransform.gameObject.activeSelf)
            {
                interactionPrompt = promptTransform.gameObject;
                interactionPrompt.SetActive(false);
            }

            Camera fromCamera = null;
            foreach (var camera in player.GetComponentsInChildren<Camera>())
            {
                if (!camera.enabled) continue;
                fromCamera = camera;
                playerCameras.Add(camera);
                camera.enabled = false;
            }
            foreach (var listener in player.GetComponentsInChildren<AudioListener>())
            {
                if (!listener.enabled) continue;
                playerListeners.Add(listener);
                listener.enabled = false;
            }

            Transform view = seatedCamera.transform;
            Vector3 destination = view.position;
            Quaternion rotation = view.rotation;
            Vector3 origin = fromCamera != null ? fromCamera.transform.position : destination;
            Quaternion originRotation = fromCamera != null ? fromCamera.transform.rotation : rotation;
            firstPerson.SetActive(true);
            for (float t = 0f; t < sitDuration; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, sitDuration));
                view.SetPositionAndRotation(Vector3.Lerp(origin, destination, blend),
                    Quaternion.Slerp(originRotation, rotation, blend));
                yield return null;
            }
            view.SetPositionAndRotation(destination, rotation);
            joe.SetActive(true);
            conversation.wrapMode = WrapMode.ClampForever;
            conversation.Play();
            if (conversationAudio != null) conversationAudio.Play();
        }

        private void OnDestroy()
        {
            if (!started) return;
            if (motor != null) motor.SetControlLocked(false);
            if (look != null) look.PoseLocked = false;
            if (interactor != null) interactor.enabled = true;
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
            foreach (var camera in playerCameras) if (camera != null) camera.enabled = true;
            foreach (var listener in playerListeners) if (listener != null) listener.enabled = true;
        }

#if UNITY_EDITOR
        [ContextMenu("Preview seated conversation (Play Mode)")]
        private void PreviewConversation()
        {
            if (!Application.isPlaying || !CanInteract) return;
            var actor = GameObject.FindGameObjectWithTag("Player");
            if (actor != null) Interact(actor);
        }
#endif
    }
}
