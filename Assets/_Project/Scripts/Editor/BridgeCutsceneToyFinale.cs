using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using GameLimon.Story;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Final pass for the bridge cutscene: replaces the blockout toy with the
    /// real wooden pull-toy prop and extends the finale camera move so it flies
    /// from the close-up of Danil to a close-up of the toy.
    /// </summary>
    public static class BridgeCutsceneToyFinale
    {
        const string ScenePath = "Assets/_Project/Scenes/02_Act1/A1_S04_UnderBridge.unity";
        const string ToyPrefabPath = "Assets/_Project/Art/Environment/Shared/Props/Toy/Prefabs/WoodToy2.prefab";
        const string FinaleClipPath = "Assets/_Project/Data/Cutscenes/Clips/CUT_A1S04_FinaleCamera.anim";
        const string FinaleTimelinePath = "Assets/_Project/Data/Cutscenes/A1_S04_Bridge_Finale.playable";

        const float FinaleDuration = 12f;
        const float FaceRevealTime = 4.5f;
        const float TurnToToyTime = 7f;
        const float FlyMidTime = 9.5f;
        const float ToyNearTime = 11.0f;

        [MenuItem("GameLimon/Act 1/Apply Toy Finale To Bridge Cutscene")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var finaleRoot = Find(scene, "FinaleProps");
            if (finaleRoot == null)
            {
                Debug.LogError("[ToyFinale] FinaleProps not found");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToyPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[ToyFinale] Missing toy prefab " + ToyPrefabPath);
                return;
            }

            var oldBlockout = finaleRoot.transform.Find("ChildToy_Car");
            if (oldBlockout != null)
                Object.DestroyImmediate(oldBlockout.gameObject);

            var previousToy = finaleRoot.transform.Find("ChildToy");
            if (previousToy != null)
                Object.DestroyImmediate(previousToy.gameObject);

            var toy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, finaleRoot.transform);
            toy.name = "ChildToy";
            toy.transform.localPosition = new Vector3(-14.70f, 0.004f, 30.00f);
            toy.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            toy.transform.localScale = Vector3.one * 0.04f;

            var hand = finaleRoot.transform.Find("ChildHand");
            if (hand != null)
            {
                hand.localPosition = new Vector3(-14.92f, 0.09f, 30.20f);
                hand.localRotation = Quaternion.Euler(90f, 200f, 0f);
            }

            var blood = finaleRoot.transform.Find("Blood_Finale");
            if (blood != null)
            {
                blood.localPosition = new Vector3(-15.02f, 0.015f, 30.15f);
                blood.localRotation = Quaternion.identity;
                blood.localScale = new Vector3(0.068f, 1f, 0.048f);
            }

            WireController(scene);
            PatchFinaleCamera(scene, toy);
            PatchFinaleTimeline();
            finaleRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ToyFinale] toy prop and camera fly-to applied");
        }

        static void WireController(Scene scene)
        {
            var cameraGo = Find(scene, "RustamCutscene");
            var controller = cameraGo != null ? cameraGo.GetComponent<RustamBridgeCutscene>() : null;
            if (controller == null)
                return;

            var so = new SerializedObject(controller);
            SetObject(so, "thugAStanding", Find(scene, "ThugA_Standing"));
            SetObject(so, "banditStanding", Find(scene, "Modern Rebel Soldier Character"));
            SetObject(so, "pistolInThugHand", Find(scene, "beretta_92fs"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PatchFinaleCamera(Scene scene, GameObject toy)
        {
            var camera = Find(scene, "CM_Shot_Finale");
            if (camera == null || toy == null)
                return;

            var toyCenter = GetBounds(toy).center;
            var parentOffset = camera.transform.parent != null ? camera.transform.parent.position : Vector3.zero;
            // Tight close-up: the camera sits to the right/front of the toy, so the
            // toy stays centered and the hand/blood stay out of the frame.
            var finalWorld = toyCenter + new Vector3(0.30f, 0.28f, 0.52f);
            var rotation = Quaternion.LookRotation((toyCenter - finalWorld).normalized, Vector3.up);

            camera.transform.SetPositionAndRotation(finalWorld, rotation);
            var cinemachine = camera.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            if (cinemachine != null)
                cinemachine.Lens.FieldOfView = 36f;

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FinaleClipPath);
            if (clip == null)
            {
                Debug.LogWarning("[ToyFinale] Missing finale camera clip");
                return;
            }

            float ground = GroundY(new Vector3(-14.2f, 0f, 27.5f));
            var startWorld = new Vector3(-14.2f, ground + 0.30f, 26.6f);
            var faceWorld = new Vector3(-14.2f, ground + 1.63f, 27.25f);
            var feetTarget = new Vector3(-14.2f, ground + 0.10f, 28.8f);
            var faceTarget = new Vector3(-14.2f, ground + 1.66f, 28.8f);
            var toyTarget = toyCenter + Vector3.up * 0.01f;

            var start = startWorld - parentOffset;
            var face = faceWorld - parentOffset;
            var final = finalWorld - parentOffset;

            // Two mid keys make the flight a real dolly move: first towards the
            // toy, then a short drift that reads like a handheld push-in.
            var mid = Vector3.Lerp(face, final, 0.55f);
            var near = Vector3.Lerp(face, final, 0.82f);

            Quaternion q0 = Quaternion.LookRotation((feetTarget - startWorld).normalized, Vector3.up);
            Quaternion qFace = Quaternion.LookRotation((faceTarget - faceWorld).normalized, Vector3.up);
            Quaternion qFaceToToy = Quaternion.LookRotation((toyTarget - faceWorld).normalized, Vector3.up);
            Quaternion qMid = Quaternion.LookRotation((toyTarget - (mid + parentOffset)).normalized, Vector3.up);
            Quaternion qNear = Quaternion.LookRotation((toyTarget - (near + parentOffset)).normalized, Vector3.up);
            Quaternion qFinal = Quaternion.LookRotation((toyTarget - finalWorld).normalized, Vector3.up);

            SetCurve(clip, "m_LocalPosition.x",
                new[] { 0f, FaceRevealTime, TurnToToyTime, FlyMidTime, ToyNearTime, FinaleDuration },
                new[] { start.x, face.x, face.x, mid.x, near.x, final.x });
            SetCurve(clip, "m_LocalPosition.y",
                new[] { 0f, FaceRevealTime, TurnToToyTime, FlyMidTime, ToyNearTime, FinaleDuration },
                new[] { start.y, face.y, face.y, mid.y, near.y, final.y });
            SetCurve(clip, "m_LocalPosition.z",
                new[] { 0f, FaceRevealTime, TurnToToyTime, FlyMidTime, ToyNearTime, FinaleDuration },
                new[] { start.z, face.z, face.z, mid.z, near.z, final.z });

            SetQuaternionCurves(clip,
                new[] { 0f, FaceRevealTime, 5.3f, 6.1f, TurnToToyTime, FlyMidTime, ToyNearTime, FinaleDuration },
                new[]
                {
                    q0,
                    qFace,
                    qFaceToToy * Quaternion.Euler(0.7f, 0.4f, 0.3f),
                    qFaceToToy * Quaternion.Euler(-0.5f, -0.3f, -0.2f),
                    qFaceToToy,
                    qMid,
                    qNear,
                    qFinal
                });

            EditorUtility.SetDirty(clip);
        }

        static void PatchFinaleTimeline()
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(FinaleTimelinePath);
            if (timeline == null)
            {
                Debug.LogWarning("[ToyFinale] Missing finale timeline");
                return;
            }

            foreach (var track in timeline.GetRootTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    clip.duration = FinaleDuration;
                    if (clip.asset is AnimationPlayableAsset animation)
                    {
                        var so = new SerializedObject(animation);
                        var removeStart = so.FindProperty("m_RemoveStartOffset");
                        if (removeStart != null)
                        {
                            removeStart.boolValue = false;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }

                        EditorUtility.SetDirty(animation);
                    }
                }
            }

            EditorUtility.SetDirty(timeline);
        }

        static void SetCurve(AnimationClip clip, string property, float[] times, float[] values)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
                keys[i] = new Keyframe(times[i], values[i]);

            var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), property);
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }

        static void SetQuaternionCurves(AnimationClip clip, float[] times, Quaternion[] values)
        {
            var quaternions = new Quaternion[values.Length];
            for (int i = 0; i < values.Length; i++)
                quaternions[i] = values[i].normalized;

            for (int i = 1; i < quaternions.Length; i++)
            {
                if (Quaternion.Dot(quaternions[i - 1], quaternions[i]) < 0f)
                {
                    var q = quaternions[i];
                    quaternions[i] = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                }
            }

            SetCurve(clip, "m_LocalRotation.x", times, System.Array.ConvertAll(quaternions, q => q.x));
            SetCurve(clip, "m_LocalRotation.y", times, System.Array.ConvertAll(quaternions, q => q.y));
            SetCurve(clip, "m_LocalRotation.z", times, System.Array.ConvertAll(quaternions, q => q.z));
            SetCurve(clip, "m_LocalRotation.w", times, System.Array.ConvertAll(quaternions, q => q.w));
            clip.EnsureQuaternionContinuity();
        }

        static Bounds GetBounds(GameObject root)
        {
            bool first = true;
            Bounds bounds = default;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        static float GroundY(Vector3 worldPosition)
        {
            var terrain = Terrain.activeTerrain;
            return terrain != null
                ? terrain.SampleHeight(worldPosition) + terrain.transform.position.y
                : 0f;
        }

        static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            var property = so.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                        return transform.gameObject;
                }
            }

            return null;
        }
    }
}
