using UnityEngine;
using System.Collections.Generic;

public class AchievementData
{
    public string ID;
    public string Name;
    public string Description;
    public string IconPath;
    public int RewardXP;
    public int RewardGems;
    public bool IsUnlocked;
}

public class AchievementsDatabase : Singleton<AchievementsDatabase>
{
    private List<AchievementData> mAchievements = new List<AchievementData>();
    private bool mInitialized = false;

    public List<AchievementData> AllAchievements
    {
        get 
        { 
            Initialize();
            return mAchievements; 
        }
    }

    public void Initialize()
    {
        if (mInitialized) return;

        SDFTreeNode sDFTree = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Achievements");
        if (sDFTree == null)
        {
            Debug.LogError("Achievements registry not found!");
            return;
        }

        foreach (KeyValuePair<string, SDFTreeNode> item in sDFTree.childs)
        {
            AchievementData data = new AchievementData();
            data.ID          = item.Key;
            data.Name        = item.Value["Name"];
            data.Description = item.Value["Desc"];
            data.IconPath    = item.Value["Icon"];
            data.RewardXP    = item.Value.hasAttribute("RewardXP") ? int.Parse(item.Value["RewardXP"]) : 0;
            data.RewardGems  = item.Value.hasAttribute("RewardGems") ? int.Parse(item.Value["RewardGems"]) : 0;
            
            // Sync with profile
            data.IsUnlocked = Singleton<Profile>.instance.GetAchievementsDisplayed().Contains(data.ID);
            
            mAchievements.Add(data);
        }

        mInitialized = true;
    }

    public AchievementData GetData(string id)
    {
        Initialize();
        return mAchievements.Find(a => a.ID == id);
    }
}
