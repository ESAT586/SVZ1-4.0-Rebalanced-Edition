using System;
using UnityEngine;

public class Helper : Character
{
	private bool mIsLeftToRightGameplay = true;
	private static System.Collections.Generic.Dictionary<string, GameObject> sWeaponPrefabCache = new System.Collections.Generic.Dictionary<string, GameObject>();

	private static GameObject GetCachedWeaponPrefab(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		GameObject cached;
		if (sWeaponPrefabCache.TryGetValue(path, out cached) && cached != null)
		{
			return cached;
		}
		cached = Resources.Load(path) as GameObject;
		if (cached != null)
		{
			sWeaponPrefabCache[path] = cached;
		}
		return cached;
	}

	public Helper(GameObject prefab, float zTarget, Vector3 pos, string weaponPropLocation, bool weaponIsRanged)
	{
		mIsLeftToRightGameplay = Singleton<PlayModesManager>.instance.gameDirection == PlayModesManager.GameDirection.LeftToRight;
		ActivateHealthBar();
		InitializeModel(prefab, weaponPropLocation, weaponIsRanged);
		base.controller.position = pos;
		float spawnZ = pos.z;
		if (WeakGlobalInstance<Smithy>.instance != null && WeakGlobalInstance<Smithy>.instance.helperSpawnArea != null)
		{
			spawnZ = WeakGlobalInstance<Smithy>.instance.helperSpawnArea.transform.position.z;
		}
		base.controller.constraintLeft = Mathf.Min(spawnZ, zTarget);
		base.controller.constraintRight = Mathf.Max(spawnZ, zTarget);
		StartWalkingTowardGoal();
	}

	public void ReinitializeModel(GameObject prefab, string weaponPropLocation, bool weaponIsRanged)
	{
		Vector3 vector = base.controller.position;
		vector.x = base.controller.xPos;
		float constraintLeft = base.controller.constraintLeft;
		float constraintRight = base.controller.constraintRight;
		InitializeModel(prefab, weaponPropLocation, weaponIsRanged);
		base.controller.position = vector;
		base.controller.constraintLeft = constraintLeft;
		base.controller.constraintRight = constraintRight;
		StartWalkingTowardGoal();
	}

    private void InitializeModel(GameObject prefab, string weaponPropLocation, bool weaponIsRanged)
    {
        // EKLENEN KISIM
        if (prefab == null)
        {
            Debug.LogError("DİKKAT: Helper (Dost Birlik) prefabı bulunamadı! Lütfen bu birliğin dosyalarını kontrol et.");
            return; // Obje yoksa devam etme.
        }

        base.controlledObject = (GameObject)UnityEngine.Object.Instantiate(prefab);
        if (base.controller != null)
        {
            base.controller.nativeFacing = CharacterModelController.GetModelNativeFacing(prefab.name);
        }
        {
			if (weaponIsRanged)
			{
				base.rangedWeaponPrefab = GetCachedWeaponPrefab(weaponPropLocation);
			}
			else
			{
				base.meleeWeaponPrefab = GetCachedWeaponPrefab(weaponPropLocation);
			}
		}
		if (base.bowAttackRange > 0f && base.meleeAttackRange == 0f)
		{
			SetRangeAttackMode(true);
		}
	}

	public override void Update()
	{
		base.Update();
		if (BloodMoonTerasuController.IsBusyWithSpecialAction(this))
		{
			return;
		}
		if (base.health == 0f || !base.isEffectivelyIdle || base.controller.impactPauseTime > 0f)
		{
			return;
		}
		bool marchRight = SVZMultiplayerManager.ShouldUnitMarchRight(base.isEnemy, mIsLeftToRightGameplay);
		bool flag = marchRight
			? (base.controller.position.z >= base.controller.constraintRight)
			: (base.controller.position.z <= base.controller.constraintLeft);
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.playerWon)
		{
			base.controller.PlayVictoryAnim();
		}
		else if (base.canMeleeAttack && IsInMeleeRangeOfOpponent(true))
		{
			base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
			base.controller.SetUseCowerIdle(false);
			SetRangeAttackMode(false);
			if (base.exploseOnMelee)
			{
				AttackByExploding();
				return;
			}
			StartMeleeAttackDelayTimer();
			base.controller.Attack(base.meleeAttackFrequency);
		}
		else if ((base.isSpellCasterOnAllies && base.canUseRangedAttack && IsInRangeOfHurtAlly(false)) || (base.canUseRangedAttack && base.stats.projectile == Projectile.EProjectileType.SpawnFriend && IsInBowRangeOfOpponent()))
		{
			base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
			StartRangedAttackDelayTimer();
			if (base.bowProjectile == Projectile.EProjectileType.SpawnFriend)
			{
				UnityEngine.Object.Instantiate(Resources.Load("FX/DivineEnemy"), base.position + new Vector3(50f, 0f, 0f), Quaternion.identity);
				base.controller.PerformSpecialAction("cast", base.OnCastSpawnAllies, base.bowAttackFrequency / 2f);
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
			base.controller.SetUseCowerIdle(!IsInMeleeRangeOfOpponent(true));
			base.controller.Idle();
		}
		else if (!base.canMove || flag || IsInBowRangeOfOpponent() || (base.isSpellCasterOnAllies && IsInRangeOfHurtAlly()))
		{
			base.controller.facing = marchRight ? FacingType.Right : FacingType.Left;
			base.controller.SetUseCowerIdle(false);
			base.controller.Idle();
		}
		else
		{
			base.controller.SetUseCowerIdle(false);
			StartWalkingTowardGoal();
		}
	}

	public override void Destroy()
	{
		base.Destroy();
	}

	public void SetSpawnOnDeath(string spawnHelperID, int num)
	{
		base.onDeathEvent = (Action)Delegate.Combine(base.onDeathEvent, (Action)delegate
		{
			SpawnOffspring(spawnHelperID, num);
		});
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
}
