using System;
using System.Collections.Generic;
using UnityEngine;

public class BloodMoonTerasuController : MonoBehaviour
{
    public enum AscensionStage
    {
        None = 0,
        Collapsing = 1,
        EclipseLightning = 2,
        SphereLevitation = 3,
        RebirthBurst = 4,
        AscendedPhase2 = 5
    }

    public enum SpearUltState
    {
        Ready = 0,
        Throwing = 1,
        Impaled = 2,
        Recalling = 3
    }

    private const float kCollapseDuration = 1.2f;
    private const float kLightningDuration = 2.6f;
    private const float kSphereDuration = 5.8f;
    private const float kRebirthDuration = 6.7f;

    private const float kSpearThrowTime = 0.48f;
    private const float kSpearImpaleHoldTime = 1.05f;
    private const float kSpearRecallFlightTime = 0.62f;
    private const float kSpearUltCooldown = 7.0f;

    private static readonly string[] kSpineShrinkJoints = new string[]
    {
        "Shogun_spine_low_jnt",
        "Shogun_spine_mid_jnt",
        "weightSpine_grp"
    };

    private Character mOwner;
    private Transform mVisualRoot;
    private Transform mHeadBone;
    private Transform mChestBone;
    private Transform mLeftClavicleBone;
    private Transform mRightClavicleBone;
    private Transform mRightHandBone;
    private Transform mRightUpArmBone;
    private Transform mRightElbowBone;
    private Transform mLeftUpArmBone;
    private Transform mLeftElbowBone;
    private Transform mLeftHandBone;
    private Transform mPelvisBone;
    private Transform mLeftThighBone;
    private Transform mLeftAnkleBone;
    private Transform mRightThighBone;
    private Transform mRightAnkleBone;
    private Transform mLeftSkirt;
    private Transform mRightSkirt;
    private List<Transform> mShrinkSpines = new List<Transform>();

    private Renderer[] mBodyRenderers;
    private Color[] mOriginalMainColors;
    private bool mCachedRenderers;

    private AscensionStage mStage = AscensionStage.None;
    private float mStageTimer = 0f;
    private bool mIsAscending = false;
    private bool mHasAscended = false;
    private bool mLightningAudioPlayed = false;
    private bool mGongAudioPlayed = false;
    private bool mRoarAudioPlayed = false;
    private bool mDarknessShattered = false;
    private bool mSpherePoseResetDone = false;
    private bool mRebirthApplied = false;

    // Ascension visual VFX
    private GameObject mAscensionVfxRoot;
    private GameObject mBloodMoonBackdrop;
    private GameObject mAscensionSphere;
    private GameObject mCoreCocoonSphere;
    private Renderer mCoreCocoonRenderer;
    private List<LineRenderer> mAscensionRings = new List<LineRenderer>();
    private List<LineRenderer> mLightningBolts = new List<LineRenderer>();
    private float mLightningRetargetTimer = 0f;
    private float mEarthquakePulseTimer = 0f;
    private float mSphereAoeTickTimer = 0f;
    private LineRenderer mGroundEclipseRing;
    private LineRenderer mGroundEclipseInnerRing;
    private GameObject mShockwaveRing;
    private float mShockwaveTimer = 0f;

    // Atmosphere backup, flashes, earthquake shake & banner
    private bool mSavedEnvColors = false;
    private Color mOrigAmbientLight;
    private Color mOrigCamBgColor;
    private float mEclipseScreenAlpha = 0f;
    private float mLightningFlashAlpha = 0f;
    private float mWhiteFlashAlpha = 0f;
    private float mBannerTimer = 0f;
    private float mCameraShakeIntensity = 0f;
    private Vector3 mCameraShakeOffset = Vector3.zero;

    // Standalone Phase 2 Meshy Rigged Terasu Prefab & Bone Bindings
    private GameObject mTerasuStandaloneInstance;
    private Animator mTerasuAnimator;
    private Transform mTHaloWheelPivot;
    private Renderer[] mWheelDrumRenderers;
    private Transform mBoneHips;
    private Vector3 mRestHipsLocalPos = new Vector3(0f, 1.0f, 0f);
    private Transform mBoneSpine;
    private Transform mBoneLeftArm;
    private Transform mBoneLeftForeArm;
    private Transform mBoneLeftHand;
    private Transform mBoneRightArm;
    private Transform mBoneRightForeArm;
    private Transform mBoneRightHand;
    private Transform mBoneLeftUpLeg;
    private Transform mBoneRightUpLeg;
    private Transform mRightHandMount;
    private Transform mLeftHandMount;
    private GameObject mTLeftVortexOrb;
    private ParticleSystem mLeftHandVortexPs;
    private ParticleSystem mRecallHandPs;
    private static Texture2D sSoftGlowTex;
    private static Texture2D sSoftSparkTex;
    private float mKitAttachedTime = 0f;
    private Vector3 mVirtualRightHandPos;
    private Vector3 mVirtualLeftHandPos;
    private GameObject mTInHandSpear;
    private Vector3 mBaseSpearLocalPos = new Vector3(0f, -0.22f, 0f);
    private Vector3 mBaseSpearLocalEuler = new Vector3(-90f, 0f, 0f);
    private Vector3 mBaseSpearLocalScale = Vector3.one;
    private string mLastPlayedClip = string.Empty;
    private bool mManualAttackPendingDamage = false;

    // Test Lab Manual Control & Live Rig Tuning
    public bool labManualMode = false;
    public bool labAutoCycleUlts = false;
    public bool labFreezeClip = false;
    public string labForcedClip = string.Empty;
    public float labForcedNormalizedTime = 0f;
    public float labAnimSpeedMultiplier = 1.0f;
    public float labScaleMultiplier = 1.0f;
    public float labHoverOffsetY = 0f;
    public Vector3 labHaloPosOffset = Vector3.zero;
    public float labHaloScaleMultiplier = 1.0f;
    public Vector3 labSpearPosOffset = Vector3.zero;
    public Vector3 labSpearEulerOffset = Vector3.zero;
    public float labSpearScaleMultiplier = 1.0f;

    private enum Phase2AttackStyle
    {
        SpearThrust = 0,
        SpearSweep = 1,
        ThanosFistWave = 2
    }

    private class ThanosWaveInstance
    {
        public GameObject root;
        public LineRenderer arcOuter;
        public LineRenderer arcInner;
        public ParticleSystem trailPs;
        public Vector3 origin;
        public Vector3 dir;
        public float age;
        public float lifetime;
        public float speed;
        public List<Character> hitList;
    }

    // Phase 2 combat & Thanos gauntlet wave state + melee slash VFX
    private bool mWasAttackAnim = false;
    private bool mWasAttackActive = false;
    private float mMeleeSwingTimer = 0f;
    private float mMeleeSwingDuration = 0.58f;
    private bool mMeleeSlashFxFired = false;
    private Phase2AttackStyle mActiveAttackStyle = Phase2AttackStyle.SpearThrust;
    private int mAttackComboIndex = 0;
    private GameObject mMeleeSlashVfxRoot;
    private LineRenderer mMeleeSlashArcOuter;
    private LineRenderer mMeleeSlashArcInner;
    private float mMeleeSlashVfxTimer = 0f;
    private float mMeleeSlashFacing = 1f;
    private Vector3 mMeleeSlashOrigin;
    private Phase2AttackStyle mMeleeSlashVfxStyle = Phase2AttackStyle.SpearThrust;
    private GameObject mThanosBlastSphere;
    private GameObject mThanosBlastOuterRing;
    private LineRenderer mThanosBlastRingLine;
    private float mThanosBlastTimer = 0f;
    private Vector3 mThanosBlastOrigin;

    // JJK Mahoraga 6-Stack Rotating Dharma Wheel, Lively Levitation, Clean Husk & Thanos Waves
    private int mMahoragaStacks = 0;
    private float mWheelAngleCurrent = 0f;
    private float mWheelAngleTarget = 0f;
    private float mWheelAngleVel = 0f;
    private float mWheelFlashTimer = 0f;
    private float mWheelOverdriveTimer = 0f;
    private float mPassiveStackTimer = 0f;
    private float mSpearUltAutoTimer = 0f;
    private float mSmoothMoveFactor = 0f;
    private float mHuskSpawnTime = -10f;
    private List<ThanosWaveInstance> mThanosWaves = new List<ThanosWaveInstance>();
    private float mObservedMaxCooldown = 2.6f;
    private GameObject mShatteredHusk;
    private LineRenderer mHaloArcLine;
    private LineRenderer mVortexVacuumLine;

    // Backline Spear Hurl, Victim Impale & Telekinetic Recall Ult
    private SpearUltState mUltState = SpearUltState.Ready;
    private float mUltCooldownTimer = 2.6f;
    private float mUltStateTimer = 0f;
    private Character mImpaledTarget;
    private GameObject mFlyingSpear;
    private LineRenderer mSpearTrailLine;
    private LineRenderer mImpalePillarLine;
    private LineRenderer mRecallTetherLine;
    private GameObject mRecallHandOrb;
    private GameObject mImpaleFlashSphere;
    private Vector3 mSpearStartPos;
    private Vector3 mSpearTargetPos;
    private Vector3 mSpearPlungeDir = new Vector3(0f, -0.45f, -0.89f);
    private Quaternion mSpearImpaleRot;
    private float mUltFacingDir = 1f;
    private List<Character> mHitDuringThrow = new List<Character>();
    private List<Character> mHitDuringRecall = new List<Character>();
    private List<Vector3> mSpearTrailHistory = new List<Vector3>();

    // Audio
    private AudioSource mAudioSource;
    private const float kPhase1TestHealth = 1000f;
    private float mPhase2BaseMaxHealth = 0f;
    private AudioClip mClipLightning;
    private AudioClip mClipGong;
    private AudioClip mClipApocalypseRoar;
    private AudioClip mClipRebirth;
    private AudioClip mClipSpearWhoosh;

    // Reusable 1x1 white texture for clean GUI banners & eclipse overlays
    private static Texture2D sWhiteTex;
    private static GUIStyle sTitleStyle;
    private static GUIStyle sSubtitleStyle;
    private static GUIStyle sTagStyle;

    public bool isAscending
    {
        get { return mIsAscending; }
    }

    public bool hasAscended
    {
        get { return mHasAscended; }
    }

    public bool isBusyWithSpecialAction
    {
        get { return mIsAscending || mUltState != SpearUltState.Ready || labManualMode; }
    }

    public int mahoragaStacks
    {
        get { return mMahoragaStacks; }
    }

    public string lastPlayedClip
    {
        get { return mLastPlayedClip; }
    }

    public Character ownerCharacter
    {
        get { return mOwner; }
    }

    public Animator terasuAnimator
    {
        get { return mTerasuAnimator; }
    }

    public AscensionStage currentStage
    {
        get { return mStage; }
    }

    public SpearUltState currentUltState
    {
        get { return mUltState; }
    }

    public float stageTimer
    {
        get { return mStageTimer; }
    }

    public float ultStateTimer
    {
        get { return mUltStateTimer; }
    }

    public Vector3 cameraShakeOffset
    {
        get { return mCameraShakeOffset; }
    }

    public static bool HasAscended(Character c)
    {
        if (c == null || c.controlledObject == null)
        {
            return false;
        }
        BloodMoonTerasuController ctrl = c.controlledObject.GetComponent<BloodMoonTerasuController>();
        return ctrl != null && ctrl.mHasAscended;
    }

    public static bool IsUndeadShogun(Character c)
    {
        if (c == null || c.isBase || c.isPlayer)
        {
            return false;
        }
        if (string.Equals(c.uniqueID, "Shogun", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        GameObject go = c.controlledObject;
        if (go != null && go.name.IndexOf("Undeadshogun", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }
        return false;
    }

    public static BloodMoonTerasuController GetOrAttach(Character c)
    {
        if (c == null || c.controlledObject == null)
        {
            return null;
        }
        BloodMoonTerasuController ctrl = c.controlledObject.GetComponent<BloodMoonTerasuController>();
        if (ctrl == null)
        {
            ctrl = c.controlledObject.AddComponent<BloodMoonTerasuController>();
        }
        ctrl.Bind(c);
        return ctrl;
    }

    public static bool IsAscending(Character c)
    {
        if (c == null || c.controlledObject == null)
        {
            return false;
        }
        BloodMoonTerasuController ctrl = c.controlledObject.GetComponent<BloodMoonTerasuController>();
        return ctrl != null && ctrl.isAscending;
    }

    public static bool IsBusyWithSpecialAction(Character c)
    {
        if (c == null || c.controlledObject == null)
        {
            return false;
        }
        BloodMoonTerasuController ctrl = c.controlledObject.GetComponent<BloodMoonTerasuController>();
        return ctrl != null && ctrl.isBusyWithSpecialAction;
    }

    public static bool TryInterceptFatalBlow(Character c)
    {
        if (!IsUndeadShogun(c))
        {
            return false;
        }
        BloodMoonTerasuController ctrl = GetOrAttach(c);
        if (ctrl == null)
        {
            return false;
        }
        if (ctrl.mHasAscended)
        {
            return false;
        }
        if (!ctrl.mIsAscending)
        {
            ctrl.BeginAscension();
        }
        return true;
    }

    public void Bind(Character owner)
    {
        if (mOwner == owner && mVisualRoot != null)
        {
            return;
        }
        mOwner = owner;
        if (mOwner != null && !mHasAscended && !mIsAscending)
        {
            if (mPhase2BaseMaxHealth <= 0f)
            {
                mPhase2BaseMaxHealth = mOwner.maxHealth;
            }
            mOwner.maxHealth = kPhase1TestHealth;
            mOwner.health = kPhase1TestHealth;
        }
        CacheHierarchy();
        LoadAudioClips();
    }

    public void TriggerSpearUltimateNow()
    {
        if (!mHasAscended || mOwner == null || mOwner.health <= 0f)
        {
            return;
        }
        if (mUltState == SpearUltState.Ready)
        {
            mMahoragaStacks = 6;
            mWheelAngleTarget += 60f;
            mWheelAngleVel += 220f;
            mWheelFlashTimer = 0.50f;
            BeginSpearThrowUltimate();
        }
    }

    private void Awake()
    {
        mAudioSource = gameObject.GetComponent<AudioSource>();
        if (mAudioSource == null)
        {
            mAudioSource = gameObject.AddComponent<AudioSource>();
        }
        mAudioSource.playOnAwake = false;
        mAudioSource.spatialBlend = 0f;
        mAudioSource.volume = 0.9f;
        CacheHierarchy();
        LoadAudioClips();
    }

    private void LoadAudioClips()
    {
        if (mClipLightning == null)
        {
            mClipLightning = Resources.Load("Audio/terasu_eclipse_lightning") as AudioClip;
        }
        if (mClipGong == null)
        {
            mClipGong = Resources.Load("Audio/terasu_ascend_gong") as AudioClip;
        }
        if (mClipApocalypseRoar == null)
        {
            mClipApocalypseRoar = Resources.Load("Audio/terasu_apocalypse_roar") as AudioClip;
        }
        if (mClipRebirth == null)
        {
            mClipRebirth = Resources.Load("Audio/terasu_rebirth_burst") as AudioClip;
        }
        if (mClipSpearWhoosh == null)
        {
            mClipSpearWhoosh = Resources.Load("Audio/terasu_spear_whoosh") as AudioClip;
        }
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip != null && mAudioSource != null)
        {
            mAudioSource.PlayOneShot(clip, volume);
        }
    }

    private void CacheHierarchy()
    {
        if (mVisualRoot == null)
        {
            Transform child = transform.Find("undeadshogun");
            if (child != null)
            {
                mVisualRoot = child;
            }
            else if (transform.childCount > 0)
            {
                mVisualRoot = transform.GetChild(0);
            }
            else
            {
                mVisualRoot = transform;
            }
        }

        mShrinkSpines.Clear();
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            string n = t.name;
            if (n.EndsWith("Shogun_head_a_jnt", StringComparison.OrdinalIgnoreCase)) mHeadBone = t;
            else if (n.EndsWith("Shogun_spine_hi_jnt", StringComparison.OrdinalIgnoreCase)) mChestBone = t;
            else if (n.EndsWith("Shogun_lf_clavicle_jnt", StringComparison.OrdinalIgnoreCase)) mLeftClavicleBone = t;
            else if (n.EndsWith("Shogun_rt_clavicle_jnt", StringComparison.OrdinalIgnoreCase)) mRightClavicleBone = t;
            else if (n.EndsWith("Shogun_rt_hand_jnt", StringComparison.OrdinalIgnoreCase) || n.EndsWith(":prop", StringComparison.OrdinalIgnoreCase))
            {
                if (mRightHandBone == null || n.EndsWith("Shogun_rt_hand_jnt", StringComparison.OrdinalIgnoreCase))
                {
                    mRightHandBone = t;
                }
            }
            else if (n.EndsWith("Shogun_rt_upArm_jnt", StringComparison.OrdinalIgnoreCase)) mRightUpArmBone = t;
            else if (n.EndsWith("Shogun_rt_elbow_jnt", StringComparison.OrdinalIgnoreCase)) mRightElbowBone = t;
            else if (n.EndsWith("Shogun_lf_upArm_jnt", StringComparison.OrdinalIgnoreCase)) mLeftUpArmBone = t;
            else if (n.EndsWith("Shogun_lf_elbow_jnt", StringComparison.OrdinalIgnoreCase)) mLeftElbowBone = t;
            else if (n.EndsWith("Shogun_lf_hand_jnt", StringComparison.OrdinalIgnoreCase)) mLeftHandBone = t;
            else if (n.EndsWith("Shogun_hip_jnt", StringComparison.OrdinalIgnoreCase)) mPelvisBone = t;
            else if (n.EndsWith("Shogun_lf_upLeg_jnt", StringComparison.OrdinalIgnoreCase)) mLeftThighBone = t;
            else if (n.EndsWith("Shogun_lf_ankle_jnt", StringComparison.OrdinalIgnoreCase)) mLeftAnkleBone = t;
            else if (n.EndsWith("Shogun_rt_upLeg_jnt", StringComparison.OrdinalIgnoreCase)) mRightThighBone = t;
            else if (n.EndsWith("Shogun_rt_ankle_jnt", StringComparison.OrdinalIgnoreCase)) mRightAnkleBone = t;
            else if (n.EndsWith("LeftSkirt", StringComparison.OrdinalIgnoreCase)) mLeftSkirt = t;
            else if (n.EndsWith("RightSkirt", StringComparison.OrdinalIgnoreCase)) mRightSkirt = t;

            for (int k = 0; k < kSpineShrinkJoints.Length; k++)
            {
                if (n.EndsWith(kSpineShrinkJoints[k], StringComparison.OrdinalIgnoreCase))
                {
                    mShrinkSpines.Add(t);
                }
            }
        }

        if (!mCachedRenderers)
        {
            mBodyRenderers = GetComponentsInChildren<Renderer>(true);
            mOriginalMainColors = new Color[mBodyRenderers.Length];
            for (int i = 0; i < mBodyRenderers.Length; i++)
            {
                Material mat = mBodyRenderers[i].material;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    mOriginalMainColors[i] = mat.color;
                }
                else
                {
                    mOriginalMainColors[i] = Color.white;
                }
            }
            mCachedRenderers = true;
        }
    }

