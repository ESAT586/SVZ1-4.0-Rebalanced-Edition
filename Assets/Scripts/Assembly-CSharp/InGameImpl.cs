using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

public class InGameImpl : WeakGlobalSceneBehavior<InGameImpl>
{
	private const int kMaxHelpers = 5;

	private const float kDialogPriority = 500f;

	private const float kWinPriority = 1000f;

	private const float kSlowMoEffectStartScale = 0.1f;

	private const float kSlowMoEffectIncrement = 0.8f;

	private const float kCameraZoomInOffset = 200f;

	private const float kCameraZoomOutSpeed = 80f;

	private const float kCameraMoveUpSpeed = 25f;

	private const int kClassicEndGameWave = 100;

	private const int kZombiesEndGameWave = 100;

	public Transform heroSpawnPointLeft;

	public Transform heroSpawnPointRight;

	public GameObject enemiesTargetLeft;

	public GameObject vortexLeft;

	public GameObject gateSparklesLeft;

	public GameObject enemiesTargetRight;

	public Camera gameCamera;

	public Transform heroWalkLeftEdge;

	public Transform heroWalkRightEdge;

	public BoxCollider enemiesSpawnAreaLeft;

	public BoxCollider enemiesSpawnAreaRight;

	public BoxCollider helpersSpawnAreaLeft;

	public BoxCollider helpersSpawnAreaRight;

	public GameObject bellRingerObject;

	public Transform[] villageArcher = new Transform[3];

	private PrefabPreloader mPrefabPreloader = new PrefabPreloader();

	private BuffIconManager mBuffIconManager = new BuffIconManager();

	private GameHUD mHUD;

	private Hero mHero;

	private Base mBase;

	private Base mEnemyBase;

	private Bell mBell;

	private CharactersManager mCharactersManager;

	private CollectableManager mCollectableManager;

	private ProjectileManager mProjectileManager;

	private WaveManager mWaveManager;

	private ProceduralShaderManager mShaderManager;

	private Smithy mSmithy;

	private TutorialManager mTutorial;

	private LegionOnTheLooseManager mLegionManager;

	private RailManager mRailManager;

	private DialogHandler mDialogHandler;

	private NarrativeManager mNarrativeManager;

	private TimedEventManager mTimedEventManager = new TimedEventManager();

	private VillageArchers mVillageArchers;

	private GraveHandsEffect mGraveHandsEffect;

	private FPSCounter mFPSCounter;

	private bool mGameOver;

	private bool mPauseGameForBlockingDialog = true;

	private bool mHaveShowReviveDialog;
	private int mRevivesCount;

	private float mAllAlliesInvincibleTimer;

	private WaveBanner mWaveBanner;

	private GenericGameBanner mGenericGameBanner;

	private MiniHealthBarRegistrar mMiniHealthBarRegistrar = new MiniHealthBarRegistrar();

	private float mCameraYOffset = 100f;

	private float mBaseTimeScale = 1f;

	private bool mStartedSlowMoFinisher;

	private PlayModesManager.GameDirection mGameDirection;

