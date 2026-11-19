using System;
using System.Collections.Generic;
using UnityEngine;

public class IronButcherController : MonoBehaviour
{
    private static readonly string[] kAttackStates = new string[]
    {
        "Double_Combo_Attack",
        "Heavy_Hammer_Swing",
        "Charged_Axe_Chop"
    };

    private static readonly string[] kClipNames = new string[]
    {
        "Combat_Stance",
        "Monster_Walk",
        "Charged_Axe_Chop",
        "Heavy_Hammer_Swing",
        "Double_Combo_Attack",
        "Hit_Reaction",
        "Dead",
        "Zombie_Scream",
        "walking",
        "running"
    };

    public float visualScaleMultiplier = 1.0f;

    [Header("Character Test Lab Manual Overrides")]
    public bool labManualMode = false;
    public string labForcedClip = string.Empty;
    public bool labFreezeClip = false;
    public float labForcedNormalizedTime = 0f;
    public float labAnimSpeedMultiplier = 1.0f;

    private Character mOwner;
    private CharacterModelController mModelCtrl;
    private Transform mVisualRoot;
    private Animator mAnimator;
    private Transform mBoneArmature;
    private Transform mBoneHips;
    private Transform mBoneRightHand;
    private Vector3 mRestHipsLocalPos = Vector3.zero;
    private Vector3 mBaseVisualAbsScale = new Vector3(80f, 80f, 80f);
    private bool mInitialized = false;

    private Vector3 mPrevWorldPos = Vector3.zero;
    private bool mHasPrevPos = false;
    private int mLastHiddenWeaponCount = -1;
    private int mLastChildCount = -1;

    private string mCurrentState = string.Empty;
    private int mAttackIndex = 0;
    private bool mWasAttacking = false;
    private bool mWasDead = false;
    private bool mCleaveFxTriggeredThisSwing = false;
    private float mIdleScreamCooldown = 6.5f;
    private float mScreamPlayTimer = 0f;
    private float mManualActionTimer = 0f;
    private string mManualActionState = string.Empty;

    private Shader mCachedParticleAdditiveShader;
    private Shader mCachedParticleAlphaShader;

    public Transform visualRoot { get { return mVisualRoot; } }
    public Animator butcherAnimator { get { return mAnimator; } }
    public Character ownerCharacter { get { return mOwner; } }
    public string currentVisualState { get { return mCurrentState; } }
    public string[] availableClips { get { return kClipNames; } }

    public static IronButcherController GetOrAttach(GameObject root)
    {
        if (root == null)
        {
            return null;
        }
        IronButcherController existing = root.GetComponent<IronButcherController>();
        if (existing != null)
        {
            existing.EnsureInitialized();
            return existing;
        }
        IronButcherController added = root.AddComponent<IronButcherController>();
        added.EnsureInitialized();
        return added;
    }

    public static IronButcherController GetOrAttach(Character owner)
    {
        if (owner == null || owner.controlledObject == null)
        {
            return null;
        }
        IronButcherController ctrl = GetOrAttach(owner.controlledObject);
        if (ctrl != null)
        {
            ctrl.Bind(owner);
        }
        return ctrl;
    }

    public void Bind(Character owner)
    {
        mOwner = owner;
        EnsureInitialized();
        SuppressLegacyHostVisuals();
    }

    public void PlayManualAction(string stateName, float normalizedTime, bool freeze)
    {
        EnsureInitialized();
        labManualMode = true;
        labForcedClip = stateName != null ? stateName : string.Empty;
        labForcedNormalizedTime = Mathf.Clamp01(normalizedTime);
        labFreezeClip = freeze;
        mManualActionTimer = 0f;
        mManualActionState = string.Empty;

        if (!string.IsNullOrEmpty(labForcedClip))
        {
            mCleaveFxTriggeredThisSwing = false;
            ForceAnimatorStateImmediate(labForcedClip, labForcedNormalizedTime, freeze ? 0f : labAnimSpeedMultiplier);
        }
    }

