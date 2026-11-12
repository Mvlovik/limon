using System.Collections.Generic;
using System.IO;
using GameLimon.Audio;
using GameLimon.Core;
using GameLimon.Save;
using GameLimon.Story;
using GameLimon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.EditorTools
{

    public static class BootSetup
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Systems/PF_GameRoot.prefab";
        private const string BootScenePath = "Assets/_Project/Scenes/00_Boot/Boot.unity";
        private const string LegacySampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MenuPath = "Tools/GameLimon/Setup Boot Scene";

        [MenuItem(MenuPath)]
        public static void RunFromMenu() => Setup(interactive: true);

        /// <summary>Точка входа для Unity batch mode.</summary>
        public static void RunFromCommandLine() => Setup(interactive: false);

        public static void Setup(bool interactive)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[BootSetup] Выйдите из Play Mode и запустите снова.");
                return;
            }

            EnsureFolder("Assets/_Project/Prefabs/Systems");

            bool prefabCreated = EnsureGameRootPrefab();
            bool sceneCreated = EnsureBootScene();
            EnsureBuildSettings();
            CleanUpGitKeeps();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary =
                "[BootSetup] Готово.\n" +
                $"- Префаб: {(prefabCreated ? "создан" : "уже существует")} — {PrefabPath}\n" +
                $"- Сцена: {(sceneCreated ? "создана" : "уже существует")} — {BootScenePath}\n" +
                "- Build Settings: Boot — индекс 0 (SampleScene убрана из списка).";

            Debug.Log(summary);

            if (interactive)
            {
                Object sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
                if (sceneAsset != null)
                {
                    Selection.activeObject = sceneAsset;
                    EditorGUIUtility.PingObject(sceneAsset);
                }

                EditorUtility.DisplayDialog("Boot Setup", summary, "Ок");
            }
        }

        private static bool EnsureGameRootPrefab()
        {
            if (File.Exists(ToAbsolutePath(PrefabPath)))
            {
                Debug.Log($"[BootSetup] Префаб уже существует, пропускаю: {PrefabPath}");
                return false;
            }

            // Временная аддитивная сцена, чтобы не трогать сцены, открытые у пользователя.
            Scene tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("GameRoot");

                // порядок компонентов = порядок в инспекторе
                root.AddComponent<GameRoot>();
                root.AddComponent<GameFlow>();
                root.AddComponent<SceneLoader>();
                root.AddComponent<GameState>();
                root.AddComponent<SaveService>();
                root.AddComponent<AudioDirector>();

                UIRoot uiRoot = root.AddComponent<UIRoot>();
                root.AddComponent<InputReader>();

                UIRoot.DefaultCanvas canvas = UIRoot.BuildDefaultCanvas(root.transform);
                uiRoot.AssignReferences(canvas.Canvas, canvas.FadeOverlay);

                PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"[BootSetup] Префаб собран: {PrefabPath}");
                return true;
            }
            finally
            {
                CloseSceneSilently(tempScene);
            }
        }

        private static bool EnsureBootScene()
        {
            if (File.Exists(ToAbsolutePath(BootScenePath)))
            {
                Debug.Log($"[BootSetup] Сцена уже существует, пропускаю: {BootScenePath}");
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[BootSetup] Префаб не найден: {PrefabPath}");
                return false;
            }

            Scene bootScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // NewScene(additive) делает новую сцену активной — инстанс попадёт в неё.
                PrefabUtility.InstantiatePrefab(prefab);

                if (!EditorSceneManager.SaveScene(bootScene, BootScenePath))
                {
                    Debug.LogError($"[BootSetup] Не удалось сохранить сцену: {BootScenePath}");
                    return false;
                }

                Debug.Log($"[BootSetup] Сцена собрана: {BootScenePath}");
                return true;
            }
            finally
            {
                CloseSceneSilently(bootScene);
            }
        }

        private static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            // Boot — всегда индекс 0; шаблонный SampleScene из списка убираем.
            scenes.RemoveAll(scene => scene.path == BootScenePath || scene.path == LegacySampleScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[BootSetup] Build Settings: Boot.unity — индекс 0.");
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] segments = folderPath.Split('/');
            string current = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void CleanUpGitKeeps()
        {
            // .gitkeep был плейсхолдером пустых папок — теперь в них реальные файлы.
            string[] placeholders =
            {
                "Assets/_Project/Scenes/00_Boot/.gitkeep",
                "Assets/_Project/Scripts/Core/.gitkeep",
                "Assets/_Project/Scripts/Story/.gitkeep",
                "Assets/_Project/Scripts/Save/.gitkeep",
                "Assets/_Project/Scripts/Audio/.gitkeep",
                "Assets/_Project/Scripts/UI/.gitkeep",
                "Assets/_Project/Scripts/Editor/.gitkeep",
                "Assets/_Project/Prefabs/Systems/.gitkeep",
            };

            foreach (string path in placeholders)
            {
                string absolute = ToAbsolutePath(path);
                if (!File.Exists(absolute))
                    continue;

                AssetDatabase.DeleteAsset(path);

                if (File.Exists(absolute))
                    File.Delete(absolute);

                if (File.Exists(absolute + ".meta"))
                    File.Delete(absolute + ".meta");
            }
        }

        private static void CloseSceneSilently(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelativePath);
        }
    }
}
