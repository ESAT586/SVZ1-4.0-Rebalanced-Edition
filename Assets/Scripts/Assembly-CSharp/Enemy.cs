using UnityEngine;
using System.Collections.Generic;

public class Enemy : Character
{
    // 1. STATÄ°K LÄ°STE VE KRONOMETRE
    public static List<Enemy> activeEnemies = new List<Enemy>();
    private static Dictionary<string, GameObject> sEnemyModelCache = new Dictionary<string, GameObject>();
    private float lastSpawnTime = 0f;
    private bool mIsLeftToRightGameplay = true;

    private static GameObject GetCachedEnemyModel(string model)
    {
        if (string.IsNullOrEmpty(model)) return null;
        GameObject cached;
        if (sEnemyModelCache.TryGetValue(model, out cached) && cached != null)
        {
            return cached;
        }
        cached = Resources.Load(model) as GameObject;
        if (cached != null)
        {
            sEnemyModelCache[model] = cached;
        }
        return cached;
    }

    // 2. CONSTRUCTOR (DOÄMA FONKSÄ°YONU)
    public Enemy(string model, float zTarget, Vector3 pos)
    {
        base.isEnemy = true;
        mIsLeftToRightGameplay = Singleton<PlayModesManager>.instance.gameDirection == PlayModesManager.GameDirection.LeftToRight;
        ActivateHealthBar();

        // BELÄ°RGÄ°N REFERANS (UnityEngine.Object ekledik)
        GameObject prefab = GetCachedEnemyModel(model);
        if (prefab != null)
        {
            base.controlledObject = (GameObject)UnityEngine.Object.Instantiate(prefab);
        }
        if (base.controller != null)
        {
            base.controller.nativeFacing = CharacterModelController.GetModelNativeFacing(model);
        }

        if (base.controlledObject != null && Singleton<PlayModesManager>.instance.selectedMode != "classic")
        {
            SoundThemePlayer component = base.controlledObject.GetComponent<SoundThemePlayer>();
            if (component != null)
            {
                component.autoPlayEvent = string.Empty;
            }
        }

        base.controller.position = pos;
        float spawnZ = pos.z;
        if (WeakGlobalInstance<WaveManager>.instance != null && WeakGlobalInstance<WaveManager>.instance.enemiesSpawnArea != null)
        {
            spawnZ = WeakGlobalInstance<WaveManager>.instance.enemiesSpawnArea.transform.position.z;
        }
        base.controller.constraintLeft = Mathf.Min(zTarget, spawnZ);
        base.controller.constraintRight = Mathf.Max(zTarget, spawnZ);

        StartWalkingTowardGoal();

        if (base.bowAttackRange > 0f && base.meleeAttackRange == 0f)
        {
            SetRangeAttackMode(true);
        }

        // Zombi listeye eklenir
        if (!activeEnemies.Contains(this))
        {
            activeEnemies.Add(this);
        }
    }

    // 3. DESTROY (SÄ°LÄ°NME FONKSÄ°YONU)
    public override void Destroy()
    {
        if (activeEnemies.Contains(this))
        {
            activeEnemies.Remove(this);
        }
        base.Destroy();
    }

