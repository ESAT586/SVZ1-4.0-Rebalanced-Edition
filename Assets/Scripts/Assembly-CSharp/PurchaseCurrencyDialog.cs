using System.Collections.Generic;
using UnityEngine;

public class PurchaseCurrencyDialog : IDialog
{
	private class Tab
	{
		private SUISprite mTabSpriteRef;

		private SUISprite mTabGlowRef;

		private SUITouchArea mTouchAreaRef;

		private Vector2 mOriginalPosition;

		private Vector2 mSelectedPosition;

		public OnSUIGenericCallback onTouched
		{
			get
			{
				return mTouchAreaRef.onAreaTouched;
			}
			set
			{
				mTouchAreaRef.onAreaTouched = value;
			}
		}

		public bool selected
		{
			get
			{
				return mTabGlowRef.visible;
			}
			set
			{
				mTabGlowRef.visible = value;
				if (mTabGlowRef.visible)
				{
					mTabSpriteRef.position = mSelectedPosition;
				}
				else
				{
					mTabSpriteRef.position = mOriginalPosition;
				}
			}
		}

		public GameObject gameObjectForSound
		{
			get
			{
				return mTabSpriteRef.gameObject;
			}
		}

		public Tab(SUILayout layout, int index)
		{
			mTabSpriteRef = (SUISprite)layout["tab" + index];
			mTabGlowRef = (SUISprite)layout["tabGlow" + index];
			mTouchAreaRef = (SUITouchArea)layout["tabTouch" + index];
			mOriginalPosition = mTabSpriteRef.position;
			mSelectedPosition = mOriginalPosition + new Vector2(0f, -22f);
			mTabGlowRef.position = mSelectedPosition;
		}
	}

	private const float kTransitionSpeed = 0.3f;

	private const float kPurchaseAlphaWhenDisabled = 0.3f;

	private const int kNumListColumns = 2;

	private const int kExpectedFullDataNum = 14;

	private const int kNumTabs = 2;

	public static PurchaseCurrencyDialog instance;

	private SUIScrollList mPurchaseList;

	private IAPItemsListController mListController;

	private Rect kPurchaseListArea = new Rect(350f, 244f, 650f, 220f);

	private Vector2 kCellSize = new Vector2(320f, 105f);

	public float startTime;

	private static float TimeOutInSecs;

	public SUILayout mLayout;

	private float mPriority;

	private bool mDone;

	private List<Tab> mTabs = new List<Tab>(2);

	private int mSelectedTab;

	private bool mSomethingPurchased;

	private bool mTapJoyActivated;

	private float mMusicVolume = 1f;

	public bool m_onResume;

	private int kEntriesInPreviousTabs
	{
		get
		{
			if (mSelectedTab == 1)
			{
				return 6;
			}
			return 8;
		}
	}

	public int kEntriesPerTabs
	{
		get
		{
			if (mSelectedTab == 1)
			{
				return 8;
			}
			return 6;
		}
	}

	public bool isDone
	{
		get
		{
			return mDone && !mLayout.isAnimating;
		}
	}

	public float priority
	{
		get
		{
			return mPriority;
		}
		set
		{
			mPriority = value;
			mLayout.basePriority = value;
		}
	}

	public bool isBlocking
	{
		get
		{
			return true;
		}
	}

	private bool buttonsVisible
	{
		get
		{
			return !((SUISprite)mLayout["waitIcon"]).visible && !((SUILabel)mLayout["errorMessage"]).visible;
		}
	}

	public PurchaseCurrencyDialog()
	{
		Init(new Cost(0, 1, 0f));
	}

	public PurchaseCurrencyDialog(Cost trigger)
	{
		Init(trigger);
	}

	private void Init(Cost trigger)
	{
		mLayout = new SUILayout("Layouts/PurchaseCurrency_GWallet");
		mLayout.AnimateIn(0.3f);
		if (trigger.hard > 0)
		{
			mSelectedTab = 1;
		}
		CreatePriceWidgets();
		for (int i = 0; i < 2; i++)
		{
			int index = i;
			mTabs.Add(new Tab(mLayout, i));
			mTabs[i].onTouched = delegate
			{
				OnTabTouched(index);
			};
		}
		mTabs[mSelectedTab].selected = true;
		((SUIButton)mLayout["back"]).onButtonPressed = Close;
		SUIButton freeBtn = mLayout["free"] as SUIButton;
		if (freeBtn != null)
		{
			freeBtn.visible = false;
			freeBtn.onButtonPressed = null;
		}
		instance = this;
		InitOnlineStore();
	}

	public void Destroy()
	{
		if (instance == this)
		{
			instance = null;
		}
		if (mPurchaseList != null)
		{
			mPurchaseList.Destroy();
			mPurchaseList = null;
		}
		if (mLayout != null)
		{
			mLayout.Destroy();
			mLayout = null;
		}
	}

