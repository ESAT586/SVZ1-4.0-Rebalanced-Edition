using System.Collections.Generic;
using UnityEngine;

public class LegionOnTheLooseManager : WeakGlobalInstance<LegionOnTheLooseManager>
{
	private class Entry
	{
		public string moduleID = string.Empty;

		public float maxTimer;

		public float currentTimer;

		public string helperID = string.Empty;

		public int numSpawns;

		public int queuedSpawns;

		public float deltaTimerMax;

		public float currentDeltaTimer;
	}

	public OnSUIStringCallback onSpawnStart;

	private List<Entry> mEntries = new List<Entry>();

	private int mMaxHelpers;

	private bool allowedToSpawn
	{
		get
		{
			return WeakGlobalInstance<CharactersManager>.instance.helpersCount < mMaxHelpers;
		}
	}

	public LegionOnTheLooseManager()
	{
		SetUniqueInstance(this);
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			return;
		}
		int num = 0;
		InGameImpl igInst = WeakGlobalSceneBehavior<InGameImpl>.instance;
		if (igInst != null && (igInst.activeCharm == "horde" || igInst.activeCharm.Contains("+horde")))
		{
			num = int.Parse(Singleton<CharmsDatabase>.instance.GetAttribute(igInst.activeCharm, "legionOnTheLooseBonus"));
		}
		switch (Singleton<PlayModesManager>.instance.selectedMode)
		{
		case "zombies":
			if (Singleton<Profile>.instance.IsModuleEnabled("LegionOnTheLoose"))
			{
				int unlocked = Singleton<Profile>.instance.GetModuleUnlockedLevel("LegionOnTheLoose");
				int chosen = Singleton<Profile>.instance.GetModuleSelectedLevel("LegionOnTheLoose");
				if (unlocked > 0)
				{
					int spawnLevel = (chosen == unlocked) ? (chosen + num) : chosen;
					AddModule("LegionOnTheLoose", spawnLevel, num > 0, (num > 0) ? 0f : 1.5f);
				}
				else if (num > 0)
				{
					AddModule("LegionOnTheLoose", num, true, 0f);
				}
			}
			if (Singleton<Profile>.instance.IsModuleEnabled("DeathFromAbove"))
			{
				int dfaUnlocked = Singleton<Profile>.instance.GetModuleUnlockedLevel("DeathFromAbove");
				int dfaChosen = Singleton<Profile>.instance.GetModuleSelectedLevel("DeathFromAbove");
				if (dfaUnlocked > 0)
				{
					AddModule("DeathFromAbove", dfaChosen, true, 0f);
				}
			}
			break;
		}
	}

	public void Update()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			return;
		}
		foreach (Entry mEntry in mEntries)
		{
			if (mEntry.queuedSpawns > 0)
			{
				mEntry.currentDeltaTimer += Time.deltaTime;
				if (mEntry.currentDeltaTimer >= mEntry.deltaTimerMax)
				{
					mEntry.currentDeltaTimer -= mEntry.deltaTimerMax;
					mEntry.queuedSpawns--;
					Spawn(mEntry.helperID);
				}
				continue;
			}
			mEntry.currentTimer += Time.deltaTime;
			if (mEntry.currentTimer >= mEntry.maxTimer)
			{
				if (allowedToSpawn && onSpawnStart != null)
				{
					onSpawnStart(mEntry.moduleID);
				}
				mEntry.currentTimer -= mEntry.maxTimer;
				mEntry.queuedSpawns = mEntry.numSpawns - 1;
				mEntry.currentDeltaTimer = 0f;
				Spawn(mEntry.helperID);
			}
		}
	}

	private void AddModule(string moduleID, int level, bool startRightAway, float initialDelayOverride)
	{
		if (level == 0)
		{
			return;
		}
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/" + moduleID);
		if (sDFTreeNode == null)
		{
			Debug.Log("WARNING: Unable to load legion module: " + moduleID);
			return;
		}
		SDFTreeNode sDFTreeNode2 = null;
		while (sDFTreeNode2 == null && level > 0)
		{
			sDFTreeNode2 = sDFTreeNode.to(level);
			level--;
		}
		if (sDFTreeNode2 != null)
		{
			int modMax = int.Parse(sDFTreeNode["maxHelpers"]);
			if (modMax > mMaxHelpers)
			{
				mMaxHelpers = modMax;
			}
			Entry entry = new Entry();
			entry.moduleID = moduleID;
			entry.maxTimer = (moduleID == "LegionOnTheLoose" || moduleID == "DeathFromAbove")
				? Singleton<Profile>.instance.GetModuleCooldownSeconds(moduleID)
				: float.Parse(sDFTreeNode2["delay"]);
			if (startRightAway)
			{
				entry.currentTimer = entry.maxTimer;
			}
			else if (initialDelayOverride > 0f)
			{
				entry.currentTimer = Mathf.Max(0f, entry.maxTimer - initialDelayOverride);
			}
			entry.deltaTimerMax = float.Parse(sDFTreeNode["inBetweenDelay"]);
			entry.helperID = sDFTreeNode2["helperID"];
			entry.numSpawns = int.Parse(sDFTreeNode2["num"]);
			mEntries.Add(entry);
		}
	}

	private void Spawn(string helperID)
	{
		if (allowedToSpawn)
		{
			WeakGlobalInstance<Smithy>.instance.ForceSpawn(helperID);
		}
	}
}
