using System.Collections;
using GameLimon.Companions;
using GameLimon.Core;
using GameLimon.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

namespace GameLimon.Story
{
    /// <summary>
    /// Scene 4 cinematic: trigger -> dialogue -> branch A -> shared finale.
    /// The scene composition lives in A1_S04_UnderBridge; this component only drives
    /// the flow and swaps staging groups. Timeline/Cinemachine handle all camera work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RustamBridgeCutscene : SceneCutscene
    {
        private const string PlayerTag = "Player";

        [Header("Timelines")]
        [SerializeField] private PlayableDirector introDirector;
        [SerializeField] private PlayableDirector branchADirector;
        [SerializeField] private PlayableDirector aftermathDirector;
        [SerializeField] private PlayableDirector finaleDirector;

        [Header("Staging marks")]
        [SerializeField] private Transform playerIntroMark;
        [SerializeField] private Transform playerFinalMark;
        [SerializeField] private Transform joeIntroMark;

        [Header("Standing cast")]
        [SerializeField] private GameObject rustamStanding;
        [SerializeField] private GameObject thugAStanding;
        [SerializeField] private GameObject thugBStanding;
        [SerializeField] private GameObject banditStanding;
        [SerializeField] private GameObject pistolInThugHand;

        [Header("Aftermath bodies")]
        [SerializeField] private GameObject rustamBody;
        [SerializeField] private GameObject thugABody;
        [SerializeField] private GameObject thugBBody;
        [SerializeField] private GameObject[] aftermathBlood;

        [Header("Finale")]
        [SerializeField] private GameObject finaleProps;

        [Header("Objects hidden for the duration")]
        [SerializeField] private GameObject[] hideDuringCutscene;

        [Header("Dialogue")]
        [SerializeField] private BridgeDialogueTalk talk;

        [Header("Idle animations (before the shootout)")]
        [SerializeField] private BridgeIdleActor[] idleActors;

        [Header("Audio")]
        [SerializeField] private AudioSource cutsceneAudio;

        [Header("Dialogue speech")]
        [SerializeField] private AudioClip speechClip;
        [SerializeField, Range(0f, 1f)] private float speechVolume = 0.32f;

        [Header("Shootout and aftermath audio")]
        [SerializeField] private AudioClip shootClip;
        [SerializeField] private AudioClip ringingClip;
        [SerializeField] private AudioClip heartbeatClip;
        [SerializeField, Range(0f, 1f)] private float shootVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float ringingVolume = 0.4f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.7f;
        [SerializeField, Min(1f)] private float shootoutDuration = 3.6f;
        [SerializeField, Min(0f)] private float aftermathFadeIn = 1.8f;
        [SerializeField, Min(0f)] private float aftermathFadeOut = 2.4f;

        private CutsceneOverlayUI _ui;
        private GameObject _player;
        private FirstPersonBody _playerBody;
        private GameObject _joe;
        private CompanionActor _joeCompanion;
        private CinemachineCamera[] _shotCameras;
        private string _desiredShot;
        private AudioSource _shootSourceA;
        private CutsceneChoice _branchChoice;
        private bool _thugsEscaped;
        private AudioSource _shootSourceB;
        private AudioSource _shootSourceC;
        private AudioSource _ringingSource;
        private AudioSource _heartbeatSource;
        private AudioSource _speechSource;
        private Coroutine _speechRoutine;

        private GameObject[] _fallActors;
        private Vector3[] _fallPositions;
        private Quaternion[] _fallRotations;

        private void Awake()
        {
            EnsureAudio();
        }

        protected override void PrepareCutscene()
        {
            base.PrepareCutscene();

            _ui = CutsceneOverlayUI.Create();
            _ui.HideAll();

            _player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (_player != null)
            {
                _playerBody = _player.GetComponentInChildren<FirstPersonBody>();
                if (_playerBody != null)
                    _playerBody.enabled = false;

                // FirstPersonBody normally hides the head for the FPS view. The
                // cutscene needs the real head for the final close-up. Only the
                // active body variant matters — other bodies stay disabled.
                Transform body = _playerBody != null ? _playerBody.transform : _player.transform;
                RestoreHeadScale(body);
                StopLocomotion(body);

                if (playerIntroMark != null)
                    _player.transform.SetPositionAndRotation(playerIntroMark.position, playerIntroMark.rotation);
            }

            _joe = FindObjectByName("Joe");
            if (_joe != null)
            {
                // Напарника НЕ глушим: катсцена теперь стартует сразу, и Джо
                // спокойно доходит до своей точки уже внутри интро (движение
                // и анимация шага продолжают работать).
                _joeCompanion = _joe.GetComponent<CompanionActor>();
            }

            RestoreStandingPoses();
            CaptureStandingPoses();
            SetStandingCast(true);
            SetAftermath(false, true);
            SetHiddenState(true);
            _thugsEscaped = false;
            _branchChoice = CutsceneChoice.Wait;
            _shotCameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
            _desiredShot = null;

            // Timeline-треки камер перехватывают CinemachineBrain и держат
            // первый шот на весь клип (у интро это 11 с) — из-за этого камера
            // «зависала». Отвязываем камерные треки: планы переключаем сами
            // через приоритеты, а анимация/атмосфера таймлайнов продолжают играть.
            DetachCameraTracks(introDirector);
            DetachCameraTracks(branchADirector);
            DetachCameraTracks(aftermathDirector);
            DetachCameraTracks(finaleDirector);

            if (finaleProps != null)
                finaleProps.SetActive(false);

            Debug.Log("[BridgeCutscene] prepared");
        }

        private void Update()
        {
            // Timeline-треки камер при остановке возвращают приоритеты Cinemachine
            // к состоянию на момент запуска и «откатывают» вручную выбранный план.
            // Пока катсцена играет, удерживаем нужный шот каждый кадр.
            if (IsPlaying && _desiredShot != null)
                ApplyShot(_desiredShot);
        }

        protected override IEnumerator PlayContent()
        {
            StartCoroutine(EnsureLetterbox());
            yield return new WaitForSecondsRealtime(0.2f);

            yield return PlayIntro();

            yield return BranchWait();

            yield return PlayFinale();

            // Ringing and heartbeat fade out during the final shot.
            StartCoroutine(FadeLoop(_ringingSource, 0f, aftermathFadeOut));
            yield return FadeLoop(_heartbeatSource, 0f, aftermathFadeOut);

            _ui.HideAll();
            _ui.DestroyOverlay();
            _ui = null;
            Debug.Log("[BridgeCutscene] content finished");
        }

        private IEnumerator EnsureLetterbox()
        {
            if (_ui == null) yield break;
            yield return _ui.SetLetterbox(true, 0.5f);
        }

        private IEnumerator PlayIntro()
        {
            SetIdleActors(true);
            FocusCamera("CM_Shot_Approach");
            PlayTimeline(introDirector);

            yield return PlayTalkLine("Джо", "Тихо. Смотри туда.", 40f, 1.2f);

            // [Камера поворачивается в сторону закутка у стены.]
            // Камерные треки таймлайнов отвязаны в PrepareCutscene, поэтому
            // план переключается сразу по репликам.
            FocusCamera("CM_Shot_Nook");
            yield return PlayTalkLine("Рустам", "Я вам все рассказал еще тогда! Оставьте меня в покое!", 45f, 0.9f);
            yield return PlayTalkLine("Бандит", "Не все, старик. Ты знаешь, кого мы ищем. Где он?", 45f, 0.9f);

            FocusCamera("CM_Shot_Approach");
            yield return PlayTalkLine("Джо", "Ждем. Узнаем, что им нужно.", 40f, 1.3f);
            Debug.Log("[BridgeCutscene] intro finished");
        }

        private IEnumerator BranchWait()
        {
            FocusCamera("CM_Shot_Wait");
            PlayTimeline(branchADirector);
            yield return PlayTalkLine("Джо", "Спокойно. Пусть выговорятся.", 40f, 0.9f);

            // Выбор по сценарию: [Ждать] / [Действовать], 5 секунд, авто-выбор «Ждать».
            _branchChoice = CutsceneChoice.Wait;
            yield return _ui.ShowChoice("Что делать?", 5f, c => _branchChoice = c);

            if (_branchChoice == CutsceneChoice.Act)
                yield return PlayActBranch();
            else
                yield return PlayWaitBranch();

            yield return PlayAftermath();

            yield return PlayTalkLine("Рустам", "Слушай... тумбочка... у кровати... там подарок от твоего отца... посмотри...", 42f, 1.4f);
            yield return PlayTalkLine("Джо", _thugsEscaped
                ? "ГГ... ГГ! Иди сюда... Быстрее!"
                : "ГГ! Сюда! Быстрее!", 40f, 1.0f);
        }

        /// <summary>Ветка А «Ждать»: бандит нервничает, Рустам кричит, Джо командует.</summary>
        private IEnumerator PlayWaitBranch()
        {
            // Бандит с пистолетом начинает нервничать.
            yield return new WaitForSecondsRealtime(1.4f);
            yield return PlayTalkLine("Бандит", "У меня нет времени. Говори, или я те колено прострелю!", 48f, 0.7f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return PlayTalkLine("Рустам", "Не-ет! Подожди! Я не знаю...", 42f, 0.5f);

            // Рустам кричит — Джо подаёт команду.
            yield return PlayTalkLine("Рустам", "А-а-а!", 70f, 0.3f);
            yield return PlayTalkLine("Джо", "Сейчас!", 40f, 0.3f);
            yield return PlayTalkLine("ГГ и Джо", "ПОЛИЦИЯ! БРОСАЙТЕ ОРУЖИЕ!", 65f, 0.3f);

            _thugsEscaped = false;
            yield return GunfireAndFade();
        }

        /// <summary>Ветка Б «Действовать»: ГГ вырывается и спугивает бандитов — они уходят живыми.</summary>
        private IEnumerator PlayActBranch()
        {
            _thugsEscaped = true;

            // ГГ дёргается вперёд — резкий короткий «толчок» камеры.
            yield return CameraFlinch();

            yield return PlayTalkLine("Джо", "Стой, придурок! У них ствол!", 48f, 0.4f);

            // QTE: жать кнопку, чтобы вырваться из хватки Джо.
            bool brokeFree = false;
            yield return _ui.RunQte("Вырвись!", 3f, 6, ok => brokeFree = ok);
            if (!brokeFree)
                yield return new WaitForSecondsRealtime(0.8f); // Вырывается с трудом — финал тот же.

            yield return PlayTalkLine("ГГ", "ПОЛИЦИЯ! ОТПУСТИ ЕГО!", 60f, 0.4f);

            yield return GunfireAndFade();

            // По сценарию ветки Б бандиты уходят живыми.
            yield return PlayTalkLine("Джо", "Твою мать! Они ушли! Ты их спугнул!", 45f, 1.0f);
        }

        /// <summary>Резкий короткий «дёрг» камеры — небольшая анимация рывка ГГ.</summary>
        private IEnumerator CameraFlinch()
        {
            if (_ui != null)
                StartCoroutine(_ui.Flash(0.25f));

            Camera camera = Camera.main;
            if (camera == null)
                yield break;

            float baseFov = camera.fieldOfView;
            const float duration = 0.24f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                camera.fieldOfView = baseFov + Mathf.Sin(t / duration * Mathf.PI) * 6f;
                yield return null;
            }
            camera.fieldOfView = baseFov;
        }

        private IEnumerator GunfireAndFade()
        {
            Debug.Log("[BridgeCutscene] gunfire");
            SetIdleActors(false);
            StopDirector(introDirector);
            StopDirector(branchADirector);

            GameRoot root = GameRoot.Instance;

            // Immediate blackout and gunfire instead of the white flash.
            StartCoroutine(PlayShootout());
            if (root != null && root.UI != null)
                yield return root.UI.FadeTo(1f, 0.3f);
            else
                yield return new WaitForSecondsRealtime(0.3f);

            float shootoutEnd = Time.unscaledTime + Mathf.Max(1f, shootoutDuration);
            while (Time.unscaledTime < shootoutEnd)
                yield return null;

            // Ringing and heartbeat fade in and keep playing until the end of the scene.
            StartCoroutine(FadeLoop(_ringingSource, ringingVolume, aftermathFadeIn));
            StartCoroutine(FadeLoop(_heartbeatSource, heartbeatVolume, aftermathFadeIn));

            // The same characters lie down at the body marks while the screen is black.
            LayDownAfterShot();

            if (pistolInThugHand != null)
                pistolInThugHand.SetActive(false);

            yield return new WaitForSecondsRealtime(0.5f);

            if (root != null && root.UI != null)
                yield return root.UI.FadeTo(0f, 1.0f);

            yield return new WaitForSecondsRealtime(0.4f);
        }

        private IEnumerator PlayAftermath()
        {
            if (aftermathDirector != null)
            {
                FocusCamera("CM_Shot_Aftermath");
                PlayTimeline(aftermathDirector);
                yield return new WaitForSecondsRealtime(1.0f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.0f);
            }
        }

        private IEnumerator PlayFinale()
        {
            if (finaleProps != null)
                finaleProps.SetActive(true);

            if (_player != null && playerFinalMark != null)
                _player.transform.SetPositionAndRotation(playerFinalMark.position, playerFinalMark.rotation);

            // Joe остаётся у JoeIntro — в финальном кадре он за камерой.
            // Раньше его переносили за спину ГГ, из-за чего в конце
            // маршрута был заметный скачок.

            Debug.Log("[BridgeCutscene] finale");
            SetLightEnabled("RimLight_Warm", false);
            FocusCamera("CM_Shot_Finale");
            PlayTimeline(finaleDirector);
            yield return WaitForTimeline(finaleDirector, 12.0f);

            // Финальный кадр — игрушка. Удерживаем камеру последнего шота,
            // иначе Cinemachine вернёт приоритеты к состоянию до Timeline.
            FocusCamera("CM_Shot_Finale");
            yield return _ui.SetLetterbox(false, 0.6f);
        }

        private void PlayTimeline(PlayableDirector director)
        {
            if (director == null)
                return;

            StopDirector(introDirector);
            StopDirector(branchADirector);
            StopDirector(aftermathDirector);
            StopDirector(finaleDirector);

            director.time = 0;
            director.Play();
        }

        private static void StopDirector(PlayableDirector director)
        {
            if (director != null && director.state == PlayState.Playing)
                director.Stop();
        }

        /// <summary>
        /// Keep the last shot alive after a Timeline stops. Without this the
        /// brain falls back to the highest-priority camera, which used to be
        /// the opening shot.
        /// </summary>
        private void FocusCamera(string cameraName)
        {
            _desiredShot = cameraName;
            ApplyShot(cameraName);
        }

        private void ApplyShot(string cameraName)
        {
            if (_shotCameras == null || _shotCameras.Length == 0)
                _shotCameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);

            foreach (CinemachineCamera camera in _shotCameras)
                if (camera != null)
                    camera.Priority = camera.name == cameraName ? 100 : 0;
        }

