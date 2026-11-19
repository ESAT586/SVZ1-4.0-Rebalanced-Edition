using System.Collections.Generic;
using UnityEngine;

public class StoreItemDisplayDialog : IDialog
{
	private const float kTransitionSpeed = 0.3f;

	private const float kFaderLevel = 0.7f;

	private readonly Vector2 kCostOffsetFromButton = new Vector2(-4f, 0f);

	private StoreData.Item mItemDesc;

	private OnSUIGenericCallback mOnItemPurchased;

	private OnSUIGenericCallback mOnSettingsChanged;

	private SUILayout mLayout;

	private bool mDone;

	private SpoilsDisplay mSpoilsDisplay;

	private class LegionSlotUI
	{
		public int level;
		public string helperName;
		public int spawnCount;
		public bool unlocked;
		public Vector2 position;
		public SUIButton button;
		public SUILabel label;
	}

	private List<LegionSlotUI> mLegionSlots;

	private SUIButton mLegionToggleBtn;

	private SUIButton mLegionSpeedBtn;

	private SUILabel mLegionStatusLabel;

	private SUISprite mLegionSelectionGlow;

	private bool mLegionChanged;

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

	public OnSUIGenericCallback onItemPurchased
	{
		get
		{
			return mOnItemPurchased;
		}
		set
		{
			mOnItemPurchased = value;
		}
	}

	public OnSUIGenericCallback onSettingsChanged
	{
		get
		{
			return mOnSettingsChanged;
		}
		set
		{
			mOnSettingsChanged = value;
		}
	}

	public StoreItemDisplayDialog(StoreData.Item itemDesc)
	{
		mItemDesc = itemDesc;
		mLayout = new SUILayout("Layouts/ShopDetails");
		RenderItem();
		mLayout.AnimateIn(0.3f);
		SUIButton sUIButton = (SUIButton)mLayout["purchaseBtn"];
		((SUIButton)mLayout["back"]).onButtonPressed = Close;

		if (sUIButton != null)
		{
			sUIButton.onButtonPressed = OnPurchase;
			if (itemDesc.locked)
			{
				sUIButton.visible = false;
			}
			else if (itemDesc.isMaxLevel)
			{
				sUIButton.visible = false;
				CostDisplay maxDisplay = new CostDisplay("default32");
				maxDisplay.SetCost(new Cost(0, 0, 0f), "MAX");
				maxDisplay.position = sUIButton.position - new Vector2(maxDisplay.width / 2f, maxDisplay.height / 2f);
				maxDisplay.priority = sUIButton.priority + 0.1f;
				maxDisplay.alpha = 0f;
				
				SUILayout.ObjectData objectData = new SUILayout.ObjectData();
				objectData.obj = maxDisplay;
				objectData.animAlpha = new SUILayoutAnim.AnimFloat(1f, 0f, new SUILayout.NormalRange(0f, 1f), Ease.Linear);
				mLayout.Add("max_text", objectData);
			}
			else
			{
				SetupPurchaseButton(sUIButton, mItemDesc.cost);
			}
		}
	}

	public void Destroy()
	{
		if (mSpoilsDisplay != null)
		{
			mSpoilsDisplay.Destroy();
			mSpoilsDisplay = null;
		}
		mLegionSlots = null;
		mLegionToggleBtn = null;
		mLegionSpeedBtn = null;
		mLegionStatusLabel = null;
		mLegionSelectionGlow = null;
		mLayout.Destroy();
		mLayout = null;
	}