    public void TriggerManualAttack(int attackVariant)
    {
        EnsureInitialized();
        labManualMode = true;
        labFreezeClip = false;
        labForcedClip = string.Empty;

        int idx;
        if (attackVariant >= 0 && attackVariant < kAttackStates.Length)
        {
            idx = attackVariant;
        }
        else
        {
            idx = mAttackIndex % kAttackStates.Length;
            mAttackIndex++;
        }

        mManualActionState = kAttackStates[idx];
        mManualActionTimer = (idx == 2) ? 1.55f : 1.15f;
        mCleaveFxTriggeredThisSwing = false;
        CrossFadeAnimatorState(mManualActionState, 0.08f, 1.25f * Mathf.Clamp(labAnimSpeedMultiplier, 0.1f, 3.0f));
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();
        SuppressLegacyHostVisuals();
    }

    private void EnsureInitialized()
    {
        if (mInitialized && mVisualRoot != null && mAnimator != null)
        {
            return;
        }

        mModelCtrl = GetComponent<CharacterModelController>();
        if (mVisualRoot == null)
        {
            mVisualRoot = FindChildRecursive(transform, "IronButcher_Visual");
        }
        if (mVisualRoot != null)
        {
            Vector3 s = mVisualRoot.localScale;
            float ax = Mathf.Abs(s.x) > 0.01f ? Mathf.Abs(s.x) : 80f;
            float ay = Mathf.Abs(s.y) > 0.01f ? Mathf.Abs(s.y) : 80f;
            float az = Mathf.Abs(s.z) > 0.01f ? Mathf.Abs(s.z) : 80f;
            mBaseVisualAbsScale = new Vector3(ax, ay, az);

            mAnimator = mVisualRoot.GetComponent<Animator>();
            if (mAnimator != null)
            {
                mAnimator.applyRootMotion = false;
                mAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            mBoneArmature = FindChildRecursive(mVisualRoot, "Armature");
            mBoneHips = FindChildRecursive(mVisualRoot, "Hips");
            if (mBoneHips != null)
            {
                mRestHipsLocalPos = mBoneHips.localPosition;
            }
            mBoneRightHand = FindChildRecursive(mVisualRoot, "RightHand");
            if (mBoneRightHand == null)
            {
                mBoneRightHand = FindChildRecursive(mVisualRoot, "Right_Hand");
            }
        }

        mPrevWorldPos = transform.position;
        mHasPrevPos = true;
        mInitialized = (mVisualRoot != null && mAnimator != null);
        SuppressLegacyHostVisuals();
    }

    private void SuppressLegacyHostVisuals()
    {
        if (mVisualRoot == null)
        {
            return;
        }

        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];
            if (r == null) continue;
            if (r.transform.IsChildOf(mVisualRoot)) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r.gameObject.name.IndexOf("IronButcher_FX", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (r.enabled)
            {
                r.enabled = false;
            }
        }

        if (mModelCtrl != null && mModelCtrl.meleeWeaponInstances != null)
        {
            for (int i = 0; i < mModelCtrl.meleeWeaponInstances.Count; i++)
            {
                GameObject weaponGo = mModelCtrl.meleeWeaponInstances[i];
                if (weaponGo != null && weaponGo.activeSelf)
                {
                    weaponGo.SetActive(false);
                }
            }
            mLastHiddenWeaponCount = mModelCtrl.meleeWeaponInstances.Count;
        }

        mLastChildCount = transform.childCount;
    }

    private void LateUpdate()
    {
        EnsureInitialized();
        if (!mInitialized)
        {
            return;
        }

        int currentWeaponCount = (mModelCtrl != null && mModelCtrl.meleeWeaponInstances != null) ? mModelCtrl.meleeWeaponInstances.Count : 0;
        if (currentWeaponCount != mLastHiddenWeaponCount || transform.childCount != mLastChildCount)
        {
            SuppressLegacyHostVisuals();
        }

        MaintainVisualRootOrientationAndBones();

        float dt = Time.deltaTime;
        float horizSpeed = 0f;
        if (mHasPrevPos && dt > 0.0001f)
        {
            float dz = transform.position.z - mPrevWorldPos.z;
            float dx = transform.position.x - mPrevWorldPos.x;
            horizSpeed = Mathf.Sqrt(dx * dx + dz * dz) / dt;
        }
        mPrevWorldPos = transform.position;
        mHasPrevPos = true;

        UpdateAnimationStateMachine(dt, horizSpeed);
    }

