using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 💀 Opponent Hero Duelist for Samurai vs Zombies Defense 1v1 PvP
/// Controls the opposing player's Champion (Zombie Hero PFZombieHero or Samurai Hero PFHero01).
/// Features:
/// - Fixed 4500 HP Duelist Boss
/// - Dual Katana Melee Slash with Life Steal FX (dual-wielding Props/PFZombieHeroSword01 & 02)
/// - Ranged Bow attacks with Bone Arrows
/// - Anti-Spam Attack Rhythm: clean 1.0s sword cadence, no animation twitching
/// - Smart Boss AI:
///   * Senses incoming Troop Trample (GiantWave) and proactively activates Shadow Barrier & Dodge!
///   * Caps single-hit super ability damage (max 400-500 HP), preventing one-shot cheese
///   * Summons 2 Elite Zombie Spear Guards at 50% HP to hold frontline
///   * Tactical Backpedal / Kiting when low HP (< 30%) or after melee combos
///   * Periodic KatanaSlash whirlwind special with Life Steal
/// - Match-deciding hero duel elimination
/// </summary>
public class OpponentHero : Enemy
{
    public static OpponentHero ActiveOpponentHero { get; private set; }

    private bool mHasAwardedSlay = false;
    private bool mIsRetreating = false;
    private float mRetreatTimer = 0f;
    private float mDodgeCooldown = 0f;
    private float mAttackCooldown = 0f; // Eliminates animation spam
    private float mSpecialCooldown = 6f; // Cooldown for Katana Cleave special
    private float mShadowShieldTimer = 0f; // Active damage reduction barrier
    private float mTrampleEvadeCooldown = 0f;
    private float mOutOfCombatRegenTimer = 0f; // Out-of-combat health regeneration delay (like P1 Hero)
    private bool mHasSummonedGuards = false;
    private bool mHasUsedLastStand = false;
    private float mSummonVanguardCooldown = 6.0f;
    private float mClientInputOverrideTimer = 0f;
    private int mMeleeComboHits = 0;
    private float mLifeStealRatio = 0.25f;
    private GameObject mLifeStealFX;
    private bool mFacingLeft = true;
    private bool mIsZombieHero = true;
    private string mDisplayName = "Zombi Kahramanı";

    public string DisplayName { get { return mDisplayName; } }
    public bool IsDead { get { return health <= 0f; } }
    public bool IsShieldActive { get { return mShadowShieldTimer > 0f; } }

    public OpponentHero(string modelPath, float zTarget, Vector3 spawnPos, bool isFacingLeft, float hpMultiplier = 1.0f)
        : base(modelPath, zTarget, spawnPos)
    {
        ActiveOpponentHero = this;
        mFacingLeft = isFacingLeft;

        bool isZombie = modelPath.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0;
        mIsZombieHero = isZombie;
        mDisplayName = isZombie ? "Zombi Kahramanı (Zombie Hero)" : "Samuray Şampiyonu (Samurai Hero)";

        if (base.controller != null)
        {
            base.controller.nativeFacing = CharacterModelController.GetModelNativeFacing(modelPath);
            base.controller.StopWalking();
            base.controller.facing = isFacingLeft ? FacingType.Left : FacingType.Right;
            base.controller.position = spawnPos;
            base.controller.constraintLeft = Mathf.Min(spawnPos.z, zTarget);
            base.controller.constraintRight = Mathf.Max(spawnPos.z, zTarget);
        }

        // Attach Level 8 Dual Swords for Zombie Hero or Level 8 Katana for Samurai Hero
        List<GameObject> swords = new List<GameObject>();
        if (isZombie)
        {
            GameObject s1 = Resources.Load("Props/PFZombieHeroSwordUpgrade01") as GameObject;
            GameObject s2 = Resources.Load("Props/PFZombieHeroSwordUpgrade02") as GameObject;
            if (s1 == null) s1 = Resources.Load("Props/PFZombieHeroSword01") as GameObject;
            if (s2 == null) s2 = Resources.Load("Props/PFZombieHeroSword02") as GameObject;
            if (s1 != null) swords.Add(s1);
            if (s2 != null) swords.Add(s2);
        }
        else
        {
            GameObject s1 = Resources.Load("Props/PFKatanaUpgrade3") as GameObject;
            if (s1 == null) s1 = Resources.Load("Props/PFHeroSword01") as GameObject;
            if (s1 != null) swords.Add(s1);
        }
        base.meleeWeaponPrefabAsList = swords;

        // Attach Bow
        string bowPath = isZombie ? "Props/PFZombieHeroBow" : "Props/PFHeroBow";
        GameObject bowObj = Resources.Load(bowPath) as GameObject;
        if (bowObj != null)
        {
            base.rangedWeaponPrefab = bowObj;
        }

        mLifeStealFX = null;

        // Configure Fixed 4000 HP Champion Stats with Balanced Sword (50) & Bow (40)
        CharacterStats stats = default(CharacterStats);
        stats.uniqueID = "OpponentHero";
        stats.isEnemy = true;
        stats.isPlayer = false;
        stats.isBoss = true;
        stats.isUnique = true;
        stats.knockbackResistance = 100;
        stats.maxHealth = 4000f;
        stats.health = 4000f;
        stats.autoHealthRecovery = 0f; // Managed via mOutOfCombatRegenTimer out-of-combat check (85 HP/s)
        stats.meleeAttackDamage = 50f; // Balanced Katana damage
        stats.meleeAttackRange = 145f;  // Katana reach
        stats.meleeAttackFrequency = 1.0f; // Katana speed
        stats.meleeWeaponIsABlade = true;
        stats.bowAttackDamage = 40f;    // Balanced Bow damage
        stats.bowAttackRange = 400f;    // Bow reach
        stats.bowAttackFrequency = 1.5f; // Bow frequency
        stats.projectile = isZombie ? Projectile.EProjectileType.BoneArrowUpgraded : Projectile.EProjectileType.HeroBigExplodingArrow;
        stats.speed = 300f; // Agile champion speed matching Player 1 Hero exactly
        base.stats = stats;

        base.maxHealth = 4000f;
        base.health = 4000f;

        SetRangeAttackMode(false);
    }

