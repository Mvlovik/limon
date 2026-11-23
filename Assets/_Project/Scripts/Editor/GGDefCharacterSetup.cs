using System.IO;
using System.Linq;
using GameLimon.Companions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Собирает GG_DEF как Humanoid-персонажа и использует анимации из
    /// Free Sample Animation Set (Vanilla Loop Studio): Idle / Jog (Walk) / Run.
    /// Пак предварительно переносится в Art/Animations.
    /// </summary>
    public static class GGDefCharacterSetup
    {
        private const string GgPath = "Assets/_Project/Art/Characters/GG/GG_DEF.fbx";

        private const string PackSource = "Assets/VanillaLoopStudio";
        private const string PackDest = "Assets/_Project/Art/Animations/VanillaLoopStudio";
        private const string PackRoot = PackDest + "/FreeSampleAnimationSet";

        private const string ControllerPath = "Assets/_Project/Art/Animations/GG/GG.controller";
        private const string MaterialFolder = "Assets/_Project/Art/Characters/Materials";
        private const string TexturePath = "Assets/_Project/Art/Characters/Textures/Character_07.png";

        private const string IdleFbx = PackRoot +
            "/Art/Animations/SurvivalSet/Mannequin/Flashlight/Idle/RightHand/Type01/A_Flashlight_Idle_01_R.fbx";
        private const string WalkFbx = PackRoot +
            "/Art/Animations/FemaleLocomotionSet/Mannequin/RootMotion/A_JogFwd_Loop.fbx";
        private const string RunFbx = PackRoot +
            "/Art/Animations/MaleLocomotionSet/Mannequin/RootMotion/A_RunFwd_Loop.fbx";

        private const string MenuPath = "Tools/GameLimon/Setup GG_DEF With Free Sample Animations";

        [MenuItem(MenuPath)]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[GGSetup] Выйдите из Play Mode и запустите снова.");
                return;
            }

            if (!MovePackToArt())
                return;

            Avatar avatar = EnsureGgHumanoidAvatar();
            if (avatar == null)
                return;

            GameObject joe = FindJoe();
            if (joe == null)
            {
                Debug.LogError("[GGSetup] Не найден Joe (CompanionActor) в открытой сцене.");
                return;
            }

            GameObject gg = EnsureGgModel(joe);
            if (gg == null)
                return;

            AnimationClip idle = ConfigureAndLoadClip(IdleFbx);
            AnimationClip walk = ConfigureAndLoadClip(WalkFbx);
            AnimationClip run = ConfigureAndLoadClip(RunFbx);

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[GGSetup] Не удалось загрузить клипы набора. Проверь пути после переноса пака.");
                return;
            }

            AnimatorController controller = BuildController(idle, walk, run);
            Animator animator = EnsureCharacterAnimator(gg, avatar, controller);
            PointCompanionTo(joe, animator);

            // Старый Animator на корне Joe (от процедурного сетапа) больше не нужен:
            // он бы конфликтовал с Animator на модели.
            Animator rootAnimator = joe.GetComponent<Animator>();
            if (rootAnimator != null && rootAnimator != animator)
                Undo.DestroyObjectImmediate(rootAnimator);

            Scene scene = joe.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = gg;

            Debug.Log($"[GGSetup] GG_DEF настроен на Free Sample Animation Set. " +
                      $"Клипы: {idle.name}, {walk.name}, {run.name}. Контроллер: {ControllerPath}. Сцена сохранена.");
        }

        // ------------------------------------------------------------------
        // Пак и GG_DEF
        // ------------------------------------------------------------------

        private static bool MovePackToArt()
        {
            if (AssetDatabase.IsValidFolder(PackSource))
            {
                EnsureFolder("Assets/_Project/Art/Animations");
                string error = AssetDatabase.MoveAsset(PackSource, PackDest);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[GGSetup] Не удалось перенести пак в Art: {error}");
                    return false;
                }

                AssetDatabase.Refresh();
                Debug.Log($"[GGSetup] Пак перенесён: {PackSource} -> {PackDest}");
            }

            if (!AssetDatabase.IsValidFolder(PackRoot))
            {
                Debug.LogError($"[GGSetup] Не найден пак анимаций: {PackRoot}");
                return false;
            }

            return true;
        }

        private static Avatar EnsureGgHumanoidAvatar()
        {
            ModelImporter importer = AssetImporter.GetAtPath(GgPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[GGSetup] Не найден импортер модели: {GgPath}");
                return null;
            }

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (!importer.autoGenerateAvatarMappingIfUnspecified)
            {
                importer.autoGenerateAvatarMappingIfUnspecified = true;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(GgPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
            {
                Debug.LogError("[GGSetup] Unity не создал Avatar для GG_DEF. Проверь Humanoid-маппинг в Import Settings модели.");
                return null;
            }

            if (!avatar.isValid)
                Debug.LogWarning("[GGSetup] Avatar GG_DEF невалиден — ретаргетинг анимаций может работать некорректно.", avatar);

            return avatar;
        }

        private static GameObject EnsureGgModel(GameObject joe)
        {
            GameObject gg = FindGgInstance(joe);
            bool created = false;

            if (gg == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(GgPath);
                if (model == null)
                {
                    Debug.LogError($"[GGSetup] Не найден ассет модели: {GgPath}");
                    return null;
                }

                gg = (GameObject)PrefabUtility.InstantiatePrefab(model, joe.transform);
                Undo.RegisterCreatedObjectUndo(gg, "Instantiate GG_DEF");
                created = true;
            }

            if (gg.transform.parent != joe.transform)
                Undo.SetTransformParent(gg.transform, joe.transform, "Attach GG_DEF to Joe");

            gg.name = "GG_DEF";
            if (!gg.activeSelf)
                gg.SetActive(true);

            Transform oldBody = null;
            foreach (Transform child in joe.transform)
            {
                if (child != gg.transform && child.name == "Body")
                {
                    oldBody = child;
                    break;
                }
            }

            float joeHeight = oldBody != null ? MeasureHeight(oldBody.gameObject) : 1.8f;

            // Для уже готового сетапа не трогаем трансформ. Для новой модели
            // (или если после реимпорта размер потерялся) ставим по месту.
            bool place = created || oldBody != null;
            if (!place)
            {
                float currentHeight = MeasureHeight(gg);
                place = currentHeight < 0.01f || Mathf.Abs(currentHeight / joeHeight - 1f) > 0.35f;
            }

            if (place)
            {
                Undo.RecordObject(gg.transform, "Place GG_DEF");
                gg.transform.localPosition = Vector3.zero;
                gg.transform.localRotation = Quaternion.identity;
                gg.transform.localScale = Vector3.one;
                OrientForward(gg, joe);
                NormalizeHeightAndFeet(gg, joe, joeHeight);
            }

            if (oldBody != null)
                Undo.DestroyObjectImmediate(oldBody.gameObject);

            StripNonCharacterComponents(gg);
            EnsureUrpMaterial(gg);
            return gg;
        }

        private static GameObject FindJoe()
        {
            GameObject joe = GameObject.Find("_Characters/Joe");
            if (joe != null)
                return joe;

            CompanionActor[] actors = Object.FindObjectsByType<CompanionActor>(FindObjectsInactive.Include);
            return actors.Length > 0 ? actors[0].gameObject : null;
        }

        private static GameObject FindGgInstance(GameObject joe)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);

            foreach (GameObject go in objects)
            {
                if (go == null || go == joe)
                    continue;

                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (root == null || root == joe)
                    continue;

                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (source == GgPath)
                    return root;
            }

            foreach (GameObject go in objects)
            {
                if (go != null && go != joe &&
                    go.name.IndexOf("GG_DEF", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return go;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Анимации пака
        // ------------------------------------------------------------------

        private static AnimationClip ConfigureAndLoadClip(string fbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[GGSetup] Не найден FBX с анимацией: {fbxPath}");
                return null;
            }

            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes != null && takes.Length > 0)
            {
                bool changed = false;
                foreach (ModelImporterClipAnimation take in takes)
                {
                    if (!take.loopTime)
                    {
                        take.loopTime = true;
                        changed = true;
                    }

                    if (take.wrapMode != WrapMode.Loop)
                    {
                        take.wrapMode = WrapMode.Loop;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = takes;
                    importer.SaveAndReimport();
                }
            }

            AnimationClip clip = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview__"))
                {
                    clip = candidate;
                    break;
                }
            }

            if (clip == null)
                Debug.LogError($"[GGSetup] В FBX нет AnimationClip: {fbxPath}");

            return clip;
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "Speed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0f
            });
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "PlaybackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;

            AnimatorState walkState = machine.AddState("Walk");
            walkState.motion = walk;
            walkState.speed = 0.8f;
            walkState.speedParameterActive = true;
            walkState.speedParameter = "PlaybackSpeed";

            AnimatorState runState = machine.AddState("Run");
            runState.motion = run;
            runState.speed = 1f;

            machine.defaultState = idleState;

            // Idle -> Run напрямую, если игрок ушёл далеко (catch-up).
            AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.2f;
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            idleToRun.AddCondition(AnimatorConditionMode.Greater, 1.55f, "PlaybackSpeed");

            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.2f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");

            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.2f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            AnimatorStateTransition walkToRun = walkState.AddTransition(runState);
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0.2f;
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 1.55f, "PlaybackSpeed");

            AnimatorStateTransition runToWalk = runState.AddTransition(walkState);
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0.2f;
            runToWalk.AddCondition(AnimatorConditionMode.Less, 1.45f, "PlaybackSpeed");
            runToWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.2f;
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static Animator EnsureCharacterAnimator(GameObject gg, Avatar avatar, AnimatorController controller)
        {
            Animator animator = gg.GetComponent<Animator>();
            if (animator == null)
                animator = gg.AddComponent<Animator>();

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);
            return animator;
        }

        private static void PointCompanionTo(GameObject joe, Animator animator)
        {
            CompanionActor actor = joe.GetComponent<CompanionActor>();
            if (actor == null)
                return;

            var so = new SerializedObject(actor);
            SerializedProperty animatorProperty = so.FindProperty("animator");
            if (animatorProperty != null)
            {
                animatorProperty.objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------
        // Вспомогательное
        // ------------------------------------------------------------------

        private static Transform FindBone(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t.name == name || t.name.EndsWith(":" + name, System.StringComparison.OrdinalIgnoreCase))
                    return t;
            }

            return null;
        }

        private static Bounds MeasureBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static float MeasureHeight(GameObject root)
        {
            Bounds bounds = MeasureBounds(root);
            return bounds.size.y > 0.01f ? bounds.size.y : 1.8f;
        }

        private static void OrientForward(GameObject gg, GameObject joe)
        {
            Transform foot = FindBone(gg.transform, "LeftFoot");
            Transform toe = FindBone(gg.transform, "LeftToeBase") ?? FindBone(gg.transform, "LeftToe_End");
            if (foot == null || toe == null)
                return;

            Vector3 toeDirection = toe.position - foot.position;
            if (Vector3.Dot(toeDirection, joe.transform.forward) < 0f)
            {
                gg.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log("[GGSetup] GG_DEF развёрнут на 180° (смотрел назад).");
            }
        }

        private static void NormalizeHeightAndFeet(GameObject gg, GameObject joe, float targetHeight)
        {
            float modelHeight = MeasureHeight(gg);
            float scale = modelHeight > 0.01f ? targetHeight / modelHeight : 1f;

            if (scale < 0.05f || scale > 20f)
                scale = 1f;

            gg.transform.localScale = Vector3.one * scale;

            Bounds bounds = MeasureBounds(gg);
            Vector3 centerLocal = joe.transform.InverseTransformPoint(bounds.center);
            Vector3 bottomLocal = joe.transform.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));

            gg.transform.localPosition += new Vector3(-centerLocal.x, -bottomLocal.y, -centerLocal.z);
        }

        private static void StripNonCharacterComponents(GameObject gg)
        {
            foreach (Camera camera in gg.GetComponentsInChildren<Camera>(true))
                Undo.DestroyObjectImmediate(camera);

            foreach (Light light in gg.GetComponentsInChildren<Light>(true))
                Undo.DestroyObjectImmediate(light);

            foreach (AudioListener listener in gg.GetComponentsInChildren<AudioListener>(true))
                Undo.DestroyObjectImmediate(listener);

            foreach (Animator nestedAnimator in gg.GetComponentsInChildren<Animator>(true))
            {
                if (nestedAnimator.gameObject != gg)
                    Undo.DestroyObjectImmediate(nestedAnimator);
            }

            foreach (Animation nestedAnimation in gg.GetComponentsInChildren<Animation>(true))
                Undo.DestroyObjectImmediate(nestedAnimation);

            foreach (SkinnedMeshRenderer skinned in gg.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.updateWhenOffscreen = true;
        }

        private static void EnsureUrpMaterial(GameObject gg)
        {
            Renderer[] renderers = gg.GetComponentsInChildren<Renderer>(true);
            bool needsFix = false;
            Texture texture = null;

            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                    {
                        needsFix = true;
                        continue;
                    }

                    bool isUrp = material.shader.name.StartsWith("Universal Render Pipeline");
                    bool missingBase = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == null;

                    if (!isUrp || missingBase)
                    {
                        needsFix = true;
                        if (texture == null)
                        {
                            if (material.HasProperty("_MainTex"))
                                texture = material.GetTexture("_MainTex");
                            if (texture == null && material.HasProperty("_BaseMap"))
                                texture = material.GetTexture("_BaseMap");
                        }
                    }
                }
            }

            if (!needsFix)
                return;

            EnsureFolder(MaterialFolder);
            string materialPath = MaterialFolder + "/MAT_GG_DEF.mat";
            Material urpMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (urpMaterial == null)
            {
                urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(urpMaterial, materialPath);
            }

            if (texture == null)
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

            urpMaterial.SetTexture("_BaseMap", texture);
            urpMaterial.SetColor("_BaseColor", Color.white);
            urpMaterial.SetFloat("_Metallic", 0f);
            urpMaterial.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(urpMaterial);

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = urpMaterial;
                renderer.sharedMaterials = materials;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