	public void Update()
	{
		mLayout.Update();
		if (mSpoilsDisplay != null)
		{
			mSpoilsDisplay.Update();
		}
		if (!mLayout.isAnimating)
		{
			UpdateInputs();
		}
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Close();
		}
	}

	private void UpdateInputs()
	{
	}

	private void RenderItem()
	{
		if (mItemDesc.locked)
		{
			try
			{
				((SUISprite)mLayout["icon"]).texture = mItemDesc.icon + "_locked";
			}
			catch
			{
				((SUISprite)mLayout["icon"]).texture = mItemDesc.icon;
			}
			((SUILabel)mLayout["unlockCondition"]).text = mItemDesc.unlockCondition;
			((SUILabel)mLayout["unlockCondition"]).visible = true;
			if (mItemDesc.isFoundInPresent)
			{
				((SUILabel)mLayout["findInPresent"]).text = Singleton<Localizer>.instance.Get("store_findInPresent");
				((SUILabel)mLayout["findInPresent"]).visible = true;
				((SUISprite)mLayout["presentIcon0"]).visible = false;
				((SUISprite)mLayout["presentIcon1"]).visible = false;
				((SUISprite)mLayout["presentIcon2"]).visible = false;
				Vector2 vector = new Vector2(128f, 0f);
				Vector2 position = ((SUISprite)mLayout["presentIcon0"]).position;
				Vector2 position2 = new Vector2(position.x - (float)(mItemDesc.containedInPresent.Count - 1) * vector.x / 2f, position.y);
				for (int i = 0; i < mItemDesc.containedInPresent.Count; i++)
				{
					((SUISprite)mLayout["presentIcon" + i]).texture = mItemDesc.containedInPresent[i];
					((SUISprite)mLayout["presentIcon" + i]).position = position2;
					position2 += vector;
					((SUISprite)mLayout["presentIcon" + i]).visible = true;
				}
			}
		}
		else
		{
			((SUISprite)mLayout["icon"]).texture = mItemDesc.icon;
		}
		((SUISprite)mLayout["icon"]).visible = true;
		((SUILabel)mLayout["title"]).text = ((!mItemDesc.locked) ? mItemDesc.title : mItemDesc.unlockTitle);
		((SUILabel)mLayout["title"]).visible = true;
		mItemDesc.details.Render(mLayout, mItemDesc.locked);
		if (mItemDesc.contentList != null)
		{
			mSpoilsDisplay = new SpoilsDisplay(mLayout, mItemDesc.contentList, "default18", new Vector2(SUIScreen.width / 2f, SUIScreen.height / 2f - 20f), 2000f, 5, 144f, string.Empty);
		}
		if ((mItemDesc.id == "LegionOnTheLoose" || mItemDesc.id == "DeathFromAbove") && !mItemDesc.locked && Singleton<Profile>.instance.GetModuleUnlockedLevel(mItemDesc.id) >= 1)
		{
			BuildLegionSelector();
		}
	}

	private void AddAnimatedWidget(string key, SUIProcess widget)
	{
		SUILayout.ObjectData od = new SUILayout.ObjectData();
		od.obj = widget;
		od.animAlpha = new SUILayoutAnim.AnimFloat(1f, 0f, new SUILayout.NormalRange(0f, 1f), Ease.Linear);
		mLayout.Add(key, od);
	}

	private void BuildLegionSelector()
	{
		string moduleID = mItemDesc.id;
		SDFTreeNode legionRoot = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/" + moduleID);
		if (legionRoot == null)
		{
			return;
		}
		int unlockedLevel = Singleton<Profile>.instance.GetModuleUnlockedLevel(moduleID);

		SUISprite mainIcon = (SUISprite)mLayout["icon"];
		if (mainIcon != null)
		{
			mainIcon.position = new Vector2(242f, 212f);
		}
		SUILabel detailsSmall = (SUILabel)mLayout["detailsSmall"];
		if (detailsSmall != null)
		{
			detailsSmall.position = new Vector2(308f, 224f);
			detailsSmall.maxWidth = 335;
			detailsSmall.maxLines = 2;
			detailsSmall.text = detailsSmall.text;
		}
		SUILabel titleLabel = (SUILabel)mLayout["title"];
		if (titleLabel != null)
		{
			titleLabel.position = new Vector2(308f, 190f);
			titleLabel.maxWidth = 330;
			titleLabel.maxLines = 2;
			titleLabel.text = titleLabel.text;
		}

		mLegionSpeedBtn = new SUIButton("default18");
		mLegionSpeedBtn.SetFrames("Sprites/Menus/stats_button_small", "Sprites/Menus/stats_button_small", "Sprites/Menus/stats_button_small");
		mLegionSpeedBtn.scale = new Vector2(1.02f, 0.72f);
		mLegionSpeedBtn.offsetWhenPressed = new Vector2(3f, 3f);
		mLegionSpeedBtn.position = new Vector2(746f, 212f);
		mLegionSpeedBtn.priority = 5f;
		mLegionSpeedBtn.alpha = 0f;
		if (mLegionSpeedBtn.label != null)
		{
			mLegionSpeedBtn.label.shadowColor = Color.black;
			mLegionSpeedBtn.label.shadowOffset = new Vector2(2f, 2f);
		}
		mLegionSpeedBtn.onButtonPressed = OpenCooldownDialog;
		AddAnimatedWidget("legion_speed_btn", mLegionSpeedBtn);

		mLegionStatusLabel = new SUILabel("default18");
		mLegionStatusLabel.anchor = TextAnchor.MiddleLeft;
		mLegionStatusLabel.alignment = TextAlignment.Left;
		mLegionStatusLabel.maxWidth = 435;
		mLegionStatusLabel.shadowColor = Color.black;
		mLegionStatusLabel.shadowOffset = new Vector2(2f, 2f);
		mLegionStatusLabel.position = new Vector2(205f, 290f);
		mLegionStatusLabel.priority = 2f;
		mLegionStatusLabel.alpha = 0f;
		AddAnimatedWidget("legion_status_label", mLegionStatusLabel);

		mLegionToggleBtn = new SUIButton("default18");
		mLegionToggleBtn.SetFrames("Sprites/Menus/stats_button_small", "Sprites/Menus/stats_button_small", "Sprites/Menus/stats_button_small");
		mLegionToggleBtn.scale = new Vector2(1.02f, 0.72f);
		mLegionToggleBtn.offsetWhenPressed = new Vector2(3f, 3f);
		mLegionToggleBtn.position = new Vector2(746f, 274f);
		mLegionToggleBtn.priority = 5f;
		mLegionToggleBtn.alpha = 0f;
		if (mLegionToggleBtn.label != null)
		{
			mLegionToggleBtn.label.shadowColor = Color.black;
			mLegionToggleBtn.label.shadowOffset = new Vector2(2f, 2f);
		}
		mLegionToggleBtn.onButtonPressed = OnToggleLegionEnabled;
		AddAnimatedWidget("legion_toggle_btn", mLegionToggleBtn);

		mLegionSelectionGlow = new SUISprite("Sprites/Icons/potion_glow");
		mLegionSelectionGlow.scale = new Vector2(0.70f, 0.70f);
		mLegionSelectionGlow.priority = 2.2f;
		mLegionSelectionGlow.alpha = 0f;
		AddAnimatedWidget("legion_select_glow", mLegionSelectionGlow);

		int totalLevels = 0;
		for (int lvl = 1; lvl <= 10; lvl++)
		{
			if (legionRoot.to(string.Format("{0:000}", lvl)) != null)
			{
				totalLevels++;
			}
		}
		mLegionSlots = new List<LegionSlotUI>(totalLevels);
		int slotIdx = 0;
		for (int lvl = 1; lvl <= 10; lvl++)
		{
			SDFTreeNode lvlNode = legionRoot.to(string.Format("{0:000}", lvl));
			if (lvlNode == null)
			{
				continue;
			}
			string helperID = lvlNode["helperID"];
			int spawnNum = 1;
			if (lvlNode.hasAttribute("num"))
			{
				int.TryParse(lvlNode["num"], out spawnNum);
			}
			string helperName = helperID;
			string iconPath = mItemDesc.icon;
			SDFTreeNode helperNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/helpers_zm/" + helperID);
			if (helperNode != null)
			{
				if (helperNode.hasAttribute("displayName"))
				{
					helperName = Singleton<Localizer>.instance.Parse(helperNode["displayName"]);
				}
				if (helperNode.hasAttribute("HUDIcon"))
				{
					iconPath = helperNode["HUDIcon"];
				}
			}
			if (lvlNode.hasAttribute("HUDIcon"))
			{
				iconPath = lvlNode["HUDIcon"];
			}

			Vector2 pos;
			if (totalLevels <= 5)
			{
				float startX = 512f - (float)(totalLevels - 1) * 70f;
				pos = new Vector2(startX + (float)slotIdx * 140f, 398f);
			}
			else if (totalLevels == 6)
			{
				int col = slotIdx % 3;
				int row = slotIdx / 3;
				pos = new Vector2(347f + (float)col * 165f, (row == 0) ? 344f : 452f);
			}
			else
			{
				int col = slotIdx % 5;
				int row = slotIdx / 5;
				pos = new Vector2(240f + (float)col * 136f, (row == 0) ? 344f : 452f);
			}
			slotIdx++;
			bool isUnlocked = lvl <= unlockedLevel;
			string normalTex = isUnlocked ? iconPath : (iconPath + "_locked");
			string lockedTex = iconPath + "_locked";

			SUIButton slotBtn = new SUIButton();
			slotBtn.SetFrames(normalTex, normalTex, lockedTex);
			slotBtn.scale = new Vector2(0.54f, 0.54f);
			slotBtn.offsetWhenPressed = new Vector2(3f, 3f);
			slotBtn.position = pos;
			slotBtn.priority = 3f;
			slotBtn.enabled = isUnlocked;
			slotBtn.alpha = 0f;
			int capturedLevel = lvl;
			slotBtn.onButtonPressed = delegate
			{
				OnSelectLegionLevel(capturedLevel);
			};
			AddAnimatedWidget("legion_slot_btn_" + lvl, slotBtn);

			SUILabel slotLbl = new SUILabel("default18");
			slotLbl.anchor = TextAnchor.MiddleCenter;
			slotLbl.alignment = TextAlignment.Center;
			slotLbl.shadowColor = Color.black;
			slotLbl.shadowOffset = new Vector2(2f, 2f);
			slotLbl.position = pos + new Vector2(0f, 47f);
			slotLbl.priority = 3.2f;
			slotLbl.alpha = 0f;
			AddAnimatedWidget("legion_slot_lbl_" + lvl, slotLbl);

			LegionSlotUI slotUI = new LegionSlotUI();
			slotUI.level = lvl;
			slotUI.helperName = helperName;
			slotUI.spawnCount = spawnNum;
			slotUI.unlocked = isUnlocked;
			slotUI.position = pos;
			slotUI.button = slotBtn;
			slotUI.label = slotLbl;
			mLegionSlots.Add(slotUI);
		}
		RefreshLegionSelectorUI();
	}

	private void OnSelectLegionLevel(int level)
	{
		if (level < 1 || level > Singleton<Profile>.instance.GetModuleUnlockedLevel(mItemDesc.id))
		{
			return;
		}
		Singleton<SUISoundManager>.instance.Play("selectSlot");
		Singleton<Profile>.instance.SetModuleSelectedLevel(mItemDesc.id, level);
		Singleton<Profile>.instance.Save();
		mLegionChanged = true;
		RefreshLegionSelectorUI();
	}

	private void OnToggleLegionEnabled()
	{
		Singleton<SUISoundManager>.instance.Play("selectSlot");
		Singleton<Profile>.instance.SetModuleEnabled(mItemDesc.id, !Singleton<Profile>.instance.IsModuleEnabled(mItemDesc.id));
		Singleton<Profile>.instance.Save();
		mLegionChanged = true;
		RefreshLegionSelectorUI();
	}

	private void RefreshLegionSelectorUI()
	{
		if (mLegionSlots == null)
		{
			return;
		}
		bool enabled = Singleton<Profile>.instance.IsModuleEnabled(mItemDesc.id);
		int selectedLevel = Singleton<Profile>.instance.GetModuleSelectedLevel(mItemDesc.id);
		LegionSlotUI activeSlot = null;

		string lvlFmt = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_lvl_short", "Sv.{0}");
		for (int i = 0; i < mLegionSlots.Count; i++)
		{
			LegionSlotUI s = mLegionSlots[i];
			bool isSelected = (s.level == selectedLevel);
			if (isSelected)
			{
				activeSlot = s;
			}
			string txt = string.Format(lvlFmt, s.level);
			if (isSelected)
			{
				s.label.text = "[" + txt + "]";
				s.label.fontColor = enabled ? Color.green : new Color(1f, 0.72f, 0.35f);
			}
			else if (s.unlocked)
			{
				s.label.text = txt;
				s.label.fontColor = enabled ? Color.white : new Color(0.72f, 0.72f, 0.72f);
			}
			else
			{
				s.label.text = txt;
				s.label.fontColor = new Color(0.45f, 0.45f, 0.45f);
			}
		}

		if (mLegionSelectionGlow != null)
		{
			if (activeSlot != null)
			{
				mLegionSelectionGlow.position = activeSlot.position;
				mLegionSelectionGlow.visible = true;
			}
			else
			{
				mLegionSelectionGlow.visible = false;
			}
		}

		if (mLegionToggleBtn != null && mLegionToggleBtn.label != null)
		{
			if (enabled)
			{
				mLegionToggleBtn.text = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_toggle_on", "DESTEK: AÇIK");
				mLegionToggleBtn.label.fontColor = Color.green;
			}
			else
			{
				mLegionToggleBtn.text = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_toggle_off", "DESTEK: KAPALI");
				mLegionToggleBtn.label.fontColor = new Color(1f, 0.45f, 0.38f);
			}
		}

		if (mLegionSpeedBtn != null && mLegionSpeedBtn.label != null)
		{
			int secs = (int)Singleton<Profile>.instance.GetModuleCooldownSeconds(mItemDesc.id);
			string speedFmt = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_speed_btn", "SÜRE: {0} SN");
			mLegionSpeedBtn.text = string.Format(speedFmt, secs);
			bool isMaxCd = Singleton<Profile>.instance.GetModuleCooldownLevel(mItemDesc.id) >= Profile.kLegionCooldownSeconds.Length - 1;
			mLegionSpeedBtn.label.fontColor = isMaxCd ? Color.green : new Color(0.55f, 0.92f, 1f);
		}

		if (mLegionStatusLabel != null)
		{
			if (!enabled)
			{
				mLegionStatusLabel.text = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_disabled_hint", "KAPALI - DESTEK GÖNDERİLMEZ");
				mLegionStatusLabel.fontColor = new Color(1f, 0.45f, 0.38f);
			}
			else if (activeSlot != null)
			{
				string selFmt = StoreAvailability_LegionsOnTheLoose.GetLocalizedOrFallback("legion_selected_format", "SEÇİLİ: Sv.{0} {1} (x{2})");
				mLegionStatusLabel.text = string.Format(selFmt, activeSlot.level, activeSlot.helperName, activeSlot.spawnCount);
				mLegionStatusLabel.fontColor = new Color(1f, 0.92f, 0.45f);
			}
		}
	}

	private void OnPurchase()
	{
		if (!mItemDesc.cost.canAfford)
		{
			ShowPurchaseCurrency(mItemDesc.cost);
			return;
		}
		mItemDesc.cost.Spend(mItemDesc.id);
		mItemDesc.Apply();
		if (mOnItemPurchased != null)
		{
			mOnItemPurchased();
		}
		Close();
	}

	private void ShowPurchaseCurrency(Cost trigger)
	{
		if (WeakGlobalInstance<DialogHandler>.instance == null)
		{
			Close();
			return;
		}
		float dialogPriority = priority;
		WeakGlobalInstance<DialogHandler>.instance.PushCreator(() => new StoreItemDisplayDialog(mItemDesc)
		{
			priority = dialogPriority,
			onItemPurchased = onItemPurchased
		});
		WeakGlobalInstance<DialogHandler>.instance.PushCreator(() => new StoreNotEnoughDialog(mItemDesc)
		{
			priority = dialogPriority
		});
		Close();
	}

	private void OpenCooldownDialog()
	{
		if (WeakGlobalInstance<DialogHandler>.instance == null)
		{
			return;
		}
		Singleton<SUISoundManager>.instance.Play("selectSlot");
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
		mDone = true;
		if (mLayout["purchaseBtn"] != null) ((SUIButton)mLayout["purchaseBtn"]).onButtonPressed = null;
		if (mLayout["back"] != null) ((SUIButton)mLayout["back"]).onButtonPressed = null;
		if (mLegionToggleBtn != null) mLegionToggleBtn.onButtonPressed = null;
		if (mLegionSpeedBtn != null) mLegionSpeedBtn.onButtonPressed = null;
		if (mLegionSlots != null)
		{
			for (int i = 0; i < mLegionSlots.Count; i++)
			{
				if (mLegionSlots[i].button != null) mLegionSlots[i].button.onButtonPressed = null;
			}
		}
		mLayout.AnimateOut(kTransitionSpeed);
	}

	private void Close()
	{
		((SUIButton)mLayout["purchaseBtn"]).onButtonPressed = null;
		((SUIButton)mLayout["back"]).onButtonPressed = null;
		if (mLegionToggleBtn != null)
		{
			mLegionToggleBtn.onButtonPressed = null;
		}
		if (mLegionSpeedBtn != null)
		{
			mLegionSpeedBtn.onButtonPressed = null;
		}
		if (mLegionSlots != null)
		{
			for (int i = 0; i < mLegionSlots.Count; i++)
			{
				if (mLegionSlots[i].button != null)
				{
					mLegionSlots[i].button.onButtonPressed = null;
				}
			}
		}
		if (mLegionChanged && mOnSettingsChanged != null)
		{
			mOnSettingsChanged();
		}
		mDone = true;
		mLayout.AnimateOut(0.3f);
		if (WeakGlobalInstance<DialogHandler>.instance != null)
		{
			WeakGlobalInstance<DialogHandler>.instance.FadeOut();
		}
	}

	private void SetupPurchaseButton(SUIButton btn, Cost cost)
	{
		CostDisplay costDisplay = new CostDisplay("default32");
		costDisplay.SetCost(cost);
		costDisplay.position = btn.position + kCostOffsetFromButton - new Vector2(costDisplay.width / 2f, costDisplay.height / 2f);
		costDisplay.priority = btn.priority + 0.1f;
		costDisplay.alpha = 0f;
		SUILayout.ObjectData objectData = new SUILayout.ObjectData();
		objectData.obj = costDisplay;
		objectData.animAlpha = new SUILayoutAnim.AnimFloat(1f, 0f, new SUILayout.NormalRange(0f, 1f), Ease.Linear);
		mLayout.Add("button_currency", objectData);
	}
}