        /// <summary>
        /// Отвязывает камерный трек таймлайна от CinemachineBrain. При пустом
        /// биндинге CinemachineMixer.ProcessFrame просто ничего не делает —
        /// камеры остаются под нашим управлением, а остальные треки работают.
        /// </summary>
        private static void DetachCameraTracks(PlayableDirector director)
        {
            if (director == null || director.playableAsset == null)
                return;

            foreach (PlayableBinding output in director.playableAsset.outputs)
            {
                if (output.outputTargetType == typeof(CinemachineBrain) && output.sourceObject != null)
                    director.SetGenericBinding(output.sourceObject, null);
            }
        }

        private static void SetLightEnabled(string lightName, bool enabled)
        {
            var go = GameObject.Find(lightName);
            var light = go != null ? go.GetComponent<Light>() : null;
            if (light != null) light.enabled = enabled;
        }

        private IEnumerator WaitForTimeline(PlayableDirector director, float fallbackHold = 0f)
        {
            if (director == null)
            {
                if (fallbackHold > 0f)
                    yield return new WaitForSecondsRealtime(fallbackHold);
                yield break;
            }

            // Ждём не «пока director Playing», а не дольше длительности клипа:
            // битый или зацикленный трек иначе держит катсцену вечно и она
            // никогда не доходит до затемнения.
            float duration = director.duration > 0.0 ? (float)director.duration : 0f;
            float limit = Mathf.Max(fallbackHold, duration + 2f);
            float end = Time.unscaledTime + limit;

            while (director.state == PlayState.Playing && Time.unscaledTime < end)
                yield return null;

            if (director.state == PlayState.Playing)
            {
                Debug.LogWarning("[BridgeCutscene] Timeline не завершился вовремя — останавливаю принудительно.");
                director.Stop();
            }

            // Не ждём лишний кадр: при остановке Timeline Cinemachine возвращает
            // приоритеты камер к состоянию на момент запуска, и без немедленного
            // FocusCamera следующего шота камера на мгновение «прыгает» назад.
        }

