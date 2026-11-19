using System.Collections.Generic;
using System.IO;
using GameLimon.Companions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameLimon.EditorTools
{
    /// <summary>
    /// Собирает для Joe настоящие анимационные клипы (.anim) и AnimatorController:
    /// Joe_Idle — дыхание/перенос веса/повороты головы, Joe_Walk — шаг с коленями,
    /// локтями, наклоном и покачиванием корпуса.
    ///
    /// Меню: Tools → GameLimon → Rebuild Joe Animation (Clips + Animator).
    /// Повторный запуск пересоздаёт клипы и контроллер и перенастраивает Joe.
    /// </summary>
    public static class JoeAnimationSetup
    {
        private const string Folder = "Assets/_Project/Art/Animations/Joe";
        private const string WalkPath = Folder + "/Joe_Walk.anim";
        private const string IdlePath = Folder + "/Joe_Idle.anim";
        private const string ControllerPath = Folder + "/Joe.controller";
        private const string ProceduralScriptPath = "Assets/_Project/Scripts/Companions/CompanionWalkAnimator.cs";

        private const string Body = "Body";
        private const string LeftLeg = "Body/LeftLeg";
        private const string RightLeg = "Body/RightLeg";
        private const string LeftKnee = "Body/LeftLeg/Knee";
        private const string RightKnee = "Body/RightLeg/Knee";
        private const string LeftArm = "Body/LeftArm";
        private const string RightArm = "Body/RightArm";
        private const string LeftElbow = "Body/LeftArm/Elbow";
        private const string RightElbow = "Body/RightArm/Elbow";
        private const string Head = "Body/Head";

        private const string MenuPath = "Tools/GameLimon/Rebuild Joe Animation (Clips + Animator)";

        private struct BasePose
        {
            public Vector3 bodyPosition;
            public Vector3 bodyEuler;
            public Vector3 leftLeg;
            public Vector3 rightLeg;
            public Vector3 leftKnee;
            public Vector3 rightKnee;
            public Vector3 leftArm;
            public Vector3 rightArm;
            public Vector3 leftElbow;
            public Vector3 rightElbow;
            public Vector3 head;
        }

        [MenuItem(MenuPath)]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[JoeAnimationSetup] Выйдите из Play Mode и повторите.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Run();
        }

        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject joe = FindJoe(scene);

            if (joe == null)
            {
                Debug.LogError("[JoeAnimationSetup] В открытой сцене нет объекта Joe.");
                return;
            }

            EnsureFolder(Folder);

            BasePose pose = CaptureBasePose(joe.transform);
            AnimationClip walk = BuildWalkClip(pose);
            AnimationClip idle = BuildIdleClip(pose);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath) != null)
                AssetDatabase.DeleteAsset(WalkPath);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath) != null)
                AssetDatabase.DeleteAsset(IdlePath);

            AssetDatabase.CreateAsset(walk, WalkPath);
            AssetDatabase.CreateAsset(idle, IdlePath);
            AssetDatabase.SaveAssets();

            AnimatorController controller = BuildController(walk, idle);
            ConfigureJoe(joe, controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[JoeAnimationSetup] Готово: " + WalkPath + ", " + IdlePath + ", " + ControllerPath);
        }

        private static GameObject FindJoe(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // Joe живёт в группе _Characters, поэтому ищем и среди потомков.
                Transform found = FindDeep(root.transform, "Joe");
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;

            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static BasePose CaptureBasePose(Transform root)
        {
            Transform body = root.Find(Body);
            Transform leftLeg = root.Find(LeftLeg);
            Transform rightLeg = root.Find(RightLeg);
            Transform leftKnee = root.Find(LeftKnee);
            Transform rightKnee = root.Find(RightKnee);
            Transform leftArm = root.Find(LeftArm);
            Transform rightArm = root.Find(RightArm);
            Transform leftElbow = root.Find(LeftElbow);
            Transform rightElbow = root.Find(RightElbow);
            Transform head = root.Find(Head);

            return new BasePose
            {
                bodyPosition = body != null ? body.localPosition : Vector3.zero,
                bodyEuler = Euler(body),
                leftLeg = Euler(leftLeg),
                rightLeg = Euler(rightLeg),
                leftKnee = Euler(leftKnee),
                rightKnee = Euler(rightKnee),
                leftArm = Euler(leftArm),
                rightArm = Euler(rightArm),
                leftElbow = Euler(leftElbow),
                rightElbow = Euler(rightElbow),
                head = Euler(head)
            };
        }

        private static AnimationClip BuildWalkClip(BasePose pose)
        {
            const float length = 0.75f;
            const int steps = 16;

            var curves = new CurveSet();

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float time = t * length;
                float phase = t * Mathf.PI * 2f;

                float swing = Mathf.Sin(phase);
                float cosine = Mathf.Cos(phase);

                float leftKnee = Mathf.Max(0f, -cosine) * 34f;
                float rightKnee = Mathf.Max(0f, cosine) * 34f;

                float leftElbowFlex = 2f + Mathf.Max(0f, swing) * 12f;
                float rightElbowFlex = 2f + Mathf.Max(0f, -swing) * 12f;

                AddEuler(curves, LeftLeg, pose.leftLeg, swing * 26f, 0f, 0f, time);
                AddEuler(curves, RightLeg, pose.rightLeg, -swing * 26f, 0f, 0f, time);
                AddEuler(curves, LeftKnee, pose.leftKnee, leftKnee, 0f, 0f, time);
                AddEuler(curves, RightKnee, pose.rightKnee, rightKnee, 0f, 0f, time);

                AddEuler(curves, LeftArm, pose.leftArm, -swing * 18f, 0f, 0f, time);
                AddEuler(curves, RightArm, pose.rightArm, swing * 18f, 0f, 0f, time);
                AddEuler(curves, LeftElbow, pose.leftElbow, -leftElbowFlex, 0f, 0f, time);
                AddEuler(curves, RightElbow, pose.rightElbow, -rightElbowFlex, 0f, 0f, time);

                float bob = (0.5f - 0.5f * Mathf.Cos(2f * phase)) * 0.045f;
                float sway = swing * 0.025f;

                curves.Add(Body, "m_LocalPosition.x", time, pose.bodyPosition.x + sway);
                curves.Add(Body, "m_LocalPosition.y", time, pose.bodyPosition.y + bob);
                curves.Add(Body, "m_LocalPosition.z", time, pose.bodyPosition.z);

                AddEuler(curves, Body, pose.bodyEuler, 4f, 0f, 0f, time);

                AddEuler(curves, Head, pose.head,
                    -Mathf.Sin(2f * phase) * 1.6f,
                    Mathf.Sin(phase) * 1.2f,
                    0f,
                    time);
            }

            var clip = new AnimationClip { name = "Joe_Walk", frameRate = 30f };
            curves.ApplyTo(clip);
            EnableLoop(clip);
            return clip;
        }

        private static AnimationClip BuildIdleClip(BasePose pose)
        {
            const float length = 6f;
            const int steps = 24;

            var curves = new CurveSet();

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float time = t * length;
                float phase = t * Mathf.PI * 2f;

                float breath = Mathf.Sin(phase * 2f);
                float sway = Mathf.Sin(phase);

                AddEuler(curves, LeftLeg, pose.leftLeg, sway * 0.4f, 0f, 0f, time);
                AddEuler(curves, RightLeg, pose.rightLeg, -sway * 0.4f, 0f, 0f, time);
                AddEuler(curves, LeftKnee, pose.leftKnee, 0f, 0f, 0f, time);
                AddEuler(curves, RightKnee, pose.rightKnee, 0f, 0f, 0f, time);

                AddEuler(curves, LeftArm, pose.leftArm, breath * 2.5f, 0f, 0f, time);
                AddEuler(curves, RightArm, pose.rightArm, -breath * 2.5f, 0f, 0f, time);
                AddEuler(curves, LeftElbow, pose.leftElbow, -(3.5f + breath * 1.5f), 0f, 0f, time);
                AddEuler(curves, RightElbow, pose.rightElbow, -(3.5f + breath * 1.5f), 0f, 0f, time);

                curves.Add(Body, "m_LocalPosition.x", time, pose.bodyPosition.x + sway * 0.015f);
                curves.Add(Body, "m_LocalPosition.y", time, pose.bodyPosition.y + breath * 0.01f);
                curves.Add(Body, "m_LocalPosition.z", time, pose.bodyPosition.z);

                AddEuler(curves, Body, pose.bodyEuler, 0f, Mathf.Sin(phase) * 0.8f, 0f, time);

                AddEuler(curves, Head, pose.head,
                    Mathf.Sin(phase * 2f + 1.9f) * 3f,
                    Mathf.Sin(phase + 0.8f) * 7f,
                    0f,
                    time);
            }

            var clip = new AnimationClip { name = "Joe_Idle", frameRate = 30f };
            curves.ApplyTo(clip);
            EnableLoop(clip);
            return clip;
        }

        private static AnimatorController BuildController(AnimationClip walk, AnimationClip idle)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("PlaybackSpeed", AnimatorControllerParameterType.Float);

            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == "PlaybackSpeed")
                    parameter.defaultFloat = 1f;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            idleState.writeDefaultValues = true;

            AnimatorState walkState = machine.AddState("Walk");
            walkState.motion = walk;
            walkState.writeDefaultValues = true;
            walkState.speedParameterActive = true;
            walkState.speedParameter = "PlaybackSpeed";

            machine.defaultState = idleState;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.exitTime = 0f;
            toWalk.hasFixedDuration = true;
            toWalk.duration = 0.18f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.exitTime = 0f;
            toIdle.hasFixedDuration = true;
            toIdle.duration = 0.2f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void ConfigureJoe(GameObject joe, AnimatorController controller)
        {
            var animator = joe.GetComponent<Animator>();
            if (animator == null)
                animator = joe.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var actor = joe.GetComponent<CompanionActor>();
            if (actor != null)
            {
                var serialized = new SerializedObject(actor);
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.FindProperty("animatorSpeedParameter").stringValue = "Speed";
                serialized.FindProperty("animatorPlaybackParameter").stringValue = "PlaybackSpeed";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            // Процедурная анимация больше не нужна — только клипы.
            foreach (var behaviour in joe.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "CompanionWalkAnimator")
                    Object.DestroyImmediate(behaviour);
            }

            if (AssetDatabase.LoadAssetAtPath<MonoScript>(ProceduralScriptPath) != null)
                AssetDatabase.DeleteAsset(ProceduralScriptPath);
        }

        private static void AddEuler(
            CurveSet curves, string path, Vector3 baseEuler, float x, float y, float z, float time)
        {
            curves.Add(path, "localEulerAnglesRaw.x", time, baseEuler.x + x);
            curves.Add(path, "localEulerAnglesRaw.y", time, baseEuler.y + y);
            curves.Add(path, "localEulerAnglesRaw.z", time, baseEuler.z + z);
        }

        private static Vector3 Euler(Transform target)
        {
            return target != null ? target.localEulerAngles : Vector3.zero;
        }

        private static void EnableLoop(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
                return;

            EnsureFolder(parent.Replace('\\', '/'));
            AssetDatabase.CreateFolder(parent.Replace('\\', '/'), Path.GetFileName(path));
        }

        private sealed class CurveSet
        {
            private sealed class Channel
            {
                public string path;
                public string property;
                public readonly List<Keyframe> keys = new List<Keyframe>();
            }

            private readonly Dictionary<string, Channel> _channels = new Dictionary<string, Channel>();

            public void Add(string path, string property, float time, float value)
            {
                string key = path + "|" + property;

                if (!_channels.TryGetValue(key, out Channel channel))
                {
                    channel = new Channel { path = path, property = property };
                    _channels.Add(key, channel);
                }

                channel.keys.Add(new Keyframe(time, value));
            }

            public void ApplyTo(AnimationClip clip)
            {
                foreach (Channel channel in _channels.Values)
                {
                    var curve = new AnimationCurve(channel.keys.ToArray());

                    for (int i = 0; i < curve.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    }

                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(channel.path, typeof(Transform), channel.property),
                        curve);
                }
            }
        }
    }
}
