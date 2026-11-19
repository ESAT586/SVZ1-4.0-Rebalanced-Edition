using System.Collections.Generic;

public class StoreAvailability_LegionsOnTheLoose
{
	public static string GetLocalizedOrFallback(string key, string fallback)
	{
		if (Singleton<Localizer>.instance.Has(key))
		{
			return Singleton<Localizer>.instance.Get(key);
		}
		return fallback;
	}

	public static void GetAll(List<StoreData.Item> items)
	{
		if (Singleton<PlayModesManager>.instance.selectedMode == "zombies")
		{
			Get("LegionOnTheLoose", items, Singleton<Profile>.instance.legionOnTheLooseLevel, UpgradeLegion);
			Get("DeathFromAbove", items, Singleton<Profile>.instance.deathFromAboveLevel, UpgradeDeathFromAbove);
		}
	}

	private static void Get(string powerID, List<StoreData.Item> items, int currentLevel, OnSUIGenericCallback onPurchase)
	{
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/" + powerID);
		if (sDFTreeNode == null)
		{
			return;
		}
		bool flag = false;
		if (Singleton<Profile>.instance.highestUnlockedWave < int.Parse(sDFTreeNode["waveToUnlock"]))
		{
			flag = true;
		}
		int num = currentLevel + 1;
		SDFTreeNode sDFTreeNode2 = sDFTreeNode.to(string.Format("{0:000}", num));
		bool isMaxLevel = false;
		if (sDFTreeNode2 == null)
		{
			num--;
			sDFTreeNode2 = sDFTreeNode.to(string.Format("{0:000}", num));
			isMaxLevel = true;
			if (sDFTreeNode2 == null)
			{
				return;
			}
		}
		StoreData.Item item = new StoreData.Item(delegate
		{
			if (!isMaxLevel)
			{
				onPurchase();
			}
		});
		if (isMaxLevel)
		{
			item.isMaxLevel = true;
		}
		if (num != 1)
		{
			SDFTreeNode sDFTreeNode3 = sDFTreeNode.to(string.Format("{0:000}", num - 1));
			if (sDFTreeNode3 == null)
			{
			}
		}
		item.id = powerID;
		item.icon = sDFTreeNode["icon"];
		item.locked = flag;
		item.unlockAtWave = int.Parse(sDFTreeNode["waveToUnlock"]);
		if (flag)
		{
			item.unlockCondition = string.Format(Singleton<Localizer>.instance.Get("store_unlockatwave"), item.unlockAtWave);
		}
		if (isMaxLevel)
		{
			item.title = Singleton<Localizer>.instance.Parse(sDFTreeNode["displayName"]);
		}
		else if (num == 1)
		{
			item.title = string.Format(Singleton<Localizer>.instance.Get("store_format_purchase"), Singleton<Localizer>.instance.Parse(sDFTreeNode["displayName"]), num);
		}
		else
		{
			item.title = string.Format(Singleton<Localizer>.instance.Get("store_format_levelup"), Singleton<Localizer>.instance.Parse(sDFTreeNode["displayName"]), num);
		}
		if ((powerID == "LegionOnTheLoose" || powerID == "DeathFromAbove") && currentLevel >= 1 && !Singleton<Profile>.instance.IsModuleEnabled(powerID))
		{
			item.title += " (" + GetLocalizedOrFallback("legion_badge_off", "KAPALI") + ")";
		}
		item.details.AddSmallDescription(Singleton<Localizer>.instance.Parse(sDFTreeNode["desc"]));
		item.unlockTitle = Singleton<Localizer>.instance.Parse(sDFTreeNode["displayName"]);
		item.cost = isMaxLevel ? new Cost("0", 0) : new Cost(sDFTreeNode2["cost"], Singleton<SalesDatabase>.instance.GetSaleFor(powerID));
		item.isEvent = sDFTreeNode["eventID"] != string.Empty;
		items.Add(item);
	}

	private static void UpgradeModule(string moduleID, string analyticsEvent)
	{
		int prevLevel = Singleton<Profile>.instance.GetModuleUnlockedLevel(moduleID);
		int prevSelected = Singleton<Profile>.instance.GetModuleSelectedLevel(moduleID);
		int nextLevel = prevLevel + 1;
		Singleton<Profile>.instance.SetModuleUnlockedLevel(moduleID, nextLevel);
		if (prevLevel <= 0 || prevSelected <= 0 || prevSelected == prevLevel)
		{
			Singleton<Profile>.instance.SetModuleSelectedLevel(moduleID, nextLevel);
		}
		Singleton<Profile>.instance.Save();
		Singleton<Analytics>.instance.LogEvent(analyticsEvent, string.Empty);
	}

	private static void UpgradeLegion()
	{
		UpgradeModule("LegionOnTheLoose", "Upgrade_LegionOnTheLoose");
	}

	private static void UpgradeDeathFromAbove()
	{
		UpgradeModule("DeathFromAbove", "Upgrade_DeathFromAbove");
	}
}