        private GameObject ResolveActor(GameObject assigned, string fallbackName)
        {
            if (assigned != null)
                return assigned;

            return FindObjectByName(fallbackName);
        }

        private GameObject ResolveBandit() => ResolveActor(banditStanding, "Modern Rebel Soldier Character");

        private GameObject ResolveThugA() => ResolveActor(thugAStanding, "ThugA_Standing");

        /// <summary>Запоминает исходные позы стоящих персонажей перед катсценой.</summary>
        private void CaptureStandingPoses()
        {
            _fallActors = new[] { rustamStanding, ResolveThugA(), ResolveBandit() };
            _fallPositions = new Vector3[_fallActors.Length];
            _fallRotations = new Quaternion[_fallActors.Length];

            for (int i = 0; i < _fallActors.Length; i++)
            {
                if (_fallActors[i] == null)
                    continue;

                _fallPositions[i] = _fallActors[i].transform.position;
                _fallRotations[i] = _fallActors[i].transform.rotation;
            }
        }

        /// <summary>Возвращает персонажей в исходную позу (при повторном запуске катсцены).</summary>
        private void RestoreStandingPoses()
        {
            if (_fallActors == null || _fallPositions == null || _fallRotations == null)
                return;

            for (int i = 0; i < _fallActors.Length; i++)
            {
                if (_fallActors[i] == null)
                    continue;

                _fallActors[i].transform.SetPositionAndRotation(_fallPositions[i], _fallRotations[i]);
            }
        }