	public Transform heroSpawnPoint
	{
		get
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				if (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate && heroSpawnPointRight != null)
				{
					return heroSpawnPointRight;
				}
				return heroSpawnPointLeft;
			}
			if (mGameDirection == PlayModesManager.GameDirection.RightToLeft)
			{
				return heroSpawnPointRight;
			}
			return heroSpawnPointLeft;
		}
	}

	public GameObject enemiesTarget
	{
		get
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				if (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate && enemiesTargetRight != null)
				{
					return enemiesTargetRight;
				}
				return enemiesTargetLeft;
			}
			if (mGameDirection == PlayModesManager.GameDirection.RightToLeft)
			{
				return enemiesTargetRight;
			}
			return enemiesTargetLeft;
		}
	}

	public BoxCollider enemiesSpawnArea
	{
		get
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				if (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate && enemiesSpawnAreaLeft != null)
				{
					return enemiesSpawnAreaLeft;
				}
				return enemiesSpawnAreaRight;
			}
			if (mGameDirection == PlayModesManager.GameDirection.RightToLeft)
			{
				return enemiesSpawnAreaLeft;
			}
			return enemiesSpawnAreaRight;
		}
	}

	public BoxCollider helpersSpawnArea
	{
		get
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				if (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate && helpersSpawnAreaRight != null)
				{
					return helpersSpawnAreaRight;
				}
				return helpersSpawnAreaLeft;
			}
			if (mGameDirection == PlayModesManager.GameDirection.RightToLeft)
			{
				return helpersSpawnAreaRight;
			}
			return helpersSpawnAreaLeft;
		}
	}

	public bool gameOver
	{
		get
		{
			return mGameOver;
		}
	}

	public bool playerWon
	{
		get
		{
			return mGameOver && hero.health > 0f;
		}
	}

	public bool enemiesWon
	{
		get
		{
			return mGameOver && hero.health <= 0f;
		}
	}

	public bool useSlowMoFinisher
	{
		get
		{
			if (mStartedSlowMoFinisher)
			{
				return true;
			}
			if (hero != null && (hero.controller.currentAnimation == "attack" || hero.controller.currentAnimation == "katanaslash"))
			{
				return true;
			}
			return false;
		}
	}

	public Hero hero
	{
		get
		{
			return mHero;
		}
	}

	public Base gate
	{
		get
		{
			return mBase;
		}
	}

	public Base enemyGate
	{
		get
		{
			return mEnemyBase;
		}
	}

	public string activeCharm { get; private set; }

	public GameHUD HUD
	{
		get
		{
			return mHUD;
		}
	}

	public float allAlliesInvincibleTimer
	{
		get
		{
			return mAllAlliesInvincibleTimer;
		}
		set
		{
			mAllAlliesInvincibleTimer = value;
		}
	}

	public float baseTimeScale
	{
		get
		{
			return mBaseTimeScale;
		}
		set
		{
			mBaseTimeScale = Mathf.Max(0.1f, value);
			if (Time.timeScale > 0f)
			{
				Time.timeScale = mBaseTimeScale;
			}
		}
	}

	private bool gamePaused
	{
		get
		{
			return Time.timeScale == 0f;
		}
		set
		{
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				return;
			}
			if (value != (Time.timeScale == 0f))
			{
				SoundThemeManager.SetAllLoopingSoundsPaused(value);
				if (value)
				{
					Time.timeScale = 0f;
				}
				else
				{
					Time.timeScale = mBaseTimeScale;
				}
			}
		}
	}

	private void Start()
	{
		if (WeakGlobalSceneBehavior<InGameImpl>.instance != null && WeakGlobalSceneBehavior<InGameImpl>.instance != this)
		{
			Debug.LogWarning("[InGameImpl] Duplicate InGameImpl detected on '" + base.gameObject.name + "' - destroying duplicate.");
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		mBaseTimeScale = 1f;
		Time.timeScale = 1f;
		SetUniqueInstance(this);
		mGameDirection = Singleton<PlayModesManager>.instance.gameDirection;
		ApplicationUtilities.instance.onApplicationPause += OnApplicationPause;
		HideUnusedGate();

		PerformanceSettings.ApplySavedSettings();

		float targetAspect = 1.7777778f; // 16:9
		float currentAspect = (float)Screen.width / Screen.height;

		if (gameCamera != null)
		{
			if (gameCamera.orthographic)
			{
				float baseOrtho = gameCamera.orthographicSize;
				if (currentAspect < targetAspect) // Narrower than 16:9 (e.g. 4:3)
				{
					gameCamera.orthographicSize = baseOrtho * (targetAspect / currentAspect);
				}
			}
		}

		if (gameCamera != null)
		{
			gameCamera.hdr = true;
		}

		Singleton<PlayStatistics>.instance.ResetStats(Singleton<Profile>.instance.waveToBeat);
		activeCharm = Singleton<Profile>.instance.selectedCharm;
		WeakGlobalInstance<SUIScreen>.instance.fader.FadeFromBlack();
		mRailManager = new RailManager();
		mProjectileManager = new ProjectileManager();
		float zTarget = (enemiesTarget != null) ? enemiesTarget.transform.position.z : 0f;
		mWaveManager = new WaveManager(Singleton<Profile>.instance.waveToBeat, enemiesSpawnArea, zTarget);
		if (mWaveManager != null) mWaveManager.PreloadEnemies();
		mSmithy = new Smithy();
		mHUD = new GameHUD();
		mHUD.onPauseGameRequest = onPauseRequest;
		mHUD.onAbilityTriggered = Singleton<AbilitiesDatabase>.instance.Execute;
		mHero = new Hero(heroSpawnPoint);
		if (mHero != null && mHero.controller != null)
		{
			if (heroWalkLeftEdge != null) mHero.controller.constraintLeft = heroWalkLeftEdge.position.z;
			if (heroWalkRightEdge != null) mHero.controller.constraintRight = heroWalkRightEdge.position.z;
			if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
			{
				if (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate)
				{
					mHero.controller.facing = FacingType.Left;
				}
				if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
				{
					if (heroWalkLeftEdge != null)
					{
						Vector3 parkedPos = mHero.controller.position;
						parkedPos.z = heroWalkLeftEdge.position.z - 400f;
						parkedPos.y = -500f;
						mHero.controller.position = parkedPos;
					}
					mHero.health = 999999f;
					mHero.maxHealth = 999999f;
					if (mHero.controlledObject != null)
					{
						Collider[] cols = mHero.controlledObject.GetComponentsInChildren<Collider>();
						for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
						Renderer[] rends = mHero.controlledObject.GetComponentsInChildren<Renderer>();
						for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;
					}
				}
				else if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy)
				{
					mHero.maxHealth = 4000f;
					mHero.health = 4000f;
					CharacterStats hStats = mHero.stats;
					hStats.maxHealth = 4000f;
					hStats.health = 4000f;
					hStats.meleeAttackDamage = 50f;
					hStats.bowAttackDamage = 40f;
					mHero.stats = hStats;
					mHero.autoHealthRecovery = 85f;
				}
			}
		}
		mBase = new Base(enemiesTarget);
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			GameObject p2GateObj = (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate)
				? enemiesTargetLeft
				: enemiesTargetRight;
			if (p2GateObj != null)
			{
				mEnemyBase = new Base(p2GateObj);
				mEnemyBase.isEnemy = true;
				mEnemyBase.RefreshHealthBarColor();
			}
		}
		mBell = new Bell(bellRingerObject);
		mShaderManager = new ProceduralShaderManager();

		float leftEdge = (heroWalkLeftEdge != null) ? heroWalkLeftEdge.position.z + 16f : 0f;
		float rightEdge = (heroWalkRightEdge != null) ? heroWalkRightEdge.position.z - 16f : 500f;
		float centerX = (heroSpawnPoint != null) ? heroSpawnPoint.position.x : 0f;
		mCollectableManager = new CollectableManager(leftEdge, rightEdge, centerX);

		mVillageArchers = new VillageArchers();
		mCharactersManager = new CharactersManager();
		bool isArmyOnlyPvP = SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive &&
		                     SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly;
		if (!isArmyOnlyPvP)
		{
			mCharactersManager.AddCharacter(mHero);
		}
		mCharactersManager.AddCharacter(mBase);
		if (mEnemyBase != null)
		{
			mCharactersManager.AddCharacter(mEnemyBase);
		}
		mSmithy.characterManagerRef = mCharactersManager;
		mSmithy.helperSpawnArea = helpersSpawnArea;
		mSmithy.helpersZTarget = enemiesSpawnArea.transform.position.z;
		mHUD.observedCharacter = mHero;
		mWaveBanner = new WaveBanner();
		bool isMp = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive);
		mLegionManager = isMp ? null : new LegionOnTheLooseManager();
		bool disableTut = isMp || (PlayerPrefs.GetInt("SVZ_DisableTutorials", 0) == 1);
		mTutorial = disableTut ? null : new TutorialManager(mWaveManager.tutorial, "InGameReactives");
		if (!isMp && !Singleton<Profile>.instance.inBonusWave)
		{
			mNarrativeManager = new NarrativeManager();
		}
		if (!isMp && Singleton<PlayModesManager>.instance.selectedMode == "zombies")
		{
			mGraveHandsEffect = new GraveHandsEffect();
		}
		ApplyInitialCharmEffects();
		SpendCharm();
		CreateFPSCounter();
		if (gameCamera.nearClipPlane < 10f)
		{
			gameCamera.transform.position = new Vector3(gameCamera.transform.position.x - (10f - gameCamera.nearClipPlane), gameCamera.transform.position.y, gameCamera.transform.position.z);
			gameCamera.nearClipPlane = 10f;
		}
		if (gameCamera.farClipPlane > 10000f)
		{
			gameCamera.farClipPlane /= 10f;
		}
		UnityEngine.Debug.Log(string.Concat("Camera at: ", gameCamera.transform.position, ", near: ", gameCamera.nearClipPlane, ", far: ", gameCamera.farClipPlane));
		if (gameCamera != null && heroSpawnPointLeft != null)
		{
			mCameraYOffset = gameCamera.transform.position.y - heroSpawnPointLeft.transform.position.y;
		}
		RenderSettings.fog = false;
		if (Singleton<Profile>.instance.waveToBeat == 1 && Singleton<Profile>.instance.GetWaveLevel(1) == 1)
		{
			Singleton<PlayHavenTowerControl>.instance.InvokeContent("tutorial_start");
		}
	}

	private void OnDestroy()
	{
		Time.timeScale = 1f;
		if (ApplicationUtilities.HasInstance)
		{
			ApplicationUtilities.instance.onApplicationPause -= OnApplicationPause;
		}
	}

	private void Update()
	{
		if (SceneBehaviourUpdate() || WeakGlobalInstance<SUIScreen>.instance.fader.onFadingDone != null)
		{
			return;
		}
		if (SVZCharacterLabController.IsLabActive)
		{
			if (mDialogHandler != null)
			{
				mDialogHandler.Destroy();
				mDialogHandler = null;
			}
			mNarrativeManager = null;
			mTutorial = null;
		}
		if (mNarrativeManager != null)
		{
			mNarrativeManager.Update();
			if (mNarrativeManager.isBlocking)
			{
				gamePaused = true;
				return;
			}
		}
		if (mTutorial != null)
		{
			if (WeakGlobalInstance<WaveManager>.instance.waveLevel == 1 && mTutorial.nextCommandIsHighPriority)
			{
				mTutorial.RunNextCommand();
			}
			UpdateTutorial();
			if (mTutorial != null && mTutorial.isBlocking)
			{
				gamePaused = true;
				return;
			}
		}
		if (mDialogHandler != null)
		{
			mPauseGameForBlockingDialog = true;
			mDialogHandler.Update();
			if (mDialogHandler.isDone)
			{
				mDialogHandler.Destroy();
				mDialogHandler = null;
			}
			else if (mDialogHandler.isBlocking)
			{
				if (mPauseGameForBlockingDialog)
				{
					gamePaused = true;
				}
				mHUD.UpdateCurrenciesOnly();
				return;
			}
		}
		gamePaused = false;
		if (DevCheatOverlay.sGodMode && mHero != null)
		{
			mHero.health = mHero.maxHealth;
		}
		if (mWaveBanner != null)
		{
			mWaveBanner.Update();
			if (mWaveBanner.isDone)
			{
				mWaveBanner.Destroy();
				mWaveBanner = null;
			}
		}
		if (mGenericGameBanner != null)
		{
			mGenericGameBanner.Update();
			if (mGenericGameBanner.isDone)
			{
				mGenericGameBanner.Destroy();
				mGenericGameBanner = null;
			}
		}
		if (mGraveHandsEffect != null)
		{
			mGraveHandsEffect.Update();
		}
		if (mTimedEventManager != null) mTimedEventManager.Update();
		if (mSmithy != null) mSmithy.Update();
		if (mWaveManager != null) mWaveManager.Update();
		if (mCharactersManager != null) mCharactersManager.Update();
		if (mCollectableManager != null) mCollectableManager.Update();
		if (mProjectileManager != null) mProjectileManager.Update();
		if (mShaderManager != null) mShaderManager.update();
		if (mBell != null) mBell.Update();
		if (mVillageArchers != null) mVillageArchers.Update();
		if (mLegionManager != null) mLegionManager.Update();
		CheckWinLoseConditions();
		if (mMiniHealthBarRegistrar != null) mMiniHealthBarRegistrar.UpdateCulling();
		if (mAllAlliesInvincibleTimer > 0f)
		{
			mAllAlliesInvincibleTimer = Mathf.Max(0f, mAllAlliesInvincibleTimer - Time.deltaTime);
		}
		if (mHUD != null) mHUD.Update();
		UpdateFPSCounter();
		if (Input.GetKeyUp(KeyCode.Escape))
		{
			onPauseRequest();
		}
	}

	private void LateUpdate()
	{
		UpdateCamera();
	}

	public void RunSpecialWaveCommand(string cmd)
	{
		switch (cmd)
		{
		case "@bossalert":
			if (mGenericGameBanner != null)
			{
				mGenericGameBanner.Destroy();
			}
			mGenericGameBanner = new GenericGameBanner(Singleton<PlayModesManager>.instance.selectedModeData["bannerfile_boss"], "bossalert");
			break;
		case "@legionalert":
		{
			if (mGenericGameBanner != null)
			{
				mGenericGameBanner.Destroy();
			}
			string bannerSprite = Singleton<PlayModesManager>.instance.selectedModeData["bannerfile_legion"];
			mGenericGameBanner = new GenericGameBanner(bannerSprite, "legionalert");
			break;
		}
		default:
			UnityEngine.Debug.Log("WARNING: Unknown wave command: " + cmd);
			break;
		}
	}

	public void ShowInGameStore(string itemID)
	{
		OnSUIGenericCallback extraUpdateCode = delegate
		{
			mHUD.consumables.DirtyCellCounters();
			mHUD.UpdateConsumableVisualsOnly();
			mHUD.UpdateCurrenciesOnly();
		};
		if (mDialogHandler != null)
		{
			mDialogHandler.PushCreator(delegate
			{
				mDialogHandler.extraUpdateCode = extraUpdateCode;
				return InGameStoreDialog.Create(Singleton<PlayModesManager>.instance.revivePotionID, true);
			});
		}
		else
		{
			SetDialogHandler(new DialogHandler(500f, InGameStoreDialog.Create(itemID, true)));
			mDialogHandler.extraUpdateCode = extraUpdateCode;
		}
	}

	public void RunAfterDelay(Action fn, float delay)
	{
		if (fn != null)
		{
			StartCoroutine(RunAfterDelayInternal(fn, delay));
		}
	}

	public void RunNextUpdate(Action fn)
	{
		if (fn != null)
		{
			StartCoroutine(RunAfterYieldInternal(fn));
		}
	}

	private void onPauseRequest()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			return;
		}
		if (!mGameOver)
		{
			PauseMenu pauseMenu = new PauseMenu();
			pauseMenu.onQuitGameRequest = OnQuitGameRequest;
			pauseMenu.onRestartGameRequest = OnRestartGameRequest;
			SetDialogHandler(new DialogHandler(500f, pauseMenu));
		}
	}

	private void OnQuitGameRequest()
	{
		WeakGlobalInstance<SUIScreen>.instance.fader.onFadingDone = delegate
		{
			if (Singleton<Profile>.instance.inBonusWave || (Singleton<Profile>.instance.waveToBeat == 1 && Singleton<Profile>.instance.GetWaveLevel(2) == 0))
			{
				Singleton<MenusFlow>.instance.LoadScene("MainMenu");
			}
			else
			{
				Singleton<MenusFlow>.instance.LoadScene("Store");
			}
		};
		WeakGlobalInstance<SUIScreen>.instance.fader.FadeToBlack();
		ApplicationUtilities.instance.HideAds();
	}

	private void OnRestartGameRequest()
	{
		WeakGlobalInstance<SUIScreen>.instance.fader.onFadingDone = delegate
		{
			WaveManager.LoadNextWaveLevel();
		};
		WeakGlobalInstance<SUIScreen>.instance.fader.FadeToBlack();
		ApplicationUtilities.instance.HideAds();
	}

	private void onFinishGameRequest()
	{
		WeakGlobalInstance<SUIScreen>.instance.fader.onFadingDone = delegate
		{
			Singleton<MenusFlow>.instance.LoadScene("Debriefing");
		};
		WeakGlobalInstance<SUIScreen>.instance.fader.FadeToBlack();
	}

	private void SetDialogHandler(DialogHandler d)
	{
		if (mDialogHandler != null)
		{
			mDialogHandler.Destroy();
		}
		mDialogHandler = d;
	}

	private void UpdateTutorial()
	{
		if (mTutorial != null)
		{
			if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.T) || (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive))
			{
				mTutorial.Destroy();
				mTutorial = null;
				return;
			}
			mTutorial.Update();
			if (mTutorial.isDone)
			{
				mTutorial.Destroy();
				mTutorial = null;
			}
		}
	}

	private float mRTSCameraZ = 0f;
	private bool mRTSCameraInitialized = false;
	private bool mAutoFollowFrontline = true;

	public bool autoFollowFrontline
	{
		get { return mAutoFollowFrontline; }
		set { mAutoFollowFrontline = value; }
	}

	public void SnapRTSCamera(int zone)
	{
		float leftBound = (heroWalkLeftEdge != null) ? heroWalkLeftEdge.position.z : -500f;
		float rightBound = (heroWalkRightEdge != null) ? heroWalkRightEdge.position.z : 500f;
		float minZ = Mathf.Min(leftBound, rightBound);
		float maxZ = Mathf.Max(leftBound, rightBound);
		mRTSCameraInitialized = true;
		mAutoFollowFrontline = false;
		if (zone <= 0) mRTSCameraZ = minZ + 250f;
		else if (zone == 1) mRTSCameraZ = (minZ + maxZ) * 0.5f;
		else mRTSCameraZ = maxZ - 250f;
	}

	public void SetRTSCameraRatio(float ratio01)
	{
		float leftBound = (heroWalkLeftEdge != null) ? heroWalkLeftEdge.position.z : -500f;
		float rightBound = (heroWalkRightEdge != null) ? heroWalkRightEdge.position.z : 500f;
		float minZ = Mathf.Min(leftBound, rightBound);
		float maxZ = Mathf.Max(leftBound, rightBound);
		mRTSCameraInitialized = true;
		mAutoFollowFrontline = false;
		mRTSCameraZ = Mathf.Lerp(minZ + 180f, maxZ - 180f, Mathf.Clamp01(ratio01));
	}

	private void UpdateCamera()
	{
		if (SVZCharacterLabController.IsLabCameraActive)
		{
			return;
		}
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
		{
			if (gameCamera != null)
			{
				float leftBound = (heroWalkLeftEdge != null) ? heroWalkLeftEdge.position.z : -500f;
				float rightBound = (heroWalkRightEdge != null) ? heroWalkRightEdge.position.z : 500f;
				float minZ = Mathf.Min(leftBound, rightBound);
				float maxZ = Mathf.Max(leftBound, rightBound);

				if (!mRTSCameraInitialized)
				{
					mRTSCameraInitialized = true;
					mRTSCameraZ = (SVZMultiplayerManager.Instance.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate)
						? (maxZ - 350f)
						: (minZ + 350f);
				}

				if (Input.GetKeyDown(KeyCode.C))
				{
					mAutoFollowFrontline = !mAutoFollowFrontline;
				}

				float camSpeed = 950f * Time.deltaTime;
				if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
				{
					mAutoFollowFrontline = false;
					mRTSCameraZ -= camSpeed;
				}
				if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
				{
					mAutoFollowFrontline = false;
					mRTSCameraZ += camSpeed;
				}

				if (mAutoFollowFrontline && mCharactersManager != null && mCharactersManager.allAlive != null)
				{
					bool p1MarchRight = (SVZMultiplayerManager.Instance.P1Side != SVZMultiplayerManager.BattlefieldSide.RightGate);
					float p1VanguardZ = p1MarchRight ? minZ : maxZ;
					float p2VanguardZ = p1MarchRight ? maxZ : minZ;
					bool hasP1 = false;
					bool hasP2 = false;
					System.Collections.Generic.List<Character> chars = mCharactersManager.allAlive;
					for (int i = 0; i < chars.Count; i++)
					{
						Character c = chars[i];
						if (c == null || c.health <= 0f || c.isBase || c.isPlayer) continue;
						if (!c.isEnemy)
						{
							hasP1 = true;
							p1VanguardZ = p1MarchRight ? Mathf.Max(p1VanguardZ, c.position.z) : Mathf.Min(p1VanguardZ, c.position.z);
						}
						else
						{
							hasP2 = true;
							p2VanguardZ = p1MarchRight ? Mathf.Min(p2VanguardZ, c.position.z) : Mathf.Max(p2VanguardZ, c.position.z);
						}
					}
					float targetZ = mRTSCameraZ;
					if (hasP1 && hasP2) targetZ = (p1VanguardZ + p2VanguardZ) * 0.5f;
					else if (hasP1) targetZ = p1VanguardZ;
					else if (hasP2) targetZ = p2VanguardZ;
					mRTSCameraZ = Mathf.Lerp(mRTSCameraZ, targetZ, Time.deltaTime * 3.8f);
				}

				mRTSCameraZ = Mathf.Clamp(mRTSCameraZ, minZ, maxZ);
				float groundY = (WeakGlobalInstance<RailManager>.instance != null)
					? WeakGlobalInstance<RailManager>.instance.GetY(mRTSCameraZ)
					: ((heroSpawnPointLeft != null) ? heroSpawnPointLeft.position.y : 0f);
				float camY = groundY + mCameraYOffset;
				gameCamera.transform.position = new Vector3(gameCamera.transform.position.x, camY, mRTSCameraZ);
			}
			return;
		}

		if (gameCamera != null)
		{
			bool isOnlineClient = (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive && !SVZMultiplayerManager.Instance.IsSamePC && SVZNetworkManager.Instance != null && !SVZNetworkManager.Instance.IsHost);
			if (isOnlineClient && SVZMultiplayerManager.Instance.CurrentOpponentHero != null)
			{
				float heroZ = SVZMultiplayerManager.Instance.CurrentOpponentHero.position.z;
				float heroY = SVZMultiplayerManager.Instance.CurrentOpponentHero.position.y;
				gameCamera.transform.position = new Vector3(gameCamera.transform.position.x, heroY + mCameraYOffset, heroZ);
			}
			else if (mHero != null)
			{
				gameCamera.transform.position = new Vector3(gameCamera.transform.position.x, mHero.position.y + mCameraYOffset, mHero.position.z);
			}
		}
	}

	private void CheckWinLoseConditions()
	{
		if (SVZCharacterLabController.IsLabActive)
		{
			return;
		}
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			return;
		}
		if (mHero == null || mBase == null || mWaveManager == null || mCharactersManager == null)
		{
			return;
		}
		bool flag = false;
		if (mHero.isOver)
		{
			if (WeakGlobalInstance<CollectableManager>.instance != null) WeakGlobalInstance<CollectableManager>.instance.OpenPresents(true);
			int reviveLimit = 1;
			if (Singleton<Config>.instance.root.to("Game")["revivesPerWave"] != null)
			{
				int.TryParse(Singleton<Config>.instance.root.to("Game")["revivesPerWave"], out reviveLimit);
			}

			if (!mHaveShowReviveDialog && mRevivesCount < reviveLimit)
			{
				mHaveShowReviveDialog = true;
				ShowReviveDialog();
				return;
			}
			flag = true;
		}
		else
		{
			if (mBase.health == 0f)
			{
				mHero.health = 0f;
			}
			mHaveShowReviveDialog = false;
		}
		if (flag)
		{
			mGameOver = true;
			if (Singleton<Profile>.instance.inBonusWave)
			{
				Singleton<Profile>.instance.bonusWaveToBeat++;
				Singleton<Profile>.instance.wavesSinceLastBonusWave = 0;
			}
			else
			{
				Singleton<Analytics>.instance.LogEvent("Deaths", Singleton<Profile>.instance.waveToBeat.ToString());
			}
			CreateLoseDialog();
			Singleton<Profile>.instance.Save();
		}
		else if ((mWaveManager.isDone && mCharactersManager.enemiesCount == 0) || (mHUD != null && mHUD.cheatClicked()))
		{
			CreateWinDialog();
			mGameOver = true;
			StartCoroutine(PostWinDelay());
		}
	}

	private void CreateWinDialog()
	{
		Camera.main.GetComponent<AudioSource>().volume = 0f;
		if (useSlowMoFinisher)
		{
			mBaseTimeScale = 0.1f;
			Time.timeScale = mBaseTimeScale;
			gameCamera.transform.position = new Vector3(gameCamera.transform.position.x - 200f, gameCamera.transform.position.y, gameCamera.transform.position.z);
			mCameraYOffset /= 2f;
			mStartedSlowMoFinisher = true;
		}
		string attribName = "postWinTime";
		string endGameLayout;
		WinLoseDisplay initialDialog;
		if (TryGetEndGameLayout(out endGameLayout))
		{
			attribName = "endGameTime";
			initialDialog = new WinLoseDisplay(endGameLayout);
		}
		else
		{
			initialDialog = new WinLoseDisplay(Singleton<PlayModesManager>.instance.selectedModeData["layout_VictoryMessage"]);
		}
		SetDialogHandler(new DialogHandler(1000f, initialDialog));
		mDialogHandler.fadeLevel = 1f;
		mDialogHandler.fadeSpeed = float.Parse(Singleton<Config>.instance.root.to("Game")[attribName]);
		mDialogHandler.extraUpdateCode = delegate
		{
			if (useSlowMoFinisher)
			{
				mBaseTimeScale = Mathf.Min(1f, mBaseTimeScale + 0.8f * Time.deltaTime);
				Time.timeScale = mBaseTimeScale;
				mCameraYOffset += 25f * Time.deltaTime;
				gameCamera.transform.position = new Vector3(gameCamera.transform.position.x + 80f * Time.deltaTime, gameCamera.transform.position.y, gameCamera.transform.position.z);
			}
			mPauseGameForBlockingDialog = false;
			mCharactersManager.Update();
			mCollectableManager.Update();
			mProjectileManager.Update();
			mShaderManager.update();
			mBell.Update();
			mVillageArchers.Update();
			mHUD.Update();
		};
	}

	private bool TryGetEndGameLayout(out string layoutPath)
	{
		layoutPath = string.Empty;
		if (Singleton<Profile>.instance.inBonusWave)
		{
			return false;
		}
		string selectedMode = Singleton<PlayModesManager>.instance.selectedMode;
		if (selectedMode == "classic" && Singleton<Profile>.instance.waveToBeat == kClassicEndGameWave && Singleton<Profile>.instance.GetWaveLevel(kClassicEndGameWave) == 1)
		{
			layoutPath = "Layouts/EndGameMessage";
			return true;
		}
		if (selectedMode == "zombies" && Singleton<Profile>.instance.waveToBeat == kZombiesEndGameWave)
		{
			layoutPath = "Layouts/EndGameMessage_ZM";
			return true;
		}
		return false;
	}

	private void CreateLoseDialog()
	{
		Camera.main.GetComponent<AudioSource>().volume = 0f;
		WinLoseDisplay winLoseDisplay = new WinLoseDisplay(Singleton<PlayModesManager>.instance.selectedModeData["layout_LoseMessage"]);
		winLoseDisplay.onPlayerPressed = onFinishGameRequest;
		SetDialogHandler(new DialogHandler(500f, winLoseDisplay));
		mDialogHandler.fadeLevel = 0.3f;
		mDialogHandler.extraUpdateCode = delegate
		{
			mPauseGameForBlockingDialog = false;
			mShaderManager.update();
			mHUD.Update();
		};
	}

	private IEnumerator PostWinDelay()
	{
		float waitTime = float.Parse(Singleton<Config>.instance.root.to("Game")["postWinTime"]);
		yield return new WaitForSeconds(waitTime);
		RegisterWin();
		onFinishGameRequest();
	}

	private void RegisterWin()
	{
		mGameOver = true;
		WeakGlobalInstance<WaveManager>.instance.AddSpecialRewardsToCollectables();
		WeakGlobalInstance<CollectableManager>.instance.BankAllResources();
		if (Singleton<Profile>.instance.inBonusWave)
		{
			Singleton<Profile>.instance.bonusWaveToBeat++;
			Singleton<Profile>.instance.wavesSinceLastBonusWave = 0;
		}
		else
		{
			int waveToBeat = Singleton<Profile>.instance.waveToBeat;
			int waveLevel = Singleton<Profile>.instance.GetWaveLevel(waveToBeat);
			Singleton<Analytics>.instance.LogEvent("WaveWins", waveToBeat.ToString());
			Singleton<Profile>.instance.SetWaveLevel(waveToBeat, waveLevel + 1);
			int nextWave = Mathf.Min(waveToBeat + 1, Singleton<Profile>.instance.maxBaseWave);
			Singleton<Profile>.instance.waveToBeat = nextWave;
			if (nextWave != waveToBeat && Singleton<Profile>.instance.GetWaveLevel(nextWave) == 0)
			{
				Singleton<Profile>.instance.SetWaveLevel(nextWave, 1);
			}
			Singleton<Profile>.instance.RegisterNormalWaveWinForBonusWave(waveToBeat, waveLevel);
		}
		Singleton<PlayStatistics>.instance.stats.lastWaveWon = true;
		Singleton<Profile>.instance.Save();
	}

	private IEnumerator RunAfterDelayInternal(Action fn, float delay)
	{
		yield return new WaitForSeconds(delay);
		if (fn != null)
		{
			fn();
		}
	}

	private IEnumerator RunAfterYieldInternal(Action fn)
	{
		yield return null;
		if (fn != null)
		{
			fn();
		}
	}

	private void SpendCharm()
	{
		if (!Singleton<Profile>.instance.inBonusWave)
		{
			string selectedCharm = Singleton<Profile>.instance.selectedCharm;
			if (selectedCharm != string.Empty)
			{
				Singleton<Profile>.instance.SetNumCharms(selectedCharm, Mathf.Max(0, Singleton<Profile>.instance.GetNumCharms(selectedCharm) - 1));
				Singleton<Profile>.instance.selectedCharm = string.Empty;
			}
		}
	}

	private void ApplyInitialCharmEffects()
	{
		if (string.IsNullOrEmpty(activeCharm)) return;
		string attribute = Singleton<CharmsDatabase>.instance.GetAttribute(activeCharm, "helper");
		if (!string.IsNullOrEmpty(attribute))
		{
			Smithy s = WeakGlobalInstance<Smithy>.instance ?? mSmithy;
			if (s != null)
			{
				Helper helper = s.ForceSpawn(attribute);
				if (helper == null)
				{
					Vector3 spawnPos = (helpersSpawnArea != null) ? helpersSpawnArea.transform.position : ((heroSpawnPoint != null) ? heroSpawnPoint.position : Vector3.zero);
					helper = s.ForceSpawnAt(attribute, spawnPos, false);
				}
				if (helper != null && mWaveManager != null && mWaveManager.multipliers != null)
				{
					helper.maxHealth *= mWaveManager.multipliers.enemiesHealth;
					helper.health = helper.maxHealth;
					helper.meleeDamage *= mWaveManager.multipliers.enemiesDamages;
				}
			}
		}
	}

	private void ShowReviveDialog()
	{
		mRevivesCount++;
		ReviveDialog reviveDialog = new ReviveDialog();
		reviveDialog.priority = 500.5f;
		SetDialogHandler(new DialogHandler(500f, reviveDialog));
	}

	private void OnApplicationPause(bool paused)
	{
		if (SVZCharacterLabController.IsLabActive)
		{
			return;
		}
		if (!gamePaused)
		{
			ApplicationUtilities.instance.HideAds();
			onPauseRequest();
		}
	}

	private void HideUnusedGate()
	{
		if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
		{
			// In PvP mode, keep BOTH gates active!
			// Ensure Left Gate is the Samurai Gate (PFGate) and Right Gate is the Zombie Gate (PFGateHell).
			if (enemiesTargetLeft != null && enemiesTargetLeft.name.Contains("Hell"))
			{
				GameObject samuraiGatePrefab = Resources.Load("Props/PFGate") as GameObject;
				if (samuraiGatePrefab != null)
				{
					Vector3 pos = enemiesTargetLeft.transform.position;
					Vector3 scale = enemiesTargetLeft.transform.localScale;
					UnityEngine.Object.DestroyImmediate(enemiesTargetLeft);
					enemiesTargetLeft = UnityEngine.Object.Instantiate(samuraiGatePrefab) as GameObject;
					enemiesTargetLeft.transform.position = pos;
					enemiesTargetLeft.transform.localScale = scale;
				}
			}
			return;
		}
		switch (mGameDirection)
		{
		case PlayModesManager.GameDirection.LeftToRight:
			if (enemiesTargetRight != null)
			{
				UnityEngine.Object.DestroyImmediate(enemiesTargetRight, true);
				enemiesTargetRight = null;
			}
			enemiesTargetLeft = CheckHalloweenGateReplacement(enemiesTargetLeft);
			break;
		case PlayModesManager.GameDirection.RightToLeft:
			if (enemiesTargetLeft != null)
			{
				UnityEngine.Object.DestroyImmediate(enemiesTargetLeft, true);
				enemiesTargetLeft = null;
				if (vortexLeft != null)
				{
					UnityEngine.Object.DestroyImmediate(vortexLeft, true);
					vortexLeft = null;
				}
				if (gateSparklesLeft != null)
				{
					UnityEngine.Object.DestroyImmediate(gateSparklesLeft, true);
					gateSparklesLeft = null;
				}
				enemiesTargetRight = CheckHalloweenGateReplacement(enemiesTargetRight);
			}
			break;
		}
	}

	private GameObject CheckHalloweenGateReplacement(GameObject orig)
	{
		if (Singleton<EventsManager>.instance.IsEventActive("halloween"))
		{
			Vector3 position = orig.transform.position;
			UnityEngine.Object.DestroyImmediate(orig);
			GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("Props/PFGateHalloween") as GameObject) as GameObject;
			gameObject.transform.position = position;
			return gameObject;
		}
		return orig;
	}

	private void CreateFPSCounter()
	{
		mFPSCounter = new FPSCounter("default18", Vector2.zero, 5000f);
	}

	private void UpdateFPSCounter()
	{
		if (mFPSCounter != null)
		{
			mFPSCounter.Update();
		}
	}
}
