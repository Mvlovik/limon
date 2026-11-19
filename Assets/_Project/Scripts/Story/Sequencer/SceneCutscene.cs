using System.Collections;
using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Player;
using GameLimon.UI;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Reusable in-scene cutscene presentation.
    /// Handles the same flow as the police-station conversation:
    /// player lock, camera handoff, optional legacy animation/audio, optional
    /// thoughts, UI hiding, fade and scene completion.
    /// Scenes only configure the references; no per-scene cutscene code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneCutscene : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera cutsceneCamera;
        [Tooltip("Camera blend from the player view to the cutscene camera.")]
        [SerializeField] private float cameraBlendDuration = 0.65f;

        [Header("Scene objects")]
        [Tooltip("Objects enabled only while the cutscene is playing (body, characters, props).")]
        [SerializeField] private GameObject[] enableDuringCutscene;

        [Header("Optional legacy animation")]
        [Tooltip("Optional legacy Animation to play. Leave empty for camera-only cutscenes.")]
        [SerializeField] private Animation legacyAnimation;
        [SerializeField] private AudioSource legacyAudio;

        [Header("Content")]
        [Tooltip("Optional thoughts/dialogue shown during the cutscene.")]
        [SerializeField] private ThoughtSequence sequence;
        [Tooltip("How long the cutscene camera holds after the content (or on its own).")]
        [SerializeField] private float holdDuration = 3f;

        [Header("Finish")]
        [SerializeField] private bool completeSceneWhenFinished = true;
        [SerializeField] private float endHoldDuration = 0.6f;
        [SerializeField] private float fadeOutDuration = 1f;
        [Tooltip("Restore player controls/camera after the cutscene. Recommended even when the scene completes.")]
        [SerializeField] private bool restorePlayerAfterCutscene = true;

        public bool IsPlaying { get; private set; }

        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInteractor _interactor;
        private readonly List<Camera> _playerCameras = new List<Camera>();
        private readonly List<AudioListener> _playerListeners = new List<AudioListener>();
        private readonly List<bool> _cameraStates = new List<bool>();
        private readonly List<bool> _listenerStates = new List<bool>();
        private readonly List<GameObject> _enabledObjects = new List<GameObject>();
        private readonly List<bool> _objectStates = new List<bool>();

        private GameObject _interactionPrompt;
        private bool _promptWasActive;
        private bool _motorWasLocked;
        private bool _lookWasLocked;
        private bool _interactorWasEnabled;
        private bool _captured;

        public void Play()
        {
            if (!IsPlaying)
                StartCoroutine(PlayRoutine());
        }

        public IEnumerator PlayRoutine()
        {
            if (IsPlaying)
                yield break;

            if (cutsceneCamera == null)
            {
                Debug.LogWarning("[SceneCutscene] Cutscene camera is not assigned.", this);
                yield break;
            }

            IsPlaying = true;
            CapturePlayerState();
            PrepareCutscene();

            Camera fromCamera = _playerCameras.Count > 0 ? _playerCameras[0] : null;

            Vector3 destination = cutsceneCamera.transform.position;
            Quaternion destinationRotation = cutsceneCamera.transform.rotation;

            cutsceneCamera.gameObject.SetActive(true);
            cutsceneCamera.enabled = true;

            AudioListener cutsceneListener = cutsceneCamera.GetComponent<AudioListener>();
            if (cutsceneListener != null)
                cutsceneListener.enabled = true;

            if (fromCamera != null && cameraBlendDuration > 0f)
            {
                cutsceneCamera.transform.SetPositionAndRotation(
                    fromCamera.transform.position, fromCamera.transform.rotation);

                for (float t = 0f; t < cameraBlendDuration; t += Time.deltaTime)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, t / cameraBlendDuration);
                    cutsceneCamera.transform.SetPositionAndRotation(
                        Vector3.Lerp(fromCamera.transform.position, destination, blend),
                        Quaternion.Slerp(fromCamera.transform.rotation, destinationRotation, blend));
                    yield return null;
                }
            }

            cutsceneCamera.transform.SetPositionAndRotation(destination, destinationRotation);

            yield return PlayContent();

            if (endHoldDuration > 0f)
                yield return new WaitForSeconds(endHoldDuration);

            if (completeSceneWhenFinished)
            {
                GameRoot root = GameRoot.Instance;
                if (root != null && root.UI != null && fadeOutDuration > 0f)
                    yield return root.UI.FadeTo(1f, fadeOutDuration);
                else if (root != null && root.UI != null)
                    root.UI.SetFade(1f);

                if (root != null && root.Flow != null)
                    root.Flow.CompleteCurrentScene();
            }

            if (restorePlayerAfterCutscene)
                RestorePlayerState();

            IsPlaying = false;
        }

        private IEnumerator PlayContent()
        {
            if (sequence != null)
                yield return StoryDirector.Ensure().PlayThoughtSequence(sequence);

            if (legacyAnimation != null && legacyAnimation.clip != null)
            {
                legacyAnimation.wrapMode = WrapMode.ClampForever;
                legacyAnimation.Play();
                if (legacyAudio != null)
                    legacyAudio.Play();

                yield return new WaitForSeconds(legacyAnimation.clip.length);
            }

            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);
        }

        private void CapturePlayerState()
        {
            _captured = false;
            _playerCameras.Clear();
            _playerListeners.Clear();
            _cameraStates.Clear();
            _listenerStates.Clear();
            _enabledObjects.Clear();
            _objectStates.Clear();

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _motor = player.GetComponent<PlayerMotor>();
                _look = player.GetComponent<PlayerLook>();
                _interactor = player.GetComponent<PlayerInteractor>();

                foreach (Camera camera in player.GetComponentsInChildren<Camera>(true))
                {
                    if (camera == null || !camera.enabled) continue;
                    _playerCameras.Add(camera);
                    _cameraStates.Add(camera.enabled);
                }

                foreach (AudioListener listener in player.GetComponentsInChildren<AudioListener>(true))
                {
                    if (listener == null || !listener.enabled) continue;
                    _playerListeners.Add(listener);
                    _listenerStates.Add(listener.enabled);
                }
            }

            _motorWasLocked = _motor != null && _motor.ControlLocked;
            _lookWasLocked = _look != null && _look.PoseLocked;
            _interactorWasEnabled = _interactor != null && _interactor.enabled;

            _interactionPrompt = null;
            _promptWasActive = false;
            Canvas canvas = GameRoot.Instance != null && GameRoot.Instance.UI != null
                ? GameRoot.Instance.UI.Canvas
                : null;
            Transform prompt = canvas != null ? canvas.transform.Find("InteractionPrompt") : null;
            if (prompt != null)
            {
                _interactionPrompt = prompt.gameObject;
                _promptWasActive = prompt.gameObject.activeSelf;
            }

            _captured = true;
        }

        private void PrepareCutscene()
        {
            if (_motor != null)
            {
                _motor.ResetMomentum();
                _motor.SetControlLocked(true);
            }

            if (_look != null)
                _look.PoseLocked = true;

            if (_interactor != null)
                _interactor.enabled = false;

            ObjectiveUI.Instance?.Hide();

            if (_interactionPrompt != null && _interactionPrompt.activeSelf)
                _interactionPrompt.SetActive(false);

            foreach (Camera camera in _playerCameras)
                if (camera != null) camera.enabled = false;

            foreach (AudioListener listener in _playerListeners)
                if (listener != null) listener.enabled = false;

            if (enableDuringCutscene != null)
            {
                foreach (GameObject target in enableDuringCutscene)
                {
                    if (target == null) continue;
                    _enabledObjects.Add(target);
                    _objectStates.Add(target.activeSelf);
                    target.SetActive(true);
                }
            }
        }

        private void RestorePlayerState()
        {
            if (!_captured)
                return;

            if (cutsceneCamera != null)
            {
                cutsceneCamera.enabled = false;
                AudioListener cutsceneListener = cutsceneCamera.GetComponent<AudioListener>();
                if (cutsceneListener != null)
                    cutsceneListener.enabled = false;
            }

            for (int i = 0; i < _playerCameras.Count; i++)
                if (_playerCameras[i] != null) _playerCameras[i].enabled = _cameraStates[i];

            for (int i = 0; i < _playerListeners.Count; i++)
                if (_playerListeners[i] != null) _playerListeners[i].enabled = _listenerStates[i];

            for (int i = 0; i < _enabledObjects.Count; i++)
                if (_enabledObjects[i] != null) _enabledObjects[i].SetActive(_objectStates[i]);

            if (_interactor != null)
                _interactor.enabled = _interactorWasEnabled;

            if (_look != null)
            {
                _look.PoseLocked = _lookWasLocked;
                if (!_lookWasLocked)
                    _look.ResyncPose();
            }

            if (_motor != null)
            {
                _motor.SetControlLocked(_motorWasLocked);
                _motor.ResetMomentum();
            }

            if (_interactionPrompt != null)
                _interactionPrompt.SetActive(_promptWasActive);

            _captured = false;
        }

        private void OnDisable()
        {
            if (IsPlaying)
            {
                StopAllCoroutines();
                RestorePlayerState();
                IsPlaying = false;
            }
        }
    }
}
