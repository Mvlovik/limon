using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.Player
{
    /// <summary>
    /// Переключает активное тело игрока под текущую сцену.
    /// Все варианты лежат в PF_Player как неактивные дети и включается только один.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class PlayerBodyVariant : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Точное имя сцены, например A1_S02_Alley.")]
            public string sceneName;

            [Tooltip("Тело игрока, которое нужно включить в этой сцене.")]
            public GameObject body;
        }

        [Tooltip("Все возможные тела игрока. В любой момент активно только одно.")]
        [SerializeField] private GameObject[] bodies = Array.Empty<GameObject>();

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Apply(SceneManager.GetActiveScene().name);
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Apply(next.name);
        }

        private void Apply(string sceneName)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.body == null)
                    continue;

                if (!string.Equals(entry.sceneName, sceneName, StringComparison.Ordinal))
                    continue;

                foreach (GameObject body in bodies)
                {
                    if (body != null)
                        body.SetActive(body == entry.body);
                }

                return;
            }
        }
    }
}