    // 4. ANA DÖNGÜ (UPDATE)
    public override void Update()
    {
        base.Update();

        if (this is OpponentHero)
        {
            return; // OpponentHero manages its own dedicated duelist hero AI!
        }

        if (BloodMoonTerasuController.IsBusyWithSpecialAction(this))
        {
            return;
        }

        if (base.health <= 0f || !base.isEffectivelyIdle || base.controller.impactPauseTime > 0f)
        {
            return;
        }

        if (base.isFlying && WeakGlobalInstance<TutorialHookup>.instance != null)
        {
            float num = base.position.z - WeakGlobalSceneBehavior<InGameImpl>.instance.hero.position.z;
            if (num > 0f && num < 250f)
            {
                WeakGlobalInstance<TutorialHookup>.instance.flyingEnemyInView = true;
            }
        }

        bool marchRight = SVZMultiplayerManager.ShouldUnitMarchRight(base.isEnemy, mIsLeftToRightGameplay);
        bool flag = marchRight
            ? (base.controller.position.z >= base.controller.constraintRight)
            : (base.controller.position.z <= base.controller.constraintLeft);

        if (base.canMeleeAttack && IsInMeleeRangeOfOpponent(true))
        {
            base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
            SetRangeAttackMode(false);
            if (base.exploseOnMelee)
            {
                AttackByExploding();
                return;
            }
            StartMeleeAttackDelayTimer();
            base.controller.Attack(base.meleeAttackFrequency);
        }
        else if (base.canUseRangedAttack && ((base.isSpellCasterOnAllies && IsInRangeOfHurtAlly()) || (base.bowProjectile == Projectile.EProjectileType.SpawnEnemy && IsInBowRangeOfOpponent())))
        {
            base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
            StartRangedAttackDelayTimer();
            if (base.bowProjectile == Projectile.EProjectileType.SpawnFriend)
            {
                base.controller.PerformSpecialAction("cast", base.OnCastSpawnAllies, base.bowAttackFrequency);
            }
            else if (base.bowProjectile == Projectile.EProjectileType.SpawnEnemy)
            {
                // Burası bizim yeni fonksiyonumuzu çağırır
                base.controller.PerformSpecialAction("cast", this.OnCastSpawnEnemies, base.bowAttackFrequency);
            }
            else
            {
                base.controller.PerformSpecialAction("cast", base.OnCastHeal, base.bowAttackFrequency);
            }
        }
        else if (base.canUseRangedAttack && base.hasRangedAttack && IsInBowRangeOfOpponent())
        {
            base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
            SetRangeAttackMode(true);
            StartRangedAttackDelayTimer();
            base.singleAttackTarget = WeakGlobalInstance<CharactersManager>.instance.GetBestRangedAttackTarget(this, base.bowAttackExtGameRange);
            if (base.singleAttackTarget != null)
            {
                base.controller.RangeAttack(base.bowAttackFrequency);
            }
        }
        else if (IsInMeleeRangeOfOpponent(false))
        {
            base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
            base.controller.Idle();
        }
        else if (!base.canMove || flag || IsInBowRangeOfOpponent() || (base.isSpellCasterOnAllies && IsInRangeOfHurtAlly()))
        {
            base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
            base.controller.Idle();
        }
        else if (WeakGlobalSceneBehavior<InGameImpl>.instance.enemiesWon)
        {
            base.controller.PlayVictoryAnim();
        }
        else
        {
            StartWalkingTowardGoal();
        }
    }

	    private void StartWalkingTowardGoal()
	    {
	        bool marchRight = SVZMultiplayerManager.ShouldUnitMarchRight(base.isEnemy, mIsLeftToRightGameplay);
	        if (marchRight)
	        {
	            base.controller.facing = FacingType.Right;
	            base.controller.StartWalkRight();
	        }
	        else
	        {
	            base.controller.facing = FacingType.Left;
	            base.controller.StartWalkLeft();
	        }
	    }

    // 5. ÖZEL YETENEK FONKSİYONU
    public void OnCastSpawnEnemies()
    {
        // AOE HEAL: Sahadaki tüm dost zombilere +35 can
        Enemy[] enemyArray = activeEnemies.ToArray();
        foreach (Enemy z in enemyArray)
        {
            if (z != null && z.health > 0f && z.isEnemy == base.isEnemy)
            {
                z.health += 35f;
            }
        }

        // KRONOMETRE: 16 saniyede bir asker çıkarma
        if (Time.time - this.lastSpawnTime < 16f)
        {
            return;
        }

	        this.lastSpawnTime = Time.time;
	        if (!string.IsNullOrEmpty(base.spawnFriendID))
	        {
	            SpawnOffspring(base.spawnFriendID, 1);
	        }
	    }
}
