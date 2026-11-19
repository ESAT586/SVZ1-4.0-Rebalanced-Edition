using System.Collections.Generic;
using UnityEngine;

public class LegionCooldownDialog : IDialog
{
	private const float kTransitionSpeed = 0.3f;

	private readonly Vector2 kCostOffsetFromButton = new Vector2(-4f, 0f);

	private StoreData.Item mItemDesc;

	private string mModuleID;

	private OnSUIGenericCallback mOnItemPurchased;

	private OnSUIGenericCallback mOnSettingsChanged;

	private SUILayout mLayout;

	private bool mDone;

	private class TierPillUI
	{
		public int tier;
		public int seconds;
		public bool unlocked;
		public Vector2 position;
		public SUIButton button;
	}

	private List<TierPillUI> mTierPills;

	private SUILabel mCompareLabel;

	private SUISprite mSelectionGlow;

	private CostDisplay mPurchaseCostDisplay;

	private CostDisplay mMaxTextDisplay;

	private bool mCostAddedToLayout;

	private bool mMaxAddedToLayout;

	public bool isDone
	{
		get
		{
			return mDone && !mLayout.isAnimating;
		}
	}

	public bool isBlocking
	{
		get
		{
			return true;
		}
	}

	public float priority
	{
		get
		{
			return mLayout.basePriority;
		}
		set
		{
			mLayout.basePriority = value;
		}
	}

	public LegionCooldownDialog(
		StoreData.Item itemDesc,
		OnSUIGenericCallback onItemPurchased,
		OnSUIGenericCallback onSettingsChanged)
	{
		mItemDesc = itemDesc;
		mModuleID = (mItemDesc != null && !string.IsNullOrEmpty(mItemDesc.id)) ? mItemDesc.id : "LegionOnTheLoose";
		mOnItemPurchased = onItemPurchased;
		mOnSettingsChanged = onSettingsChanged;
		mLayout = new SUILayout("Layouts/ShopDetails");
		RenderContent();
		mLayout.AnimateIn(kTransitionSpeed);
		((SUIButton)mLayout["back"]).onButtonPressed = Close;
	}

	public void Destroy()
	{
		mTierPills = null;
		mCompareLabel = null;
		mSelectionGlow = null;
		mPurchaseCostDisplay = null;
		mMaxTextDisplay = null;
		mLayout.Destroy();
		mLayout = null;
	}

