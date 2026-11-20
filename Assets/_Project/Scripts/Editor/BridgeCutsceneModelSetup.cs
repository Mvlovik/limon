using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using GameLimon.Story;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Replaces the scene-4 blockout actors with the rigged human models from
    /// Assets/_Project/Art/chelikiуу. The models are posed once from GG_Idle so
    /// the cutscene has believable standing/hostage silhouettes without adding
    /// another animator controller to every extra.
    /// </summary>
    public static class BridgeCutsceneModelSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/02_Act1/A1_S04_UnderBridge.unity";
        const string ModelRoot = "Assets/_Project/Art/chelikiуу/Models/";
        const string RustamModel = ModelRoot + "RUSTEM.fbx";
        const string ThugAModel = ModelRoot + "DJO_KURTKA.fbx";
        const string ThugBModel = ModelRoot + "GG_KURTKA_SH.fbx";
        const string IdleClipPath = "Assets/_Project/Art/Animations/GG/GG_Idle.anim";
        const string IdleControllerPath = "Assets/_Project/Art/Animations/GG/GG.controller";

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var cast = FindInScene(scene, "Cast");
            if (cast == null) { Debug.LogError("[BridgeModels] Cast not found"); return; }

            var controller = FindComponent<RustamBridgeCutscene>(scene, "RustamCutscene");
            if (controller == null) { Debug.LogError("[BridgeModels] RustamBridgeCutscene not found"); return; }

            var so = new SerializedObject(controller);
            var rustamStanding = so.FindProperty("rustamStanding");
            var thugAStanding = so.FindProperty("thugAStanding");
            var thugBStanding = so.FindProperty("thugBStanding");
            var rustamBody = so.FindProperty("rustamBody");
            var thugABody = so.FindProperty("thugABody");
            var thugBBody = so.FindProperty("thugBBody");
            var pistolProp = so.FindProperty("pistolInThugHand");

            Vector3 rustamPos = new Vector3(-17.7f, GroundY(new Vector3(-17.7f, 0f, 32.0f)) + 0.02f, 32.0f);
            Quaternion rustamRot = LookAtFlat(rustamPos, new Vector3(-14.8f, 0f, 29.0f));
            Vector3 thugAPos = new Vector3(-16.4f, GroundY(new Vector3(-16.4f, 0f, 31.1f)) + 0.02f, 31.1f);
            Quaternion thugARot = LookAtFlat(thugAPos, new Vector3(-17.7f, 0f, 32.0f));
            Vector3 thugBPos = new Vector3(-18.8f, GroundY(new Vector3(-18.8f, 0f, 31.4f)) + 0.02f, 31.4f);
            Quaternion thugBRot = LookAtFlat(thugBPos, new Vector3(-17.7f, 0f, 32.0f));

            Vector3 rustamBodyPos = new Vector3(-17.2f, GroundY(new Vector3(-17.2f, 0f, 30.7f)) + 0.06f, 30.7f);
            Vector3 thugABodyPos = new Vector3(-16.0f, GroundY(new Vector3(-16.0f, 0f, 32.0f)) + 0.06f, 32.0f);
            Vector3 thugBBodyPos = new Vector3(-18.5f, GroundY(new Vector3(-18.5f, 0f, 31.7f)) + 0.06f, 31.7f);

            CaptureTransform(rustamStanding, ref rustamPos, ref rustamRot);
            CaptureTransform(thugAStanding, ref thugAPos, ref thugARot);
            CaptureTransform(thugBStanding, ref thugBPos, ref thugBRot);
            CapturePosition(rustamBody, ref rustamBodyPos);
            CapturePosition(thugABody, ref thugABodyPos);
            CapturePosition(thugBBody, ref thugBBodyPos);

            DestroyActor(rustamStanding);
            DestroyActor(thugAStanding);
            DestroyActor(thugBStanding);
            DestroyActor(rustamBody);
            DestroyActor(thugABody);
            DestroyActor(thugBBody);

            GameObject newRustam = SpawnModel("Rustam_Standing", RustamModel, cast.transform, rustamPos, rustamRot, false);
            GameObject newThugA = SpawnModel("ThugA_Standing", ThugAModel, cast.transform, thugAPos, thugARot, false);
            GameObject newThugB = SpawnModel("ThugB_Standing", ThugBModel, cast.transform, thugBPos, thugBRot, false, false);

            GameObject newRustamBody = SpawnModel("Rustam_Body", RustamModel, cast.transform, rustamBodyPos, Quaternion.Euler(90f, 80f, 0f), true);
            GameObject newThugABody = SpawnModel("ThugA_Body", ThugAModel, cast.transform, thugABodyPos, Quaternion.Euler(90f, -110f, 0f), true);
            GameObject newThugBBody = SpawnModel("ThugB_Body", ThugBModel, cast.transform, thugBBodyPos, Quaternion.Euler(90f, 150f, 0f), true);

            newRustamBody.SetActive(false);
            newThugABody.SetActive(false);
            newThugBBody.SetActive(false);

            so.FindProperty("rustamStanding").objectReferenceValue = newRustam;
            so.FindProperty("thugAStanding").objectReferenceValue = newThugA;
            so.FindProperty("thugBStanding").objectReferenceValue = newThugB;
            so.FindProperty("rustamBody").objectReferenceValue = newRustamBody;
            so.FindProperty("thugABody").objectReferenceValue = newThugABody;
            so.FindProperty("thugBBody").objectReferenceValue = newThugBBody;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (pistolProp != null && pistolProp.objectReferenceValue is GameObject pistol)
            {
                Transform hand = FindBone(newThugA.transform, "mixamorig:RightHand");
                if (hand != null)
                {
                    Vector3 aim = newRustam.transform.position + Vector3.up * 1.15f;
                    pistol.transform.position = hand.position + hand.forward * 0.08f + Vector3.up * 0.02f;
                    pistol.transform.rotation = Quaternion.LookRotation((aim - pistol.transform.position).normalized, Vector3.up);
                }
            }

            var cameraGo = FindInScene(scene, "RustamCutscene");
            var brain = cameraGo != null ? cameraGo.GetComponent<CinemachineBrain>() : null;
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BridgeModels] cast replaced with chelikiуу models");
        }

        static GameObject SpawnModel(string name, string assetPath, Transform parent, Vector3 position, Quaternion rotation, bool body, bool keepAnimator = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError("[BridgeModels] Missing model " + assetPath);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one;
            RemoveSceneHelpers(instance, keepAnimator);
            PoseFromIdle(instance);

            if (keepAnimator)
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            if (body)
                instance.transform.rotation = rotation;
            return instance;
        }

        static void RemoveSceneHelpers(GameObject root, bool keepAnimator)
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                if (camera != null) Object.DestroyImmediate(camera);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                if (listener != null) Object.DestroyImmediate(listener);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                if (collider != null) Object.DestroyImmediate(collider);
            if (!keepAnimator)
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    if (animator != null) Object.DestroyImmediate(animator);
        }

        static float GroundY(Vector3 worldPosition)
        {
            var terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(worldPosition) + terrain.transform.position.y : 0f;
        }

        static void PoseFromIdle(GameObject model)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
            if (clip == null) return;

            // GG_Idle curve paths begin with the GG_DEF root name. Rename the
            // instance only for sampling, then restore the scene name.
            string originalName = model.name;
            model.name = "GG_DEF";
            clip.SampleAnimation(model, 0.4f);
            model.name = originalName;
        }

        static void CaptureTransform(SerializedProperty property, ref Vector3 position, ref Quaternion rotation)
        {
            if (property != null && property.objectReferenceValue is GameObject go && go != null)
            {
                position = go.transform.position;
                rotation = go.transform.rotation;
            }
        }

        static void CapturePosition(SerializedProperty property, ref Vector3 position)
        {
            if (property != null && property.objectReferenceValue is GameObject go && go != null)
                position = go.transform.position;
        }

        static void DestroyActor(SerializedProperty property)
        {
            if (property != null && property.objectReferenceValue is GameObject go && go != null)
                Object.DestroyImmediate(go);
        }

        static Transform FindBone(Transform root, string boneName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            return null;
        }

        static Quaternion LookAtFlat(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        }

        static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        static T FindComponent<T>(UnityEngine.SceneManagement.Scene scene, string name) where T : Component
        {
            var go = FindInScene(scene, name);
            return go != null ? go.GetComponent<T>() : null;
        }
    }
}
