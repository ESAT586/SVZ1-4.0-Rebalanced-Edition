using System.Globalization;
using Debug = UnityEngine.Debug;

public class CashIn
{
	public enum ItemType
	{
		Unknown = 0,
		Coins = 1,
		Gems = 2,
		Balls = 3,
		BoosterPack = 4,
		Potion = 5,
		Charm = 6,
		Helper = 7,
		Ability = 8
	}

	private static string NormalizeRewardId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return string.Empty;
		}
		string text = id.Trim();
		string lower = text.ToLowerInvariant();
		if (lower == "frostee" || lower == "frostie" || lower == "frostiebowman")
		{
			return "IceArcher";
		}
		return text;
	}

	public static bool From(string id, int val, string gwDesc, string gwType)
	{
		return From(id, val.ToString(), gwDesc, gwType);
	}

	public static bool From(string id, string val, string gwDesc, string gwType)
	{
		string text = NormalizeRewardId(id);
		if (text == "gems")
		{
			ApplicationUtilities.GWalletBalance(int.Parse(val, CultureInfo.InvariantCulture), gwDesc, gwType);
			return true;
		}
		if (text == "coins" && gwType == "CREDIT_GC_PURCHASE")
		{
			Singleton<Profile>.instance.coins += int.Parse(val, CultureInfo.InvariantCulture);
		}
		return From(text, val);
	}

	private static bool From(string id, string val)
	{
		string text = NormalizeRewardId(id);
		switch (GetType(text))
		{
		case ItemType.Coins:
			Singleton<Profile>.instance.coins += int.Parse(val, CultureInfo.InvariantCulture);
			break;
		case ItemType.Gems:
			Singleton<Profile>.instance.gems += int.Parse(val, CultureInfo.InvariantCulture);
			break;
		case ItemType.Balls:
			Singleton<Profile>.instance.pachinkoBalls += int.Parse(val, CultureInfo.InvariantCulture);
			break;
		case ItemType.BoosterPack:
			if (text == "booster_free")
			{
				Singleton<Profile>.instance.freeBoosterPacks += int.Parse(val, CultureInfo.InvariantCulture);
			}
			break;
		case ItemType.Potion:
			Singleton<Profile>.instance.SetNumPotions(text, Singleton<Profile>.instance.GetNumPotions(text) + int.Parse(val, CultureInfo.InvariantCulture));
			break;
		case ItemType.Charm:
			Singleton<Profile>.instance.SetNumCharms(text, Singleton<Profile>.instance.GetNumCharms(text) + int.Parse(val, CultureInfo.InvariantCulture));
			break;
		case ItemType.Helper:
			Singleton<Profile>.instance.SetHelperLevel(text, Singleton<Profile>.instance.GetHelperLevel(text) + int.Parse(val, CultureInfo.InvariantCulture), true);
			break;
		case ItemType.Ability:
			Singleton<Profile>.instance.SetAbilityLevel(text, Singleton<Profile>.instance.GetAbilityLevel(text) + int.Parse(val, CultureInfo.InvariantCulture));
			break;
		default:
			Debug.Log("ERROR: Unknown Booster Card Payload ID: '" + text + "'");
			return false;
		}
		return true;
	}

	public static ItemType GetType(string id)
	{
		string text = NormalizeRewardId(id);
		switch (text)
		{
		case "coins":
			return ItemType.Coins;
		case "gems":
			return ItemType.Gems;
		case "balls":
			return ItemType.Balls;
		case "booster_free":
			return ItemType.BoosterPack;
		case "IceArcher":
			return ItemType.Helper;
		default:
			if (Singleton<PotionsDatabase>.instance.Contains(text))
			{
				return ItemType.Potion;
			}
			if (Singleton<CharmsDatabase>.instance.Contains(text))
			{
				return ItemType.Charm;
			}
			if (Singleton<HelpersDatabase>.instance.Contains(text))
			{
				return ItemType.Helper;
			}
			if (Singleton<AbilitiesDatabase>.instance.Contains(text))
			{
				return ItemType.Ability;
			}
			return ItemType.Unknown;
		}
	}
}