    public override void Update()
    {
        if (health <= 0f)
        {
            if (!mHasAwardedSlay)
            {
                mHasAwardedSlay = true;
                if (SVZMultiplayerManager.Instance != null)
                {
                    SVZMultiplayerManager.Instance.OnOpponentHeroKilled(this);
                }
            }
            base.Update();
            return;
        }

        // Keep 4000 HP max synchronized
        if (maxHealth != 4000f)
        {
            maxHealth = 4000f;
        }

        // Out-of-Combat Health Regeneration (Just like Player 1's Hero: recovers HP when not taking damage for 2.5s)
        if (mOutOfCombatRegenTimer > 0f)
        {
            mOutOfCombatRegenTimer -= Time.deltaTime;
        }
        else if (health > 0f && health < maxHealth)
        {
            health = Mathf.Min(maxHealth, health + 85f * Time.deltaTime);
        }

        // Timers
        if (mAttackCooldown > 0f) mAttackCooldown -= Time.deltaTime;
        if (mDodgeCooldown > 0f) mDodgeCooldown -= Time.deltaTime;
        if (mSpecialCooldown > 0f) mSpecialCooldown -= Time.deltaTime;
        if (mShadowShieldTimer > 0f) mShadowShieldTimer -= Time.deltaTime;
        if (mTrampleEvadeCooldown > 0f) mTrampleEvadeCooldown -= Time.deltaTime;
        if (mSummonVanguardCooldown > 0f) mSummonVanguardCooldown -= Time.deltaTime;

        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        SVZNetworkManager net = SVZNetworkManager.Instance;
        bool isOnline = (mpMgr != null && !mpMgr.IsSamePC && net != null && net.IsConnected);
        bool isOnlineClient = (isOnline && !net.IsHost);
        bool isHotseatP2 = (mpMgr != null && mpMgr.IsSamePC && !mpMgr.P2AutoBot);

        if (mClientInputOverrideTimer > 0f)
        {
            mClientInputOverrideTimer -= Time.deltaTime;
        }

        if (isOnlineClient)
        {
            UpdateOnlineClientInput();
        }
        else if (isHotseatP2)
        {
            UpdateManualInput();
        }
        else
        {
            // On Host or Solo Bot: if client recently provided input, defer to client command
            if (mClientInputOverrideTimer <= 0f)
            {
                UpdateDuelistAI();
            }
        }

        base.Update();
    }

    public override void RecievedAttack(EAttackType attackType, float damage)
    {
        if (health <= 0f) return;

        // Reset out-of-combat health regeneration delay whenever taking damage (same as P1 Hero)
        if (damage > 0f)
        {
            mOutOfCombatRegenTimer = 2.5f;
        }

        // 1. SMART DEFENSE AGAINST TROOP TRAMPLE (GiantWave Cavalry Rush)
        if (attackType == EAttackType.Trample)
        {
            if (mShadowShieldTimer > 0f)
            {
                // Shadow Barrier Active: absorbs 85% of trample damage
                damage = Mathf.Min(damage * 0.15f, 120f);
                try { SVZMultiplayerManager.PlaySFX("Hero_Samurai_Sword_Block_01", 1.0f); } catch { }
            }
            else
            {
                // Passive Boss Resilience: caps damage to 320 (never one-shot from 4500 HP!)
                damage = Mathf.Min(damage, 320f);
                ActivateShadowShield(2.2f);
                TriggerDodgeBackpedal(0.95f);
            }
        }
        else if (attackType == EAttackType.Arrow || attackType == EAttackType.FireArrow)
        {
            // Arrow Deflection with Katana (35% chance to deflect incoming projectile)
            if (UnityEngine.Random.value < 0.35f)
            {
                damage *= 0.25f;
                try { SVZMultiplayerManager.PlaySFX("Hero_Samurai_Sword_Block_01", 1.0f); } catch { }
            }
        }
        else
        {
            if (mShadowShieldTimer > 0f)
            {
                damage *= 0.5f;
            }
            // 2. Cap extreme single-hit abilities (e.g. 5000+ damage) to max 340
            damage = Mathf.Min(damage, 340f);
        }

        base.RecievedAttack(attackType, damage);
        if (base.controller != null)
        {
            base.controller.impactPauseTime = 0f; // Champion has boss poise; cannot be flinch-locked by basic attacks
        }
    }

