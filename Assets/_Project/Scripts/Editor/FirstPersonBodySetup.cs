using System.IO;
using System.Linq;
using GameLimon.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Добавляет GG_POLICE как first-person тело игрока в PF_Player:
    /// Humanoid-аватар, контроллер Idle/Walk/Run/Crouch из Free Sample Animation Set,
    /// скрытие головы и процедурный подгиб ног в прыжке.
    /// </summary>
    public static class FirstPersonBodySetup
    {
        private const string GgPolicePath = "Assets/_Project/Art/cheliki/Models/GG_POLICE.fbx";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PF_Player.prefab";

        private const string PackSource = "Assets/VanillaLoopStudio";
        private const string PackDest = "Assets/_Project/Art/Animations/VanillaLoopStudio";
        private const string PackRoot = PackDest + "/FreeSampleAnimationSet";

        private const string ControllerPath = "Assets/_Project/Art/Animations/Player/PlayerBody.controller";
        private const string BodyMeshPath = "Assets/_Project/Art/Animations/Player/GG_POLICE_FirstPerson.asset";
        private const string MaterialFolder = "Assets/_Project/Art/cheliki/Materials";
        private const string TexturePath = "Assets/_Project/Art/cheliki/Textures/4532.png";
        private const string MaterialPath = MaterialFolder + "/MAT_GG_POLICE.mat";

        private const string IdlePoseFbx = PackRoot +
            "/Art/Animations/ItemPickupSet/Mannequin/A_ItemPickup_fromIdle_RH_100cm.fbx";
        private const string WalkFbx = PackRoot +
            "/Art/Animations/FemaleLocomotionSet/Mannequin/RootMotion/A_JogFwd_Loop.fbx";
        private const string RunFbx = PackRoot +
            "/Art/Animations/MaleLocomotionSet/Mannequin/RootMotion/A_RunFwd_Loop.fbx";
        private const string CrouchFbx = PackRoot +
            "/Art/Animations/CoverSet/Mannequin/RootMotion/A_CoverHi_Walk_L_Loop.fbx";

        private const string MenuPath = "Tools/GameLimon/Setup First-Person Body (GG_POLICE)";

        [MenuItem(MenuPath)]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FPSetup] Выйдите из Play Mode и запустите снова.");
                return;
            }

            if (!MovePackToArt())
                return;

            Avatar avatar = EnsureHumanoidAvatar(GgPolicePath);
            if (avatar == null)
                return;

            AnimationClip idlePose = ConfigureAndLoadClip(IdlePoseFbx);
            AnimationClip walk = ConfigureAndLoadClip(WalkFbx);
            AnimationClip run = ConfigureAndLoadClip(RunFbx);
            AnimationClip crouch = ConfigureAndLoadClip(CrouchFbx);

            if (idlePose == null || walk == null || run == null || crouch == null)
            {
                Debug.LogError("[FPSetup] Не удалось загрузить клипы набора.");
                return;
            }

            AnimatorController controller = BuildController(idlePose, walk, run, crouch);
            AddBodyToPlayerPrefab(avatar, controller);

            AssetDatabase.SaveAssets();
            Debug.Log($"[FPSetup] First-person тело GG_POLICE добавлено в PF_Player. Контроллер: {ControllerPath}. " +
                      "Запускай игру из Boot.");
        }

        [MenuItem("Tools/GameLimon/Fix First-Person Cameras (PF_Player)")]
        public static void FixFirstPersonCameras()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FPSetup] Выйдите из Play Mode и запустите снова.");
                return;
            }

            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (player == null)
            {
                Debug.LogError($"[FPSetup] Не удалось открыть префаб: {PlayerPrefabPath}");
                return;
            }

            try
            {
                Transform playerCamera = player.transform.Find("PlayerCamera");
                int disabled = 0;

                foreach (Camera camera in player.GetComponentsInChildren<Camera>(true))
                {
                    if (playerCamera != null && camera.transform == playerCamera)
                        continue;

                    camera.enabled = false;
                    camera.gameObject.SetActive(false);
                    disabled++;
                }

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[FPSetup] Отключено сторонних камер в PF_Player: {disabled}. " +
                          "Активной осталась только PlayerCamera.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        [MenuItem("Tools/GameLimon/First-Person Body: Hide Torso (Keep Arms + Legs)")]
        public static void HideTorsoFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[FPSetup] Выйдите из Play Mode и запустите снова.");
                return;
            }

            EnsureModelReadable(GgPolicePath);

            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (player == null)
            {
                Debug.LogError($"[FPSetup] Не удалось открыть префаб: {PlayerPrefabPath}");
                return;
            }

            try
            {
                Transform body = player.transform.Find("GG_POLICE");
                if (body == null)
                {
                    Debug.LogError("[FPSetup] В PF_Player нет тела GG_POLICE. Сначала запусти Setup First-Person Body.");
                    return;
                }

                int filtered = FilterBodyTorso(body.gameObject);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[FPSetup] Торс скрыт, оставлены руки и ноги. Отфильтровано рендереров: {filtered}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        private static void EnsureModelReadable(string modelPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static int FilterBodyTorso(GameObject body)
        {
            EnsureFolder("Assets/_Project/Art/Animations/Player");

            SkinnedMeshRenderer[] renderers = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int filteredCount = 0;

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Mesh source = renderer.sharedMesh;
                if (source == null)
                    continue;

                if (!source.isReadable)
                {
                    EnsureModelReadable(GgPolicePath);
                    source = renderer.sharedMesh;
                }

                if (source == null || !source.isReadable)
                {
                    Debug.LogWarning($"[FPSetup] Меш {renderer.name} недоступен для чтения, фильтрация пропущена.", renderer);
                    continue;
                }

                Mesh filtered = BuildArmLegMesh(source, renderer.bones);
                if (filtered == null)
                    continue;

                string assetPath = filteredCount == 0
                    ? BodyMeshPath
                    : BodyMeshPath.Replace(".asset", "_" + filteredCount + ".asset");

                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);

                AssetDatabase.CreateAsset(filtered, assetPath);
                renderer.sharedMesh = filtered;
                EditorUtility.SetDirty(renderer);
                filteredCount++;
            }

            return filteredCount;
        }

        private static Mesh BuildArmLegMesh(Mesh source, Transform[] bones)
        {
            BoneWeight[] weights = source.boneWeights;
            if (weights == null || weights.Length == 0)
                return null;

            bool[] keep = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                keep[i] = IsArmOrLegBone(bones[i] != null ? bones[i].name : string.Empty);

            Mesh filtered = Object.Instantiate(source);
            filtered.name = "GG_POLICE_FirstPerson";
            int totalKept = 0;

            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                int[] triangles = source.GetTriangles(submesh);
                var kept = new System.Collections.Generic.List<int>(triangles.Length);

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    if (DominantBoneKept(triangles[i], weights, keep) &&
                        DominantBoneKept(triangles[i + 1], weights, keep) &&
                        DominantBoneKept(triangles[i + 2], weights, keep))
                    {
                        kept.Add(triangles[i]);
                        kept.Add(triangles[i + 1]);
                        kept.Add(triangles[i + 2]);
                    }
                }

                totalKept += kept.Count;
                filtered.SetTriangles(kept, submesh);
            }

            if (totalKept == 0)
            {
                Debug.LogWarning("[FPSetup] Не найдено треугольников, привязанных к рукам/ногам. Меш не изменён.");
                Object.DestroyImmediate(filtered);
                return null;
            }

            filtered.RecalculateBounds();
            return filtered;
        }

        private static bool DominantBoneKept(int vertex, BoneWeight[] weights, bool[] keep)
        {
            BoneWeight weight = weights[vertex];
            float bestWeight = -1f;
            int bestBone = -1;

            Check(weight.weight0, weight.boneIndex0);
            Check(weight.weight1, weight.boneIndex1);
            Check(weight.weight2, weight.boneIndex2);
            Check(weight.weight3, weight.boneIndex3);

            return bestBone >= 0 && bestBone < keep.Length && keep[bestBone];

            void Check(float value, int bone)
            {
                if (value > bestWeight)
                {
                    bestWeight = value;
                    bestBone = bone;
                }
            }
        }

        private static bool IsArmOrLegBone(string boneName)
        {
            string name = boneName.ToLowerInvariant();

            if (name.Contains("spine") || name.Contains("neck") || name.Contains("head") ||
                name.Contains("hips") || name.Contains("pelvis"))
                return false;

            return name.Contains("shoulder") || name.Contains("arm") || name.Contains("hand") ||
                   name.Contains("thumb") || name.Contains("index") || name.Contains("middle") ||
                   name.Contains("ring") || name.Contains("pinky") || name.Contains("upleg") ||
                   name.Contains("leg") || name.Contains("foot") || name.Contains("toe");
        }

        // ------------------------------------------------------------------
        // Пак и GG_POLICE
        // ------------------------------------------------------------------

        private static bool MovePackToArt()
        {
            if (AssetDatabase.IsValidFolder(PackSource))
            {
                EnsureFolder("Assets/_Project/Art/Animations");
                string error = AssetDatabase.MoveAsset(PackSource, PackDest);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[FPSetup] Не удалось перенести пак: {error}");
                    return false;
                }

                AssetDatabase.Refresh();
            }

            if (!AssetDatabase.IsValidFolder(PackRoot))
            {
                Debug.LogError($"[FPSetup] Не найден пак анимаций: {PackRoot}");
                return false;
            }

            return true;
        }

        private static Avatar EnsureHumanoidAvatar(string modelPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[FPSetup] Не найден импортер модели: {modelPath}");
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

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null)
            {
                Debug.LogError("[FPSetup] Unity не создал Avatar для GG_POLICE.");
                return null;
            }

            if (!avatar.isValid)
                Debug.LogWarning("[FPSetup] Avatar GG_POLICE невалиден — ретаргетинг может быть неточным.", avatar);

            return avatar;
        }

        // ------------------------------------------------------------------
        // Клипы
        // ------------------------------------------------------------------

        private static AnimationClip ConfigureAndLoadClip(string fbxPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[FPSetup] Не найден FBX с анимацией: {fbxPath}");
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

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }

            Debug.LogError($"[FPSetup] В FBX нет AnimationClip: {fbxPath}");
            return null;
        }

        private static AnimatorController BuildController(AnimationClip idlePose, AnimationClip walk, AnimationClip run, AnimationClip crouch)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            EnsureFolder("Assets/_Project/Art/Animations/Player");
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
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "Crouching",
                type = AnimatorControllerParameterType.Bool,
                defaultBool = false
            });
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "CrouchSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 0.1f
            });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Нейтральная стойка: первый кадр ItemPickup (из Idle) заморожен.
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idlePose;
            idleState.speed = 0f;

            BlendTree locomotion = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed"
            };
            AssetDatabase.AddObjectToAsset(locomotion, controller);
            locomotion.AddChild(walk, 0.62f);
            locomotion.AddChild(run, 1f);

            AnimatorState locomotionState = machine.AddState("Locomotion");
            locomotionState.motion = locomotion;
            locomotionState.speedParameterActive = true;
            locomotionState.speedParameter = "PlaybackSpeed";

            AnimatorState crouchState = machine.AddState("Crouch");
            crouchState.motion = crouch;
            crouchState.speed = 1f;
            crouchState.speedParameterActive = true;
            crouchState.speedParameter = "CrouchSpeed";

            machine.defaultState = idleState;

            AnimatorStateTransition idleToCrouch = idleState.AddTransition(crouchState);
            idleToCrouch.hasExitTime = false;
            idleToCrouch.duration = 0.2f;
            idleToCrouch.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");

            AnimatorStateTransition idleToLocomotion = idleState.AddTransition(locomotionState);
            idleToLocomotion.hasExitTime = false;
            idleToLocomotion.duration = 0.2f;
            idleToLocomotion.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");

            AnimatorStateTransition locomotionToCrouch = locomotionState.AddTransition(crouchState);
            locomotionToCrouch.hasExitTime = false;
            locomotionToCrouch.duration = 0.2f;
            locomotionToCrouch.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");

            AnimatorStateTransition locomotionToIdle = locomotionState.AddTransition(idleState);
            locomotionToIdle.hasExitTime = false;
            locomotionToIdle.duration = 0.2f;
            locomotionToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            AnimatorStateTransition crouchToLocomotion = crouchState.AddTransition(locomotionState);
            crouchToLocomotion.hasExitTime = false;
            crouchToLocomotion.duration = 0.2f;
            crouchToLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
            crouchToLocomotion.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            AnimatorStateTransition crouchToIdle = crouchState.AddTransition(idleState);
            crouchToIdle.hasExitTime = false;
            crouchToIdle.duration = 0.2f;
            crouchToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
            crouchToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ------------------------------------------------------------------
        // PF_Player
        // ------------------------------------------------------------------

        private static void AddBodyToPlayerPrefab(Avatar avatar, AnimatorController controller)
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (player == null)
            {
                Debug.LogError($"[FPSetup] Не удалось открыть префаб: {PlayerPrefabPath}");
                return;
            }

            try
            {
                CharacterController character = player.GetComponent<CharacterController>();
                float targetHeight = character != null ? character.height : 1.8f;

                // Удаляем прежнее тело, если сетап уже запускали.
                foreach (FirstPersonBody existingBody in player.GetComponentsInChildren<FirstPersonBody>(true))
                    Object.DestroyImmediate(existingBody.gameObject);

                for (int i = player.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = player.transform.GetChild(i);
                    if (child.name == "GG_POLICE")
                        Object.DestroyImmediate(child.gameObject);
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(GgPolicePath);
                if (model == null)
                {
                    Debug.LogError($"[FPSetup] Не найден ассет модели: {GgPolicePath}");
                    return;
                }

                GameObject body = Object.Instantiate(model, player.transform);
                body.name = "GG_POLICE";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = Vector3.one;

                OrientForward(body, player);
                NormalizeHeightAndFeet(body, player, targetHeight);
                StripNonCharacterComponents(body);
                EnsureUrpMaterial(body);

                // Чуть отодвигаем тело назад, чтобы шея/грудь не попадали
                // в камеру при взгляде вниз. Ноги при этом остаются видны.
                body.transform.localPosition += new Vector3(0f, 0f, -0.08f);

                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }

                Animator animator = body.GetComponent<Animator>();
                if (animator == null)
                    animator = body.AddComponent<Animator>();

                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PlayerMotor motor = player.GetComponent<PlayerMotor>();
                float ratio = 1.625f;
                if (motor != null)
                {
                    var motorObject = new SerializedObject(motor);
                    SerializedProperty walkSpeed = motorObject.FindProperty("walkSpeed");
                    SerializedProperty sprintSpeed = motorObject.FindProperty("sprintSpeed");
                    if (walkSpeed != null && sprintSpeed != null && walkSpeed.floatValue > 0.01f)
                        ratio = sprintSpeed.floatValue / walkSpeed.floatValue;
                }

                FirstPersonBody firstPersonBody = body.AddComponent<FirstPersonBody>();
                var bodyObject = new SerializedObject(firstPersonBody);
                bodyObject.FindProperty("animator").objectReferenceValue = animator;
                bodyObject.FindProperty("motor").objectReferenceValue = motor;
                bodyObject.FindProperty("sprintToWalkRatio").floatValue = ratio;
                bodyObject.ApplyModifiedPropertiesWithoutUndo();

                PlayerLook look = player.GetComponent<PlayerLook>();
                if (look != null)
                {
                    var lookObject = new SerializedObject(look);
                    SerializedProperty minPitch = lookObject.FindProperty("minPitch");
                    SerializedProperty maxPitch = lookObject.FindProperty("maxPitch");
                    if (minPitch != null)
                        minPitch.floatValue = -85f;
                    if (maxPitch != null)
                        maxPitch.floatValue = 30f;
                    lookObject.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
                Debug.Log($"[FPSetup] Тело GG_POLICE добавлено в {PlayerPrefabPath} (рост {targetHeight:0.00} м, ratio {ratio:0.000}).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
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

        private static void OrientForward(GameObject body, GameObject player)
        {
            Transform foot = FindBone(body.transform, "LeftFoot");
            Transform toe = FindBone(body.transform, "LeftToeBase") ?? FindBone(body.transform, "LeftToe_End");
            if (foot == null || toe == null)
                return;

            Vector3 toeDirection = toe.position - foot.position;
            if (Vector3.Dot(toeDirection, player.transform.forward) < 0f)
            {
                body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log("[FPSetup] GG_POLICE развёрнут на 180°.");
            }
        }

        private static void NormalizeHeightAndFeet(GameObject body, GameObject player, float targetHeight)
        {
            Bounds raw = MeasureBounds(body);
            float scale = raw.size.y > 0.01f ? targetHeight / raw.size.y : 1f;
            if (scale < 0.05f || scale > 20f)
                scale = 1f;

            body.transform.localScale = Vector3.one * scale;

            Bounds bounds = MeasureBounds(body);
            Vector3 centerLocal = player.transform.InverseTransformPoint(bounds.center);
            Vector3 bottomLocal = player.transform.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));

            body.transform.localPosition = new Vector3(-centerLocal.x, -bottomLocal.y, -centerLocal.z);
        }

        private static void StripNonCharacterComponents(GameObject body)
        {
            foreach (Camera camera in body.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
                camera.gameObject.SetActive(false);
            }

            foreach (Light light in body.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(light);

            foreach (AudioListener listener in body.GetComponentsInChildren<AudioListener>(true))
                Object.DestroyImmediate(listener);

            foreach (Animator animator in body.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);

            foreach (Animation animation in body.GetComponentsInChildren<Animation>(true))
                Object.DestroyImmediate(animation);

            foreach (SkinnedMeshRenderer skinned in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.updateWhenOffscreen = true;
        }

        private static void EnsureUrpMaterial(GameObject body)
        {
            Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
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
                        if (texture == null && material.HasProperty("_MainTex"))
                            texture = material.GetTexture("_MainTex");
                        if (texture == null && material.HasProperty("_BaseMap"))
                            texture = material.GetTexture("_BaseMap");
                    }
                }
            }

            if (!needsFix)
                return;

            EnsureFolder(MaterialFolder);
            Material urpMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (urpMaterial == null)
            {
                urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(urpMaterial, MaterialPath);
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
