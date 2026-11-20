using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using GameLimon.Story;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Final blockout pass: grounds the cast, clusters it around the support
    /// pillar and updates the Cinemachine camera positions plus their Timeline
    /// camera-move clips. Keeps the cutscene editable in one place.
    /// </summary>
    public static class BridgeCutsceneStagingPolish
    {
        const string ScenePath = "Assets/_Project/Scenes/02_Act1/A1_S04_UnderBridge.unity";

        public static void Apply()
        {
            // Reopen the saved scene so temporary preview moves never stick.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var terrain = Terrain.activeTerrain;
            float Ground(float x, float z) => terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : 0f;

            Vector3 rustamPos = new Vector3(-15.7f, Ground(-15.7f, 31.7f) + 0.02f, 31.7f);
            Vector3 thugAPos = new Vector3(-14.7f, Ground(-14.7f, 30.8f) + 0.02f, 30.8f);
            Vector3 thugBPos = new Vector3(-16.4f, Ground(-16.4f, 30.7f) - 0.35f, 30.7f);

            // Rustam faces the player/camera; the thugs close in from both sides.
            Quaternion rustamRot = LookAtFlat(rustamPos, new Vector3(-15.7f, 0f, 28.8f));
            Quaternion thugARot = LookAtFlat(thugAPos, rustamPos);
            Quaternion thugBRot = LookAtFlat(thugBPos, rustamPos);

            var rustam = FindInScene(scene, "Rustam_Standing");
            var thugA = FindInScene(scene, "ThugA_Standing");
            var thugB = FindInScene(scene, "ThugB_Standing");
            SetTransform(rustam, rustamPos, rustamRot);
            SetTransform(thugA, thugAPos, thugARot);
            SetTransform(thugB, thugBPos, thugBRot);

            var pistol = FindInScene(scene, "PistolInHand");
            Transform hand = thugA != null ? FindBone(thugA.transform, "mixamorig:RightHand") : null;
            if (pistol != null && hand != null)
            {
                Vector3 aim = rustamPos + Vector3.up * 1.25f;
                pistol.transform.position = hand.position + hand.forward * 0.08f;
                pistol.transform.rotation = Quaternion.LookRotation((aim - pistol.transform.position).normalized, Vector3.up);
            }

            // Dead cast lies around the support.
            SetBody("Rustam_Body", new Vector3(-15.9f, Ground(-15.9f, 30.6f) + 0.06f, 30.6f), 62f);
            SetBody("ThugA_Body", new Vector3(-14.3f, Ground(-14.3f, 31.3f) + 0.06f, 31.3f), -110f);
            SetBody("ThugB_Body", new Vector3(-17.0f, Ground(-17.0f, 31.2f) + 0.06f, 31.2f), 150f);

            Move("Blood_Rustam", new Vector3(-15.9f, Ground(-15.9f, 30.5f) + 0.02f, 30.5f));
            Move("Blood_ThugA", new Vector3(-14.4f, Ground(-14.4f, 31.2f) + 0.02f, 31.2f));
            Move("Blood_ThugB", new Vector3(-16.9f, Ground(-16.9f, 31.1f) + 0.02f, 31.1f));

            // Player / Joe marks.
            var playerIntro = FindInScene(scene, "PlayerIntro");
            var joeIntro = FindInScene(scene, "JoeIntro");
            var playerFinal = FindInScene(scene, "PlayerFinal");
            if (playerIntro != null)
                playerIntro.transform.SetPositionAndRotation(
                    new Vector3(-9.0f, Ground(-9.0f, 23.4f) + 0.02f, 23.4f),
                    LookAtFlat(new Vector3(-9.0f, 0f, 23.4f), new Vector3(-15.3f, 0f, 30.8f)));
            if (joeIntro != null)
                joeIntro.transform.SetPositionAndRotation(
                    new Vector3(-10.0f, Ground(-10.0f, 23.1f) + 0.02f, 23.1f),
                    LookAtFlat(new Vector3(-10.0f, 0f, 23.1f), new Vector3(-15.3f, 0f, 30.8f)));
            if (playerFinal != null)
                playerFinal.transform.SetPositionAndRotation(
                    new Vector3(-14.2f, Ground(-14.2f, 28.8f) + 0.02f, 28.8f),
                    LookAtFlat(new Vector3(-14.2f, 0f, 28.8f), new Vector3(-14.2f, 0f, 26.6f)));

            var finaleProps = FindInScene(scene, "FinaleProps");
            if (finaleProps != null)
            {
                finaleProps.transform.position = new Vector3(0f, Ground(-13.8f, 25.0f), 0f);

                var blood = finaleProps.transform.Find("Blood_Finale");
                if (blood != null)
                    blood.localScale = new Vector3(0.55f, 0.004f, 0.38f);

                // The reveal is intentionally dark; a point light here blooms
                // against the near wall and hides the silent-film composition.
                var lightTransform = finaleProps.transform.Find("FinaleRevealLight");
                if (lightTransform != null)
                    Object.DestroyImmediate(lightTransform.gameObject);
            }

            // Cameras. Values mirror the Timeline camera-move clips below.
            SetCamera(scene, "CM_Shot_Approach", new Vector3(-10.8f, 2.10f, 22.0f), new Vector3(-15.3f, 1.65f, 30.6f), 44f);
            SetCamera(scene, "CM_Shot_Nook", new Vector3(-12.5f, 2.00f, 27.6f), new Vector3(-15.5f, 1.50f, 31.2f), 36f);
            SetCamera(scene, "CM_Shot_Rustam", new Vector3(-15.7f, 1.78f, 29.5f), new Vector3(-15.7f, 1.66f, 31.7f), 32f);
            SetCamera(scene, "CM_Shot_Wait", new Vector3(-12.2f, 2.00f, 27.3f), new Vector3(-15.5f, 1.50f, 31.2f), 36f);
            SetCamera(scene, "CM_Shot_Aftermath", new Vector3(-14.2f, 2.80f, 28.8f), new Vector3(-15.8f, 0.50f, 31.0f), 48f);
            SetCamera(scene, "CM_Shot_Death", new Vector3(-14.8f, 1.30f, 29.7f), new Vector3(-15.7f, 0.50f, 31.0f), 36f);
            SetCamera(scene, "CM_Shot_Finale", new Vector3(-14.2f, 0.35f, 26.6f), new Vector3(-14.2f, 0.12f, 28.8f), 42f);

            // The old point fill lit the whole wall and bloomed into the reveal.
            // A narrow spot behind the close-up camera lights only the face.
            var fillGo = FindInScene(scene, "Finale_Fill");
            if (fillGo != null)
            {
                var fill = fillGo.GetComponent<Light>();
                if (fill != null)
                {
                    fill.type = LightType.Spot;
                    fill.color = new Color(1f, 0.78f, 0.58f);
                    fill.intensity = 2.0f;
                    fill.range = 5.5f;
                    fill.spotAngle = 52f;
                    fill.shadows = LightShadows.Soft;
                }
                fillGo.transform.position = new Vector3(-14.2f, 2.4f, 27.35f);
                fillGo.transform.LookAt(new Vector3(-14.2f, 1.45f, 28.8f));
            }

            // Camera-move clips.
            var nookStart = new Vector3(-12.5f, 2.00f, 27.6f);
            var nookEnd = new Vector3(-13.7f, 1.85f, 29.2f);
            var nookTarget = new Vector3(-15.5f, 1.50f, 31.2f);
            PatchCameraClip("CUT_A1S04_NookPushIn", 0f, 3.4f, nookStart, nookEnd, nookTarget);

            var waitStart = new Vector3(-12.2f, 2.00f, 27.3f);
            var waitEnd = new Vector3(-13.6f, 1.80f, 29.3f);
            PatchCameraClip("CUT_A1S04_WaitPushIn", 0f, 5.5f, waitStart, waitEnd, nookTarget);

            PatchFinaleClip();
            PersistAbsoluteCameraMotion();

            var cameraGo = FindInScene(scene, "RustamCutscene");
            var brain = cameraGo != null ? cameraGo.GetComponent<CinemachineBrain>() : null;
            if (brain != null)
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BridgePolish] staging, marks and camera clips updated");
        }

        static void PersistAbsoluteCameraMotion()
        {
            foreach (var path in new[]
            {
                "Assets/_Project/Data/Cutscenes/A1_S04_Bridge_Intro.playable",
                "Assets/_Project/Data/Cutscenes/A1_S04_Bridge_BranchA.playable",
                "Assets/_Project/Data/Cutscenes/A1_S04_Bridge_Finale.playable"
            })
            {
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (timeline == null) continue;
                bool changed = false;
                foreach (var track in timeline.GetRootTracks())
                {
                    if (!(track is AnimationTrack animTrack)) continue;
                    if (!animTrack.name.StartsWith("CAM_")) continue;
                    foreach (var clip in animTrack.GetClips())
                    {
                        if (!(clip.asset is AnimationPlayableAsset apa)) continue;
                        var so = new SerializedObject(apa);
                        var prop = so.FindProperty("m_RemoveStartOffset");
                        if (prop == null || !prop.boolValue) continue;
                        prop.boolValue = false;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(apa);
                        changed = true;
                    }
                }
                if (changed) EditorUtility.SetDirty(timeline);
            }
        }

        static void PatchCameraClip(string name, float startTime, float duration, Vector3 startPos, Vector3 endPos, Vector3 target)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Data/Cutscenes/Clips/" + name + ".anim");
            if (clip == null) { Debug.LogWarning("[BridgePolish] missing clip " + name); return; }
            Quaternion startRot = Quaternion.LookRotation((target - startPos).normalized, Vector3.up);
            Quaternion endRot = Quaternion.LookRotation((target - endPos).normalized, Vector3.up);
            SetVectorClipCurves(clip, startTime, startTime + duration, startPos, endPos);
            SetQuaternionClipCurves(clip, new[] { startTime, startTime + duration }, new[] { startRot, endRot });
            EditorUtility.SetDirty(clip);
        }

        static void PatchFinaleClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Data/Cutscenes/Clips/CUT_A1S04_FinaleCamera.anim");
            if (clip == null) { Debug.LogWarning("[BridgePolish] missing finale clip"); return; }

            var ground = Terrain.activeTerrain;
            float g = ground != null ? ground.SampleHeight(new Vector3(-14.2f, 0f, 27.5f)) + ground.transform.position.y : 0f;
            Vector3 startPos = new Vector3(-14.2f, g + 0.30f, 26.6f);
            Vector3 facePos = new Vector3(-14.2f, g + 1.63f, 27.25f);
            Vector3 feetTarget = new Vector3(-14.2f, g + 0.10f, 28.8f);
            Vector3 faceTarget = new Vector3(-14.2f, g + 1.66f, 28.8f);
            Vector3 handTarget = new Vector3(-14.55f, g + 0.38f, 24.9f);

            Quaternion q0 = Quaternion.LookRotation((feetTarget - startPos).normalized, Vector3.up);
            Quaternion q1 = Quaternion.LookRotation((faceTarget - facePos).normalized, Vector3.up);
            Quaternion q2 = Quaternion.LookRotation((handTarget - facePos).normalized, Vector3.up);

            SetCurve(clip, "m_LocalPosition.x", new[] { 0f, 4.5f, 9.5f }, new[] { startPos.x, facePos.x, facePos.x });
            SetCurve(clip, "m_LocalPosition.y", new[] { 0f, 4.5f, 9.5f }, new[] { startPos.y, facePos.y, facePos.y });
            SetCurve(clip, "m_LocalPosition.z", new[] { 0f, 4.5f, 9.5f }, new[] { startPos.z, facePos.z, facePos.z });
            SetQuaternionClipCurves(clip, new[] { 0f, 4.5f, 5.3f, 6.1f, 7.0f, 9.5f },
                new[] { q0, q1, q2 * Quaternion.Euler(0.7f, 0.4f, 0.3f), q2 * Quaternion.Euler(-0.5f, -0.3f, -0.2f), q2, q2 });
            EditorUtility.SetDirty(clip);
        }

        static void SetVectorClipCurves(AnimationClip clip, float startTime, float endTime, Vector3 start, Vector3 end)
        {
            SetCurve(clip, "m_LocalPosition.x", new[] { startTime, endTime }, new[] { start.x, end.x });
            SetCurve(clip, "m_LocalPosition.y", new[] { startTime, endTime }, new[] { start.y, end.y });
            SetCurve(clip, "m_LocalPosition.z", new[] { startTime, endTime }, new[] { start.z, end.z });
        }

        static void SetQuaternionClipCurves(AnimationClip clip, float[] times, Quaternion[] values)
        {
            var qs = new Quaternion[values.Length];
            for (int i = 0; i < values.Length; i++) qs[i] = values[i].normalized;
            for (int i = 1; i < qs.Length; i++)
                if (Quaternion.Dot(qs[i - 1], qs[i]) < 0f)
                    qs[i] = new Quaternion(-qs[i].x, -qs[i].y, -qs[i].z, -qs[i].w);

            SetCurve(clip, "m_LocalRotation.x", times, System.Array.ConvertAll(qs, q => q.x));
            SetCurve(clip, "m_LocalRotation.y", times, System.Array.ConvertAll(qs, q => q.y));
            SetCurve(clip, "m_LocalRotation.z", times, System.Array.ConvertAll(qs, q => q.z));
            SetCurve(clip, "m_LocalRotation.w", times, System.Array.ConvertAll(qs, q => q.w));
            clip.EnsureQuaternionContinuity();
        }

        static void SetCurve(AnimationClip clip, string property, float[] times, float[] values)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++) keys[i] = new Keyframe(times[i], values[i]);
            var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), property);
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }

        static void SetBody(string name, Vector3 position, float yaw)
        {
            var go = FindInScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), name);
            if (go != null)
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, yaw, 0f));
        }

        static void Move(string name, Vector3 position)
        {
            var go = FindInScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), name);
            if (go != null) go.transform.position = position;
        }

        static void SetCamera(UnityEngine.SceneManagement.Scene scene, string name, Vector3 position, Vector3 target, float fov)
        {
            var go = FindInScene(scene, name);
            if (go == null) { Debug.LogWarning("[BridgePolish] missing camera " + name); return; }
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
            var cm = go.GetComponent<CinemachineCamera>();
            if (cm != null) cm.Lens.FieldOfView = fov;
        }

        static void SetTransform(GameObject go, Vector3 position, Quaternion rotation)
        {
            if (go != null) go.transform.SetPositionAndRotation(position, rotation);
        }

        static Quaternion LookAtFlat(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from; dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        }

        static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        static Transform FindBone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
