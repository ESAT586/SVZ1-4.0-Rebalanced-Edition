using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TerasuPlaytestKeeper : MonoBehaviour
{
    private const string kOutDir = "C:/Users/esatg/.gemini/antigravity/brain/1d9330f5-545a-490f-9101-73dd658a599e/";

    public bool autoSequenceActive = false;
    public int sequenceStep = 0;
    public BloodMoonTerasuController targetCtrl;
    public Character targetChar;
    private float mElapsed = 0f;
    private float mAscendedElapsed = 0f;
    private bool mWideCamera = false;

    private void Update()
    {
        if (!Application.isPlaying) return;
        Application.runInBackground = true;
        TerasuPlaytestMenu.ClearBlockingPauseDialog();
        Time.timeScale = 1f;

        if (!autoSequenceActive) return;

        CharactersManager cmKeep = WeakGlobalInstance<CharactersManager>.instance;
        if (cmKeep != null)
        {
            List<Character> aliveKeep = cmKeep.allAlive;
            for (int i = aliveKeep.Count - 1; i >= 0; i--)
            {
                Character c = aliveKeep[i];
                bool isOurTarget = (targetCtrl != null && c != null && c.controlledObject == targetCtrl.gameObject);
                if (c != null && c.isEnemy && !c.isBase && !isOurTarget)
                {
                    cmKeep.DestroyCharacter(c);
                }
            }
        }

        mElapsed += Time.deltaTime;
        BloodMoonTerasuController ctrl = targetCtrl != null ? targetCtrl : UnityEngine.Object.FindObjectOfType<BloodMoonTerasuController>();

        if (ctrl != null && ctrl.hasAscended && targetChar != null && targetChar.health > 0f)
        {
            targetChar.health = targetChar.maxHealth;
        }
        InGameImpl igKeep = WeakGlobalSceneBehavior<InGameImpl>.instance;
        if (sequenceStep >= 2 && ctrl != null && igKeep != null && igKeep.hero != null && igKeep.hero.controller != null)
        {
            float maxHeroZ = ctrl.transform.position.z - 140f;
            if (igKeep.hero.controller.position.z > maxHeroZ)
            {
                Vector3 hp = igKeep.hero.controller.position;
                hp.z = maxHeroZ;
                igKeep.hero.controller.position = hp;
                igKeep.hero.controller.StopWalking();
            }
        }

        if (sequenceStep == 0 && mElapsed >= 2.20f)
        {
            ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_1_phase1_sword.png");
            Debug.Log("[TerasuPlaytest] CAPTURED_1: Phase 1 Zombie Shogun with rusty sword");
            sequenceStep = 1;
        }
        else if (sequenceStep == 1 && mElapsed >= 2.45f)
        {
            TerasuPlaytestMenu.TriggerFatalBlow();
            sequenceStep = 2;
        }
        else if (sequenceStep == 2 && ctrl != null &&
                 ctrl.currentStage == BloodMoonTerasuController.AscensionStage.EclipseLightning &&
                 ctrl.stageTimer >= 1.85f)
        {
            ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_2_eclipse_lightning.png");
            Debug.Log("[TerasuPlaytest] CAPTURED_2: Blood Moon Eclipse & Lightning Convulsion (t=" + ctrl.stageTimer.ToString("F2") + ")");
            sequenceStep = 3;
        }
        else if (sequenceStep == 3 && ctrl != null &&
                 ctrl.currentStage == BloodMoonTerasuController.AscensionStage.SphereLevitation &&
                 ctrl.stageTimer >= 5.20f)
        {
            ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_3_sphere_ascension.png");
            Debug.Log("[TerasuPlaytest] CAPTURED_3: Azir-Style Levitating Blood Moon Sphere (t=" + ctrl.stageTimer.ToString("F2") + ")");
            sequenceStep = 4;
        }
        else if (sequenceStep == 4 && ctrl != null && ctrl.hasAscended)
        {
            mAscendedElapsed += Time.deltaTime;
            if (mAscendedElapsed >= 1.15f)
            {
                ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_4_phase2_terasu.png");
                Debug.Log("[TerasuPlaytest] CAPTURED_4: Phase 2 Terasu Rise of the Blood Moon + Banner + Hover + Crown");
                sequenceStep = 5;
            }
        }
        else if (sequenceStep == 5 && ctrl != null && ctrl.hasAscended)
        {
            mAscendedElapsed += Time.deltaTime;
            if (mAscendedElapsed >= 2.20f)
            {
                mWideCamera = true;
                ctrl.TriggerSpearUltimateNow();
                Debug.Log("[TerasuPlaytest] Triggered Sun Wukong Backline Spear Ult");
                sequenceStep = 6;
            }
        }
        else if (sequenceStep == 6 && ctrl != null &&
                 ctrl.currentUltState == BloodMoonTerasuController.SpearUltState.Impaled &&
                 ctrl.ultStateTimer >= 0.35f)
        {
            ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_5_spear_backline_impale.png");
            Debug.Log("[TerasuPlaytest] CAPTURED_5: Backline Spear Impale AoE");
            sequenceStep = 7;
        }
        else if (sequenceStep == 7 && ctrl != null &&
                 ctrl.currentUltState == BloodMoonTerasuController.SpearUltState.Recalling &&
                 ctrl.ultStateTimer >= 0.24f)
        {
            ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_6_spear_telekinetic_recall.png");
            Debug.Log("[TerasuPlaytest] CAPTURED_6: Sun Wukong Hand-Raise Telekinetic Recall + Tether");
            mAscendedElapsed = 0f;
            sequenceStep = 8;
        }
        else if (sequenceStep == 8 && ctrl != null &&
                 ctrl.currentUltState == BloodMoonTerasuController.SpearUltState.Ready)
        {
            mAscendedElapsed += Time.deltaTime;
            if (mAscendedElapsed >= 0.55f)
            {
                ctrl.TriggerSixthWheelUltNow();
                sequenceStep = 9;
                mAscendedElapsed = 0f;
            }
        }
        else if (sequenceStep == 9 && ctrl != null)
        {
            mAscendedElapsed += Time.deltaTime;
            if (mAscendedElapsed >= 0.68f)
            {
                ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_7_thanos_shockwave.png");
                Debug.Log("[TerasuPlaytest] CAPTURED_7: 6th Wheel Overdrive Glow + Thanos Fist Particle Shockwave Explosion");
                mWideCamera = false;
                mAscendedElapsed = 0f;
                sequenceStep = 10;
            }
        }
        else if (sequenceStep == 10 && ctrl != null)
        {
            mAscendedElapsed += Time.deltaTime;
            if (mAscendedElapsed >= 1.15f)
            {
                ScreenCapture.CaptureScreenshot(kOutDir + "terasu_live_8_halo_and_stance_close.png");
                Debug.Log("[TerasuPlaytest] CAPTURED_8: Close-up Post-Ult Stance, Back Halo Clearance & Left Hand");
                Debug.Log("[TerasuPlaytest] SEQUENCE_COMPLETE");
                autoSequenceActive = false;
                sequenceStep = 11;
            }
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || Camera.main == null) return;
        BloodMoonTerasuController ctrl = targetCtrl != null ? targetCtrl : UnityEngine.Object.FindObjectOfType<BloodMoonTerasuController>();
        if (ctrl == null) return;

        Vector3 focus = ctrl.transform.position;
        Vector3 shake = ctrl.cameraShakeOffset;
        Camera cam = Camera.main;
        if (mWideCamera)
        {
            cam.transform.position = new Vector3(focus.x + 680f, focus.y + 185f, focus.z - 135f) + shake;
            cam.transform.LookAt(new Vector3(focus.x, focus.y + 72f, focus.z - 125f));
        }
        else
        {
            cam.transform.position = new Vector3(focus.x + 560f, focus.y + 160f, focus.z - 35f) + shake;
            cam.transform.LookAt(new Vector3(focus.x, focus.y + 72f, focus.z - 30f));
        }
        if (shake.sqrMagnitude > 0.1f)
        {
            float roll = Mathf.Clamp(shake.x * 0.22f + shake.y * 0.18f, -4.5f, 4.5f);
            cam.transform.Rotate(0f, 0f, roll, Space.Self);
        }
    }
}

