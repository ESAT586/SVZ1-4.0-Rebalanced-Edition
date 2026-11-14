using System.Collections.Generic;

public class PlayStatistics : Singleton<PlayStatistics>
{
	public struct Stats
	{
		public bool zombieModeStartedUnlocked;

		public string lastPlayMode;

		public int lastWavePlayed;

		public int lastWaveLevel;

		public bool lastWaveWon;

		public int lastWaveTotalEnemies;

		public int lastWaveCoinsAward;

		public int lastWaveCoinsCollected;

		public bool heroUsedHisMeleeAttack;

		public bool heroUsedHisRangedAttack;

		public bool heroInvokedHelpers;

		public bool gateWasDamaged;

		public bool summonedTroopOtherThanFarmerOrSwordsmith;

		public bool summonedTroopFarmer;

		public bool summonedTroopSwordsmith;

		public List<StoreData.Item> previousStoreItems;
	}

	public Stats stats;

	public void ResetStats(int forWave)
	{
		stats = default(Stats);
		stats.previousStoreItems = StoreAvailability.GetList();
		stats.lastPlayMode = Singleton<PlayModesManager>.instance.selectedMode;
		stats.lastWavePlayed = forWave;
		stats.lastWaveLevel = Singleton<Profile>.instance.GetWaveLevel(forWave);
		stats.zombieModeStartedUnlocked = Singleton<Profile>.instance.zombieModeUnlocked;
	}
}