    private void MaintainVisualRootOrientationAndBones()
    {
        if (mVisualRoot == null)
        {
            return;
        }

        // Keep foot base planted cleanly at local origin facing parent's +Z forward axis
        mVisualRoot.localPosition = Vector3.zero;
        mVisualRoot.localRotation = Quaternion.identity;

        // Cancel CharacterModelController's negative-X scale mirror so mesh winding stays positive and cleaver stays in RightHand
        float parentSignX = (transform.localScale.x < 0f) ? -1f : 1f;
        float mul = Mathf.Clamp(visualScaleMultiplier, 0.4f, 2.5f);
        mVisualRoot.localScale = new Vector3(
            mBaseVisualAbsScale.x * mul * parentSignX,
            mBaseVisualAbsScale.y * mul,
            mBaseVisualAbsScale.z * mul
        );

        if (mBoneArmature != null)
        {
            Vector3 ap = mBoneArmature.localPosition;
            if (Mathf.Abs(ap.x) > 0.05f || Mathf.Abs(ap.z) > 0.05f || Mathf.Abs(ap.y) > 0.35f)
            {
                mBoneArmature.localPosition = new Vector3(0f, Mathf.Clamp(ap.y, -0.15f, 0.25f), 0f);
            }
        }

        if (mBoneHips != null)
        {
            Vector3 hp = mBoneHips.localPosition;
            bool isDeadPose = string.Equals(mCurrentState, "Dead", StringComparison.Ordinal);
            float minY = isDeadPose ? -1.05f : -0.20f;
            mBoneHips.localPosition = new Vector3(
                mRestHipsLocalPos.x,
                Mathf.Clamp(hp.y, mRestHipsLocalPos.y + minY, mRestHipsLocalPos.y + 0.35f),
                mRestHipsLocalPos.z
            );
        }
    }