        /// <summary>
        /// После выстрела укладывает тех же персонажей на землю: берём точку и
        /// поворот у соответствующих *_Body, затем прижимаем модель к поверхности.
        /// </summary>
        private void LayDownAfterShot()
        {
            LayDown(rustamStanding, rustamBody);

            if (_thugsEscaped)
            {
                // Ветка Б: бандиты ушли живыми — их тел на полу нет.
                if (thugAStanding != null) thugAStanding.SetActive(false);
                if (thugABody != null) thugABody.SetActive(false);

                GameObject bandit = ResolveBandit();
                if (bandit != null) bandit.SetActive(false);
                if (thugBBody != null) thugBBody.SetActive(false);

                if (pistolInThugHand != null)
                    pistolInThugHand.SetActive(false);
            }
            else
            {
                LayDown(ResolveThugA(), thugABody);
                LayDown(ResolveBandit(), thugBBody);
            }

            SetAftermath(false, !_thugsEscaped);
            PlaceBloodOnGround();
        }

        private static void LayDown(GameObject actor, GameObject bodyMark)
        {
            if (actor == null)
                return;

            actor.SetActive(true);
            if (bodyMark == null)
                return;

            Transform tr = actor.transform;

            // Маркеры тел повёрнуты «лицом вниз» (X = 90). Для трупа нужен
            // поворот «лицом вверх» (X = -90), иначе руки в A-позе уходят
            // под землю, а торс висит в воздухе.
            float yaw = bodyMark.transform.eulerAngles.y;
            tr.SetPositionAndRotation(bodyMark.transform.position, Quaternion.Euler(-90f, yaw, 0f));

            if (!TryGetGround(tr.position, out Vector3 groundPoint, out _))
                return;

            // У разных FBX разные пивоты (у части моделей — ступни, у части —
            // бёдра). Выравниваем по кости бёдер: её центр должен оказаться
            // примерно на половине толщины тела над землёй.
            Transform hips = FindHips(actor);
            if (hips == null)
                return;

            tr.position += Vector3.up * (groundPoint.y + 0.17f - hips.position.y);
        }

