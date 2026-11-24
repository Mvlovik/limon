using System.Collections;
using GameLimon.Core;
using GameLimon.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GameLimon.Story
{
    public sealed class Act2Scene3Controller : MonoBehaviour
    {
        private enum Phase { Thought, FirstKnock, SecondKnock, Dialogue, Dark, PhotoSlide, Photo, Ending }

        private readonly string[] dialogue =
        {
            "Рита: Уходи.", "Дима: Рита. Мне нужен Рустам.", "Рита: ...Ему уже ничего не нужно.",
            "Дима: Я был там. Я не успел.", "Рита: Я знаю, где ты был. Мне сказали. Ты стоял и смотрел.",
            "Дима: Открой.", "Рита: Я не могу на тебя смотреть.",
            "Рита: Я его просила. Не связывайся, говорю, с этим человеком. Не надо нам этого всего. Он не слушал.",
            "Дима: С каким человеком, Рита?", "Рита: Он просил отдать это тебе, если что-то случится. Я думала, он шутит.",
            "Дима: Рита...", "Рита: Ты знал, кем был твой отец?", "Рита: Посмотри. И больше не приходи."
        };

        [SerializeField] private Texture2D photo;
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField] private AudioClip footstepsClip;
        [SerializeField] private AudioClip knockClip;
        [SerializeField] private AudioClip cryingClip;
        [SerializeField] private AudioClip ritaVoiceClip;
        [SerializeField] private AudioClip dimaVoiceClip;
        [SerializeField] private GameObject handPrefab;

        private Phase phase = Phase.Thought;
        private InputReader input;
        private PlayerMotor motor;
        private PlayerLook look;
        private AudioSource voice;
        private ThoughtUI thoughtUI;
        private Transform door;
        private Quaternion doorClosed;
        private float photoSlide;
        private GameObject handInstance;
        private GameObject photoObject;
        private GameObject playerObject;
        private bool interactPressed;
        private int lineIndex;
        private float phaseStarted;
        private int ritaLine;
        private int inspectedZones;
        private readonly bool[] visited = new bool[4];
        private string thought;

        private IEnumerator Start()
        {
            GameObject player = null;
            for (int i = 0; i < 120 && player == null; i++)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) yield return null;
            }
            if (player != null)
            {
                playerObject = player;
                input = GameRoot.Instance != null ? GameRoot.Instance.Input : FindFirstObjectByType<InputReader>();
                motor = player.GetComponent<PlayerMotor>();
                look = player.GetComponent<PlayerLook>();
                BindPlayerView(player);
                SetControl(true);
                StartCoroutine(ApproachDoor(player));
            }
            if (input != null) input.InteractPressed += OnInteract;
            if (GameRoot.Instance != null && GameRoot.Instance.UI != null)
                thoughtUI = ThoughtUI.GetOrCreate(GameRoot.Instance.UI.Canvas);
            door = FindDoor();
            if (door != null) doorClosed = door.localRotation;
            AddSource(ambienceClip, 0.45f, true);
            voice = AddSource(null, 0.65f, false);
            StartCoroutine(PlayIntroFootsteps());
            thought = "Я сам сказал ей запереться. Даже от меня.";
            thoughtUI?.ShowImmediate(thought);
            phaseStarted = Time.unscaledTime;
        }

        private IEnumerator ApproachDoor(GameObject player)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            Vector3 start = player.transform.position;
            Quaternion startRotation = player.transform.rotation;
            Vector3 target = start;
            Quaternion targetRotation = startRotation;
            if (door != null)
            {
                Vector3 offset = player.transform.position - door.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < 0.01f) offset = -door.forward;
                offset.Normalize();
                target = door.position + offset * 1.35f;
                target.y = start.y;
                Vector3 look = door.position - target;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) targetRotation = Quaternion.LookRotation(look, Vector3.up);
            }
            for (float t = 0f; t < 1.8f; t += Time.unscaledDeltaTime)
            {
                float amount = Mathf.SmoothStep(0f, 1f, t / 1.8f);
                CharacterController controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.transform.position = Vector3.Lerp(start, target, amount);
                player.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, amount);
                if (controller != null) controller.enabled = true;
                yield return null;
            }
            phase = Phase.FirstKnock;
            phaseStarted = Time.unscaledTime;
            thoughtUI?.Hide();
        }

        private IEnumerator PlayIntroFootsteps()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            OneShot(footstepsClip);
        }

        private void BindPlayerView(GameObject player)
        {
            PlayerSpawnPoint spawn = FindFirstObjectByType<PlayerSpawnPoint>();
            CharacterController controller = player.GetComponent<CharacterController>();
            if (spawn != null)
            {
                if (controller != null) controller.enabled = false;
                player.transform.SetPositionAndRotation(spawn.Position, Quaternion.Euler(0f, spawn.Rotation.eulerAngles.y, 0f));
                if (controller != null) controller.enabled = true;
            }

            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (camera == null || camera.gameObject.scene != gameObject.scene) continue;
                camera.enabled = false;
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }

            Camera[] playerCameras = player.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in playerCameras)
            {
                camera.enabled = true;
                camera.tag = "MainCamera";
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }

            if (playerCameras.Length == 0)
            {
                Camera camera = player.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.gameObject.AddComponent<AudioListener>();
            }
        }

        private AudioSource AddSource(AudioClip clip, float volume, bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            if (clip != null) source.Play();
            return source;
        }

        private void Update()
        {
            bool interact = interactPressed;
            interactPressed = false;
            if (phase == Phase.Thought && Time.unscaledTime - phaseStarted >= 1.5f)
            {
                phase = Phase.FirstKnock;
                thoughtUI?.Hide();
                phaseStarted = Time.unscaledTime;
            }
            else if (phase == Phase.FirstKnock && interact)
            {
                OneShot(knockClip);
                StartCoroutine(AnimateDoor(0.12f, 0.45f));
                phase = Phase.SecondKnock;
                phaseStarted = Time.unscaledTime;
            }
            else if (phase == Phase.SecondKnock && Time.unscaledTime - phaseStarted >= 3f && interact)
            {
                OneShot(knockClip);
                phase = Phase.Dialogue;
                lineIndex = 0;
                SetControl(true);
                ShowDialogueLine();
                PlayVoice();
            }
            else if (phase == Phase.Dialogue && interact)
            {
                lineIndex++;
                if (lineIndex >= dialogue.Length)
                {
                    phase = Phase.Dark;
                    thoughtUI?.Hide();
                    OneShot(cryingClip);
                    GameRoot.Instance?.UI?.FadeTo(1f, 0.8f);
                    StartCoroutine(ShowPhotoAfterDark());
                }
                else
                {
                    ShowDialogueLine();
                    PlayVoice();
                }
            }
            else if (phase == Phase.PhotoSlide)
            {
                photoSlide = Mathf.MoveTowards(photoSlide, 1f, Time.unscaledDeltaTime * 0.5f);
                if (interact && photoSlide >= 1f)
                {
                    phase = Phase.Photo;
                    CreateHeldPhoto();
                    thoughtUI?.ShowImmediate(thought);
                }
            }
            else if (phase == Phase.Photo) UpdatePhotoInspection();
        }

        private IEnumerator ShowPhotoAfterDark()
        {
            yield return new WaitForSecondsRealtime(2f);
            phase = Phase.PhotoSlide;
            photoSlide = 0f;
            GameRoot.Instance?.UI?.FadeTo(0f, 0.8f);
        }

        private void UpdatePhotoInspection()
        {
            Rect rect = PhotoRect();
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Vector2 point = new Vector2(mouse.x, Screen.height - mouse.y);
            if (!rect.Contains(point)) return;
            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, point.x);
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, point.y);
            int zone = x < 0.34f ? 0 : x < 0.67f ? 1 : y > 0.52f ? 2 : 3;
            if (visited[zone]) return;
            visited[zone] = true;
            if (zone > 0) inspectedZones++;
            if (zone == 1) thought = "Рустам. Молодой. Он тут...";
            if (zone == 2) thought = "Отец... Он моложе, чем я сейчас.";
            if (zone == 3) thought = "А это кто?..";
            if (inspectedZones >= 3) StartCoroutine(FinishPhoto());
        }

        private IEnumerator FinishPhoto()
        {
            phase = Phase.Ending;
            yield return new WaitForSecondsRealtime(5f);
            if (GameRoot.Instance != null && GameRoot.Instance.UI != null)
                yield return GameRoot.Instance.UI.FadeTo(1f, 1.2f);
            GameRoot.Instance?.UI?.ShowLoading("Загрузка");
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        private void PlayVoice()
        {
            if (voice == null) return;
            bool isRita = lineIndex % 2 == 0;
            voice.Stop();
            voice.clip = isRita ? ritaVoiceClip : dimaVoiceClip;
            if (voice.clip == null) return;
            voice.loop = isRita;
            voice.pitch = 1f;
            voice.panStereo = 0f;
            voice.volume = isRita ? 0.46f : 0.65f;
            if (!isRita)
            {
                voice.Play();
                return;
            }

            float[] offsets = { .01f, .49f, .24f, .76f, .12f, .62f, .36f, .87f };
            float offset = offsets[ritaLine++ % offsets.Length];
            voice.timeSamples = Mathf.FloorToInt(voice.clip.samples * offset);
            double start = AudioSettings.dspTime + .025;
            float duration = Mathf.Max(.38f, (dialogue[lineIndex].Length - 5f) / 27f);
            voice.PlayScheduled(start);
            voice.SetScheduledEndTime(start + duration);
        }

        private void CreateHeldPhoto()
        {
            if (playerObject == null) return;
            Camera camera = playerObject.GetComponentInChildren<Camera>(true);
            if (camera == null) return;
            if (handPrefab != null)
            {
                handInstance = Instantiate(handPrefab, camera.transform);
                handInstance.name = "PhotoHoldingHand";
                handInstance.transform.localPosition = new Vector3(0.36f, -0.78f, 0.48f);
                handInstance.transform.localRotation = Quaternion.Euler(8f, 180f, -8f);
                handInstance.SetActive(true);
            }
            if (photo != null)
            {
                photoObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                photoObject.name = "HeldPhoto";
                photoObject.transform.SetParent(camera.transform, false);
                photoObject.transform.localPosition = new Vector3(0.12f, -0.62f, 0.62f);
                photoObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                photoObject.transform.localScale = new Vector3(0.34f, 0.48f, 1f);
                Collider collider = photoObject.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                Shader shader = Shader.Find("Unlit/Texture");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.mainTexture = photo;
                    photoObject.GetComponent<Renderer>().material = material;
                }
            }
            StartCoroutine(AnimatePickup());
        }

        private IEnumerator AnimatePickup()
        {
            Vector3 handTarget = new Vector3(0.36f, -0.34f, 0.48f);
            Vector3 photoTarget = new Vector3(0.12f, -0.2f, 0.62f);
            Vector3 handStart = handInstance != null ? handInstance.transform.localPosition : handTarget;
            Vector3 photoStart = photoObject != null ? photoObject.transform.localPosition : photoTarget;
            for (float t = 0f; t < 0.65f; t += Time.unscaledDeltaTime)
            {
                float amount = Mathf.SmoothStep(0f, 1f, t / 0.65f);
                if (handInstance != null) handInstance.transform.localPosition = Vector3.Lerp(handStart, handTarget, amount);
                if (photoObject != null) photoObject.transform.localPosition = Vector3.Lerp(photoStart, photoTarget, amount);
                yield return null;
            }
        }

        private void ShowDialogueLine()
        {
            thoughtUI?.ShowImmediate(dialogue[lineIndex]);
        }

        private IEnumerator AnimateDoor(float angle, float duration)
        {
            if (door == null) yield break;
            Quaternion target = doorClosed * Quaternion.Euler(0f, angle, 0f);
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                door.localRotation = Quaternion.Slerp(doorClosed, target, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            door.localRotation = target;
        }

        private Transform FindDoor()
        {
            GameObject hinge = GameObject.Find("EntranceDoorHinge");
            if (hinge != null) return hinge.transform;
            GameObject namedDoor = GameObject.Find("Door and conversation");
            return namedDoor != null ? namedDoor.transform : null;
        }

        private void OneShot(AudioClip clip)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, 0.8f);
        }

        private void SetControl(bool locked)
        {
            if (motor != null) motor.SetControlLocked(locked);
            if (look != null) look.PoseLocked = locked;
        }

        private void OnInteract() => interactPressed = true;

        private Rect PhotoRect()
        {
            float height = Screen.height * 0.7f;
            float width = photo != null ? height * photo.width / photo.height : Screen.width * 0.7f;
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void OnGUI()
        {
            GUIStyle text = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUIStyle box = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            if (phase == Phase.FirstKnock) GUI.Label(new Rect(0f, Screen.height * .84f, Screen.width, 40f), "E — постучать", text);
            if (phase == Phase.SecondKnock) GUI.Label(new Rect(0f, Screen.height * .84f, Screen.width, 40f), Time.unscaledTime - phaseStarted >= 3f ? "E — постучать ещё раз" : "...", text);
            if (phase == Phase.Dialogue)
            {
                GUI.Label(new Rect(0f, Screen.height * .87f, Screen.width, 35f), "E — продолжить", text);
            }
            if (phase == Phase.PhotoSlide)
            {
                Rect full = PhotoRect();
                Rect slide = new Rect(full.x, Mathf.Lerp(Screen.height * .84f, full.y, photoSlide), full.width, full.height * 0.3f);
                if (photo != null) GUI.DrawTexture(slide, photo, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(0f, Screen.height * .87f, Screen.width, 35f), photoSlide >= 1f ? "E — взять фотографию" : "", text);
            }
            if (phase == Phase.Photo)
            {
                if (photoObject == null && photo != null) GUI.DrawTexture(PhotoRect(), photo, ScaleMode.ScaleToFit);
            }
            if (phase == Phase.Ending) GUI.Label(new Rect(0f, Screen.height * .42f, Screen.width, 70f), "Акт 2 — конец", text);
        }

        private void OnDisable()
        {
            if (input != null) input.InteractPressed -= OnInteract;
            SetControl(false);
        }
    }
}
