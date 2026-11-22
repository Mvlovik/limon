using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using GameLimon.Story;

namespace GameLimon.EditorTools
{
/// <summary>
/// Editor-side builder for the scene-4 bridge cutscene. Re-running rebuilds the
/// staging, Timeline assets and director bindings from one source of truth.
/// </summary>
public static class BridgeCutsceneSetup
{
    const string ScenePath = "Assets/_Project/Scenes/02_Act1/A1_S04_UnderBridge.unity";
    const string CutsceneRoot = "Assets/_Project/Data/Cutscenes";
    const string ClipRoot = "Assets/_Project/Data/Cutscenes/Clips";
    const string MatRoot = "Assets/_Project/Art/Materials/Cutscene";

    public static void Build()
    {
        Debug.Log("[BridgeBuilder] stage: start");
        EnsureFolder("Assets/_Project/Data");
        EnsureFolder(CutsceneRoot);
        EnsureFolder(ClipRoot);
        EnsureFolder("Assets/_Project/Art/Materials");
        EnsureFolder(MatRoot);

        Debug.Log("[BridgeBuilder] stage: folders");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[BridgeBuilder] stage: scene");

        Material thugCoat = MakeMaterial("MAT_Cutscene_ThugCoat", new Color(0.10f, 0.10f, 0.115f), 0.05f, 0.15f);
        Material rustamCoat = MakeMaterial("MAT_Cutscene_RustamCoat", new Color(0.33f, 0.30f, 0.26f), 0.02f, 0.2f);
        Material skin = MakeMaterial("MAT_Cutscene_Skin", new Color(0.67f, 0.48f, 0.37f), 0f, 0.35f);
        Material hair = MakeMaterial("MAT_Cutscene_Hair", new Color(0.10f, 0.09f, 0.08f), 0f, 0.1f);
        Material grayHair = MakeMaterial("MAT_Cutscene_GrayHair", new Color(0.55f, 0.54f, 0.5f), 0f, 0.1f);
        Material pistolMat = MakeMaterial("MAT_Cutscene_Pistol", new Color(0.025f, 0.025f, 0.03f), 0.75f, 0.6f);
        Material blood = MakeMaterial("MAT_Cutscene_Blood", new Color(0.26f, 0.015f, 0.015f), 0.1f, 0.2f);
        Material toyRed = MakeMaterial("MAT_Cutscene_ToyRed", new Color(0.72f, 0.05f, 0.04f), 0.15f, 0.5f);
        Material toyBlue = MakeMaterial("MAT_Cutscene_ToyBlue", new Color(0.06f, 0.15f, 0.5f), 0.15f, 0.5f);
        Material pants = MakeMaterial("MAT_Cutscene_Pants", new Color(0.13f, 0.14f, 0.17f), 0f, 0.15f);
        Material boots = MakeMaterial("MAT_Cutscene_Boots", new Color(0.04f, 0.035f, 0.03f), 0f, 0.2f);

        Debug.Log("[BridgeBuilder] stage: materials");
        var group = FindInScene(scene, "BridgeCutscene");
        if (group != null)
            UnityEngine.Object.DestroyImmediate(group);

        var systems = FindInScene(scene, "_Systems");
        Transform cutscenesParent = systems != null ? systems.transform.Find("Cutscenes") : null;
        if (cutscenesParent == null)
        {
            GameObject systemsGo = systems != null ? systems : new GameObject("_Systems");
            var cutscenesGo = new GameObject("Cutscenes");
            cutscenesGo.transform.SetParent(systemsGo.transform, false);
            cutscenesParent = cutscenesGo.transform;
        }

        var bridge = new GameObject("BridgeCutscene");
        bridge.transform.SetParent(cutscenesParent, false);
        Undo.RegisterCreatedObjectUndo(bridge, "Build Bridge Cutscene");

        var castRoot = NewChild(bridge.transform, "Cast");
        var propsRoot = NewChild(bridge.transform, "Props");
        var lightsRoot = NewChild(bridge.transform, "Lights");
        var marksRoot = NewChild(bridge.transform, "Marks");
        var camerasRoot = NewChild(bridge.transform, "Cameras");
        var timelinesRoot = NewChild(bridge.transform, "Timelines");
        var finaleRoot = NewChild(bridge.transform, "FinaleProps");

        Debug.Log("[BridgeBuilder] stage: group");
        // ---- Marks -------------------------------------------------------
        Transform playerIntro = NewChild(marksRoot, "PlayerIntro");
        playerIntro.position = new Vector3(-9.0f, 0f, 23.4f);
        playerIntro.rotation = LookAtFlat(playerIntro.position, new Vector3(-16.5f, 0f, 31.8f));

        Transform playerFinal = NewChild(marksRoot, "PlayerFinal");
        playerFinal.position = new Vector3(-14.7f, 0f, 28.7f);
        playerFinal.rotation = LookAtFlat(playerFinal.position, new Vector3(-14.7f, 0f, 26.6f));

        Transform joeIntro = NewChild(marksRoot, "JoeIntro");
        joeIntro.position = new Vector3(-10.0f, 0f, 23.1f);
        joeIntro.rotation = LookAtFlat(joeIntro.position, new Vector3(-16.5f, 0f, 31.8f));

        Debug.Log("[BridgeBuilder] stage: marks");
        // ---- Cast --------------------------------------------------------
        GameObject rustam = BuildActor("Rustam_Standing", castRoot, new Vector3(-17.7f, 0f, 32.0f),
            LookAtFlat(new Vector3(-17.7f, 0f, 32.0f), new Vector3(-14.8f, 0f, 29.0f)),
            rustamCoat, grayHair, skin, pants, boots, armsBehind: true);
        GameObject thugA = BuildActor("ThugA_Standing", castRoot, new Vector3(-16.4f, 0f, 31.1f),
            LookAtFlat(new Vector3(-16.4f, 0f, 31.1f), new Vector3(-17.7f, 0f, 32.0f)),
            thugCoat, hair, skin, pants, boots, armsBehind: false);
        GameObject thugB = BuildActor("ThugB_Standing", castRoot, new Vector3(-18.8f, 0f, 31.4f),
            LookAtFlat(new Vector3(-18.8f, 0f, 31.4f), new Vector3(-17.7f, 0f, 32.0f)),
            thugCoat, hair, skin, pants, boots, armsBehind: false);

        GameObject pistol = BuildPistol(propsRoot, new Vector3(-16.08f, 1.20f, 31.55f),
            LookAtFlat(new Vector3(-16.08f, 1.20f, 31.55f), new Vector3(-17.7f, 1.45f, 32.0f)), pistolMat);

        GameObject rustamBody = BuildBody("Rustam_Body", castRoot, new Vector3(-17.2f, 0.18f, 30.7f), 80f, rustamCoat, grayHair, skin, boots);
        GameObject thugABody = BuildBody("ThugA_Body", castRoot, new Vector3(-16.0f, 0.18f, 32.0f), -110f, thugCoat, hair, skin, boots);
        GameObject thugBBody = BuildBody("ThugB_Body", castRoot, new Vector3(-18.5f, 0.18f, 31.7f), 150f, thugCoat, hair, skin, boots);
        rustamBody.SetActive(false);
        thugABody.SetActive(false);
        thugBBody.SetActive(false);

        GameObject bloodRustam = BuildBlood(propsRoot, "Blood_Rustam", new Vector3(-17.2f, 0.02f, 30.5f), new Vector3(0.9f, 0.004f, 0.65f), blood);
        GameObject bloodA = BuildBlood(propsRoot, "Blood_ThugA", new Vector3(-16.1f, 0.02f, 31.9f), new Vector3(0.75f, 0.004f, 0.55f), blood);
        GameObject bloodB = BuildBlood(propsRoot, "Blood_ThugB", new Vector3(-18.4f, 0.02f, 31.6f), new Vector3(0.7f, 0.004f, 0.5f), blood);
        bloodRustam.SetActive(false);
        bloodA.SetActive(false);
        bloodB.SetActive(false);

        Debug.Log("[BridgeBuilder] stage: cast");
        // ---- Finale props (child hand + toy, no full body) ---------------
        var hand = BuildChildHand(finaleRoot, skin, blood);
        var toy = BuildToy(finaleRoot, toyRed, toyBlue, boots);
        BuildBlood(finaleRoot, "Blood_Finale", new Vector3(-13.8f, 0.015f, 25.0f), new Vector3(0.85f, 0.004f, 0.6f), blood);
        finaleRoot.gameObject.SetActive(false);

        Debug.Log("[BridgeBuilder] stage: finale props");
        // ---- Lights ------------------------------------------------------
        GameObject rimLight = BuildLight(lightsRoot, "RimLight_Warm", new Vector3(-16.6f, 2.35f, 29.8f),
            new Color(1f, 0.64f, 0.34f), 3.2f, 10f);
        GameObject atmosphere = BuildLight(lightsRoot, "Atmosphere_Cool", new Vector3(-13.4f, 2.9f, 28.2f),
            new Color(0.35f, 0.48f, 0.75f), 0.8f, 14f);
        atmosphere.SetActive(false);
        BuildLight(lightsRoot, "Finale_Fill", new Vector3(-14.7f, 2.1f, 27.7f),
            new Color(1f, 0.78f, 0.58f), 1.1f, 4.5f);

        Debug.Log("[BridgeBuilder] stage: lights");
        // ---- Cinemachine cameras -----------------------------------------
        var approach = BuildCamera(camerasRoot, "CM_Shot_Approach", new Vector3(-10.8f, 1.80f, 21.9f),
            new Vector3(-16.6f, 1.25f, 31.8f), 44f, 100, false);
        var nook = BuildCamera(camerasRoot, "CM_Shot_Nook", new Vector3(-11.4f, 1.70f, 26.2f),
            new Vector3(-17.2f, 1.25f, 31.9f), 38f, 0, true);
        var rustamCam = BuildCamera(camerasRoot, "CM_Shot_Rustam", new Vector3(-16.6f, 1.68f, 29.1f),
            new Vector3(-18.0f, 1.50f, 32.2f), 34f, 0, false);
        var waitCam = BuildCamera(camerasRoot, "CM_Shot_Wait", new Vector3(-12.3f, 1.60f, 27.0f),
            new Vector3(-17.4f, 1.20f, 31.9f), 36f, 0, true);
        var aftermathCam = BuildCamera(camerasRoot, "CM_Shot_Aftermath", new Vector3(-15.2f, 2.70f, 28.2f),
            new Vector3(-17.3f, 0.25f, 31.0f), 48f, 0, false);
        var deathCam = BuildCamera(camerasRoot, "CM_Shot_Death", new Vector3(-16.2f, 1.05f, 29.6f),
            new Vector3(-17.2f, 0.25f, 30.9f), 36f, 0, false);
        var finaleCam = BuildCamera(camerasRoot, "CM_Shot_Finale", new Vector3(-14.7f, 0.28f, 26.6f),
            new Vector3(-14.7f, 0.10f, 28.7f), 42f, 0, true);

        Debug.Log("[BridgeBuilder] stage: cameras");
        // ---- Timelines ---------------------------------------------------
        var joe = FindInScene(scene, "Joe");
        Animator joeAnimator = joe != null ? joe.GetComponentInChildren<Animator>(true) : null;
        Transform rightArm = joeAnimator != null ? FindRightArm(joeAnimator.transform) : null;

        AnimationClip nookMove = CreateCameraMoveClip("CUT_A1S04_NookPushIn",
            nook.transform.position, nook.transform.rotation,
            new Vector3(-13.2f, 1.55f, 28.2f), nook.transform.rotation, 3.4f);
        AnimationClip waitMove = CreateCameraMoveClip("CUT_A1S04_WaitPushIn",
            waitCam.transform.position, waitCam.transform.rotation,
            new Vector3(-14.1f, 1.45f, 29.1f), waitCam.transform.rotation, 5.5f);
        AnimationClip finaleMove = CreateFinaleClip(finaleCam, playerFinal.position);
        AnimationClip joeArm = CreateJoePistolClip("CUT_A1S04_JoeRaiseArm", joeAnimator, rightArm);

        Debug.Log("[BridgeBuilder] stage: timeline assets");
        PlayableDirector intro = BuildDirector(timelinesRoot, "DIR_Intro", "A1_S04_Bridge_Intro");
        CinemachineBrain brain = FindInScene(scene, "RustamCutscene").GetComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        TimelineAsset introAsset = (TimelineAsset)intro.playableAsset;
        var introCm = (CinemachineTrack)introAsset.CreateTrack(typeof(CinemachineTrack), null, "Cinemachine");
        intro.SetGenericBinding(introCm, brain);
        AddCinemachineShot(introCm, intro, approach, 0f, 3.2f);
        AddCinemachineShot(introCm, intro, nook, 3.1f, 3.4f);
        AddCinemachineShot(introCm, intro, rustamCam, 6.3f, 2.9f);
        AddCameraAnimationTrack(introAsset, intro, "CAM_Nook_PushIn", nook.GetComponent<Animator>(), nookMove, 3.1f, false);
        if (joeAnimator != null && joeArm != null)
            AddCameraAnimationTrack(introAsset, intro, "JOE_RaisePistol", joeAnimator, joeArm, 0.2f, true, "Joe arm");
        AddActivationTrack(introAsset, intro, "ATMO_Cutscene", atmosphere, 0f, 9.2f);

        Debug.Log("[BridgeBuilder] stage: intro timeline");
        PlayableDirector branchA = BuildDirector(timelinesRoot, "DIR_BranchA", "A1_S04_Bridge_BranchA");
        TimelineAsset branchAAsset = (TimelineAsset)branchA.playableAsset;
        var branchACm = (CinemachineTrack)branchAAsset.CreateTrack(typeof(CinemachineTrack), null, "Cinemachine");
        branchA.SetGenericBinding(branchACm, brain);
        AddCinemachineShot(branchACm, branchA, waitCam, 0f, 5.5f);
        AddCameraAnimationTrack(branchAAsset, branchA, "CAM_Wait_PushIn", waitCam.GetComponent<Animator>(), waitMove, 0f, false);

        Debug.Log("[BridgeBuilder] stage: branchA timeline");
        PlayableDirector aftermath = BuildDirector(timelinesRoot, "DIR_Aftermath", "A1_S04_Bridge_Aftermath");
        TimelineAsset aftermathAsset = (TimelineAsset)aftermath.playableAsset;
        var aftermathCm = (CinemachineTrack)aftermathAsset.CreateTrack(typeof(CinemachineTrack), null, "Cinemachine");
        aftermath.SetGenericBinding(aftermathCm, brain);
        AddCinemachineShot(aftermathCm, aftermath, aftermathCam, 0f, 3.1f);
        AddCinemachineShot(aftermathCm, aftermath, deathCam, 3.0f, 3.0f);

        Debug.Log("[BridgeBuilder] stage: aftermath timeline");
        PlayableDirector finale = BuildDirector(timelinesRoot, "DIR_Finale", "A1_S04_Bridge_Finale");
        TimelineAsset finaleAsset = (TimelineAsset)finale.playableAsset;
        var finaleCm = (CinemachineTrack)finaleAsset.CreateTrack(typeof(CinemachineTrack), null, "Cinemachine");
        finale.SetGenericBinding(finaleCm, brain);
        AddCinemachineShot(finaleCm, finale, finaleCam, 0f, 9.5f);
        AddCameraAnimationTrack(finaleAsset, finale, "CAM_Finale_RiseAndTurn", finaleCam.GetComponent<Animator>(), finaleMove, 0f, false);

        Debug.Log("[BridgeBuilder] stage: finale timeline");
        // ---- Cutscene component ------------------------------------------
        var cameraGo = FindInScene(scene, "RustamCutscene");
        foreach (var old in cameraGo.GetComponents<SceneCutscene>())
            UnityEngine.Object.DestroyImmediate(old);

        var controller = cameraGo.AddComponent<RustamBridgeCutscene>();
        AudioSource audioSource = cameraGo.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = cameraGo.AddComponent<AudioSource>();

        var so = new SerializedObject(controller);
        SetObject(so, "cutsceneCamera", cameraGo.GetComponent<Camera>());
        SetFloat(so, "cameraBlendDuration", 0f);
        SetObjectArray(so, "enableDuringCutscene", new UnityEngine.Object[0]);
        SetObject(so, "sequence", null);
        SetFloat(so, "holdDuration", 0f);
        SetBool(so, "completeSceneWhenFinished", true);
        SetFloat(so, "endHoldDuration", 1.0f);
        SetFloat(so, "fadeOutDuration", 1.2f);
        SetBool(so, "restorePlayerAfterCutscene", true);
        SetObject(so, "timeline", null);
        SetBool(so, "waitForTimeline", false);

        SetObject(so, "introDirector", intro);
        SetObject(so, "branchADirector", branchA);
        SetObject(so, "aftermathDirector", aftermath);
        SetObject(so, "finaleDirector", finale);
        SetObject(so, "playerIntroMark", playerIntro);
        SetObject(so, "playerFinalMark", playerFinal);
        SetObject(so, "joeIntroMark", joeIntro);
        SetObject(so, "rustamStanding", rustam);
        SetObject(so, "thugAStanding", thugA);
        SetObject(so, "thugBStanding", thugB);
        SetObject(so, "pistolInThugHand", pistol);
        SetObject(so, "rustamBody", rustamBody);
        SetObject(so, "thugABody", thugABody);
        SetObject(so, "thugBBody", thugBBody);
        SetObjectArray(so, "aftermathBlood", new UnityEngine.Object[] { bloodRustam, bloodA, bloodB });
        SetObject(so, "finaleProps", finaleRoot.gameObject);
        var rita = FindInScene(scene, "RITA");
        SetObjectArray(so, "hideDuringCutscene", rita != null ? new UnityEngine.Object[] { rita } : new UnityEngine.Object[0]);
        SetObject(so, "cutsceneAudio", audioSource);
        SetFloat(so, "gunshotVolume", 0.9f);
        SetFloat(so, "ringingVolume", 0.35f);
        so.ApplyModifiedPropertiesWithoutUndo();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (rita != null)
            rita.SetActive(false);

        Debug.Log("[BridgeBuilder] stage: component");
        // ---- Trigger wiring ---------------------------------------------
        var trigger = FindInScene(scene, "Trigger_RustamFounded");
        var triggerVolume = trigger != null ? trigger.GetComponent<TriggerVolume>() : null;
        if (triggerVolume != null)
        {
            var triggerSo = new SerializedObject(triggerVolume);
            var onEnter = triggerSo.FindProperty("onEnter");
            if (onEnter != null && onEnter.arraySize > 0)
            {
                var action = onEnter.GetArrayElementAtIndex(0);
                action.FindPropertyRelative("type").enumValueIndex = (int)StoryActionType.PlayCutscene;
                action.FindPropertyRelative("cutscene").objectReferenceValue = controller;
            }
            triggerSo.FindProperty("requiredStep").stringValue = "A1_S04_RustamFounded";
            triggerSo.FindProperty("completedFlag").stringValue = "A1_S04_RustamFounded";
            triggerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("[BridgeBuilder] stage: trigger; saving");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Stage the real human models and apply the final camera/mark pass.
        // These passes reopen the scene, so cache names/flags before they run
        // (the old object references are destroyed by OpenScene).
        bool triggerWired = triggerVolume != null;
        string finaleName = finale != null ? finale.name : "null";

        BridgeCutsceneModelSetup.Apply();
        BridgeCutsceneStagingPolish.Apply();
        BridgeCutsceneToyFinale.Apply();

        Debug.Log($"[BridgeBuilder] done. Trigger wired: {triggerWired}; finale={finaleName}");
    }

    // ------------------------------------------------------------------ helpers

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MatRoot + "/" + name + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            AssetDatabase.DeleteAsset(path);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Transform NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Quaternion LookAtFlat(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return Quaternion.identity;
        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    static GameObject BuildActor(string name, Transform parent, Vector3 pos, Quaternion rot,
        Material coat, Material hairMat, Material skinMat, Material pantsMat, Material bootsMat, bool armsBehind)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, rot);

        Part(root.transform, "Coat", PrimitiveType.Capsule, new Vector3(0f, 1.02f, 0f), new Vector3(0.42f, 0.36f, 0.30f), Quaternion.identity, coat);
        Part(root.transform, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.77f, 0f), new Vector3(0.36f, 0.22f, 0.25f), Quaternion.identity, coat);
        Part(root.transform, "LegL", PrimitiveType.Capsule, new Vector3(-0.13f, 0.44f, 0f), new Vector3(0.14f, 0.38f, 0.14f), Quaternion.identity, pantsMat);
        Part(root.transform, "LegR", PrimitiveType.Capsule, new Vector3(0.13f, 0.44f, 0f), new Vector3(0.14f, 0.38f, 0.14f), Quaternion.identity, pantsMat);
        Part(root.transform, "BootL", PrimitiveType.Cube, new Vector3(-0.13f, 0.08f, 0.06f), new Vector3(0.17f, 0.11f, 0.28f), Quaternion.identity, bootsMat);
        Part(root.transform, "BootR", PrimitiveType.Cube, new Vector3(0.13f, 0.08f, 0.06f), new Vector3(0.17f, 0.11f, 0.28f), Quaternion.identity, bootsMat);
        Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.64f, 0f), new Vector3(0.24f, 0.27f, 0.24f), Quaternion.identity, skinMat);
        Part(root.transform, "Hair", PrimitiveType.Sphere, new Vector3(0f, 1.73f, -0.015f), new Vector3(0.25f, 0.16f, 0.25f), Quaternion.identity, hairMat);

        if (armsBehind)
        {
            Part(root.transform, "ArmL", PrimitiveType.Capsule, new Vector3(-0.22f, 1.00f, -0.16f), new Vector3(0.105f, 0.30f, 0.105f), Quaternion.Euler(-25f, 0f, 20f), coat);
            Part(root.transform, "ArmR", PrimitiveType.Capsule, new Vector3(0.22f, 1.00f, -0.16f), new Vector3(0.105f, 0.30f, 0.105f), Quaternion.Euler(-25f, 0f, -20f), coat);
            Part(root.transform, "HandL", PrimitiveType.Sphere, new Vector3(-0.28f, 0.86f, 0.03f), new Vector3(0.09f, 0.11f, 0.09f), Quaternion.identity, skinMat);
            Part(root.transform, "HandR", PrimitiveType.Sphere, new Vector3(0.28f, 0.86f, 0.03f), new Vector3(0.09f, 0.11f, 0.09f), Quaternion.identity, skinMat);
        }
        else
        {
            Part(root.transform, "ArmL", PrimitiveType.Capsule, new Vector3(-0.25f, 1.08f, 0.12f), new Vector3(0.105f, 0.30f, 0.105f), Quaternion.Euler(58f, 0f, 12f), coat);
            Part(root.transform, "ArmR", PrimitiveType.Capsule, new Vector3(0.25f, 1.08f, 0.12f), new Vector3(0.105f, 0.30f, 0.105f), Quaternion.Euler(58f, 0f, -12f), coat);
            Part(root.transform, "HandL", PrimitiveType.Sphere, new Vector3(-0.28f, 1.15f, 0.38f), new Vector3(0.09f, 0.11f, 0.09f), Quaternion.identity, skinMat);
            Part(root.transform, "HandR", PrimitiveType.Sphere, new Vector3(0.28f, 1.15f, 0.38f), new Vector3(0.09f, 0.11f, 0.09f), Quaternion.identity, skinMat);
        }

        return root;
    }

    static GameObject BuildBody(string name, Transform parent, Vector3 pos, float yaw,
        Material coat, Material hairMat, Material skinMat, Material bootsMat)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, yaw, 0f));

        Part(root.transform, "Coat", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.44f, 0.42f, 0.32f), Quaternion.identity, coat);
        Part(root.transform, "Hips", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(0.38f, 0.28f, 0.26f), Quaternion.identity, coat);
        Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.5f, 0f), new Vector3(0.24f, 0.27f, 0.24f), Quaternion.identity, skinMat);
        Part(root.transform, "Hair", PrimitiveType.Sphere, new Vector3(0f, 1.6f, -0.02f), new Vector3(0.25f, 0.15f, 0.25f), Quaternion.identity, hairMat);
        Part(root.transform, "LegL", PrimitiveType.Capsule, new Vector3(-0.14f, 0.0f, 0.38f), new Vector3(0.14f, 0.34f, 0.14f), Quaternion.Euler(90f, 0f, 0f), coat);
        Part(root.transform, "LegR", PrimitiveType.Capsule, new Vector3(0.14f, 0.0f, 0.38f), new Vector3(0.14f, 0.34f, 0.14f), Quaternion.Euler(90f, 0f, 0f), coat);
        return root;
    }

    static GameObject BuildPistol(Transform parent, Vector3 pos, Quaternion rot, Material mat)
    {
        var root = new GameObject("PistolInHand");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, rot);
        Part(root.transform, "Slide", PrimitiveType.Cube, new Vector3(0f, 0f, 0.10f), new Vector3(0.055f, 0.07f, 0.32f), Quaternion.identity, mat);
        Part(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.10f, -0.03f), new Vector3(0.05f, 0.17f, 0.07f), Quaternion.Euler(18f, 0f, 0f), mat);
        return root;
    }

    static GameObject BuildBlood(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        // Plane уже лежит в плоскости XZ (нормаль +Y) и показывает всю
        // текстуру пятна, в отличие от цилиндра, который обрезает её в круг.
        GameObject go = Part(parent, name, PrimitiveType.Plane, pos,
            new Vector3(scale.x * 0.1f, 1f, scale.z * 0.1f), Quaternion.identity, mat);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static GameObject BuildChildHand(Transform parent, Material skin, Material blood)
    {
        var root = new GameObject("ChildHand");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(-13.85f, 0.09f, 25.0f);
        root.transform.rotation = Quaternion.Euler(90f, 20f, 0f);

        Part(root.transform, "Forearm", PrimitiveType.Capsule, new Vector3(0f, 0.13f, 0f), new Vector3(0.065f, 0.16f, 0.065f), Quaternion.identity, skin);
        Part(root.transform, "Hand", PrimitiveType.Sphere, new Vector3(0f, 0.31f, 0f), new Vector3(0.09f, 0.055f, 0.11f), Quaternion.identity, skin);
        Part(root.transform, "Sleeve", PrimitiveType.Cube, new Vector3(0f, -0.03f, 0f), new Vector3(0.14f, 0.10f, 0.14f), Quaternion.identity, blood);
        return root;
    }

    static GameObject BuildToy(Transform parent, Material red, Material blue, Material dark)
    {
        var root = new GameObject("ChildToy_Car");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(new Vector3(-13.45f, 0.10f, 24.86f), Quaternion.Euler(0f, 28f, 0f));

        Part(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0.25f, 0.09f, 0.13f), Quaternion.identity, red);
        Part(root.transform, "Cabin", PrimitiveType.Cube, new Vector3(-0.02f, 0.13f, 0f), new Vector3(0.13f, 0.08f, 0.11f), Quaternion.identity, blue);
        Part(root.transform, "Wheel1", PrimitiveType.Cylinder, new Vector3(-0.08f, 0.02f, 0.07f), new Vector3(0.035f, 0.02f, 0.035f), Quaternion.Euler(90f, 0f, 0f), dark);
        Part(root.transform, "Wheel2", PrimitiveType.Cylinder, new Vector3(0.08f, 0.02f, 0.07f), new Vector3(0.035f, 0.02f, 0.035f), Quaternion.Euler(90f, 0f, 0f), dark);
        Part(root.transform, "Wheel3", PrimitiveType.Cylinder, new Vector3(-0.08f, 0.02f, -0.07f), new Vector3(0.035f, 0.02f, 0.035f), Quaternion.Euler(90f, 0f, 0f), dark);
        Part(root.transform, "Wheel4", PrimitiveType.Cylinder, new Vector3(0.08f, 0.02f, -0.07f), new Vector3(0.035f, 0.02f, 0.035f), Quaternion.Euler(90f, 0f, 0f), dark);
        return root;
    }

    static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = mat;

        return go;
    }

    static GameObject BuildLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.55f;
        return go;
    }

    static CinemachineCamera BuildCamera(Transform parent, string name, Vector3 pos, Vector3 target, float fov, int priority, bool animated)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation((target - pos).normalized, Vector3.up);

        var camera = go.AddComponent<CinemachineCamera>();
        camera.Lens.FieldOfView = fov;
        camera.Lens.NearClipPlane = 0.05f;
        camera.Lens.FarClipPlane = 250f;
        camera.Priority = priority;

        if (animated)
        {
            var animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
        }

        return camera;
    }

    static Animator GetAnimator(CinemachineCamera camera)
    {
        return camera != null ? camera.GetComponent<Animator>() : null;
    }

    static AnimationClip CreateCameraMoveClip(string name, Vector3 start, Quaternion startRot, Vector3 end, Quaternion endRot, float duration)
    {
        AnimationClip clip = NewClip(name);
        SetVectorCurves(clip, "", new[] { 0f, duration }, new[] { start, end }, "m_LocalPosition");
        SetQuaternionCurves(clip, "", new[] { 0f, duration }, new[] { startRot, endRot });
        clip.EnsureQuaternionContinuity();
        SaveClip(clip, name);
        return clip;
    }

    static AnimationClip CreateFinaleClip(CinemachineCamera camera, Vector3 playerFeet)
    {
        const float duration = 9.5f;
        var startPos = new Vector3(-14.7f, 0.30f, 26.6f);
        var facePos = new Vector3(-14.7f, 1.56f, 26.75f);
        var feetTarget = new Vector3(-14.7f, 0.10f, 28.7f);
        var faceTarget = new Vector3(-14.7f, 1.58f, 28.7f);
        var handTarget = new Vector3(-13.8f, 0.10f, 24.85f);

        Quaternion q0 = Quaternion.LookRotation((feetTarget - startPos).normalized, Vector3.up);
        Quaternion q1 = Quaternion.LookRotation((faceTarget - facePos).normalized, Vector3.up);
        Quaternion q2 = Quaternion.LookRotation((handTarget - facePos).normalized, Vector3.up);

        AnimationClip clip = NewClip("CUT_A1S04_FinaleCamera");
        SetVectorCurves(clip, "", new[] { 0f, 4.5f, 9.5f }, new[] { startPos, facePos, facePos }, "m_LocalPosition");
        SetQuaternionCurves(clip, "",
            new[] { 0f, 4.5f, 5.3f, 6.1f, 7.0f, 9.5f },
            new[] { q0, q1, q2 * Quaternion.Euler(0.7f, 0.4f, 0.3f), q2 * Quaternion.Euler(-0.5f, -0.3f, -0.2f), q2, q2 });
        clip.EnsureQuaternionContinuity();
        SaveClip(clip, "CUT_A1S04_FinaleCamera");
        return clip;
    }

    static AnimationClip CreateJoePistolClip(string name, Animator joeAnimator, Transform rightArm)
    {
        AnimationClip clip = NewClip(name);
        if (joeAnimator == null || rightArm == null)
        {
            SaveClip(clip, name);
            return clip;
        }

        string path = AnimationUtility.CalculateTransformPath(rightArm, joeAnimator.transform);
        Quaternion rest = rightArm.localRotation;
        Quaternion raised = rest * Quaternion.Euler(-38f, 0f, -18f);
        SetQuaternionCurves(clip, path, new[] { 0f, 1.0f, 2.4f }, new[] { rest, raised, rest });
        clip.EnsureQuaternionContinuity();
        SaveClip(clip, name);
        return clip;
    }

    static AnimationClip NewClip(string name)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };
        return clip;
    }

    static void SaveClip(AnimationClip clip, string name)
    {
        string path = ClipRoot + "/" + name + ".anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
    }

    static void SetVectorCurves(AnimationClip clip, string path, float[] times, Vector3[] values, string propertyPrefix)
    {
        var xs = new Keyframe[times.Length];
        var ys = new Keyframe[times.Length];
        var zs = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            xs[i] = new Keyframe(times[i], values[i].x);
            ys[i] = new Keyframe(times[i], values[i].y);
            zs[i] = new Keyframe(times[i], values[i].z);
        }
        AddCurve(clip, path, propertyPrefix + ".x", xs);
        AddCurve(clip, path, propertyPrefix + ".y", ys);
        AddCurve(clip, path, propertyPrefix + ".z", zs);
    }

    static void SetQuaternionCurves(AnimationClip clip, string path, float[] times, Quaternion[] values)
    {
        var qs = new Quaternion[values.Length];
        for (int i = 0; i < values.Length; i++)
            qs[i] = values[i].normalized;

        for (int i = 1; i < qs.Length; i++)
        {
            if (Quaternion.Dot(qs[i - 1], qs[i]) < 0f)
                qs[i] = new Quaternion(-qs[i].x, -qs[i].y, -qs[i].z, -qs[i].w);
        }

        var xs = new Keyframe[times.Length];
        var ys = new Keyframe[times.Length];
        var zs = new Keyframe[times.Length];
        var ws = new Keyframe[times.Length];
        for (int i = 0; i < times.Length; i++)
        {
            xs[i] = new Keyframe(times[i], qs[i].x);
            ys[i] = new Keyframe(times[i], qs[i].y);
            zs[i] = new Keyframe(times[i], qs[i].z);
            ws[i] = new Keyframe(times[i], qs[i].w);
        }
        AddCurve(clip, path, "m_LocalRotation.x", xs);
        AddCurve(clip, path, "m_LocalRotation.y", ys);
        AddCurve(clip, path, "m_LocalRotation.z", zs);
        AddCurve(clip, path, "m_LocalRotation.w", ws);
    }

    static void AddCurve(AnimationClip clip, string path, string property, Keyframe[] keys)
    {
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), property);
        AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
    }

    static PlayableDirector BuildDirector(Transform parent, string name, string assetName)
    {
        string path = CutsceneRoot + "/" + assetName + ".playable";
        if (AssetDatabase.LoadAssetAtPath<PlayableAsset>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = assetName;
        AssetDatabase.CreateAsset(timeline, path);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var director = go.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
        return director;
    }

    static void AddCinemachineShot(CinemachineTrack track, PlayableDirector director, CinemachineCamera camera, float start, float duration)
    {
        TimelineClip clip = track.CreateClip<CinemachineShot>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = camera.name;

        var shot = (CinemachineShot)clip.asset;
        shot.DisplayName = camera.name;
        string exposedName = Guid.NewGuid().ToString();
        shot.VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = exposedName };
        director.SetReferenceValue(exposedName, camera);
    }

    static void AddCameraAnimationTrack(TimelineAsset timeline, PlayableDirector director, string trackName,
        Animator animator, AnimationClip clipAsset, float start, bool removeStartOffset = true, string label = "Camera")
    {
        if (animator == null || clipAsset == null) return;

        var track = (AnimationTrack)timeline.CreateTrack(typeof(AnimationTrack), null, trackName);
        director.SetGenericBinding(track, animator);

        TimelineClip clip = track.CreateClip<AnimationPlayableAsset>();
        clip.start = start;
        clip.duration = Mathf.Max(0.1f, clipAsset.length);
        clip.displayName = label;

        var asset = (AnimationPlayableAsset)clip.asset;
        asset.clip = clipAsset;
        asset.loop = AnimationPlayableAsset.LoopMode.Off;

        var assetSo = new SerializedObject(asset);
        var offsetProp = assetSo.FindProperty("m_RemoveStartOffset");
        if (offsetProp != null)
        {
            offsetProp.boolValue = removeStartOffset;
            assetSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!removeStartOffset)
            EditorUtility.SetDirty(timeline);
    }

    static void AddActivationTrack(TimelineAsset timeline, PlayableDirector director, string trackName,
        GameObject target, float start, float duration)
    {
        if (target == null) return;

        var track = (ActivationTrack)timeline.CreateTrack(typeof(ActivationTrack), null, trackName);
        track.postPlaybackState = ActivationTrack.PostPlaybackState.Revert;
        director.SetGenericBinding(track, target);

        TimelineClip clip = track.CreateDefaultClip();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = "Atmosphere";
    }

    static Transform FindRightArm(Transform animatorRoot)
    {
        Transform[] all = animatorRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
            if (t.name == "mixamorig:RightArm" || t.name.EndsWith(":RightArm"))
                return t;
        return null;
    }

    static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
                if (t.name == name)
                    return t.gameObject;
        }
        return null;
    }

    static void SetObject(SerializedObject so, string name, UnityEngine.Object value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[BridgeBuilder] Missing property " + name);
    }

    static void SetObjectArray(SerializedObject so, string name, UnityEngine.Object[] values)
    {
        var p = so.FindProperty(name);
        if (p == null)
        {
            Debug.LogWarning("[BridgeBuilder] Missing array property " + name);
            return;
        }

        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[BridgeBuilder] Missing float " + name);
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        var p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
        else Debug.LogWarning("[BridgeBuilder] Missing bool " + name);
    }
}
}
