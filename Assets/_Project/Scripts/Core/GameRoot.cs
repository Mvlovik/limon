using GameLimon.Audio;
using GameLimon.Save;
using GameLimon.UI;
using UnityEngine;

namespace GameLimon.Core
{
    /// <summary>
    /// Корневой объект Boot и единственный владелец глобальных сервисов.
    /// Сцены не создают своих менеджеров — ссылки берутся отсюда
    /// (через сериализованные поля или события).
    /// Boot всегда загружена, поэтому DontDestroyOnLoad не нужен.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        public GameState State { get; private set; }
        public GameFlow Flow { get; private set; }
        public SceneLoader Loader { get; private set; }
        public SaveService Save { get; private set; }
        public AudioDirector Audio { get; private set; }
        public UIRoot UI { get; private set; }
        public InputReader Input { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameRoot] Найден второй GameRoot — уничтожен. GameRoot должен быть один, в Boot.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            State = GetComponent<GameState>();
            Flow = GetComponent<GameFlow>();
            Loader = GetComponent<SceneLoader>();
            Save = GetComponent<SaveService>();
            Audio = GetComponent<AudioDirector>();
            UI = GetComponent<UIRoot>();
            Input = GetComponent<InputReader>();

            Debug.Log("[GameRoot] Сервисы инициализированы. Boot готов.", this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