        private static Transform FindHips(GameObject actor)
        {
            foreach (Transform part in actor.GetComponentsInChildren<Transform>(true))
                if (part.name == "mixamorig:Hips")
                    return part;

            return null;
        }

        /// <summary>
        /// Включает пятна крови и укладывает их на землю: позиция — верхняя поверхность
        /// под точкой, поворот — по нормали (с случайным поворотом вокруг нормали).
        /// </summary>
        private void PlaceBloodOnGround()
        {
            if (aftermathBlood == null)
                return;

            foreach (GameObject target in aftermathBlood)
            {
                if (target == null)
                    continue;

                Transform mark = target.transform;
                target.SetActive(true);

                if (!TryGetGround(mark.position, out Vector3 groundPoint, out Vector3 normal))
                    continue;

                // Пятно — плоская Plane-декаль с нормалью +Y. Поворачиваем
                // только вокруг нормали, чтобы не поставить её на ребро.
                mark.SetPositionAndRotation(
                    groundPoint + normal * 0.02f,
                    Quaternion.FromToRotation(Vector3.up, normal)
                    * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        /// <summary>
        /// Ищет поверхность пола под точкой (коллайдеры, затем террейн).
        /// Луч стартует чуть выше маркера и игнорирует геометрию над ним
        /// (например, коллайдер моста), иначе тела и кровь «улетают» наверх.
        /// </summary>
        private static bool TryGetGround(Vector3 worldPosition, out Vector3 point, out Vector3 normal)
        {
            point = worldPosition;
            normal = Vector3.up;
            bool found = false;
            float bestY = float.NegativeInfinity;

            // Маркеры тел/крови стоят на 2–50 см выше пола. Всё, что выше
            // этого допуска, — потолок/мост/декор над персонажем.
            float maxFloorY = worldPosition.y + 0.35f;

            RaycastHit[] hits = Physics.RaycastAll(
                worldPosition + Vector3.up * 2f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.point.y > maxFloorY)
                    continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    point = hit.point;
                    normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : Vector3.up;
                    found = true;
                }
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
                if (y <= maxFloorY && (!found || y > bestY))
                {
                    point = new Vector3(worldPosition.x, y, worldPosition.z);
                    normal = Vector3.up;
                    found = true;
                }
            }

            return found;
        }

        private void SetStandingCast(bool active)
        {
            if (rustamStanding != null) rustamStanding.SetActive(active);
            if (thugAStanding != null) thugAStanding.SetActive(active);
            if (thugBStanding != null) thugBStanding.SetActive(active);

            // В сцене третий бандит лежит отдельным объектом с именем
            // "Modern Rebel Soldier Character"; ссылка может быть не задана.
            GameObject bandit = ResolveBandit();
            if (bandit != null) bandit.SetActive(active);

            if (pistolInThugHand != null) pistolInThugHand.SetActive(active);
        }

        private void SetIdleActors(bool active)
        {
            if (idleActors == null)
                return;

            foreach (BridgeIdleActor idle in idleActors)
            {
                if (idle == null)
                    continue;

                if (active)
                    idle.Play();
                else
                    idle.Stop();
            }
        }

        private IEnumerator PlayTalkLine(string speaker, string text, float speed, float hold)
        {
            float lineDuration = EstimateLineDuration(text, speed, hold);

            if (talk != null)
                talk.Speak(speaker, lineDuration);

            PlaySpeech(speaker, lineDuration);

            yield return _ui.PlayLine(speaker, text, speed, hold);

            if (talk != null)
                talk.Silence();
        }

        private void PlaySpeech(string speaker, float duration)
        {
            if (_speechSource == null || _speechSource.clip == null)
                return;

            StopSpeech();
            _speechRoutine = StartCoroutine(SpeechRoutine(speaker, duration));
        }

        private IEnumerator SpeechRoutine(string speaker, float duration)
        {
            _speechSource.pitch = SpeechPitch(speaker);
            float length = _speechSource.clip.length;
            _speechSource.time = Random.Range(0f, Mathf.Max(0.01f, length - 0.05f));
            _speechSource.volume = 0f;
            _speechSource.Play();

            const float fadeIn = 0.15f;
            const float fadeOut = 0.25f;
            float holdEnd = Mathf.Max(fadeIn, duration - fadeOut);

            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                _speechSource.volume = Mathf.Lerp(0f, speechVolume, t / fadeIn);
                yield return null;
            }

            _speechSource.volume = speechVolume;

            float elapsed = fadeIn;
            while (elapsed < holdEnd)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
            {
                _speechSource.volume = Mathf.Lerp(speechVolume, 0f, t / fadeOut);
                yield return null;
            }

            _speechSource.volume = 0f;
            _speechSource.Stop();
            _speechRoutine = null;
        }