    private void UpdateAnimationStateMachine(float dt, float horizSpeed)
    {
        if (mAnimator == null)
        {
            return;
        }

        float speedScale = Mathf.Clamp(labAnimSpeedMultiplier, 0.1f, 3.0f);
        if (mModelCtrl != null)
        {
            speedScale *= Mathf.Clamp(mModelCtrl.speedModifier, 0.25f, 2.5f);
        }

        // Manual Test Lab clip lock / frame freeze
        if (labManualMode && !string.IsNullOrEmpty(labForcedClip))
        {
            if (labFreezeClip)
            {
                ForceAnimatorStateImmediate(labForcedClip, Mathf.Repeat(labForcedNormalizedTime, 1.0f), 0f);
            }
            else
            {
                mAnimator.speed = 1f;
                if (!string.Equals(mCurrentState, labForcedClip, StringComparison.Ordinal))
                {
                    CrossFadeAnimatorState(labForcedClip, 0.10f, speedScale);
                }
                else
                {
                    SetAnimatorSpeedParam(speedScale);
                    AnimatorStateInfo st = mAnimator.GetCurrentAnimatorStateInfo(0);
                    if (!st.loop && st.normalizedTime >= 0.99f && !mAnimator.IsInTransition(0))
                    {
                        mAnimator.Play(Animator.StringToHash(labForcedClip), 0, 0f);
                    }
                }
                CheckAttackCleaveFxWindow(labForcedClip);
            }
            return;
        }

        if (labManualMode && mManualActionTimer > 0f)
        {
            mManualActionTimer -= dt;
            mAnimator.speed = labFreezeClip ? 0f : 1f;
            SetAnimatorSpeedParam(1.25f * speedScale);
            CheckAttackCleaveFxWindow(mManualActionState);
            if (mManualActionTimer <= 0f)
            {
                mManualActionState = string.Empty;
                mCleaveFxTriggeredThisSwing = false;
            }
            return;
        }

        // Determine SVZ1 character state (Priority: Dead > Hurt > Attack > Walk > Idle / Warcry)
        bool isDead = false;
        bool isHurt = false;
        bool isAttacking = false;
        bool isMoving = horizSpeed > 0.08f;
        string mcAnim = (mModelCtrl != null) ? mModelCtrl.currentAnimation : string.Empty;

        if (mModelCtrl != null)
        {
            if (mModelCtrl.startedDieAnim || IsAnimPrefix(mcAnim, "die"))
            {
                isDead = true;
            }
            if (mModelCtrl.attackActive || IsAnimPrefix(mcAnim, "attack"))
            {
                isAttacking = true;
            }
            if (mModelCtrl.isInHurtState || mModelCtrl.isInKnockback)
            {
                isHurt = true;
            }
            if (mModelCtrl.isMoving)
            {
                isMoving = true;
            }
        }
        if (mOwner != null && mOwner.health <= 0f)
        {
            isDead = true;
        }

        mAnimator.speed = labFreezeClip ? 0f : 1f;

        if (isDead)
        {
            if (!mWasDead)
            {
                mWasDead = true;
                mWasAttacking = false;
                CrossFadeAnimatorState("Dead", 0.12f, 1.0f);
            }
            else
            {
                SetAnimatorSpeedParam(1.0f);
            }
            return;
        }
        mWasDead = false;

        if (isHurt && !isAttacking)
        {
            mWasAttacking = false;
            mScreamPlayTimer = 0f;
            if (!string.Equals(mCurrentState, "Hit_Reaction", StringComparison.Ordinal))
            {
                CrossFadeAnimatorState("Hit_Reaction", 0.12f, 1.05f * speedScale);
            }
            else
            {
                SetAnimatorSpeedParam(1.05f * speedScale);
            }
            return;
        }

        if (isAttacking)
        {
            mScreamPlayTimer = 0f;
            if (!mWasAttacking)
            {
                mWasAttacking = true;
                mCleaveFxTriggeredThisSwing = false;
                string atkClip = kAttackStates[mAttackIndex % kAttackStates.Length];
                mAttackIndex++;
                CrossFadeAnimatorState(atkClip, 0.08f, 1.25f * speedScale);
            }
            else
            {
                SetAnimatorSpeedParam(1.25f * speedScale);
            }
            CheckAttackCleaveFxWindow(mCurrentState);
            return;
        }
        mWasAttacking = false;
        mCleaveFxTriggeredThisSwing = false;

        if (isMoving)
        {
            mScreamPlayTimer = 0f;
            float walkSpeed = (horizSpeed > 5f) ? Mathf.Clamp(horizSpeed / 80f, 0.85f, 1.45f) : 1.05f;
            if (!string.Equals(mCurrentState, "Monster_Walk", StringComparison.Ordinal))
            {
                CrossFadeAnimatorState("Monster_Walk", 0.15f, walkSpeed * speedScale);
            }
            else
            {
                SetAnimatorSpeedParam(walkSpeed * speedScale);
            }
            return;
        }

        // Idle + periodic Zombie Scream warcry when standing idle
        if (mScreamPlayTimer > 0f)
        {
            mScreamPlayTimer -= dt;
            if (!string.Equals(mCurrentState, "Zombie_Scream", StringComparison.Ordinal))
            {
                CrossFadeAnimatorState("Zombie_Scream", 0.18f, 1.0f * speedScale);
            }
            else
            {
                SetAnimatorSpeedParam(1.0f * speedScale);
            }
            return;
        }

        if (!labManualMode)
        {
            mIdleScreamCooldown -= dt;
            if (mIdleScreamCooldown <= 0f)
            {
                mIdleScreamCooldown = 9.0f;
                mScreamPlayTimer = 1.65f;
                CrossFadeAnimatorState("Zombie_Scream", 0.18f, 1.0f * speedScale);
                return;
            }
        }

        if (!string.Equals(mCurrentState, "Combat_Stance", StringComparison.Ordinal))
        {
            CrossFadeAnimatorState("Combat_Stance", 0.20f, 1.0f * speedScale);
        }
        else
        {
            SetAnimatorSpeedParam(1.0f * speedScale);
        }
    }

    private void CheckAttackCleaveFxWindow(string activeState)
    {
        if (mAnimator == null || mCleaveFxTriggeredThisSwing)
        {
            return;
        }
        bool isAttackClip = false;
        for (int i = 0; i < kAttackStates.Length; i++)
        {
            if (string.Equals(activeState, kAttackStates[i], StringComparison.Ordinal))
            {
                isAttackClip = true;
                break;
            }
        }
        if (!isAttackClip)
        {
            return;
        }

        AnimatorStateInfo st = mAnimator.GetCurrentAnimatorStateInfo(0);
        int targetHash = Animator.StringToHash(activeState);
        if (st.shortNameHash == targetHash && Mathf.Repeat(st.normalizedTime, 1.0f) >= 0.42f)
        {
            mCleaveFxTriggeredThisSwing = true;
            SpawnCleaveImpactFX();
        }
    }

