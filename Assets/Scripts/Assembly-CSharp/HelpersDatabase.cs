using System;
using System.Collections.Generic;
using UnityEngine;

public class HelpersDatabase : Singleton<HelpersDatabase>
{
	private class HelperData
	{
		public string id;

		public SDFTreeNode registryData;

		public HelperData(string abId)
		{
			id = abId;
		}

		public void EnsureDataCached()
		{
			if (registryData == null)
			{
				registryData = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Helpers/" + id);
				if (registryData == null)
				{
					registryData = SingletonMonoBehaviour<ResourcesManager>.instance.Open("registry/helpers/" + id, false);
				}
				if (registryData == null)
				{
					registryData = SingletonMonoBehaviour<ResourcesManager>.instance.Open("registry/helpers_zm/" + id, false);
				}
			}
		}
	}

	private List<HelperData> mData = new List<HelperData>();

	private string[] mAllIDs;

	public string[] allIDs
	{
		get
		{
			return mAllIDs;
		}
	}

	public HelpersDatabase()
	{
		ResetCachedData();
		SingletonMonoBehaviour<ResourcesManager>.instance.onInvalidateCache += ResetCachedData;
	}

	public void ResetCachedData()
	{
		mData = new List<HelperData>();
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Helpers/AllHelpers");
		if (sDFTreeNode == null || sDFTreeNode.attributeCount == 0)
		{
			Debug.Log("ERROR: Cannot find the list of Helpers (AllHelpers.txt)");
			mAllIDs = new string[0];
			return;
		}
		for (int i = 0; sDFTreeNode.hasAttribute(i); i++)
		{
			mData.Add(new HelperData(sDFTreeNode[i]));
		}
		string[] extraHelpers = {
			"SuperFarmer", "SuperNinja", "SamuraiRifleman",
			"CharmAssassin", "CharmFarmer", "CharmNobunaga", "CharmPanzer", "CharmSamurai", "CharmSpearman", "CharmTakeda",
			"CharmJurogumo", "CharmOni", "CharmWanyudoFire", "CharmWanyudoMetal", "JurogumoBaby", "ZombieMelee_Summon", "Nobusuma"
		};
		foreach (string extra in extraHelpers)
		{
			bool already = false;
			foreach (HelperData hd in mData)
			{
				if (hd.id.Equals(extra, StringComparison.OrdinalIgnoreCase)) { already = true; break; }
			}
			if (!already)
			{
				mData.Add(new HelperData(extra));
			}
		}
		SDFTreeNode zmTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Helpers_ZM/AllHelpers", false);
		if (zmTreeNode != null)
		{
			for (int j = 0; zmTreeNode.hasAttribute(j); j++)
			{
				string zmId = zmTreeNode[j];
				bool alreadyZm = false;
				foreach (HelperData hd in mData)
				{
					if (hd.id.Equals(zmId, StringComparison.OrdinalIgnoreCase)) { alreadyZm = true; break; }
				}
				if (!alreadyZm)
				{
					mData.Add(new HelperData(zmId));
				}
			}
		}
		CacheSimpleIDList();
	}