        private void StopSpeech()
        {
            if (_speechRoutine != null)
            {
                StopCoroutine(_speechRoutine);
                _speechRoutine = null;
            }

            if (_speechSource != null)
            {
                _speechSource.Stop();
                _speechSource.volume = 0f;
            }
        }

        private static float SpeechPitch(string speaker)
        {
            switch (speaker)
            {
                case "\u0420\u0443\u0441\u0442\u0430\u043c": return 0.82f;
                case "\u0411\u0430\u043d\u0434\u0438\u0442": return 1.14f;
                case "\u0413\u0413 \u0438 \u0414\u0436\u043e": return 1.06f;
                default: return 1f;
            }
        }

        private static float EstimateLineDuration(string text, float speed, float hold) =>
            Mathf.Max(0.4f, text.Length / Mathf.Max(1f, speed)) + hold;

        private void SetAftermath(bool active, bool includeThugs)
        {
            if (rustamBody != null) rustamBody.SetActive(active);
            if (thugABody != null) thugABody.SetActive(active && includeThugs);
            if (thugBBody != null) thugBBody.SetActive(active && includeThugs);

            if (aftermathBlood != null)
            {
                foreach (GameObject target in aftermathBlood)
                    if (target != null) target.SetActive(active);
            }
        }

        private void SetHiddenState(bool hidden)
        {
            if (hideDuringCutscene == null)
                return;

            foreach (GameObject target in hideDuringCutscene)
                if (target != null) target.SetActive(!hidden);
        }