    private void BeginAscension()
    {
        CacheHierarchy();
        mIsAscending = true;
        mHasAscended = false;
        mStage = AscensionStage.Collapsing;
        mStageTimer = 0f;
        mLightningAudioPlayed = false;
        mGongAudioPlayed = false;
        mRoarAudioPlayed = false;
        mDarknessShattered = false;
        mSpherePoseResetDone = false;
        mRebirthApplied = false;
        mEarthquakePulseTimer = 0f;
        mSphereAoeTickTimer = 0f;
        mCameraShakeIntensity = 0f;
        mCameraShakeOffset = Vector3.zero;

        if (!mSavedEnvColors)
        {
            mOrigAmbientLight = RenderSettings.ambientLight;
            if (Camera.main != null)
            {
                mOrigCamBgColor = Camera.main.backgroundColor;
            }
            mSavedEnvColors = true;
        }

        if (mOwner != null)
        {
            mOwner.DestroyMeleeWeaponPrefabs();
            if (mOwner.controller != null)
            {
                mOwner.controller.ForceCollapseForAscension();
            }
        }

        CreateAscensionVfx();
    }

    private void Update()
    {
        if (mBannerTimer > 0f)
        {
            mBannerTimer = Mathf.Max(0f, mBannerTimer - Time.deltaTime);
        }
        if (mWhiteFlashAlpha > 0f)
        {
            mWhiteFlashAlpha = Mathf.Max(0f, mWhiteFlashAlpha - Time.deltaTime * 2.2f);
        }
        if (mLightningFlashAlpha > 0f)
        {
            mLightningFlashAlpha = Mathf.Max(0f, mLightningFlashAlpha - Time.deltaTime * 5.2f);
        }
        if (mShockwaveRing != null)
        {
            UpdateShockwaveRing();
        }

        if (mIsAscending)
        {
            UpdateAscension();
            mCameraShakeOffset = (mCameraShakeIntensity > 0.05f) ? (UnityEngine.Random.insideUnitSphere * mCameraShakeIntensity) : Vector3.zero;
            mCameraShakeIntensity = Mathf.Max(0f, mCameraShakeIntensity - Time.deltaTime * 16f);
            return;
        }

        mCameraShakeOffset = (mCameraShakeIntensity > 0.05f) ? (UnityEngine.Random.insideUnitSphere * mCameraShakeIntensity) : Vector3.zero;
        mCameraShakeIntensity = Mathf.Max(0f, mCameraShakeIntensity - Time.deltaTime * 24f);

        if (mHasAscended)
        {
            // Darkness stays completely shattered in Phase 2 so Terasu and combat read crisply
            mEclipseScreenAlpha = 0f;
            HideOriginalChibiWeapons();
            UpdatePhase2AnimationClip();
            UpdatePhase2MeleeAnimState();
            UpdateMeleeSlashVfx();
            UpdateThanosWaves();
            UpdateThanosBlastSphere();
            UpdateShatteredHuskLifecycle();
            UpdateGroundHaloRing();
            UpdateSpearUltimate();
            UpdateSixRingDeityHalo();
            UpdateLeftHandVortexVacuum();
        }
    }

    private void LateUpdate()
    {
        if (mVisualRoot == null)
        {
            return;
        }
        if (!mDarknessShattered)
        {
            UpdateBloodMoonPosition();
        }

        if (mIsAscending)
        {
            ApplyAscensionLatePose();
            if (mRebirthApplied)
            {
                ApplyPhase2PhysiqueAndHover();
                UpdateRiggedMeshyPhase2LatePose();
            }
            return;
        }

        if (mHasAscended && mOwner != null && mOwner.health > 0f)
        {
            ApplyPhase2PhysiqueAndHover();
            ApplyRecallArmPoseIfNeeded();
            UpdateRiggedMeshyPhase2LatePose();
        }
    }

    private void PlayTerasuClip(string clipName, float fadeLength, bool forceRestart, float speed)
    {
        if (mTerasuAnimator == null || !mTerasuAnimator.isInitialized || string.IsNullOrEmpty(clipName))
        {
            return;
        }
        mTerasuAnimator.speed = labFreezeClip ? 0f : 1f;
        mTerasuAnimator.SetFloat("AnimSpeed", speed * Mathf.Clamp(labAnimSpeedMultiplier, 0.1f, 3.0f));
        if (forceRestart)
        {
            mTerasuAnimator.Play(clipName, 0, 0f);
            mLastPlayedClip = clipName;
        }
        else if (!string.Equals(mLastPlayedClip, clipName, StringComparison.Ordinal))
        {
            mTerasuAnimator.CrossFadeInFixedTime(clipName, fadeLength, 0, 0f);
            mLastPlayedClip = clipName;
        }
    }

    private void UpdatePhase2AnimationClip()
    {
        if (mTerasuAnimator == null)
        {
            return;
        }

        if (labFreezeClip)
        {
            string targetFreezeClip = !string.IsNullOrEmpty(labForcedClip)
                ? labForcedClip
                : (!string.IsNullOrEmpty(mLastPlayedClip) ? mLastPlayedClip : "Combat_Stance");
            mTerasuAnimator.speed = 0f;
            mTerasuAnimator.Play(targetFreezeClip, 0, Mathf.Clamp01(labForcedNormalizedTime));
            mLastPlayedClip = targetFreezeClip;
            return;
        }

        mTerasuAnimator.speed = 1f;
        if (!string.IsNullOrEmpty(labForcedClip))
        {
            PlayTerasuClip(labForcedClip, 0.08f, false, 1.0f);
            return;
        }

        if (mOwner == null || mOwner.health <= 0f)
        {
            PlayTerasuClip("Dead", 0.12f, false, 1.0f);
            return;
        }

        if (mUltState == SpearUltState.Throwing)
        {
            PlayTerasuClip("Over_Shoulder_Throw", 0.08f, false, 1.25f);
            return;
        }
        if (mUltState == SpearUltState.Impaled)
        {
            if (mUltStateTimer >= kSpearImpaleHoldTime * 0.20f)
            {
                PlayTerasuClip("Charged_Spell_Cast", 0.14f, false, 1.05f);
            }
            else
            {
                PlayTerasuClip("Over_Shoulder_Throw", 0.06f, false, 1.0f);
            }
            return;
        }
        if (mUltState == SpearUltState.Recalling)
        {
            PlayTerasuClip("Charged_Spell_Cast", 0.08f, false, 1.35f);
            return;
        }

        if (mMeleeSwingTimer > 0f)
        {
            if (mActiveAttackStyle == Phase2AttackStyle.ThanosFistWave)
            {
                PlayTerasuClip("ThanosShockwave", 0.08f, false, 1.22f);
            }
            else if (mActiveAttackStyle == Phase2AttackStyle.SpearSweep)
            {
                PlayTerasuClip("Spear_Sky_Slam", 0.08f, false, 1.35f);
            }
            else
            {
                PlayTerasuClip("Double_Combo_Attack", 0.08f, false, 1.45f);
            }
            return;
        }

        bool isMoving = (mOwner.controller != null && mOwner.controller.isMoving);
        if (isMoving)
        {
            PlayTerasuClip("Hover_Fly", 0.16f, false, 1.0f);
        }
        else
        {
            PlayTerasuClip("Combat_Stance", 0.18f, false, 1.0f);
        }
    }

    private void UpdateRiggedMeshyPhase2LatePose()
    {
        if (mTerasuStandaloneInstance == null || mVisualRoot == null)
        {
            return;
        }

        float t = Time.time - mKitAttachedTime;
        bool rawMoving = (mOwner != null && mOwner.controller != null && mOwner.controller.isMoving && mMeleeSwingTimer <= 0f && mUltState == SpearUltState.Ready);
        mSmoothMoveFactor = Mathf.MoveTowards(mSmoothMoveFactor, rawMoving ? 1f : 0f, Time.deltaTime * 5.2f);

        // Guard against rogue FBX root/Armature/Hips XZ translation (e.g., ThanosShockwave baked root offset)
        for (int ci = 0; ci < mTerasuStandaloneInstance.transform.childCount; ci++)
        {
            Transform ch = mTerasuStandaloneInstance.transform.GetChild(ci);
            if (ch == mTHaloWheelPivot) continue;
            Vector3 lp = ch.localPosition;
            if (Mathf.Abs(lp.x) > 0.25f || Mathf.Abs(lp.z) > 0.25f || Mathf.Abs(lp.y) > 0.40f)
            {
                ch.localPosition = new Vector3(0f, Mathf.Clamp(lp.y, -0.20f, 0.35f), 0f);
            }
        }
        if (mBoneHips != null)
        {
            Vector3 hlp = mBoneHips.localPosition;
            bool isDead = (mOwner == null || mOwner.health <= 0f);
            float minHipY = isDead ? -0.90f : -0.20f;
            mBoneHips.localPosition = new Vector3(
                mRestHipsLocalPos.x,
                Mathf.Clamp(hlp.y, mRestHipsLocalPos.y + minHipY, mRestHipsLocalPos.y + 0.45f),
                mRestHipsLocalPos.z
            );
        }
        if (mBoneSpine != null && mOwner != null && mOwner.health > 0f)
        {
            float breathPitch = Mathf.Sin(t * 2.1f + 0.6f) * 1.1f;
            mBoneSpine.localRotation = mBoneSpine.localRotation * Quaternion.Euler(breathPitch, 0f, 0f);
        }

        // Thanos Left-Hand Vortex Particle flare (zero solid flat balloon over wrist!)
        bool isChargingThanos = (mMeleeSwingTimer > 0f && mActiveAttackStyle == Phase2AttackStyle.ThanosFistWave);
        float thanosOrbScale = 0.06f + Mathf.Sin(t * 6.5f) * 0.01f;
        if (isChargingThanos)
        {
            float dur = Mathf.Max(0.2f, mMeleeSwingDuration);
            float p = 1.0f - Mathf.Clamp01(mMeleeSwingTimer / dur);
            if (p < 0.45f)
            {
                float raiseP = Mathf.SmoothStep(0f, 1f, p / 0.45f);
                thanosOrbScale = Mathf.Lerp(0.06f, 0.14f, raiseP);
            }
            else if (p < 0.62f)
            {
                thanosOrbScale = Mathf.Lerp(0.14f, 0.04f, (p - 0.45f) / 0.17f);
            }
        }

        // Live character scale tuning for Test Lab
        mTerasuStandaloneInstance.transform.localScale = Vector3.one * (134f * Mathf.Clamp(labScaleMultiplier, 0.4f, 2.5f));

        // Smooth spring-damped 6-drum Enel + Mahoraga Halo Wheel positioned cleanly BEHIND back/cape (NEVER inside torso!)
        float dt = Time.deltaTime;
        mWheelAngleCurrent = Mathf.SmoothDamp(mWheelAngleCurrent, mWheelAngleTarget, ref mWheelAngleVel, 0.20f, 640f, dt);
        mWheelFlashTimer = Mathf.Max(0f, mWheelFlashTimer - dt);
        mWheelOverdriveTimer = Mathf.Max(0f, mWheelOverdriveTimer - dt);
        if (mTHaloWheelPivot != null)
        {
            if (mTHaloWheelPivot.parent != mTerasuStandaloneInstance.transform)
            {
                mTHaloWheelPivot.SetParent(mTerasuStandaloneInstance.transform, false);
            }
            Vector3 chestLocal = (mBoneSpine != null)
                ? mTerasuStandaloneInstance.transform.InverseTransformPoint(mBoneSpine.position)
                : new Vector3(0f, 1.32f, 0f);
            // Backmost cape/hair in rest pose reaches z = -0.272m; placing pivot at <= -0.46m leaves crisp open air behind his cape!
            float haloZ = Mathf.Min(-0.46f, chestLocal.z - 0.36f);
            float haloY = Mathf.Max(1.48f, chestLocal.y + 0.16f);
            mTHaloWheelPivot.localPosition = new Vector3(chestLocal.x, haloY, haloZ) + labHaloPosOffset;
            mTHaloWheelPivot.localRotation = Quaternion.Euler(0f, 0f, -mWheelAngleCurrent);
            float wheelPulse = (mWheelOverdriveTimer > 0f) ? (1.0f + 0.10f * Mathf.Sin(t * 28f)) : 1.0f;
            mTHaloWheelPivot.localScale = Vector3.one * (wheelPulse * Mathf.Clamp(labHaloScaleMultiplier, 0.3f, 2.5f));
        }

        if (mTInHandSpear != null)
        {
            mTInHandSpear.transform.localPosition = mBaseSpearLocalPos + labSpearPosOffset;
            mTInHandSpear.transform.localRotation = Quaternion.Euler(mBaseSpearLocalEuler + labSpearEulerOffset);
            mTInHandSpear.transform.localScale = mBaseSpearLocalScale * Mathf.Clamp(labSpearScaleMultiplier, 0.3f, 2.5f);
        }

        // Sync true rigged hand world anchors & offset LeftVortexOrb distally into palm (not over wrist joint!)
        if (mLeftHandMount != null && mBoneLeftHand != null && mBoneLeftForeArm != null)
        {
            Vector3 palmDirWorld = (mBoneLeftHand.position - mBoneLeftForeArm.position).normalized;
            if (palmDirWorld.sqrMagnitude > 0.001f)
            {
                mLeftHandMount.position = mBoneLeftHand.position + palmDirWorld * 14f;
            }
        }
        if (mRightHandMount != null)
        {
            mVirtualRightHandPos = mRightHandMount.position;
        }
        else if (mBoneRightHand != null)
        {
            mVirtualRightHandPos = mBoneRightHand.position;
        }

        if (mLeftHandMount != null)
        {
            mVirtualLeftHandPos = mLeftHandMount.position;
        }
        else if (mBoneLeftHand != null)
        {
            mVirtualLeftHandPos = mBoneLeftHand.position;
        }

        if (mTLeftVortexOrb != null)
        {
            mTLeftVortexOrb.transform.localScale = new Vector3(thanosOrbScale, thanosOrbScale, thanosOrbScale);
            mTLeftVortexOrb.transform.Rotate(dt * 140f, dt * 210f, dt * 95f, Space.Self);
        }
        if (mLeftHandVortexPs != null)
        {
            ParticleSystem.EmissionModule em = mLeftHandVortexPs.emission;
            em.rateOverTime = isChargingThanos ? 55f : 16f;
        }
    }