    public bool IsActionBusy
    {
        get
        {
            if (base.controller == null) return false;
            return base.controller.isAnimationPlaying || (base.controller.animPlayer != null && base.controller.animPlayer.actionAnimState != null);
        }
    }

    public void PerformHeroMeleeAttack()
    {
        if (base.controller == null || health <= 0f) return;
        bool allowBack = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.AllowBackAttack);
        if (!allowBack)
        {
            base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
        }
        SetRangeAttackMode(false);
        mAttackCooldown = 0.88f;
        StartMeleeAttackDelayTimer();
        base.controller.Attack(base.meleeAttackFrequency);
        mMeleeComboHits++;
        PlayKatanaSlashSFX();

        DeliverMeleeBlow();

        if (mSpecialCooldown <= 0f && (mMeleeComboHits >= 2 || (base.singleAttackTarget != null && base.singleAttackTarget.isPlayer) || UnityEngine.Random.value < 0.4f))
        {
            mSpecialCooldown = 4.5f;
            mMeleeComboHits = 0;
            OnSpecialSlashDelivery();
        }
    }

    private void DeliverMeleeBlow()
    {
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null || base.controller == null) return;

        float myZ = base.controller.position.z;
        float reach = base.meleeAttackRange * 1.3f;
        bool facingLeft = (base.controller.facing == FacingType.Left);
        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c.health <= 0f || c.isBase || c.isEnemy == base.isEnemy || c.isFlying) continue;

            bool inFront = facingLeft
                ? (c.position.z <= myZ && (myZ - c.position.z) <= reach)
                : (c.position.z >= myZ && (c.position.z - myZ) <= reach);
            if (inFront)
            {
                c.RecievedAttack(EAttackType.Blade, base.meleeDamage);
                if (c.controller != null && c.controller.bladeImpactEffect != null)
                {
                    c.controller.SpawnEffectAtJoint(c.controller.bladeImpactEffect, "impact_target", false);
                }
            }
        }
    }

    public void ActivateShadowShield(float duration)
    {
        mShadowShieldTimer = duration;
        MaterialColorFadeInOut(new Color(0.7f, 0.15f, 0.95f), 0.2f, duration, 0.3f);
        try { SVZMultiplayerManager.PlaySFX("Hero_Samurai_Sword_Block_01", 1.0f); } catch { }
    }

    public void TriggerDodgeBackpedal(float duration)
    {
        mIsRetreating = true;
        mRetreatTimer = duration;
        mDodgeCooldown = 9.0f;
        if (base.controller != null)
        {
            base.controller.speed = 300f; // Quick tactical step-back
            if (mFacingLeft) base.controller.StartWalkRight();
            else base.controller.StartWalkLeft();
        }
    }

    public void RegisterClientActivity()
    {
        mClientInputOverrideTimer = 1.2f;
    }

    public void ExecuteNetworkCommand(string cmd)
    {
        if (base.controller == null) return;
        bool allowBack = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.AllowBackAttack);

        switch (cmd)
        {
            case "MOVE_LEFT":
                base.controller.facing = FacingType.Left;
                base.controller.speed = 300f;
                base.controller.StartWalkLeft();
                break;

            case "MOVE_RIGHT":
                base.controller.facing = FacingType.Right;
                base.controller.speed = 300f;
                base.controller.StartWalkRight();
                break;

            case "STOP":
                base.controller.Idle();
                if (!allowBack)
                {
                    base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
                }
                break;

            case "MELEE":
                if (!IsActionBusy && mAttackCooldown <= 0f)
                {
                    if (!allowBack)
                    {
                        base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
                    }
                    PerformHeroMeleeAttack();
                }
                break;

            case "BOW":
                if (!allowBack)
                {
                    base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
                }
                if (!IsActionBusy && mAttackCooldown <= 0f && IsInBowRangeOfOpponent())
                {
                    SetRangeAttackMode(true);
                    mAttackCooldown = 1.35f;
                    StartRangedAttackDelayTimer();
                    base.singleAttackTarget = WeakGlobalInstance<CharactersManager>.instance.GetBestRangedAttackTarget(this, base.bowAttackExtGameRange);
                    if (base.singleAttackTarget != null)
                    {
                        base.controller.RangeAttack(base.bowAttackFrequency);
                    }
                }
                break;
        }
    }

    private void UpdateOnlineClientInput()
    {
        if (base.controller == null) return;
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (net == null || !net.IsConnected) return;

        if (IsActionBusy || mAttackCooldown > 0f) return;

        bool moveLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        bool moveRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        bool meleeKey = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.RightControl);
        bool bowKey = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.RightShift);

        if (meleeKey)
        {
            ExecuteNetworkCommand("MELEE");
            net.SendPacket("P2_HERO_CMD", "MELEE");
        }
        else if (bowKey && IsInBowRangeOfOpponent())
        {
            ExecuteNetworkCommand("BOW");
            net.SendPacket("P2_HERO_CMD", "BOW");
        }
        else if (moveLeft)
        {
            if (base.controller.facing != FacingType.Left || !base.controller.isMoving)
            {
                ExecuteNetworkCommand("MOVE_LEFT");
                net.SendPacket("P2_HERO_CMD", "MOVE_LEFT");
            }
        }
        else if (moveRight)
        {
            if (base.controller.facing != FacingType.Right || !base.controller.isMoving)
            {
                ExecuteNetworkCommand("MOVE_RIGHT");
                net.SendPacket("P2_HERO_CMD", "MOVE_RIGHT");
            }
        }
        else
        {
            if (base.controller.isMoving)
            {
                ExecuteNetworkCommand("STOP");
                net.SendPacket("P2_HERO_CMD", "STOP");
            }
        }
    }

    private void UpdateManualInput()
    {
        if (base.controller == null) return;

        if (IsActionBusy || mAttackCooldown > 0f) return;

        bool moveLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Keypad4);
        bool moveRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Keypad6);
        bool meleeKey = Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool bowKey = Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.Keypad2);

        if (meleeKey)
        {
            PerformHeroMeleeAttack();
        }
        else if (bowKey && IsInBowRangeOfOpponent())
        {
            SetRangeAttackMode(true);
            mAttackCooldown = 1.35f;
            StartRangedAttackDelayTimer();
            base.singleAttackTarget = WeakGlobalInstance<CharactersManager>.instance.GetBestRangedAttackTarget(this, base.bowAttackExtGameRange);
            if (base.singleAttackTarget != null)
            {
                base.controller.RangeAttack(base.bowAttackFrequency);
            }
        }
        else if (moveLeft)
        {
            base.controller.facing = FacingType.Left;
            base.controller.StartWalkLeft();
        }
        else if (moveRight)
        {
            base.controller.facing = FacingType.Right;
            base.controller.StartWalkRight();
        }
        else if (!IsInMeleeRangeOfOpponent(false) && !IsInBowRangeOfOpponent())
        {
            base.controller.Idle();
        }
    }

    private Character FindForemostAlliedTroop()
    {
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return null;

        Character foremost = null;
        float bestZ = mFacingLeft ? float.MaxValue : float.MinValue;

        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c == this || c.health <= 0f || c.isBase || c.isFlying) continue;
            if (c.isEnemy != base.isEnemy) continue; // Only allied troops on our side!

            if (mFacingLeft)
            {
                // Moving Right to Left (-z). Foremost troop has smallest z (closest to enemy gate)
                if (c.position.z < bestZ)
                {
                    bestZ = c.position.z;
                    foremost = c;
                }
            }
            else
            {
                // Moving Left to Right (+z). Foremost troop has largest z
                if (c.position.z > bestZ)
                {
                    bestZ = c.position.z;
                    foremost = c;
                }
            }
        }

        return foremost;
    }

    private int CountAlliedGroundTroops()
    {
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return 0;
        int count = 0;
        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c != null && c != this && c.health > 0f && !c.isBase && !c.isFlying && c.isEnemy == base.isEnemy)
            {
                count++;
            }
        }
        return count;
    }

    private void CountArmyCounts(out int hostileArmyCount, out int alliedArmyCount)
    {
        hostileArmyCount = 0;
        alliedArmyCount = 0;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return;

        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c.health <= 0f || c.isBase) continue;
            if (c.isEnemy == base.isEnemy)
            {
                if (c != this) alliedArmyCount++;
            }
            else
            {
                if (!c.isPlayer) hostileArmyCount++;
            }
        }
    }

    private bool HasAlliedVanguardTank(float myZ, out Character vanguardTank)
    {
        vanguardTank = null;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return false;

        float bestFrontZ = mFacingLeft ? float.MaxValue : float.MinValue;
        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c == this || c.health <= 0f || c.isBase || c.isFlying || c.isEnemy != base.isEnemy) continue;

            string uid = !string.IsNullOrEmpty(c.uniqueID) ? c.uniqueID : ((c.controlledObject != null) ? c.controlledObject.name : string.Empty);
            SVZMultiplayerManager.UnitTacticalRole role = !string.IsNullOrEmpty(uid) ? SVZMultiplayerManager.GetUnitTacticalRole(uid) : SVZMultiplayerManager.UnitTacticalRole.SwarmScout;
            SVZMultiplayerManager.UnitTier tier = !string.IsNullOrEmpty(uid) ? SVZMultiplayerManager.GetUnitTier(uid) : SVZMultiplayerManager.UnitTier.Core;

            bool isTankOrSuper = c.isBoss || c.maxHealth >= 220f ||
                                 tier != SVZMultiplayerManager.UnitTier.Core ||
                                 role == SVZMultiplayerManager.UnitTacticalRole.TankShield ||
                                 role == SVZMultiplayerManager.UnitTacticalRole.ApexBoss ||
                                 role == SVZMultiplayerManager.UnitTacticalRole.ArmorBreaker ||
                                 role == SVZMultiplayerManager.UnitTacticalRole.AntiCavalrySpear;
            if (!isTankOrSuper) continue;

            bool inFrontOrScreen = mFacingLeft ? (c.position.z <= myZ + 55f) : (c.position.z >= myZ - 55f);
            if (!inFrontOrScreen) continue;

            if (mFacingLeft ? (c.position.z < bestFrontZ) : (c.position.z > bestFrontZ))
            {
                bestFrontZ = c.position.z;
                vanguardTank = c;
            }
        }
        return vanguardTank != null;
    }

    private Character FindPriorityHeroTarget(float myZ, bool allowBack, out float heroDist, out bool heroFleeingOrRanged)
    {
        heroDist = 99999f;
        heroFleeingOrRanged = false;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null) return null;

        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c.health <= 0f || c.isBase || !c.isPlayer || c.isEnemy == base.isEnemy) continue;

            bool isBehindMe = mFacingLeft ? (c.position.z > myZ + 12f) : (c.position.z < myZ - 12f);
            if (!allowBack && isBehindMe) continue;

            heroDist = Mathf.Abs(c.position.z - myZ);
            bool isLowHp = (c.maxHealth > 0f && c.health < c.maxHealth * 0.45f);
            bool isRangedStance = false;
            bool isRetreating = false;

            if (c.controller != null)
            {
                string curAnim = c.controller.currentAnimation ?? string.Empty;
                string baseAnim = (c.controller.animPlayer != null && c.controller.animPlayer.baseAnim != null) ? c.controller.animPlayer.baseAnim : string.Empty;
                isRangedStance = (curAnim == "rangedattack" || curAnim == "rangedattackidle" || baseAnim == "rangedattackidle");
                bool isBackpedaling = (curAnim == "backpedal" || curAnim == "runback" || curAnim == "backpedalturn" || baseAnim == "backpedal" || baseAnim == "runback");
                bool isMovingAway = c.controller.isMoving && ((mFacingLeft && c.controller.facing == FacingType.Left) || (!mFacingLeft && c.controller.facing == FacingType.Right));
                isRetreating = isBackpedaling || isMovingAway;
            }

            heroFleeingOrRanged = isLowHp || isRangedStance || isRetreating;
            return c;
        }
        return null;
    }

    private Character FindNearestEnemy(out float nearestDist, out int enemiesInMeleeCount)
    {
        nearestDist = 99999f;
        enemiesInMeleeCount = 0;
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || charMgr.allAlive == null || base.controller == null) return null;

        bool allowBack = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.AllowBackAttack);
        float myZ = base.controller.position.z;
        Character bestTarget = null;
        float bestScore = 99999f;

        List<Character> alive = charMgr.allAlive;
        for (int i = 0; i < alive.Count; i++)
        {
            Character c = alive[i];
            if (c == null || c.health <= 0f || c.isBase || c.isEnemy == base.isEnemy) continue;

            float d = Mathf.Abs(c.position.z - myZ);
            if (!c.isFlying && d <= 155f) enemiesInMeleeCount++;

            // Check if the enemy (or Player's Hero) passed BEHIND OpponentHero toward P2's Gate!
            bool isBehindMe = mFacingLeft ? (c.position.z > myZ) : (c.position.z < myZ);
            if (!allowBack && isBehindMe)
            {
                continue;
            }

            // Effective priority distance: strongly prioritize backstabbers/flankers (if allowBack) and the enemy Hero!
            float score = d;
            if (allowBack && isBehindMe && d <= 520f)
            {
                score *= 0.45f; // Immediately turn around to punish backdooring units/Hero!
            }
            if (c.isPlayer && d <= 360f)
            {
                score *= 0.75f; // Focus the enemy Hero in duels!
            }

            if (score < bestScore)
            {
                bestScore = score;
                nearestDist = d;
                bestTarget = c;
            }
        }
        return bestTarget;
    }

    private void FaceTargetZ(float targetZ)
    {
        if (base.controller == null) return;
        bool allowBack = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.AllowBackAttack);
        if (!allowBack)
        {
            base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
            return;
        }
        float myZ = base.controller.position.z;
        if (Mathf.Abs(targetZ - myZ) > 4f)
        {
            base.controller.facing = (targetZ <= myZ) ? FacingType.Left : FacingType.Right;
        }
    }

    private void MoveTowardZ(float targetZ, float moveSpeed)
    {
        if (base.controller == null) return;
        base.controller.speed = moveSpeed;
        if (!base.canMove)
        {
            base.controller.Idle();
            return;
        }
        float myZ = base.controller.position.z;
        if (targetZ <= myZ)
        {
            base.controller.facing = FacingType.Left;
            base.controller.StartWalkLeft();
        }
        else
        {
            base.controller.facing = FacingType.Right;
            base.controller.StartWalkRight();
        }
    }

    private void UpdateDuelistAI()
    {
        if (base.controller == null) return;

        float myZ = base.controller.position.z;

        // 0. SENSE INCOMING THREATS (Troop Trample / GiantWave)
        GiantWave incomingWave = UnityEngine.Object.FindObjectOfType<GiantWave>();
        if (incomingWave != null && mTrampleEvadeCooldown <= 0f)
        {
            float dist = Mathf.Abs(incomingWave.transform.position.z - myZ);
            if (dist < 700f)
            {
                mTrampleEvadeCooldown = 7.0f;
                ActivateShadowShield(2.8f);
                TriggerDodgeBackpedal(0.95f);
                SVZMultiplayerManager.AddChatMessage("[ZOMBİ KAHRAMANI]", "💀 'Süvari hücumu mu?! Gölge Kalkanı ile savuşturuyorum!'");
                return;
            }
        }

        // 1. SHADOW SHIELD AT 50% HP (No free unit spawns)
        if (health <= maxHealth * 0.50f && !mHasSummonedGuards)
        {
            mHasSummonedGuards = true;
            ActivateShadowShield(2.8f);
        }

        // 2. BERSERK LAST STAND AT 25% HP (No free unit spawns)
        if (health <= maxHealth * 0.25f && !mHasUsedLastStand)
        {
            mHasUsedLastStand = true;
            ActivateShadowShield(3.5f);
        }

        // 3. BRIEF TACTICAL STEP-BACK (Only active for <1s when evading Trample or critically surrounded alone)
        if (mIsRetreating)
        {
            mRetreatTimer -= Time.deltaTime;
            if (mRetreatTimer <= 0f)
            {
                mIsRetreating = false;
                if (base.controller != null) base.controller.speed = 300f;
            }
            else
            {
                if (mFacingLeft) base.controller.StartWalkRight();
                else base.controller.StartWalkLeft();
                return;
            }
        }

        // 4. CHECK IF HERO IS ALREADY IN THE MIDDLE OF AN ATTACK (Anti-Spam Guard)
        if (IsActionBusy || mAttackCooldown > 0f)
        {
            return;
        }

        float nearestEnemyDist;
        int enemiesInMelee;
        Character nearestEnemy = FindNearestEnemy(out nearestEnemyDist, out enemiesInMelee);
        int aliveTroops = CountAlliedGroundTroops();
        int p1Count, p2Count;
        CountArmyCounts(out p1Count, out p2Count);

        InGameImpl ig = WeakGlobalSceneBehavior<InGameImpl>.instance;
        float ownGateZ = base.isEnemy
            ? ((ig != null && ig.enemyGate != null) ? ig.enemyGate.position.z : (mFacingLeft ? 900f : -900f))
            : ((ig != null && ig.gate != null) ? ig.gate.position.z : (mFacingLeft ? 900f : -900f));
        float oppGateZ = base.isEnemy
            ? ((ig != null && ig.gate != null) ? ig.gate.position.z : (mFacingLeft ? -500f : 500f))
            : ((ig != null && ig.enemyGate != null) ? ig.enemyGate.position.z : (mFacingLeft ? -500f : 500f));
        bool nearOwnGate = Mathf.Abs(myZ - ownGateZ) <= 190f;

        // Dynamically turn toward the priority enemy (when AllowBackAttack is true; otherwise face forward)
        if (nearestEnemy != null)
        {
            FaceTargetZ(nearestEnemy.position.z);
        }
        else
        {
            base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
        }

        // 5. ACTIVE SMART SPELL CASTING IN COMBAT
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        if (mpMgr != null && mpMgr.IsActive && mpMgr.P2Spells != null)
        {
            float hpPct = (maxHealth > 0f) ? (health / maxHealth) : 1.0f;
            bool gateThreat = (nearestEnemy != null && Mathf.Abs(nearestEnemy.position.z - ownGateZ) < 300f);

            for (int slot = 0; slot < 2; slot++)
            {
                if (slot < mpMgr.P2Spells.Count && mpMgr.IsP2SpellAvailable(slot))
                {
                    string spId = mpMgr.P2Spells[slot];
                    if (SVZMultiplayerManager.ShouldBotCastSpell(spId, nearestEnemy, nearestEnemyDist, enemiesInMelee, p1Count, p2Count, hpPct, gateThreat))
                    {
                        mpMgr.TriggerPlayer2Spell(slot);
                        mAttackCooldown = 0.60f;
                        return;
                    }
                }
            }
        }

        bool allowBack = (mpMgr != null && mpMgr.IsActive && mpMgr.AllowBackAttack);
        Character foremostAlly = FindForemostAlliedTroop();
        Character vanguardTank;
        bool hasVanguardTank = HasAlliedVanguardTank(myZ, out vanguardTank);
        float heroDist;
        bool heroFleeingOrRanged;
        Character priorityHero = FindPriorityHeroTarget(myZ, allowBack, out heroDist, out heroFleeingOrRanged);
        bool shouldPunishOppHero = (priorityHero != null && heroFleeingOrRanged && heroDist <= 560f && health >= maxHealth * 0.25f && (p1Count <= 2 || hasVanguardTank || (priorityHero.maxHealth > 0f && priorityHero.health < priorityHero.maxHealth * 0.40f)));

        // 6. SMART TACTICAL KITING & RETREAT WHEN LONE (NO VANGUARD TANK) OR LOW HP (< 35%)
        if (nearestEnemy != null && !nearOwnGate && !shouldPunishOppHero)
        {
            bool enemyBehindMe = mFacingLeft ? (nearestEnemy.position.z > myZ + 15f) : (nearestEnemy.position.z < myZ - 15f);
            if (health < maxHealth * 0.35f)
            {
                if (aliveTroops >= 1 && foremostAlly != null && !enemyBehindMe && (enemiesInMelee >= 1 || nearestEnemyDist <= 160f) && mDodgeCooldown <= 0f)
                {
                    TriggerDodgeBackpedal(0.90f);
                    mMeleeComboHits = 0;
                    return;
                }
                else if (aliveTroops == 0 && nearestEnemyDist <= 185f && mDodgeCooldown <= 0f)
                {
                    TriggerDodgeBackpedal(0.95f);
                    mMeleeComboHits = 0;
                    return;
                }
            }
            else if (!hasVanguardTank && (aliveTroops == 0 || p1Count >= 2 || health < maxHealth * 0.78f))
            {
                // Unsupported Hero with no tank/super unit in front: kite back to bow distance and wait for allies/spells!
                if (!enemyBehindMe && (enemiesInMelee >= 1 || nearestEnemyDist <= 175f))
                {
                    if (mDodgeCooldown <= 0f)
                    {
                        TriggerDodgeBackpedal(0.85f);
                        mMeleeComboHits = 0;
                        return;
                    }
                    else if (!nearestEnemy.isFlying && nearestEnemyDist <= 155f)
                    {
                        SetRangeAttackMode(false);
                        MoveTowardZ(ownGateZ, 300f);
                        return;
                    }
                }
            }
        }

        // 7. CLOSE-QUARTERS MELEE COMBAT (Dual Katana Combos + Whirlwind Cleave)
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        bool enemyInMelee = (nearestEnemy != null && !nearestEnemy.isFlying && nearestEnemyDist <= base.meleeAttackRange + 10f) || IsInMeleeRangeOfOpponent(true);
        if (!enemyInMelee && charMgr != null)
        {
            List<Character> hits = charMgr.GetCharactersInRange(base.meleeAttackHitGameRange, !base.isEnemy);
            enemyInMelee = (hits != null && hits.Count > 0);
        }

        if (enemyInMelee)
        {
            if (nearestEnemy != null && nearestEnemyDist <= base.meleeAttackRange + 20f)
            {
                FaceTargetZ(nearestEnemy.position.z);
            }
            SetRangeAttackMode(false);

            // Step back if unsupported without vanguard tank and outnumbered by 2+ melee enemies, or critical HP alone
            if (!nearOwnGate && !shouldPunishOppHero && mDodgeCooldown <= 0f &&
                ((!hasVanguardTank && enemiesInMelee >= 2 && health < maxHealth * 0.65f) || (aliveTroops == 0 && enemiesInMelee >= 3 && health < maxHealth * 0.25f)))
            {
                TriggerDodgeBackpedal(0.85f);
                mMeleeComboHits = 0;
                return;
            }

            PerformHeroMeleeAttack();
            return;
        }

        mMeleeComboHits = 0;

        // 9. PRIORITY PURSUIT: FLEEING LOW-HP OR BOW-KITTING OPPONENT HERO -> INTERCEPT & CUT DOWN!
        if (shouldPunishOppHero && (!nearOwnGate || heroDist <= 280f))
        {
            SetRangeAttackMode(false);
            MoveTowardZ(priorityHero.position.z, 325f);
            return;
        }

        // 9B. AGGRESSIVE MELEE PURSUIT (WHEN SUPPORTED BY VANGUARD/ALLIES OR DEFENDING GATE)
        if (nearestEnemy != null && !nearestEnemy.isFlying)
        {
            bool isBehindMe = mFacingLeft ? (nearestEnemy.position.z > myZ + 12f) : (nearestEnemy.position.z < myZ - 12f);
            if (allowBack && isBehindMe && nearestEnemyDist <= 520f)
            {
                SetRangeAttackMode(false);
                MoveTowardZ(nearestEnemy.position.z, 300f); // Sprint back to intercept flankers/backdoor Hero!
                return;
            }
            if (!isBehindMe && (hasVanguardTank || aliveTroops >= 1 || nearOwnGate) && nearestEnemyDist <= 290f && !(health < maxHealth * 0.28f && aliveTroops >= 1))
            {
                SetRangeAttackMode(false);
                MoveTowardZ(nearestEnemy.position.z, 300f);
                return;
            }
            if (!isBehindMe && nearestEnemy.isPlayer && nearestEnemy.health < nearestEnemy.maxHealth * 0.45f && nearestEnemyDist <= 480f)
            {
                SetRangeAttackMode(false);
                MoveTowardZ(nearestEnemy.position.z, 325f);
                return;
            }
        }

        // 10. RANGED LEVEL 8 BOW SNIPE AT MEDIUM-LONG RANGE (<= 400f) OR WHILE KITING BEHIND TANKS
        if (base.hasRangedAttack && charMgr != null)
        {
            Character bowTarget = null;
            if (nearestEnemy != null && nearestEnemyDist <= base.bowAttackRange)
            {
                bowTarget = nearestEnemy;
            }
            else if (IsInBowRangeOfOpponent())
            {
                bowTarget = charMgr.GetBestRangedAttackTarget(this, base.bowAttackExtGameRange);
            }

            if (bowTarget != null)
            {
                FaceTargetZ(bowTarget.position.z);
                SetRangeAttackMode(true);
                if (base.canUseRangedAttack)
                {
                    mAttackCooldown = 1.25f;
                    StartRangedAttackDelayTimer();
                    base.singleAttackTarget = bowTarget;
                    base.controller.RangeAttack(base.bowAttackFrequency);
                    return;
                }
            }
        }

        // 11. MARCH BEHIND VANGUARD TANK / ARMY (OR HOLD RALLY POINT TO WAIT FOR SUPPORT IF ALONE!)
        Character followAnchor = (vanguardTank != null) ? vanguardTank : foremostAlly;
        float followOffset = (health < maxHealth * 0.40f || !hasVanguardTank) ? 110f : 25f;

        if (followAnchor != null && base.controller != null)
        {
            float vanguardZ = followAnchor.position.z;

            if (mFacingLeft)
            {
                float desiredZ = vanguardZ + followOffset;
                if (myZ > desiredZ + 20f)
                {
                    SetRangeAttackMode(false);
                    MoveTowardZ(desiredZ, 300f);
                    return;
                }
                else if (myZ < desiredZ - 35f && aliveTroops >= 1)
                {
                    if (health < maxHealth * 0.40f || !hasVanguardTank)
                    {
                        // Step back behind the tank to safely regenerate HP & kite!
                        SetRangeAttackMode(false);
                        MoveTowardZ(desiredZ, 300f);
                    }
                    else
                    {
                        base.controller.facing = FacingType.Left;
                        base.controller.Idle();
                    }
                    return;
                }
            }
            else
            {
                float desiredZ = vanguardZ - followOffset;
                if (myZ < desiredZ - 20f)
                {
                    SetRangeAttackMode(false);
                    MoveTowardZ(desiredZ, 300f);
                    return;
                }
                else if (myZ > desiredZ + 35f && aliveTroops >= 1)
                {
                    if (health < maxHealth * 0.40f || !hasVanguardTank)
                    {
                        SetRangeAttackMode(false);
                        MoveTowardZ(desiredZ, 300f);
                    }
                    else
                    {
                        base.controller.facing = FacingType.Right;
                        base.controller.Idle();
                    }
                    return;
                }
            }
        }
        else if (!hasVanguardTank && aliveTroops == 0)
        {
            // No allied tank or army on the field: hold a safe midfield/defensive rally point waiting for backup instead of rushing alone into tower fire!
            float rallyZ = Mathf.Lerp(ownGateZ, oppGateZ, 0.35f);
            if (Mathf.Abs(myZ - rallyZ) > 35f)
            {
                SetRangeAttackMode(false);
                MoveTowardZ(rallyZ, 285f);
            }
            else
            {
                base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
                base.controller.Idle();
            }
            return;
        }

        // Advance aggressively down the lane toward the opponent's Gate when supported!
        SetRangeAttackMode(false);
        base.controller.facing = mFacingLeft ? FacingType.Left : FacingType.Right;
        base.controller.speed = 300f;
        if (!base.canMove)
        {
            base.controller.Idle();
        }
        else
        {
            if (mFacingLeft) base.controller.StartWalkLeft();
            else base.controller.StartWalkRight();
        }
    }

    private void OnSpecialSlashDelivery()
    {
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr == null || base.controller == null) return;

        float myZ = base.controller.position.z;
        float cleaveRadius = base.meleeAttackRange * 1.25f;
        GameRange directionalRange = (base.controller.facing == FacingType.Left)
            ? new GameRange(myZ - cleaveRadius, myZ)
            : new GameRange(myZ, myZ + cleaveRadius);

        List<Character> victims = charMgr.GetCharactersInRange(directionalRange, !base.isEnemy);
        if (victims != null)
        {
            foreach (Character c in victims)
            {
                if (c != null && c.health > 0f && !c.isFlying && !c.isBase)
                {
                    c.RecievedAttack(EAttackType.BladeCritical, base.meleeDamage * 1.5f);
                    c.TryKnockback(160);
                }
            }
        }
    }

    private void TriggerLifeStealHeal()
    {
        // Disabled passive life steal heal & FX spam
    }

    private void PlayKatanaSlashSFX()
    {
        try
        {
            SVZMultiplayerManager.PlaySFX("Hero_Samurai_Sword_Slash_01", 0.7f);
        }
        catch { }
    }

    public override void Destroy()
    {
        if (ActiveOpponentHero == this)
        {
            ActiveOpponentHero = null;
        }
        base.Destroy();
    }
}