	public void Update()
	{
		mLayout.Update();
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Close();
		}
	}

	private void AddAnimatedWidget(string key, SUIProcess widget)
	{
		SUILayout.ObjectData od = new SUILayout.ObjectData();
		od.obj = widget;
		od.animAlpha = new SUILayoutAnim.AnimFloat(1f, 0f, new SUILayout.NormalRange(0f, 1f), Ease.Linear);
		mLayout.Add(key, od);
	}

	private void RenderContent()
	{
		SUISprite icon = (SUISprite)mLayout["icon"];
		if (icon != null)
		{
			icon.texture = (mItemDesc != null && !string.IsNullOrEmpty(mItemDesc.icon))
				? mItemDesc.icon
				: "Sprites/Icons/upgrade_legion_on_the_loose";
			icon.visible = true;
		}
		bool isDFA = (mModuleID == "DeathFromAbove");
		SUILabel title = (SUILabel)mLayout["title"];
		if (title != null)
		{
			title.text = isDFA
				? StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("dfa_speed_title", "GÖKTEN GELEN ÖLÜM GELİŞ SÜRESİ")
				: StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_speed_title", "BAŞIBOŞ LEJYON GELİŞ SÜRESİ");
			title.visible = true;
		}
		SUILabel details = (SUILabel)mLayout["detailsSmall"];
		if (details != null)
		{
			details.text = isDFA
				? StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("dfa_speed_desc", "Gökten Gelen Ölüm birliklerinin kaç saniyede bir geleceğini elmasla kısalt.")
				: StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_speed_desc", "Başıboş Lejyon askerlerinin kaç saniyede bir geleceğini elmasla kısalt.");
			details.visible = true;
		}

		mCompareLabel = new SUILabel("default18");
		mCompareLabel.anchor = TextAnchor.MiddleCenter;
		mCompareLabel.alignment = TextAlignment.Center;
		mCompareLabel.maxWidth = 600;
		mCompareLabel.shadowColor = Color.black;
		mCompareLabel.shadowOffset = new Vector2(2f, 2f);
		mCompareLabel.position = new Vector2(512f, 308f);
		mCompareLabel.priority = 2f;
		mCompareLabel.alpha = 0f;
		AddAnimatedWidget("legion_cd_compare", mCompareLabel);

		mSelectionGlow = new SUISprite("Sprites/Icons/potion_glow");
		mSelectionGlow.scale = new Vector2(0.86f, 0.50f);
		mSelectionGlow.priority = 2.2f;
		mSelectionGlow.alpha = 0f;
		AddAnimatedWidget("legion_cd_glow", mSelectionGlow);

		int totalTiers = Profile.kLegionCooldownSeconds.Length;
		mTierPills = new List<TierPillUI>(totalTiers);
		for (int i = 0; i < totalTiers; i++)
		{
			int secs = Profile.kLegionCooldownSeconds[i];
			Vector2 pos;
			if (i < 6)
			{
				pos = new Vector2(242f + (float)i * 108f, 374f);
			}
			else
			{
				pos = new Vector2(262f + (float)(i - 6) * 125f, 448f);
			}

			SUIButton btn = new SUIButton("default18");
			btn.SetFrames("Sprites/Menus/stats_button_small", "Sprites/Menus/stats_button_small", "Sprites/Menus/stats_button_small");
			btn.scale = new Vector2(0.44f, 0.56f);
			btn.offsetWhenPressed = new Vector2(3f, 3f);
			btn.position = pos;
			btn.priority = 3f;
			btn.alpha = 0f;
			if (btn.label != null)
			{
				btn.label.shadowColor = Color.black;
				btn.label.shadowOffset = new Vector2(2f, 2f);
			}
			int capturedTier = i;
			btn.onButtonPressed = delegate
			{
				OnSelectTier(capturedTier);
			};
			AddAnimatedWidget("legion_cd_tier_" + i, btn);

			TierPillUI pill = new TierPillUI();
			pill.tier = i;
			pill.seconds = secs;
			pill.position = pos;
			pill.button = btn;
			mTierPills.Add(pill);
		}

		SUIButton purchaseBtn = (SUIButton)mLayout["purchaseBtn"];
		if (purchaseBtn != null)
		{
			purchaseBtn.onButtonPressed = OnUpgradeSpeed;
		}
		RefreshUI(true);
	}

	private void OnSelectTier(int tier)
	{
		if (tier < 0 || tier > Singleton<Profile>.instance.GetModuleCooldownLevel(mModuleID))
		{
			return;
		}
		Singleton<SUISoundManager>.instance.Play("selectSlot");
		Singleton<Profile>.instance.SetModuleCooldownSelectedLevel(mModuleID, tier);
		Singleton<Profile>.instance.Save();
		RefreshUI(false);
	}

	private void OnUpgradeSpeed()
	{
		int nextGemCost = Singleton<Profile>.instance.GetModuleNextCooldownGemCost(mModuleID);
		if (nextGemCost <= 0)
		{
			return;
		}
		Cost cost = new Cost(0, nextGemCost, 0f);
		if (!cost.canAfford)
		{
			ShowNotEnoughGems();
			return;
		}
		int prevMax = Singleton<Profile>.instance.GetModuleCooldownLevel(mModuleID);
		int prevSel = Singleton<Profile>.instance.GetModuleCooldownSelectedLevel(mModuleID);
		bool isDFA = (mModuleID == "DeathFromAbove");
		cost.Spend(isDFA ? "DeathFromAboveCooldown" : "LegionCooldown");
		int nextLevel = prevMax + 1;
		Singleton<Profile>.instance.SetModuleCooldownLevel(mModuleID, nextLevel);
		if (prevSel == prevMax)
		{
			Singleton<Profile>.instance.SetModuleCooldownSelectedLevel(mModuleID, nextLevel);
		}
		Singleton<Profile>.instance.Save();
		Singleton<Analytics>.instance.LogEvent(isDFA ? "Upgrade_DeathFromAboveCooldown" : "Upgrade_LegionCooldown", string.Empty);
		RefreshUI(false);
	}

	private void ShowNotEnoughGems()
	{
		if (WeakGlobalInstance<DialogHandler>.instance == null)
		{
			Close();
			return;
		}
		float dialogPriority = priority;
		StoreData.Item itemCopy = mItemDesc;
		OnSUIGenericCallback purchasedCb = mOnItemPurchased;
		OnSUIGenericCallback settingsCb = mOnSettingsChanged;
		WeakGlobalInstance<DialogHandler>.instance.PushCreator(() => new StoreItemDisplayDialog(itemCopy)
		{
			priority = dialogPriority,
			onItemPurchased = purchasedCb,
			onSettingsChanged = settingsCb
		});
		WeakGlobalInstance<DialogHandler>.instance.PushCreator(() => new LegionCooldownDialog(itemCopy, purchasedCb, settingsCb)
		{
			priority = dialogPriority
		});
		WeakGlobalInstance<DialogHandler>.instance.PushCreator(() => new StoreNotEnoughDialog(itemCopy)
		{
			priority = dialogPriority
		});
		mDone = true;
		if (mLayout["back"] != null) ((SUIButton)mLayout["back"]).onButtonPressed = null;
		if (mLayout["purchaseBtn"] != null) ((SUIButton)mLayout["purchaseBtn"]).onButtonPressed = null;
		mLayout.AnimateOut(kTransitionSpeed);
	}

	private void RefreshUI(bool initialBuild)
	{
		int unlockedTier = Singleton<Profile>.instance.GetModuleCooldownLevel(mModuleID);
		int selectedTier = Singleton<Profile>.instance.GetModuleCooldownSelectedLevel(mModuleID);
		int currentSecs = Singleton<Profile>.instance.GetLegionCooldownSecondsForTier(selectedTier);
		int maxTierIdx = Profile.kLegionCooldownSeconds.Length - 1;
		int nextGemCost = Singleton<Profile>.instance.GetModuleNextCooldownGemCost(mModuleID);

		string secFmt = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_sec_short", "{0}sn");
		TierPillUI activePill = null;
		for (int i = 0; i < mTierPills.Count; i++)
		{
			TierPillUI p = mTierPills[i];
			p.unlocked = (p.tier <= unlockedTier);
			p.button.enabled = p.unlocked;
			string lbl = string.Format(secFmt, p.seconds);
			if (p.tier == selectedTier)
			{
				activePill = p;
				p.button.text = "[" + lbl + "]";
				if (p.button.label != null) p.button.label.fontColor = Color.green;
			}
			else if (p.unlocked)
			{
				p.button.text = lbl;
				if (p.button.label != null) p.button.label.fontColor = Color.white;
			}
			else
			{
				p.button.text = lbl;
				if (p.button.label != null) p.button.label.fontColor = new Color(0.55f, 0.55f, 0.55f);
			}
		}

		if (mSelectionGlow != null && activePill != null)
		{
			mSelectionGlow.position = activePill.position;
			mSelectionGlow.visible = true;
		}

		if (mCompareLabel != null)
		{
			if (unlockedTier >= maxTierIdx)
			{
				string maxFmt = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_speed_max", "MAKSİMUM HIZ: HER {0} SANİYEDE BİR");
				mCompareLabel.text = string.Format(maxFmt, currentSecs);
				mCompareLabel.fontColor = Color.green;
			}
			else
			{
				int nextSecs = Singleton<Profile>.instance.GetLegionCooldownSecondsForTier(unlockedTier + 1);
				string cmpFmt = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_speed_cmp", "MEVCUT: {0} SN   ->   SONRAKİ: {1} SN");
				mCompareLabel.text = string.Format(cmpFmt, currentSecs, nextSecs);
				mCompareLabel.fontColor = new Color(1f, 0.92f, 0.45f);
			}
		}

		SUIButton purchaseBtn = (SUIButton)mLayout["purchaseBtn"];
		if (purchaseBtn == null)
		{
			return;
		}
		if (nextGemCost > 0)
		{
			purchaseBtn.visible = true;
			Cost nextCost = new Cost(0, nextGemCost, 0f);
			if (mPurchaseCostDisplay == null)
			{
				mPurchaseCostDisplay = new CostDisplay("default32");
				mPurchaseCostDisplay.priority = purchaseBtn.priority + 0.1f;
				mPurchaseCostDisplay.alpha = initialBuild ? 0f : 1f;
				AddAnimatedWidget("legion_cd_cost", mPurchaseCostDisplay);
				mCostAddedToLayout = true;
			}
			mPurchaseCostDisplay.SetCost(nextCost);
			mPurchaseCostDisplay.position = purchaseBtn.position + kCostOffsetFromButton - new Vector2(mPurchaseCostDisplay.width / 2f, mPurchaseCostDisplay.height / 2f);
			mPurchaseCostDisplay.visible = true;
			if (mMaxTextDisplay != null) mMaxTextDisplay.visible = false;
		}
		else
		{
			purchaseBtn.visible = false;
			if (mPurchaseCostDisplay != null && mCostAddedToLayout)
			{
				mPurchaseCostDisplay.visible = false;
			}
			if (mMaxTextDisplay == null)
			{
				mMaxTextDisplay = new CostDisplay("default32");
				mMaxTextDisplay.SetCost(new Cost(0, 0, 0f), "MAX");
				mMaxTextDisplay.position = purchaseBtn.position - new Vector2(mMaxTextDisplay.width / 2f, mMaxTextDisplay.height / 2f);
				mMaxTextDisplay.priority = purchaseBtn.priority + 0.1f;
				mMaxTextDisplay.alpha = initialBuild ? 0f : 1f;
				AddAnimatedWidget("legion_cd_max", mMaxTextDisplay);
				mMaxAddedToLayout = true;
			}
			if (mMaxAddedToLayout) mMaxTextDisplay.visible = true;
		}
	}

	private void Close()
	{
		if (mLayout["back"] != null) ((SUIButton)mLayout["back"]).onButtonPressed = null;
		if (mLayout["purchaseBtn"] != null) ((SUIButton)mLayout["purchaseBtn"]).onButtonPressed = null;
		if (mTierPills != null)
		{
			for (int i = 0; i < mTierPills.Count; i++)
			{
				if (mTierPills[i].button != null) mTierPills[i].button.onButtonPressed = null;
			}
		}
		mDone = true;
		mLayout.AnimateOut(kTransitionSpeed);
		if (WeakGlobalInstance<DialogHandler>.instance != null)
		{
			WeakGlobalInstance<DialogHandler>.instance.FadeOut();
		}
	}
}
