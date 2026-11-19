using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class WaveManager : WeakGlobalInstance<WaveManager>
{
	private enum CommandType
	{
		Spawn = 0,
		Delay = 1,
		LegionTag = 2,
		UserDefined = 3,
		Num = 4,
		Unknown = 5
	}

	public class WaveRecyclingMultipliers
	{
		public float enemiesHealth = 1f;

		public float enemiesDamages = 1f;

		public float drops = 1f;
	}

	private const float kSpawnDelayTimerMin = 1f;

	private const float kSpawnDelayTimerMax = 4f;

	private const int kEnemiesMax = 10;

	private int mWaveIndex;

	private BoxCollider mEnemiesSpawnArea;

	private float mZTarget;

	private SDFTreeNode waveRootData;

	private SDFTreeNode waveCommands;

	private SDFTreeNode enemiesData;

	private SDFTreeNode rewards;

	private GameObject mSpawnEffect;

	private GameObject mAltSpawnEffect;

	private Dictionary<string, GameObject> mCachedPrefabs = new Dictionary<string, GameObject>();

	private WaveRecyclingMultipliers mLevelMultipliers = new WaveRecyclingMultipliers();

	private int mNextCommandToRun;

	private float mSpawnDelayTimer;

	private bool mSkipNextLegion;

	private string mTutorial = string.Empty;

	private int mTotalNumEnemies;

	private int mSpawnedEnemiesSoFar;

	private int mEnemiesKilledSoFar;

	private List<string> mAllDifferentEnemies = new List<string>();

	private List<int> mLegionMarkers = new List<int>();

	private int mVillageArchersLevel;

	private int mBellLevel;

	public bool isDone
	{
		get
		{
			return mNextCommandToRun >= waveCommands.attributeCount;
		}
	}

	public int totalEnemies
	{
		get
		{
			return mTotalNumEnemies;
		}
		set
		{
			mTotalNumEnemies = value;
		}
	}

	public int enemiesKilledSoFar
	{
		get
		{
			return mEnemiesKilledSoFar;
		}
	}

	public List<string> allDifferentEnemies
	{
		get
		{
			return mAllDifferentEnemies;
		}
	}

	public bool skipNextLegion
	{
		get
		{
			return mSkipNextLegion;
		}
		set
		{
			mSkipNextLegion = value;
		}
	}

	public BoxCollider enemiesSpawnArea
	{
		get
		{
			return mEnemiesSpawnArea;
		}
	}

	public List<int> legionMarkers
	{
		get
		{
			return mLegionMarkers;
		}
	}

	public int waveLevel
	{
		get
		{
			return mWaveIndex;
		}
	}

	public string tutorial
	{
		get
		{
			return mTutorial;
		}
	}

	public WaveRecyclingMultipliers multipliers
	{
		get
		{
			return mLevelMultipliers;
		}
	}

	public int villageArchersLevel
	{
		get
		{
			return mVillageArchersLevel;
		}
	}

	public int bellLevel
	{
		get
		{
			return mBellLevel;
		}
	}

	public event OnSUIGenericCallback onLegionStart;

	public WaveManager(int waveIndex, BoxCollider enemiesSpawnArea, float zTarget)
	{
		SetUniqueInstance(this);
		mWaveIndex = waveIndex;
		mEnemiesSpawnArea = enemiesSpawnArea;
		mZTarget = zTarget;
		LoadData();
		AnalyseWaveCommandsForStats();
		mNextCommandToRun = 0;
		mEnemiesKilledSoFar = 0;
		mSpawnEffect = Resources.Load("FX/TeleportEnemy") as GameObject;
		mAltSpawnEffect = Resources.Load("FX/Divine") as GameObject;
	}

	public void Update()
	{
		UpdateDelayTimer();
	}

	public static void LoadNextWaveLevel()
	{
		Singleton<Profile>.instance.ClearBonusWaveData();
		if (Singleton<Profile>.instance.readyToStartBonusWave)
		{
			SDFTreeNode waveData = GetWaveData(Singleton<Profile>.instance.bonusWaveToBeat, true);
			Singleton<Profile>.instance.LoadBonusWaveData(waveData);
			Singleton<MenusFlow>.instance.LoadScene(waveData["scene"]);
			SingletonMonoBehaviour<WaitingIconBetweenScenes>.instance.visible = true;
		}
		else
		{
			SDFTreeNode waveData2 = GetWaveData(Singleton<Profile>.instance.waveToBeat);
			Singleton<MenusFlow>.instance.LoadScene(waveData2["scene"]);
			SingletonMonoBehaviour<WaitingIconBetweenScenes>.instance.visible = true;
		}
	}

	public void registerEnemyKilled(string enemyID)
	{
		mEnemiesKilledSoFar++;
		Singleton<Profile>.instance.IncNumKillsOfEnemyType(enemyID);
	}

	public static string GetNextWaveNumberDisplay()
	{
		int waveToBeat = Singleton<Profile>.instance.waveToBeat;
		return GetWaveNumberDisplay(waveToBeat, Singleton<Profile>.instance.GetWaveLevel(waveToBeat));
	}

	public static string GetWaveNumberDisplay(int waveNum, int waveLevel)
	{
		if (waveLevel <= 1)
		{
			return waveNum.ToString();
		}
		return string.Format("{0}-{1}", waveNum.ToString(), waveLevel.ToString());
	}

	private SDFTreeNode mCachedNormalEnemies;
	private SDFTreeNode mCachedZmEnemies;

	private SDFTreeNode ResolveEnemyNode(string enemyID)
	{
		if (string.IsNullOrEmpty(enemyID)) return null;
		SDFTreeNode node = (enemiesData != null) ? enemiesData.to(enemyID) : null;
		if (node == null)
		{
			if (mCachedNormalEnemies == null && SingletonMonoBehaviour<ResourcesManager>.instance != null)
			{
				mCachedNormalEnemies = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Enemies");
			}
			if (mCachedNormalEnemies != null) node = mCachedNormalEnemies.to(enemyID);
		}
		if (node == null)
		{
			if (mCachedZmEnemies == null && SingletonMonoBehaviour<ResourcesManager>.instance != null)
			{
				mCachedZmEnemies = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Enemies_ZM");
			}
			if (mCachedZmEnemies != null) node = mCachedZmEnemies.to(enemyID);
		}
		return node;
	}

	private void PreloadEnemyById(string enemyID)
	{
		SDFTreeNode sDFTreeNode = ResolveEnemyNode(enemyID);
		if (sDFTreeNode == null) return;
		string prefabPath = sDFTreeNode["prefab"];
		if (!string.IsNullOrEmpty(prefabPath))
		{
			WeakGlobalInstance<PrefabPreloader>.instance.Preload(prefabPath);
		}
		if (sDFTreeNode.hasAttribute("weaponMeleePrefab"))
		{
			string meleePath = sDFTreeNode["weaponMeleePrefab"];
			if (!string.IsNullOrEmpty(meleePath) && !mCachedPrefabs.ContainsKey(meleePath))
			{
				mCachedPrefabs[meleePath] = Resources.Load(meleePath) as GameObject;
			}
		}
		if (sDFTreeNode.hasAttribute("weaponRangedPrefab"))
		{
			string rangedPath = sDFTreeNode["weaponRangedPrefab"];
			if (!string.IsNullOrEmpty(rangedPath) && !mCachedPrefabs.ContainsKey(rangedPath))
			{
				mCachedPrefabs[rangedPath] = Resources.Load(rangedPath) as GameObject;
			}
		}
	}

	public void PreloadEnemies()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			List<string> p1Deck = SVZMultiplayerManager.Instance.P1Deck;
			for (int i = 0; i < p1Deck.Count; i++)
			{
				if (SVZMultiplayerManager.IsZombieUnit(p1Deck[i]))
				{
					PreloadEnemyById(p1Deck[i]);
				}
			}
			List<string> p2Deck = SVZMultiplayerManager.Instance.P2Deck;
			for (int i = 0; i < p2Deck.Count; i++)
			{
				if (SVZMultiplayerManager.IsZombieUnit(p2Deck[i]))
				{
					PreloadEnemyById(p2Deck[i]);
				}
			}
			return;
		}
		foreach (string mAllDifferentEnemy in mAllDifferentEnemies)
		{
			SDFTreeNode sDFTreeNode = enemiesData.to(mAllDifferentEnemy);
			if (sDFTreeNode != null)
			{
				WeakGlobalInstance<PrefabPreloader>.instance.Preload(sDFTreeNode["prefab"]);
			}
		}
	}

	public SDFTreeNode GetEnemyData(string id)
	{
		return ResolveEnemyNode(id);
	}

	public void AddSpecialRewardsToCollectables()
	{
		if (Singleton<Profile>.instance.GetWaveLevel(mWaveIndex) != 1)
		{
			return;
		}
		if (rewards != null && rewards.attributeCount > 0)
		{
			for (int i = 0; i < rewards.attributeCount; i++)
			{
				string[] array = rewards[i].Split(',');
				switch (array[0])
				{
				case "coins":
					WeakGlobalInstance<CollectableManager>.instance.GiveResource(ECollectableType.coin, int.Parse(array[1]));
					break;
				case "gems":
					WeakGlobalInstance<CollectableManager>.instance.GiveResource(ECollectableType.gem, int.Parse(array[1]));
					break;
				case "balls":
					WeakGlobalInstance<CollectableManager>.instance.GiveResource(ECollectableType.pachinkoball, int.Parse(array[1]));
					break;
				default:
					WeakGlobalInstance<CollectableManager>.instance.GiveSpecificPresent(array[0]);
					break;
				}
			}
		}
		AddZombieMilestoneRewards();
	}

	private void AddZombieMilestoneRewards()
	{
		if (Singleton<PlayModesManager>.instance.selectedMode != "zombies" || mWaveIndex < 1 || mWaveIndex > 100 || mWaveIndex % 5 != 0)
		{
			return;
		}
		bool flag = mWaveIndex % 10 == 0;
		WeakGlobalInstance<CollectableManager>.instance.GiveResource(ECollectableType.coin, mWaveIndex * ((!flag) ? 150 : 300));
		int num = ((!flag) ? 1 : 2);
		for (int i = 0; i < num; i++)
		{
			WeakGlobalInstance<CollectableManager>.instance.GiveSpecificPresent(GetRandomZombieMilestoneCharm());
		}
		float num2 = ((!flag) ? 0.1f : 0.25f);
		if (UnityEngine.Random.value < num2)
		{
			WeakGlobalInstance<CollectableManager>.instance.GiveSpecificPresent("booster_free");
		}
	}

	private string GetRandomZombieMilestoneCharm()
	{
		string[] array = new string[5] { "z+power", "z+luck", "z+wealth", "z+haste", "z+peace" };
		return array[UnityEngine.Random.Range(0, array.Length)];
	}

	public static void ApplySpecialEvent(SDFTreeNode enemiesData)
	{
		if (Singleton<EventsManager>.instance.IsEventActive("halloween") && Singleton<PlayModesManager>.instance.selectedMode == "classic")
		{
			SDFTreeNode sDFTreeNode = enemiesData.to("L_Zombie_Melee");
			sDFTreeNode["prefab"] = "Characters/PFLzombiePumpkin";
			sDFTreeNode = enemiesData.to("L_Zombie_Melee_Tutorial");
			sDFTreeNode["prefab"] = "Characters/PFLzombiePumpkin";
		}
	}

	public static SDFTreeNode GetWaveData(int waveID)
	{
		return GetWaveData(waveID, false);
	}

	public static SDFTreeNode GetWaveData(int waveID, bool bonusWave)
	{
		int num = waveID;
		while (num > 0 && num < 1000)
		{
			string waveFilePath = GetWaveFilePath(num, bonusWave);
			if (Resources.Load(waveFilePath, typeof(TextAsset)) == null)
			{
				Debug.Log("WARNING: Could not find wave #" + num + ", will try next one or fallback.");
				num++;
				if (num > waveID + 5)
				{
					break;
				}
				continue;
			}
			return FilterEventData(SingletonMonoBehaviour<ResourcesManager>.instance.Open(waveFilePath));
		}
		return null;
	}

	private static SDFTreeNode FilterEventData(SDFTreeNode data)
	{
		if (Singleton<EventsManager>.instance.IsEventActive("halloween"))
		{
			if (data["scene"] == "InGame")
			{
				data["scene"] = "InGame_summernight";
			}
			else if (data["scene"] == "InGame_evil")
			{
				data["scene"] = "InGame_evilnight";
			}
			else if (data["scene"] == "InGame_fallday")
			{
				data["scene"] = "InGame_fallnight";
			}
			else if (data["scene"] == "InGame_springday")
			{
				data["scene"] = "InGame_springnight";
			}
			else if (data["scene"] == "InGame_winterday")
			{
				data["scene"] = "InGame_winternight";
			}
		}
		return data;
	}

	public static string GetWaveFilePath(int waveID)
	{
		return GetWaveFilePath(waveID, false);
	}

	public static string GetWaveFilePath(int waveID, bool bonusWave)
	{
		string text = "Registry/";
		if (!bonusWave && !Singleton<Profile>.instance.inBonusWave)
		{
			text += Singleton<PlayModesManager>.instance.selectedModeData["wavesPath"];
		}
		else
		{
			string selectedModeBonusWavesPath = Singleton<Profile>.instance.selectedModeBonusWavesPath;
			if (selectedModeBonusWavesPath == string.Empty)
			{
				selectedModeBonusWavesPath = "BonusWaves/BonusWave";
			}
			text += selectedModeBonusWavesPath;
		}
		return text + string.Format("{0:000}", waveID);
	}

	private void LoadData()
	{
		enemiesData = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Enemies");
		if (enemiesData != null)
		{
			ApplySpecialEvent(enemiesData);
		}
		waveRootData = GetWaveData(mWaveIndex);
		if (waveRootData == null)
		{
			Debug.LogError("WaveManager: waveRootData is null for wave " + mWaveIndex);
			waveCommands = new SDFTreeNode();
			rewards = new SDFTreeNode();
			return;
		}
		waveCommands = waveRootData.to("Commands");
		rewards = waveRootData.to("rewards");
		if (waveRootData.hasAttribute("tutorial"))
		{
			mTutorial = waveRootData["tutorial"];
		}
		if (waveRootData.hasAttribute("villageArchers"))
		{
			mVillageArchersLevel = int.Parse(waveRootData["villageArchers"], CultureInfo.InvariantCulture);
		}
		if (waveRootData.hasAttribute("bell"))
		{
			mBellLevel = int.Parse(waveRootData["bell"], CultureInfo.InvariantCulture);
		}
		LoadLevelMultipliers();
	}

	private void LoadLevelMultipliers()
	{
		int num = Singleton<Profile>.instance.GetWaveLevel(mWaveIndex);
		if (num >= 2)
		{
			SDFTreeNode sDFTreeNode = Singleton<Config>.instance.root.to("waveLevelMultipliers");
			mLevelMultipliers.enemiesHealth = CalcMultiplier(float.Parse(sDFTreeNode["enemiesHealth"], CultureInfo.InvariantCulture), num - 1);
			mLevelMultipliers.enemiesDamages = CalcMultiplier(float.Parse(sDFTreeNode["enemiesDamages"], CultureInfo.InvariantCulture), num - 1);
			mLevelMultipliers.drops = CalcMultiplier(float.Parse(sDFTreeNode["drops"], CultureInfo.InvariantCulture), num - 1);
		}
		// Dynamic Difficulty Scaling (Kolay / Normal / Zor / Kabus)
		mLevelMultipliers.enemiesHealth *= PCQoLManager.DifficultyHealthMultiplier;
		mLevelMultipliers.enemiesDamages *= PCQoLManager.DifficultyDamageMultiplier;
		mLevelMultipliers.drops *= PCQoLManager.DifficultyDropMultiplier;
	}

	private void RunNextCommand()
	{
		if (!waveCommands.hasAttribute(mNextCommandToRun))
		{
			return;
		}
		string text = waveCommands[mNextCommandToRun];
		mNextCommandToRun++;
		switch (GetCommandType(text))
		{
		case CommandType.UserDefined:
			WeakGlobalSceneBehavior<InGameImpl>.instance.RunSpecialWaveCommand(text.ToLower());
			break;
		case CommandType.Delay:
		{
			KeyValuePair<float, float> keyValuePair = ExtractTimer(text);
			mSpawnDelayTimer += UnityEngine.Random.value * (keyValuePair.Value - keyValuePair.Key) + keyValuePair.Key;
			break;
		}
		case CommandType.Spawn:
		{
			bool flag = false;
			foreach (string mAllDifferentEnemy in mAllDifferentEnemies)
			{
				if (string.Compare(mAllDifferentEnemy, text, true) == 0)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				WeakGlobalInstance<CharactersManager>.instance.AddCharacter(ConstructEnemy(text));
			}
			else
			{
				Debug.Log("WARNING: Unknown enemy ID in wave data: " + text);
			}
			break;
		}
		case CommandType.LegionTag:
			if (text[0] != '(')
			{
				break;
			}
			if (mSkipNextLegion)
			{
				SkipToEndOfLegion();
				mSkipNextLegion = false;
			}
			else if (mSpawnedEnemiesSoFar == mEnemiesKilledSoFar)
			{
				WeakGlobalSceneBehavior<InGameImpl>.instance.RunSpecialWaveCommand("@legionalert");
				if (this.onLegionStart != null)
				{
					this.onLegionStart();
				}
			}
			else
			{
				mNextCommandToRun--;
			}
			break;
		}
	}

	private void SkipToEndOfLegion()
	{
		while (mNextCommandToRun < waveCommands.attributeCount)
		{
			string text = waveCommands[mNextCommandToRun];
			CommandType commandType = GetCommandType(text);
			mNextCommandToRun++;
			switch (commandType)
			{
			case CommandType.Spawn:
				mEnemiesKilledSoFar++;
				break;
			case CommandType.LegionTag:
				if (text[0] == ')')
				{
					return;
				}
				break;
			}
		}
	}

	private KeyValuePair<float, float> ExtractTimer(string cmd)
	{
		string[] array = cmd.Split(',');
		if (array.Length != 2)
		{
			return new KeyValuePair<float, float>(-1f, -1f);
		}
		return new KeyValuePair<float, float>(float.Parse(array[0], CultureInfo.InvariantCulture), float.Parse(array[1], CultureInfo.InvariantCulture));
	}

	private void UpdateDelayTimer()
	{
		if (SVZCharacterLabController.IsLabWavePaused)
		{
			return;
		}
		if (isDone)
		{
			return;
		}
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			return;
		}
		mSpawnDelayTimer -= Time.deltaTime;
		if (!(mSpawnDelayTimer > 0f))
		{
			mSpawnDelayTimer = 0f;
			if (WeakGlobalInstance<CharactersManager>.instance != null && WeakGlobalInstance<CharactersManager>.instance.enemiesCount < GetEnemiesMax())
			{
				RunNextCommand();
			}
		}
	}

	private int GetEnemiesMax()
	{
		if (Singleton<PlayModesManager>.instance != null && Singleton<PlayModesManager>.instance.selectedMode == "zombies")
		{
			if (mWaveIndex >= 86)
			{
				return 42;
			}
			if (mWaveIndex >= 71)
			{
				return 36;
			}
			if (mWaveIndex >= 61)
			{
				return 30;
			}
			if (mWaveIndex >= 45)
			{
				return 24;
			}
			if (mWaveIndex >= 31)
			{
				return 18;
			}
		}
		return kEnemiesMax;
	}

	public Enemy SpawnEnemy(string enemyID)
	{
		Enemy e = ConstructEnemy(enemyID);
		if (e != null && WeakGlobalInstance<CharactersManager>.instance != null)
		{
			WeakGlobalInstance<CharactersManager>.instance.AddCharacter(e);
		}
		return e;
	}

	public Enemy ConstructEnemy(string enemyID)
	{
		SDFTreeNode sDFTreeNode = ResolveEnemyNode(enemyID);
		if (sDFTreeNode == null)
		{
			Debug.Log("ERROR: Unknown enemy: " + enemyID);
			return null;
		}

		float spawnWidth = 100f;
		Vector3 spawnPos = Vector3.zero;
		if (mEnemiesSpawnArea != null)
		{
			spawnWidth = mEnemiesSpawnArea.size.x;
			spawnPos = mEnemiesSpawnArea.transform.position;
		}
		else
		{
			InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
			if (inGame != null && inGame.heroWalkRightEdge != null)
			{
				spawnPos = inGame.heroWalkRightEdge.position;
			}
		}
		return SpawnEnemy(enemyID, sDFTreeNode, spawnWidth, spawnPos, false);
	}

	public Enemy ConstructEnemy(string enemyID, float sizeOfSpawnArea, Vector3 spawnPos, bool dynamicSpawn)
	{
		SDFTreeNode sDFTreeNode = ResolveEnemyNode(enemyID);
		if (sDFTreeNode == null)
		{
			Debug.Log("ERROR: Unkown enemy: " + enemyID);
			return null;
		}
		return SpawnEnemy(enemyID, sDFTreeNode, sizeOfSpawnArea, spawnPos, dynamicSpawn);
	}

	public Enemy ConstructEnemyAt(string enemyID, Vector3 spawnPos, bool isAllyTeam)
	{
		SDFTreeNode sDFTreeNode = ResolveEnemyNode(enemyID);
		if (sDFTreeNode == null)
		{
			Debug.Log("ERROR: Unknown enemy: " + enemyID);
			return null;
		}

		float areaWidth = (mEnemiesSpawnArea != null) ? mEnemiesSpawnArea.size.x : 100f;
		Enemy e = SpawnEnemy(enemyID, sDFTreeNode, areaWidth, spawnPos, false);
		if (e != null)
		{
			e.isEnemy = !isAllyTeam;
			InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
			float leftZ = (inGame != null && inGame.enemiesTargetLeft != null) ? inGame.enemiesTargetLeft.transform.position.z : ((inGame != null && inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 0f);
			float rightZ = (inGame != null && inGame.enemiesTargetRight != null) ? inGame.enemiesTargetRight.transform.position.z : ((inGame != null && inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 3800f);
			e.controller.constraintLeft = Mathf.Min(leftZ, spawnPos.z);
			e.controller.constraintRight = Mathf.Max(rightZ, spawnPos.z);

			bool marchRight = SVZMultiplayerManager.ShouldUnitMarchRight(e.isEnemy, true);
			e.controller.StopWalking();
			if (marchRight)
			{
				e.controller.facing = FacingType.Right;
				e.controller.StartWalkRight();
			}
			else
			{
				e.controller.facing = FacingType.Left;
				e.controller.StartWalkLeft();
			}
			if (isAllyTeam)
			{
				e.RefreshHealthBarColor();
			}

			if (WeakGlobalInstance<CharactersManager>.instance != null)
			{
				WeakGlobalInstance<CharactersManager>.instance.AddCharacter(e);
			}
		}
		return e;
	}

    public Enemy SpawnEnemy(string uniqueID, SDFTreeNode data, float sizeOfSpawnArea, Vector3 spawnPos, bool dynamicSpawn)
    {
        // --- BİZİM EKLEDİĞİMİZ HAYAT KURTARAN GÜVENLİK KODU ---
        if (data == null)
        {
            data = this.enemiesData.to(uniqueID); // Eğer data boşsa, Enemies.txt içinden ID'yi arayıp bulur!
            if (data == null) return null; // Eğer Enemies.txt içinde de bulamazsa oyunu çökertmeden iptal eder.
        }
        // ------------------------------------------------------

        mSpawnedEnemiesSoFar++;
        bool result = false;
        bool.TryParse(data["flying"], out result);
        float result2 = 0f;
        float.TryParse(data["bowRange"], NumberStyles.Float, CultureInfo.InvariantCulture, out result2);
        CharactersManager.ELanePreference preference = CharactersManager.ELanePreference.any;
        if (data.hasAttribute("lane"))
        {
            preference = (CharactersManager.ELanePreference)(int)Enum.Parse(typeof(CharactersManager.ELanePreference), data["lane"]);
        }

        // ... Fonksiyonun geri kalanı sende nasılsa aynen öyle devam etsin ...
        spawnPos.x = WeakGlobalInstance<CharactersManager>.instance.GetBestSpawnXPos(spawnPos, sizeOfSpawnArea, preference, true, result, result2 > 0f);
		Enemy e = new Enemy(data["prefab"], mZTarget, spawnPos);
		e.uniqueID = uniqueID;
		UnityEngine.Object.Instantiate((!dynamicSpawn) ? mSpawnEffect : mAltSpawnEffect, e.position, Quaternion.identity);
		float tempFloat = 0f;
		float.TryParse(data["health"], NumberStyles.Float, CultureInfo.InvariantCulture, out tempFloat);
		e.maxHealth = tempFloat;
		e.health = e.maxHealth;
		float result3 = 0f;
		float.TryParse(data["meleeRange"], NumberStyles.Float, CultureInfo.InvariantCulture, out result3);
		e.meleeAttackRange = result3;
		float.TryParse(data["meleeDamage"], NumberStyles.Float, CultureInfo.InvariantCulture, out result3);
		e.meleeDamage = result3;
		e.bowAttackRange = result2;
		float.TryParse(data["bowDamage"], NumberStyles.Float, CultureInfo.InvariantCulture, out result3);
		e.bowDamage = result3;
		float.TryParse(data["damageBuffPercent"], NumberStyles.Float, CultureInfo.InvariantCulture, out result3);
		e.damageBuffPercent = result3;
		if (data.hasAttribute("projectile"))
		{
			e.bowProjectile = (Projectile.EProjectileType)(int)Enum.Parse(typeof(Projectile.EProjectileType), data["projectile"]);
		}
		bool result4 = false;
		bool.TryParse(data["usesBladeWeapon"], out result4);
		e.meleeWeaponIsABlade = result4;
		bool.TryParse(data["gateRusher"], out result4);
		e.isGateRusher = result4;
		bool.TryParse(data["exploseOnMelee"], out result4);
		e.exploseOnMelee = result4;
		float.TryParse(data["meleeFreeze"], NumberStyles.Float, CultureInfo.InvariantCulture, out result3);
		e.meleeFreeze = result3;
		e.isFlying = result;
		float.TryParse(data["autoHealthRecovery"], NumberStyles.Float, CultureInfo.InvariantCulture, out result3);
		e.autoHealthRecovery = result3;
		bool.TryParse(data["boss"], out result4);
		e.isBoss = result4;
		float.TryParse(data["attackFrequency"], NumberStyles.Float, CultureInfo.InvariantCulture, out tempFloat);
		e.meleeAttackFrequency = tempFloat;
		e.bowAttackFrequency = e.meleeAttackFrequency;
		e.spawnFriendID = data["spawnFriendID"];
		float num = 0f;
		float.TryParse(data["speedMin"], NumberStyles.Float, CultureInfo.InvariantCulture, out num);
		float num2 = 0f;
		float.TryParse(data["speedMax"], NumberStyles.Float, CultureInfo.InvariantCulture, out num2);
		e.controller.speed = UnityEngine.Random.value * (num2 - num) + num;
		if (data.hasAttribute("knockbackPower"))
		{
			e.knockbackPower = int.Parse(data["knockbackPower"]);
		}
		if (data.hasAttribute("knockbackResistance"))
		{
			e.knockbackResistance = int.Parse(data["knockbackResistance"]);
		}
		e.resourceDrops.amountDropped.max = int.Parse(data["resourceDropMax"]);
		e.resourceDrops.amountDropped.min = int.Parse(data["resourceDropMin"]);
		e.resourceDrops.guaranteedCoinsAward = int.Parse(data["award"]);
		e.maxHealth *= mLevelMultipliers.enemiesHealth;
		e.health = e.maxHealth;
		e.meleeDamage *= mLevelMultipliers.enemiesDamages;
		e.bowDamage *= mLevelMultipliers.enemiesDamages;
		if (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "peace" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+peace"))
		{
			float num3 = float.Parse(Singleton<CharmsDatabase>.instance.GetAttribute(WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm, "damageMultiplier"), CultureInfo.InvariantCulture);
			e.meleeDamage *= num3;
			e.bowDamage *= num3;
		}
		if (data.hasAttribute("weaponMeleePrefab"))
		{
			string meleePath = data["weaponMeleePrefab"];
			if (!mCachedPrefabs.ContainsKey(meleePath))
			{
				mCachedPrefabs[meleePath] = Resources.Load(meleePath) as GameObject;
			}
			e.meleeWeaponPrefab = mCachedPrefabs[meleePath];
		}
		if (e.meleeWeaponPrefab != null && (WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm == "peace" || WeakGlobalSceneBehavior<InGameImpl>.instance.activeCharm.Contains("+peace")))
		{
			string chickenPath = "Props/PFChickenSword";
			if (!mCachedPrefabs.ContainsKey(chickenPath))
			{
				mCachedPrefabs[chickenPath] = Resources.Load(chickenPath) as GameObject;
			}
			e.meleeWeaponPrefab = mCachedPrefabs[chickenPath];
		}
		if (data.hasAttribute("weaponRangedPrefab"))
		{
			string rangedPath = data["weaponRangedPrefab"];
			if (!mCachedPrefabs.ContainsKey(rangedPath))
			{
				mCachedPrefabs[rangedPath] = Resources.Load(rangedPath) as GameObject;
			}
			e.rangedWeaponPrefab = mCachedPrefabs[rangedPath];
		}
		if (data.hasAttribute("spawnOnDeath"))
		{
			int spawnCount = 1;
			if (data.hasAttribute("spawnOnDeathCount"))
			{
				spawnCount = int.Parse(data["spawnOnDeathCount"]);
			}
			Enemy enemy = e;
			enemy.onDeathEvent = (Action)Delegate.Combine(enemy.onDeathEvent, (Action)delegate
			{
				e.SpawnOffspring(data["spawnOnDeath"], spawnCount);
			});
		}
		if (data.hasAttribute("resourceDropAlways"))
		{
			Character selfPtr = e;
			Enemy enemy2 = e;
			enemy2.onDeathEvent = (Action)Delegate.Combine(enemy2.onDeathEvent, (Action)delegate
			{
				SpawnExtraCollectable(data["resourceDropAlways"], selfPtr);
			});
		}
		return e;
	}

	private void SpawnExtraCollectable(string collectableType, Character theEnemy)
	{
		if (theEnemy != null)
		{
			WeakGlobalInstance<CollectableManager>.instance.ForceSpawnResourceType(collectableType, theEnemy.position);
		}
	}

	private void AnalyseWaveCommandsForStats()
	{
		mTotalNumEnemies = 0;
		mAllDifferentEnemies.Clear();
		for (int i = 0; waveCommands.hasAttribute(i); i++)
		{
			string data = waveCommands[i].ToLower();
			switch (GetCommandType(data))
			{
			case CommandType.Spawn:
				mTotalNumEnemies++;
				if (enemiesData.to(data) == null)
				{
					Debug.Log("WARNING: Unknown enemy ID in wave: " + data);
				}
				else if (!mAllDifferentEnemies.Exists((string element) => element == data))
				{
					mAllDifferentEnemies.Add(data);
				}
				break;
			case CommandType.LegionTag:
				if (data[0] == '(')
				{
					mLegionMarkers.Add(mTotalNumEnemies);
				}
				break;
			}
		}
		Singleton<PlayStatistics>.instance.stats.lastWaveTotalEnemies = mTotalNumEnemies;
	}

	private CommandType GetCommandType(string cmd)
	{
		if (cmd.Length == 0)
		{
			return CommandType.Unknown;
		}
		if (cmd[0] == '@')
		{
			return CommandType.UserDefined;
		}
		if (cmd[0] == '(' || cmd[0] == ')')
		{
			return CommandType.LegionTag;
		}
		if (ExtractTimer(cmd).Key != -1f)
		{
			return CommandType.Delay;
		}
		return CommandType.Spawn;
	}

	private static float CalcMultiplier(float mult, int times)
	{
		float num = mult;
		num -= 1f;
		num *= (float)times;
		return num + 1f;
	}
}
