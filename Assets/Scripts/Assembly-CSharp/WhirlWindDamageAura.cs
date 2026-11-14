using System.Collections.Generic;
using UnityEngine;

public class WhirlWindDamageAura : MonoBehaviour
{
	private const float kDamageFrequency = 0.2f;

	private const float kDissipateTime = 2f;

	private float mDamagePerHit;

	private float mFlierDamageMultiplier = 1f;

	private float mTimeUntilNextDamage;

	private float mRemainingDuration;

	private GameRange mDamageRange = new GameRange(0f, 0f);

	private bool mStoppedEmitting;

	private ParticleEmitter[] mEmitters;

	public bool targetEnemies = true;

	private void Start()
	{
		mEmitters = GetComponentsInChildren<ParticleEmitter>();
		string durStr = Singleton<AbilitiesDatabase>.instance.GetAttribute("SummonTornadoes", "duration");
		mRemainingDuration = !string.IsNullOrEmpty(durStr) ? float.Parse(durStr) : 6f;
		float totalDmg = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>("SummonTornadoes", "infiniteUpgradeDamage", "damage");
		if (totalDmg <= 0f) totalDmg = 300f;
		mDamagePerHit = totalDmg / (mRemainingDuration / 0.2f);
		string flierStr = Singleton<AbilitiesDatabase>.instance.GetAttribute("SummonTornadoes", "flyerDamageMultiplier");
		mFlierDamageMultiplier = !string.IsNullOrEmpty(flierStr) ? float.Parse(flierStr) : 1.5f;
		string radStr = Singleton<AbilitiesDatabase>.instance.GetAttribute("SummonTornadoes", "damageRadius");
		float num = !string.IsNullOrEmpty(radStr) ? float.Parse(radStr) : 140f;
		mDamageRange.left = base.transform.position.z - num;
		mDamageRange.right = mDamageRange.left + num * 2f;
	}

	private void Update()
	{
		if (mStoppedEmitting)
		{
			bool flag = false;
			ParticleEmitter[] array = mEmitters;
			foreach (ParticleEmitter particleEmitter in array)
			{
				particleEmitter.emit = false;
				if (particleEmitter.particleCount > 0)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				Object.Destroy(base.gameObject);
			}
			return;
		}
		mRemainingDuration -= Time.deltaTime;
		mTimeUntilNextDamage -= Time.deltaTime;
		while (mTimeUntilNextDamage <= 0f)
		{
			mTimeUntilNextDamage += 0.2f;
			List<Character> charactersInRange = WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(mDamageRange, targetEnemies);
			foreach (Character item in charactersInRange)
			{
				if (item != null && !item.isBase)
				{
					if (item.isFlying)
					{
						item.RecievedAttack(EAttackType.DOT, mDamagePerHit * mFlierDamageMultiplier);
					}
					else
					{
						item.RecievedAttack(EAttackType.DOT, mDamagePerHit);
					}
				}
			}
		}
		if (mRemainingDuration <= 0f)
		{
			mStoppedEmitting = true;
		}
	}
}
