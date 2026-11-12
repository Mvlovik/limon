using System;
using System.Collections.Generic;
using GameLimon.Save;
using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// Рантайм-состояние прохождения
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameState : MonoBehaviour
    {
        private readonly HashSet<string> _flags = new HashSet<string>();
        private readonly Dictionary<string, string> _choices = new Dictionary<string, string>();

        public event Action<string, bool> FlagChanged;
        public event Action<string, string> ChoiceMade;

        public string CurrentSceneId { get; private set; }
        public string CurrentCheckpointId { get; private set; }

        public bool GetFlag(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public void SetFlag(string flag, bool value)
        {
            if (string.IsNullOrEmpty(flag))
            {
                Debug.LogWarning("[GameState] Пустое имя флага");
                return;
            }

            if (_flags.Contains(flag) == value)
                return;

            if (value)
                _flags.Add(flag);
            else
                _flags.Remove(flag);

            FlagChanged?.Invoke(flag, value);
        }

        public bool TryGetChoice(string id, out string value) => _choices.TryGetValue(id, out value);

        public void SetChoice(string id, string value)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[GameState] Пустое имя выбора");
                return;
            }

            _choices[id] = value;
            ChoiceMade?.Invoke(id, value);
        }

        public void SetCurrentScene(string sceneId) => CurrentSceneId = sceneId;

        public void SetCheckpoint(string checkpointId) => CurrentCheckpointId = checkpointId;

        /// <summary>Снимок состояния для сохранения</summary>
        public GameStateSnapshot CaptureSnapshot()
        {
            var snapshot = new GameStateSnapshot
            {
                sceneId = CurrentSceneId,
                checkpointId = CurrentCheckpointId
            };

            snapshot.flags.AddRange(_flags);

            foreach (KeyValuePair<string, string> pair in _choices)
                snapshot.choices.Add(new ChoiceEntry(pair.Key, pair.Value));

            return snapshot;
        }

        /// <summary>Восстановление состояния из сейва</summary>
        public void ApplySnapshot(GameStateSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            _flags.Clear();
            if (snapshot.flags != null)
                _flags.UnionWith(snapshot.flags);

            _choices.Clear();
            if (snapshot.choices != null)
            {
                foreach (ChoiceEntry entry in snapshot.choices)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.id))
                        _choices[entry.id] = entry.value;
                }
            }

            CurrentSceneId = snapshot.sceneId;
            CurrentCheckpointId = snapshot.checkpointId;
        }

        /// <summary>Сброс к началу (новая игра)</summary>
        public void Reset()
        {
            _flags.Clear();
            _choices.Clear();
            CurrentSceneId = null;
            CurrentCheckpointId = null;
        }
    }
}
