using UnityEngine;

public class TowerArcher : Character
{
	public TowerArcher(string model, Vector3 pos, string weaponPropLocation, Projectile.EProjectileType projectileType, float damage, float range, float attackFrequency, bool againstPlayer, FacingType facing = FacingType.Right)
	{
		GameObject modelPrefab = LoadPrefab(model) as GameObject;
		if (modelPrefab == null)
		{
			Debug.LogError("TowerArcher: missing archer prefab: " + model);
			return;
		}
		CharacterStats characterStats = new CharacterStats
		{
			maxHealth = 100f,
			health = 100f,
			bowAttackFrequency = attackFrequency,
			bowAttackDamage = damage,
			bowAttackRange = range,
			meleeAttackRange = 30f,
			projectile = projectileType,
			isEnemy = againstPlayer
		};
		base.controlledObject = (GameObject)Object.Instantiate(modelPrefab);
		SoundThemePlayer soundTheme = base.controlledObject.GetComponent<SoundThemePlayer>();
		if (soundTheme != null)
		{
			soundTheme.autoPlayEvent = string.Empty;
		}
		if (base.controller != null)
		{
			base.controller.snapToGround = false;
			base.controller.nativeFacing = (model != null && model.IndexOf("zombie", System.StringComparison.OrdinalIgnoreCase) >= 0) ? FacingType.Left : FacingType.Right;
		}
		base.controller.position = pos;
		base.controller.facing = facing;
		base.stats = characterStats;
		if (weaponPropLocation != null)
		{
			base.rangedWeaponPrefab = LoadPrefab(weaponPropLocation) as GameObject;
			if (base.rangedWeaponPrefab == null)
			{
				Debug.LogWarning("TowerArcher: missing ranged weapon prefab: " + weaponPropLocation);
			}
		}
		SetRangeAttackMode(true);
	}

	private static Object LoadPrefab(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}
		Object prefab = Resources.Load(path);
		if (prefab != null)
		{
			return prefab;
		}
		int slash = path.IndexOf('/');
		if (slash > 0)
		{
			string fallback = path.Substring(0, slash).ToLower() + path.Substring(slash);
			prefab = Resources.Load(fallback);
		}
		return prefab;
	}

	public override void Update()
	{
		base.Update();
		if (base.health != 0f && base.isEffectivelyIdle)
		{
			if (WeakGlobalSceneBehavior<InGameImpl>.instance.playerWon && !base.isEnemy)
			{
				base.controller.PlayVictoryAnim();
			}
			else if (base.canUseRangedAttack && IsInBowRangeOfOpponent())
			{
				StartRangedAttackDelayTimer();
				base.singleAttackTarget = WeakGlobalInstance<CharactersManager>.instance.GetBestRangedAttackTarget(this, base.bowAttackGameRange);
				base.controller.RangeAttack(base.bowAttackFrequency);
			}
		}
	}
}