	public bool Contains(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return false;
		}
		if (mAllIDs != null)
		{
			string[] array = mAllIDs;
			foreach (string strA in array)
			{
				if (string.Compare(strA, id, true) == 0)
				{
					return true;
				}
			}
		}
		return Seek(id) != null;
	}

	public string GetAttribute(string helperID, string attributeName)
	{
		return GetAttribute(helperID, attributeName, GetHelperLevel(helperID), false);
	}

	public string GetNextLevelAttribute(string helperID, string attributeName)
	{
		int nextLevel = GetHelperLevel(helperID) + 1;
		if (nextLevel > GetMaxLevel(helperID))
		{
			return string.Empty;
		}
		return GetAttribute(helperID, attributeName, nextLevel, false);
	}

	public string GetAttributeOrNull(string helperID, string attributeName)
	{
		return GetAttribute(helperID, attributeName, GetHelperLevel(helperID), true);
	}

	public int GetMaxLevel(string helperID)
	{
		HelperData helperData = Seek(helperID);
		if (helperData != null)
		{
			helperData.EnsureDataCached();
			if (helperData.registryData != null)
			{
				for (int i = 1; i < 1000; i++)
				{
					if (helperData.registryData.to(i) == null)
					{
						return Mathf.Max(1, i - 1);
					}
				}
			}
		}
		Debug.Log("ERROR: Unknown helper ID, or helper data malformed: " + helperID);
		return 10;
	}

	private void CacheSimpleIDList()
	{
		mAllIDs = new string[mData.Count];
		int num = 0;
		foreach (HelperData mDatum in mData)
		{
			mAllIDs[num++] = mDatum.id;
		}
	}

	private HelperData Seek(string helperID)
	{
		foreach (HelperData mDatum in mData)
		{
			if (mDatum.id.Equals(helperID, StringComparison.OrdinalIgnoreCase))
			{
				return mDatum;
			}
		}
		if (!string.IsNullOrEmpty(helperID) && SingletonMonoBehaviour<ResourcesManager>.instance != null)
		{
			SDFTreeNode node = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Helpers/" + helperID);
			if (node == null)
			{
				node = SingletonMonoBehaviour<ResourcesManager>.instance.Open("registry/helpers/" + helperID, false);
			}
			if (node == null)
			{
				node = SingletonMonoBehaviour<ResourcesManager>.instance.Open("registry/helpers_zm/" + helperID, false);
			}
			if (node != null)
			{
				HelperData dynamicData = new HelperData(helperID);
				dynamicData.registryData = node;
				mData.Add(dynamicData);
				return dynamicData;
			}
		}
		return null;
	}

	private string GetAttribute(string helperID, string attributeName, int level, bool noWarningIfNotFound)
	{
		if (helperID != null && helperID.Equals("SuperFarmer", StringComparison.OrdinalIgnoreCase) && attributeName.Equals("levelMatchOtherHelper", StringComparison.OrdinalIgnoreCase))
		{
			return "Farmer";
		}
		HelperData helperData = Seek(helperID);
		if (helperData == null)
		{
			Debug.Log("WARNING: Could not find helper: " + helperID);
			return string.Empty;
		}
		helperData.EnsureDataCached();
		if (helperData.registryData != null && helperData.registryData.hasAttribute(attributeName))
		{
			return Singleton<Localizer>.instance.Parse(helperData.registryData[attributeName]);
		}
		level = Mathf.Max(level, 1);
		if (helperData.registryData != null)
		{
			for (int l = level; l >= 1; l--)
			{
				SDFTreeNode sDFTreeNode = helperData.registryData.to(l);
				if (sDFTreeNode != null && sDFTreeNode.hasAttribute(attributeName))
				{
					return Singleton<Localizer>.instance.Parse(sDFTreeNode[attributeName]);
				}
			}
		}
		if (!noWarningIfNotFound)
		{
			Debug.Log("WARNING: Could not find helper attribute: " + attributeName);
		}
		return string.Empty;
	}

	private int GetHelperLevel(string helperID)
	{
		int lvl;
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			lvl = SVZMultiplayerManager.Instance.GetPvPHelperLevel(helperID);
			return Mathf.Min(Mathf.Max(lvl, 1), GetMaxLevel(helperID));
		}
		HelperData helperData = Seek(helperID);
		if (helperData == null)
		{
			return Singleton<Profile>.instance.GetHelperLevel(helperID);
		}
		helperData.EnsureDataCached();
		if (helperData.registryData != null && helperData.registryData.hasAttribute("levelMatchOtherHelper"))
		{
			string text = helperData.registryData["levelMatchOtherHelper"];
			if (text != string.Empty)
			{
				lvl = Singleton<Profile>.instance.GetHelperLevel(text);
				return (lvl > 0) ? Mathf.Min(lvl, GetMaxLevel(helperID)) : lvl;
			}
		}
		lvl = Singleton<Profile>.instance.GetHelperLevel(helperID);
		return (lvl > 0) ? Mathf.Min(lvl, GetMaxLevel(helperID)) : lvl;
	}
}