public static class TerasuPlaytestMenu
{
    private static void ClearFieldDialog(object target, string fieldName)
    {
        if (target == null) return;
        FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (fi == null) return;
        object val = fi.GetValue(target);
        if (val != null)
        {
            MethodInfo destroyMethod = val.GetType().GetMethod("Destroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (destroyMethod != null)
            {
                destroyMethod.Invoke(val, null);
            }
            fi.SetValue(target, null);
        }
    }

    public static void ClearBlockingPauseDialog()
    {
        InGameImpl[] allInGames = UnityEngine.Object.FindObjectsOfType<InGameImpl>();
        for (int i = 0; i < allInGames.Length; i++)
        {
            InGameImpl g = allInGames[i];
            if (g == null) continue;
            ClearFieldDialog(g, "mDialogHandler");
            ClearFieldDialog(g, "mWaveBanner");
            ClearFieldDialog(g, "mGenericGameBanner");
        }

        if (WeakGlobalInstance<DialogHandler>.instance != null)
        {
            WeakGlobalInstance<DialogHandler>.instance.Destroy();
        }

        for (int guard = 0; guard < 20; guard++)
        {
            GameObject bell = GameObject.Find("BellShockwave(Clone)");
            if (bell == null) break;
            bell.SetActive(false);
            bell.name = "BellShockwave_Cleared";
            UnityEngine.Object.Destroy(bell);
        }

        Time.timeScale = 1f;
    }

    private static void ConfigureTextureImport(string path, bool isNormalMap)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            bool changed = false;
            if (isNormalMap && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (ti.maxTextureSize != 2048)
            {
                ti.maxTextureSize = 2048;
                changed = true;
            }
            if (changed)
            {
                ti.SaveAndReimport();
            }
        }
    }

    private static Material GetOrCreateMaterial(string matPath, Shader shader, Texture2D mainTex, Texture2D bumpMap, Color color)
    {
        Material mat = (Material)AssetDatabase.LoadAssetAtPath(matPath, typeof(Material));
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (shader != null)
        {
            mat.shader = shader;
        }
        if (mat.HasProperty("_Color")) mat.color = color;
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
        if (bumpMap != null && mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", bumpMap);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ConfigureModelImporter(string fbxPath, ModelImporterAnimationType animType, Avatar sourceAvatar, bool importAnim, bool loopAll)
    {
        ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) return;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.isReadable = true;
        mi.importMaterials = false;
        mi.optimizeGameObjects = false;
        mi.animationType = animType;
        mi.importAnimation = importAnim;
        mi.animationCompression = ModelImporterAnimationCompression.Off;
        if (animType == ModelImporterAnimationType.Generic)
        {
            mi.sourceAvatar = sourceAvatar;
        }
        if (importAnim)
        {
            mi.animationWrapMode = loopAll ? WrapMode.Loop : WrapMode.Once;
            ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    bool shouldLoop = loopAll ||
                        clips[i].name.IndexOf("Combat_Stance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        clips[i].name.IndexOf("Spear_Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        clips[i].name.IndexOf("walking", StringComparison.OrdinalIgnoreCase) >= 0;
                    clips[i].loopTime = shouldLoop;
                    clips[i].wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Once;
                }
                mi.clipAnimations = clips;
            }
        }
        mi.SaveAndReimport();
    }

    private static Avatar FindAvatarInFbx(string fbxPath)
    {
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (subAssets == null) return null;
        for (int i = 0; i < subAssets.Length; i++)
        {
            Avatar av = subAssets[i] as Avatar;
            if (av != null) return av;
        }
        return null;
    }

