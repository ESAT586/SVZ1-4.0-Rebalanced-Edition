using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class Smithy : WeakGlobalInstance<Smithy>
{
	public struct SmithyCost
	{
		public float smithy;

		public int gems;

		public int coins;

		public string helperId;

		public bool canAfford
		{
			get
			{
				if (smithy > 0f)
				{
					return WeakGlobalInstance<Smithy>.instance.resources >= smithy;
				}
				if (gems > 0)
				{
					return Singleton<Profile>.instance.gems >= gems;
				}
				if (coins > 0)
				{
					return Singleton<Profile>.instance.coins >= coins;
				}
				return false;
			}
		}

		public SmithyCost(string data)
		{
			smithy = 0f;
			gems = 0;
			coins = 0;
			helperId = string.Empty;
			string[] array = data.Split(',');
			if (array.Length >= 1)
			{
				float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out smithy);
			}
			if (array.Length >= 2)
			{
				int.TryParse(array[1], out gems);
			}
			if (array.Length >= 3)
			{
				int.TryParse(array[2], out coins);
			}
		}

		public void Spend()
		{
			if (smithy > 0f)
			{
				WeakGlobalInstance<Smithy>.instance.resources -= smithy;
			}
			else if (gems > 0)
			{
				ApplicationUtilities.GWalletBalance(-gems, "Smithy Spend: " + helperId, "DEBIT_IN_APP_PURCHASE");
			}
			else if (coins > 0)
			{
				Singleton<Profile>.instance.coins -= coins;
			}
		}
	}

	private class HelperTypeData
	{
		public string id;

		public string displayName;

		public string prefabPath;

		public GameObject prefab;

		public string HUDIcon;

		public bool unique;

		public SmithyCost smithyCost;

		public float health;

		public float speedMin;

		public float speedMax;

		public bool isFlying;

		public bool exploseOnMelee;

		public float meleeFreeze;

		public float autoHealthRecovery;

		public float swordAttackRange;

		public float bowAttackRange;

		public float meleeDamage;

		public float bowDamage;

		public float attackFrequency;

		public float damageBuffPercent;

		public int knockbackPower;

		public int knockbackResistance;

		public Projectile.EProjectileType projectile;

		public CharactersManager.ELanePreference lanePref;

		public float baseCooldown;

		public float totalCooldown;

		public float currentCooldown;

		public int spawnBurstCount;

		public string weaponPrefabLocation;

		public bool rangedWeapon;

		public bool bladedWeapon;

		public string upgradeAlliesFrom;

		public string upgradeAlliesTo;

		public string spawnOnDeathType;

		public int spawnOnDeathNum;

		public string spawnFriendID;

		public string auraFX;

		public string auraJoint;

		public HelperTypeData(string helperID)
		{
			id = helperID;
			displayName = Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "displayName");
			prefabPath = Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "prefab");
			prefab = LoadPrefab(prefabPath);
			if (prefab == null)
			{
				Debug.LogError("Smithy: missing helper prefab for " + helperID + ": " + prefabPath);
			}
			HUDIcon = Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "HUDIcon");
			smithyCost = new SmithyCost(Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "resourcesCost"));
			smithyCost.helperId = helperID;
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "cooldownTimer"), NumberStyles.Float, CultureInfo.InvariantCulture, out totalCooldown);
			if (totalCooldown <= 0f) totalCooldown = 1f;
			baseCooldown = totalCooldown;
			currentCooldown = totalCooldown;
			spawnBurstCount = 0;
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "health"), NumberStyles.Float, CultureInfo.InvariantCulture, out health);
			if (health <= 0f) health = 50f;
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "speedMin"), NumberStyles.Float, CultureInfo.InvariantCulture, out speedMin);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "speedMax"), NumberStyles.Float, CultureInfo.InvariantCulture, out speedMax);
			if (speedMax <= 0f) { speedMin = 100f; speedMax = 100f; }
			bool.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "flying"), out isFlying);
			bool.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "exploseOnMelee"), out exploseOnMelee);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "meleeFreeze"), NumberStyles.Float, CultureInfo.InvariantCulture, out meleeFreeze);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "autoHealthRecovery"), NumberStyles.Float, CultureInfo.InvariantCulture, out autoHealthRecovery);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "meleeRange"), NumberStyles.Float, CultureInfo.InvariantCulture, out swordAttackRange);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "bowRange"), NumberStyles.Float, CultureInfo.InvariantCulture, out bowAttackRange);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "meleeDamage"), NumberStyles.Float, CultureInfo.InvariantCulture, out meleeDamage);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "bowDamage"), NumberStyles.Float, CultureInfo.InvariantCulture, out bowDamage);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttribute(helperID, "attackFrequency"), NumberStyles.Float, CultureInfo.InvariantCulture, out attackFrequency);
			float.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "damageBuffPercent"), out damageBuffPercent);
			bool.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "usesBladeWeapon"), out bladedWeapon);
			int.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "knockbackPower"), out knockbackPower);
			int.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "knockbackResistance"), out knockbackResistance);
			bool.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "unique"), out unique);
			if (!SVZMultiplayerManager.DisableEliteLimitsStatic && SVZMultiplayerManager.GetUniversalMaxAliveCap(helperID) == 1)
			{
				unique = true;
			}
			else if (SVZMultiplayerManager.DisableEliteLimitsStatic)
			{
				unique = false;
			}
			spawnOnDeathType = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "spawnOnDeath");
			int.TryParse(Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "spawnOnDeathCount"), out spawnOnDeathNum);
			spawnFriendID = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "spawnFriendID");
			auraFX = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "auraFX");
			auraJoint = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "auraJoint");
			string attributeOrNull = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "projectile");
			if (!string.IsNullOrEmpty(attributeOrNull))
			{
				projectile = (Projectile.EProjectileType)(int)Enum.Parse(typeof(Projectile.EProjectileType), attributeOrNull);
			}
			attributeOrNull = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "lane");
			if (!string.IsNullOrEmpty(attributeOrNull))
			{
				lanePref = (CharactersManager.ELanePreference)(int)Enum.Parse(typeof(CharactersManager.ELanePreference), attributeOrNull);
			}
			attributeOrNull = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "weaponMeleePrefab");
			if (!string.IsNullOrEmpty(attributeOrNull))
			{
				weaponPrefabLocation = attributeOrNull;
			}
			attributeOrNull = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "weaponRangedPrefab");
			if (!string.IsNullOrEmpty(attributeOrNull))
			{
				weaponPrefabLocation = attributeOrNull;
				rangedWeapon = true;
			}
			attributeOrNull = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "upgradeAlliesFrom");
			if (!string.IsNullOrEmpty(attributeOrNull))
			{
				upgradeAlliesFrom = attributeOrNull;
				rangedWeapon = true;
			}
			attributeOrNull = Singleton<HelpersDatabase>.instance.GetAttributeOrNull(helperID, "upgradeAlliesTo");
			if (!string.IsNullOrEmpty(attributeOrNull))
			{
				upgradeAlliesTo = attributeOrNull;
				rangedWeapon = true;
			}
			if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "haste" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+haste"))
			{
				float num = float.Parse(Singleton<CharmsDatabase>.instance.GetAttribute(WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm, "speedMultiplier"), CultureInfo.InvariantCulture);
				attackFrequency /= num;
				speedMax *= num;
				speedMin *= num;
			}
		}

		public void Update()
		{
			currentCooldown = Mathf.Min(totalCooldown, currentCooldown + Time.deltaTime);
		}

		public void EngageCooldown()
		{
			currentCooldown = 0f;
		}
	}

	private List<HelperTypeData> mHelperTypes = new List<HelperTypeData>();

	public void ResetAllCooldowns()
	{
		for (int i = 0; i < mHelperTypes.Count; i++)
		{
			if (mHelperTypes[i] != null)
			{
				mHelperTypes[i].currentCooldown = mHelperTypes[i].totalCooldown;
			}
		}
	}

	private CharactersManager mCharManagerRef;

	private BoxCollider mHelperSpawnArea;

	private float mHelpersZTarget;

	private SDFTreeNode mSmithyData;

	private SDFTreeNode mLevelData;

	private static GameObject mSpawnEffect;

	private static GameObject mHelperReplaceEffect;

	private List<string> mUniquesAlive = new List<string>();

	private int mLevel;

	private int mMaxLevel;

	private int mExperience;

	private float mIncreaseRate;

	private float mMaxResources;

	private float mResources;

	private float mAbsoluteMaxResources = -1f;

	private float mLevelUpThreshold;

	private bool mIsLeftToRightGameplay = true;

	private float mSharedUndeathRegeneration = Undeath.regeneration;

	private Dictionary<string, HelperTypeData> mHelperDataCache = new Dictionary<string, HelperTypeData>();

	public int numTypes
	{
		get
		{
			return mHelperTypes.Count;
		}
	}

	public CharactersManager characterManagerRef
	{
		get
		{
			return mCharManagerRef;
		}
		set
		{
			mCharManagerRef = value;
		}
	}

	public BoxCollider helperSpawnArea
	{
		get
		{
			return mHelperSpawnArea;
		}
		set
		{
			mHelperSpawnArea = value;
		}
	}

	public float helpersZTarget
	{
		get
		{
			return mHelpersZTarget;
		}
		set
		{
			mHelpersZTarget = value;
		}
	}

	public float maxResources
	{
		get
		{
			return mMaxResources;
		}
	}

	public float absoluteMaxResources
	{
		get
		{
			return mAbsoluteMaxResources;
		}
	}

	public float resources
	{
		get
		{
			return mResources;
		}
		set
		{
			mResources = Mathf.Clamp(value, 0f, mMaxResources);
		}
	}

	public float leadership
	{
		get
		{
			return mResources;
		}
		set
		{
			mResources = Mathf.Clamp(value, 0f, mMaxResources);
		}
	}

	public float maxLeadership
	{
		get
		{
			return mMaxResources;
		}
	}

	public float increaseRate
	{
		get
		{
			return mIncreaseRate;
		}
		set
		{
			mIncreaseRate = value;
		}
	}

	public int level
	{
		get
		{
			return mLevel;
		}
		set
		{
			SetSmithyLevel(value);
		}
	}

	public int maxLevel
	{
		get
		{
			return mMaxLevel;
		}
	}

	public float levelUpThreshold
	{
		get
		{
			return mLevelUpThreshold;
		}
	}

	public bool isUpgradable
	{
		get
		{
			if (mLevel == mMaxLevel)
			{
				return false;
			}
			if (mResources < mLevelUpThreshold)
			{
				return false;
			}
			return true;
		}
	}

	public Smithy()
	{
		SetUniqueInstance(this);
		mIsLeftToRightGameplay = Singleton<PlayModesManager>.instance.gameDirection == PlayModesManager.GameDirection.LeftToRight;
		mSmithyData = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Smithy");
		mMaxLevel = 50;
		while (mMaxLevel >= 1 && mSmithyData.to(mMaxLevel) == null)
		{
			mMaxLevel--;
		}
		mAbsoluteMaxResources = float.Parse(mSmithyData.to(mMaxLevel)["maxResource"]);
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
			{
				SetSmithyLevel(4);
			}
			else
			{
				SetSmithyLevel(1);
			}
		}
		else
		{
			SetSmithyLevel(Singleton<Profile>.instance.initialSmithyLevel);
		}
		List<string> selectedHelpers = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			? new List<string>(SVZMultiplayerManager.Instance.IsOnlineClient ? SVZMultiplayerManager.Instance.P2Deck : SVZMultiplayerManager.Instance.P1Deck)
			: Singleton<Profile>.instance.GetSelectedHelpers();
		foreach (string item in selectedHelpers)
		{
			HelperTypeData helperTypeData = LoadHelperData(item);
			if (helperTypeData != null)
			{
				mHelperTypes.Add(helperTypeData);
				if (helperTypeData.prefab != null)
				{
					WeakGlobalInstance<PrefabPreloader>.instance.Preload(helperTypeData.prefabPath, helperTypeData.prefab);
				}
			}
		}
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			List<string> p1Deck = SVZMultiplayerManager.Instance.P1Deck;
			for (int i = 0; i < p1Deck.Count; i++)
			{
				HelperTypeData hd = LoadHelperData(p1Deck[i]);
				if (hd != null && hd.prefab != null)
				{
					WeakGlobalInstance<PrefabPreloader>.instance.Preload(hd.prefabPath, hd.prefab);
				}
			}
			List<string> p2Deck = SVZMultiplayerManager.Instance.P2Deck;
			for (int i = 0; i < p2Deck.Count; i++)
			{
				HelperTypeData hd2 = LoadHelperData(p2Deck[i]);
				if (hd2 != null && hd2.prefab != null)
				{
					WeakGlobalInstance<PrefabPreloader>.instance.Preload(hd2.prefabPath, hd2.prefab);
				}
			}
		}
		mResources = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive) ? 20f : 0f;
		mSpawnEffect = Resources.Load("FX/TeleportAlly") as GameObject;
		mHelperReplaceEffect = Resources.Load("FX/Swapping") as GameObject;
	}

	public void Update()
	{
		resources += Time.deltaTime * mIncreaseRate;
		foreach (HelperTypeData mHelperType in mHelperTypes)
		{
			mHelperType.Update();
		}
		if (mHelperTypes.Count > 0 && WeakGlobalInstance<TutorialHookup>.instance != null)
		{
			WeakGlobalInstance<TutorialHookup>.instance.firstHelperAvailable = IsAvailable(0);
		}
	}

	public float GetCoolDown(int typeIndex)
	{
		if (typeIndex < 0 || typeIndex >= mHelperTypes.Count)
		{
			return 0f;
		}
		return mHelperTypes[typeIndex].currentCooldown / mHelperTypes[typeIndex].totalCooldown;
	}

	public string GetIconFile(int typeIndex)
	{
		if (typeIndex < 0 || typeIndex >= mHelperTypes.Count)
		{
			return string.Empty;
		}
		return mHelperTypes[typeIndex].HUDIcon;
	}

	public bool IsAvailable(int typeIndex)
	{
		if (typeIndex < 0 || typeIndex >= mHelperTypes.Count)
		{
			return false;
		}
		HelperTypeData helperTypeData = mHelperTypes[typeIndex];
		if (!SVZMultiplayerManager.DisableEliteLimitsStatic && helperTypeData.unique && IsUniqueAlive(helperTypeData.id))
		{
			return false;
		}
		if (!SVZMultiplayerManager.CanTeamSpawnUnit(helperTypeData.id, false))
		{
			return false;
		}
		float num = 0f;
		if (helperTypeData.smithyCost.smithy > 0f)
		{
			num = Mathf.Min(1f, mResources / helperTypeData.smithyCost.smithy);
		}
		else if (helperTypeData.smithyCost.canAfford)
		{
			num = 1f;
		}
		return GetCoolDown(typeIndex) == 1f && num == 1f;
	}

	public Helper Spawn(int typeIndex)
	{
		if (typeIndex < 0 || typeIndex >= mHelperTypes.Count)
		{
			Debug.Log("ERROR: Trying to spawn an out of bound helper index: " + typeIndex);
			return null;
		}
		HelperTypeData helperTypeData = mHelperTypes[typeIndex];
		if (!SVZMultiplayerManager.CanTeamSpawnUnit(helperTypeData.id, false))
		{
			return null;
		}
		float spawnSize;
		Vector3 spawnPos;
		if (!TryGetSpawnArea(out spawnSize, out spawnPos) || helperTypeData.prefab == null)
		{
			Debug.LogError("Smithy: cannot spawn helper " + helperTypeData.id + ". spawnArea=" + (mHelperSpawnArea != null) + " prefab=" + (helperTypeData.prefab != null) + " path=" + helperTypeData.prefabPath);
			return null;
		}
		if (!(helperTypeData.id == "Farmer") && !(helperTypeData.id == "Swordsmith"))
		{
			Singleton<PlayStatistics>.instance.stats.summonedTroopOtherThanFarmerOrSwordsmith = true;
		}
		switch (helperTypeData.id)
		{
		case "Farmer":
			Singleton<Profile>.instance.hasSummonedFarmer = true;
			Singleton<PlayStatistics>.instance.stats.summonedTroopFarmer = true;
			break;
		case "HeavySamurai":
			Singleton<Profile>.instance.hasSummonedPanzerSamurai = true;
			break;
		case "IceArcher":
			Singleton<Profile>.instance.hasSummonedFrostie = true;
			break;
		case "Nobunaga":
			Singleton<Profile>.instance.hasSummonedNobunaga = true;
			break;
		case "Priest":
			Singleton<Profile>.instance.hasSummonedPriest = true;
			break;
		case "Samurai":
			Singleton<Profile>.instance.hasSummonedSwordWarrior = true;
			break;
		case "SamuraiArcher":
			Singleton<Profile>.instance.hasSummonedBowman = true;
			break;
		case "Rifleman":
			Singleton<Profile>.instance.hasSummonedRifleman = true;
			break;
		case "SpearHorseman":
			Singleton<Profile>.instance.hasSummonedSpearHorseman = true;
			break;
		case "SpearSamurai":
			Singleton<Profile>.instance.hasSummonedSpearWarrior = true;
			break;
		case "Swordsmith":
			Singleton<Profile>.instance.hasSummonedSwordsmith = true;
			Singleton<PlayStatistics>.instance.stats.summonedTroopSwordsmith = true;
			break;
		case "Takeda":
			Singleton<Profile>.instance.hasSummonedTakeda = true;
			break;
		}
		if (!SVZMultiplayerManager.DisableEliteLimitsStatic && helperTypeData.unique)
		{
			if (IsUniqueAlive(helperTypeData.id))
			{
				Debug.Log("ERROR: Trying to spawn a second copy of a unique helper!");
				return null;
			}
			RegisterUnique(helperTypeData.id, true);
		}
		Singleton<Analytics>.instance.LogEvent("AllySummoned", Singleton<Profile>.instance.waveToBeat.ToString(), typeIndex);
		helperTypeData.smithyCost.Spend();
		helperTypeData.spawnBurstCount++;
		helperTypeData.totalCooldown = SVZMultiplayerManager.GetUniversalSpawnCooldown(helperTypeData.id, helperTypeData.spawnBurstCount, helperTypeData.baseCooldown);
		helperTypeData.EngageCooldown();
		Helper summoned = SpawnHelper(helperTypeData, spawnSize, spawnPos);
		if (summoned != null && SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZNetworkManager.Instance != null && SVZNetworkManager.Instance.IsHost)
		{
			SVZNetworkManager.Instance.SendPacket("P1_SUMMON", helperTypeData.id);
		}
		return summoned;
	}

	public int GetRandomHelperToSpawn()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < mHelperTypes.Count; i++)
		{
			if (SVZMultiplayerManager.DisableEliteLimitsStatic || !mHelperTypes[i].unique)
			{
				list.Add(i);
			}
		}
		if (list.Count == 0)
		{
			return -1;
		}
		return list[UnityEngine.Random.Range(0, list.Count)];
	}

	public Helper SpawnForFree(int typeIndex, float sizeOfSpawnArea, Vector3 spawnPos)
	{
		if (typeIndex < 0 || typeIndex >= mHelperTypes.Count)
		{
			Debug.Log("ERROR: Trying to spawn an out of bound helper index: " + typeIndex);
			return null;
		}
		HelperTypeData data = mHelperTypes[typeIndex];
		return SpawnHelper(data, sizeOfSpawnArea, spawnPos);
	}

	public Helper ForceSpawn(string helperID)
	{
		float spawnSize;
		Vector3 spawnPos;
		if (!TryGetSpawnArea(out spawnSize, out spawnPos))
		{
			Debug.LogError("Smithy: cannot force spawn helper without a spawn area: " + helperID);
			return null;
		}
		return SpawnHelper(LoadHelperData(helperID), spawnSize, spawnPos);
	}

	public Helper ForceSpawnAt(string helperID, Vector3 spawnPos, bool isEnemyTeam)
	{
		HelperTypeData data = LoadHelperData(helperID);
		if (data == null || data.prefab == null) return null;
		if (mCharManagerRef == null) mCharManagerRef = WeakGlobalInstance<CharactersManager>.instance;
		if (mCharManagerRef == null) return null;

		InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
		float leftZ = (inGame != null && inGame.enemiesTargetLeft != null) ? inGame.enemiesTargetLeft.transform.position.z : ((inGame != null && inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 0f);
		float rightZ = (inGame != null && inGame.enemiesTargetRight != null) ? inGame.enemiesTargetRight.transform.position.z : ((inGame != null && inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 3800f);
		bool marchRight = SVZMultiplayerManager.ShouldUnitMarchRight(isEnemyTeam, mIsLeftToRightGameplay);
		float zTarget = marchRight ? rightZ : leftZ;
		float areaWidth = (mHelperSpawnArea != null) ? mHelperSpawnArea.size.x : 100f;
		spawnPos.x = mCharManagerRef.GetBestSpawnXPos(spawnPos, areaWidth, data.lanePref, isEnemyTeam, data.isFlying, data.bowAttackRange > 0f);

		Helper helper = new Helper(data.prefab, zTarget, spawnPos, data.weaponPrefabLocation, data.rangedWeapon);
		InitializeHelper(ref data, helper, false);
		helper.isEnemy = isEnemyTeam;
		helper.controller.constraintLeft = Mathf.Min(leftZ, spawnPos.z);
		helper.controller.constraintRight = Mathf.Max(rightZ, spawnPos.z);
		// Stop any walk/backpedal started in constructor (isEnemy was wrong there), then restart correctly
		helper.controller.StopWalking();
		if (marchRight)
		{
			helper.controller.facing = FacingType.Right;
			helper.controller.StartWalkRight();
		}
		else
		{
			helper.controller.facing = FacingType.Left;
			helper.controller.StartWalkLeft();
		}
		if (isEnemyTeam)
		{
			helper.RefreshHealthBarColor();
		}

		if (data.spawnOnDeathType != null && data.spawnOnDeathType != string.Empty && data.spawnOnDeathNum > 0)
		{
			helper.SetSpawnOnDeath(data.spawnOnDeathType, data.spawnOnDeathNum);
		}
		mCharManagerRef.AddCharacter(helper);
		return helper;
	}

	public void ReplaceHelperWith(Character c, string helperID)
	{
		if (c != null && !(c.health <= 0f) && c is Helper)
		{
			bool wasEnemy = c.isEnemy;
			int helperLevel = Singleton<Profile>.instance.GetHelperLevel(helperID);
			int donorLevel = Mathf.Max(1, Singleton<Profile>.instance.GetHelperLevel(c.uniqueID));
			Singleton<Profile>.instance.SetHelperLevel(helperID, donorLevel, false);
			mHelperDataCache.Remove(helperID);
			HelperTypeData data = LoadHelperData(helperID);
			if (data != null && data.prefab != null)
			{
				((Helper)c).ReinitializeModel(data.prefab, data.weaponPrefabLocation, data.rangedWeapon);
				InitializeHelper(ref data, c, true);
				c.isEnemy = wasEnemy;
				if (wasEnemy)
				{
					c.RefreshHealthBarColor();
				}
			}
			Singleton<Profile>.instance.SetHelperLevel(helperID, helperLevel, false);
		}
	}

	public void LevelUp()
	{
		if (isUpgradable)
		{
			mResources -= mLevelUpThreshold;
			SetSmithyLevel(mLevel + 1);
		}
	}

	public void RegisterUnique(string id, bool isAlive)
	{
		if (isAlive)
		{
			if (!IsUniqueAlive(id))
			{
				mUniquesAlive.Add(id);
			}
		}
		else if (IsUniqueAlive(id))
		{
			mUniquesAlive.Remove(id);
		}
	}

	private HelperTypeData LoadHelperData(string helperID)
	{
		if (string.IsNullOrEmpty(helperID) || Singleton<HelpersDatabase>.instance == null || !Singleton<HelpersDatabase>.instance.Contains(helperID))
		{
			return null;
		}
		if (!mHelperDataCache.ContainsKey(helperID))
		{
			HelperTypeData helperTypeData = new HelperTypeData(helperID);
			mHelperDataCache.Add(helperID, helperTypeData);
			if (helperTypeData.spawnOnDeathType != string.Empty)
			{
				LoadHelperData(helperTypeData.spawnOnDeathType);
			}
			if (helperTypeData.spawnFriendID != string.Empty)
			{
				LoadHelperData(helperTypeData.spawnFriendID);
			}
		}
		return mHelperDataCache[helperID];
	}

	private static Dictionary<string, GameObject> sLoadedPrefabCache = new Dictionary<string, GameObject>();

	private static GameObject LoadPrefab(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}
		GameObject cached;
		if (sLoadedPrefabCache.TryGetValue(path, out cached) && cached != null)
		{
			return cached;
		}
		GameObject prefab = Resources.Load(path) as GameObject;
		if (prefab != null)
		{
			sLoadedPrefabCache[path] = prefab;
			return prefab;
		}
		int slash = path.IndexOf('/');
		if (slash > 0)
		{
			string fallback = path.Substring(0, slash).ToLower() + path.Substring(slash);
			prefab = Resources.Load(fallback) as GameObject;
			if (prefab != null)
			{
				sLoadedPrefabCache[path] = prefab;
			}
		}
		return prefab;
	}

	private bool TryGetSpawnArea(out float sizeOfSpawnArea, out Vector3 spawnPos)
	{
		if (mHelperSpawnArea != null)
		{
			sizeOfSpawnArea = mHelperSpawnArea.size.x;
			spawnPos = mHelperSpawnArea.transform.position;
			return true;
		}
		InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
		if (inGame != null && inGame.helpersSpawnArea != null)
		{
			mHelperSpawnArea = inGame.helpersSpawnArea;
			sizeOfSpawnArea = mHelperSpawnArea.size.x;
			spawnPos = mHelperSpawnArea.transform.position;
			return true;
		}
		if (inGame != null && inGame.enemiesSpawnArea != null)
		{
			mHelperSpawnArea = inGame.enemiesSpawnArea;
			sizeOfSpawnArea = mHelperSpawnArea.size.x;
			spawnPos = mHelperSpawnArea.transform.position;
			Debug.LogWarning("Smithy: helper spawn area missing, falling back to enemy spawn area.");
			return true;
		}
		sizeOfSpawnArea = 0f;
		spawnPos = Vector3.zero;
		return false;
	}

	private Helper SpawnHelper(HelperTypeData data, float sizeOfSpawnArea, Vector3 spawnPos)
	{
		if (data == null || data.prefab == null)
		{
			Debug.LogError("Smithy: SpawnHelper canceled because helper data or prefab is missing.");
			return null;
		}
		if (mCharManagerRef == null)
		{
			mCharManagerRef = WeakGlobalInstance<CharactersManager>.instance;
		}
		if (mCharManagerRef == null)
		{
			Debug.LogError("Smithy: SpawnHelper canceled because CharactersManager is missing.");
			return null;
		}
		Singleton<PlayStatistics>.instance.stats.heroInvokedHelpers = true;
		bool marchRight = SVZMultiplayerManager.ShouldUnitMarchRight(false, mIsLeftToRightGameplay);
		float num = Mathf.Max(data.swordAttackRange, data.bowAttackRange);
		float zTarget = ((!marchRight) ? (mHelpersZTarget + num) : (mHelpersZTarget - num));
		spawnPos.x = WeakGlobalInstance<CharactersManager>.instance.GetBestSpawnXPos(spawnPos, sizeOfSpawnArea, data.lanePref, false, false, data.bowAttackRange > 0f);
		Helper helper = new Helper(data.prefab, zTarget, spawnPos, data.weaponPrefabLocation, data.rangedWeapon);
		InitializeHelper(ref data, helper, false);
		if (data.spawnOnDeathType != null && data.spawnOnDeathType != string.Empty && data.spawnOnDeathNum > 0)
		{
			helper.SetSpawnOnDeath(data.spawnOnDeathType, data.spawnOnDeathNum);
		}
		mCharManagerRef.AddCharacter(helper);
		return helper;
	}

	private void InitializeHelper(ref HelperTypeData data, Character h, bool asReplacement)
	{
		h.uniqueID = data.id;
		h.isUnique = data.unique;
		h.meleeAttackFrequency = data.attackFrequency;
		h.bowAttackFrequency = data.attackFrequency;
		h.meleeAttackRange = data.swordAttackRange;
		h.bowAttackRange = data.bowAttackRange;
		h.bowProjectile = data.projectile;
		h.meleeDamage = data.meleeDamage;
		h.bowDamage = data.bowDamage;
		h.knockbackPower = data.knockbackPower;
		h.knockbackResistance = data.knockbackResistance;
		h.isFlying = data.isFlying;
		h.autoHealthRecovery = ((data.autoHealthRecovery == 0f) ? mSharedUndeathRegeneration : data.autoHealthRecovery);
		h.exploseOnMelee = data.exploseOnMelee;
		h.meleeFreeze = data.meleeFreeze;
		h.controller.speed = UnityEngine.Random.value * (data.speedMax - data.speedMin) + data.speedMin;
		h.meleeWeaponIsABlade = data.bladedWeapon;
		h.spawnFriendID = data.spawnFriendID;
		if (asReplacement)
		{
			float ratio = (h.maxHealth > 0f) ? (h.health / h.maxHealth) : 1f;
			if (float.IsNaN(ratio) || ratio <= 0f) ratio = 1f;
			h.maxHealth = Mathf.Max(data.health, 50f);
			h.health = Mathf.Max(1f, h.maxHealth * Mathf.Clamp01(ratio));
			if (mHelperReplaceEffect != null)
			{
				UnityEngine.Object.Instantiate(mHelperReplaceEffect, h.position, Quaternion.identity);
			}
		}
		else
		{
			h.maxHealth = data.health;
			h.health = data.health;
			if (mSpawnEffect != null)
			{
				UnityEngine.Object.Instantiate(mSpawnEffect, h.position, Quaternion.identity);
			}
		}
		h.damageBuffPercent = data.damageBuffPercent;
		h.upgradeAlliesFrom = data.upgradeAlliesFrom;
		h.upgradeAlliesTo = data.upgradeAlliesTo;
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "haste" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+haste"))
		{
			h.buffIcon.Show("FX/SpeedIcon");
		}
		if (!string.IsNullOrEmpty(data.auraFX))
		{
			GameObject gameObject = LoadPrefab(data.auraFX);
			if (gameObject != null)
			{
				string theJointLabel = (!string.IsNullOrEmpty(data.auraJoint)) ? data.auraJoint : "body_effect";
				h.controller.SpawnEffectAtJoint(gameObject, theJointLabel, true);
			}
			else
			{
				Debug.LogWarning("Smithy: missing helper auraFX for " + data.id + ": " + data.auraFX);
			}
		}
	}

	public void EnforceMaxCapacityAndRate(float maxRes, float rate)
	{
		mLevel = 4;
		mMaxResources = maxRes;
		mLevelUpThreshold = 9999f;
		mIncreaseRate = rate;
	}

	private void SetSmithyLevel(int level)
	{
		mLevel = Mathf.Clamp(level, 0, mMaxLevel);
		mLevelData = null;
		for (int num = mLevel; num > 0; num--)
		{
			mLevelData = mSmithyData.to(string.Format("{0:000}", mLevel));
			if (mLevelData != null)
			{
				break;
			}
		}
		mIncreaseRate = float.Parse(mLevelData["resourcesPerSeconds"], CultureInfo.InvariantCulture);
		mMaxResources = float.Parse(mLevelData["maxResource"], CultureInfo.InvariantCulture);
		mLevelUpThreshold = float.Parse(mLevelData["levelUpThreshold"], CultureInfo.InvariantCulture);
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
			{
				mLevel = 4;
				mMaxResources = 225f;
				mLevelUpThreshold = 9999f;
				mIncreaseRate = 8f;
				return;
			}
			switch (mLevel)
			{
			case 1:
				mMaxResources = 50f;
				mLevelUpThreshold = 30f;
				break;
			case 2:
				mMaxResources = 65f;
				mLevelUpThreshold = 50f;
				break;
			case 3:
				mMaxResources = 100f;
				mLevelUpThreshold = 80f;
				break;
			default:
				mMaxResources = 135f;
				mLevelUpThreshold = 140f;
				break;
			}
		}
	}

	private bool IsUniqueAlive(string id)
	{
		return mUniquesAlive.Contains(id);
	}
}