        protected override void RestorePlayerState()
        {
            SetLightEnabled("RimLight_Warm", true);
            SetHiddenState(false);

            if (_playerBody != null)
                _playerBody.enabled = true;

            if (_joeCompanion != null)
                _joeCompanion.enabled = true;

            base.RestorePlayerState();
        }

        private void OnDisable()
        {
            SetIdleActors(false);
            StopAftermathAudio();
            StopSpeech();

            if (_ui != null)
                _ui.HideAll();
        }

        private void EnsureAudio()
        {
            if (cutsceneAudio == null)
                cutsceneAudio = GetComponent<AudioSource>();

            if (cutsceneAudio == null)
                cutsceneAudio = gameObject.AddComponent<AudioSource>();

            cutsceneAudio.playOnAwake = false;
            cutsceneAudio.loop = false;
            cutsceneAudio.spatialBlend = 0f;
            cutsceneAudio.volume = 1f;

            if (_shootSourceA == null) _shootSourceA = CreateAudioSource("ShootA", 0.93f, false, null, 1f);
            if (_shootSourceB == null) _shootSourceB = CreateAudioSource("ShootB", 1f, false, null, 1f);
            if (_shootSourceC == null) _shootSourceC = CreateAudioSource("ShootC", 1.07f, false, null, 1f);
            if (_ringingSource == null) _ringingSource = CreateAudioSource("EarRinging", 1f, true, ringingClip, 0f);
            if (_heartbeatSource == null) _heartbeatSource = CreateAudioSource("Heartbeat", 1f, true, heartbeatClip, 0f);
            if (_speechSource == null) _speechSource = CreateAudioSource("DialogueSpeech", 1f, true, speechClip, 0f);
        }

