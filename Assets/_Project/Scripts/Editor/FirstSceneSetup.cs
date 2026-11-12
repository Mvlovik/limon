using System.Collections.Generic;
using GameLimon.Core;
using GameLimon.Interaction;
using GameLimon.Player;
using GameLimon.Story;
using GameLimon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Один клик — первый играбельный вертикальный срез:
    /// PF_Player (камера, движение, взаимодействие), Boot с игроком,
    /// A1_S01_Apartment (blockout на платформе-земле, дверь телепортирует на улицу),
    /// StorySequence и Build Settings.
    ///
    /// Меню: Tools → GameLimon → Setup First Playable Scene.
    /// Обновление уже собранной сцены: Tools → GameLimon → Upgrade First Scene (Ground & Street).
    /// Скрипт идемпотентен: существующие ассеты и сцены не перезаписываются.
    /// </summary>
    public static class FirstSceneSetup
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";
        private const string GameRootPrefabPath = "Assets/_Project/Prefabs/Systems/PF_GameRoot.prefab";
        private const string BootScenePath = "Assets/_Project/Scenes/00_Boot/Boot.unity";
        private const string ApartmentScenePath = "Assets/_Project/Scenes/02_Act1/A1_S01_Apartment.unity";
        private const string SceneDefinitionPath = "Assets/_Project/Data/Story/SCN_A1_S01_Apartment.asset";
        private const string SequencePath = "Assets/_Project/Data/Story/SEQ_Act1.asset";
        private const string WallMaterialPath = "Assets/_Project/Art/Materials/MAT_Blockout_Wall.mat";
        private const string FloorMaterialPath = "Assets/_Project/Art/Materials/MAT_Blockout_Floor.mat";
        private const string PropMaterialPath = "Assets/_Project/Art/Materials/MAT_Blockout_Prop.mat";
        private const string AsphaltMaterialPath = "Assets/_Project/Art/Materials/MAT_Blockout_Asphalt.mat";

        private const string MenuPath = "Tools/GameLimon/Setup First Playable Scene";
        private const string UpgradeMenuPath = "Tools/GameLimon/Upgrade First Scene (Ground & Street)";

        /// <summary>Высота комнаты над землёй: примерно 4-5 этажей.</summary>
        private const float RoomHeight = 15f;

        [MenuItem(MenuPath)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Setup(interactive: true);
        }

        /// <summary>Для batch mode: Unity -executeMethod GameLimon.EditorTools.FirstSceneSetup.RunFromCommandLine</summary>
        public static void RunFromCommandLine() => Setup(interactive: false);

        [MenuItem(UpgradeMenuPath)]
        public static void UpgradeFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FirstSceneSetup] Выйдите из Play Mode и повторите.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ApartmentScenePath, OpenSceneMode.Single);
            EnsureStreetLayout(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[FirstSceneSetup] Сцена обновлена: земля, комната поднята на высоту ~5 этажа, дверь телепортирует на улицу.");
        }

        public static void Setup(bool interactive)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FirstSceneSetup] Выйдите из Play Mode и повторите.");
                return;
            }

            // 1. Boot и GameRoot создаёт отдельный инструмент — не дублируем логику.
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(GameRootPrefabPath) ||
                !AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath))
            {
                BootSetup.Setup(interactive: false);
            }

            EnsureFolders();

            bool playerCreated = EnsurePlayerPrefab();
            EnsureStoryAssets();
            EnsureGameRootServices();
            EnsureBootHasPlayer();
            bool sceneCreated = EnsureApartmentScene();
            EnsureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary =
                "[FirstSceneSetup] Готово.\n" +
                $"- Игрок: {(playerCreated ? "создан" : "уже был")} — {PlayerPrefabPath}\n" +
                $"- Boot: игрок в сцене — {BootScenePath}\n" +
                $"- Сцена: {(sceneCreated ? "создана" : "уже была")} — {ApartmentScenePath}\n" +
                "- Сцена: платформа-земля, комната на высоте ~5 этажа, дверь — телепорт на улицу\n" +
                $"- Story: {SequencePath}\n" +
                "- Build Settings: Boot = 0, A1_S01_Apartment = 1\n\n" +
                "Дальше: Play из Boot.unity. WASD — ходьба, мышь — камера, E — взаимодействие; дверь выводит на улицу.";

            Debug.Log(summary);

            if (interactive)
            {
                EditorUtility.DisplayDialog("First Playable Scene", summary, "OK");
                SceneAsset boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
                if (boot != null)
                {
                    Selection.activeObject = boot;
                    EditorGUIUtility.PingObject(boot);
                }
            }
        }

        // ------------------------------------------------------------------
        // Игрок
        // ------------------------------------------------------------------

        private static bool EnsurePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
            {
                Debug.Log($"[FirstSceneSetup] Префаб игрока уже существует: {PlayerPrefabPath}");
                return false;
            }

            var root = new GameObject("PF_Player");
            root.tag = "Player";

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.02f;

            root.AddComponent<PlayerMotor>();
            PlayerLook look = root.AddComponent<PlayerLook>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
            root.AddComponent<InteractionPromptUI>();

            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 300f;
            camera.fieldOfView = 65f;

            cameraObject.AddComponent<AudioListener>();

            SetObjectReference(look, "cameraTransform", cameraObject.transform);
            SetObjectReference(interactor, "view", cameraObject.transform);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[FirstSceneSetup] Создан префаб игрока: {PlayerPrefabPath}");
            return true;
        }

        // ------------------------------------------------------------------
        // Данные сюжета
        // ------------------------------------------------------------------

        private static void EnsureStoryAssets()
        {
            SceneDefinition definition = AssetDatabase.LoadAssetAtPath<SceneDefinition>(SceneDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<SceneDefinition>();
                AssetDatabase.CreateAsset(definition, SceneDefinitionPath);
            }

            SetString(definition, "id", "A1_S01");
            SetString(definition, "sceneName", "A1_S01_Apartment");
            SetString(definition, "displayName", "Квартира");
            SetString(definition, "checkpointId", "A1_S01_Start");
            EditorUtility.SetDirty(definition);

            StorySequence sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(SequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<StorySequence>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }

            var serialized = new SerializedObject(sequence);
            SerializedProperty scenes = serialized.FindProperty("scenes");
            scenes.arraySize = 1;
            scenes.GetArrayElementAtIndex(0).objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);

            Debug.Log($"[FirstSceneSetup] Story обновлён: {SequencePath} (1 сцена).");
        }

        // ------------------------------------------------------------------
        // Сервисы GameRoot
        // ------------------------------------------------------------------

        private static void EnsureGameRootServices()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(GameRootPrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[FirstSceneSetup] Не найден префаб {GameRootPrefabPath}");
                return;
            }

            try
            {
                if (contents.GetComponent<PlayerSpawnService>() == null)
                    contents.AddComponent<PlayerSpawnService>();

                if (contents.GetComponent<BootAutoStart>() == null)
                    contents.AddComponent<BootAutoStart>();

                GameFlow flow = contents.GetComponent<GameFlow>();
                StorySequence sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(SequencePath);

                if (flow != null && sequence != null)
                    SetObjectReference(flow, "sequence", sequence);

                PrefabUtility.SaveAsPrefabAsset(contents, GameRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Debug.Log("[FirstSceneSetup] GameRoot: добавлен PlayerSpawnService, подключён StorySequence.");
        }

        private static void EnsureBootHasPlayer()
        {
            if (!System.IO.File.Exists(ToAbsolutePath(BootScenePath)))
                return;

            Scene boot = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            GameObject existing = null;
            foreach (GameObject root in boot.GetRootGameObjects())
            {
                if (root.CompareTag("Player") || root.name.StartsWith("PF_Player"))
                {
                    existing = root;
                    break;
                }
            }

            if (existing == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boot);
                instance.transform.position = Vector3.zero;
                Debug.Log("[FirstSceneSetup] Игрок добавлен в Boot.unity.");
            }

            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);
        }

        // ------------------------------------------------------------------
        // Первая сцена
        // ------------------------------------------------------------------

        private static bool EnsureApartmentScene()
        {
            if (System.IO.File.Exists(ToAbsolutePath(ApartmentScenePath)))
            {
                Debug.Log($"[FirstSceneSetup] Сцена уже существует: {ApartmentScenePath}");
                return false;
            }

            Material wall = EnsureMaterial(WallMaterialPath, new Color(0.62f, 0.6f, 0.55f), 0.05f);
            Material floor = EnsureMaterial(FloorMaterialPath, new Color(0.35f, 0.3f, 0.25f), 0.1f);
            Material prop = EnsureMaterial(PropMaterialPath, new Color(0.45f, 0.42f, 0.38f), 0.2f);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Transform systems = CreateRoot("_Systems");
            Transform environment = CreateRoot("_Environment");
            Transform gameplay = CreateRoot("_Gameplay");
            Transform lighting = CreateRoot("_Lighting");
            CreateRoot("_Audio");
            CreateRoot("_Triggers");

            BuildApartment(environment, wall, floor, prop);

            // Спавн: угол комнаты, взгляд в сторону двери в прихожую.
            var spawnObject = new GameObject("PlayerSpawn");
            spawnObject.transform.SetParent(systems, false);
            // Спавн задаёт только направление взгляда по горизонту — без наклона.
            Vector3 lookDirection = new Vector3(0f, 1.4f, -4.4f) - new Vector3(-3.2f, 0.05f, 2.6f);
            lookDirection.y = 0f;

            spawnObject.transform.SetPositionAndRotation(
                new Vector3(-3.2f, 0.05f, 2.6f),
                Quaternion.LookRotation(lookDirection));
            spawnObject.AddComponent<PlayerSpawnPoint>();

            BuildInteractables(gameplay, prop);
            BuildLighting(lighting);

            // Земля под комнатой, подъём на высоту этажей, дверь-телепорт на улицу.
            EnsureStreetLayout(scene);

            EditorSceneManager.SaveScene(scene, ApartmentScenePath);
            Debug.Log($"[FirstSceneSetup] Создана сцена: {ApartmentScenePath}");
            return true;
        }

        private static void BuildApartment(Transform parent, Material wall, Material floor, Material prop)
        {
            Transform room = CreateGroup(parent, "Room");
            Transform hallway = CreateGroup(parent, "Hallway");
            Transform furniture = CreateGroup(parent, "Furniture");

            // Комната 10 x 9, высота 3. Пол в y = 0.
            CreateBox(room, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(10f, 0.2f, 9f), floor);
            CreateBox(room, "Ceiling", new Vector3(0f, 3.1f, 0f), new Vector3(10f, 0.2f, 9f), wall);
            CreateBox(room, "Wall_North", new Vector3(0f, 1.5f, 4.4f), new Vector3(10f, 3f, 0.2f), wall);
            CreateBox(room, "Wall_West", new Vector3(-4.9f, 1.5f, 0f), new Vector3(0.2f, 3f, 9f), wall);
            CreateBox(room, "Wall_East", new Vector3(4.9f, 1.5f, 0f), new Vector3(0.2f, 3f, 9f), wall);

            // Южная стена с дверным проёмом 2 м по центру.
            CreateBox(room, "Wall_South_Left", new Vector3(-3f, 1.5f, -4.4f), new Vector3(4f, 3f, 0.2f), wall);
            CreateBox(room, "Wall_South_Right", new Vector3(3f, 1.5f, -4.4f), new Vector3(4f, 3f, 0.2f), wall);
            CreateBox(room, "Wall_South_Lintel", new Vector3(0f, 2.75f, -4.4f), new Vector3(2f, 0.5f, 0.2f), wall);

            // Прихожая за дверью.
            CreateBox(hallway, "Floor", new Vector3(0f, -0.1f, -6.2f), new Vector3(2.8f, 0.2f, 3.6f), floor);
            CreateBox(hallway, "Ceiling", new Vector3(0f, 3.1f, -6.2f), new Vector3(2.8f, 0.2f, 3.6f), wall);
            CreateBox(hallway, "Wall_West", new Vector3(-1.4f, 1.5f, -6.2f), new Vector3(0.2f, 3f, 3.6f), wall);
            CreateBox(hallway, "Wall_East", new Vector3(1.4f, 1.5f, -6.2f), new Vector3(0.2f, 3f, 3.6f), wall);
            CreateBox(hallway, "Wall_End", new Vector3(0f, 1.5f, -8f), new Vector3(2.8f, 3f, 0.2f), wall);

            // Кухонный угол: стойка и плита-полустенка.
            CreateBox(furniture, "Kitchen_Island", new Vector3(3.3f, 0.45f, 2.6f), new Vector3(2.6f, 0.9f, 1.2f), prop);
            CreateBox(furniture, "Kitchen_HalfWall", new Vector3(1.9f, 0.55f, 2.6f), new Vector3(0.15f, 1.1f, 1.2f), wall);

            // Диван как ориентир в комнате.
            CreateBox(furniture, "Sofa_Seat", new Vector3(-3.2f, 0.35f, -0.4f), new Vector3(1.1f, 0.7f, 2.6f), prop);
            CreateBox(furniture, "Sofa_Back", new Vector3(-3.85f, 0.75f, -0.4f), new Vector3(0.25f, 1.5f, 2.6f), prop);
        }

        private static void BuildInteractables(Transform parent, Material prop)
        {
            GameObject coffee = CreateBox(parent, "CoffeeMachine", new Vector3(3.3f, 1.15f, 2.6f), new Vector3(0.55f, 0.5f, 0.45f), prop);
            var coffeeInteractable = coffee.AddComponent<FlagInteractable>();
            SetString(coffeeInteractable, "prompt", "E — заварить кофе");
            SetString(coffeeInteractable, "flag", "A1_S01_CoffeeStarted");
            SetString(coffeeInteractable, "debugMessage", "Кофе поставлен вариться.");
            SetBool(coffeeInteractable, "oneShot", true);

            GameObject door = CreateBox(parent, "FrontDoor", new Vector3(0f, 1.05f, -7.85f), new Vector3(1.1f, 2.1f, 0.1f), prop);
            var doorInteractable = door.AddComponent<FlagInteractable>();
            SetString(doorInteractable, "prompt", "E — выйти из квартиры");
            SetString(doorInteractable, "flag", "A1_S01_LeftApartment");
            SetString(doorInteractable, "debugMessage", "Выход из квартиры (сцена 2 ещё не собрана).");
            SetBool(doorInteractable, "oneShot", true);

            // Пробуждение: невидимый триггер над кроватью — лежащий игрок смотрит в потолок и видит его.
            GameObject wake = CreateBox(parent, "BedWakeUp", new Vector3(-3.2f, 2.75f, -0.4f), new Vector3(1.4f, 0.3f, 1.4f), null);
            wake.GetComponent<MeshRenderer>().enabled = false;
            var wakeInteractable = wake.AddComponent<BedWakeUp>();
            SetString(wakeInteractable, "prompt", "E — проснуться");
            SetString(wakeInteractable, "flag", "A1_S01_WokeUp");
            SetString(wakeInteractable, "debugMessage", "Игрок проснулся и встал.");
        }

        private static void BuildLighting(Transform parent)
        {
            CreatePointLight(parent, "Light_LivingRoom", new Vector3(-1f, 2.75f, 1f), new Color(1f, 0.86f, 0.65f), 1.6f, 11f);
            CreatePointLight(parent, "Light_Kitchen", new Vector3(3.3f, 2.7f, 2.6f), new Color(1f, 0.9f, 0.75f), 1.3f, 5f);
            CreatePointLight(parent, "Light_Hallway", new Vector3(0f, 2.75f, -6.2f), new Color(1f, 0.8f, 0.6f), 1.1f, 4.5f);

            var keyObject = new GameObject("Light_WindowKey");
            keyObject.transform.SetParent(parent, false);
            keyObject.transform.SetPositionAndRotation(new Vector3(-3f, 2.6f, -3f), Quaternion.Euler(50f, 20f, 0f));

            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.6f, 0.7f, 1f);
            key.intensity = 0.15f;
            key.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------
        // Земля и улица
        // ------------------------------------------------------------------

        /// <summary>
        /// Поднимает комнату на высоту ~5 этажа, кладёт под ней большую платформу-землю
        /// и превращает FrontDoor в телепорт на улицу. Идемпотентно.
        /// </summary>
        public static void EnsureStreetLayout(Scene scene)
        {
            Material asphalt = EnsureMaterial(AsphaltMaterialPath, new Color(0.28f, 0.28f, 0.3f), 0.35f);

            Transform environment = FindOrCreateRoot(scene, "_Environment");

            Transform ground = environment.Find("Ground");
            if (ground == null)
            {
                // Платформа 120 x 120, верхняя грань на y = 0.
                CreateBox(environment, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(120f, 1f, 120f), asphalt);
                Debug.Log("[FirstSceneSetup] Добавлена платформа-земля 120x120 под сценой.");
            }

            RaiseToRoomLevel(environment, "Ground");
            RaiseToRoomLevel(FindOrCreateRoot(scene, "_Gameplay"));
            RaiseToRoomLevel(FindOrCreateRoot(scene, "_Lighting"));

            Transform systems = FindOrCreateRoot(scene, "_Systems");
            RaiseToRoomLevel(systems);

            // Точка телепорта на улице — после подъёма, чтобы не попасть в RoomLevel.
            Transform streetSpawn = systems.Find("StreetSpawn");
            if (streetSpawn == null)
            {
                var streetObject = new GameObject("StreetSpawn");
                streetObject.transform.SetParent(systems, false);
                // Игрок смотрит вдоль улицы, от дома.
                streetObject.transform.SetPositionAndRotation(
                    new Vector3(0f, 0.05f, -12f),
                    Quaternion.LookRotation(Vector3.forward));
                streetSpawn = streetObject.transform;
            }

            // Дверь прихожей: теперь телепорт на улицу.
            Transform gameplay = FindOrCreateRoot(scene, "_Gameplay");
            Transform door = gameplay.Find("RoomLevel/FrontDoor");
            if (door == null)
                door = gameplay.Find("FrontDoor");

            if (door != null)
                ConfigureStreetDoor(door.gameObject, streetSpawn);
            else
                Debug.LogWarning("[FirstSceneSetup] FrontDoor не найден — телепорт не настроен.");
        }

        /// <summary>Переносит всех детей root (кроме исключений) под группу RoomLevel на высоте комнаты.</summary>
        private static void RaiseToRoomLevel(Transform root, params string[] excludedNames)
        {
            if (root.Find("RoomLevel") != null)
                return; // уже поднято

            Transform level = new GameObject("RoomLevel").transform;
            level.SetParent(root, false);

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);

                if (child == level)
                    continue;

                if (excludedNames != null && System.Array.IndexOf(excludedNames, child.name) >= 0)
                    continue;

                child.SetParent(level, true); // мировые позиции сохраняются
            }

            // Группа собрана в (0,0,0) — теперь поднимаем её вместе с содержимым.
            level.position = new Vector3(0f, RoomHeight, 0f);
        }

        /// <summary>Заменяет FlagInteractable двери на телепорт на улицу.</summary>
        private static void ConfigureStreetDoor(GameObject door, Transform destination)
        {
            foreach (Component component in door.GetComponents<Component>())
            {
                if (component is FlagInteractable)
                    Object.DestroyImmediate(component);
            }

            TeleportInteractable teleport = door.GetComponent<TeleportInteractable>();
            if (teleport == null)
                teleport = door.AddComponent<TeleportInteractable>();

            SetString(teleport, "prompt", "E — выйти на улицу");
            SetString(teleport, "flag", "A1_S01_LeftApartment");
            SetString(teleport, "debugMessage", "Телепортация на улицу.");
            SetObjectReference(teleport, "destination", destination);
            SetBool(teleport, "oneShot", false);
        }

        private static Transform FindOrCreateRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root.transform;
            }

            return CreateRoot(name);
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
        }

        // ------------------------------------------------------------------
        // Build Settings
        // ------------------------------------------------------------------

        private static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(scene => scene.path == BootScenePath || scene.path == ApartmentScenePath);

            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(ApartmentScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[FirstSceneSetup] Build Settings: Boot = 0, A1_S01_Apartment = 1.");
        }

        // ------------------------------------------------------------------
        // Утилиты
        // ------------------------------------------------------------------

        private static Transform CreateRoot(string name) => new GameObject(name).transform;

        private static Transform CreateGroup(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;

            if (material != null)
                box.GetComponent<Renderer>().sharedMaterial = material;

            return box;
        }

        private static Material EnsureMaterial(string path, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { color = color };
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[FirstSceneSetup] Поле '{field}' не найдено в {target.name}.");
                return;
            }

            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[FirstSceneSetup] Поле '{field}' не найдено в {target.name}.");
                return;
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[FirstSceneSetup] Поле '{field}' не найдено в {target.name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Prefabs/Player");
            EnsureFolder("Assets/_Project/Data/Story");
            EnsureFolder("Assets/_Project/Scenes/02_Act1");
            EnsureFolder("Assets/_Project/Art/Materials");
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

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return System.IO.Path.Combine(projectRoot, projectRelativePath);
        }
    }
}
