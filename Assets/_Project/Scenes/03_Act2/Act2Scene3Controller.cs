using System.Collections;
using GameLimon.Core;
using GameLimon.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GameLimon.Story
{
    public sealed class Act2Scene3Controller : MonoBehaviour
    {
        private readonly struct Line
        {
            public readonly bool Rita;
            public readonly string Text;
            public readonly float VoiceStart;
            public readonly float PauseAfter;

            public Line(bool rita, string text, float voiceStart, float pauseAfter = .15f)
            {
                Rita = rita;
                Text = text;
                VoiceStart = voiceStart;
                PauseAfter = pauseAfter;
            }
        }

        private static readonly Line[] Dialogue =
        {
            new Line(true, "Уходи.", .9f),
            new Line(false, "Рита. Мне нужен Рустам.", .3f, 1.05f),
            new Line(true, "…Ему уже ничего не нужно.", 3.9f),
            new Line(false, "Я был там. Я не успел.", 1.2f),
            new Line(true, "Я знаю, где ты был. Мне сказали. Ты стоял и смотрел.", 5.7f),
            new Line(false, "Открой.", 2.4f),
            new Line(true, "Я не могу на тебя смотреть.", 7.8f),
            new Line(true, "Я его просила. Не связывайся, говорю, с этим человеком. Не надо нам этого всего. Он не слушал.", 10.5f),
            new Line(false, "С каким человеком, Рита?", .6f, 1.2f),
            new Line(true, "Он просил отдать это тебе, если что-то случится. Я думала, он шутит.", 1.8f),
            new Line(false, "Рита…", 1.2f),
            new Line(true, "Ты знал, кем был твой отец?", 6.9f),
            new Line(true, "Посмотри. И больше не приходи.", 8.7f)
        };

        [SerializeField] private PlayerSpawnPoint doorView;
        [FormerlySerializedAs("landingLight")]
        [SerializeField] private Light entranceLight;
        [SerializeField] private Transform photo;
        [SerializeField] private Transform photoEnd;
        [SerializeField] private AudioSource doorSound;
        [SerializeField] private AudioSource hallwaySound;
        [SerializeField] private AudioSource stepEcho;
        [SerializeField] private AudioSource ritaVoice;
        [SerializeField] private AudioSource dimaVoice;
        [SerializeField] private AudioClip footsteps;
        [SerializeField] private AudioClip dogBark;
        [SerializeField] private AudioClip knock;
        [SerializeField] private AudioClip rustle;
        [SerializeField] private AudioClip crying;
        [SerializeField] private Texture2D endingImage;

        [Header("Фотография дома")]
        [SerializeField] private GameObject[] exterior;
        [SerializeField] private GameObject apartment;
        [SerializeField] private Transform apartmentView;
        [SerializeField] private Transform heldPhoto;
        [SerializeField] private Vector2 photoSize = new Vector2(.47f, .302f);
        [SerializeField] private AudioSource finalMusic;

        private const int FatherZone = 1;
        private static readonly Rect[] PhotoZones =
        {
            new Rect(.520f, .609f, .063f, .082f),
            new Rect(.612f, .64f, .061f, .084f),
            new Rect(.886f, .444f, .072f, .108f)
        };
        private static readonly string[] PhotoThoughts =
        {
            "Рустам. Молодой. Он тут…",
            "Отец… Он моложе, чем я сейчас.",
            "А это кто?.."
        };

        private CharacterController _seatedController;
        private bool _controllerWasEnabled;
        private float _outdoorFov;
        private Vector3 _heldPosition;
        private Camera _inspectionView;
        private int _inspectedPhotoZones;
        private int _pendingPhotoZone = -1;
        private int _hoveredPhotoZone = -1;
        private GameObject _photoOverlay;
        private Text[] _photoMarkers;
        private Text _photoHint;

        private GameRoot _root;
        private InputReader _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private PlayerInteractor _interactor;
        private Transform _camera;
        private ThoughtUI _captions;
        private Text _prompt;
        private Vector3 _eyePosition;
        private float _viewPitch = 12f;
        private bool _waitingForKnock;
        private bool _knockPressed;
        private bool _interactorWasEnabled;
        private FirstPersonBody[] _bodies;
        private CanvasGroup _blackout;

        private IEnumerator Start()
        {
            _root = GameRoot.Instance;
            if (_root == null || _root.UI == null) yield break;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) yield break;
            BindPlayer(player);
            _input = _root.Input;
            if (_input != null) _input.InteractPressed += OnInteract;

            _captions = ThoughtUI.GetOrCreate(_root.UI.Canvas);
            _captions?.Hide();
            _prompt = CreatePrompt();
            _blackout = CreateBlackout();

            StartCoroutine(FlickerLamp());
            hallwaySound.PlayOneShot(footsteps, .6f);
            yield return new WaitForSecondsRealtime(.085f);
            stepEcho.PlayOneShot(footsteps, .23f);
            yield return new WaitForSecondsRealtime(.47f);
            hallwaySound.PlayOneShot(dogBark, .44f);
            yield return new WaitForSecondsRealtime(.65f);
            yield return FadeCard(_blackout, 0f, 1.05f);

            _captions?.ShowImmediate("Я сам сказал ей запереться. Даже от меня.");
            yield return new WaitForSecondsRealtime(2.6f);
            _captions?.Hide();

            yield return WaitForKnock("E — постучать");
            doorSound.PlayOneShot(knock, .75f);
            yield return new WaitForSecondsRealtime(1.65f);
            yield return WaitForKnock("E — постучать ещё раз");
            doorSound.PlayOneShot(knock, .75f);
            yield return new WaitForSecondsRealtime(.72f);

            for (int i = 0; i < Dialogue.Length; i++)
            {
                Line line = Dialogue[i];
                string speaker = line.Rita ? "Рита" : "Дима";
                _captions?.ShowImmediate($"{speaker}: {line.Text}");
                float duration = Mathf.Max(1.45f, .55f + line.Text.Length / 20f);
                AudioSource voice = line.Rita ? ritaVoice : dimaVoice;
                PlayVoice(voice, line.VoiceStart, duration);
                yield return new WaitForSecondsRealtime(duration);
                voice.Stop();
                _captions?.Hide();
                yield return new WaitForSecondsRealtime(line.PauseAfter);

                if (i == 8)
                {
                    doorSound.PlayOneShot(rustle, .55f);
                    yield return new WaitForSecondsRealtime(.7f);
                }
            }

            yield return SlidePhoto();
            doorSound.clip = crying;
            doorSound.volume = .31f;
            doorSound.loop = true;
            doorSound.Play();
            yield return new WaitForSecondsRealtime(.65f);
            yield return FadeCard(_blackout, 1f, 2.1f);
            yield return new WaitForSecondsRealtime(2f);
            doorSound.Stop();

            yield return InspectPhotoAtHome(player);

            CanvasGroup card = CreateEndingCard();
            yield return FadeCard(card, 1f, .85f);
            yield return new WaitForSecondsRealtime(EndingHoldSeconds);
            yield return FadeCard(card, 0f, 1.25f);
            _root.Flow?.CompleteCurrentScene();
        }


        private IEnumerator InspectPhotoAtHome(GameObject player)
        {
            foreach (GameObject item in exterior) item.SetActive(false);
            apartment.SetActive(true);

            _seatedController = player.GetComponent<CharacterController>();
            if (_seatedController != null)
            {
                _controllerWasEnabled = _seatedController.enabled;
                _seatedController.enabled = false;
            }
            player.transform.SetPositionAndRotation(apartmentView.position, apartmentView.rotation);
            _eyePosition = Vector3.zero;
            _viewPitch = 15f;
            var view = _camera.GetComponent<Camera>();
            _outdoorFov = view.fieldOfView;
            view.fieldOfView = 63f;
            _heldPosition = new Vector3(0f, -.025f, .43f);
            heldPhoto.gameObject.SetActive(true);
            UpdateCameraPose();

            finalMusic.volume = 0f;
            finalMusic.Play();
            StartCoroutine(RaiseMusic());
            yield return FadeCard(_blackout, 0f, 2.2f);
            _look.LockCursor(false);
            CreatePhotoMarkers();

            int allZones = (1 << PhotoZones.Length) - 1;
            while (_inspectedPhotoZones != allZones)
            {
                _inspectionView = view;
                yield return new WaitUntil(() => _pendingPhotoZone >= 0);
                int zone = _pendingPhotoZone;
                _pendingPhotoZone = -1;
                _inspectionView = null;
                _hoveredPhotoZone = -1;
                _inspectedPhotoZones |= 1 << zone;
                if (zone == FatherZone)
                    yield return FocusOnFather(view, Mouse.current);

                _captions?.ShowImmediate(PhotoThoughts[zone]);
                yield return new WaitForSecondsRealtime(zone == FatherZone ? 3.1f : 2.35f);
                _captions?.Hide();
                if (zone == FatherZone)
                    yield return ZoomPhoto(view, 63f, .65f, null);
            }
            _look.LockCursor(true);
            Destroy(_photoOverlay);
            _photoOverlay = null;

            Vector3 start = _heldPosition;
            float pitch = _viewPitch;
            for (float elapsed = 0f; elapsed < 2.5f; elapsed += Time.unscaledDeltaTime)
            {
                _heldPosition = Vector3.Lerp(start, start + Vector3.down * .56f,
                    Mathf.SmoothStep(0f, 1f, elapsed / 1.85f));
                _viewPitch = Mathf.Lerp(pitch, -24f,
                    Mathf.SmoothStep(0f, 1f, (elapsed - .48f) / 2.02f));
                yield return null;
            }
            heldPhoto.gameObject.SetActive(false);
            yield return FadeCard(_blackout, 1f, 2.7f);

            var title = Stretch(_blackout.transform.parent, "Act 2 end title").gameObject.AddComponent<Text>();
            ThoughtUI.StyleCaption(title);
            title.text = "Акт 2 - конец.";
            title.fontSize = 44;
            var titleOpacity = title.gameObject.AddComponent<CanvasGroup>();
            titleOpacity.alpha = 0f;
            yield return FadeCard(titleOpacity, 1f, 1.15f);
            yield return new WaitForSecondsRealtime(4f);
            yield return FadeCard(titleOpacity, 0f, .8f);
        }

        private IEnumerator RaiseMusic()
        {
            const float seconds = 8f;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                finalMusic.volume = Mathf.SmoothStep(0f, .65f, elapsed / seconds);
                yield return null;
            }
            finalMusic.volume = .65f;
        }

        private int GetPhotoZone(Camera view, Vector2 screenPosition)
        {
            Ray ray = view.ScreenPointToRay(screenPosition);
            var plane = new Plane(-heldPhoto.forward, heldPhoto.position);
            if (!plane.Raycast(ray, out float distance)) return -1;
            Vector3 local = heldPhoto.InverseTransformPoint(ray.GetPoint(distance));
            var uv = new Vector2(local.x / photoSize.x + .5f, local.y / photoSize.y + .5f);
            for (int i = 0; i < PhotoZones.Length; i++)
                if (PhotoZones[i].Contains(uv)) return i;
            return -1;
        }

        private IEnumerator FocusOnFather(Camera view, Mouse mouse) =>
            ZoomPhoto(view, 48f, .55f, mouse);

        private IEnumerator ZoomPhoto(Camera view, float targetFov, float duration, Mouse stickyCursor)
        {
            float original = view.fieldOfView;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                view.fieldOfView = Mathf.Lerp(original, targetFov, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                if (stickyCursor != null)
                {
                    Vector3 point = PhotoPoint(PhotoZones[FatherZone].center);
                    stickyCursor.WarpCursorPosition(view.WorldToScreenPoint(point));
                }
                yield return null;
            }
            view.fieldOfView = targetFov;
        }


        private void BindPlayer(GameObject player)
        {
            _motor = player.GetComponent<PlayerMotor>();
            _look = player.GetComponent<PlayerLook>();
            _interactor = player.GetComponent<PlayerInteractor>();
            _bodies = player.GetComponentsInChildren<FirstPersonBody>(true);
            foreach (FirstPersonBody body in _bodies) body.HideForCutscene();

            if (_motor != null)
            {
                _motor.SetControlLocked(true);
                _motor.ResetMomentum();
            }
            if (_look != null)
            {
                _look.PoseLocked = true;
                _look.LockCursor(true);
            }
            _interactorWasEnabled = _interactor != null && _interactor.enabled;
            if (_interactor != null) _interactor.enabled = false;

            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                _camera = camera.transform;
                camera.enabled = true;
                var listener = camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
            _eyePosition = new Vector3(0f, _motor != null ? _motor.StandEyeHeight : 1.62f, .09f);
            var controller = player.GetComponent<CharacterController>();
            bool controllerEnabled = controller != null && controller.enabled;
            if (controllerEnabled) controller.enabled = false;
            player.transform.SetPositionAndRotation(doorView.Position, doorView.Rotation);
            if (controllerEnabled) controller.enabled = true;
            UpdateCameraPose();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (_inspectionView == null || mouse == null) return;

            UpdatePhotoHover(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame)
                SelectPhotoZone();
        }

        private void UpdatePhotoHover(Vector2 position)
        {
            int zone = GetPhotoZone(_inspectionView, position);
            _hoveredPhotoZone = zone >= 0 && (_inspectedPhotoZones & (1 << zone)) == 0 ? zone : -1;
        }

        private void SelectPhotoZone()
        {
            if (_inspectionView != null && _pendingPhotoZone < 0)
                _pendingPhotoZone = _hoveredPhotoZone;
        }

        private void LateUpdate()
        {
            UpdateCameraPose();
            UpdatePhotoMarkers();
        }

        private void UpdateCameraPose()
        {
            if (_camera == null) return;
            float time = Time.unscaledTime;
            float breath = Mathf.Sin(time * 2.15f + .2f * Mathf.Sin(time * .73f));
            _camera.localPosition = _eyePosition + Vector3.up * (.021f * breath);
            _camera.localRotation = Quaternion.Euler(_viewPitch + .43f * breath, 0f, 0f);
            if (heldPhoto.gameObject.activeSelf)
            {
                Vector3 pose = _heldPosition + Vector3.up * (.005f * breath);
                Quaternion tilt = Quaternion.Euler(.3f * breath, 0f, .6f * breath);
                heldPhoto.SetPositionAndRotation(_camera.TransformPoint(pose), _camera.rotation * tilt);
            }
        }

        private IEnumerator WaitForKnock(string message)
        {
            _knockPressed = false;
            _waitingForKnock = true;
            _prompt.text = message;
            yield return new WaitUntil(() => _knockPressed);
            _waitingForKnock = false;
            _prompt.text = string.Empty;
        }

        private void OnInteract()
        {
            if (_waitingForKnock)
                _knockPressed = true;
            else
                SelectPhotoZone();
        }

        private void PlayVoice(AudioSource voice, float startSeconds, float duration)
        {
            if (voice.clip == null) return;
            voice.loop = true;
            voice.timeSamples = Mathf.Min(Mathf.RoundToInt(startSeconds * voice.clip.frequency), voice.clip.samples - 1);
            double start = AudioSettings.dspTime + .02;
            voice.PlayScheduled(start);
            voice.SetScheduledEndTime(start + Mathf.Max(.3f, duration - .12f));
        }

        private IEnumerator FlickerLamp()
        {
            float normal = entranceLight.intensity;
            while (true)
            {
                yield return new WaitForSecondsRealtime(Random.Range(6f, 11f));
                entranceLight.intensity = normal * .16f;
                yield return new WaitForSecondsRealtime(.07f);
                entranceLight.intensity = normal;
            }
        }

        private IEnumerator SlidePhoto()
        {
            Vector3 start = photo.position;
            photo.gameObject.SetActive(true);
            doorSound.PlayOneShot(rustle, .44f);
            for (float elapsed = 0f; elapsed < 2f; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / 2f);
                Vector3 position = Vector3.Lerp(start, photoEnd.position, progress);
                // Опускаем фотографию только после того, как она сходит с порога.
                position.y = Mathf.Lerp(start.y, photoEnd.position.y,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.8f, 1f, progress)));
                photo.position = position;
                yield return null;
            }
            photo.position = photoEnd.position;
        }


        private void CreatePhotoMarkers()
        {
            _photoOverlay = new GameObject("Photo inspection markers", typeof(RectTransform), typeof(Canvas));
            _photoOverlay.transform.SetParent(transform, false);
            Canvas canvas = _photoOverlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3100;

            _photoMarkers = new Text[PhotoZones.Length];
            for (int i = 0; i < _photoMarkers.Length; i++)
                _photoMarkers[i] = CreatePhotoLabel("Photo marker " + (i + 1), "○", new Vector2(34f, 34f));

            _photoHint = CreatePhotoLabel("Photo inspect prompt", "Осмотреть", new Vector2(180f, 32f));
        }

        private Text CreatePhotoLabel(string name, string message, Vector2 size)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            host.transform.SetParent(_photoOverlay.transform, false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = size;
            var text = host.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            host.SetActive(false);
            return text;
        }

        private void UpdatePhotoMarkers()
        {
            if (_photoOverlay == null) return;

            var view = _camera.GetComponent<Camera>();
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .024f), 15, 27);
            for (int i = 0; i < _photoMarkers.Length; i++)
            {
                Text ring = _photoMarkers[i];
                bool visible = (_inspectedPhotoZones & (1 << i)) == 0;
                ring.gameObject.SetActive(visible);
                if (!visible) continue;

                ring.rectTransform.anchoredPosition = view.WorldToScreenPoint(PhotoPoint(PhotoZones[i].center));
                ring.fontSize = fontSize;
                ring.color = i == _hoveredPhotoZone ? Color.white : new Color(1f, .96f, .85f, .72f);
            }

            bool showHint = _inspectionView != null && _hoveredPhotoZone >= 0;
            _photoHint.gameObject.SetActive(showHint);
            if (showHint)
            {
                _photoHint.fontSize = Mathf.Max(13, fontSize - 5);
                _photoHint.rectTransform.anchoredPosition =
                    _photoMarkers[_hoveredPhotoZone].rectTransform.anchoredPosition + Vector2.up * (fontSize + 4f);
            }
        }

        private Vector3 PhotoPoint(Vector2 uv) =>
            heldPhoto.TransformPoint((uv.x - .5f) * photoSize.x, (uv.y - .5f) * photoSize.y, 0f);

        private Text CreatePrompt()
        {
            var rect = Stretch(_root.UI.Canvas.transform, "RitaKnockPrompt");
            rect.anchorMin = new Vector2(.2f, .12f);
            rect.anchorMax = new Vector2(.8f, .20f);
            var label = rect.gameObject.AddComponent<Text>();
            ThoughtUI.StyleCaption(label);
            return label;
        }

        private CanvasGroup CreateBlackout()
        {
            var screen = new GameObject("RitaBlackout", typeof(RectTransform), typeof(Canvas));
            screen.transform.SetParent(transform, false);
            var canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4999;
            var black = Stretch(screen.transform, "Black");
            black.gameObject.AddComponent<Image>().color = Color.black;
            return black.gameObject.AddComponent<CanvasGroup>();
        }

        private void OnDestroy()
        {
            if (_input != null) _input.InteractPressed -= OnInteract;
            if (_prompt != null) Destroy(_prompt.gameObject);
            _captions?.Hide();
            if (_motor != null) _motor.SetControlLocked(false);
            if (_look != null)
            {
                _look.PoseLocked = false;
                _look.LockCursor(true);
            }
            if (_seatedController != null) _seatedController.enabled = _controllerWasEnabled;
            if (_camera != null && _outdoorFov > 0f) _camera.GetComponent<Camera>().fieldOfView = _outdoorFov;
            if (_interactor != null) _interactor.enabled = _interactorWasEnabled;
            if (_bodies != null)
                foreach (FirstPersonBody body in _bodies)
                    if (body != null) body.RestoreAfterCutscene();
        }

        private const string EndingText =
            "Представленная версия игры является прототипом. Некоторые механики и запланированный контент " +
            "не были реализованы в рамках отведённого времени.\n\nСпасибо за внимание и прохождение!";
        private const float EndingHoldSeconds = 30f;

        private CanvasGroup CreateEndingCard()
        {
            var screen = new GameObject("PrototypeEnding", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            screen.transform.SetParent(transform, false);
            var canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = screen.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            var black = Stretch(screen.transform, "Black");
            black.gameObject.AddComponent<Image>().color = Color.black;
            var content = Stretch(screen.transform, "EndingContent");
            var group = content.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            if (endingImage != null)
            {
                var photo = Stretch(content, "StreetPhoto");
                photo.gameObject.AddComponent<RawImage>().texture = endingImage;
                var fitter = photo.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)endingImage.width / endingImage.height;
            }

            var caption = Stretch(content, "Message");
            caption.anchorMin = new Vector2(.15f, .2f);
            caption.anchorMax = new Vector2(.85f, .8f);
            var label = caption.gameObject.AddComponent<Text>();
            label.text = EndingText;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 42;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 24;
            label.resizeTextMaxSize = 42;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            var shadow = caption.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .95f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return group;
        }

        private static RectTransform Stretch(Transform parent, string objectName)
        {
            var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static IEnumerator FadeCard(CanvasGroup card, float target, float duration)
        {
            float start = card.alpha;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                card.alpha = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            card.alpha = target;
        }
    }
}