    private static Transform FindDeepChild(Transform root, string nameExact)
    {
        if (root == null) return null;
        if (root.name.Equals(nameExact, StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindDeepChild(root.GetChild(i), nameExact);
            if (hit != null) return hit;
        }
        return null;
    }

    private static void AddAnimatorStatesFromFbx(AnimatorStateMachine sm, string fbxPath, string overrideSingleClipName)
    {
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (subAssets == null) return;
        for (int i = 0; i < subAssets.Length; i++)
        {
            AnimationClip clip = subAssets[i] as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
            string clipKey = clip.name;
            int pipeIdx = clipKey.LastIndexOf('|');
            if (pipeIdx >= 0 && pipeIdx + 1 < clipKey.Length)
            {
                clipKey = clipKey.Substring(pipeIdx + 1);
            }
            if (!string.IsNullOrEmpty(overrideSingleClipName))
            {
                clipKey = overrideSingleClipName;
            }
            AnimationClip finalClip = SanitizeOverscaledPositionCurvesIfNeeded(clip, clipKey);
            AnimatorState st = sm.AddState(clipKey);
            st.motion = finalClip;
            st.speedParameterActive = true;
            st.speedParameter = "AnimSpeed";
            if (string.Equals(clipKey, "Combat_Stance", StringComparison.OrdinalIgnoreCase))
            {
                sm.defaultState = st;
            }
            Debug.Log("[TerasuPrefab] Registered AnimatorState '" + clipKey + "' (len=" + finalClip.length.ToString("F2") + "s)");
        }
    }

    private static AnimationClip SanitizeOverscaledPositionCurvesIfNeeded(AnimationClip srcClip, string clipKey)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(srcClip);
        bool hasOverscaledPos = false;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].propertyName != null && bindings[i].propertyName.Contains("m_LocalPosition"))
            {
                AnimationCurve c = AnimationUtility.GetEditorCurve(srcClip, bindings[i]);
                if (c != null)
                {
                    Keyframe[] keys = c.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        if (Mathf.Abs(keys[k].value) > 2.0f)
                        {
                            hasOverscaledPos = true;
                            break;
                        }
                    }
                }
            }
            if (hasOverscaledPos) break;
        }

        if (!hasOverscaledPos)
        {
            return srcClip;
        }

        string cleanPath = "Assets/Resources/characters/" + clipKey + "_Clean.anim";
        if (System.IO.File.Exists(cleanPath))
        {
            AssetDatabase.DeleteAsset(cleanPath);
        }
        AnimationClip cleanClip = new AnimationClip();
        cleanClip.name = clipKey;
        cleanClip.frameRate = srcClip.frameRate;
        AnimationUtility.SetAnimationClipSettings(cleanClip, AnimationUtility.GetAnimationClipSettings(srcClip));
        int strippedPos = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding b = bindings[i];
            if (b.propertyName != null && b.propertyName.Contains("m_LocalPosition"))
            {
                strippedPos++;
                continue;
            }
            AnimationCurve c = AnimationUtility.GetEditorCurve(srcClip, b);
            if (c != null)
            {
                AnimationUtility.SetEditorCurve(cleanClip, b, new AnimationCurve(c.keys));
            }
        }
        AssetDatabase.CreateAsset(cleanClip, cleanPath);
        Debug.Log("[TerasuPrefab] Stripped " + strippedPos + " overscaled (cm->m 100x) m_LocalPosition curves from '" + clipKey + "' -> " + cleanPath);
        return cleanClip;
    }

    [MenuItem("Tools/SVZ/Terasu/0. Build Standalone PFTerasuBloodMoon Prefab")]
    public static void BuildStandaloneTerasuPrefab()
    {
        string meshyDir = "Assets/Resources/characters/TerasuMeshy";
        string propDir = "Assets/Resources/props";
        string prefabDir = "Assets/Resources/characters";
        string matDir = prefabDir + "/Materials";
        if (!System.IO.Directory.Exists(prefabDir)) System.IO.Directory.CreateDirectory(prefabDir);
        if (!System.IO.Directory.Exists(matDir)) System.IO.Directory.CreateDirectory(matDir);

        string godBcPath = meshyDir + "/terasu_god_bc.png";
        string godNrmPath = meshyDir + "/terasu_god_nrm.png";
        string spearBcPath = propDir + "/terasu_spear_bc.png";
        string spearNrmPath = propDir + "/terasu_spear_nrm.png";
        ConfigureTextureImport(godBcPath, false);
        ConfigureTextureImport(godNrmPath, true);
        ConfigureTextureImport(spearBcPath, false);
        ConfigureTextureImport(spearNrmPath, true);

        Texture2D godBc = (Texture2D)AssetDatabase.LoadAssetAtPath(godBcPath, typeof(Texture2D));
        Texture2D godNrm = (Texture2D)AssetDatabase.LoadAssetAtPath(godNrmPath, typeof(Texture2D));
        Texture2D spearBc = (Texture2D)AssetDatabase.LoadAssetAtPath(spearBcPath, typeof(Texture2D));
        Texture2D spearNrm = (Texture2D)AssetDatabase.LoadAssetAtPath(spearNrmPath, typeof(Texture2D));

        Shader stdShader = Shader.Find("Standard");
        if (stdShader == null) stdShader = Shader.Find("Bumped Diffuse");
        if (stdShader == null) stdShader = Shader.Find("Diffuse");
        Shader unlitColor = Shader.Find("Unlit/Color");

        Material matGod = GetOrCreateMaterial(matDir + "/Mat_TerasuGod.mat", stdShader, godBc, godNrm, Color.white);
        if (matGod.HasProperty("_Glossiness")) matGod.SetFloat("_Glossiness", 0.28f);
        if (matGod.HasProperty("_Metallic")) matGod.SetFloat("_Metallic", 0.12f);
        if (matGod.HasProperty("_EmissionColor"))
        {
            matGod.EnableKeyword("_EMISSION");
            matGod.SetTexture("_EmissionMap", godBc);
            matGod.SetColor("_EmissionColor", new Color(0.58f, 0.44f, 0.46f));
        }

        Material matRuneCore = GetOrCreateMaterial(matDir + "/Mat_TerasuRuneCore.mat", unlitColor, null, null, new Color(1.0f, 0.28f, 0.08f, 1.0f));

        Material matSpear = GetOrCreateMaterial(matDir + "/Mat_TerasuSpear.mat", stdShader, spearBc, spearNrm, Color.white);
        if (matSpear.HasProperty("_Glossiness")) matSpear.SetFloat("_Glossiness", 0.35f);
        if (matSpear.HasProperty("_Metallic")) matSpear.SetFloat("_Metallic", 0.18f);
        if (matSpear.HasProperty("_EmissionColor"))
        {
            matSpear.EnableKeyword("_EMISSION");
            matSpear.SetTexture("_EmissionMap", spearBc);
            matSpear.SetColor("_EmissionColor", new Color(0.68f, 0.22f, 0.20f));
        }

        string baseFbx = meshyDir + "/Terasu_Base.fbx";
        string wheelFbx = meshyDir + "/Terasu_HaloWheel.fbx";
        string deityFbx = meshyDir + "/Terasu_Anims_Deity.fbx";
        string spearFbx = propDir + "/terasu_blood_moon_spear.fbx";

        ConfigureModelImporter(baseFbx, ModelImporterAnimationType.Generic, null, true, false);
        Avatar baseAvatar = FindAvatarInFbx(baseFbx);
        ConfigureModelImporter(wheelFbx, ModelImporterAnimationType.None, null, false, false);
        ConfigureModelImporter(spearFbx, ModelImporterAnimationType.None, null, false, false);
        ConfigureModelImporter(deityFbx, ModelImporterAnimationType.Generic, baseAvatar, true, false);
        AssetDatabase.Refresh();

        // 1. Build PFTerasuBloodMoonSpear.prefab (blade aligned cleanly along +Z forward)
        GameObject spearFbxAsset = (GameObject)AssetDatabase.LoadAssetAtPath(spearFbx, typeof(GameObject));
        if (spearFbxAsset != null)
        {
            GameObject spearRoot = new GameObject("PFTerasuBloodMoonSpear");
            GameObject spearMesh = (GameObject)UnityEngine.Object.Instantiate(spearFbxAsset);
            spearMesh.name = "SpearMesh";
            spearMesh.transform.SetParent(spearRoot.transform, false);
            spearMesh.transform.localPosition = Vector3.zero;
            spearMesh.transform.localRotation = Quaternion.identity;
            spearMesh.transform.localScale = Vector3.one;

            MeshFilter mf = spearMesh.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 sz = mf.sharedMesh.bounds.size;
                Vector3 bmin = mf.sharedMesh.bounds.min;
                Vector3 bmax = mf.sharedMesh.bounds.max;
                Debug.Log("[TerasuPrefab] Spear raw bounds size=" + sz + ", min=" + bmin + ", max=" + bmax);
                // Orient longest axis to +Z forward!
                if (sz.y > sz.z && sz.y > sz.x)
                {
                    spearMesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                else if (sz.x > sz.z && sz.x > sz.y)
                {
                    spearMesh.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                }
            }
            Renderer[] sr = spearRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < sr.Length; i++)
            {
                sr[i].sharedMaterial = matSpear;
            }
            string spearPrefabPath = propDir + "/PFTerasuBloodMoonSpear.prefab";
            PrefabUtility.CreatePrefab(spearPrefabPath, spearRoot, ReplacePrefabOptions.ReplaceNameBased);
            UnityEngine.Object.DestroyImmediate(spearRoot);
            Debug.Log("[TerasuPrefab] Built " + spearPrefabPath);
        }

        // 2. Build PFTerasuBloodMoon.prefab
        GameObject fbxAsset = (GameObject)AssetDatabase.LoadAssetAtPath(baseFbx, typeof(GameObject));
        if (fbxAsset == null)
        {
            Debug.LogError("[TerasuPrefab] Failed to load FBX at " + baseFbx);
            return;
        }
        GameObject inst = (GameObject)UnityEngine.Object.Instantiate(fbxAsset);
        inst.name = "PFTerasuBloodMoon";
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        // Check facing (+Z check via Head vs headfront)
        Transform tHead = FindDeepChild(inst.transform, "Head");
        Transform tHeadFront = FindDeepChild(inst.transform, "headfront");
        if (tHead != null && tHeadFront != null)
        {
            Debug.Log("[TerasuPrefab] Head pos=" + tHead.position + ", headfront pos=" + tHeadFront.position + " (deltaZ=" + (tHeadFront.position.z - tHead.position.z).ToString("F4") + ")");
        }

        SkinnedMeshRenderer smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null)
        {
            smr.sharedMaterial = matGod;
            smr.updateWhenOffscreen = true;
            Debug.Log("[TerasuPrefab] Body bounds size=" + smr.bounds.size + ", min=" + smr.bounds.min + ", max=" + smr.bounds.max);
        }

        // Setup Generic Mecanim Animator + AnimatorController (immune to Legacy BlendOptimized crash!)
        string ctrlPath = prefabDir + "/TerasuAnimatorController.controller";
        if (System.IO.File.Exists(ctrlPath))
        {
            AssetDatabase.DeleteAsset(ctrlPath);
        }
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        AnimatorControllerParameter speedParam = new AnimatorControllerParameter();
        speedParam.name = "AnimSpeed";
        speedParam.type = AnimatorControllerParameterType.Float;
        speedParam.defaultFloat = 1.0f;
        ac.AddParameter(speedParam);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;
        AddAnimatorStatesFromFbx(sm, deityFbx, null);
        EditorUtility.SetDirty(ac);

        Animation oldLegacy = inst.GetComponent<Animation>();
        if (oldLegacy != null) UnityEngine.Object.DestroyImmediate(oldLegacy);
        Animator animator = inst.GetComponent<Animator>();
        if (animator == null) animator = inst.AddComponent<Animator>();
        animator.runtimeAnimatorController = ac;
        if (baseAvatar != null) animator.avatar = baseAvatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Attach detached 6-drum Enel+Mahoraga Halo Wheel cleanly behind upper back under root (ZERO Spine 45-deg tilt clipping!)
        GameObject wheelAsset = (GameObject)AssetDatabase.LoadAssetAtPath(wheelFbx, typeof(GameObject));
        if (wheelAsset != null)
        {
            GameObject wheelPivot = new GameObject("Terasu_HaloWheelPivot");
            wheelPivot.transform.SetParent(inst.transform, false);
            wheelPivot.transform.localPosition = new Vector3(0f, 1.48f, -0.46f);
            wheelPivot.transform.localRotation = Quaternion.identity;

            GameObject wheelMesh = (GameObject)UnityEngine.Object.Instantiate(wheelAsset);
            wheelMesh.name = "Terasu_HaloWheelMesh";
            wheelMesh.transform.SetParent(wheelPivot.transform, false);
            wheelMesh.transform.localPosition = Vector3.zero;
            wheelMesh.transform.localRotation = Quaternion.identity;
            wheelMesh.transform.localScale = Vector3.one * 1.15f;
            Renderer[] wr = wheelMesh.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < wr.Length; i++)
            {
                if (wr[i].sharedMaterials != null && wr[i].sharedMaterials.Length >= 2)
                {
                    wr[i].sharedMaterials = new Material[] { matGod, matRuneCore };
                }
                else
                {
                    wr[i].sharedMaterial = matGod;
                }
            }
        }

        // Add RightHand Spear Mount & LeftHand Fist Socket empties
        Transform rightHandBone = FindDeepChild(inst.transform, "RightHand");
        if (rightHandBone != null)
        {
            GameObject mountR = new GameObject("Terasu_RightHandMount");
            mountR.transform.position = rightHandBone.position;
            mountR.transform.rotation = Quaternion.identity;
            mountR.transform.SetParent(rightHandBone, true);
        }
        Transform leftHandBone = FindDeepChild(inst.transform, "LeftHand");
        if (leftHandBone != null)
        {
            GameObject mountL = new GameObject("Terasu_LeftHandMount");
            mountL.transform.position = leftHandBone.position;
            mountL.transform.rotation = Quaternion.identity;
            mountL.transform.SetParent(leftHandBone, true);
        }

        string prefabPath = prefabDir + "/PFTerasuBloodMoon.prefab";
        GameObject savedPrefab = PrefabUtility.CreatePrefab(prefabPath, inst, ReplacePrefabOptions.ReplaceNameBased);
        UnityEngine.Object.DestroyImmediate(inst);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TerasuPrefab] Built rigged Meshy prefab: " + (savedPrefab != null ? AssetDatabase.GetAssetPath(savedPrefab) : prefabPath));
    }

    [MenuItem("Tools/SVZ/Terasu/1. Spawn Phase 1 Shogun & Backline Targets")]
    public static void SpawnShogunAndBacklineTargets()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TerasuPlaytest] Enter Play Mode in InGame.unity first.");
            return;
        }

        Application.runInBackground = true;
        ClearBlockingPauseDialog();

        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        WaveManager wm = WeakGlobalInstance<WaveManager>.instance;
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (inGame == null || wm == null || cm == null)
        {
            Debug.LogError("[TerasuPlaytest] InGameImpl / WaveManager / CharactersManager not ready.");
            return;
        }

        inGame.allAlliesInvincibleTimer = 240f;

        float leftZ = (inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 100f;
        float rightZ = (inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 1400f;
        float minWalk = Mathf.Min(leftZ, rightZ);
        float maxWalk = Mathf.Max(leftZ, rightZ);
        float openMinZ = Mathf.Max(minWalk + 120f, 380f);
        float bridgeZ = (maxWalk > 2500f) ? 780f : Mathf.Lerp(openMinZ, maxWalk - 150f, 0.45f);

        // Place Hero out in the open bridge lane
        float xLane = 0f;
        if (inGame.hero != null)
        {
            xLane = inGame.hero.position.x;
            inGame.hero.maxHealth = 99999f;
            inGame.hero.health = 99999f;
            if (inGame.hero.controller != null)
            {
                inGame.hero.controller.position = new Vector3(xLane, 0f, bridgeZ - 190f);
            }
        }

        // Remove any existing Shogun/enemies
        List<Character> alive = cm.allAlive;
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            Character c = alive[i];
            if (c != null && c.isEnemy && !c.isBase)
            {
                cm.DestroyCharacter(c);
            }
        }

        Vector3 shogunPos = new Vector3(xLane, 0f, bridgeZ + 80f);
        Enemy shogun = wm.ConstructEnemyAt("Shogun", shogunPos, false);
        BloodMoonTerasuController spawnedCtrl = null;
        if (shogun != null)
        {
            spawnedCtrl = BloodMoonTerasuController.GetOrAttach(shogun);
            shogun.maxHealth = 60000f;
            shogun.health = 60000f;
            shogun.meleeDamage = 40f;
            if (shogun.controller != null)
            {
                shogun.controller.constraintLeft = Mathf.Min(leftZ, rightZ);
                shogun.controller.constraintRight = Mathf.Max(leftZ, rightZ);
            }
        }

        Enemy frontAlly = wm.ConstructEnemyAt("H_Zombie_Sword", new Vector3(xLane - 24f, 0f, bridgeZ - 70f), true);
        if (frontAlly != null)
        {
            frontAlly.maxHealth = 99999f;
            frontAlly.health = 99999f;
        }

        Enemy backlineAlly = wm.ConstructEnemyAt("H_Zombie_Bow", new Vector3(xLane + 18f, 0f, bridgeZ - 260f), true);
        if (backlineAlly != null)
        {
            backlineAlly.maxHealth = 99999f;
            backlineAlly.health = 99999f;
        }

        GameObject go = GameObject.Find("TerasuPlaytestKeeper");
        if (go == null)
        {
            go = new GameObject("TerasuPlaytestKeeper");
        }
        TerasuPlaytestKeeper keeper = go.GetComponent<TerasuPlaytestKeeper>();
        if (keeper == null)
        {
            keeper = go.AddComponent<TerasuPlaytestKeeper>();
        }
        keeper.targetCtrl = spawnedCtrl;
        keeper.targetChar = shogun;
        keeper.autoSequenceActive = true;
        keeper.sequenceStep = 0;

        Debug.Log("[TerasuPlaytest] Open-lane Showcase Started -> Shogun Z=" + shogunPos.z + ", Hero Z=" + (bridgeZ - 190f) + ", Backline Z=" + (bridgeZ - 260f));
    }

    [MenuItem("Tools/SVZ/Terasu/2. Trigger Fatal Blow (Start Blood Moon Ascension)")]
    public static void TriggerFatalBlow()
    {
        if (!Application.isPlaying) return;
        ClearBlockingPauseDialog();
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null) return;

        List<Character> alive = cm.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (BloodMoonTerasuController.IsUndeadShogun(c))
            {
                c.RecievedAttack(EAttackType.BladeCritical, c.health + 5000f);
                BloodMoonTerasuController ctrl = c.controlledObject != null
                    ? c.controlledObject.GetComponent<BloodMoonTerasuController>()
                    : null;
                Debug.Log("[TerasuPlaytest] Triggered Fatal Blow -> stage=" + (ctrl != null ? ctrl.currentStage.ToString() : "null"));
                return;
            }
        }
    }

    [MenuItem("Tools/SVZ/Terasu/3. Trigger Sun Wukong Backline Spear Ult")]
    public static void TriggerSpearUlt()
    {
        if (!Application.isPlaying) return;
        ClearBlockingPauseDialog();
        BloodMoonTerasuController ctrl = UnityEngine.Object.FindObjectOfType<BloodMoonTerasuController>();
        if (ctrl == null)
        {
            Debug.LogWarning("[TerasuPlaytest] No BloodMoonTerasuController found.");
            return;
        }
        ctrl.TriggerSpearUltimateNow();
        Debug.Log("[TerasuPlaytest] Spear Ult triggered -> state=" + ctrl.currentUltState.ToString());
    }

    private const string kPendingWave100Key = "SVZ_PendingWave100Test";

    [InitializeOnLoadMethod]
    private static void InitWave100PlayHook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChangedForWave100;
        EditorApplication.playModeStateChanged += OnPlayModeStateChangedForWave100;
    }

    private static void OnPlayModeStateChangedForWave100(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(kPendingWave100Key, false))
        {
            EditorPrefs.DeleteKey(kPendingWave100Key);
            ConfigureWave100AndReloadScene();
        }
    }

    [MenuItem("Tools/SVZ/Terasu/4. Start Wave 100 (Hero Immortal + Shogun First)")]
    public static void StartWave100Test()
    {
        if (Application.isPlaying)
        {
            ConfigureWave100AndReloadScene();
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorPrefs.SetBool(kPendingWave100Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/InGame_hell_purple.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
    }

    private static void ConfigureWave100AndReloadScene()
    {
        Singleton<PlayModesManager>.instance.selectedMode = "classic";
        Singleton<Profile>.instance.ClearBonusWaveData();
        for (int i = 1; i < 100; i++)
        {
            if (Singleton<Profile>.instance.GetWaveLevel(i) == 0)
            {
                Singleton<Profile>.instance.SetWaveLevel(i, 2);
            }
        }
        Singleton<Profile>.instance.waveToBeat = 100;
        Singleton<Profile>.instance.SetWaveLevel(100, 1);
        Singleton<Profile>.instance.Save();
        DevCheatOverlay.sGodMode = true;
        Application.runInBackground = true;
        Debug.Log("[TerasuPlaytest] Wave 100 configured -> mode=classic, waveToBeat=100, sGodMode=true");
        SceneManager.LoadScene("InGame_hell_purple");
    }

    private const string kCharacterLabSourceScenePath = "Assets/Scenes/InGame_hell_purple.unity";
    private const string kCharacterLabScenePath = "Assets/Scenes/SVZ_CharacterTestLab.unity";
    private const string kCharacterLabStudioName = "SVZ_CharacterTestLab_Studio";

    public static void CreateOrUpdateCharacterLabScene()
    {
        if (!System.IO.File.Exists(kCharacterLabScenePath))
        {
            if (!AssetDatabase.CopyAsset(kCharacterLabSourceScenePath, kCharacterLabScenePath))
            {
                Debug.LogError("[SVZLab] Failed to copy source scene to " + kCharacterLabScenePath);
                return;
            }
            AssetDatabase.Refresh();
        }

        UnityEngine.SceneManagement.Scene labScene = EditorSceneManager.OpenScene(kCharacterLabScenePath, OpenSceneMode.Single);

        // Remove any stray Edit-Mode Terasu T-pose prefabs dragged into the scene
        string[] strayNames = new string[] { "PFTerasuBloodMoon", "PFTerasuBloodMoon(Clone)" };
        for (int i = 0; i < strayNames.Length; i++)
        {
            GameObject stray = GameObject.Find(strayNames[i]);
            while (stray != null)
            {
                UnityEngine.Object.DestroyImmediate(stray);
                stray = GameObject.Find(strayNames[i]);
            }
        }

        GameObject studio = GameObject.Find(kCharacterLabStudioName);
        if (studio == null)
        {
            studio = new GameObject(kCharacterLabStudioName);
        }
        if (studio.GetComponent<SVZCharacterLabController>() == null)
        {
            studio.AddComponent<SVZCharacterLabController>();
        }

        EnsureSceneInBuildSettings(kCharacterLabScenePath);
        EditorSceneManager.MarkSceneDirty(labScene);
        EditorSceneManager.SaveScene(labScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SVZLab] Character Test Lab scene ready: " + kCharacterLabScenePath);
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        for (int i = 0; i < existing.Length; i++)
        {
            if (string.Equals(existing[i].path, scenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                if (!existing[i].enabled)
                {
                    existing[i].enabled = true;
                    EditorBuildSettings.scenes = existing;
                }
                return;
            }
        }

        System.Collections.Generic.List<EditorBuildSettingsScene> updated =
            new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
        updated.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = updated.ToArray();
    }

    [MenuItem("Tools/SVZ/Terasu/00. Open Terasu Character Test Lab Scene")]
    public static void OpenCharacterTestLabScene()
    {
        if (Application.isPlaying)
        {
            ClearBlockingPauseDialog();
            if (SceneManager.GetActiveScene().name != "SVZ_CharacterTestLab" &&
                Application.CanStreamedLevelBeLoaded("SVZ_CharacterTestLab"))
            {
                SceneManager.LoadScene("SVZ_CharacterTestLab");
            }
            else
            {
                EnsureLabControllerInPlayMode(false);
            }
            return;
        }

        CreateOrUpdateCharacterLabScene();
        Application.runInBackground = true;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/SVZ/Terasu/01. Toggle Character Test Lab HUD")]
    public static void ToggleCharacterLabInCurrentScene()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "SVZ Character Test Lab",
                "HUD sadece Play Modunda acilip kapanir. Once 'Tools -> SVZ -> Terasu -> 00. Open Terasu Character Test Lab Scene' ile Lab sahnesini baslatin.",
                "Tamam"
            );
            return;
        }

        ClearBlockingPauseDialog();
        EnsureLabControllerInPlayMode(true);
    }

    private static void EnsureLabControllerInPlayMode(bool toggleIfPresent)
    {
        SVZCharacterLabController existing = UnityEngine.Object.FindObjectOfType<SVZCharacterLabController>();
        if (existing != null)
        {
            if (toggleIfPresent)
            {
                existing.ToggleHud();
            }
            return;
        }

        GameObject studio = GameObject.Find(kCharacterLabStudioName);
        if (studio == null)
        {
            studio = new GameObject(kCharacterLabStudioName);
        }
        studio.AddComponent<SVZCharacterLabController>();
        Debug.Log("[SVZLab] Attached SVZCharacterLabController in Play Mode.");
    }

    [MenuItem("Tools/SVZ/Iron Butcher/Build PFIronButcher Prefab")]
    public static void BuildIronButcherPrefab()
    {
        string ibDir = "Assets/Resources/characters/IronButcherMeshy";
        string clipsDir = ibDir + "/Clips";
        string prefabDir = "Assets/Resources/characters";
        string iconPath = "Assets/Resources/Sprites/Icons/enemy_iron_butcher.png";
        string iconLockedPath = "Assets/Resources/Sprites/Icons/enemy_iron_butcher_locked.png";

        if (!System.IO.Directory.Exists(clipsDir))
        {
            System.IO.Directory.CreateDirectory(clipsDir);
        }

        // 1. Configure HUD Icons (Default non-NPOT crisp UI textures)
        ConfigureIronButcherIconImport(iconPath);
        ConfigureIronButcherIconImport(iconLockedPath);

        // 2. Configure PBR textures & create IronButcherMat.mat
        string albedoPath = ibDir + "/IronButcher_Albedo.png";
        string normalPath = ibDir + "/IronButcher_Normal.png";
        string metallicPath = ibDir + "/IronButcher_Metallic.png";
        string roughnessPath = ibDir + "/IronButcher_Roughness.png";

        ConfigureTextureImport(albedoPath, false);
        ConfigureTextureImport(normalPath, true);
        ConfigureTextureImport(metallicPath, false);
        ConfigureTextureImport(roughnessPath, false);

        Texture2D albedoTex = (Texture2D)AssetDatabase.LoadAssetAtPath(albedoPath, typeof(Texture2D));
        Texture2D normalTex = (Texture2D)AssetDatabase.LoadAssetAtPath(normalPath, typeof(Texture2D));
        Texture2D metallicTex = (Texture2D)AssetDatabase.LoadAssetAtPath(metallicPath, typeof(Texture2D));

        Shader stdShader = Shader.Find("Standard");
        if (stdShader == null) stdShader = Shader.Find("Bumped Diffuse");
        if (stdShader == null) stdShader = Shader.Find("Diffuse");

        Material ibMat = GetOrCreateMaterial(ibDir + "/IronButcherMat.mat", stdShader, albedoTex, normalTex, Color.white);
        if (metallicTex != null && ibMat.HasProperty("_MetallicGlossMap"))
        {
            ibMat.SetTexture("_MetallicGlossMap", metallicTex);
            ibMat.EnableKeyword("_METALLICGLOSSMAP");
        }
        if (normalTex != null)
        {
            ibMat.EnableKeyword("_NORMALMAP");
        }
        if (ibMat.HasProperty("_BumpScale")) ibMat.SetFloat("_BumpScale", 1.0f);
        if (ibMat.HasProperty("_Metallic")) ibMat.SetFloat("_Metallic", 0.45f);
        if (ibMat.HasProperty("_Glossiness")) ibMat.SetFloat("_Glossiness", 0.25f);
        if (albedoTex != null && ibMat.HasProperty("_EmissionColor"))
        {
            ibMat.EnableKeyword("_EMISSION");
            ibMat.SetTexture("_EmissionMap", albedoTex);
            ibMat.SetColor("_EmissionColor", new Color(0.16f, 0.14f, 0.14f));
        }
        EditorUtility.SetDirty(ibMat);

        // 3. Configure Meshy FBX ModelImporters (Base Generic Avatar + 8 Anims + WalkBasic + RunBasic)
        string baseFbx = ibDir + "/IronButcher_Base.fbx";
        string animsFbx = ibDir + "/IronButcher_Anims.fbx";
        string walkFbx = ibDir + "/IronButcher_WalkBasic.fbx";
        string runFbx = ibDir + "/IronButcher_RunBasic.fbx";

        ConfigureIronButcherModelImporter(baseFbx, ModelImporterAnimationType.Generic, null, false);
        Avatar baseAvatar = FindAvatarInFbx(baseFbx);
        if (baseAvatar == null)
        {
            ConfigureIronButcherModelImporter(baseFbx, ModelImporterAnimationType.Generic, null, true);
            baseAvatar = FindAvatarInFbx(baseFbx);
        }
        ConfigureIronButcherModelImporter(animsFbx, ModelImporterAnimationType.Generic, baseAvatar, true);
        ConfigureIronButcherModelImporter(walkFbx, ModelImporterAnimationType.Generic, baseAvatar, true);
        ConfigureIronButcherModelImporter(runFbx, ModelImporterAnimationType.Generic, baseAvatar, true);
        AssetDatabase.Refresh();

        // 4. Build IronButcherAnimatorController.controller with isolated clean clips
        string ctrlPath = prefabDir + "/IronButcherAnimatorController.controller";
        if (System.IO.File.Exists(ctrlPath))
        {
            AssetDatabase.DeleteAsset(ctrlPath);
        }
        AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        AnimatorControllerParameter speedParam = new AnimatorControllerParameter();
        speedParam.name = "AnimSpeed";
        speedParam.type = AnimatorControllerParameterType.Float;
        speedParam.defaultFloat = 1.0f;
        ac.AddParameter(speedParam);

        AnimatorStateMachine sm = ac.layers[0].stateMachine;
        HashSet<string> addedStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIronButcherAnimatorStatesFromFbx(sm, animsFbx, null, clipsDir, addedStates);
        AddIronButcherAnimatorStatesFromFbx(sm, walkFbx, "walking", clipsDir, addedStates);
        AddIronButcherAnimatorStatesFromFbx(sm, runFbx, "running", clipsDir, addedStates);
        EditorUtility.SetDirty(ac);

        // 5. Instantiate PFHzombieAxe host + attach IronButcher_Visual
        string hostPrefabPath = prefabDir + "/PFHzombieAxe.prefab";
        GameObject hostPrefab = (GameObject)AssetDatabase.LoadAssetAtPath(hostPrefabPath, typeof(GameObject));
        GameObject baseFbxAsset = (GameObject)AssetDatabase.LoadAssetAtPath(baseFbx, typeof(GameObject));
        if (hostPrefab == null || baseFbxAsset == null)
        {
            Debug.LogError("[IronButcherPrefab] Missing host prefab or base FBX: host=" + hostPrefab + " base=" + baseFbxAsset);
            return;
        }

        GameObject host = (GameObject)UnityEngine.Object.Instantiate(hostPrefab);
        host.name = "PFIronButcher";
        host.transform.position = Vector3.zero;
        host.transform.rotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;

        // Measure host height in SVZ1 world space before hiding its renderers
        float hostHeight = MeasureRenderersHeight(host);

        // Remove legacy PrefabLink metadata so PFHzombie never resets PFIronButcher
        PrefabLink[] links = host.GetComponentsInChildren<PrefabLink>(true);
        for (int i = 0; i < links.Length; i++)
        {
            if (links[i] != null) UnityEngine.Object.DestroyImmediate(links[i]);
        }

        // Hide all legacy PFHzombieAxe renderers
        Renderer[] hostRenderers = host.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < hostRenderers.Length; i++)
        {
            if (hostRenderers[i] != null) hostRenderers[i].enabled = false;
        }

        // Instantiate IronButcher_Visual as child of host root
        GameObject visualGo = (GameObject)UnityEngine.Object.Instantiate(baseFbxAsset);
        visualGo.name = "IronButcher_Visual";
        visualGo.transform.SetParent(host.transform, false);
        visualGo.transform.localPosition = Vector3.zero;
        visualGo.transform.localRotation = Quaternion.identity;
        visualGo.transform.localScale = Vector3.one;

        float rawVisualHeight = MeasureRenderersHeight(visualGo);
        // rawVisualHeight includes raised weapon tip (~2.32m vs ~1.70m body), so target ~195 world units total for Heavy Samurai torso parity
        float desiredHeight = (hostHeight > 80f && hostHeight < 260f) ? Mathf.Max(195f, hostHeight * 0.785f) : 195f;
        float computedScale = (rawVisualHeight > 0.05f) ? (desiredHeight / rawVisualHeight) : 84f;
        visualGo.transform.localScale = Vector3.one * computedScale;

        Renderer[] visualRenderers = visualGo.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            Renderer vr = visualRenderers[i];
            if (vr == null) continue;
            if (vr.sharedMaterials != null && vr.sharedMaterials.Length > 1)
            {
                Material[] arr = new Material[vr.sharedMaterials.Length];
                for (int m = 0; m < arr.Length; m++) arr[m] = ibMat;
                vr.sharedMaterials = arr;
            }
            else
            {
                vr.sharedMaterial = ibMat;
            }
        }

        Animation oldVisualAnim = visualGo.GetComponent<Animation>();
        if (oldVisualAnim != null) UnityEngine.Object.DestroyImmediate(oldVisualAnim);

        Animator animator = visualGo.GetComponent<Animator>();
        if (animator == null) animator = visualGo.AddComponent<Animator>();
        animator.runtimeAnimatorController = ac;
        if (baseAvatar != null) animator.avatar = baseAvatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        IronButcherController ctrl = host.GetComponent<IronButcherController>();
        if (ctrl == null) ctrl = host.AddComponent<IronButcherController>();

        string ibPrefabPath = prefabDir + "/PFIronButcher.prefab";
        GameObject savedPrefab = PrefabUtility.CreatePrefab(ibPrefabPath, host, ReplacePrefabOptions.ConnectToPrefab);
        UnityEngine.Object.DestroyImmediate(host);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(string.Format(
            "[IronButcherPrefab] Built {0} (hostH={1:F1}, rawVisualH={2:F3}, scale={3:F2}, targetH={4:F1})",
            savedPrefab != null ? AssetDatabase.GetAssetPath(savedPrefab) : ibPrefabPath,
            hostHeight, rawVisualHeight, computedScale, desiredHeight
        ));
    }

    private static float MeasureRenderersHeight(GameObject root)
    {
        if (root == null) return 0f;
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            if (!hasBounds)
            {
                b = rends[i].bounds;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(rends[i].bounds);
            }
        }
        return hasBounds ? b.size.y : 0f;
    }

    private static void ConfigureIronButcherIconImport(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
    }

    private static void ConfigureIronButcherModelImporter(string fbxPath, ModelImporterAnimationType animType, Avatar sourceAvatar, bool importAnim)
    {
        ModelImporter mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi == null) return;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.isReadable = true;
        mi.importMaterials = false;
        mi.optimizeGameObjects = false;
        mi.animationType = animType;
        mi.importAnimation = importAnim;
        mi.animationCompression = ModelImporterAnimationCompression.Off;
        if (animType == ModelImporterAnimationType.Generic && sourceAvatar != null)
        {
            mi.sourceAvatar = sourceAvatar;
        }
        if (importAnim)
        {
            ModelImporterClipAnimation[] clips = mi.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    string cn = clips[i].name;
                    bool shouldLoop =
                        cn.IndexOf("Combat_Stance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cn.IndexOf("Monster_Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cn.IndexOf("walking", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cn.IndexOf("running", StringComparison.OrdinalIgnoreCase) >= 0;
                    clips[i].loopTime = shouldLoop;
                    clips[i].wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Once;
                }
                mi.clipAnimations = clips;
            }
        }
        mi.SaveAndReimport();
    }

    private static void AddIronButcherAnimatorStatesFromFbx(AnimatorStateMachine sm, string fbxPath, string overrideSingleClipName, string cleanClipsDir, HashSet<string> addedStates)
    {
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (subAssets == null) return;
        for (int i = 0; i < subAssets.Length; i++)
        {
            AnimationClip clip = subAssets[i] as AnimationClip;
            if (clip == null) continue;
            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
            string clipKey = clip.name;
            int pipeIdx = clipKey.LastIndexOf('|');
            if (pipeIdx >= 0 && pipeIdx + 1 < clipKey.Length)
            {
                clipKey = clipKey.Substring(pipeIdx + 1);
            }
            if (!string.IsNullOrEmpty(overrideSingleClipName))
            {
                clipKey = overrideSingleClipName;
            }
            if (addedStates.Contains(clipKey))
            {
                continue;
            }
            addedStates.Add(clipKey);

            AnimationClip finalClip = SanitizeIronButcherOverscaledCurvesIfNeeded(clip, clipKey, cleanClipsDir);
            AnimatorState st = sm.AddState(clipKey);
            st.motion = finalClip;
            st.speedParameterActive = true;
            st.speedParameter = "AnimSpeed";
            if (string.Equals(clipKey, "Combat_Stance", StringComparison.OrdinalIgnoreCase))
            {
                sm.defaultState = st;
            }
            Debug.Log("[IronButcherPrefab] Registered AnimatorState '" + clipKey + "' (len=" + finalClip.length.ToString("F2") + "s, loop=" + finalClip.isLooping + ")");
        }
    }

    private static AnimationClip SanitizeIronButcherOverscaledCurvesIfNeeded(AnimationClip srcClip, string clipKey, string cleanClipsDir)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(srcClip);
        bool hasOverscaledPos = false;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].propertyName != null && bindings[i].propertyName.Contains("m_LocalPosition"))
            {
                AnimationCurve c = AnimationUtility.GetEditorCurve(srcClip, bindings[i]);
                if (c != null)
                {
                    Keyframe[] keys = c.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        if (Mathf.Abs(keys[k].value) > 2.0f)
                        {
                            hasOverscaledPos = true;
                            break;
                        }
                    }
                }
            }
            if (hasOverscaledPos) break;
        }

        if (!hasOverscaledPos)
        {
            return srcClip;
        }

        string cleanPath = cleanClipsDir + "/" + clipKey + "_Clean.anim";
        if (System.IO.File.Exists(cleanPath))
        {
            AssetDatabase.DeleteAsset(cleanPath);
        }
        AnimationClip cleanClip = new AnimationClip();
        cleanClip.name = clipKey;
        cleanClip.frameRate = srcClip.frameRate;
        AnimationUtility.SetAnimationClipSettings(cleanClip, AnimationUtility.GetAnimationClipSettings(srcClip));
        int strippedPos = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding b = bindings[i];
            if (b.propertyName != null && b.propertyName.Contains("m_LocalPosition"))
            {
                strippedPos++;
                continue;
            }
            AnimationCurve c = AnimationUtility.GetEditorCurve(srcClip, b);
            if (c != null)
            {
                AnimationUtility.SetEditorCurve(cleanClip, b, new AnimationCurve(c.keys));
            }
        }
        AssetDatabase.CreateAsset(cleanClip, cleanPath);
        Debug.Log("[IronButcherPrefab] Stripped " + strippedPos + " overscaled (cm->m 100x) m_LocalPosition curves from '" + clipKey + "' -> " + cleanPath);
        return cleanClip;
    }

    [MenuItem("Tools/SVZ/Iron Butcher/Inspect Iron Butcher in Lab")]
    public static void InspectIronButcherInLab()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[IronButcherLab] Enter Play Mode in SVZ_CharacterTestLab.unity first.");
            return;
        }
        EnsureLabControllerInPlayMode(false);
        SVZCharacterLabController lab = UnityEngine.Object.FindObjectOfType<SVZCharacterLabController>();
        if (lab == null)
        {
            Debug.LogError("[IronButcherLab] SVZCharacterLabController not found.");
            return;
        }
        IronButcherController ib = lab.InspectIronButcherPreset("Combat_Stance");
        Debug.Log("[IronButcherLab] Spawned Iron_Butcher in Lab: ctrl=" + (ib != null) + ", state=" + (ib != null ? ib.currentVisualState : "null"));
    }

    [MenuItem("Tools/SVZ/Iron Butcher/Play Cleave Attack in Lab")]
    public static void PlayIronButcherCleaveInLab()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        SVZCharacterLabController lab = UnityEngine.Object.FindObjectOfType<SVZCharacterLabController>();
        IronButcherController ib = UnityEngine.Object.FindObjectOfType<IronButcherController>();
        if (ib == null && lab != null)
        {
            ib = lab.InspectIronButcherPreset("Double_Combo_Attack");
        }
        if (ib != null)
        {
            ib.PlayManualAction("Double_Combo_Attack", 0.38f, true);
            Debug.Log("[IronButcherLab] Froze IronButcher at Double_Combo_Attack cleave frame (0.38).");
        }
    }
}
