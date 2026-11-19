using UnityEngine;

public class GameHUD
{
	private SUILayout mLayout;

	private HUDHealthBar mHealthBar;

	private HUDHelpersBar mHelpersBar;

	private HUDAbilities mAbilities;

	private HUDConsumables mConsumables;

	private HUDLegionBar mLegionBar;

	private HUDSmithy mSmithyBar;

	private HUDBloodPulse mBloodPulse;

	private Rect mPauseButtonRect;

	private SUIButton mDevWinButton;

	private SUILabel mDevWinLabel;

	private int mLastDisplayedCoins = -1;

	private int mLastDisplayedGems = -1;

	private bool m_cheatClicked;

	public OnSUIGenericCallback onPauseGameRequest
	{
		get
		{
			return ((SUIButton)mLayout["pauseBtn"]).onButtonPressed;
		}
		set
		{
			((SUIButton)mLayout["pauseBtn"]).onButtonPressed = value;
		}
	}

	public OnSUIStringCallback onAbilityTriggered
	{
		get
		{
			return mAbilities.onAbilityTriggered;
		}
		set
		{
			mAbilities.onAbilityTriggered = value;
		}
	}

	public Character observedCharacter
	{
		get
		{
			return mHealthBar.observedCharacter;
		}
		set
		{
			mHealthBar.observedCharacter = value;
		}
	}

	public SUISprite coinsUISprite
	{
		get
		{
			return (SUISprite)mLayout["coinsIcon"];
		}
	}

	public SUISprite gemsUISprite
	{
		get
		{
			return (SUISprite)mLayout["gemsIcon"];
		}
	}

	public Vector2 smithyBarLocation
	{
		get
		{
			return ((SUISprite)mLayout["leadershipIcon"]).position;
		}
	}

	public HUDConsumables consumables
	{
		get
		{
			return mConsumables;
		}
	}

	public GameHUD()
	{
		mLayout = new SUILayout(Singleton<PlayModesManager>.instance.selectedModeData["hudLayout"]);
		mPauseButtonRect = ((SUIButton)mLayout["pauseBtn"]).area;
		mHealthBar = new HUDHealthBar((SUISprite)mLayout["lifebar"]);
		mHelpersBar = new HUDHelpersBar(mLayout);
		mAbilities = new HUDAbilities(mLayout);
		mConsumables = new HUDConsumables(mLayout);
		mLegionBar = new HUDLegionBar(mLayout);
		mSmithyBar = new HUDSmithy(mLayout);
		mBloodPulse = new HUDBloodPulse();
		mDevWinButton = (SUIButton)mLayout["cheats"];
		mDevWinLabel = (SUILabel)mLayout["cheatsLabel"];
		mDevWinButton.scale = new Vector2(2f, 2f);
		if (CanUseDevWin())
		{
			mDevWinButton.onButtonPressed = WinTheWave;
		}
		else
		{
			mDevWinButton.visible = false;
			if (mDevWinLabel != null)
			{
				mDevWinLabel.visible = false;
			}
		}
		m_cheatClicked = false;
		InitCharm();
		UpdateCoins();
		mLayout.AnimateIn();
	}

	public void Update()
	{
		mLayout.Update();
		UpdateCoins();
		mHelpersBar.Update();
		mAbilities.Update();
		mConsumables.Update();
		mSmithyBar.Update();

		bool isPvP = SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive;
		if (isPvP)
		{
			bool isHeroMode = SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy;
			if (mLayout.Exists("pauseBtn")) ((SUIButton)mLayout["pauseBtn"]).visible = false;
			if (mLayout.Exists("lifebarBg")) ((SUISprite)mLayout["lifebarBg"]).visible = isHeroMode;
			if (mLayout.Exists("lifebar")) ((SUISprite)mLayout["lifebar"]).visible = isHeroMode;
			if (mLayout.Exists("lifebarFrame")) ((SUISprite)mLayout["lifebarFrame"]).visible = isHeroMode;
			if (mLayout.Exists("legionBg")) ((SUISprite)mLayout["legionBg"]).visible = false;
			if (mLayout.Exists("legionPercent")) ((SUILabel)mLayout["legionPercent"]).visible = false;
			if (mLayout.Exists("coinsIcon")) ((SUISprite)mLayout["coinsIcon"]).visible = true;
			if (mLayout.Exists("gemsIcon")) ((SUISprite)mLayout["gemsIcon"]).visible = true;
			if (mLayout.Exists("currencyIcons")) ((SUISprite)mLayout["currencyIcons"]).visible = true;
			if (mLayout.Exists("currencyCounter")) ((SUILabel)mLayout["currencyCounter"]).visible = true;
			if (mLayout.Exists("charm")) ((SUISprite)mLayout["charm"]).visible = false;
			if (mDevWinButton != null) mDevWinButton.visible = false;
			if (mDevWinLabel != null) mDevWinLabel.visible = false;
			if (isHeroMode && !mLayout.isAnimating && mHealthBar != null)
			{
				mHealthBar.Update();
			}
			return;
		}

		mLegionBar.Update();
		mBloodPulse.Update();
		if (CanUseDevWin() && (Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.BackQuote)))
		{
			WinTheWave();
		}
		if (!mLayout.isAnimating)
		{
			mHealthBar.Update();
		}
	}

	public void UpdateConsumableVisualsOnly()
	{
		mConsumables.UpdateVisualsOnly();
	}

	public void UpdateCurrenciesOnly()
	{
		UpdateCoins();
	}

	public bool IsTouchingHUD(Vector2 pos)
	{
		bool isPvP = SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive;
		if (mSmithyBar.IsTouchZone(pos) || mHealthBar.IsTouchZone(pos) || mHelpersBar.IsTouchZone(pos) || mAbilities.IsTouchZone(pos) || mConsumables.IsTouchZone(pos) || mLegionBar.IsTouchZone(pos) || (!isPvP && mPauseButtonRect.Contains(pos)))
		{
			return true;
		}
		return false;
	}

	private void UpdateCoins()
	{
		int coins = Singleton<Profile>.instance.coins;
		int gems = Singleton<Profile>.instance.gems;
		if (coins != mLastDisplayedCoins || gems != mLastDisplayedGems)
		{
			mLastDisplayedCoins = coins;
			mLastDisplayedGems = gems;
			((SUILabel)mLayout["currencyCounter"]).text = mLastDisplayedCoins + "\n" + mLastDisplayedGems;
		}
	}

	private void InitCharm()
	{
		if (Singleton<Profile>.instance.selectedCharm != string.Empty)
		{
			string selectedCharm = Singleton<Profile>.instance.selectedCharm;
			SUISprite sUISprite = (SUISprite)mLayout["charm"];
			sUISprite.texture = Singleton<CharmsDatabase>.instance.GetAttribute(selectedCharm, "icon");
			sUISprite.visible = true;
		}
	}

	public void WinTheWave()
	{
		m_cheatClicked = true;
	}

	public bool CanUseDevWin()
	{
		return false;
	}

	public bool cheatClicked()
	{
		return m_cheatClicked;
	}
}
