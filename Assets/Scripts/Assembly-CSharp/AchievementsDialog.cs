using UnityEngine;
using System.Collections.Generic;

public class AchievementsDialog : IDialog
{
    private SUILayout mLayout;
    private bool mDone;
    private int mCurrentPage = 0;
    private const int kItemsPerPage = 4;

    public bool isDone { get { return mDone && !mLayout.isAnimating; } }
    public bool isBlocking { get { return true; } }
    public float priority { get; set; }

    public AchievementsDialog()
    {
        mLayout = new SUILayout("Layouts/AchievementsDialog");
        
        // Buttons
        if (mLayout.Exists("backBtn"))
            ((SUIButton)mLayout["backBtn"]).onButtonPressed = Close;
            
        if (mLayout.Exists("prevBtn"))
            ((SUIButton)mLayout["prevBtn"]).onButtonPressed = delegate { mCurrentPage = Mathf.Max(0, mCurrentPage - 1); RefreshPage(); };
            
        if (mLayout.Exists("nextBtn"))
            ((SUIButton)mLayout["nextBtn"]).onButtonPressed = delegate { mCurrentPage++; RefreshPage(); };

        RefreshPage();
        mLayout.AnimateIn();
    }

    private void RefreshPage()
    {
        List<AchievementData> all = Singleton<AchievementsDatabase>.instance.AllAchievements;
        int maxPage = Mathf.Max(0, (all.Count - 1) / kItemsPerPage);
        if (mCurrentPage > maxPage) mCurrentPage = maxPage;

        if (mLayout.Exists("pageLbl"))
            ((SUILabel)mLayout["pageLbl"]).text = string.Format("PAGE {0}/{1}", mCurrentPage + 1, maxPage + 1);

        for (int i = 0; i < kItemsPerPage; i++)
        {
            int index = mCurrentPage * kItemsPerPage + i;
            string rowPrefix = "row" + (i + 1);
            
            bool hasData = index < all.Count;
            SetRowVisible(rowPrefix, hasData);
            
            if (hasData)
            {
                AchievementData data = all[index];
                if (mLayout.Exists(rowPrefix + "Title")) ((SUILabel)mLayout[rowPrefix + "Title"]).text = data.Name.ToUpper();
                if (mLayout.Exists(rowPrefix + "Desc"))  ((SUILabel)mLayout[rowPrefix + "Desc"]).text  = data.Description;
                
                if (mLayout.Exists(rowPrefix + "Status"))
                {
                    SUILabel statusLbl = (SUILabel)mLayout[rowPrefix + "Status"];
                    statusLbl.text = data.IsUnlocked ? "UNLOCKED" : "LOCKED";
                    statusLbl.fontColor = data.IsUnlocked ? Color.white : Color.gray;
                }

                if (mLayout.Exists(rowPrefix + "Icon"))
                {
                    SUISprite icon = (SUISprite)mLayout[rowPrefix + "Icon"];
                    icon.texture = data.IsUnlocked ? data.IconPath : "Sprites/Menus/achievement_kill"; // Fallback to kill icon if locked
                    icon.alpha = data.IsUnlocked ? 1f : 0.4f;
                }
            }
        }
        
        if (mLayout.Exists("prevBtn")) ((SUIButton)mLayout["prevBtn"]).visible = mCurrentPage > 0;
        if (mLayout.Exists("nextBtn")) ((SUIButton)mLayout["nextBtn"]).visible = (mCurrentPage + 1) * kItemsPerPage < all.Count;
    }
    
    private void SetRowVisible(string prefix, bool visible)
    {
        string[] widgets = { "Bg", "Icon", "Title", "Desc", "Status" };
        foreach (string w in widgets)
        {
            string id = prefix + w;
            if (mLayout.Exists(id)) 
            {
                SUIWidget widget = mLayout[id] as SUIWidget;
                if (widget != null) widget.visible = visible;
            }
        }
    }

    public void Update()
    {
        mLayout.Update();
        if (!mDone && Input.GetKeyUp(KeyCode.Escape)) Close();
    }

    public void Destroy()
    {
        mLayout.Destroy();
        mLayout = null;
    }

    private void Close()
    {
        mDone = true;
        mLayout.SetAllVisible(false);
        mLayout.AnimateOut();
        if (WeakGlobalInstance<DialogHandler>.instance != null)
            WeakGlobalInstance<DialogHandler>.instance.FadeOut();
    }
}
