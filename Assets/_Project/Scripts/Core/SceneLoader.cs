using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.Core
{
    /// <summary>
    /// Аддитивная загрузка/выгрузка игровых сцен поверх Boot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour
    {
        private readonly HashSet<string> _loadedScenes = new HashSet<string>();

        private Scene _previousActiveScene;

        public event Action<string> SceneLoaded;
        public event Action<string> SceneUnloaded;

        public bool IsBusy { get; private set; }

        public bool IsLoaded(string sceneName) => _loadedScenes.Contains(sceneName);

        /// <summary>Аддитивно грузит сцену по имени</summary>
        public Coroutine LoadAdditive(string sceneName, Action onLoaded = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Пустое имя сцены.");
                onLoaded?.Invoke();
                return null;
            }

            if (_loadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[SceneLoader] Сцена \"{sceneName}\" уже загружена.");
                onLoaded?.Invoke();
                return null;
            }

            return StartCoroutine(LoadRoutine(sceneName, onLoaded));
        }

        /// <summary>Выгружает ранее загруженную сцену</summary>
        public Coroutine Unload(string sceneName, Action onUnloaded = null)
        {
            if (string.IsNullOrEmpty(sceneName) || !_loadedScenes.Contains(sceneName))
            {
                onUnloaded?.Invoke();
                return null;
            }

            return StartCoroutine(UnloadRoutine(sceneName, onUnloaded));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onLoaded)
        {
            IsBusy = true;

            Scene activeBeforeLoad = SceneManager.GetActiveScene();
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                IsBusy = false;
                Debug.LogError($"[SceneLoader] Не удалось начать загрузку \"{sceneName}\". Сцена есть в Build Settings?");
                yield break;
            }

            yield return operation;

            Scene loaded = SceneManager.GetSceneByName(sceneName);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                // Аддитивная сцена не применяет свои RenderSettings (skybox, fog, ambient),
                // пока не станет активной. Запоминаем Boot/меню, чтобы вернуть их при выгрузке.
                _previousActiveScene = activeBeforeLoad;
                SceneManager.SetActiveScene(loaded);
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] Сцена \"{sceneName}\" загрузилась, но не найдена по имени.");
            }

            _loadedScenes.Add(sceneName);
            IsBusy = false;

            Debug.Log($"[SceneLoader] Загружена: {sceneName}");
            SceneLoaded?.Invoke(sceneName);
            onLoaded?.Invoke();
        }

        private IEnumerator UnloadRoutine(string sceneName, Action onUnloaded)
        {
            IsBusy = true;

            bool wasActive = SceneManager.GetActiveScene().name == sceneName;
            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);
            if (operation == null)
            {
                IsBusy = false;
                Debug.LogError($"[SceneLoader] Не удалось начать выгрузку \"{sceneName}\"");
                yield break;
            }

            yield return operation;

            _loadedScenes.Remove(sceneName);
            IsBusy = false;

            if (wasActive)
                ActivateFallbackScene();

            Debug.Log($"[SceneLoader] Выгружена: \"{sceneName}\"");
            SceneUnloaded?.Invoke(sceneName);
            onUnloaded?.Invoke();
        }

        private void ActivateFallbackScene()
        {
            if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(_previousActiveScene);
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded)
                {
                    SceneManager.SetActiveScene(candidate);
                    return;
                }
            }
        }
    }
}