    private void CrossFadeAnimatorState(string stateName, float fadeDuration, float animSpeed)
    {
        if (mAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }
        mCurrentState = stateName;
        SetAnimatorSpeedParam(animSpeed);
        mAnimator.CrossFadeInFixedTime(Animator.StringToHash(stateName), fadeDuration, 0, 0f);
    }

    private void ForceAnimatorStateImmediate(string stateName, float normalizedTime, float speed)
    {
        if (mAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }
        mCurrentState = stateName;
        mAnimator.speed = speed;
        SetAnimatorSpeedParam(Mathf.Max(0.1f, Mathf.Abs(speed)));
        mAnimator.Play(Animator.StringToHash(stateName), 0, normalizedTime);
        mAnimator.Update(0f);
    }

    private void SetAnimatorSpeedParam(float speedVal)
    {
        if (mAnimator == null)
        {
            return;
        }
        mAnimator.SetFloat("AnimSpeed", Mathf.Clamp(speedVal, 0.05f, 4.0f));
    }

    private void SpawnCleaveImpactFX()
    {
        Vector3 fwd = transform.forward;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        Vector3 origin = (mBoneRightHand != null)
            ? (mBoneRightHand.position + fwd * 36f)
            : (transform.position + new Vector3(0f, 52f, 0f) + fwd * 68f);
        origin.y = Mathf.Max(transform.position.y + 34f, origin.y);

        // 1. Metallic Amber Spark Burst
        CreateImpactBurst(
            "IronButcher_FX_Sparks",
            origin,
            new Color(1.0f, 0.72f, 0.22f, 0.95f),
            new Color(1.0f, 0.25f, 0.05f, 0.0f),
            18,
            7.5f,
            180f,
            true
        );

        // 2. Crimson Blood Cleave Arc
        CreateImpactBurst(
            "IronButcher_FX_Cleave",
            origin,
            new Color(0.75f, 0.06f, 0.06f, 0.90f),
            new Color(0.28f, 0.01f, 0.02f, 0.0f),
            14,
            13.5f,
            115f,
            false
        );

        if (mModelCtrl != null)
        {
            mModelCtrl.PlaySoundEvent("MeleeHit");
        }
    }

    private void CreateImpactBurst(string goName, Vector3 worldPos, Color startCol, Color endCol, int burstCount, float startSize, float startSpeed, bool additive)
    {
        GameObject fx = new GameObject(goName);
        fx.transform.position = worldPos;
        fx.transform.rotation = transform.rotation;

        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.6f;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed * 0.45f, startSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.65f, startSize * 1.25f);
        main.gravityModifier = additive ? 0.45f : 0.75f;
        main.startColor = startCol;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 12f;

        ParticleSystem.ColorOverLifetimeModule colMod = ps.colorOverLifetime;
        colMod.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(startCol, 0f), new GradientColorKey(endCol, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(startCol.a, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colMod.color = new ParticleSystem.MinMaxGradient(grad);

        ParticleSystem.SizeOverLifetimeModule szMod = ps.sizeOverLifetime;
        szMod.enabled = true;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 1.0f),
            new Keyframe(1f, 0.12f)
        );
        szMod.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        ParticleSystemRenderer pr = fx.GetComponent<ParticleSystemRenderer>();
        if (pr != null)
        {
            Shader sh = additive ? mCachedParticleAdditiveShader : mCachedParticleAlphaShader;
            if (sh == null)
            {
                sh = Shader.Find(additive ? "Particles/Additive" : "Particles/Alpha Blended");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                if (additive) mCachedParticleAdditiveShader = sh;
                else mCachedParticleAlphaShader = sh;
            }
            if (sh != null)
            {
                Material m = new Material(sh);
                m.color = startCol;
                pr.material = m;
            }
        }

        ps.Play();
        Destroy(fx, 1.15f);
    }

    private static bool IsAnimPrefix(string animName, string prefix)
    {
        if (string.IsNullOrEmpty(animName) || string.IsNullOrEmpty(prefix))
        {
            return false;
        }
        return animName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindChildRecursive(Transform root, string exactName)
    {
        if (root == null) return null;
        if (string.Equals(root.name, exactName, StringComparison.Ordinal)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), exactName);
            if (found != null) return found;
        }
        return null;
    }
}