	public void Update()
	{
		if (mLayout == null)
		{
			return;
		}
		mLayout.Update();
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			Close();
			return;
		}
		if (mPurchaseList != null)
		{
			mPurchaseList.Update();
		}
		m_onResume = false;
	}

	private void CreatePriceWidgets()
	{
	}

	private void OnTabTouched(int index)
	{
		if (buttonsVisible && mSelectedTab != index)
		{
			mSelectedTab = index;
			for (int i = 0; i < mTabs.Count; i++)
			{
				mTabs[i].selected = i == mSelectedTab;
			}
			RedrawButtons();
			Singleton<SUISoundManager>.instance.Play("selectSlot", mTabs[0].gameObjectForSound);
		}
	}

	private void Close()
	{
		if (mDone)
		{
			return;
		}
		for (int i = 0; i < kEntriesPerTabs; i++)
		{
			SUIButton btn = mLayout["btn" + i] as SUIButton;
			if (btn != null)
			{
				btn.onButtonPressed = null;
			}
		}
		mDone = true;
		if (mLayout != null)
		{
			mLayout.AnimateOut(0.3f);
		}
		if (WeakGlobalInstance<DialogHandler>.instance != null)
		{
			WeakGlobalInstance<DialogHandler>.instance.FadeOut();
		}
	}

	public void RedrawButtons()
	{
		if (Debug.isDebugBuild)
		{
			Debug.Log("** RedrawButtons() START >>>");
		}
		if (mLayout == null)
		{
			return;
		}
		if (mListController != null && mPurchaseList != null)
		{
			List<OnlineItemsManager.Item> list = new List<OnlineItemsManager.Item>();
			for (int i = mSelectedTab * kEntriesInPreviousTabs; i < mSelectedTab * kEntriesInPreviousTabs + kEntriesPerTabs; i++)
			{
				if (ApplicationUtilities.IsGWalletAvailable())
				{
					if (Singleton<OnlineItemsManager>.instance.items[i].type == OnlineItemsManager.ItemType.Vip_Silver || Singleton<OnlineItemsManager>.instance.items[i].type == OnlineItemsManager.ItemType.Vip_Gold)
					{
						int idx = i - mSelectedTab * kEntriesInPreviousTabs;
						OnlineItemsManager.Item item = ApplicationUtilities.GWalletGetSubscriptionRecommendationAtIndex(idx);
						if (item != null)
						{
							list.Add(item);
						}
						continue;
					}
				}
				else if (Singleton<OnlineItemsManager>.instance.items[i].type == OnlineItemsManager.ItemType.Vip_Silver || Singleton<OnlineItemsManager>.instance.items[i].type == OnlineItemsManager.ItemType.Vip_Gold)
				{
					continue;
				}
				list.Add(Singleton<OnlineItemsManager>.instance.items[i]);
			}
			mListController.HideAllCells();
			mListController.ReloadDataSource(list.ToArray());
			mPurchaseList.ForceRedrawList();
		}
		SetAllButtonsVisible(true);
		((SUILabel)mLayout["title"]).text = Singleton<Localizer>.instance.Get("store_currencytab" + mSelectedTab);
		((SUILabel)mLayout["title"]).visible = true;
		if (Debug.isDebugBuild)
		{
			Debug.Log("** RedrawButtons() FINISH >>>");
		}
	}

	private void SetAllButtonsVisible(bool v)
	{
		if (!v && mListController != null)
		{
			mListController.HideAllCells();
		}
		SUIButton freeBtn = mLayout["free"] as SUIButton;
		if (freeBtn != null)
		{
			freeBtn.visible = false;
			freeBtn.onButtonPressed = null;
		}
		((SUISprite)mLayout["waitIcon"]).visible = false;
		((SUILabel)mLayout["errorMessage"]).visible = !v;
	}

	public void ResetButtons()
	{
	}

	private void InitOnlineStore()
	{
		SUIButton freeBtn = mLayout["free"] as SUIButton;
		if (freeBtn != null)
		{
			freeBtn.visible = false;
			freeBtn.onButtonPressed = null;
		}
		SUISprite waitIcon = mLayout["waitIcon"] as SUISprite;
		if (waitIcon != null)
		{
			waitIcon.visible = false;
		}
		SUILabel errLabel = mLayout["errorMessage"] as SUILabel;
		if (errLabel != null)
		{
			errLabel.text = "Not enough currency.";
			errLabel.visible = true;
		}
	}

	private void OnIAPItemsInfoReceived(IAPItemDescriptor[] items)
	{
		Singleton<OnlineItemsManager>.instance.ApplyOnlineInformations(items);
		RedrawButtons();
	}

	public void OnIAPError(int code, string msg)
	{
		if (ApplicationUtilities.instance != null)
		{
			ApplicationUtilities.instance.bShouldTimeoutIAPs = false;
		}
		RedrawButtons();
		SetAllButtonsVisible(false);
		((SUISprite)mLayout["waitIcon"]).visible = false;
		((SUILabel)mLayout["errorMessage"]).visible = true;
		((SUILabel)mLayout["errorMessage"]).text = "Purchases are unavailable offline.";
	}

	private void OnIAPPurchaseCompleted(string eventInfo)
	{
		SetAllButtonsVisible(true);
	}

	private void OnFreeGemsButtonPressed()
	{
	}

	public void StartPurchase(string id, OnlineItemsManager.ItemType type)
	{
		mSomethingPurchased = true;
		if (MultiLanguages.isCheatIAP)
		{
			Singleton<OnlineItemsManager>.instance.CompletePurchase(id);
			return;
		}
		SetAllButtonsVisible(false);
		((SUISprite)mLayout["waitIcon"]).visible = false;
		((SUILabel)mLayout["errorMessage"]).visible = true;
		((SUILabel)mLayout["errorMessage"]).text = "Purchases are unavailable offline.";
	}

	private void IAPTimeOutTick()
	{
		if (Time.realtimeSinceStartup - startTime > TimeOutInSecs)
		{
			instance.OnIAPError(0, "IAP_TIMEOUT");
			startTime = 0f;
		}
		Debug.Log("*** Time elapsed: " + (Time.realtimeSinceStartup - startTime) + " startTime:" + startTime + " Time.realtimeSinceStartup:" + Time.realtimeSinceStartup);
	}
}
