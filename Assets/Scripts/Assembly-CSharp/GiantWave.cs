using System.Collections.Generic;
using UnityEngine;

public class GiantWave : MonoBehaviour
{
	private const float kDamageFrequency = 0.2f;

	private const float kDamageRange = 200f;

	private float mSpeed;

	private float mTimeUntilNextDamage;

	private float mDamageRemaining;

	private float mEndZ;

	private string mAbilityID = string.Empty;

	public bool useCustomSettings = false;
	public bool targetEnemies = true;
	public bool moveLeftToRight = true;
	public string customAbilityID = string.Empty;

	private void Start()
	{
		if (useCustomSettings && !string.IsNullOrEmpty(customAbilityID))
		{
			mAbilityID = customAbilityID;
		}
		else
		{
			mAbilityID = Singleton<PlayModesManager>.instance.selectedModeData["giantWaveID"];
			moveLeftToRight = (Singleton<PlayModesManager>.instance.gameDirection != PlayModesManager.GameDirection.RightToLeft);
			targetEnemies = true;
		}
		string speedStr = Singleton<AbilitiesDatabase>.instance.GetAttribute(mAbilityID, "speed");
		mSpeed = !string.IsNullOrEmpty(speedStr) ? float.Parse(speedStr) : 450f;
		mDamageRemaining = Singleton<AbilitiesDatabase>.instance.Extrapolate<float>(mAbilityID, "infiniteUpgradeDamage", "damage");
		if (mDamageRemaining <= 0f) mDamageRemaining = 1200f;

		InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
		if (inGame == null)
		{
			Destroy(gameObject);
			return;
		}
		float leftZ = (inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : ((inGame.hero != null && inGame.hero.controller != null) ? inGame.hero.controller.constraintLeft : -500f);
		float rightZ = (inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : ((inGame.hero != null && inGame.hero.controller != null) ? inGame.hero.controller.constraintRight : 500f);
		float minZ = Mathf.Min(leftZ, rightZ);
		float maxZ = Mathf.Max(leftZ, rightZ);
		mEndZ = moveLeftToRight ? maxZ : minZ;
	}

	private void Update()
	{
		Vector3 position = base.transform.position;
		float num = mSpeed * Time.deltaTime;
		if (!moveLeftToRight)
		{
			num = 0f - num;
		}
		position.z += num;
		mTimeUntilNextDamage -= Time.deltaTime;
		if (mTimeUntilNextDamage <= 0f)
		{
			mTimeUntilNextDamage = 0.2f;
			List<Character> list = (moveLeftToRight ? WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(position.z, position.z + 200f, targetEnemies) : WeakGlobalInstance<CharactersManager>.instance.GetCharactersInRange(position.z - 200f, position.z, targetEnemies));
			foreach (Character item in list)
			{
				if (item != null && mDamageRemaining > 0f && item.health > 0f)
				{
					if (item.isBase)
					{
						continue;
					}
					float health = item.health;
					item.RecievedAttack(EAttackType.Trample, mDamageRemaining);
					mDamageRemaining -= health;
				}
			}
		}
		bool flag = (moveLeftToRight && position.z >= mEndZ) || (!moveLeftToRight && position.z <= mEndZ);
		if (mDamageRemaining <= 0f || flag)
		{
			if (!moveLeftToRight)
			{
				position.z = Mathf.Max(position.z, mEndZ);
			}
			else
			{
				position.z = Mathf.Min(position.z, mEndZ);
			}
			position.y = WeakGlobalInstance<RailManager>.instance.GetY(position.z);
			base.transform.position = position;
			string attribute = Singleton<AbilitiesDatabase>.instance.GetAttribute(mAbilityID, "fxDie");
			if (attribute != string.Empty)
			{
				Object.Instantiate(Resources.Load(attribute) as GameObject, base.transform.position, base.transform.rotation);
			}
			Object.Destroy(base.gameObject);
		}
		else
		{
			position.y = WeakGlobalInstance<RailManager>.instance.GetY(position.z);
			base.transform.position = position;
		}
	}
}