        private AudioSource CreateAudioSource(string name, float pitch, bool loop, AudioClip clip, float volume)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);

            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.pitch = pitch;
            source.clip = clip;
            source.volume = volume;
            return source;
        }

        private IEnumerator PlayShootout()
        {
            float end = Time.unscaledTime + Mathf.Max(1f, shootoutDuration);
            int shot = 0;

            while (Time.unscaledTime < end)
            {
                PlayShoot(shot++);
                yield return new WaitForSecondsRealtime(Random.Range(0.07f, 0.26f));
            }
        }

        private void PlayShoot(int index)
        {
            AudioSource source;
            if (index % 3 == 0) source = _shootSourceA;
            else if (index % 3 == 1) source = _shootSourceB;
            else source = _shootSourceC;

            if (source == null || shootClip == null)
                return;

            source.PlayOneShot(shootClip, shootVolume * Random.Range(0.7f, 1f));
        }

        private static IEnumerator FadeLoop(AudioSource source, float target, float duration)
        {
            if (source == null)
                yield break;

            if (target > 0.001f && !source.isPlaying && source.clip != null)
                source.Play();

            float start = source.volume;

            if (duration <= 0f)
            {
                source.volume = target;
            }
            else
            {
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    source.volume = Mathf.Lerp(start, target, t / duration);
                    yield return null;
                }

                source.volume = target;
            }

            if (target <= 0.001f)
                source.Stop();
        }

        private void StopAftermathAudio()
        {
            if (_ringingSource != null)
            {
                _ringingSource.Stop();
                _ringingSource.volume = 0f;
            }

            if (_heartbeatSource != null)
            {
                _heartbeatSource.Stop();
                _heartbeatSource.volume = 0f;
            }
        }

        private static void StopLocomotion(Transform root)
        {
            var animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
            if (animator == null) return;
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("PlaybackSpeed", 1f);
        }

        private static void RestoreHeadScale(Transform player)
        {
            // FirstPersonBody скрывает и голову, и шею — для катсцены
            // возвращаем обе кости, иначе в крупном плане не будет головы.
            Transform[] all = player.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in all)
            {
                if (candidate.name == "mixamorig:Head" || candidate.name.EndsWith(":Head") ||
                    candidate.name == "mixamorig:Neck" || candidate.name.EndsWith(":Neck"))
                {
                    candidate.localScale = Vector3.one;
                }
            }
        }

        private static GameObject FindObjectByName(string targetName)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in all)
                    if (candidate.name == targetName)
                        return candidate.gameObject;
            }

            return null;
        }
    }
}
