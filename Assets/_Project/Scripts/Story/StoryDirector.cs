using System.Collections;
using System.Collections.Generic;
using GameLimon.Audio;
using GameLimon.Core;
using GameLimon.UI;
using UnityEngine;

namespace GameLimon.Story
{
    /// <summary>
    /// Сервис сюжетных действий. Проигрывет списки StoryAction по очереди
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryDirector : MonoBehaviour
    {
        public static StoryDirector Instance { get; private set; }

        [Header("Мысли")]
        [Tooltip("Скорость печати текста, символов в секунду.")]
        [SerializeField] private float charsPerSecond = 32f;

        [Tooltip("Длительность появления/исчезновения панели мыслей, сек.")]
        [SerializeField] private float thoughtFade = 0.25f;

        private struct Request
        {
            public IReadOnlyList<StoryAction> Actions;
            public ThoughtSequence Sequence;
            public ThoughtLine Line;
            public Vector3 Position;
        }

        private readonly Queue<Request> _queue = new Queue<Request>();

        private ThoughtUI _thoughtUI;
        private AudioSource _sfx2D;
        private AudioSource _voice;
        private Coroutine _worker;

        public bool IsBusy => _worker != null;

        /// <summary>Найти или создать сервис (переживает смену сцен)</summary>
        public static StoryDirector Ensure()
        {
            if (Instance != null) return Instance;

            var host = new GameObject("[StoryDirector]");
            host.AddComponent<StoryDirector>();
            DontDestroyOnLoad(host);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfx2D = CreateSource("StorySfx", false);
            _voice = CreateSource("ThoughtVoice", false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);


            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Проиграть список действий</summary>
        public void Run(IReadOnlyList<StoryAction> actions, Vector3 position = default)
        {
            if (actions == null || actions.Count == 0) return;

            _queue.Enqueue(new Request { Actions = actions, Position = position });
            EnsureWorker();
        }

        /// <summary>Проиграть действия и дождаться, пока очередь опустеет.</summary>
        public IEnumerator RunBlocking(IReadOnlyList<StoryAction> actions, Vector3 position = default)
        {
            Run(actions, position);

            while (IsBusy)
                yield return null;
        }

        /// <summary>Проиграть последовательность мыслей</summary>
        public void Play(ThoughtSequence sequence)
        {
            if (sequence == null || sequence.Count == 0)
                return;

            _queue.Enqueue(new Request { Sequence = sequence });
            EnsureWorker();
        }

        /// <summary>Проиграть одну мысль</summary>
        public void PlayLine(ThoughtLine line)
        {
            if (line == null)
                return;

            _queue.Enqueue(new Request { Line = line });
            EnsureWorker();
        }

        /// <summary>Оборвать всё: очередь, текущее действие, панель мыслей</summary>
        public void Stop()
        {
            StopAllCoroutines();
            _worker = null;
            _queue.Clear();

            if (_thoughtUI != null) _thoughtUI.Hide();
        }

        private void EnsureWorker()
        {
            if (_worker == null) _worker = StartCoroutine(WorkerRoutine());
        }

        private IEnumerator WorkerRoutine()
        {
            while (_queue.Count > 0)
            {
                Request request = _queue.Dequeue();

                if (request.Sequence != null)
                {
                    yield return PlaySequence(request.Sequence);
                    continue;
                }

                if (request.Line != null)
                {
                    yield return PlayLineRoutine(request.Line);
                    continue;
                }
                yield return RunActions(request.Actions, request.Position);
            }
            _worker = null;
        }

        private IEnumerator RunActions(IReadOnlyList<StoryAction> actions, Vector3 position)
        {
            GameRoot root = GameRoot.Instance;

            for (int i = 0; i < actions.Count; i++)
            {
                StoryAction action = actions[i];
                if (action == null) continue;

                switch (action.Type)
                {
                    case StoryActionType.Wait:
                        if (action.Duration > 0f)
                            yield return new WaitForSeconds(action.Duration);
                        break;

                    case StoryActionType.SetFlag:
                    case StoryActionType.ClearFlag:
                        if (root != null && root.State != null && !string.IsNullOrEmpty(action.Flag))
                            root.State.SetFlag(action.Flag, action.Type == StoryActionType.SetFlag);
                        break;

                    case StoryActionType.PlayThought:
                        yield return PlaySequence(action.Thought);
                        break;

                    case StoryActionType.PlaySfx:
                        PlaySfx(action, position);
                        break;

                    case StoryActionType.Fade:
                        if (root != null && root.UI != null)
                            yield return root.UI.FadeTo(action.FadeAlpha, action.Duration);
                        break;

                    case StoryActionType.CompleteScene:
                        if (root != null && root.Flow != null)
                            root.Flow.CompleteCurrentScene();
                        break;

                    case StoryActionType.RestartScene:
                        if (root != null && root.Flow != null)
                            root.Flow.RestartCurrentScene();
                        break;

                    case StoryActionType.Log:
                        Debug.Log($"[Story] {action.Message}");
                        break;

                    case StoryActionType.HideObjective:
                        ObjectiveUI.Instance?.Hide();
                        break;
                }
            }
        }

        private IEnumerator PlaySequence(ThoughtSequence sequence)
        {
            if (sequence == null) yield break;

            AudioDirector audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;

            // Музыка и указание — часть той же «системы мыслей», запускаются вместе с репликами.
            if (sequence.Music != null && audio != null)
                audio.PlayMusic(sequence.Music);

            if (sequence.Objective != null)
                ObjectiveUI.Ensure()?.Show(sequence.Objective);

            for (int i = 0; i < sequence.Count; i++)
                yield return PlayLineRoutine(sequence.GetLine(i));

            if (sequence.StopMusicAfter && audio != null)
                audio.StopMusic();
        }

        private IEnumerator PlayLineRoutine(ThoughtLine line)
        {
            if (line == null) yield break;

            // Реплика без текста — только озвучка (например, первая реплика при пробуждении).
            if (string.IsNullOrEmpty(line.Text))
            {
                if (line.Voice != null && _voice != null)
                {
                    _voice.PlayOneShot(line.Voice);
                    yield return new WaitForSeconds(line.Voice.length);
                }
                yield break;
            }

            EnsureThoughtUI();
            if (_thoughtUI == null)
            {
                Debug.LogWarning("[StoryDirector] Нет панели мыслей (UIRoot не найден).", this);
                yield break;
            }

            if (line.Voice != null && _voice != null)
                _voice.PlayOneShot(line.Voice);

            yield return _thoughtUI.Play(line, charsPerSecond, thoughtFade);
        }

        private void EnsureThoughtUI()
        {
            if (_thoughtUI != null)
                return;

            UIRoot ui = GameRoot.Instance != null ? GameRoot.Instance.UI : null;
            if (ui == null || ui.Canvas == null)
                return;

            _thoughtUI = ui.GetComponentInChildren<ThoughtUI>(true);
            if (_thoughtUI == null)
                _thoughtUI = ThoughtUI.Create(ui.Canvas);
        }

        private void PlaySfx(StoryAction action, Vector3 position)
        {
            if (action.Clip == null)
                return;

            if (action.Spatial)
                AudioSource.PlayClipAtPoint(action.Clip, position, action.Volume);
            else if (_sfx2D != null)
                _sfx2D.PlayOneShot(action.Clip, action.Volume);
        }
    }
}
