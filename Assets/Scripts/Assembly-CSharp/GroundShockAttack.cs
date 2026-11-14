using System.Collections.Generic;
using UnityEngine;

public class GroundShockAttack : MonoBehaviour
{
	private GameRange mDamageRange = new GameRange(0f, 0f);

	private float mDamage;

	public bool useCustomTarget = false;
	public bool targetEnemies = true;

	private void Start()
	{
		string radStr = Singleton<AbilitiesDatabase>.instance.GetAttribute("GroundShock", "damageRadius");
		float num = !string.IsNullOrEmpty(radStr) ? float.Parse(radStr) : 160f;
		mDamageRange.left = base.transform.position.z - num;
		mDamageRange.right = mDamageRange.left + num * 2f;
		mDamage = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>("GroundShock", "infiniteUpgradeDamage", "damage");
		if (mDamage <= 0f) mDamage = 200f;
	}

	private void DeliverAttack()
	{
		if (useCustomTarget)
		{
			List<Character> targets = WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(mDamageRange, targetEnemies);
			foreach (Character item in targets)
			{
				if (item != null && item.health > 0f && !item.isBase)
				{
					item.RecievedAttack(EAttackType.Stomp, mDamage);
				}
			}
			return;
		}
		List<Character> charactersInRange = WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(mDamageRange, true);
		foreach (Character item in charactersInRange)
		{
			if (item != null && item.health > 0f && !item.isBase)
			{
				item.RecievedAttack(EAttackType.Stomp, mDamage);
			}
		}
	}
}