    private void OnDestroy()
    {
        RestoreAtmosphereIfNeeded();
        if (mBloodMoonBackdrop != null)
        {
            Destroy(mBloodMoonBackdrop);
        }
        if (mFlyingSpear != null)
        {
            Destroy(mFlyingSpear);
        }
        if (mRecallTetherLine != null)
        {
            Destroy(mRecallTetherLine.gameObject);
        }
        if (mImpalePillarLine != null)
        {
            Destroy(mImpalePillarLine.gameObject);
        }
        if (mRecallHandOrb != null)
        {
            Destroy(mRecallHandOrb);
        }
        if (mShockwaveRing != null)
        {
            Destroy(mShockwaveRing);
        }
        if (mShatteredHusk != null)
        {
            Destroy(mShatteredHusk);
        }
        if (mHaloArcLine != null)
        {
            Destroy(mHaloArcLine.gameObject);
        }
        if (mVortexVacuumLine != null)
        {
            Destroy(mVortexVacuumLine.gameObject);
        }
        if (mMeleeSlashVfxRoot != null)
        {
            Destroy(mMeleeSlashVfxRoot);
        }
        if (mThanosBlastSphere != null)
        {
            Destroy(mThanosBlastSphere);
        }
        if (mThanosBlastOuterRing != null)
        {
            Destroy(mThanosBlastOuterRing);
        }
        for (int i = 0; i < mThanosWaves.Count; i++)
        {
            if (mThanosWaves[i] != null && mThanosWaves[i].root != null)
            {
                Destroy(mThanosWaves[i].root);
            }
        }
        mThanosWaves.Clear();
    }

