using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Profile : Singleton<Profile>
{
	public enum EItemType
	{
		eItemType_Charm = 0,
		eItemType_Potion = 1
	}

	private const int kMaxCurrencies = 999999999;

	private const int kMaxWaveLevel = 99;

	private const int kMoneyBagsAchievementAmount = 15000;

	private const int kDontSpendOnePlaceAchievementAmount = 10000;

	private const int kNumberOfFarmersToUpGradeForEveryAbleBodyAchievment = 25;

	private const int kNumberOfArchersToUpGradeForWeNeedShotgunsAchievment = 25;

	private const int kUndeadKilledViaTroopTrampleForClearTheRoomAchievement = 250;

	private const int kUndeadKilledViaKatanaSlashForRazorSharpAchievement = 100;

	private const int kWavesWithNoGateDamageForMustProtectHouseAchievment = 10;

	private const int kNumberOfOniToKillForSendThemBackAchievement = 50;

	private const int kNumberOfChochinobakeToKillForGetBackHereAchievement = 50;

	private const int kWaveToUnlockZombieMode = 10;

	private static readonly string[] kZombieBonusFallbackHelpers = new string[5] { "Zombie", "ZombieAxe", "ZombieArcher", "SmallOni", "ZombieDrummer" };

	private static readonly int[] kZombieBonusFallbackHelperLevels = new int[5] { 3, 3, 2, 2, 1 };

	private static readonly string[] kZombieBonusFallbackAbilities = new string[2] { "NightOfTheDead", "GraveHands" };

	private static readonly int[] kZombieBonusFallbackAbilityLevels = new int[2] { 3, 2 };

	private SavedData mSavedData;

	private SavedData mCloudSavedData;

	private SavedData mBonusWaveData;

	private bool mForcingBaseSaveData;

	private bool m_needSave;

	private bool m_inSaving;

	public bool neverShowRateMeAlertAgain
	{
		get
		{
			return mSavedData.GetValueBool("neverShowRateMeAlertAgain");
		}
		set
		{
			mSavedData.SetValueBool("neverShowRateMeAlertAgain", value);
		}
	}

	public bool wasBasicGameBeaten
	{
		get
		{
			return GetWaveLevel(maxBaseWave) > 1;
		}
	}

	public float timeStamp
	{
		get
		{
			return mSavedData.GetValueFloat("timeStamp");
		}
		set
		{
			mSavedData.SetValueFloat("timeStamp", value);
		}
	}

	public int timeRemainingUntilNotification
	{
		get
		{
			return mSavedData.GetValueInt("timeRemainingUntilNotification");
		}
		set
		{
			mSavedData.SetValueInt("timeRemainingUntilNotification", value);
		}
	}

	public int lastYearPlayed
	{
		get
		{
			return mSavedData.GetValueInt("lastYearPlayed");
		}
		set
		{
			mSavedData.SetValueInt("lastYearPlayed", value);
		}
	}

	public int lastMonthPlayed
	{
		get
		{
			return mSavedData.GetValueInt("lastMonthPlayed");
		}
		set
		{
			mSavedData.SetValueInt("lastMonthPlayed", value);
		}
	}

	public int lastDayPlayed
	{
		get
		{
			return mSavedData.GetValueInt("lastDayPlayed");
		}
		set
		{
			mSavedData.SetValueInt("lastDayPlayed", value);
		}
	}

	public string profileName
	{
		get
		{
			return mSavedData.GetValue("profileName");
		}
		set
		{
			mSavedData.SetValue("profileName", value);
		}
	}

	public string latestDetectedOnlineVersion
	{
		get
		{
			return mSavedData.GetValue("latestDetectedOnlineVersion");
		}
		set
		{
			mSavedData.SetValue("latestDetectedOnlineVersion", value);
		}
	}

	public bool newVersionDetected
	{
		get
		{
			return activeSavedData.GetValueBool("newVersionDetected");
		}
		set
		{
			mSavedData.SetValueBool("newVersionDetected", value);
		}
	}

	public int currentDailyRewardNumber
	{
		get
		{
			return mSavedData.GetValueInt("currentDailyRewardNumber");
		}
		set
		{
			mSavedData.SetValueInt("currentDailyRewardNumber", value);
		}
	}

	public bool tutorialIsComplete
	{
		get
		{
			return GetWaveLevel(3) > 0;
		}
	}

	public bool hasReportedUser
	{
		get
		{
			return mSavedData.GetValueBool("hasReportedUser");
		}
		set
		{
			mSavedData.SetValueBool("hasReportedUser", value);
		}
	}

	public bool hasReportedGemPurchase
	{
		get
		{
			return mSavedData.GetValueBool("hasReportedGemPurchase");
		}
		set
		{
			mSavedData.SetValueBool("hasReportedGemPurchase", value);
		}
	}

	public int latestOnlineBundleVersion
	{
		get
		{
			return mSavedData.GetValueInt("onlineBundleVersion");
		}
		set
		{
			mSavedData.SetValueInt("onlineBundleVersion", value);
		}
	}

	public bool hasWonPachinkoBefore
	{
		get
		{
			return mSavedData.GetValueBool("hasWonPachinkoBefore");
		}
		set
		{
			mSavedData.SetValueBool("hasWonPachinkoBefore", value);
		}
	}

	public int coins
	{
		get
		{
			return mSavedData.GetValueInt("coins");
		}
		set
		{
			if (value > 15000)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_MONEY_BAGS");
			}
			else if (value > 10000)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_DONT_SPEND_ONE_PLACE");
			}
			PlayerPrefs.SetInt("asvz.coins", Mathf.Clamp(value, 0, 999999999));
			mSavedData.SetValueInt("coins", Mathf.Clamp(value, 0, 999999999));
		}
	}

	public int purchasedCoins
	{
		get
		{
			return mSavedData.GetValueInt("purchasedCoins");
		}
		set
		{
			mSavedData.SetValueInt("purchasedCoins", Mathf.Clamp(value, 0, 999999999));
		}
	}

	public bool earnsDoubleCoins
	{
		get
		{
			return mSavedData.GetValueBool("earnsDoubleCoins");
		}
		set
		{
			mSavedData.SetValueBool("earnsDoubleCoins", value);
		}
	}

	public int gems
	{
		get
		{
			return mSavedData.GetValueInt("gems");
		}
		set
		{
			PlayerPrefs.SetInt("asvz.gems", Mathf.Clamp(value, 0, 999999999));
			mSavedData.SetValueInt("gems", Mathf.Clamp(value, 0, 999999999));
		}
	}

	public int purchasedGems
	{
		get
		{
			return mSavedData.GetValueInt("purchasedGems");
		}
		set
		{
			mSavedData.SetValueInt("purchasedGems", Mathf.Clamp(value, 0, 999999999));
		}
	}

	public int pachinkoBalls
	{
		get
		{
			return mSavedData.GetValueInt("pachinkoBalls");
		}
		set
		{
			mSavedData.SetValueInt("pachinkoBalls", Mathf.Clamp(value, 0, 999999999));
		}
	}

	public int pachinkoBallsLaunched
	{
		get
		{
			return mSavedData.GetValueInt("pachinkoBallsLaunched");
		}
		set
		{
			mSavedData.SetValueInt("pachinkoBallsLaunched", Mathf.Clamp(value, 0, 999999999));
		}
	}

	public int freeBoosterPacks
	{
		get
		{
			return mSavedData.GetValueInt("fbp");
		}
		set
		{
			mSavedData.SetValueInt("fbp", value);
		}
	}

	public int storeVisitCount
	{
		get
		{
			return mSavedData.GetValueInt("storeVisitsCount");
		}
		set
		{
			mSavedData.SetValueInt("storeVisitsCount", value);
		}
	}

	public int wavesSinceLastBonusWave
	{
		get
		{
			return mSavedData.GetValueInt(GetBonusWaveCounterKey());
		}
		set
		{
			mSavedData.SetValueInt(GetBonusWaveCounterKey(), value);
		}
	}

	public bool readyToStartBonusWave
	{
		get
		{
			if (!selectedModeSupportsBonusWaves)
			{
				return false;
			}
			int num = GetBonusWaveMaxBaseWave();
			if (num > 0 && waveToBeat - 1 > num)
			{
				return false;
			}
			if (num > 0)
			{
				int num2 = waveToBeat - 1;
				int wavesPerBonusWave = GetWavesPerBonusWave();
				if (num2 <= 0 || num2 % wavesPerBonusWave != 0)
				{
					return false;
				}
				int num3 = num2 / wavesPerBonusWave;
				int num4 = GetBonusWaveCount();
				if ((num4 > 0 && num3 > num4) || bonusWaveToBeat != num3)
				{
					return false;
				}
			}
			int num5 = GetBonusWaveCount();
			if (num5 > 0 && bonusWaveToBeat > num5)
			{
				return false;
			}
			return wavesSinceLastBonusWave >= GetWavesPerBonusWave();
		}
	}

	public int bonusWaveToBeat
	{
		get
		{
			return Mathf.Max(1, mSavedData.GetValueInt(GetBonusWaveToBeatKey()));
		}
		set
		{
			int val = ValidateBonusWaveIndex(value);
			mSavedData.SetValueInt(GetBonusWaveToBeatKey(), val);
		}
	}

	public bool selectedModeSupportsBonusWaves
	{
		get
		{
			return GetSelectedModeBonusWavesPath() != string.Empty;
		}
	}

	public string selectedModeBonusWavesPath
	{
		get
		{
			return GetSelectedModeBonusWavesPath();
		}
	}

	public void RegisterNormalWaveWinForBonusWave(int completedWave, int completedWaveLevel)
	{
		if (!selectedModeSupportsBonusWaves)
		{
			return;
		}
		if (!PrepareBonusWaveForCompletedWave(completedWave))
		{
			wavesSinceLastBonusWave++;
		}
	}

	public bool PrepareBonusWaveForCompletedWave(int completedWave)
	{
		if (!selectedModeSupportsBonusWaves)
		{
			return false;
		}
		int bonusWaveMaxBaseWave = GetBonusWaveMaxBaseWave();
		if (bonusWaveMaxBaseWave <= 0)
		{
			return false;
		}
		int wavesPerBonusWave = GetWavesPerBonusWave();
		if (completedWave <= 0 || completedWave > bonusWaveMaxBaseWave || completedWave % wavesPerBonusWave != 0)
		{
			wavesSinceLastBonusWave = 0;
			return false;
		}
		int num = completedWave / wavesPerBonusWave;
		int bonusWaveCount = GetBonusWaveCount();
		if (bonusWaveCount > 0 && num > bonusWaveCount)
		{
			wavesSinceLastBonusWave = 0;
			return false;
		}
		bonusWaveToBeat = num;
		wavesSinceLastBonusWave = wavesPerBonusWave;
		return true;
	}

	public int waveToBeat
	{
		get
		{
			if (inBonusWave)
			{
				return bonusWaveToBeat;
			}
			return Mathf.Max(1, mSavedData.GetValueInt("waveToBeat", playModeSubSection));
		}
		set
		{
			int num = ValidateWaveIndex(value);
			mSavedData.SetValueInt("waveToBeat", num, playModeSubSection);
			if (GetWaveLevel(num) == 0)
			{
				SetWaveLevel(num, 1);
			}
		}
	}

	public bool inBonusWave
	{
		get
		{
			return mBonusWaveData != null;
		}
	}

	public SavedData activeSavedData
	{
		get
		{
			return (!inBonusWave || mForcingBaseSaveData) ? mSavedData : mBonusWaveData;
		}
	}

	private string activeDataSubSection
	{
		get
		{
			return (inBonusWave && !mForcingBaseSaveData) ? null : playModeSubSection;
		}
	}

	public SavedData saveData
	{
		get
		{
			return mSavedData;
		}
	}

	public SavedData cloudSaveData
	{
		get
		{
			return mCloudSavedData;
		}
	}

	public int heroLevel
	{
		get
		{
			return Mathf.Max(1, mSavedData.GetValueInt("heroLevel", playModeSubSection));
		}
		set
		{
			mSavedData.SetValueInt("heroLevel", Mathf.Max(0, value), playModeSubSection);
		}
	}

	public string heroID
	{
		get
		{
			string value = mSavedData.GetValue("heroID", playModeSubSection);
			if (value == string.Empty)
			{
				return Singleton<PlayModesManager>.instance.selectedModeData["defaultHeroID"];
			}
			return value;
		}
		set
		{
			mSavedData.SetValue("heroID", value, playModeSubSection);
		}
	}

	public int initialSmithyLevel
	{
		get
		{
			return Mathf.Max(1, activeSavedData.GetValueInt("initialLeadershipLevel", activeDataSubSection));
		}
		set
		{
			mSavedData.SetValueInt("initialLeadershipLevel", Mathf.Max(1, value), playModeSubSection);
		}
	}

	public int swordLevel
	{
		get
		{
			return Mathf.Max(1, mSavedData.GetValueInt("swordLevel", playModeSubSection));
		}
		set
		{
			mSavedData.SetValueInt("swordLevel", Mathf.Max(0, value), playModeSubSection);
		}
	}

	public string swordID
	{
		get
		{
			string value = mSavedData.GetValue("swordID", playModeSubSection);
			if (value == string.Empty)
			{
				return Singleton<PlayModesManager>.instance.selectedModeData["defaultMeleeWeaponID"];
			}
			return value;
		}
		set
		{
			mSavedData.SetValue("swordID", value, playModeSubSection);
		}
	}

	public int bowLevel
	{
		get
		{
			return Mathf.Max(1, mSavedData.GetValueInt("bowLevel", playModeSubSection));
		}
		set
		{
			mSavedData.SetValueInt("bowLevel", Mathf.Max(0, value), playModeSubSection);
		}
	}

	public string bowID
	{
		get
		{
			string value = mSavedData.GetValue("bowID", playModeSubSection);
			if (value == string.Empty)
			{
				return Singleton<PlayModesManager>.instance.selectedModeData["defaultRangeWeaponID"];
			}
			return value;
		}
		set
		{
			mSavedData.SetValue("bowID", value, playModeSubSection);
		}
	}

	public int archerLevel
	{
		get
		{
			return activeSavedData.GetValueInt("archerLevel", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValueInt("archerLevel", value, playModeSubSection);
		}
	}

	public int baseLevel
	{
		get
		{
			return Mathf.Max(1, activeSavedData.GetValueInt("baseLevel", activeDataSubSection));
		}
		set
		{
			mSavedData.SetValueInt("baseLevel", Mathf.Max(0, value), playModeSubSection);
		}
	}

	public int bellLevel
	{
		get
		{
			return activeSavedData.GetValueInt("bellLevel", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValueInt("bellLevel", value, playModeSubSection);
		}
	}

	public int undeathLevel
	{
		get
		{
			return activeSavedData.GetValueInt("undeathLevel", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValueInt("undeathLevel", value, playModeSubSection);
		}
	}

	public int legionOnTheLooseLevel
	{
		get
		{
			return activeSavedData.GetValueInt("legionOnTheLoose", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValueInt("legionOnTheLoose", value, playModeSubSection);
		}
	}

	public static readonly int[] kLegionCooldownSeconds = new int[] { 40, 35, 30, 28, 26, 24, 22, 20, 18, 16, 15 };

	public static readonly int[] kLegionCooldownGemCosts = new int[] { 10, 15, 20, 25, 30, 35, 40, 50, 60, 75 };

	private static string GetModuleKeyPrefix(string moduleID)
	{
		return (moduleID == "DeathFromAbove") ? "deathFromAbove" : "legionOnTheLoose";
	}

	private static string GetModuleCooldownPrefix(string moduleID)
	{
		return (moduleID == "DeathFromAbove") ? "deathFromAboveCooldown" : "legionCooldown";
	}

	public int GetModuleUnlockedLevel(string moduleID)
	{
		return activeSavedData.GetValueInt(GetModuleKeyPrefix(moduleID), activeDataSubSection);
	}

	public void SetModuleUnlockedLevel(string moduleID, int level)
	{
		mSavedData.SetValueInt(GetModuleKeyPrefix(moduleID), level, playModeSubSection);
	}

	public int GetModuleSelectedLevel(string moduleID)
	{
		int maxLevel = GetModuleUnlockedLevel(moduleID);
		if (maxLevel <= 0)
		{
			return 0;
		}
		int selected = mSavedData.GetValueInt(GetModuleKeyPrefix(moduleID) + "Selected", playModeSubSection);
		if (selected <= 0 || selected > maxLevel)
		{
			return maxLevel;
		}
		return selected;
	}

	public void SetModuleSelectedLevel(string moduleID, int level)
	{
		int maxLevel = GetModuleUnlockedLevel(moduleID);
		int clamped = (maxLevel <= 0) ? 0 : ((level < 1) ? 1 : ((level > maxLevel) ? maxLevel : level));
		mSavedData.SetValueInt(GetModuleKeyPrefix(moduleID) + "Selected", clamped, playModeSubSection);
	}

	public bool IsModuleEnabled(string moduleID)
	{
		return !mSavedData.GetValueBool(GetModuleKeyPrefix(moduleID) + "Disabled", playModeSubSection);
	}

	public void SetModuleEnabled(string moduleID, bool enabled)
	{
		mSavedData.SetValueBool(GetModuleKeyPrefix(moduleID) + "Disabled", !enabled, playModeSubSection);
	}

	public int GetModuleCooldownLevel(string moduleID)
	{
		int val = mSavedData.GetValueInt(GetModuleCooldownPrefix(moduleID) + "Level", playModeSubSection);
		int maxIdx = kLegionCooldownSeconds.Length - 1;
		if (val < 0) return 0;
		if (val > maxIdx) return maxIdx;
		return val;
	}

	public void SetModuleCooldownLevel(string moduleID, int level)
	{
		int maxIdx = kLegionCooldownSeconds.Length - 1;
		int clamped = (level < 0) ? 0 : ((level > maxIdx) ? maxIdx : level);
		mSavedData.SetValueInt(GetModuleCooldownPrefix(moduleID) + "Level", clamped, playModeSubSection);
	}

	public int GetModuleCooldownSelectedLevel(string moduleID)
	{
		int maxUnlocked = GetModuleCooldownLevel(moduleID);
		int stored = mSavedData.GetValueInt(GetModuleCooldownPrefix(moduleID) + "Selected", playModeSubSection);
		if (stored <= 0)
		{
			return maxUnlocked;
		}
		int sel = stored - 1;
		if (sel < 0 || sel > maxUnlocked)
		{
			return maxUnlocked;
		}
		return sel;
	}

	public void SetModuleCooldownSelectedLevel(string moduleID, int level)
	{
		int maxUnlocked = GetModuleCooldownLevel(moduleID);
		int clamped = (level < 0) ? 0 : ((level > maxUnlocked) ? maxUnlocked : level);
		mSavedData.SetValueInt(GetModuleCooldownPrefix(moduleID) + "Selected", clamped + 1, playModeSubSection);
	}

	public float GetModuleCooldownSeconds(string moduleID)
	{
		return (float)GetLegionCooldownSecondsForTier(GetModuleCooldownSelectedLevel(moduleID));
	}

	public int GetModuleNextCooldownGemCost(string moduleID)
	{
		int lvl = GetModuleCooldownLevel(moduleID);
		if (lvl < 0 || lvl >= kLegionCooldownGemCosts.Length)
		{
			return -1;
		}
		return kLegionCooldownGemCosts[lvl];
	}

	public int legionOnTheLooseSelectedLevel
	{
		get { return GetModuleSelectedLevel("LegionOnTheLoose"); }
		set { SetModuleSelectedLevel("LegionOnTheLoose", value); }
	}

	public bool legionOnTheLooseEnabled
	{
		get { return IsModuleEnabled("LegionOnTheLoose"); }
		set { SetModuleEnabled("LegionOnTheLoose", value); }
	}

	public int legionCooldownLevel
	{
		get { return GetModuleCooldownLevel("LegionOnTheLoose"); }
		set { SetModuleCooldownLevel("LegionOnTheLoose", value); }
	}

	public int legionCooldownSelectedLevel
	{
		get { return GetModuleCooldownSelectedLevel("LegionOnTheLoose"); }
		set { SetModuleCooldownSelectedLevel("LegionOnTheLoose", value); }
	}

	public int GetLegionCooldownSecondsForTier(int tier)
	{
		if (tier < 0) tier = 0;
		if (tier >= kLegionCooldownSeconds.Length) tier = kLegionCooldownSeconds.Length - 1;
		return kLegionCooldownSeconds[tier];
	}

	public float GetCurrentLegionCooldownSeconds()
	{
		return GetModuleCooldownSeconds("LegionOnTheLoose");
	}

	public int GetNextLegionCooldownGemCost()
	{
		return GetModuleNextCooldownGemCost("LegionOnTheLoose");
	}

	public int deathFromAboveLevel
	{
		get
		{
			return activeSavedData.GetValueInt("deathFromAbove", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValueInt("deathFromAbove", value, playModeSubSection);
		}
	}

	public bool fromTheShadows
	{
		get
		{
			return activeSavedData.GetValueBool("fromTheShadows", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValueBool("fromTheShadows", value, playModeSubSection);
			if (value)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_WITHOUT_A_TRACE");
			}
		}
	}

	public List<string> novelties
	{
		get
		{
			return mSavedData.GetSimpleList("novelties", playModeSubSection);
		}
		set
		{
			mSavedData.SetSimpleList("novelties", value, playModeSubSection);
		}
	}

	public List<string> alreadyEarnedAchievements
	{
		get
		{
			return mSavedData.GetSimpleList("alreadyEarnedAchievements");
		}
		set
		{
			mSavedData.SetSimpleList("alreadyEarnedAchievements", value);
		}
	}

	public List<string> giveOnceCompleted
	{
		get
		{
			return mSavedData.GetSimpleList("giveOnce");
		}
		set
		{
			mSavedData.SetSimpleList("giveOnce", value);
		}
	}

	public bool zombieModeUnlocked
	{
		get
		{
			if (Singleton<PlayModesManager>.instance.selectedMode != "classic")
			{
				return true;
			}
			if (activeSavedData.GetValueBool("zombieMode"))
			{
				return true;
			}
			return highestUnlockedWave > 10;
		}
	}

	public bool zombieModeUnlockedMessageShown
	{
		get
		{
			return activeSavedData.GetValueBool("zombieModeUnlockedMessageShown");
		}
		set
		{
			mSavedData.SetValueBool("zombieModeUnlockedMessageShown", value);
		}
	}

	public int upgradedFarmers
	{
		get
		{
			return mSavedData.GetValueInt("upgradedFarmers", playModeSubSection);
		}
		set
		{
			mSavedData.SetValueInt("upgradedFarmers", value, playModeSubSection);
			if (mSavedData.GetValueInt("upgradedFarmers", playModeSubSection) > 25)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_EVERY_ABLE_BODY");
			}
		}
	}

	public int upgradedArchers
	{
		get
		{
			return mSavedData.GetValueInt("upgradedArchers", playModeSubSection);
		}
		set
		{
			mSavedData.SetValueInt("upgradedArchers", value, playModeSubSection);
			if (mSavedData.GetValueInt("upgradedArchers", playModeSubSection) > 25)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_WE_NEED_SHOTGUNS");
			}
		}
	}

	public int maxSelectedHelpers
	{
		get
		{
			return 5;
		}
	}

	public int UndeadKilledViaTroopTrample
	{
		get
		{
			return mSavedData.GetValueInt("UndeadKilledViaTroopTrample", playModeSubSection);
		}
		set
		{
			mSavedData.SetValueInt("UndeadKilledViaTroopTrample", value, playModeSubSection);
			if (value > 250)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_CLEAR_THE_ROOM");
			}
		}
	}

	public int UndeadKilledViaKatanaSlash
	{
		get
		{
			return mSavedData.GetValueInt("UndeadKilledViaKatanaSlash", playModeSubSection);
		}
		set
		{
			mSavedData.SetValueInt("UndeadKilledViaKatanaSlash", value, playModeSubSection);
			if (value > 100)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_RAZOR_SHARP");
			}
		}
	}

	public int maxSelectedAbilities
	{
		get
		{
			return 2;
		}
	}

	public int maxSelectedCharms
	{
		get
		{
			return 1;
		}
	}

	public string selectedCharm
	{
		get
		{
			return activeSavedData.GetValue("selectedCharm", activeDataSubSection);
		}
		set
		{
			mSavedData.SetValue("selectedCharm", value, playModeSubSection);
		}
	}

	public int highestUnlockedWave
	{
		get
		{
			for (int i = 1; i < 1000; i++)
			{
				if (GetWaveLevel(i) == 0)
				{
					return Mathf.Max(1, i - 1);
				}
			}
			Debug.Log("WARNING: Something REALLY strange just happened.");
			return 1;
		}
	}

	public bool hasSummonedFarmer
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedFarmer");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedFarmer", value);
		}
	}

	public bool hasSummonedSwordWarrior
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedSwordWarrior");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedSwordWarrior", value);
		}
	}

	public bool hasSummonedSpearWarrior
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedSpearWarrior");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedSpearWarrior", value);
		}
	}

	public bool hasSummonedBowman
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedBowman");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedBowman", value);
		}
	}

	public bool hasSummonedPanzerSamurai
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedPanzerSamurai");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedPanzerSamurai", value);
		}
	}

	public bool hasSummonedPriest
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedPriest");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedPriest", value);
		}
	}

	public bool hasSummonedNobunaga
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedNobunaga");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedNobunaga", value);
		}
	}

	public bool hasSummonedSpearHorseman
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedSpearHorseman");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedSpearHorseman", value);
		}
	}

	public bool hasSummonedTakeda
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedTakeda");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedTakeda", value);
		}
	}

	public bool hasSummonedRifleman
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedRifleman");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedRifleman", value);
		}
	}

	public bool hasSummonedFrostie
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedFrostie");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedFrostie", value);
		}
	}

	public bool hasSummonedSwordsmith
	{
		get
		{
			return mSavedData.GetValueBool("hasSummonedSwordsmith");
		}
		set
		{
			mSavedData.SetValueBool("hasSummonedSwordsmith", value);
		}
	}

	public int wavesWithZeroGateDamage
	{
		get
		{
			return mSavedData.GetValueInt("wavesWithZeroGateDamage");
		}
		set
		{
			mSavedData.SetValueInt("wavesWithZeroGateDamage", value);
			if (value >= 10)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_MUST_PROTECT_HOUSE");
			}
		}
	}

	public int maxBaseWave
	{
		get
		{
			return int.Parse(Singleton<PlayModesManager>.instance.selectedModeData["maxBaseWave"]);
		}
	}

	public string playModeSubSection
	{
		get
		{
			string selectedMode = Singleton<PlayModesManager>.instance.selectedMode;
			if (selectedMode == "classic")
			{
				return null;
			}
			return selectedMode;
		}
	}

	private static string sCachedSaveDirectory;

	public static string saveDirectory
	{
		get
		{
			if (!string.IsNullOrEmpty(sCachedSaveDirectory))
			{
				return sCachedSaveDirectory;
			}
			if (Application.isMobilePlatform || Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer)
			{
				try
				{
					string mobileDir = Path.Combine(Application.persistentDataPath, "SaveData");
					if (!Directory.Exists(mobileDir))
					{
						Directory.CreateDirectory(mobileDir);
					}
					sCachedSaveDirectory = mobileDir;
					return sCachedSaveDirectory;
				}
				catch
				{
					sCachedSaveDirectory = Application.persistentDataPath;
					return sCachedSaveDirectory;
				}
			}
			try
			{
				string projectRoot = Directory.GetParent(Application.dataPath).FullName;
				string dir = Path.Combine(projectRoot, "SaveData");
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}
				string probe = Path.Combine(dir, ".svz_write_test");
				File.WriteAllText(probe, "1");
				File.Delete(probe);
				sCachedSaveDirectory = dir;
				return sCachedSaveDirectory;
			}
			catch
			{
				string fallbackDir = Path.Combine(Application.persistentDataPath, "SaveData");
				try
				{
					if (!Directory.Exists(fallbackDir))
					{
						Directory.CreateDirectory(fallbackDir);
					}
					sCachedSaveDirectory = fallbackDir;
				}
				catch
				{
					sCachedSaveDirectory = Application.persistentDataPath;
				}
				return sCachedSaveDirectory;
			}
		}
	}

	public string saveFilePath
	{
		get
		{
			string text = "save.data";
			if (m_CurrentSlot > 0)
			{
				text = "save_slot" + m_CurrentSlot + ".data";
			}
			return Path.Combine(saveDirectory, text);
		}
	}

	public int tapFeatureAdsCnt
	{
		get
		{
			return mSavedData.GetValueInt("tapFeatureAdsCnt");
		}
		set
		{
			mSavedData.SetValueInt("tapFeatureAdsCnt", value);
		}
	}

	public static string prevSaveFilePath
	{
		get
		{
			int num = Mathf.Max(0, PlayerPrefs.GetInt("CurrentSaveSlot", 0));
			string text = "prevsave.data";
			if (num > 0)
			{
				text = "prevsave_slot" + num + ".data";
			}
			return Path.Combine(saveDirectory, text);
		}
	}

	private int m_CurrentSlot;

	public int currentSlot
	{
		get { return m_CurrentSlot; }
		set { SwitchSlot(value); }
	}

	public void SwitchSlot(int slotIndex)
	{
		slotIndex = Mathf.Clamp(slotIndex, 0, 2);
		if (m_CurrentSlot == slotIndex)
		{
			return;
		}
		SaveNow();
		m_CurrentSlot = slotIndex;
		PlayerPrefs.SetInt("CurrentSaveSlot", m_CurrentSlot);
		mSavedData.saveFilePath = saveFilePath;
		Load();
		ClearBonusWaveData();
	}

	public string ExportSaveData()
	{
		try
		{
			SaveNow();
			return System.Convert.ToBase64String(mSavedData.GetAsByteArray());
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[Profile] ExportSaveData failed: " + ex.Message);
			return string.Empty;
		}
	}

	public void ImportSaveData(string data)
	{
		if (string.IsNullOrEmpty(data))
		{
			return;
		}
		string cleaned = data.Trim();
		if (string.IsNullOrEmpty(cleaned))
		{
			return;
		}
		try
		{
			SDFTree.CreateManualBackup(saveFilePath);
			byte[] data2;
			try
			{
				data2 = System.Convert.FromBase64String(cleaned);
			}
			catch (System.FormatException)
			{
				// Allow direct plain-text SDF clipboard import on mobile/PC as fallback
				if (cleaned.IndexOf('=') >= 0)
				{
					data2 = System.Text.Encoding.ASCII.GetBytes(cleaned);
				}
				else
				{
					throw;
				}
			}
			if (!mSavedData.TrySetWithByteArray(data2))
			{
				Debug.LogWarning("[Profile] ImportSaveData failed: invalid save data");
				return;
			}
			PostLoadSyncing();
			SaveNow();
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[Profile] ImportSaveData failed: " + ex.Message);
		}
	}

	public Profile()
	{
		EnsureSaveSeeded();
		m_needSave = false;
		m_inSaving = false;
		m_CurrentSlot = Mathf.Max(0, PlayerPrefs.GetInt("CurrentSaveSlot", 0));
		mSavedData = new SavedData(saveFilePath);
		Load();
		ClearBonusWaveData();
	}

	private static void EnsureSaveSeeded()
	{
		try
		{
			string targetFile = Path.Combine(saveDirectory, "save.data");
			if (!File.Exists(targetFile))
			{
				string legacyFile = Path.Combine(Application.persistentDataPath, "save.data");
				if (File.Exists(legacyFile))
				{
					File.Copy(legacyFile, targetFile, false);
					for (int i = 1; i <= 2; i++)
					{
						string legacySlot = Path.Combine(Application.persistentDataPath, "save_slot" + i + ".data");
						string targetSlot = Path.Combine(saveDirectory, "save_slot" + i + ".data");
						if (File.Exists(legacySlot) && !File.Exists(targetSlot))
						{
							File.Copy(legacySlot, targetSlot, false);
						}
					}
					Debug.Log("[Profile] Seeded local SaveData from legacy save.data");
				}
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[Profile] EnsureSaveSeeded: " + ex.Message);
		}
	}

	public void Save()
	{
		m_needSave = true;
	}

	public void SaveCloudFile(string cloudSavePath)
	{
		mCloudSavedData = new SavedData(cloudSavePath);
		mCloudSavedData.SetSDFTreeNode(mSavedData.saveData.Clone());
		mCloudSavedData.Save();
	}

	private void Load()
	{
		if (!mSavedData.Load())
		{
			Debug.Log("***** RESETTING DATA ****");
			ResetData();
		}
		PostLoadSyncing();
	}

	public void LoadBonusWaveData(SDFTreeNode data)
	{
		if (data == null)
		{
			ClearBonusWaveData();
			return;
		}
		mBonusWaveData = new SavedData(null);
		mBonusWaveData.SetSDFTreeNode(data);
		ValidateZombieBonusWaveLoadout();
	}

	private void ValidateZombieBonusWaveLoadout()
	{
		if (Singleton<PlayModesManager>.instance.selectedMode != "zombies" || mBonusWaveData == null)
		{
			return;
		}
		bool flag = false;
		List<string> subNodeValueList = mBonusWaveData.GetSubNodeValueList("selectedHelpers");
		if (subNodeValueList.Count != maxSelectedHelpers)
		{
			flag = true;
		}
		foreach (string item in subNodeValueList)
		{
			if (!Singleton<HelpersDatabase>.instance.Contains(item) || mBonusWaveData.GetDictionaryValue<int>("helperLevels", item) <= 0)
			{
				flag = true;
				break;
			}
		}
		List<string> subNodeValueList2 = mBonusWaveData.GetSubNodeValueList("selectedAbilities");
		if (subNodeValueList2.Count != maxSelectedAbilities)
		{
			flag = true;
		}
		foreach (string item2 in subNodeValueList2)
		{
			if (!Singleton<AbilitiesDatabase>.instance.Contains(item2) || mBonusWaveData.GetDictionaryValue<int>("abilityLevels", item2) <= 0)
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			Debug.LogError("Profile: zombie bonus wave loadout is invalid; using safe zombie bonus fallback.");
			ApplyZombieBonusFallbackLoadout();
		}
	}

	private void ApplyZombieBonusFallbackLoadout()
	{
		List<string> list = new List<string>();
		for (int i = 0; i < kZombieBonusFallbackHelpers.Length; i++)
		{
			list.Add(kZombieBonusFallbackHelpers[i]);
			mBonusWaveData.SetDictionaryValue("helperLevels", kZombieBonusFallbackHelpers[i], kZombieBonusFallbackHelperLevels[i]);
		}
		mBonusWaveData.SetSubNodeValueList("selectedHelpers", list);
		List<string> list2 = new List<string>();
		for (int j = 0; j < kZombieBonusFallbackAbilities.Length; j++)
		{
			list2.Add(kZombieBonusFallbackAbilities[j]);
			mBonusWaveData.SetDictionaryValue("abilityLevels", kZombieBonusFallbackAbilities[j], kZombieBonusFallbackAbilityLevels[j]);
		}
		mBonusWaveData.SetSubNodeValueList("selectedAbilities", list2);
		mBonusWaveData.SetValueInt("initialLeadershipLevel", 2);
		mBonusWaveData.SetValueInt("baseLevel", 1);
		mBonusWaveData.SetValueInt("bellLevel", 1);
		mBonusWaveData.SetValueInt("archerLevel", 1);
	}

	public void ClearBonusWaveData()
	{
		mBonusWaveData = null;
	}

	public void ForceActiveSaveData(bool bonusWaveData)
	{
		mForcingBaseSaveData = !bonusWaveData;
	}

	public void ResetData()
	{
		SDFTree.CreateEmergencyBackup(saveFilePath);
		int num = latestOnlineBundleVersion;
		List<string> achievementsDisplayed = GetAchievementsDisplayed();
		int num2 = tapFeatureAdsCnt;
		mSavedData.Clear();
		latestOnlineBundleVersion = num;
		SetAchievementsDisplayed(achievementsDisplayed);
		tapFeatureAdsCnt = num2;
		SetNumCharms("power", 1);
		SetNumCharms("luck", 1);
		PostLoadSyncing();
		mSavedData.Save();
	}

	public bool CreateManualSaveBackup()
	{
		SaveNow();
		return SDFTree.CreateManualBackup(saveFilePath);
	}

	public bool RestoreLatestSaveBackup()
	{
		if (!SDFTree.RestoreLatestBackup(saveFilePath))
		{
			return false;
		}
		Load();
		ClearBonusWaveData();
		return true;
	}

	public bool VerifySaveHealth(out string error)
	{
		return SDFTree.VerifyEncryptedSave(saveFilePath, out error);
	}

	public bool IsEventRewardsGiven(string eventRewardID)
	{
		return activeSavedData.GetDictionaryValue<bool>("eventRewards", eventRewardID);
	}

	public void SetEventRewardsGiven(string eventRewardID)
	{
		activeSavedData.SetDictionaryValue("eventRewards", eventRewardID, true);
	}

	public int GetHelperLevel(string helperID)
	{
		int val = activeSavedData.GetDictionaryValue<int>("helperLevels", helperID, activeDataSubSection);
		if (val == 0 && SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			string altSection = (activeDataSubSection == "zombies") ? "classic" : "zombies";
			val = activeSavedData.GetDictionaryValue<int>("helperLevels", helperID, altSection);
		}
		return Mathf.Clamp(val, 0, Singleton<HelpersDatabase>.instance.GetMaxLevel(helperID));
	}

	public void SetHelperLevel(string helperID, int val, bool autoFillSelectedHelpers)
	{
		val = Mathf.Clamp(val, 0, Singleton<HelpersDatabase>.instance.GetMaxLevel(helperID));
		if (autoFillSelectedHelpers && GetHelperLevel(helperID) == 0 && val > 0 && !SVZMultiplayerManager.IsHiddenOrSubHelper(helperID))
		{
			List<string> selectedHelpers = GetSelectedHelpers();
			if (selectedHelpers.Count < maxSelectedHelpers)
			{
				selectedHelpers.Add(helperID);
				SetSelectedHelpers(selectedHelpers);
			}
		}
		activeSavedData.SetDictionaryValue("helperLevels", helperID, val, activeDataSubSection);
	}

	public List<string> GetSelectedHelpers()
	{
		List<string> list = activeSavedData.GetSubNodeValueList("selectedHelpers", activeDataSubSection);
		if (list != null)
		{
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (SVZMultiplayerManager.IsHiddenOrSubHelper(list[i]))
				{
					list.RemoveAt(i);
				}
			}
		}
		return list;
	}

	public void SetSelectedHelpers(List<string> helpers)
	{
		activeSavedData.SetSubNodeValueList("selectedHelpers", helpers, activeDataSubSection);
	}

	public int GetAbilityLevel(string abilityID)
	{
		return Mathf.Max(0, activeSavedData.GetDictionaryValue<int>("abilityLevels", abilityID, activeDataSubSection));
	}

	public void SetAbilityLevel(string abilityID, int val)
	{
		activeSavedData.SetDictionaryValue("abilityLevels", abilityID, Mathf.Max(0, val), activeDataSubSection);
	}

	public List<string> GetSelectedAbilities()
	{
		return activeSavedData.GetSubNodeValueList("selectedAbilities", activeDataSubSection);
	}

	public void SetSelectedAbilities(List<string> abilities)
	{
		activeSavedData.SetSubNodeValueList("selectedAbilities", abilities, activeDataSubSection);
	}

	public int GetNumCharms(string charmID)
	{
		return mSavedData.GetDictionaryValue<int>("numCharms", charmID);
	}

	public void SetNumCharms(string charmID, int val)
	{
		mSavedData.SetDictionaryValue("numCharms", charmID, val);
	}

	public int GetNumPotions(string potionID)
	{
		return mSavedData.GetDictionaryValue<int>("numPotions", potionID);
	}

	public void SetNumPotions(string potionID, int val)
	{
		mSavedData.SetDictionaryValue("numPotions", potionID, val);
	}

	public bool IsChapterAvailable(string chapterID)
	{
		return mSavedData.GetDictionaryValue<bool>("chapterUnlock", chapterID, playModeSubSection);
	}

	public void SetChapterAvailable(string chapterID, bool val)
	{
		mSavedData.SetDictionaryValue("chapterUnlock", chapterID, val, playModeSubSection);
	}

	public int GetWaveLevel(int waveIndex)
	{
		return (!inBonusWave) ? mSavedData.GetDictionaryValue<int>("waves", "w" + waveIndex, playModeSubSection) : 0;
	}

	public void SetWaveLevel(int waveIndex, int level)
	{
		if (GetWaveLevel(waveIndex) >= level)
		{
			return;
		}
		level = Mathf.Clamp(level, 0, 99);
		mSavedData.SetDictionaryValue("waves", "w" + waveIndex, level, playModeSubSection);
		if (level == 99)
		{
			SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_FINALLY");
		}
		int num = maxBaseWave;
		for (int i = 1; i <= num && GetWaveLevel(i) == 99; i++)
		{
			if (i == num)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_THAT_TOOK_FOREVER");
			}
		}
	}

	public bool IsTutorialDone(string module, string id)
	{
		return mSavedData.GetDictionaryValue<bool>("tutorials", module + "_" + id);
	}

	public void SetTutorialDone(string module, string id)
	{
		mSavedData.SetDictionaryValue("tutorials", module + "_" + id, true);
	}

	public List<string> GetAchievementsDisplayed()
	{
		return mSavedData.GetSubNodeValueList("achievementsDisplayed");
	}

	public void SetAchievementsDisplayed(List<string> achievementsDisplayed)
	{
		mSavedData.SetSubNodeValueList("achievementsDisplayed", achievementsDisplayed);
	}

	public int GetNumKillsOfEnemyType(string enemyID)
	{
		return mSavedData.GetDictionaryValue<int>("stats_enemiesKilled", enemyID);
	}

	public void IncNumKillsOfEnemyType(string enemyID)
	{
		int num = GetNumKillsOfEnemyType(enemyID) + 1;
		mSavedData.SetDictionaryValue("stats_enemiesKilled", enemyID, num.ToString());
		switch (enemyID)
		{
		case "Oni":
		case "Oni_boss":
		case "Oni_small":
			if (GetNumKillsOfEnemyType("Oni") + GetNumKillsOfEnemyType("Oni_boss") + GetNumKillsOfEnemyType("Oni_small") > 50)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_SEND_THEM_BACK");
			}
			break;
		case "Chochinobake":
		case "Chochinobake_alt":
			if (GetNumKillsOfEnemyType("Chochinobake") + GetNumKillsOfEnemyType("Chochinobake_alt") > 50)
			{
				SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_GET_BACK_HERE");
			}
			break;
		case "Jurogumo":
			SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_SPIDER_DEMON");
			break;
		case "Shogun":
			SingletonMonoBehaviour<Achievements>.instance.Award("SAMUZOMBIE_ACHIEVE_SO_LONG");
			break;
		}
	}

	public void PostLoadSyncing()
	{
		if (Singleton<PlayModesManager>.instance.selectedMode == "classic")
		{
			UpgradeWaveRecycling();
			UpgradeToExtraWaves();
		}
		SanitizeSavedProfile();
		waveToBeat = ValidateWaveIndex(waveToBeat);
		if (GetWaveLevel(1) == 0)
		{
			SetWaveLevel(1, 1);
		}
	}

	private void SanitizeSavedProfile()
	{
		try
		{
			if (mSavedData == null || mSavedData.saveData == null)
			{
				return;
			}
			bool modified = false;
			if (ResourcesManager.initialized)
			{
				modified |= SanitizeModeSubsection("classic");
				modified |= SanitizeModeSubsection("zombies");
			}
			if (modified)
			{
				SDFTree.CreateEmergencyBackup(saveFilePath);
				Save();
				Debug.LogWarning("[Profile] Sanitized out-of-range save entries automatically without resetting progress.");
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning("[Profile] SanitizeSavedProfile skipped: " + ex.Message);
		}
	}

	private bool SanitizeModeSubsection(string modeSection)
	{
		bool changed = false;
		SDFTreeNode modeNode = mSavedData.saveData.to(modeSection);
		if (modeNode == null)
		{
			return false;
		}
		string modeHelpersPath = string.Equals(modeSection, "zombies", System.StringComparison.OrdinalIgnoreCase)
			? "registry/helpers_zm/AllHelpers"
			: "registry/helpers/AllHelpers";
		SDFTreeNode modeHelpersNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open(modeHelpersPath, false);
		HashSet<string> modeHelperSet = null;
		if (modeHelpersNode != null && modeHelpersNode.attributeCount > 0)
		{
			modeHelperSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
			for (int i = 0; modeHelpersNode.hasAttribute(i); i++)
			{
				modeHelperSet.Add(modeHelpersNode[i]);
			}
		}
		SDFTreeNode helpersNode = modeNode.to("helperLevels");
		if (helpersNode != null)
		{
			List<string> helperKeys = new List<string>();
			foreach (KeyValuePair<string, string> kv in helpersNode.attributes)
			{
				helperKeys.Add(kv.Key);
			}
			foreach (string key in helperKeys)
			{
				int val;
				if (!int.TryParse(helpersNode[key], out val))
				{
					helpersNode[key] = "0";
					changed = true;
					continue;
				}
				if (!Singleton<HelpersDatabase>.instance.Contains(key) || (modeHelperSet != null && !modeHelperSet.Contains(key)))
				{
					if (val != 0)
					{
						helpersNode[key] = "0";
						changed = true;
					}
					continue;
				}
				int maxLvl = Singleton<HelpersDatabase>.instance.GetMaxLevel(key);
				int clamped = Mathf.Clamp(val, 0, Mathf.Max(1, maxLvl));
				if (clamped != val)
				{
					helpersNode[key] = clamped.ToString();
					changed = true;
				}
			}
		}
		List<string> selectedHelpers = mSavedData.GetSubNodeValueList("selectedHelpers", modeSection);
		if (selectedHelpers != null && selectedHelpers.Count > 0)
		{
			List<string> validHelpers = new List<string>();
			foreach (string id in selectedHelpers)
			{
				if (!string.IsNullOrEmpty(id)
					&& Singleton<HelpersDatabase>.instance.Contains(id)
					&& (modeHelperSet == null || modeHelperSet.Contains(id))
					&& !SVZMultiplayerManager.IsHiddenOrSubHelper(id))
				{
					int lvl = mSavedData.GetDictionaryValue<int>("helperLevels", id, modeSection);
					if (lvl > 0 && !validHelpers.Contains(id) && validHelpers.Count < maxSelectedHelpers)
					{
						validHelpers.Add(id);
					}
				}
			}
			if (validHelpers.Count != selectedHelpers.Count)
			{
				mSavedData.SetSubNodeValueList("selectedHelpers", validHelpers, modeSection);
				changed = true;
			}
		}
		SDFTreeNode abilitiesNode = modeNode.to("abilityLevels");
		if (abilitiesNode != null)
		{
			List<string> abilityKeys = new List<string>();
			foreach (KeyValuePair<string, string> kv in abilitiesNode.attributes)
			{
				abilityKeys.Add(kv.Key);
			}
			foreach (string key in abilityKeys)
			{
				int val;
				if (!int.TryParse(abilitiesNode[key], out val) || val < 0)
				{
					abilitiesNode[key] = "0";
					changed = true;
				}
			}
		}
		return changed;
	}

	private int ValidateWaveIndex(int index)
	{
		if (index < 1)
		{
			return 1;
		}
		int maxWave = Mathf.Max(1, maxBaseWave);
		if (index > maxWave)
		{
			index = maxWave;
		}
		string path = "Registry/" + Singleton<PlayModesManager>.instance.selectedModeData["wavesPath"] + string.Format("{0:000}", index);
		if (SingletonMonoBehaviour<ResourcesManager>.instance.Open(path) == null)
		{
			index = 1;
		}
		return index;
	}

	private int ValidateBonusWaveIndex(int index)
	{
		if (index < 1)
		{
			return 1;
		}
		int bonusWaveCount = GetBonusWaveCount();
		if (bonusWaveCount > 0 && index > bonusWaveCount)
		{
			return index;
		}
		string selectedModeBonusWavesPath = GetSelectedModeBonusWavesPath();
		if (selectedModeBonusWavesPath == string.Empty)
		{
			selectedModeBonusWavesPath = "BonusWaves/BonusWave";
		}
		string path = "Registry/" + selectedModeBonusWavesPath + string.Format("{0:000}", index);
		if (SingletonMonoBehaviour<ResourcesManager>.instance.Open(path) == null)
		{
			index = 1;
		}
		return index;
	}

	private string GetBonusWaveCounterKey()
	{
		string selectedMode = Singleton<PlayModesManager>.instance.selectedMode;
		if (selectedMode == "classic")
		{
			return "wavesSinceLastBonusWave";
		}
		return "wavesSinceLastBonusWave_" + selectedMode;
	}

	private string GetBonusWaveToBeatKey()
	{
		string selectedMode = Singleton<PlayModesManager>.instance.selectedMode;
		if (selectedMode == "classic")
		{
			return "bonusWaveToBeat";
		}
		return "bonusWaveToBeat_" + selectedMode;
	}

	private string GetSelectedModeBonusWavesPath()
	{
		SDFTreeNode selectedModeData = Singleton<PlayModesManager>.instance.selectedModeData;
		string text = (selectedModeData != null) ? selectedModeData["bonusWavesPath"] : string.Empty;
		if (text == null)
		{
			text = string.Empty;
		}
		if (text == string.Empty && Singleton<PlayModesManager>.instance.selectedMode == "classic")
		{
			return "BonusWaves/BonusWave";
		}
		return text;
	}

	private int GetWavesPerBonusWave()
	{
		int result;
		SDFTreeNode selectedModeData = Singleton<PlayModesManager>.instance.selectedModeData;
		string text = (selectedModeData != null) ? selectedModeData["wavesPerBonusWave"] : string.Empty;
		if (text != null && int.TryParse(text, out result) && result > 0)
		{
			return result;
		}
		if (int.TryParse(Singleton<Config>.instance.root.to("Game")["wavesPerBonusWave"], out result) && result > 0)
		{
			return result;
		}
		return 5;
	}

	private int GetBonusWaveCount()
	{
		int result;
		SDFTreeNode selectedModeData = Singleton<PlayModesManager>.instance.selectedModeData;
		string text = (selectedModeData != null) ? selectedModeData["bonusWaveCount"] : string.Empty;
		if (text != null && int.TryParse(text, out result))
		{
			return result;
		}
		return 0;
	}

	private int GetBonusWaveMaxBaseWave()
	{
		int result;
		SDFTreeNode selectedModeData = Singleton<PlayModesManager>.instance.selectedModeData;
		string text = (selectedModeData != null) ? selectedModeData["bonusWaveMaxBaseWave"] : string.Empty;
		if (text != null && int.TryParse(text, out result))
		{
			return result;
		}
		return 0;
	}

	private void UpgradeWaveRecycling()
	{
		int num = waveToBeat;
		int valueInt = mSavedData.GetValueInt("waveRecycle");
		if (GetWaveLevel(num) != 0)
		{
			return;
		}
		for (int i = 1; i <= 20; i++)
		{
			if (i < num)
			{
				SetWaveLevel(i, valueInt + 2);
			}
			else if (i == num)
			{
				SetWaveLevel(i, valueInt + 1);
			}
			else if (valueInt > 0)
			{
				SetWaveLevel(i, valueInt + 1);
			}
		}
		if (valueInt > 0)
		{
			mSavedData.SetValueInt("waveRecycle", 0);
			waveToBeat = 21;
		}
	}

	public void UpgradeToExtraWaves()
	{
		if (!ResourcesManager.initialized)
		{
			return;
		}
		int num = highestUnlockedWave;
		if (GetWaveLevel(num) > 1)
		{
			string path = "Registry/Waves/Wave" + string.Format("{0:000}", num + 1);
			if (SingletonMonoBehaviour<ResourcesManager>.instance.Open(path) != null)
			{
				SetWaveLevel(num + 1, 1);
				waveToBeat = num + 1;
			}
		}
	}

	public void ConvertLocalGemsToGWallet()
	{
	}

	public void Update()
	{
		SaveNow();
	}

	private void SaveNow()
	{
		if (m_needSave && !m_inSaving)
		{
			m_inSaving = true;
			if (Debug.isDebugBuild)
			{
				Debug.Log("ASYNCH_SAVING");
			}
			mSavedData.Save();
			m_inSaving = false;
			m_needSave = false;
		}
	}
}