    private void RestoreAtmosphereIfNeeded()
    {
        if (mSavedEnvColors)
        {
            RenderSettings.ambientLight = mOrigAmbientLight;
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = mOrigCamBgColor;
            }
            mSavedEnvColors = false;
        }
    }

    private void UpdateAscension()
    {
        mStageTimer += Time.deltaTime;
        UpdateBloodMoonPosition();
        UpdateGroundHaloRing();

        if (mStageTimer < kCollapseDuration)
        {
            mStage = AscensionStage.Collapsing;
            float p = Mathf.Clamp01(mStageTimer / kCollapseDuration);
            mEclipseScreenAlpha = Mathf.Lerp(0f, 0.56f, p);
            ApplyAtmosphereDarkening(p * 0.92f);
            SetAscensionSphereActive(false, 0f);
            SetLightningVisible(false);
        }
        else if (mStageTimer < kLightningDuration)
        {
            mStage = AscensionStage.EclipseLightning;
            if (!mLightningAudioPlayed)
            {
                mLightningAudioPlayed = true;
                PlaySfx(mClipLightning, 1.0f);
                mLightningFlashAlpha = 0.78f;
                CameraShaker.RequestShake(transform.position, 4.5f);
            }
            mEclipseScreenAlpha = 0.52f + Mathf.Sin(Time.time * 32f) * 0.08f;
            ApplyAtmosphereDarkening(0.95f);

            // Heavy earthquake tremor on ground & screen during lightning storm
            mCameraShakeIntensity = Mathf.Max(mCameraShakeIntensity, 12.5f);
            mEarthquakePulseTimer -= Time.deltaTime;
            if (mEarthquakePulseTimer <= 0f)
            {
                mEarthquakePulseTimer = 0.36f;
                mLightningFlashAlpha = Mathf.Max(mLightningFlashAlpha, 0.72f);
                SpawnShockwaveRing(transform.position + Vector3.up * 2f, new Color(1.0f, 0.25f, 0.10f, 0.95f), 175f);
                CameraShaker.RequestShake(transform.position, 4.0f);
            }

            // Convulse fallen body violently on the shaking bridge
            float jx = Mathf.Sin(Time.time * 78f) * 11.5f;
            float jz = Mathf.Cos(Time.time * 67f) * 11.5f;
            mVisualRoot.localPosition = new Vector3(jx, 2.5f + Mathf.Abs(Mathf.Sin(Time.time * 54f)) * 6.5f, jz);

            float flash = (Mathf.Sin(Time.time * 42f) > 0f) ? 1f : 0f;
            TintBodyRenderers(Color.Lerp(new Color(0.28f, 0.06f, 0.10f), new Color(1.0f, 0.34f, 0.26f), flash));

            UpdateLightningStrikes();
            SetAscensionSphereActive(false, 0f);
        }
        else if (mStageTimer < kSphereDuration)
        {
            mStage = AscensionStage.SphereLevitation;
            if (!mGongAudioPlayed)
            {
                mGongAudioPlayed = true;
                PlaySfx(mClipGong, 1.0f);
            }
            if (!mRoarAudioPlayed)
            {
                mRoarAudioPlayed = true;
                PlaySfx(mClipApocalypseRoar, 1.0f);
                mLightningFlashAlpha = 0.85f;
                CameraShaker.RequestShake(transform.position, 6.0f);
            }
            if (!mSpherePoseResetDone)
            {
                mSpherePoseResetDone = true;
                if (mOwner != null && mOwner.controller != null)
                {
                    mOwner.controller.ResetAfterAscension();
                }
            }

            float p = Mathf.Clamp01((mStageTimer - kLightningDuration) / (kSphereDuration - kLightningDuration));
            float smooth = p * p * (3f - 2f * p);
            float liftY = Mathf.Lerp(4f, 82f, smooth);
            mVisualRoot.localPosition = new Vector3(Mathf.Sin(Time.time * 24f) * 2.2f, liftY, 0f);

            mEclipseScreenAlpha = Mathf.Lerp(0.50f, 0.58f, smooth);
            ApplyAtmosphereDarkening(0.95f);

            // Sustained apocalyptic earthquake tremor peaking right at climax cocoon
            mCameraShakeIntensity = Mathf.Max(mCameraShakeIntensity, Mathf.Lerp(8.5f, 16.0f, p));

            // Continuous AoE lightning & shockwave damage every 0.50s around the Ascension sphere!
            mSphereAoeTickTimer -= Time.deltaTime;
            if (mSphereAoeTickTimer <= 0f)
            {
                mSphereAoeTickTimer = 0.50f;
                mLightningFlashAlpha = Mathf.Max(mLightningFlashAlpha, 0.45f + p * 0.35f);
                SpawnShockwaveRing(transform.position + Vector3.up * 4f, new Color(1.0f, 0.16f, 0.10f, 0.92f), 220f + p * 60f);
                DamageAndKnockbackArea(transform.position, 210f, GetBaseDamage() * 0.45f, 45, false);
                CameraShaker.RequestShake(transform.position, 4.5f);
            }

            // Climax energy veil ("Max Steel / Azir" transformation curtain at apex p >= 0.68)
            if (p >= 0.72f)
            {
                float veil = Mathf.Clamp01((p - 0.72f) / 0.28f);
                mWhiteFlashAlpha = Mathf.Max(mWhiteFlashAlpha, veil * 0.65f);
                TintBodyRenderers(Color.Lerp(new Color(1.0f, 0.32f, 0.18f), new Color(1.0f, 0.92f, 0.75f), veil));
            }
            else
            {
                TintBodyRenderers(Color.Lerp(new Color(0.45f, 0.14f, 0.16f), new Color(1.0f, 0.34f, 0.28f), 0.35f + 0.35f * Mathf.Sin(Time.time * 14f)));
            }

            UpdateLightningStrikes();
            SetAscensionSphereActive(true, smooth);
        }
        else if (mStageTimer < kRebirthDuration)
        {
            mStage = AscensionStage.RebirthBurst;
            if (!mRebirthApplied)
            {
                mRebirthApplied = true;
                PerformRebirthTransform();
            }
            mEclipseScreenAlpha = 0f;
            SetAscensionSphereActive(false, 0f);
            SetLightningVisible(false);
        }
        else
        {
            FinishAscension();
        }
    }

    private void PerformRebirthTransform()
    {
        PlaySfx(mClipRebirth, 1.0f);
        mWhiteFlashAlpha = 0.95f;
        mLightningFlashAlpha = 0.90f;
        mCameraShakeIntensity = 18.0f;
        CameraShaker.RequestShake(transform.position, 7.5f);
        SpawnShockwaveRing(transform.position + Vector3.up * 8f, new Color(1f, 0.14f, 0.10f, 0.95f), 340f);
        DamageAndKnockbackArea(transform.position, 240f, GetBaseDamage() * 1.5f, 90, true);

        // Shatter all Ascension darkness immediately so Phase 2 Terasu shines cleanly!
        mDarknessShattered = true;
        mEclipseScreenAlpha = 0f;
        RestoreAtmosphereIfNeeded();
        if (mBloodMoonBackdrop != null)
        {
            mBloodMoonBackdrop.SetActive(false);
        }

        // Completely disable original Zombie Shogun meshes and spawn standalone 3D Terasu prefab + shattered husk
        DisableOriginalShogunRenderers();
        AttachStandaloneTerasuPrefab();
        SpawnShatteredShogunHusk();

        if (mOwner != null)
        {
            float phase2Base = (mPhase2BaseMaxHealth > 0f) ? mPhase2BaseMaxHealth : mOwner.maxHealth;
            mOwner.maxHealth = Mathf.Round(Mathf.Max(phase2Base, 1000f) * 1.35f);
            mOwner.health = mOwner.maxHealth;
            mOwner.meleeDamage = Mathf.Max(mOwner.meleeDamage, 200f) * 1.45f;
            mOwner.meleeAttackRange = Mathf.Max(mOwner.meleeAttackRange, 300f);
            mOwner.meleeAttackFrequency = Mathf.Max(1.4f, mOwner.meleeAttackFrequency * 0.72f);
            // Unstoppable deity poise: enemy hits cannot knock Terasu backward/forward
            mOwner.knockbackResistance = 1000;

            if (mOwner.controller != null)
            {
                mOwner.DestroyMeleeWeaponPrefabs();
                mOwner.controller.ResetAfterAscension();
                if (mOwner.controller.HasAnim("victory"))
                {
                    mOwner.controller.PlayVictoryAnim();
                }
            }
        }

        mBannerTimer = 2.25f;
    }

    private void FinishAscension()
    {
        mIsAscending = false;
        mHasAscended = true;
        mStage = AscensionStage.AscendedPhase2;
        mUltCooldownTimer = 2.0f;

        if (mAscensionSphere != null)
        {
            mAscensionSphere.SetActive(false);
        }
        SetLightningVisible(false);

        if (mOwner != null && mOwner.controller != null)
        {
            mOwner.controller.ResetAfterAscension();
        }
    }

    private void ApplyAscensionLatePose()
    {
        if (mStage == AscensionStage.SphereLevitation || mStage == AscensionStage.RebirthBurst)
        {
            // Azir-inspired spread-arms & tilted-back floating posture
            if (mLeftUpArmBone != null)
            {
                mLeftUpArmBone.Rotate(0f, 0f, -55f, Space.Self);
            }
            if (mRightUpArmBone != null)
            {
                mRightUpArmBone.Rotate(0f, 0f, 55f, Space.Self);
            }
            if (mHeadBone != null)
            {
                mHeadBone.Rotate(-22f, 0f, 0f, Space.Self);
            }
        }

        if (mHasAscended)
        {
            ApplySlimVTaperBones();
        }
    }

    private void ApplyPhase2PhysiqueAndHover()
    {
        ApplySlimVTaperBones();

        // Serene vertical deity levitation breath (ZERO forward-back rocking!)
        float t = Time.time;
        float hoverY = 7f + labHoverOffsetY + Mathf.Sin(t * 1.45f) * 3.8f + Mathf.Sin(t * 2.9f + 0.8f) * 0.8f;
        float swayX = Mathf.Cos(t * 1.10f) * 1.1f;
        mVisualRoot.localPosition = new Vector3(swayX, hoverY, 0f);
    }

    public void ForceInstantPhase2()
    {
        if (mHasAscended && mTerasuStandaloneInstance != null)
        {
            return;
        }
        CacheHierarchy();
        if (mOwner != null)
        {
            mPhase2BaseMaxHealth = Mathf.Max(mOwner.maxHealth, 1000f);
        }
        mDarknessShattered = true;
        mEclipseScreenAlpha = 0f;
        mRebirthApplied = true;
        PerformRebirthTransform();
        mBannerTimer = 0f;
        mWhiteFlashAlpha = 0.25f;
        FinishAscension();
    }

    public void TriggerManualMeleeAttack(bool isSweep)
    {
        if (!mHasAscended || mOwner == null || mOwner.health <= 0f)
        {
            return;
        }
        labForcedClip = string.Empty;
        labFreezeClip = false;
        if (mOwner.controller != null)
        {
            mOwner.controller.StopWalking();
        }
        mActiveAttackStyle = isSweep ? Phase2AttackStyle.SpearSweep : Phase2AttackStyle.SpearThrust;
        mMeleeSwingDuration = 0.78f;
        mMeleeSwingTimer = mMeleeSwingDuration;
        mMeleeSlashFxFired = false;
        mManualAttackPendingDamage = true;
        AdvanceMahoragaWheelStack();
        if (mActiveAttackStyle == Phase2AttackStyle.ThanosFistWave)
        {
            mManualAttackPendingDamage = false;
            return;
        }
        PlayTerasuClip(isSweep ? "Spear_Sky_Slam" : "Double_Combo_Attack", 0.06f, true, isSweep ? 1.35f : 1.45f);
    }

    public void StepHaloWheelOnce()
    {
        AdvanceMahoragaWheelStack();
    }

    public void ResetLabOffsets()
    {
        labScaleMultiplier = 1.0f;
        labHoverOffsetY = 0f;
        labAnimSpeedMultiplier = 1.0f;
        labHaloPosOffset = Vector3.zero;
        labHaloScaleMultiplier = 1.0f;
        labSpearPosOffset = Vector3.zero;
        labSpearEulerOffset = Vector3.zero;
        labSpearScaleMultiplier = 1.0f;
        labFreezeClip = false;
        labForcedClip = string.Empty;
    }

    private void HideOriginalChibiWeapons()
    {
        if (mOwner == null || mOwner.controller == null)
        {
            return;
        }
        List<GameObject> instances = mOwner.controller.meleeWeaponInstances;
        if (instances == null)
        {
            return;
        }
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null && instances[i].activeSelf)
            {
                instances[i].SetActive(false);
            }
        }
    }

    public void TriggerThanosShockwaveNow()
    {
        if (!mHasAscended || mOwner == null || mOwner.health <= 0f)
        {
            return;
        }
        Character target = FindFarthestBacklineOpponent();
        if (target != null && mOwner.controller != null)
        {
            mOwner.controller.facing = (target.position.z >= transform.position.z) ? FacingType.Right : FacingType.Left;
            mOwner.controller.StopWalking();
        }
        mActiveAttackStyle = Phase2AttackStyle.ThanosFistWave;
        mMeleeSwingDuration = 1.25f;
        mMeleeSwingTimer = 1.25f;
        mMeleeSlashFxFired = false;
        PlayTerasuClip("ThanosShockwave", 0.08f, true, 1.22f);
        Debug.Log("[BloodMoonTerasu] TriggerThanosShockwaveNow started (hp=" + mOwner.health + "/" + mOwner.maxHealth + ")");
    }

    public void TriggerSixthWheelUltNow()
    {
        if (!mHasAscended || mOwner == null || mOwner.health <= 0f)
        {
            return;
        }
        mMahoragaStacks = 5;
        AdvanceMahoragaWheelStack();
    }

    private void UpdatePhase2MeleeAnimState()
    {
        if (mOwner == null || mOwner.controller == null)
        {
            return;
        }
        bool isAttackAnim = string.Equals(mOwner.controller.currentAnimation, "attack", StringComparison.OrdinalIgnoreCase);
        bool isAttackActive = mOwner.controller.attackActive;

        bool startedNewSwing = (mUltState == SpearUltState.Ready) &&
            ((isAttackAnim && !mWasAttackAnim) || (isAttackActive && !mWasAttackActive && mMeleeSwingTimer <= 0.14f));
        if (startedNewSwing)
        {
            mActiveAttackStyle = ((mAttackComboIndex % 2) == 0) ? Phase2AttackStyle.SpearThrust : Phase2AttackStyle.SpearSweep;
            mAttackComboIndex = (mAttackComboIndex + 1) % 2;
            mMeleeSwingDuration = 0.78f;
            mMeleeSwingTimer = mMeleeSwingDuration;
            mMeleeSlashFxFired = false;
            AdvanceMahoragaWheelStack();
            if (mActiveAttackStyle == Phase2AttackStyle.ThanosFistWave)
            {
                mMeleeSwingDuration = 1.25f;
                mMeleeSwingTimer = 1.25f;
                PlayTerasuClip("ThanosShockwave", 0.08f, true, 1.22f);
            }
            else if (mActiveAttackStyle == Phase2AttackStyle.SpearSweep)
            {
                PlayTerasuClip("Spear_Sky_Slam", 0.08f, true, 1.35f);
            }
            else
            {
                PlayTerasuClip("Double_Combo_Attack", 0.08f, true, 1.45f);
            }
        }

        mWasAttackAnim = isAttackAnim;
        mWasAttackActive = isAttackActive;

        if (mMeleeSwingTimer > 0f)
        {
            mMeleeSwingTimer = Mathf.Max(0f, mMeleeSwingTimer - Time.deltaTime);
            float p = 1.0f - Mathf.Clamp01(mMeleeSwingTimer / Mathf.Max(0.2f, mMeleeSwingDuration));
            float fireThresh = (mActiveAttackStyle == Phase2AttackStyle.ThanosFistWave) ? 0.46f : 0.36f;
            if (p >= fireThresh && !mMeleeSlashFxFired)
            {
                mMeleeSlashFxFired = true;
                if (mActiveAttackStyle == Phase2AttackStyle.ThanosFistWave)
                {
                    SpawnThanosMagicWave();
                }
                else
                {
                    SpawnMeleeSlashArcVfx();
                    if (mManualAttackPendingDamage)
                    {
                        mManualAttackPendingDamage = false;
                        Vector3 fwd = (mVisualRoot != null) ? mVisualRoot.forward : Vector3.forward;
                        DamageAndKnockbackArea(transform.position + fwd * 125f, 155f, GetBaseDamage(), 20, true);
                    }
                }
            }
        }
        else
        {
            mManualAttackPendingDamage = false;
        }
    }

    private void SpawnMeleeSlashArcVfx()
    {
        if (mVisualRoot == null)
        {
            return;
        }
        Vector3 fwd = mVisualRoot.forward;
        mMeleeSlashFacing = (fwd.z >= 0f) ? 1f : -1f;
        mMeleeSlashVfxStyle = mActiveAttackStyle;
        mMeleeSlashOrigin = transform.position + Vector3.up * 58f + fwd * 38f;
        mMeleeSlashVfxTimer = 0.28f;

        if (mMeleeSlashVfxRoot == null)
        {
            mMeleeSlashVfxRoot = new GameObject("TerasuMeleeCrescentSlash");
            GameObject outerGo = new GameObject("SlashArcOuter");
            outerGo.transform.SetParent(mMeleeSlashVfxRoot.transform, false);
            mMeleeSlashArcOuter = outerGo.AddComponent<LineRenderer>();
            mMeleeSlashArcOuter.useWorldSpace = true;
            mMeleeSlashArcOuter.material = CreateGlowMaterial(Color.white, true);
            mMeleeSlashArcOuter.startColor = new Color(1.0f, 0.14f, 0.08f, 0.96f);
            mMeleeSlashArcOuter.endColor = new Color(1.0f, 0.45f, 0.14f, 0.25f);
            mMeleeSlashArcOuter.startWidth = 14f;
            mMeleeSlashArcOuter.endWidth = 3f;

            GameObject innerGo = new GameObject("SlashArcInner");
            innerGo.transform.SetParent(mMeleeSlashVfxRoot.transform, false);
            mMeleeSlashArcInner = innerGo.AddComponent<LineRenderer>();
            mMeleeSlashArcInner.useWorldSpace = true;
            mMeleeSlashArcInner.material = CreateGlowMaterial(Color.white, true);
            mMeleeSlashArcInner.startColor = new Color(1.0f, 0.88f, 0.55f, 0.98f);
            mMeleeSlashArcInner.endColor = new Color(1.0f, 0.25f, 0.12f, 0.30f);
            mMeleeSlashArcInner.startWidth = 6.5f;
            mMeleeSlashArcInner.endWidth = 1.5f;
        }
        mMeleeSlashVfxRoot.SetActive(true);
        Vector3 impactRingPos = (mMeleeSlashVfxStyle == Phase2AttackStyle.SpearThrust)
            ? (transform.position + fwd * 125f + Vector3.up * 52f)
            : (transform.position + fwd * 92f + Vector3.up * 48f);
        SpawnShockwaveRing(impactRingPos, new Color(1.0f, 0.32f, 0.14f, 0.92f), 98f);
        UpdateMeleeSlashVfxGeometry(0f);
    }

    private void UpdateMeleeSlashVfx()
    {
        if (mMeleeSlashVfxTimer <= 0f)
        {
            if (mMeleeSlashVfxRoot != null && mMeleeSlashVfxRoot.activeSelf)
            {
                mMeleeSlashVfxRoot.SetActive(false);
            }
            return;
        }
        mMeleeSlashVfxTimer -= Time.deltaTime;
        if (mMeleeSlashVfxTimer <= 0f)
        {
            if (mMeleeSlashVfxRoot != null)
            {
                mMeleeSlashVfxRoot.SetActive(false);
            }
            return;
        }
        float p = 1.0f - Mathf.Clamp01(mMeleeSlashVfxTimer / 0.28f);
        UpdateMeleeSlashVfxGeometry(p);
    }

    private void UpdateMeleeSlashVfxGeometry(float p)
    {
        if (mMeleeSlashArcOuter == null || mMeleeSlashArcInner == null)
        {
            return;
        }
        const int segs = 16;
        mMeleeSlashArcOuter.positionCount = segs;
        mMeleeSlashArcInner.positionCount = segs;

        float alpha = Mathf.Clamp01(1.0f - p * 0.85f);
        mMeleeSlashArcOuter.startColor = new Color(1.0f, 0.16f, 0.10f, alpha);
        mMeleeSlashArcOuter.endColor = new Color(0.95f, 0.12f, 0.08f, alpha * 0.15f);
        mMeleeSlashArcInner.startColor = new Color(1.0f, 0.92f, 0.58f, alpha);
        mMeleeSlashArcInner.endColor = new Color(1.0f, 0.32f, 0.14f, alpha * 0.20f);

        if (mMeleeSlashVfxStyle == Phase2AttackStyle.SpearThrust)
        {
            // Linear High-Velocity Piston Spear Thrust Lance (straight horizontal pierce!)
            mMeleeSlashArcOuter.startWidth = Mathf.Lerp(16f, 3f, p);
            mMeleeSlashArcOuter.endWidth = 2f;
            mMeleeSlashArcInner.startWidth = Mathf.Lerp(8f, 1.5f, p);
            mMeleeSlashArcInner.endWidth = 1f;

            float reachMax = Mathf.Lerp(55f, 148f, Mathf.Sin(p * Mathf.PI * 0.5f));
            Vector3 handAnchor = (mVirtualRightHandPos.sqrMagnitude > 1f) ? mVirtualRightHandPos : mMeleeSlashOrigin;
            Vector3 fwdVec = new Vector3(0f, 0f, mMeleeSlashFacing);
            for (int i = 0; i < segs; i++)
            {
                float frac = i / (float)(segs - 1);
                float spiral = Mathf.Sin(frac * Mathf.PI) * 5.0f;
                float phase = Time.time * 24f + frac * 10f;
                Vector3 pt = handAnchor + fwdVec * (frac * reachMax);
                pt.x += Mathf.Cos(phase) * spiral * (1f - frac * 0.6f);
                pt.y += Mathf.Sin(phase) * spiral * (1f - frac * 0.6f);
                mMeleeSlashArcOuter.SetPosition(i, pt);
                mMeleeSlashArcInner.SetPosition(i, handAnchor + fwdVec * (frac * reachMax * 0.94f));
            }
        }
        else
        {
            // Wide Horizontal XZ Polearm Sweep (NO top-down katana chop!)
            mMeleeSlashArcOuter.startWidth = Mathf.Lerp(16f, 4f, p);
            mMeleeSlashArcOuter.endWidth = 3f;
            mMeleeSlashArcInner.startWidth = Mathf.Lerp(7.5f, 2f, p);
            mMeleeSlashArcInner.endWidth = 1.5f;

            float radius = Mathf.Lerp(84f, 122f, p);
            float headAngle = Mathf.Lerp(-82f, 82f, p);
            float arcSpan = 104f;
            for (int i = 0; i < segs; i++)
            {
                float frac = i / (float)(segs - 1);
                float angDeg = headAngle - frac * arcSpan;
                float rad = angDeg * Mathf.Deg2Rad;
                float forwardReach = Mathf.Cos(rad) * radius * mMeleeSlashFacing;
                float sideSweep = Mathf.Sin(rad) * radius * 0.92f;
                float slightTiltY = Mathf.Sin(rad) * 14f;

                Vector3 outerPos = mMeleeSlashOrigin + new Vector3(sideSweep, slightTiltY, forwardReach);
                Vector3 innerPos = mMeleeSlashOrigin + new Vector3(sideSweep * 0.84f, slightTiltY * 0.84f, forwardReach * 0.84f);
                mMeleeSlashArcOuter.SetPosition(i, outerPos);
                mMeleeSlashArcInner.SetPosition(i, innerPos);
            }
        }
    }

    private void SpawnThanosMagicWave()
    {
        if (mOwner == null || mVisualRoot == null)
        {
            return;
        }
        Vector3 fwd = mVisualRoot.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f)
        {
            fwd = Vector3.forward;
        }
        fwd.Normalize();

        Vector3 bodyRef = transform.position + Vector3.up * 58f + fwd * 42f;
        Vector3 spawnOrigin = (mVirtualLeftHandPos.sqrMagnitude > 1f && Vector3.Distance(mVirtualLeftHandPos, transform.position) <= 220f)
            ? new Vector3(mVirtualLeftHandPos.x, Mathf.Max(transform.position.y + 42f, mVirtualLeftHandPos.y), mVirtualLeftHandPos.z)
            : bodyRef;

        GameObject rootGo = new GameObject("ThanosGauntletWave");
        ThanosWaveInstance wave = new ThanosWaveInstance();
        wave.root = rootGo;
        wave.origin = spawnOrigin;
        wave.dir = fwd;
        wave.age = 0f;
        wave.lifetime = 0.72f;
        wave.speed = 500f;
        wave.hitList = new List<Character>();

        GameObject oGo = new GameObject("WaveOuter");
        oGo.transform.SetParent(rootGo.transform, false);
        wave.arcOuter = oGo.AddComponent<LineRenderer>();
        wave.arcOuter.useWorldSpace = true;
        wave.arcOuter.material = CreateGlowMaterial(Color.white, true);
        wave.arcOuter.startColor = new Color(0.92f, 0.05f, 0.07f, 0.96f);
        wave.arcOuter.endColor = new Color(0.42f, 0.02f, 0.05f, 0.82f);
        wave.arcOuter.startWidth = 18f;
        wave.arcOuter.endWidth = 14f;

        GameObject iGo = new GameObject("WaveInner");
        iGo.transform.SetParent(rootGo.transform, false);
        wave.arcInner = iGo.AddComponent<LineRenderer>();
        wave.arcInner.useWorldSpace = true;
        wave.arcInner.material = CreateGlowMaterial(Color.white, true);
        wave.arcInner.startColor = new Color(1.0f, 0.72f, 0.32f, 0.98f);
        wave.arcInner.endColor = new Color(0.96f, 0.14f, 0.08f, 0.92f);
        wave.arcInner.startWidth = 8.5f;
        wave.arcInner.endWidth = 6.5f;

        GameObject trailPsGo = new GameObject("WaveParticleTrail");
        trailPsGo.transform.SetParent(rootGo.transform, false);
        trailPsGo.transform.position = spawnOrigin;
        wave.trailPs = CreateShockwaveTrailPs(trailPsGo, fwd);

        mThanosWaves.Add(wave);

        // Ground slam earthquake shake, multi-layer Particle Explosion & gong-blast audio when fist clenches!
        mCameraShakeIntensity = Mathf.Max(mCameraShakeIntensity, 16.5f);
        CameraShaker.RequestShake(spawnOrigin, 7.5f);
        mLightningFlashAlpha = Mathf.Max(mLightningFlashAlpha, 0.35f);
        PlaySfx(mClipRebirth != null ? mClipRebirth : (mClipSpearWhoosh != null ? mClipSpearWhoosh : mClipGong), 0.95f);
        SpawnShockwaveRing(spawnOrigin, new Color(0.95f, 0.16f, 0.08f, 0.96f), 230f);
        SpawnThanosBlastDome(spawnOrigin, fwd);
    }

    private void SpawnThanosBlastDome(Vector3 center, Vector3 fwd)
    {
        if (mThanosBlastRingLine == null)
        {
            GameObject ringGo = new GameObject("ThanosFistShockRingLine");
            mThanosBlastRingLine = CreateCircleLineRenderer(ringGo, 44, 1f, new Color(1.0f, 0.42f, 0.14f, 0.98f), 14f, false);
        }
        mThanosBlastOrigin = center;
        mThanosBlastTimer = 0.78f;
        if (mThanosBlastSphere != null && mThanosBlastSphere.activeSelf) mThanosBlastSphere.SetActive(false);
        if (mThanosBlastOuterRing != null && mThanosBlastOuterRing.activeSelf) mThanosBlastOuterRing.SetActive(false);
        if (mThanosBlastRingLine != null)
        {
            mThanosBlastRingLine.gameObject.SetActive(true);
            mThanosBlastRingLine.transform.position = new Vector3(center.x, Mathf.Max(14f, transform.position.y + 38f), center.z);
            mThanosBlastRingLine.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
            mThanosBlastRingLine.transform.localScale = Vector3.one * 30f;
        }
        SpawnThanosExplosionParticles(center, fwd);
        Debug.Log("[BloodMoonTerasu] SpawnThanosBlastDome (Particle Explosion) at " + center);
    }

    private void UpdateThanosBlastSphere()
    {
        if (mThanosBlastTimer <= 0f)
        {
            if (mThanosBlastSphere != null && mThanosBlastSphere.activeSelf) mThanosBlastSphere.SetActive(false);
            if (mThanosBlastOuterRing != null && mThanosBlastOuterRing.activeSelf) mThanosBlastOuterRing.SetActive(false);
            if (mThanosBlastRingLine != null && mThanosBlastRingLine.gameObject.activeSelf) mThanosBlastRingLine.gameObject.SetActive(false);
            return;
        }
        mThanosBlastTimer -= Time.deltaTime;
        float p = 1.0f - Mathf.Clamp01(mThanosBlastTimer / 0.78f);
        float ease = 1.0f - (1.0f - p) * (1.0f - p);
        if (mThanosBlastRingLine != null)
        {
            float lineRadius = Mathf.Lerp(32f, 290f, ease);
            mThanosBlastRingLine.transform.localScale = new Vector3(lineRadius, lineRadius, lineRadius);
            float alpha = Mathf.Clamp01((1.0f - p) * 1.15f);
            mThanosBlastRingLine.startColor = new Color(1.0f, 0.54f, 0.16f, alpha);
            mThanosBlastRingLine.endColor = new Color(0.85f, 0.08f, 0.06f, alpha);
            mThanosBlastRingLine.startWidth = Mathf.Lerp(16f, 4f, p);
            mThanosBlastRingLine.endWidth = Mathf.Lerp(16f, 4f, p);
        }
    }

    private void UpdateThanosWaves()
    {
        for (int w = mThanosWaves.Count - 1; w >= 0; w--)
        {
            ThanosWaveInstance wave = mThanosWaves[w];
            if (wave == null || wave.root == null)
            {
                mThanosWaves.RemoveAt(w);
                continue;
            }
            wave.age += Time.deltaTime;
            if (wave.age >= wave.lifetime)
            {
                Destroy(wave.root);
                mThanosWaves.RemoveAt(w);
                continue;
            }

            float p = Mathf.Clamp01(wave.age / wave.lifetime);
            Vector3 center = wave.origin + wave.dir * (wave.age * wave.speed);
            float groundY = center.y;
            RailManager rm = WeakGlobalInstance<RailManager>.instance;
            if (rm != null)
            {
                groundY = rm.GetY(center.z) + 38f;
            }
            center.y = groundY;
            if (wave.trailPs != null)
            {
                wave.trailPs.transform.position = center;
            }

            // Update towering crescent wall wave geometry
            const int segs = 14;
            wave.arcOuter.positionCount = segs;
            wave.arcInner.positionCount = segs;
            float spanWidth = Mathf.Lerp(56f, 118f, p);
            float heightSpan = Mathf.Lerp(42f, 72f, p);
            float alpha = Mathf.Clamp01((1.0f - p) * 1.35f);
            wave.arcOuter.startColor = new Color(0.92f, 0.05f, 0.07f, alpha);
            wave.arcOuter.endColor = new Color(0.42f, 0.02f, 0.05f, alpha * 0.78f);
            wave.arcInner.startColor = new Color(1.0f, 0.72f, 0.32f, alpha);
            wave.arcInner.endColor = new Color(0.96f, 0.14f, 0.08f, alpha);

            Vector3 side = new Vector3(1f, 0f, 0f);
            for (int i = 0; i < segs; i++)
            {
                float frac = i / (float)(segs - 1);
                float arch = (frac - 0.5f) * 2.0f; // -1 .. +1
                float archCurve = Mathf.Cos(arch * Mathf.PI * 0.48f);
                Vector3 posOuter = center + side * (arch * spanWidth) + wave.dir * (archCurve * 26f) + Vector3.up * ((1f - Mathf.Abs(arch) * 0.5f) * heightSpan * 0.35f);
                Vector3 posInner = center + side * (arch * spanWidth * 0.88f) + wave.dir * (archCurve * 34f) + Vector3.up * ((1f - Mathf.Abs(arch) * 0.5f) * heightSpan * 0.35f);
                wave.arcOuter.SetPosition(i, posOuter);
                wave.arcInner.SetPosition(i, posInner);
            }

            // AoE Magic Damage + Knockback along wave path
            float dmg = GetBaseDamage() * 0.85f;
            SliceOpponentsNearPoint(center, 92f, dmg, wave.hitList);
            CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
            if (cm != null && mOwner != null)
            {
                bool oppAreEnemies = !mOwner.isEnemy;
                List<Character> near = cm.GetCharactersInRange(center.z - 80f, center.z + 80f, oppAreEnemies);
                for (int i = 0; i < near.Count; i++)
                {
                    Character opp = near[i];
                    if (opp != null && opp != mOwner && !opp.isBase && opp.health > 0f && !wave.hitList.Contains(opp))
                    {
                        wave.hitList.Add(opp);
                        opp.RecievedAttack(EAttackType.Lightning | EAttackType.BladeCritical, dmg);
                        opp.TryKnockback(75);
                    }
                }
            }
        }
    }

    private void ApplySlimVTaperBones()
    {
        for (int i = 0; i < mShrinkSpines.Count; i++)
        {
            if (mShrinkSpines[i] != null)
            {
                mShrinkSpines[i].localScale = new Vector3(0.62f, 1.07f, 0.58f);
            }
        }
        if (mChestBone != null)
        {
            mChestBone.localScale = new Vector3(1.48f, 1.04f, 1.48f);
        }
        if (mLeftSkirt != null)
        {
            mLeftSkirt.localScale = new Vector3(0.72f, 1.0f, 0.72f);
        }
        if (mRightSkirt != null)
        {
            mRightSkirt.localScale = new Vector3(0.72f, 1.0f, 0.72f);
        }
    }

    private Vector3 GetImpaledSpearWorldPos()
    {
        if (mImpaledTarget != null && mImpaledTarget.health > 0f)
        {
            return mImpaledTarget.position + Vector3.up * 36f;
        }
        return new Vector3(mSpearTargetPos.x, 8f, mSpearTargetPos.z);
    }

    private void ApplyRecallArmPoseIfNeeded()
    {
        bool shouldRaiseHand = (mUltState == SpearUltState.Recalling) ||
            (mUltState == SpearUltState.Impaled && mUltStateTimer >= kSpearImpaleHoldTime * 0.20f);
        if (!shouldRaiseHand || mFlyingSpear == null)
        {
            return;
        }
        Vector3 shoulderAnchor = GetShoulderAnchorWorldPos();
        Vector3 toSpear = (mFlyingSpear.transform.position - shoulderAnchor);
        if (toSpear.sqrMagnitude < 0.001f)
        {
            return;
        }
        toSpear.Normalize();

        // Raise arm up & forward toward impaled spear, then tug back sharply when Recalling!
        Vector3 aimTargetDir = toSpear;
        if (mUltState == SpearUltState.Impaled)
        {
            float raiseBlend = Mathf.Clamp01((mUltStateTimer - kSpearImpaleHoldTime * 0.20f) / (kSpearImpaleHoldTime * 0.35f));
            aimTargetDir = Vector3.Slerp(mVisualRoot.forward, (toSpear * 0.85f + Vector3.up * 0.42f).normalized, raiseBlend).normalized;
        }
        else if (mUltState == SpearUltState.Recalling)
        {
            float pullFrac = Mathf.Clamp01(mUltStateTimer / Mathf.Max(0.15f, kSpearRecallFlightTime * 0.55f));
            aimTargetDir = Vector3.Slerp((toSpear * 0.85f + Vector3.up * 0.42f).normalized, (toSpear * 0.55f + Vector3.up * 0.65f - mVisualRoot.forward * 0.35f).normalized, pullFrac).normalized;
        }

        Transform upperArm = (mBoneRightArm != null) ? mBoneRightArm : mRightUpArmBone;
        Transform foreArm = (mBoneRightForeArm != null) ? mBoneRightForeArm : mRightElbowBone;
        Transform handBone = (mBoneRightHand != null) ? mBoneRightHand : mRightHandBone;
        if (upperArm != null)
        {
            Vector3 upArmDir = (foreArm != null)
                ? (foreArm.position - upperArm.position).normalized
                : mVisualRoot.forward;
            if (upArmDir.sqrMagnitude > 0.001f)
            {
                Quaternion aimDelta = Quaternion.FromToRotation(upArmDir, aimTargetDir);
                upperArm.rotation = Quaternion.Slerp(Quaternion.identity, aimDelta, 0.35f) * upperArm.rotation;
            }
        }
        if (foreArm != null && handBone != null)
        {
            Vector3 foreDir = (handBone.position - foreArm.position).normalized;
            if (foreDir.sqrMagnitude > 0.001f)
            {
                Quaternion alignFore = Quaternion.FromToRotation(foreDir, aimTargetDir);
                foreArm.rotation = Quaternion.Slerp(Quaternion.identity, alignFore, 0.40f) * foreArm.rotation;
            }
        }
    }

    private Vector3 GetShoulderAnchorWorldPos()
    {
        if (mBoneRightArm != null)
        {
            return mBoneRightArm.position;
        }
        if (mRightUpArmBone != null)
        {
            return mRightUpArmBone.position;
        }
        return transform.position + Vector3.up * 88f;
    }

    private Vector3 GetHandWorldPos()
    {
        if (mRightHandMount != null)
        {
            return mRightHandMount.position;
        }
        if (mBoneRightHand != null)
        {
            return mBoneRightHand.position;
        }
        if (mTerasuStandaloneInstance != null && mVirtualRightHandPos.sqrMagnitude > 1.0f)
        {
            return mVirtualRightHandPos;
        }
        if (mRightHandBone != null)
        {
            return mRightHandBone.position;
        }
        if (mRightElbowBone != null)
        {
            return mRightElbowBone.position;
        }
        return transform.position + Vector3.up * 72f;
    }

    // -------------------------------------------------------------------------
    // Phase 2 Ultimate: Sun Wukong Backline Spear Hurl & Telekinetic Recall
    // -------------------------------------------------------------------------
    private void AdvanceMahoragaWheelStack()
    {
        if (!mHasAscended)
        {
            return;
        }
        mPassiveStackTimer = 0f;
        mMahoragaStacks = Mathf.Clamp(mMahoragaStacks + 1, 0, 6);
        mWheelAngleTarget += 60f;
        mWheelAngleVel += 260f;
        mWheelFlashTimer = 0.55f;
        PlaySfx(mClipGong, (mMahoragaStacks >= 6) ? 0.85f : 0.32f);

        if (mMahoragaStacks >= 6)
        {
            // 6th rotation: blaze wheel brightly & unleash Fist Clench Particle Shockwave Ultimate!
            mMahoragaStacks = 0;
            mWheelFlashTimer = 1.45f;
            mWheelOverdriveTimer = 1.65f;
            if (mTHaloWheelPivot != null)
            {
                SpawnHaloOverdriveParticles(mTHaloWheelPivot.position);
            }
            if (mUltState == SpearUltState.Ready && mActiveAttackStyle != Phase2AttackStyle.ThanosFistWave)
            {
                TriggerThanosShockwaveNow();
            }
        }
    }

    private void UpdateSpearUltimate()
    {
        if (mOwner == null || mOwner.health <= 0f)
        {
            CleanupActiveSpearUlt(true);
            return;
        }

        if (mUltState == SpearUltState.Ready)
        {
            if (labManualMode && !labAutoCycleUlts)
            {
                return;
            }
            // Wheel charges smoothly toward 6th rotation -> Glow Overdrive -> Shockwave Ultimate
            if (mMeleeSwingTimer <= 0f || mActiveAttackStyle != Phase2AttackStyle.ThanosFistWave)
            {
                mPassiveStackTimer += Time.deltaTime;
                if (mPassiveStackTimer >= 2.45f)
                {
                    AdvanceMahoragaWheelStack();
                }
            }

            // Independent Naginata Spear Throw + Magnet Recall cadence during Phase 2 battle
            if (mMeleeSwingTimer <= 0.1f)
            {
                mSpearUltAutoTimer += Time.deltaTime;
                if (mSpearUltAutoTimer >= 9.2f)
                {
                    mSpearUltAutoTimer = 0f;
                    BeginSpearThrowUltimate();
                }
            }
            return;
        }

        mUltStateTimer += Time.deltaTime;

        if (mUltState == SpearUltState.Throwing)
        {
            if (mImpaledTarget != null && mImpaledTarget.health > 0f)
            {
                mSpearTargetPos = new Vector3(mImpaledTarget.position.x, mImpaledTarget.position.y + 36f, mImpaledTarget.position.z);
            }
            float p = Mathf.Clamp01(mUltStateTimer / kSpearThrowTime);
            if (mFlyingSpear != null)
            {
                Vector3 pos = Vector3.Lerp(mSpearStartPos, mSpearTargetPos, p);
                pos.y += Mathf.Sin(p * Mathf.PI) * 52f;
                Vector3 nextP = Vector3.Lerp(mSpearStartPos, mSpearTargetPos, Mathf.Min(1f, p + 0.05f));
                nextP.y += Mathf.Sin(Mathf.Min(1f, p + 0.05f) * Mathf.PI) * 52f;
                Vector3 velDir = (nextP - pos).normalized;
                mFlyingSpear.transform.position = pos;
                if (velDir.sqrMagnitude > 0.001f)
                {
                    mFlyingSpear.transform.rotation = Quaternion.LookRotation(-velDir);
                }
                UpdateSpearTrail(pos);
                SliceOpponentsNearPoint(pos, 72f, GetBaseDamage() * 0.75f, mHitDuringThrow);
            }

            if (mUltStateTimer >= kSpearThrowTime)
            {
                EnterSpearImpaledState();
            }
        }
        else if (mUltState == SpearUltState.Impaled)
        {
            Vector3 impalePos = GetImpaledSpearWorldPos();
            if (mFlyingSpear != null)
            {
                mFlyingSpear.transform.position = impalePos - mSpearPlungeDir * 44f;
                mFlyingSpear.transform.rotation = mSpearImpaleRot;
                UpdateSpearTrail(impalePos);
            }
            if (mImpaleFlashSphere != null && mImpaleFlashSphere.activeSelf)
            {
                mImpaleFlashSphere.transform.position = impalePos;
                float flashScale = 28f + Mathf.Sin(Time.time * 26f) * 6f;
                mImpaleFlashSphere.transform.localScale = Vector3.one * flashScale;
            }
            if (mRecallHandPs != null && mUltStateTimer >= kSpearImpaleHoldTime * 0.20f)
            {
                if (!mRecallHandPs.gameObject.activeSelf) mRecallHandPs.gameObject.SetActive(true);
                mRecallHandPs.transform.position = GetHandWorldPos();
            }

            if (mUltStateTimer >= kSpearImpaleHoldTime)
            {
                EnterSpearRecallState();
            }
        }
        else if (mUltState == SpearUltState.Recalling)
        {
            float p = Mathf.Clamp01(mUltStateTimer / kSpearRecallFlightTime);
            float ease = p * p; // accelerates cleanly back into Terasu's hand
            Vector3 handPos = GetHandWorldPos();
            Vector3 impaleStart = GetImpaledSpearWorldPos();

            if (mRecallHandPs != null)
            {
                mRecallHandPs.transform.position = handPos;
            }

            if (mFlyingSpear != null)
            {
                Vector3 pos = Vector3.Lerp(impaleStart, handPos, ease);
                pos.y += Mathf.Sin(p * Mathf.PI) * 34f;
                mFlyingSpear.transform.position = pos;
                Vector3 toHand = (handPos - pos).normalized;
                if (toHand.sqrMagnitude > 0.001f)
                {
                    mFlyingSpear.transform.rotation = Quaternion.LookRotation(toHand);
                }
                UpdateSpearTrail(pos);
                UpdateRecallTether(handPos, pos);
                SliceOpponentsNearPoint(pos, 95f, GetBaseDamage() * 1.25f, mHitDuringRecall);
            }

            if (mUltStateTimer >= kSpearRecallFlightTime)
            {
                CompleteSpearRecall();
            }
        }
    }

    private void BeginSpearThrowUltimate()
    {
        Character backlineTarget = FindFarthestBacklineOpponent();
        if (backlineTarget == null)
        {
            mUltCooldownTimer = 1.5f;
            return;
        }

        mImpaledTarget = backlineTarget;
        mUltFacingDir = (backlineTarget.position.z >= transform.position.z) ? 1f : -1f;
        if (mOwner.controller != null)
        {
            mOwner.controller.facing = (mUltFacingDir >= 0f) ? FacingType.Right : FacingType.Left;
            mOwner.controller.StopWalking();
            mOwner.controller.PerformSpecialAction("attack", null, 0.48f);
        }

        PlaySfx(mClipSpearWhoosh, 1.0f);
        SetInHandSpearVisible(false);

        mSpearStartPos = GetHandWorldPos();
        mSpearTargetPos = new Vector3(backlineTarget.position.x, backlineTarget.position.y + 36f, backlineTarget.position.z);

        SpawnFlyingSpear(mSpearStartPos);
        PlayTerasuClip("Over_Shoulder_Throw", 0.06f, true, 1.45f);
        mHitDuringThrow.Clear();
        mHitDuringRecall.Clear();
        mSpearTrailHistory.Clear();

        mUltState = SpearUltState.Throwing;
        mUltStateTimer = 0f;
    }

    private void EnterSpearImpaledState()
    {
        mUltState = SpearUltState.Impaled;
        mUltStateTimer = 0f;

        Vector3 impalePos = GetImpaledSpearWorldPos();
        mSpearPlungeDir = new Vector3(0f, -0.46f, mUltFacingDir * 0.89f).normalized;
        mSpearImpaleRot = Quaternion.LookRotation(-mSpearPlungeDir);
        if (mFlyingSpear != null)
        {
            mFlyingSpear.transform.position = impalePos - mSpearPlungeDir * 44f;
            mFlyingSpear.transform.rotation = mSpearImpaleRot;
        }

        if (mImpaleFlashSphere == null)
        {
            mImpaleFlashSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mImpaleFlashSphere.name = "SpearImpaleFlash";
            Collider fCol = mImpaleFlashSphere.GetComponent<Collider>();
            if (fCol != null) Destroy(fCol);
            Renderer fRend = mImpaleFlashSphere.GetComponent<Renderer>();
            if (fRend != null)
            {
                fRend.material = CreateGlowMaterial(new Color(1.0f, 0.22f, 0.12f, 0.72f), true);
            }
        }
        mImpaleFlashSphere.SetActive(true);
        mImpaleFlashSphere.transform.position = impalePos;
        mImpaleFlashSphere.transform.localScale = Vector3.one * 30f;

        if (mImpalePillarLine == null)
        {
            GameObject pGo = new GameObject("SpearImpaleLightningPillar");
            mImpalePillarLine = pGo.AddComponent<LineRenderer>();
            mImpalePillarLine.useWorldSpace = true;
            mImpalePillarLine.material = CreateGlowMaterial(new Color(1f, 0.24f, 0.14f, 0.96f), true);
            mImpalePillarLine.startWidth = 13.0f;
            mImpalePillarLine.endWidth = 3.0f;
        }
        mImpalePillarLine.gameObject.SetActive(true);
        const int boltPts = 8;
        mImpalePillarLine.positionCount = boltPts;
        Vector3 boltBot = impalePos - Vector3.up * 28f;
        Vector3 boltTop = impalePos + Vector3.up * 210f;
        for (int bIdx = 0; bIdx < boltPts; bIdx++)
        {
            float bf = bIdx / (float)(boltPts - 1);
            Vector3 bp = Vector3.Lerp(boltBot, boltTop, bf);
            if (bIdx > 0 && bIdx < boltPts - 1)
            {
                float zig = ((bIdx % 2 == 0) ? 1f : -1f) * 11f * Mathf.Sin(bf * Mathf.PI);
                bp.z += zig;
                bp.x += ((bIdx % 3 == 0) ? 6f : -6f);
            }
            mImpalePillarLine.SetPosition(bIdx, bp);
        }

        if (mRecallHandPs == null)
        {
            GameObject rGo = new GameObject("TerasuRecallHandPs");
            mRecallHandPs = CreateRecallHandMagnetPs(rGo);
        }
        mRecallHandPs.gameObject.SetActive(false);

        PlaySfx(mClipLightning, 0.85f);
        mLightningFlashAlpha = Mathf.Max(mLightningFlashAlpha, 0.45f);
        SpawnShockwaveRing(impalePos, new Color(1f, 0.16f, 0.10f, 0.95f), 175f);
        DamageAndKnockbackArea(impalePos, 155f, GetBaseDamage() * 1.65f, 0, true);
    }

    private void EnterSpearRecallState()
    {
        mUltState = SpearUltState.Recalling;
        mUltStateTimer = 0f;
        PlaySfx(mClipSpearWhoosh, 1.0f);
        if (mImpalePillarLine != null)
        {
            mImpalePillarLine.gameObject.SetActive(false);
        }
        if (mImpaleFlashSphere != null)
        {
            mImpaleFlashSphere.SetActive(false);
        }
        if (mVortexVacuumLine != null)
        {
            mVortexVacuumLine.gameObject.SetActive(false);
        }

        Vector3 impalePos = GetImpaledSpearWorldPos();
        if (mOwner != null && mOwner.controller != null)
        {
            mOwner.controller.facing = (impalePos.z >= transform.position.z) ? FacingType.Right : FacingType.Left;
            mOwner.controller.Idle();
        }
        PlayTerasuClip("Charged_Spell_Cast", 0.08f, true, 1.4f);

        if (mRecallHandPs == null)
        {
            GameObject rGo = new GameObject("TerasuRecallHandPs");
            mRecallHandPs = CreateRecallHandMagnetPs(rGo);
        }
        mRecallHandPs.gameObject.SetActive(true);
        mRecallHandPs.transform.position = GetHandWorldPos();

        if (mRecallTetherLine == null)
        {
            GameObject go = new GameObject("TerasuRecallTether");
            mRecallTetherLine = go.AddComponent<LineRenderer>();
            mRecallTetherLine.material = CreateGlowMaterial(new Color(1f, 0.24f, 0.14f, 0.92f), true);
            mRecallTetherLine.startWidth = 5.0f;
            mRecallTetherLine.endWidth = 2.0f;
        }
        mRecallTetherLine.gameObject.SetActive(true);
        UpdateRecallTether(GetHandWorldPos(), impalePos);
    }

    private void CompleteSpearRecall()
    {
        SpawnShockwaveRing(GetHandWorldPos(), new Color(1f, 0.25f, 0.12f, 0.85f), 85f);
        CleanupActiveSpearUlt(true);
        mMahoragaStacks = 0;
        mPassiveStackTimer = 0f;
        mUltCooldownTimer = 0.8f;
    }

    private void CleanupActiveSpearUlt(bool showHandSpear)
    {
        mImpaledTarget = null;
        if (mFlyingSpear != null)
        {
            Destroy(mFlyingSpear);
            mFlyingSpear = null;
        }
        mSpearTrailLine = null;
        if (mImpalePillarLine != null)
        {
            mImpalePillarLine.gameObject.SetActive(false);
        }
        if (mImpaleFlashSphere != null)
        {
            mImpaleFlashSphere.SetActive(false);
        }
        if (mRecallTetherLine != null)
        {
            mRecallTetherLine.gameObject.SetActive(false);
        }
        if (mRecallHandOrb != null)
        {
            mRecallHandOrb.SetActive(false);
        }
        if (mRecallHandPs != null)
        {
            mRecallHandPs.gameObject.SetActive(false);
        }
        if (mVortexVacuumLine != null)
        {
            mVortexVacuumLine.gameObject.SetActive(false);
        }
        if (showHandSpear)
        {
            SetInHandSpearVisible(true);
        }
        mUltState = SpearUltState.Ready;
        mUltStateTimer = 0f;
    }

    private void SetInHandSpearVisible(bool visible)
    {
        HideOriginalChibiWeapons();
        if (mTInHandSpear != null)
        {
            mTInHandSpear.SetActive(visible);
        }
    }

    private void SpawnFlyingSpear(Vector3 startPos)
    {
        GameObject spearPrefab = Resources.Load("props/PFTerasuBloodMoonSpear") as GameObject;
        if (spearPrefab == null)
        {
            spearPrefab = Resources.Load("Props/PFZombieShogunSpear") as GameObject;
        }
        if (spearPrefab != null)
        {
            mFlyingSpear = Instantiate(spearPrefab, startPos, Quaternion.identity) as GameObject;
        }
        else
        {
            mFlyingSpear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mFlyingSpear.transform.position = startPos;
            mFlyingSpear.transform.localScale = new Vector3(4f, 45f, 4f);
        }
        mFlyingSpear.name = "FlyingBloodMoonSpear";
        float scaleMul = (mFlyingSpear.GetComponentInChildren<SkinnedMeshRenderer>() == null && spearPrefab != null && spearPrefab.name.IndexOf("Terasu", StringComparison.OrdinalIgnoreCase) >= 0) ? 136f : 1.15f;
        mFlyingSpear.transform.localScale = mFlyingSpear.transform.localScale * scaleMul;
        Collider[] cols = mFlyingSpear.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Destroy(cols[i]);
        }

        // Focused blade-collar ember glow (preserves spear silhouette without blocking mesh)
        GameObject auraOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        auraOrb.name = "SpearEclipseAura";
        auraOrb.transform.SetParent(mFlyingSpear.transform, false);
        float invScale = 1f / Mathf.Max(0.1f, Mathf.Abs(mFlyingSpear.transform.lossyScale.x));
        auraOrb.transform.localScale = Vector3.one * (8.5f * invScale);
        auraOrb.transform.localPosition = new Vector3(0f, 0f, 52f * invScale);
        Collider orbCol = auraOrb.GetComponent<Collider>();
        if (orbCol != null) Destroy(orbCol);
        Renderer orbRend = auraOrb.GetComponent<Renderer>();
        if (orbRend != null)
        {
            orbRend.material = CreateGlowMaterial(new Color(1f, 0.22f, 0.12f, 0.62f), true);
        }

        GameObject trailGo = new GameObject("SpearCrimsonTrail");
        trailGo.transform.SetParent(mFlyingSpear.transform, false);
        mSpearTrailLine = trailGo.AddComponent<LineRenderer>();
        mSpearTrailLine.useWorldSpace = true;
        mSpearTrailLine.material = CreateGlowMaterial(new Color(1f, 0.16f, 0.08f, 0.90f), true);
        mSpearTrailLine.startWidth = 6.5f;
        mSpearTrailLine.endWidth = 1.2f;
    }

    private void UpdateSpearTrail(Vector3 worldPos)
    {
        if (mSpearTrailLine == null)
        {
            return;
        }
        mSpearTrailHistory.Insert(0, worldPos);
        if (mSpearTrailHistory.Count > 10)
        {
            mSpearTrailHistory.RemoveAt(mSpearTrailHistory.Count - 1);
        }
        mSpearTrailLine.positionCount = mSpearTrailHistory.Count;
        for (int i = 0; i < mSpearTrailHistory.Count; i++)
        {
            mSpearTrailLine.SetPosition(i, mSpearTrailHistory[i]);
        }
    }

    private void UpdateRecallTether(Vector3 handPos, Vector3 spearPos)
    {
        if (mRecallTetherLine == null)
        {
            return;
        }
        const int segments = 12;
        mRecallTetherLine.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 p = Vector3.Lerp(handPos, spearPos, t);
            if (i > 0 && i < segments - 1)
            {
                float wave = Mathf.Sin(t * Mathf.PI * 3.0f - Time.time * 24f) * Mathf.Sin(t * Mathf.PI) * 4.5f;
                p.y += wave;
            }
            mRecallTetherLine.SetPosition(i, p);
        }
    }

    private Character FindFarthestBacklineOpponent()
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null || mOwner == null)
        {
            return null;
        }
        List<Character> alive = cm.allAlive;
        Character best = null;
        float bestDist = -1f;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c == mOwner || c.isBase || c.health <= 0f || c.isEnemy == mOwner.isEnemy)
            {
                continue;
            }
            float dz = Mathf.Abs(c.position.z - transform.position.z);
            if (dz <= 900f && dz > bestDist)
            {
                bestDist = dz;
                best = c;
            }
        }
        return best;
    }

    private float GetBaseDamage()
    {
        if (mOwner != null && mOwner.meleeDamage > 0f)
        {
            return mOwner.meleeDamage;
        }
        return 300f;
    }

    private void SliceOpponentsNearPoint(Vector3 worldPos, float radius, float damage, List<Character> hitList)
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null || mOwner == null)
        {
            return;
        }
        List<Character> alive = cm.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c == mOwner || c.health <= 0f || c.isEnemy == mOwner.isEnemy || hitList.Contains(c))
            {
                continue;
            }
            if (Mathf.Abs(c.position.z - worldPos.z) <= radius)
            {
                hitList.Add(c);
                c.RecievedAttack(EAttackType.BladeCritical, damage);
            }
        }
    }

    private void DamageAndKnockbackArea(Vector3 center, float radius, float damage, int knockbackPower, bool skipBases)
    {
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm == null || mOwner == null)
        {
            return;
        }
        List<Character> alive = cm.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c == mOwner || c.isBase || c.health <= 0f || c.isEnemy == mOwner.isEnemy)
            {
                continue;
            }
            if (skipBases && c.isBase)
            {
                continue;
            }
            if (Mathf.Abs(c.position.z - center.z) <= radius)
            {
                c.RecievedAttack(EAttackType.Lightning, damage);
                if (knockbackPower > 0)
                {
                    c.TryKnockback(knockbackPower);
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Standalone Phase 2 Terasu Prefab Replacement & Procedural Eclipse VFX
    // -------------------------------------------------------------------------
    private void DisableOriginalShogunRenderers()
    {
        if (mBodyRenderers == null) return;
        for (int i = 0; i < mBodyRenderers.Length; i++)
        {
            Renderer r = mBodyRenderers[i];
            if (r == null) continue;
            if (r.name.IndexOf("Yari", StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.name.IndexOf("Terasu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }
            r.enabled = false;
        }
    }

    private static Material CreateUnlitSolidMaterial(Color color)
    {
        Shader sh = Shader.Find("Unlit/Color");
        if (sh == null)
        {
            sh = Shader.Find("Sprites/Default");
        }
        if (sh == null)
        {
            sh = Shader.Find("Diffuse");
        }
        if (sh == null)
        {
            return null;
        }
        Material mat = new Material(sh);
        if (mat.HasProperty("_Color"))
        {
            mat.color = color;
        }
        return mat;
    }

    private void SpawnShatteredShogunHusk()
    {
        if (mShatteredHusk != null)
        {
            Destroy(mShatteredHusk);
            mShatteredHusk = null;
        }

        float groundY = transform.position.y;
        RailManager rm = WeakGlobalInstance<RailManager>.instance;
        if (rm != null)
        {
            groundY = rm.GetY(transform.position.z);
        }

        Vector3 groundPos = new Vector3(transform.position.x, groundY + 0.5f, transform.position.z);
        mShatteredHusk = new GameObject("ShatteredZombieShogunHusk");
        mShatteredHusk.transform.position = groundPos;
        float yaw = (mVisualRoot != null) ? mVisualRoot.eulerAngles.y : transform.eulerAngles.y;
        mShatteredHusk.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        mHuskSpawnTime = Time.time;

        // Clean ground-snapped crimson scorch fissure ring & broken Shogun katana (zero floating pancake cubes!)
        GameObject emberRingGo = new GameObject("CraterEmberRing");
        emberRingGo.transform.SetParent(mShatteredHusk.transform, false);
        emberRingGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        CreateCircleLineRenderer(emberRingGo, 24, 34f, new Color(1.0f, 0.22f, 0.10f, 0.88f), 4.2f, false);

        for (int cIdx = 0; cIdx < 3; cIdx++)
        {
            GameObject crackGo = new GameObject("CraterCrack_" + cIdx);
            crackGo.transform.SetParent(mShatteredHusk.transform, false);
            LineRenderer clr = crackGo.AddComponent<LineRenderer>();
            clr.useWorldSpace = false;
            clr.material = CreateGlowMaterial(new Color(1.0f, 0.28f, 0.12f, 0.86f), true);
            clr.startWidth = 3.6f;
            clr.endWidth = 1.2f;
            clr.positionCount = 3;
            float ang = (cIdx * 120f + 22f) * Mathf.Deg2Rad;
            clr.SetPosition(0, new Vector3(0f, 1.6f, 0f));
            clr.SetPosition(1, new Vector3(Mathf.Cos(ang + 0.18f) * 18f, 1.6f, Mathf.Sin(ang + 0.18f) * 18f));
            clr.SetPosition(2, new Vector3(Mathf.Cos(ang) * 34f, 1.6f, Mathf.Sin(ang) * 34f));
        }

        // Snapped old Zombie Shogun katana blade lying flat on bridge
        GameObject sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sword.name = "BrokenShogunKatana";
        Destroy(sword.GetComponent<Collider>());
        sword.transform.SetParent(mShatteredHusk.transform, false);
        sword.transform.localPosition = new Vector3(-12f, 1.6f, -10f);
        sword.transform.localRotation = Quaternion.Euler(2f, 54f, 90f);
        sword.transform.localScale = new Vector3(1.6f, 30f, 0.8f);
        sword.GetComponent<Renderer>().material = CreateUnlitSolidMaterial(new Color(0.42f, 0.44f, 0.50f, 1f));
    }

    private void UpdateShatteredHuskLifecycle()
    {
        if (mShatteredHusk == null)
        {
            return;
        }
        float age = Time.time - mHuskSpawnTime;
        if (age > 4.2f)
        {
            Destroy(mShatteredHusk);
            mShatteredHusk = null;
        }
        else if (age > 2.6f)
        {
            float shrink = Mathf.Clamp01(1.0f - (age - 2.6f) / 1.6f);
            mShatteredHusk.transform.localScale = new Vector3(shrink, shrink, shrink);
        }
    }

    private void UpdateSixRingDeityHalo()
    {
        if (mTerasuStandaloneInstance == null || mTHaloWheelPivot == null)
        {
            return;
        }
        bool overdrive = (mWheelOverdriveTimer > 0f);
        int litCount = overdrive ? 6 : mMahoragaStacks;
        float flashPulse = overdrive
            ? (Mathf.Sin(Time.time * 28f) * 0.5f + 0.5f) * 0.95f
            : ((mWheelFlashTimer > 0f) ? (Mathf.Sin(mWheelFlashTimer * 36f) * 0.5f + 0.5f) * 0.45f : 0f);
        float glowIntensity = 0.55f + (litCount / 6.0f) * 0.55f + flashPulse;
        if (mWheelDrumRenderers != null)
        {
            Color runeGlow = overdrive
                ? new Color(1.0f, 0.55f + 0.35f * flashPulse, 0.16f + 0.25f * flashPulse, 1.0f)
                : new Color(1.0f * glowIntensity, (0.14f + flashPulse * 0.5f), 0.10f, 1.0f);
            Color ringTint = overdrive
                ? Color.Lerp(Color.white, new Color(1.0f, 0.42f, 0.18f), 0.45f + 0.35f * flashPulse)
                : Color.white;
            for (int r = 0; r < mWheelDrumRenderers.Length; r++)
            {
                Renderer rend = mWheelDrumRenderers[r];
                if (rend == null) continue;
                Material[] mats = rend.materials;
                if (mats != null && mats.Length >= 1 && mats[0] != null && mats[0].HasProperty("_Color"))
                {
                    mats[0].color = ringTint;
                }
                if (mats != null && mats.Length >= 2 && mats[1] != null && mats[1].HasProperty("_Color"))
                {
                    mats[1].color = runeGlow;
                }
            }
        }
    }

    private void UpdateLeftHandVortexVacuum()
    {
        if (mUltState != SpearUltState.Impaled)
        {
            if (mVortexVacuumLine != null && mVortexVacuumLine.gameObject.activeSelf)
            {
                mVortexVacuumLine.gameObject.SetActive(false);
            }
            return;
        }

        Vector3 impalePos = GetImpaledSpearWorldPos();

        // Vacuum-pull opponents in backline toward the impaled spear
        CharactersManager cm = WeakGlobalInstance<CharactersManager>.instance;
        if (cm != null && mOwner != null)
        {
            bool oppAreEnemies = !mOwner.isEnemy;
            List<Character> candidates = cm.GetCharactersInRange(impalePos.z - 235f, impalePos.z + 235f, oppAreEnemies);
            for (int i = 0; i < candidates.Count; i++)
            {
                Character opp = candidates[i];
                if (opp == null || opp == mOwner || opp == mImpaledTarget || opp.isBase || opp.health <= 0f)
                {
                    continue;
                }
                Vector3 curPos = opp.position;
                Vector3 toImpale = impalePos - curPos;
                toImpale.y = 0f;
                float dist = toImpale.magnitude;
                if (dist > 14f && dist < 240f)
                {
                    float step = Mathf.Min(dist - 10f, 150f * Time.deltaTime);
                    Vector3 nextPos = curPos + (toImpale / dist) * step;
                    if (opp.controller != null)
                    {
                        nextPos.z = Mathf.Clamp(nextPos.z, opp.controller.constraintLeft + 10f, opp.controller.constraintRight - 10f);
                    }
                    opp.position = nextPos;
                }
            }
        }

        if (mVortexVacuumLine == null)
        {
            GameObject vGo = new GameObject("RightHandTelekineticAimBeam");
            if (mAscensionVfxRoot != null)
            {
                vGo.transform.SetParent(mAscensionVfxRoot.transform, false);
            }
            mVortexVacuumLine = vGo.AddComponent<LineRenderer>();
            mVortexVacuumLine.useWorldSpace = true;
            mVortexVacuumLine.material = CreateGlowMaterial(Color.white, true);
            mVortexVacuumLine.startColor = new Color(0.95f, 0.14f, 0.08f, 0.92f);
            mVortexVacuumLine.endColor = new Color(0.65f, 0.03f, 0.06f, 0.92f);
            mVortexVacuumLine.startWidth = 3.8f;
            mVortexVacuumLine.endWidth = 6.2f;
        }

        if (mUltStateTimer >= kSpearImpaleHoldTime * 0.42f)
        {
            mVortexVacuumLine.gameObject.SetActive(true);
            Vector3 startPos = GetHandWorldPos();
            Vector3 endPos = impalePos;
            const int segs = 16;
            mVortexVacuumLine.positionCount = segs;
            for (int i = 0; i < segs; i++)
            {
                float frac = i / (float)(segs - 1);
                Vector3 p = Vector3.Lerp(startPos, endPos, frac);
                if (i > 0 && i < segs - 1)
                {
                    float amp = Mathf.Sin(frac * Mathf.PI) * 4.0f;
                    float phase = Time.time * 16f - frac * 12f;
                    p.x += Mathf.Cos(phase) * amp;
                    p.y += Mathf.Sin(phase) * amp;
                }
                mVortexVacuumLine.SetPosition(i, p);
            }
        }
        else if (mVortexVacuumLine.gameObject.activeSelf)
        {
            mVortexVacuumLine.gameObject.SetActive(false);
        }
    }

    public static void ConfigureTerasuKitMaterials(GameObject root)
    {
        if (root == null) return;
        Renderer[] kitRenderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < kitRenderers.Length; i++)
        {
            if (kitRenderers[i] is LineRenderer) continue;
            Material[] mats = Application.isPlaying ? kitRenderers[i].materials : kitRenderers[i].sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null) continue;
                // Preserve crisp baked Meshy materials (Mat_TerasuGod / Mat_TerasuRuneCore / Mat_TerasuSpear)
                if (mats[m].name.IndexOf("Mat_Terasu", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }
            }
        }
    }

    private void AttachStandaloneTerasuPrefab()
    {
        if (mTerasuStandaloneInstance != null)
        {
            return;
        }
        GameObject prefab = Resources.Load("characters/PFTerasuBloodMoon") as GameObject;
        if (prefab == null)
        {
            prefab = Resources.Load("Characters/PFTerasuBloodMoon") as GameObject;
        }
        if (prefab == null)
        {
            return;
        }
        mTerasuStandaloneInstance = Instantiate(prefab) as GameObject;
        mTerasuStandaloneInstance.name = "PFTerasuBloodMoon_ActiveInstance";
        mTerasuStandaloneInstance.transform.SetParent(mVisualRoot, false);
        mTerasuStandaloneInstance.transform.localPosition = Vector3.zero;
        mTerasuStandaloneInstance.transform.localRotation = Quaternion.identity;
        mTerasuStandaloneInstance.transform.localScale = Vector3.one * 134f;
        mKitAttachedTime = Time.time;

        mTerasuAnimator = mTerasuStandaloneInstance.GetComponent<Animator>();
        mTHaloWheelPivot = FindChildRecursive(mTerasuStandaloneInstance.transform, "Terasu_HaloWheelPivot");
        if (mTHaloWheelPivot != null)
        {
            if (mTHaloWheelPivot.parent != mTerasuStandaloneInstance.transform)
            {
                mTHaloWheelPivot.SetParent(mTerasuStandaloneInstance.transform, false);
            }
            mWheelDrumRenderers = mTHaloWheelPivot.GetComponentsInChildren<Renderer>(true);
        }

        mBoneHips = FindChildRecursive(mTerasuStandaloneInstance.transform, "Hips");
        if (mBoneHips != null)
        {
            mRestHipsLocalPos = mBoneHips.localPosition;
        }
        mBoneSpine = FindChildRecursive(mTerasuStandaloneInstance.transform, "Spine");
        mBoneLeftArm = FindChildRecursive(mTerasuStandaloneInstance.transform, "LeftArm");
        mBoneLeftForeArm = FindChildRecursive(mTerasuStandaloneInstance.transform, "LeftForeArm");
        mBoneLeftHand = FindChildRecursive(mTerasuStandaloneInstance.transform, "LeftHand");
        mBoneRightArm = FindChildRecursive(mTerasuStandaloneInstance.transform, "RightArm");
        mBoneRightForeArm = FindChildRecursive(mTerasuStandaloneInstance.transform, "RightForeArm");
        mBoneRightHand = FindChildRecursive(mTerasuStandaloneInstance.transform, "RightHand");
        mBoneLeftUpLeg = FindChildRecursive(mTerasuStandaloneInstance.transform, "LeftUpLeg");
        mBoneRightUpLeg = FindChildRecursive(mTerasuStandaloneInstance.transform, "RightUpLeg");
        mRightHandMount = FindChildRecursive(mTerasuStandaloneInstance.transform, "Terasu_RightHandMount");
        mLeftHandMount = FindChildRecursive(mTerasuStandaloneInstance.transform, "Terasu_LeftHandMount");

        // Attach glowing Left-Hand Thanos Particle Vortex inside distal palm socket (no giant wrist balloon!)
        if (mTLeftVortexOrb == null && mLeftHandMount != null)
        {
            mTLeftVortexOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mTLeftVortexOrb.name = "Terasu_LeftVortexOrb";
            mTLeftVortexOrb.transform.SetParent(mLeftHandMount, false);
            mTLeftVortexOrb.transform.localPosition = Vector3.zero;
            mTLeftVortexOrb.transform.localScale = Vector3.one * 0.06f;
            Collider orbCol = mTLeftVortexOrb.GetComponent<Collider>();
            if (orbCol != null) Destroy(orbCol);
            Renderer orbRend = mTLeftVortexOrb.GetComponent<Renderer>();
            if (orbRend != null)
            {
                orbRend.material = CreateGlowMaterial(new Color(0.28f, 0.01f, 0.02f, 0.88f), false);
            }
            mLeftHandVortexPs = CreateLeftHandVortexPs(mTLeftVortexOrb);
        }

        // Attach cursed Talismans Naginata cleanly inside RightHandMount
        if (mTInHandSpear == null)
        {
            GameObject spearPrefab = Resources.Load("props/PFTerasuBloodMoonSpear") as GameObject;
            if (spearPrefab == null)
            {
                spearPrefab = Resources.Load("Props/PFZombieShogunSpear") as GameObject;
            }
            if (spearPrefab != null)
            {
                Transform handParent = (mRightHandMount != null) ? mRightHandMount : mTerasuStandaloneInstance.transform;
                mTInHandSpear = Instantiate(spearPrefab) as GameObject;
                mTInHandSpear.name = "TerasuPhase2InHandSpear";
                mTInHandSpear.transform.SetParent(handParent, false);
                bool isMeshySpear = spearPrefab.name.IndexOf("Terasu", StringComparison.OrdinalIgnoreCase) >= 0;
                mBaseSpearLocalPos = isMeshySpear ? new Vector3(0f, -0.22f, 0f) : Vector3.zero;
                mBaseSpearLocalEuler = isMeshySpear ? new Vector3(-90f, 0f, 0f) : Vector3.zero;
                mBaseSpearLocalScale = isMeshySpear ? Vector3.one : (Vector3.one * 0.012f);
                mTInHandSpear.transform.localPosition = mBaseSpearLocalPos + labSpearPosOffset;
                mTInHandSpear.transform.localRotation = Quaternion.Euler(mBaseSpearLocalEuler + labSpearEulerOffset);
                mTInHandSpear.transform.localScale = mBaseSpearLocalScale * Mathf.Clamp(labSpearScaleMultiplier, 0.3f, 2.5f);
                Collider[] cols = mTInHandSpear.GetComponentsInChildren<Collider>(true);
                for (int cIdx = 0; cIdx < cols.Length; cIdx++)
                {
                    Destroy(cols[cIdx]);
                }
            }
        }

        HideOriginalChibiWeapons();
        mLastPlayedClip = string.Empty;
        PlayTerasuClip("Combat_Stance", 0.05f, true, 1.0f);
        UpdateRiggedMeshyPhase2LatePose();
    }

    private static Transform FindChildRecursive(Transform root, string exactOrPrefix)
    {
        if (root == null) return null;
        if (root.name.Equals(exactOrPrefix, StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform hit = FindChildRecursive(root.GetChild(i), exactOrPrefix);
            if (hit != null) return hit;
        }
        return null;
    }

    private void TintBodyRenderers(Color targetColor)
    {
        if (mBodyRenderers == null) return;
        for (int i = 0; i < mBodyRenderers.Length; i++)
        {
            if (mBodyRenderers[i] == null) continue;
            Material mat = mBodyRenderers[i].material;
            if (mat != null && mat.HasProperty("_Color"))
            {
                mat.color = targetColor;
            }
        }
    }

    private void ApplyAtmosphereDarkening(float t)
    {
        if (!mSavedEnvColors) return;
        RenderSettings.ambientLight = Color.Lerp(mOrigAmbientLight, new Color(0.56f, 0.28f, 0.30f), t);
        if (Camera.main != null)
        {
            Camera.main.backgroundColor = Color.Lerp(mOrigCamBgColor, new Color(0.18f, 0.03f, 0.05f), t);
        }
    }

    private void CreateAscensionVfx()
    {
        if (mAscensionVfxRoot == null)
        {
            mAscensionVfxRoot = new GameObject("TerasuAscensionVfx");
            mAscensionVfxRoot.transform.SetParent(transform, false);
        }

        // Translucent Blood Moon Energy Sphere
        if (mAscensionSphere == null)
        {
            mAscensionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mAscensionSphere.name = "BloodMoonAscensionSphere";
            mAscensionSphere.transform.SetParent(mAscensionVfxRoot.transform, false);
            mAscensionSphere.transform.localPosition = new Vector3(0f, 65f, 0f);
            Collider col = mAscensionSphere.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = mAscensionSphere.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = CreateGlowMaterial(new Color(0.95f, 0.10f, 0.08f, 0.34f), true);
            }
            mAscensionSphere.SetActive(false);
        }

        // Inner blazing Max Steel / Azir transformation energy cocoon that shields & swells at apex
        if (mCoreCocoonSphere == null && mAscensionSphere != null)
        {
            mCoreCocoonSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mCoreCocoonSphere.name = "BloodMoonCoreCocoon";
            mCoreCocoonSphere.transform.SetParent(mAscensionSphere.transform, false);
            mCoreCocoonSphere.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            Collider colC = mCoreCocoonSphere.GetComponent<Collider>();
            if (colC != null) Destroy(colC);
            mCoreCocoonRenderer = mCoreCocoonSphere.GetComponent<Renderer>();
            if (mCoreCocoonRenderer != null)
            {
                mCoreCocoonRenderer.material = CreateGlowMaterial(new Color(1.0f, 0.32f, 0.14f, 0.65f), true);
            }
        }

        // 3 Orbital Azir-Style Energy Rings around the sphere
        if (mAscensionRings.Count == 0)
        {
            for (int rIdx = 0; rIdx < 3; rIdx++)
            {
                GameObject rGo = new GameObject("OrbitalRing_" + rIdx);
                rGo.transform.SetParent(mAscensionSphere.transform, false);
                LineRenderer lr = CreateCircleLineRenderer(rGo, 36, 0.62f, new Color(1f, 0.28f, 0.14f, 0.9f), 4.5f, false);
                mAscensionRings.Add(lr);
            }
        }

        // 12 Crimson-Black Lightning Bolts for apocalyptic Ascension storm
        if (mLightningBolts.Count == 0)
        {
            for (int b = 0; b < 12; b++)
            {
                GameObject lGo = new GameObject("BloodLightning_" + b);
                lGo.transform.SetParent(mAscensionVfxRoot.transform, false);
                LineRenderer lr = lGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                Color bCol = (b % 3 == 0) ? new Color(1f, 0.65f, 0.35f, 0.98f)
                    : ((b % 3 == 1) ? new Color(1f, 0.14f, 0.08f, 0.95f) : new Color(0.24f, 0.02f, 0.04f, 0.95f));
                lr.material = CreateGlowMaterial(bCol, true);
                lr.startWidth = 9.0f;
                lr.endWidth = 2.5f;
                lGo.SetActive(false);
                mLightningBolts.Add(lr);
            }
        }

        // Ground Blood-Eclipse Halo Ring
        if (mGroundEclipseRing == null)
        {
            GameObject gGo = new GameObject("GroundEclipseHaloRing");
            gGo.transform.SetParent(mAscensionVfxRoot.transform, false);
            gGo.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            mGroundEclipseRing = CreateCircleLineRenderer(gGo, 40, 48f, new Color(0.95f, 0.12f, 0.08f, 0.85f), 4.5f, false);

            GameObject gIn = new GameObject("GroundEclipseInnerRing");
            gIn.transform.SetParent(gGo.transform, false);
            mGroundEclipseInnerRing = CreateCircleLineRenderer(gIn, 32, 30f, new Color(1f, 0.35f, 0.15f, 0.75f), 2.8f, false);
        }

        // 3D Blood Moon Backdrop Disc behind the lane in the sky
        if (mBloodMoonBackdrop == null)
        {
            mBloodMoonBackdrop = new GameObject("BloodMoonEclipseSky");
            GameObject moonDisc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moonDisc.name = "BloodMoonCorona";
            moonDisc.transform.SetParent(mBloodMoonBackdrop.transform, false);
            moonDisc.transform.localScale = new Vector3(160f, 160f, 8f);
            Destroy(moonDisc.GetComponent<Collider>());
            moonDisc.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.92f, 0.08f, 0.06f, 0.92f), true);

            GameObject umbra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            umbra.name = "EclipseUmbra";
            umbra.transform.SetParent(mBloodMoonBackdrop.transform, false);
            umbra.transform.localScale = new Vector3(132f, 132f, 10f);
            umbra.transform.localPosition = new Vector3(6f, 6f, -6f);
            Destroy(umbra.GetComponent<Collider>());
            Renderer ur = umbra.GetComponent<Renderer>();
            Shader unlit = Shader.Find("Unlit/Color");
            if (unlit != null) ur.material.shader = unlit;
            ur.material.color = new Color(0.05f, 0.01f, 0.02f, 1f);
        }
        UpdateBloodMoonPosition();
    }

    private void UpdateBloodMoonPosition()
    {
        if (mBloodMoonBackdrop == null || Camera.main == null) return;
        Transform cam = Camera.main.transform;
        Vector3 skyPos = cam.position + cam.forward * 480f + cam.up * 88f + cam.right * 115f;
        mBloodMoonBackdrop.transform.position = skyPos;
        mBloodMoonBackdrop.transform.LookAt(cam.position);
    }

    private void UpdateGroundHaloRing()
    {
        if (mGroundEclipseRing == null) return;
        float rOuter = mHasAscended ? (44f + Mathf.Sin(Time.time * 4f) * 3f) : 52f;
        mGroundEclipseRing.transform.Rotate(0f, Time.deltaTime * 55f, 0f, Space.Self);
        mGroundEclipseRing.transform.localScale = new Vector3(rOuter / 48f, 1f, rOuter / 48f);
        if (mGroundEclipseInnerRing != null)
        {
            mGroundEclipseInnerRing.transform.Rotate(0f, -Time.deltaTime * 90f, 0f, Space.Self);
        }
    }

    private void SetAscensionSphereActive(bool active, float progress)
    {
        if (mAscensionSphere == null) return;
        mAscensionSphere.SetActive(active);
        if (!active) return;

        Vector3 center = (mVisualRoot != null) ? (mVisualRoot.localPosition + Vector3.up * 44f) : new Vector3(0f, 80f, 0f);
        mAscensionSphere.transform.localPosition = center;
        float climaxSwirl = (progress >= 0.72f) ? Mathf.Lerp(1.0f, 1.38f, (progress - 0.72f) / 0.28f) : 1.0f;
        float radius = (Mathf.Lerp(68f, 118f, progress) + Mathf.Sin(Time.time * 16f) * 5f) * climaxSwirl;
        mAscensionSphere.transform.localScale = new Vector3(radius, radius, radius);

        if (mCoreCocoonSphere != null && mCoreCocoonRenderer != null && mCoreCocoonRenderer.material != null)
        {
            float cScale = (progress >= 0.68f) ? Mathf.Lerp(0.45f, 0.98f, (progress - 0.68f) / 0.32f) : (0.36f + progress * 0.14f);
            mCoreCocoonSphere.transform.localScale = new Vector3(cScale, cScale, cScale);
            float cAlpha = (progress >= 0.68f) ? Mathf.Lerp(0.55f, 0.96f, (progress - 0.68f) / 0.32f) : 0.42f;
            Color cCol = Color.Lerp(new Color(1.0f, 0.18f, 0.08f, cAlpha), new Color(1.0f, 0.75f, 0.42f, cAlpha), Mathf.Max(0f, progress - 0.5f) * 2f);
            if (mCoreCocoonRenderer.material.HasProperty("_TintColor"))
            {
                mCoreCocoonRenderer.material.SetColor("_TintColor", cCol);
            }
            if (mCoreCocoonRenderer.material.HasProperty("_Color"))
            {
                mCoreCocoonRenderer.material.color = cCol;
            }
        }

        for (int i = 0; i < mAscensionRings.Count; i++)
        {
            Transform rt = mAscensionRings[i].transform;
            rt.localRotation = Quaternion.Euler(
                35f + i * 50f + Time.time * (95f + i * 35f),
                Time.time * (120f - i * 30f),
                i * 42f
            );
        }
    }

    private void SetLightningVisible(bool visible)
    {
        for (int i = 0; i < mLightningBolts.Count; i++)
        {
            if (mLightningBolts[i] != null)
            {
                mLightningBolts[i].gameObject.SetActive(visible);
            }
        }
    }

    private void UpdateLightningStrikes()
    {
        mLightningRetargetTimer -= Time.deltaTime;
        if (mLightningRetargetTimer > 0f)
        {
            return;
        }
        mLightningRetargetTimer = 0.08f;

        Vector3 target = transform.position + (mVisualRoot != null ? mVisualRoot.localPosition : Vector3.zero) + Vector3.up * 22f;
        for (int i = 0; i < mLightningBolts.Count; i++)
        {
            LineRenderer lr = mLightningBolts[i];
            if (lr == null) continue;
            lr.gameObject.SetActive(true);
            Vector3 skyOrigin = target + new Vector3(
                UnityEngine.Random.Range(-110f, 110f),
                UnityEngine.Random.Range(160f, 260f),
                UnityEngine.Random.Range(-110f, 110f)
            );
            const int pts = 8;
            lr.positionCount = pts;
            for (int p = 0; p < pts; p++)
            {
                float frac = p / (float)(pts - 1);
                Vector3 pos = Vector3.Lerp(skyOrigin, target, frac);
                if (p > 0 && p < pts - 1)
                {
                    pos.x += UnityEngine.Random.Range(-22f, 22f);
                    pos.z += UnityEngine.Random.Range(-22f, 22f);
                }
                lr.SetPosition(p, pos);
            }
        }
    }

    private void SpawnShockwaveRing(Vector3 worldPos, Color color, float maxScale)
    {
        if (mShockwaveRing == null)
        {
            mShockwaveRing = new GameObject("TerasuShockwave");
            CreateCircleLineRenderer(mShockwaveRing, 36, 1f, color, 8f, false);
        }
        mShockwaveRing.SetActive(true);
        mShockwaveRing.transform.position = worldPos;
        mShockwaveRing.transform.localScale = new Vector3(10f, maxScale, 10f);
        mShockwaveTimer = 0.001f;
    }

    private void UpdateShockwaveRing()
    {
        if (mShockwaveTimer <= 0f || mShockwaveRing == null) return;
        mShockwaveTimer += Time.deltaTime;
        float dur = 0.42f;
        float p = Mathf.Clamp01(mShockwaveTimer / dur);
        float maxR = Mathf.Max(80f, mShockwaveRing.transform.localScale.y);
        float r = Mathf.Lerp(12f, maxR, p);
        mShockwaveRing.transform.localScale = new Vector3(r, maxR, r);
        if (mShockwaveTimer >= dur)
        {
            mShockwaveTimer = 0f;
            mShockwaveRing.SetActive(false);
        }
    }

    private static LineRenderer CreateCircleLineRenderer(GameObject go, int segments, float radius, Color color, float width, bool useWorld)
    {
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = useWorld;
        lr.loop = true;
        lr.material = CreateGlowMaterial(color, true);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float a = (2f * Mathf.PI * i) / segments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        return lr;
    }

    private static void EnsureParticleTextures()
    {
        if (sSoftGlowTex == null)
        {
            const int size = 64;
            sSoftGlowTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            sSoftGlowTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Clamp01(1.0f - r);
                    float a = Mathf.Pow(falloff, 2.6f);
                    byte ab = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, ab);
                }
            }
            sSoftGlowTex.SetPixels32(pixels);
            sSoftGlowTex.Apply(false, false);
        }
        if (sSoftSparkTex == null)
        {
            const int w = 64;
            const int h = 32;
            sSoftSparkTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            sSoftSparkTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[w * h];
            float hx = (w - 1) * 0.5f;
            float hy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - hx) / hx;
                    float dy = (y - hy) / hy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1.0f - r);
                    a = a * a;
                    byte ab = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * w + x] = new Color32(255, 255, 255, ab);
                }
            }
            sSoftSparkTex.SetPixels32(pixels);
            sSoftSparkTex.Apply(false, false);
        }
    }

    private static Material CreateSoftParticleMaterial(bool additive, Texture2D tex, Color tint)
    {
        EnsureParticleTextures();
        // Unity built-in Particle shaders multiply _TintColor * 2.0f; use 0.5f base so radial falloff stays silky-soft
        Color calibrated = new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, tint.a * 0.5f);
        Material mat = CreateGlowMaterial(calibrated, additive);
        if (mat != null)
        {
            mat.mainTexture = (tex != null) ? tex : sSoftGlowTex;
        }
        return mat;
    }

    private static void SetupParticleFadeAndShrink(ParticleSystem ps, Color startColor, Color endColor, float startSizeEndFrac)
    {
        ParticleSystem.ColorOverLifetimeModule colMod = ps.colorOverLifetime;
        colMod.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startColor, 0.0f), new GradientColorKey(endColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(startColor.a, 0.0f), new GradientAlphaKey(startColor.a * 0.9f, 0.45f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colMod.color = new ParticleSystem.MinMaxGradient(grad);

        ParticleSystem.SizeOverLifetimeModule szMod = ps.sizeOverLifetime;
        szMod.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, startSizeEndFrac);
        szMod.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
    }

    private ParticleSystem CreateLeftHandVortexPs(GameObject host)
    {
        EnsureParticleTextures();
        ParticleSystem ps = host.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 38f);
        main.startSize = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.18f, 0.14f, 0.85f));
        main.maxParticles = 64;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 24f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.06f;

        SetupParticleFadeAndShrink(ps, new Color(1.0f, 0.34f, 0.16f, 0.85f), new Color(0.55f, 0.04f, 0.10f, 0f), 0.15f);

        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateSoftParticleMaterial(true, sSoftGlowTex, Color.white);
        return ps;
    }

    private ParticleSystem CreateRecallHandMagnetPs(GameObject host)
    {
        EnsureParticleTextures();
        ParticleSystem ps = host.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 48f);
        main.startSize = new ParticleSystem.MinMaxCurve(12f, 26f);
        main.maxParticles = 72;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 42f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 6f;

        SetupParticleFadeAndShrink(ps, new Color(1.0f, 0.48f, 0.16f, 0.95f), new Color(0.92f, 0.06f, 0.08f, 0f), 0.2f);

        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateSoftParticleMaterial(true, sSoftGlowTex, Color.white);
        return ps;
    }

    private ParticleSystem CreateShockwaveTrailPs(GameObject host, Vector3 fwd)
    {
        EnsureParticleTextures();
        ParticleSystem ps = host.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.44f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(45f, 140f);
        main.startSize = new ParticleSystem.MinMaxCurve(10f, 22f);
        main.maxParticles = 140;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 110f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(130f, 36f, 18f);

        SetupParticleFadeAndShrink(ps, new Color(1.0f, 0.48f, 0.16f, 0.92f), new Color(0.65f, 0.04f, 0.06f, 0f), 0.2f);

        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateSoftParticleMaterial(true, sSoftGlowTex, Color.white);
        return ps;
    }

    private void SpawnThanosExplosionParticles(Vector3 center, Vector3 fwd)
    {
        EnsureParticleTextures();

        // Layer 1: Blazing Crimson/Gold Fireball Particle Burst (Additive)
        GameObject fireGo = new GameObject("ThanosExplosionFireBurst");
        fireGo.transform.position = center;
        fireGo.transform.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 0.001f ? fwd : Vector3.forward);
        ParticleSystem firePs = fireGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule fm = firePs.main;
        fm.loop = false;
        fm.playOnAwake = false;
        fm.simulationSpace = ParticleSystemSimulationSpace.World;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(240f, 580f);
        fm.startSize = new ParticleSystem.MinMaxCurve(10f, 26f);
        fm.gravityModifier = -0.15f;
        fm.maxParticles = 140;
        ParticleSystem.EmissionModule fe = firePs.emission;
        fe.enabled = false;
        ParticleSystem.ShapeModule fsh = firePs.shape;
        fsh.enabled = true;
        fsh.shapeType = ParticleSystemShapeType.Cone;
        fsh.angle = 48f;
        fsh.radius = 10f;
        SetupParticleFadeAndShrink(firePs, new Color(1.0f, 0.58f, 0.14f, 0.90f), new Color(0.90f, 0.08f, 0.05f, 0f), 0.25f);
        ParticleSystemRenderer fpr = firePs.GetComponent<ParticleSystemRenderer>();
        fpr.renderMode = ParticleSystemRenderMode.Billboard;
        fpr.material = CreateSoftParticleMaterial(true, sSoftGlowTex, Color.white);
        firePs.Emit(75);
        Destroy(fireGo, 1.3f);

        // Layer 2: Obsidian Void Dark Shockwave Plumes (Translucent Alpha Blended wisps so Terasu remains sharp)
        GameObject darkGo = new GameObject("ThanosExplosionDarkPlume");
        darkGo.transform.position = center;
        ParticleSystem darkPs = darkGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule dm = darkPs.main;
        dm.loop = false;
        dm.playOnAwake = false;
        dm.simulationSpace = ParticleSystemSimulationSpace.World;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.36f, 0.72f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(180f, 440f);
        dm.startSize = new ParticleSystem.MinMaxCurve(14f, 32f);
        dm.maxParticles = 60;
        ParticleSystem.EmissionModule de = darkPs.emission;
        de.enabled = false;
        ParticleSystem.ShapeModule dsh = darkPs.shape;
        dsh.enabled = true;
        dsh.shapeType = ParticleSystemShapeType.Sphere;
        dsh.radius = 12f;
        SetupParticleFadeAndShrink(darkPs, new Color(0.24f, 0.02f, 0.08f, 0.34f), new Color(0.05f, 0.0f, 0.02f, 0f), 1.65f);
        ParticleSystemRenderer dpr = darkPs.GetComponent<ParticleSystemRenderer>();
        dpr.renderMode = ParticleSystemRenderMode.Billboard;
        dpr.material = CreateSoftParticleMaterial(false, sSoftGlowTex, Color.white);
        darkPs.Emit(32);
        Destroy(darkGo, 1.4f);

        // Layer 3: Stretched Ember Streaks & Lightning Shards
        GameObject sparkGo = new GameObject("ThanosExplosionSparks");
        sparkGo.transform.position = center;
        sparkGo.transform.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 0.001f ? fwd : Vector3.forward);
        ParticleSystem sparkPs = sparkGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule sm = sparkPs.main;
        sm.loop = false;
        sm.playOnAwake = false;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.62f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(380f, 820f);
        sm.startSize = new ParticleSystem.MinMaxCurve(5f, 14f);
        sm.gravityModifier = 0.25f;
        sm.maxParticles = 160;
        ParticleSystem.EmissionModule se = sparkPs.emission;
        se.enabled = false;
        ParticleSystem.ShapeModule ssh = sparkPs.shape;
        ssh.enabled = true;
        ssh.shapeType = ParticleSystemShapeType.Sphere;
        ssh.radius = 8f;
        SetupParticleFadeAndShrink(sparkPs, new Color(1.0f, 0.88f, 0.45f, 1.0f), new Color(1.0f, 0.16f, 0.06f, 0f), 0.1f);
        ParticleSystemRenderer spr = sparkPs.GetComponent<ParticleSystemRenderer>();
        spr.renderMode = ParticleSystemRenderMode.Stretch;
        spr.velocityScale = 0.12f;
        spr.lengthScale = 3.2f;
        spr.material = CreateSoftParticleMaterial(true, sSoftSparkTex, Color.white);
        sparkPs.Emit(120);
        Destroy(sparkGo, 1.3f);
    }

    private void SpawnHaloOverdriveParticles(Vector3 wheelWorldPos)
    {
        EnsureParticleTextures();
        GameObject go = new GameObject("TerasuHalo6thSpinFlare");
        go.transform.position = wheelWorldPos;
        go.transform.rotation = (mTHaloWheelPivot != null) ? mTHaloWheelPivot.rotation : Quaternion.identity;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule m = ps.main;
        m.loop = false;
        m.playOnAwake = false;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(95f, 240f);
        m.startSize = new ParticleSystem.MinMaxCurve(18f, 38f);
        m.maxParticles = 80;
        ParticleSystem.EmissionModule e = ps.emission;
        e.enabled = false;
        ParticleSystem.ShapeModule sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 52f;
        SetupParticleFadeAndShrink(ps, new Color(1.0f, 0.82f, 0.30f, 1.0f), new Color(1.0f, 0.14f, 0.08f, 0f), 0.25f);
        ParticleSystemRenderer pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.material = CreateSoftParticleMaterial(true, sSoftGlowTex, Color.white);
        ps.Emit(65);
        Destroy(go, 1.4f);
    }

    private static Material CreateGlowMaterial(Color color, bool additive)
    {
        Shader sh = Shader.Find(additive ? "Particles/Additive" : "Particles/Alpha Blended");
        if (sh == null)
        {
            sh = Shader.Find("Unlit/Color");
        }
        if (sh == null)
        {
            sh = Shader.Find("Sprites/Default");
        }
        if (sh == null)
        {
            sh = Shader.Find("Diffuse");
        }
        if (sh == null)
        {
            return null;
        }
        Material mat = new Material(sh);
        SetGlowMaterialColor(mat, color);
        return mat;
    }

    private static void SetGlowMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_TintColor"))
        {
            mat.SetColor("_TintColor", color);
        }
        if (mat.HasProperty("_Color"))
        {
            mat.color = color;
        }
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", color);
        }
    }

    // -------------------------------------------------------------------------
    // Atmospheric Eclipse Overlay & "TERASU: RISE OF THE BLOOD MOON" Banner
    // -------------------------------------------------------------------------
    private void OnGUI()
    {
        if (mEclipseScreenAlpha <= 0.01f && mBannerTimer <= 0f && mWhiteFlashAlpha <= 0.01f && mLightningFlashAlpha <= 0.01f)
        {
            return;
        }
        EnsureGuiResources();

        GUI.depth = -20;

        if (mEclipseScreenAlpha > 0.01f)
        {
            // Upper-sky Blood Moon crimson wash + twilight eclipse darken during Stage 1-3 (shattered cleanly at Rebirth!)
            GUI.color = new Color(0.32f, 0.01f, 0.03f, mEclipseScreenAlpha * 0.42f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height * 0.48f), sWhiteTex);

            GUI.color = new Color(0.12f, 0.0f, 0.02f, mEclipseScreenAlpha * 0.34f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), sWhiteTex);

            if (mIsAscending)
            {
                float barH = Screen.height * 0.065f;
                GUI.color = new Color(0.03f, 0.0f, 0.01f, Mathf.Min(0.88f, mEclipseScreenAlpha * 1.45f));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, barH), sWhiteTex);
                GUI.DrawTexture(new Rect(0f, Screen.height - barH, Screen.width, barH), sWhiteTex);
            }
        }

        float combinedFlash = Mathf.Max(mWhiteFlashAlpha, mLightningFlashAlpha * 0.85f);
        if (combinedFlash > 0.01f)
        {
            GUI.color = Color.Lerp(new Color(1.0f, 0.24f, 0.14f, combinedFlash * 0.78f), new Color(1.0f, 0.92f, 0.86f, combinedFlash * 0.82f), mWhiteFlashAlpha * 0.5f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), sWhiteTex);
        }

        if (mBannerTimer > 0f)
        {
            float alpha = Mathf.Clamp01(mBannerTimer < 0.45f ? mBannerTimer / 0.45f : 1f);
            float bw = Mathf.Min(Screen.width * 0.72f, 760f);
            float bh = 76f;
            float bx = (Screen.width - bw) * 0.5f;
            float by = Screen.height * 0.08f;

            GUI.color = new Color(0.06f, 0.01f, 0.02f, 0.88f * alpha);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), sWhiteTex);

            GUI.color = new Color(0.95f, 0.15f, 0.10f, 0.95f * alpha);
            GUI.DrawTexture(new Rect(bx, by, bw, 3f), sWhiteTex);
            GUI.DrawTexture(new Rect(bx, by + bh - 3f, bw, 3f), sWhiteTex);

            GUI.color = Color.white;
            sTagStyle.normal.textColor = new Color(1.0f, 0.36f, 0.26f, alpha);
            sTitleStyle.normal.textColor = new Color(1.0f, 0.90f, 0.86f, alpha);
            sSubtitleStyle.normal.textColor = new Color(0.95f, 0.62f, 0.45f, alpha);

            GUI.Label(new Rect(bx, by + 4f, bw, 18f), "PHASE II ASCENDED BOSS", sTagStyle);
            GUI.Label(new Rect(bx, by + 20f, bw, 32f), "TERASU: RISE OF THE BLOOD MOON", sTitleStyle);
            GUI.Label(new Rect(bx, by + 51f, bw, 20f), "BLOOD-ECLIPSE ONI MONARCH", sSubtitleStyle);
        }

        GUI.color = Color.white;
    }

    private static void EnsureGuiResources()
    {
        if (sWhiteTex == null)
        {
            sWhiteTex = new Texture2D(1, 1);
            sWhiteTex.SetPixel(0, 0, Color.white);
            sWhiteTex.Apply();
        }
        if (sTitleStyle == null)
        {
            sTitleStyle = new GUIStyle(GUI.skin.label);
            sTitleStyle.alignment = TextAnchor.MiddleCenter;
            sTitleStyle.fontSize = 30;
            sTitleStyle.fontStyle = FontStyle.Bold;
        }
        if (sSubtitleStyle == null)
        {
            sSubtitleStyle = new GUIStyle(GUI.skin.label);
            sSubtitleStyle.alignment = TextAnchor.MiddleCenter;
            sSubtitleStyle.fontSize = 15;
            sSubtitleStyle.fontStyle = FontStyle.Bold;
        }
        if (sTagStyle == null)
        {
            sTagStyle = new GUIStyle(GUI.skin.label);
            sTagStyle.alignment = TextAnchor.MiddleCenter;
            sTagStyle.fontSize = 13;
            sTagStyle.fontStyle = FontStyle.Bold;
        }
    }
}
