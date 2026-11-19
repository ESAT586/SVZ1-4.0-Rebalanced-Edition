using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class DevCheatOverlay : MonoBehaviour
{
	private delegate int ModeAction();

	private const string kAccessCode = "S5716321453M";
	private const int kClassicTargetWave = 100;
	private const int kZombiesTargetWave = 100;
	private const int kCoinsGrant = 999999;
	private const int kGemsGrant = 99999;
	private const int kPachinkoGrant = 999;
	private const int kMaxResource = 999999999;

	public static bool sGodMode = false;
	public static bool sInfiniteLeadership = false;
	public static bool sNoCooldown = false;
	public static bool sOneHitKill = false;

	private static readonly string[] kTabs = { "Cheats", "Wave Lab", "Progress", "Resources", "Unlocks", "Upgrades", "Loadout", "Performance", "Profile", "Danger" };
	private static readonly string[] kWaveLabModes = { "classic", "zombies" };
	private static readonly int[] kClassicWavePresets = { 1, 10, 30, 50, 70, 80, 90, 100 };
	private static readonly int[] kZombieWavePresets = { 1, 10, 30, 31, 40, 50, 60, 70, 80, 90, 100 };
	private static readonly string[,] kKnownTutorials = {
		{ "TutorialWave2", "leadership" },
		{ "TutorialWave2", "summonAlly" },
		{ "TutorialWave1", "protectGate" },
		{ "TutorialWave1", "showMovements" },
		{ "TutorialWave1", "autoAttack" },
		{ "TutorialWave1", "ability" },
		{ "TutorialBonusWave1", "welcome" },
		{ "TutorialBonusWave1", "allies" },
		{ "TutorialBonusWave1", "charm" },
		{ "TutorialBonusWave1", "unlock" },
		{ "StoreDealPacks", "dealpacks" },
		{ "Store", "intro" },
		{ "PachinkoTutorial", "lever" },
		{ "PachinkoTutorial", "spin" },
		{ "PachinkoTutorial", "multiplier" },
		{ "reactives", "flyingEnemies" },
		{ "BoosterPacksTutorial", "freePack" }
	};
	private static DevCheatOverlay sInstance;

	private string mPasswordInput = string.Empty;
	private string mStatus = "";
	private bool mUnlocked = true;
	private int mSelectedTab;
	private Vector2 mScroll;
	private string mClassicWaveInput = kClassicTargetWave.ToString();
	private string mZombiesWaveInput = kZombiesTargetWave.ToString();
	private int mWaveLabModeIndex = 1;
	private string mWaveLabWaveInput = "31";
	private bool mConfirmReset;
	private bool mConfirmSaveRestore;
	private int mLastTouchScrollFrame = -1;
	private float mTouchDragAccum = 0f;
	private bool mTouchDragActive = false;

	public static bool IsEnabled
	{
		get { return true; }
	}

	public static bool IsOpen
	{
		get { return sInstance != null; }
	}

	public static void Open()
	{
		if (!IsEnabled)
		{
			return;
		}
		if (sInstance == null)
		{
			GameObject gameObject = new GameObject("DevCheatOverlay");
			Object.DontDestroyOnLoad(gameObject);
			sInstance = gameObject.AddComponent<DevCheatOverlay>();
		}
		sInstance.ResetLogin();
	}

	private void OnDestroy()
	{
		if (sInstance == this)
		{
			sInstance = null;
		}
	}

	private void Update()
	{
		if (!IsEnabled)
		{
			Close();
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Close();
		}

		// Apply realtime combat cheats
		if (sGodMode)
		{
			InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
			if (inGame != null && inGame.hero != null)
			{
				inGame.hero.health = inGame.hero.maxHealth;
				inGame.allAlliesInvincibleTimer = Mathf.Max(inGame.allAlliesInvincibleTimer, 1.0f);
				if (inGame.gate != null)
				{
					inGame.gate.health = inGame.gate.maxHealth;
				}
			}
		}

		if (sInfiniteLeadership)
		{
			Smithy smithy = WeakGlobalInstance<Smithy>.instance;
			if (smithy != null)
			{
				smithy.resources = smithy.maxResources;
			}
		}

		if (sNoCooldown)
		{
			Smithy smithy = WeakGlobalInstance<Smithy>.instance;
			if (smithy != null)
			{
				smithy.ResetAllCooldowns();
			}
			HUDAbilities hudAbilities = WeakGlobalInstance<HUDAbilities>.instance;
			if (hudAbilities != null)
			{
				hudAbilities.ResetAllCooldowns();
			}
		}

		if (sOneHitKill)
		{
			CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
			if (charMgr != null && charMgr.allEnemies != null)
			{
				foreach (Character c in charMgr.allEnemies)
				{
					if (c != null && c.health > 1f)
					{
						c.health = 1f;
					}
				}
			}
		}
	}

	private void OnGUI()
	{
		if (!IsEnabled)
		{
			return;
		}

		// Screen-height based virtual scaling with cutout/safe-area awareness across PC & Android 5-16
		float scale, virtualWidth, virtualHeight;
		Rect safeRect;
		SVZDeviceCompatibility.GetSafeVirtualViewport(720f, 940f, out scale, out virtualWidth, out virtualHeight, out safeRect);

		Matrix4x4 oldMatrix = GUI.matrix;
		GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

		int oldButtonSize = GUI.skin.button.fontSize;
		int oldLabelSize = GUI.skin.label.fontSize;
		int oldBoxSize = GUI.skin.box.fontSize;
		int oldTextFieldSize = GUI.skin.textField.fontSize;
		Font oldBtnFont = GUI.skin.button.font;
		Font oldLblFont = GUI.skin.label.font;
		Font oldBoxFont = GUI.skin.box.font;
		float oldScrollWidth = GUI.skin.verticalScrollbar.fixedWidth;
		float oldThumbWidth = GUI.skin.verticalScrollbarThumb.fixedWidth;

		try
		{
			GUI.depth = -10000;
			GUI.color = new Color(0f, 0f, 0f, 0.65f);
			GUI.DrawTexture(new Rect(0f, 0f, virtualWidth, virtualHeight), Texture2D.whiteTexture);
			GUI.color = Color.white;

			Font dynFont = LocalizationManager.GetDynamicOSFont(18);
			if (dynFont != null)
			{
				GUI.skin.button.font = dynFont;
				GUI.skin.label.font = dynFont;
				GUI.skin.box.font = dynFont;
			}
			GUI.skin.button.fontSize = 18;
			GUI.skin.label.fontSize = 17;
			GUI.skin.box.fontSize = 18;
			GUI.skin.textField.fontSize = 19;
			GUI.skin.verticalScrollbar.fixedWidth = 24f;
			GUI.skin.verticalScrollbarThumb.fixedWidth = 24f;

			float width = Mathf.Min(920f, safeRect.width * 0.96f);
			float height = Mathf.Min(680f, safeRect.height * 0.94f);
			Rect rect = new Rect(safeRect.x + (safeRect.width - width) * 0.5f, safeRect.y + (safeRect.height - height) * 0.5f, width, height);

			HandleTouchDragScroll(rect, scale);

			GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.97f);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = Color.white;
			GUI.ModalWindow(571632, rect, DrawWindow, "DEV TOOLS");
		}
		finally
		{
			GUI.skin.button.font = oldBtnFont;
			GUI.skin.label.font = oldLblFont;
			GUI.skin.box.font = oldBoxFont;
			GUI.skin.button.fontSize = oldButtonSize;
			GUI.skin.label.fontSize = oldLabelSize;
			GUI.skin.box.fontSize = oldBoxSize;
			GUI.skin.textField.fontSize = oldTextFieldSize;
			GUI.skin.verticalScrollbar.fixedWidth = oldScrollWidth;
			GUI.skin.verticalScrollbarThumb.fixedWidth = oldThumbWidth;
			GUI.matrix = oldMatrix;
		}
	}

	private void HandleTouchDragScroll(Rect windowRect, float scale)
	{
		if (Input.touchCount == 1)
		{
			Touch t = Input.GetTouch(0);
			Vector2 virtualTouch = new Vector2(t.position.x / scale, (Screen.height - t.position.y) / scale);
			if (t.phase == TouchPhase.Began)
			{
				mTouchDragAccum = 0f;
				mTouchDragActive = false;
			}
			else if (t.phase == TouchPhase.Moved && windowRect.Contains(virtualTouch))
			{
				mTouchDragAccum += Mathf.Abs(t.deltaPosition.y);
				if (mTouchDragAccum > 8f)
				{
					mTouchDragActive = true;
					if (mLastTouchScrollFrame != Time.frameCount)
					{
						mLastTouchScrollFrame = Time.frameCount;
						mScroll.y = Mathf.Max(0f, mScroll.y + (t.deltaPosition.y / scale));
					}
				}
			}
		}
		else
		{
			if (Input.touchCount == 0 && Event.current != null && Event.current.type == EventType.Repaint)
			{
				mTouchDragActive = false;
				mTouchDragAccum = 0f;
			}
			if (Input.touchCount == 0 && Event.current != null && Event.current.type == EventType.MouseDrag && windowRect.Contains(Event.current.mousePosition))
			{
				if (Mathf.Abs(Event.current.delta.y) > 0.5f && mLastTouchScrollFrame != Time.frameCount)
				{
					mLastTouchScrollFrame = Time.frameCount;
					mScroll.y = Mathf.Max(0f, mScroll.y - Event.current.delta.y);
					Event.current.Use();
				}
			}
		}
	}

	private void DrawWindow(int windowId)
	{
		mUnlocked = true;
		bool prevEnabled = GUI.enabled;
		if ((mTouchDragActive && Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended) || LocalizationManager.IsPickerOpen)
		{
			// Prevent accidental button tap on release immediately after a finger drag scroll or while language picker is open
			GUI.enabled = false;
		}
		try
		{
			GUILayout.BeginVertical();
			GUILayout.Space(10f);
			string headerStatus = string.IsNullOrEmpty(mStatus)
				? LocalizationManager.L("mod_devtools_title", "DEV TOOLS & CHEATS (UNLOCKED - REBALANCED)", "DEV TOOLS & CHEATS (KILITSIZ - REBALANCED)")
				: mStatus;
			GUILayout.Label(headerStatus);
			GUILayout.Space(8f);

			DrawDebugPanel();

			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			if (GUILayout.Button(LocalizationManager.L("mod_lang_switch", "LANGUAGE (30 LANGUAGES): ", "DIL SEC (30 DIL): ") + LocalizationManager.instance.GetCurrentLanguage().ToUpper(), GUILayout.Height(52f), GUILayout.Width(380f)))
			{
				LocalizationManager.OpenLanguagePicker();
			}
			if (GUILayout.Button(LocalizationManager.L("mod_close", "CLOSE", "KAPAT"), GUILayout.Height(52f)))
			{
				Close();
			}
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}
		finally
		{
			GUI.enabled = prevEnabled;
		}
	}

	private void DrawLogin()
	{
		GUI.SetNextControlName("DevPasswordField");
		mPasswordInput = GUILayout.PasswordField(mPasswordInput, '*', GUILayout.Height(52f));
		GUI.FocusControl("DevPasswordField");

		GUILayout.Space(8f);
		GUILayout.BeginHorizontal();
		GUI.color = new Color(0.4f, 1f, 0.4f);
		if (GUILayout.Button(LocalizationManager.T("SKIP PASSWORD & UNLOCK", "SIFREYI ATLA & KILIDI AC"), GUILayout.Height(54f)))
		{
			mUnlocked = true;
			mSelectedTab = 0;
			mStatus = LocalizationManager.T("Unlocked successfully!", "Kilit basariyla acildi!");
		}
		GUI.color = Color.white;
		if (GUILayout.Button("UNLOCK", GUILayout.Height(54f)))
		{
			SubmitPassword();
		}
		if (GUILayout.Button("PASTE", GUILayout.Height(54f)))
		{
			mPasswordInput = GUIUtility.systemCopyBuffer;
			mStatus = "Password pasted";
		}
		if (GUILayout.Button("CLEAR", GUILayout.Height(54f)))
		{
			mPasswordInput = string.Empty;
			mStatus = "Enter developer password";
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(8f);
		GUILayout.Label("This panel is available only in Editor and Development builds.");
	}

	private void DrawDebugPanel()
	{
		int newTab = GUILayout.SelectionGrid(mSelectedTab, kTabs, 5, GUILayout.Height(78f));
		if (newTab != mSelectedTab)
		{
			mSelectedTab = newTab;
			mScroll = Vector2.zero;
			mConfirmReset = false;
			mConfirmSaveRestore = false;
		}
		GUILayout.Space(8f);

		mScroll = GUILayout.BeginScrollView(mScroll);
		switch (mSelectedTab)
		{
		case 0:
			DrawCheatsTab();
			break;
		case 1:
			DrawWaveLabTab();
			break;
		case 2:
			DrawProgressTab();
			break;
		case 3:
			DrawResourcesTab();
			break;
		case 4:
			DrawUnlocksTab();
			break;
		case 5:
			DrawUpgradesTab();
			break;
		case 6:
			DrawLoadoutTab();
			break;
		case 7:
			DrawPerformanceTab();
			break;
		case 8:
			DrawProfileTab();
			break;
		case 9:
			DrawDangerTab();
			break;
		}
		GUILayout.EndScrollView();
	}


	private void DrawCheatsTab()
	{
		InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
		bool inCombat = inGame != null && inGame.hero != null;

		GUILayout.Box(inCombat
			? LocalizationManager.T("IN COMBAT: Toggle live battle cheats anytime.", "AKTIF SAVASTASINIZ: Savas ici hileleri canli olarak acip kapatabilirsiniz.")
			: LocalizationManager.T("IN MENU: Unlock waves, resources, and all content in one tap.", "MENUDESINIZ: Ilerleme, kaynak ve kilitli tum icerikleri tek tikla acabilirsiniz."));
		GUILayout.Space(8f);

		GUILayout.Label(LocalizationManager.T("REAL-TIME COMBAT CHEATS", "SAVAS ICI CANLI HILELER (REAL-TIME CHEATS)"));
		GUILayout.BeginHorizontal();
		GUI.color = sGodMode ? new Color(0.4f, 1f, 0.4f) : Color.white;
		if (GUILayout.Button(sGodMode ? LocalizationManager.L("mod_god_mode_on", "GOD MODE: [ON]", "GOD MODE: [ACIK]") : LocalizationManager.L("mod_god_mode_off", "GOD MODE: [OFF]", "GOD MODE: [KAPALI]"), GUILayout.Height(54f)))
		{
			sGodMode = !sGodMode;
			mStatus = sGodMode
				? LocalizationManager.T("God Mode ON: Hero, allies, and gate are invincible!", "God Mode aktif: Kahraman, muttefikler ve kale olumsuz yapildi!")
				: LocalizationManager.T("God Mode OFF.", "God Mode kapatildi.");
		}
		GUI.color = sInfiniteLeadership ? new Color(0.4f, 1f, 0.4f) : Color.white;
		if (GUILayout.Button(sInfiniteLeadership ? LocalizationManager.L("mod_leadership_inf", "LEADERSHIP: [INFINITE]", "LIDERLIK: [SINIRSIZ]") : LocalizationManager.L("mod_leadership_norm", "LEADERSHIP: [NORMAL]", "LIDERLIK: [NORMAL]"), GUILayout.Height(54f)))
		{
			sInfiniteLeadership = !sInfiniteLeadership;
			mStatus = sInfiniteLeadership
				? LocalizationManager.T("Infinite Leadership ON: Smithy always provides max leadership!", "Sinirsiz Liderlik aktif: Demirci her zaman max liderlik saglar!")
				: LocalizationManager.T("Infinite Leadership OFF.", "Sinirsiz liderlik kapatildi.");
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		GUI.color = sNoCooldown ? new Color(0.4f, 1f, 0.4f) : Color.white;
		if (GUILayout.Button(sNoCooldown ? LocalizationManager.L("mod_cooldown_zero", "COOLDOWN: [ZERO]", "BEKLEME SURESI: [SIFIR]") : LocalizationManager.L("mod_cooldown_norm", "COOLDOWN: [NORMAL]", "BEKLEME SURESI: [NORMAL]"), GUILayout.Height(54f)))
		{
			sNoCooldown = !sNoCooldown;
			mStatus = sNoCooldown
				? LocalizationManager.T("Zero Cooldown ON: Abilities and allies are ready instantly!", "Sifir bekleme suresi: Yetenekler ve birlikler aninda hazir!")
				: LocalizationManager.T("Normal cooldown restored.", "Normal bekleme suresi.");
		}
		GUI.color = sOneHitKill ? new Color(0.4f, 1f, 0.4f) : Color.white;
		if (GUILayout.Button(sOneHitKill ? LocalizationManager.L("mod_onehit_on", "ONE HIT KILL: [ON]", "TEK VURUSTA OLDUR: [ACIK]") : LocalizationManager.L("mod_onehit_off", "ONE HIT KILL: [OFF]", "TEK VURUSTA OLDUR: [KAPALI]"), GUILayout.Height(54f)))
		{
			sOneHitKill = !sOneHitKill;
			mStatus = sOneHitKill
				? LocalizationManager.T("One Hit Kill ON: All enemy HP reduced to 1!", "Tek vurusta oldur aktif: Tum dusmanlarin cani 1'e indirildi!")
				: LocalizationManager.T("One Hit Kill OFF.", "Tek vurusta oldur kapatildi.");
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();

		GUILayout.Space(8f);
		GUILayout.BeginHorizontal();
		GUI.color = new Color(1f, 0.4f, 0.4f);
		if (GUILayout.Button(LocalizationManager.L("mod_kill_all", "KILL ALL ENEMIES", "TUM DUSMANLARI YOK ET"), GUILayout.Height(52f)))
		{
			int killed = KillAllActiveEnemies();
			mStatus = killed + LocalizationManager.T(" enemies cleared from battlefield!", " adet dusman sahadan temizlendi!");
		}
		GUI.color = new Color(1f, 0.85f, 0.3f);
		if (GUILayout.Button(LocalizationManager.L("mod_win_wave", "INSTANT WIN WAVE", "DALGAYI ANINDA KAZAN (WIN)"), GUILayout.Height(52f)))
		{
			InstantWinWave();
			mStatus = LocalizationManager.T("Wave victory triggered!", "Dalga zaferi tetiklendi!");
		}
		GUI.color = new Color(0.4f, 0.9f, 1f);
		if (GUILayout.Button(LocalizationManager.L("mod_collect_loot", "COLLECT ALL LOOT", "GANIMETLERI TOPLA"), GUILayout.Height(52f)))
		{
			CollectableManager cMgr = WeakGlobalInstance<CollectableManager>.instance;
			int c = (cMgr != null) ? cMgr.CollectAll() : 0;
			mStatus = c + LocalizationManager.T(" loot items collected!", " adet ganimet toplandi!");
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();

		GUILayout.Space(12f);
		GUILayout.Label(LocalizationManager.T("DIFFICULTY MODES - HOTKEY: [F2]", "ZORLUK MODU SECIMI (DIFFICULTY MODES) - KISAYOL: [F2]"));
		GUILayout.BeginHorizontal();
		GUI.color = (PCQoLManager.CurrentDifficulty == GameDifficulty.Kolay) ? new Color(0.3f, 1f, 0.4f) : new Color(0.25f, 0.25f, 0.3f);
		if (GUILayout.Button(LocalizationManager.L("mod_diff_easy", "EASY (0.75x)", "KOLAY (0.75x)"), GUILayout.Height(46f)))
		{
			PCQoLManager.CurrentDifficulty = GameDifficulty.Kolay;
			mStatus = LocalizationManager.T("Difficulty set to EASY! (Enemy HP & damage -25%)", "Zorluk KOLAY olarak ayarlandi! (Dusman cani ve hasari %25 azaldi)");
		}
		GUI.color = (PCQoLManager.CurrentDifficulty == GameDifficulty.Normal) ? new Color(1f, 0.9f, 0.3f) : new Color(0.25f, 0.25f, 0.3f);
		if (GUILayout.Button(LocalizationManager.L("mod_diff_normal", "NORMAL (1.0x)", "NORMAL (1.0x)"), GUILayout.Height(46f)))
		{
			PCQoLManager.CurrentDifficulty = GameDifficulty.Normal;
			mStatus = LocalizationManager.T("Difficulty set to NORMAL.", "Zorluk NORMAL olarak ayarlandi.");
		}
		GUI.color = (PCQoLManager.CurrentDifficulty == GameDifficulty.Zor) ? new Color(1f, 0.6f, 0.2f) : new Color(0.25f, 0.25f, 0.3f);
		if (GUILayout.Button(LocalizationManager.L("mod_diff_hard", "HARD (1.35x)", "ZOR (1.35x)"), GUILayout.Height(46f)))
		{
			PCQoLManager.CurrentDifficulty = GameDifficulty.Zor;
			mStatus = LocalizationManager.T("Difficulty set to HARD! (Enemies +35% stronger, +50% loot)", "Zorluk ZOR olarak ayarlandi! (Dusmanlar %35 daha guclu, ganimet %50 fazla)");
		}
		GUI.color = (PCQoLManager.CurrentDifficulty == GameDifficulty.Kabus) ? new Color(1f, 0.25f, 0.25f) : new Color(0.25f, 0.25f, 0.3f);
		if (GUILayout.Button(LocalizationManager.L("mod_diff_hell", "HELL / NIGHTMARE (1.8x)", "KABUS / HELL (1.8x)"), GUILayout.Height(46f)))
		{
			PCQoLManager.CurrentDifficulty = GameDifficulty.Kabus;
			mStatus = LocalizationManager.T("Difficulty set to HELL! (Extreme challenge, 2x Loot!)", "Zorluk KABUS (HELL) olarak ayarlandi! (Ekstra Edition meydan okumasi, 2x Ganimet!)");
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();

		GUILayout.Space(8f);
		GUILayout.BeginHorizontal();
		GUI.color = new Color(0.3f, 0.8f, 1f);
		if (GUILayout.Button(LocalizationManager.L("mod_almanac_btn", "ENEMY & ALLY ALMANAC [B]", "DUSMAN & MUTTEFIK REHBERI (ALMANAC) [B]"), GUILayout.Height(48f)))
		{
			SVZAlmanac.Toggle();
		}
		GUI.color = new Color(1f, 0.5f, 0.2f);
		if (GUILayout.Button(LocalizationManager.L("mod_mp_btn", "1v1 PvP MULTIPLAYER MODE [M]", "1v1 PvP COK OYUNCULU MOD [M]"), GUILayout.Height(48f)))
		{
			SVZMultiplayerUI.ToggleLobby();
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();

		GUILayout.Space(12f);
		GUILayout.Box(LocalizationManager.T(
			"SHINTO PRIEST HAT LIVE ROTATION TUNER\nCurrent Angle: " + CharacterModelController.sPriestHatEulerOffset + "\n(Adjust angles live so the hat sits level on his head)",
			"SINTO RAHIBI (PRIEST) SAPKA CANLI ROTASYON AYARI (LIVE TUNER)\nGuncel Aci: " + CharacterModelController.sPriestHatEulerOffset + "\n(Sapkayi kafasinda tam yatay/duz yapacak acilari canli olarak ayarlayabilirsiniz)"));

		GUILayout.BeginHorizontal();
		GUI.color = new Color(0.3f, 1f, 0.5f);
		if (GUILayout.Button(LocalizationManager.T("DEFAULT FLAT (0, 0, 0)", "VARSAYILAN DUZ (0, 0, 0)"), GUILayout.Height(36f)))
		{
			CharacterModelController.SavePriestHatConfig(Vector3.zero);
			mStatus = LocalizationManager.T("Priest hat reset to flat: (0, 0, 0)", "Priest sapkasi duz ayarlandi: (0, 0, 0)");
		}
		GUI.color = new Color(0.9f, 0.9f, 0.4f);
		if (GUILayout.Button(LocalizationManager.T("RESET (0, 0, 0)", "SIFIRLA (0, 0, 0)"), GUILayout.Height(36f)))
		{
			CharacterModelController.SavePriestHatConfig(Vector3.zero);
			mStatus = LocalizationManager.T("Priest hat reset: (0, 0, 0)", "Priest sapkasi sifirlandi: (0, 0, 0)");
		}
		GUI.color = new Color(0.5f, 0.8f, 1f);
		if (GUILayout.Button("X 90°", GUILayout.Height(36f)))
		{
			CharacterModelController.SavePriestHatConfig(new Vector3(90f, 0f, 0f));
		}
		if (GUILayout.Button("Z 90°", GUILayout.Height(36f)))
		{
			CharacterModelController.SavePriestHatConfig(new Vector3(0f, 0f, 90f));
		}
		GUI.color = Color.white;
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("X -15°", GUILayout.Height(32f)))
		{
			Vector3 cur = CharacterModelController.sPriestHatEulerOffset;
			CharacterModelController.SavePriestHatConfig(new Vector3(cur.x - 15f, cur.y, cur.z));
		}
		if (GUILayout.Button("X +15°", GUILayout.Height(32f)))
		{
			Vector3 cur = CharacterModelController.sPriestHatEulerOffset;
			CharacterModelController.SavePriestHatConfig(new Vector3(cur.x + 15f, cur.y, cur.z));
		}
		if (GUILayout.Button("Y -15°", GUILayout.Height(32f)))
		{
			Vector3 cur = CharacterModelController.sPriestHatEulerOffset;
			CharacterModelController.SavePriestHatConfig(new Vector3(cur.x, cur.y - 15f, cur.z));
		}
		if (GUILayout.Button("Y +15°", GUILayout.Height(32f)))
		{
			Vector3 cur = CharacterModelController.sPriestHatEulerOffset;
			CharacterModelController.SavePriestHatConfig(new Vector3(cur.x, cur.y + 15f, cur.z));
		}
		if (GUILayout.Button("Z -15°", GUILayout.Height(32f)))
		{
			Vector3 cur = CharacterModelController.sPriestHatEulerOffset;
			CharacterModelController.SavePriestHatConfig(new Vector3(cur.x, cur.y, cur.z - 15f));
		}
		if (GUILayout.Button("Z +15°", GUILayout.Height(32f)))
		{
			Vector3 cur = CharacterModelController.sPriestHatEulerOffset;
			CharacterModelController.SavePriestHatConfig(new Vector3(cur.x, cur.y, cur.z + 15f));
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(14f);
		GUILayout.Label(LocalizationManager.T("ONE-CLICK MEGA UNLOCKS", "TEK TIKLA MEGA HILELER (ONE-CLICK MEGA UNLOCKS)"));

		GUI.color = new Color(0.3f, 1f, 0.6f);
		if (GUILayout.Button(LocalizationManager.L("mod_unlock_all", "UNLOCK EVERYTHING (WAVES 1-100 & FULL STORE)", "TUM OYUNU VE HER SEYI AC (WAVE 1-100 & MAGAZA)"), GUILayout.Height(58f)))
		{
			UnlockModeProgressAndRestore("classic", kClassicTargetWave);
			UnlockModeProgressAndRestore("zombies", kZombiesTargetWave);
			UnlockStoreForMode("classic");
			UnlockStoreForMode("zombies");
			UnlockSpecialHelpersForMode("classic");
			UnlockSpecialHelpersForMode("zombies");
			SaveProfile(LocalizationManager.T("All content unlocked: Classic & Zombies Waves 1-100 and full store!", "Tum oyun basariyla acildi: Classic & Zombies Wave 1-100 ve tum magaza!"));
			mStatus = LocalizationManager.T("EVERYTHING UNLOCKED! Classic 1-100, Zombies 1-100, special heroes, and store ready!", "TUM OYUN ACILDI! Classic 1-100, Zombies 1-100, ozel kahramanlar ve magaza hazir!");
		}

		GUI.color = new Color(0.4f, 0.8f, 1f);
		if (GUILayout.Button(LocalizationManager.L("mod_max_all", "MAX ALL ALLIES, ABILITIES & UPGRADES", "TUM BIRLIK, YETENEK VE GELISTIRMELERI MAX SEVIYE YAP"), GUILayout.Height(58f)))
		{
			MaxCoreUpgradesForMode("classic");
			MaxCoreUpgradesForMode("zombies");
			SetCurrentModeStoreItemsToLevel(true);
			SaveProfile(LocalizationManager.T("All upgrades maxed out!", "Tum gelistirmeler max seviyeye cikarildi!"));
			mStatus = LocalizationManager.T("ALL UPGRADES MAXED! Weapons, gate, health, and all units at max level!", "TUM GELISTIRMELER MAX! Silahlar, kale, can ve tum birlikler zirvede!");
		}

		GUI.color = new Color(1f, 0.85f, 0.2f);
		if (GUILayout.Button(LocalizationManager.L("mod_get_rich", "GET RICH (+10,000,000 Coins, +500,000 Gems, +9,999 Pachinko)", "ZENGIN OL (+10,000,000 Altin, +500,000 Elmas, +9,999 Pachinko)"), GUILayout.Height(58f)))
		{
			Singleton<Profile>.instance.coins = Mathf.Min(kMaxResource, Singleton<Profile>.instance.coins + 10000000);
			Singleton<Profile>.instance.gems = Mathf.Min(kMaxResource, Singleton<Profile>.instance.gems + 500000);
			Singleton<Profile>.instance.pachinkoBalls = Mathf.Min(kMaxResource, Singleton<Profile>.instance.pachinkoBalls + 9999);
			SaveProfile(LocalizationManager.T("Added 10M Coins, 500K Gems, and 9999 Pachinko balls!", "10M Altin, 500K Elmas ve 9999 Pachinko eklendi!"));
			mStatus = LocalizationManager.T("RICH MODE! +10,000,000 Coins, +500,000 Gems, +9,999 Pachinko granted!", "ZENGIN OLUNDU! +10,000,000 Altin, +500,000 Elmas, +9,999 Pachinko verildi!");
		}
		GUI.color = Color.white;
	}

	private int KillAllActiveEnemies()
	{
		CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
		if (charMgr == null || charMgr.allEnemies == null) return 0;
		List<Character> enemies = new List<Character>(charMgr.allEnemies);
		int count = 0;
		foreach (Character c in enemies)
		{
			if (c != null && c.health > 0f)
			{
				c.health = 0f;
				count++;
			}
		}
		return count;
	}

	private void InstantWinWave()
	{
		KillAllActiveEnemies();
		GameHUD hud = WeakGlobalInstance<GameHUD>.instance;
		if (hud != null)
		{
			hud.WinTheWave();
		}
	}

	private void DrawWaveLabTab()
	{
		GUILayout.Box(GetWaveLabStatus());
		GUILayout.Space(8f);

		GUILayout.Label("Target mode");
		int newMode = GUILayout.Toolbar(mWaveLabModeIndex, kWaveLabModes, GUILayout.Height(48f));
		if (newMode != mWaveLabModeIndex)
		{
			mWaveLabModeIndex = newMode;
			mWaveLabWaveInput = waveLabMode == "zombies" ? "31" : "50";
		}

		GUILayout.Space(8f);
		GUILayout.Label("Target wave");
		mWaveLabWaveInput = GUILayout.TextField(mWaveLabWaveInput, GUILayout.Height(48f));
		DrawWavePresetButtons(waveLabMode == "zombies" ? kZombieWavePresets : kClassicWavePresets);

		GUILayout.Space(12f);
		if (GUILayout.Button("PREPARE WAVE", GUILayout.Height(58f)))
		{
			int target = PrepareWaveForMode(waveLabMode, ParseWaveInput(mWaveLabWaveInput, GetWaveLabFallbackWave()), false);
			SaveProfile("Prepared " + waveLabMode + " wave " + target);
		}
		if (GUILayout.Button("SET EXACT PROGRESS", GUILayout.Height(58f)))
		{
			int target = PrepareWaveForMode(waveLabMode, ParseWaveInput(mWaveLabWaveInput, GetWaveLabFallbackWave()), true);
			SaveProfile("Exact progress set: " + waveLabMode + " wave " + target);
		}
		if (GUILayout.Button("OPEN ZOMBIE WAVE SELECT TEST", GUILayout.Height(58f)))
		{
			int target = PrepareWaveForMode("zombies", 31, true);
			mWaveLabModeIndex = 1;
			mWaveLabWaveInput = target.ToString();
			SaveProfile("Zombie wave select test ready: waves 1-31 open");
		}
		if (GUILayout.Button("ZOMBIE WAVE 100 FINAL TEST", GUILayout.Height(58f)))
		{
			int target = PrepareWaveForMode("zombies", kZombiesTargetWave, true);
			mWaveLabModeIndex = 1;
			mWaveLabWaveInput = target.ToString();
			SaveProfile("Zombie final test ready: waves 1-100 open");
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("ZOMBIE BONUS 1", GUILayout.Height(54f)))
		{
			PrepareZombieBonusTest(1);
			SaveProfile("Zombie bonus 1 ready");
		}
		if (GUILayout.Button("ZOMBIE BONUS 5", GUILayout.Height(54f)))
		{
			PrepareZombieBonusTest(5);
			SaveProfile("Zombie bonus 5 ready");
		}
		if (GUILayout.Button("ZOMBIE BONUS 10", GUILayout.Height(54f)))
		{
			PrepareZombieBonusTest(10);
			SaveProfile("Zombie bonus 10 ready");
		}
		GUILayout.EndHorizontal();
		if (GUILayout.Button("BALANCED TEST PRESET", GUILayout.Height(58f)))
		{
			int total = ApplyBalancedTestPreset(waveLabMode);
			SaveProfile("Balanced test preset applied to " + waveLabMode + ": " + total);
		}

		GUILayout.Space(8f);
		GUILayout.Box("Prepare keeps future progress. Exact progress locks waves after the target.");
	}

	private void DrawWavePresetButtons(int[] presets)
	{
		GUILayout.BeginHorizontal();
		for (int i = 0; i < presets.Length; i++)
		{
			if (i > 0 && i % 4 == 0)
			{
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
			}
			if (GUILayout.Button(presets[i].ToString(), GUILayout.Height(48f)))
			{
				mWaveLabWaveInput = presets[i].ToString();
			}
		}
		GUILayout.EndHorizontal();
	}

	private void DrawProgressTab()
	{
		Profile profile = Singleton<Profile>.instance;
		string currentMode = Singleton<PlayModesManager>.instance.selectedMode;
		GUILayout.Box("Current mode: " + currentMode + " | Wave to beat: " + profile.waveToBeat + " | Highest wave: " + profile.highestUnlockedWave);
		GUILayout.Space(8f);

		GUILayout.Label("Classic target wave");
		mClassicWaveInput = GUILayout.TextField(mClassicWaveInput, GUILayout.Height(48f));
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("CLASSIC WAVE 70", GUILayout.Height(54f)))
		{
			UnlockModeProgressAndRestore("classic", kClassicTargetWave);
			SaveProfile("Classic wave 70 unlocked");
		}
		if (GUILayout.Button("UNLOCK CLASSIC TARGET", GUILayout.Height(54f)))
		{
			int target = ParseWaveInput(mClassicWaveInput, kClassicTargetWave);
			UnlockModeProgressAndRestore("classic", target);
			SaveProfile("Classic wave " + target + " unlocked");
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(12f);
		GUILayout.Label("Zombies target wave");
		mZombiesWaveInput = GUILayout.TextField(mZombiesWaveInput, GUILayout.Height(48f));
		GUILayout.BeginHorizontal();
			if (GUILayout.Button("ZOMBIE WAVE 100", GUILayout.Height(54f)))
			{
				UnlockModeProgressAndRestore("zombies", kZombiesTargetWave);
				SaveProfile("Zombie wave 100 unlocked");
		}
		if (GUILayout.Button("UNLOCK ZOMBIE TARGET", GUILayout.Height(54f)))
		{
			int target = ParseWaveInput(mZombiesWaveInput, kZombiesTargetWave);
			UnlockModeProgressAndRestore("zombies", target);
			SaveProfile("Zombie wave " + target + " unlocked");
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(12f);
		if (GUILayout.Button("RESTORE FULL PROGRESS", GUILayout.Height(58f)))
		{
			RestoreFullProgress();
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("UNLOCK ALL CHAPTERS", GUILayout.Height(54f)))
		{
			int total = UnlockAllChaptersForBothModes();
			SaveProfile("Chapters unlocked: " + total);
		}
		if (GUILayout.Button("COMPLETE TUTORIALS", GUILayout.Height(54f)))
		{
			CompleteKnownTutorials();
			SaveProfile("Known tutorials completed");
		}
		GUILayout.EndHorizontal();
	}

	private void DrawResourcesTab()
	{
		Profile profile = Singleton<Profile>.instance;
		GUILayout.Box("Coins: " + profile.coins + " | Gems: " + profile.gems + " | Pachinko: " + profile.pachinkoBalls + " | Free boosters: " + profile.freeBoosterPacks);
		GUILayout.Space(8f);

		DrawResourceRow("COINS", AddCoins, SetCoinsMax);
		DrawResourceRow("GEMS", AddGems, SetGemsMax);
		DrawResourceRow("PACHINKO", AddPachinko, SetPachinkoMax);
		DrawResourceRow("FREE BOOSTER PACKS", AddFreeBoosters, SetFreeBoostersMax);

		GUILayout.Space(12f);
		GUILayout.Label("Consumables");
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("ALL POTIONS +10", GUILayout.Height(50f)))
		{
			int total = AddAllPotions(10);
			SaveProfile("Potion stacks increased: " + total);
		}
		if (GUILayout.Button("ALL POTIONS +99", GUILayout.Height(50f)))
		{
			int total = AddAllPotions(99);
			SaveProfile("Potion stacks increased: " + total);
		}
		if (GUILayout.Button("ALL POTIONS MAX", GUILayout.Height(50f)))
		{
			int total = SetAllPotions(kMaxResource);
			SaveProfile("Potions set to max: " + total);
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("STORE CHARMS +10", GUILayout.Height(50f)))
		{
			int total = AddStoreCharms(10);
			SaveProfile("Store charm stacks increased: " + total);
		}
		if (GUILayout.Button("STORE CHARMS +99", GUILayout.Height(50f)))
		{
			int total = AddStoreCharms(99);
			SaveProfile("Store charm stacks increased: " + total);
		}
		if (GUILayout.Button("STORE CHARMS MAX", GUILayout.Height(50f)))
		{
			int total = SetStoreCharms(kMaxResource);
			SaveProfile("Store charms set to max: " + total);
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(12f);
		if (GUILayout.Button("APPLY STANDARD RESOURCE PRESET", GUILayout.Height(58f)))
		{
			AddResources();
			SaveProfile("Resource preset applied");
		}
	}

	private void DrawResourceRow(string title, OnSUIIntCallback addFunc, OnSUIGenericCallback maxFunc)
	{
		GUILayout.Label(title);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("+1000", GUILayout.Height(50f)))
		{
			addFunc(1000);
		}
		if (GUILayout.Button("+10000", GUILayout.Height(50f)))
		{
			addFunc(10000);
		}
		if (GUILayout.Button("MAX", GUILayout.Height(50f)))
		{
			maxFunc();
		}
		GUILayout.EndHorizontal();
	}

	private void DrawUnlocksTab()
	{
		GUILayout.Box("Store unlocks affect visible store items. Special helper unlocks open visible special units like Necromancer, but hidden summon/charm internals stay untouched.");
		GUILayout.Space(8f);

		if (GUILayout.Button("UNLOCK CURRENT MODE STORE", GUILayout.Height(58f)))
		{
			int total = UnlockCurrentStoreItems();
			SaveProfile("Current store unlocks applied: " + total);
		}
		if (GUILayout.Button("UNLOCK CLASSIC STORE", GUILayout.Height(58f)))
		{
			int total = UnlockStoreForMode("classic");
			SaveProfile("Classic store unlocks applied: " + total);
		}
		if (GUILayout.Button("UNLOCK ZOMBIES STORE", GUILayout.Height(58f)))
		{
			int total = UnlockStoreForMode("zombies");
			SaveProfile("Zombie store unlocks applied: " + total);
		}
		if (GUILayout.Button("UNLOCK BOTH STORES", GUILayout.Height(58f)))
		{
			int total = UnlockStoreForMode("classic") + UnlockStoreForMode("zombies");
			SaveProfile("Both store unlocks applied: " + total);
		}
		GUILayout.Space(12f);
		if (GUILayout.Button("UNLOCK CURRENT MODE SPECIAL HELPERS", GUILayout.Height(58f)))
		{
			int total = UnlockSpecialHelpersCurrentMode();
			SaveProfile("Current special helpers unlocked: " + total);
		}
		if (GUILayout.Button("UNLOCK ZOMBIES SPECIAL HELPERS", GUILayout.Height(58f)))
		{
			int total = UnlockSpecialHelpersForMode("zombies");
			SaveProfile("Zombie special helpers unlocked: " + total);
		}
		if (GUILayout.Button("UNLOCK BOTH MODE SPECIAL HELPERS", GUILayout.Height(58f)))
		{
			int total = UnlockSpecialHelpersForMode("classic") + UnlockSpecialHelpersForMode("zombies");
			SaveProfile("Both mode special helpers unlocked: " + total);
		}
	}

	private void DrawUpgradesTab()
	{
		Profile profile = Singleton<Profile>.instance;
		GUILayout.Box("Mode: " + Singleton<PlayModesManager>.instance.selectedMode + " | Hero " + profile.heroLevel + " | Sword " + profile.swordLevel + " | Bow " + profile.bowLevel + " | Base " + profile.baseLevel);
		GUILayout.Space(8f);

		if (GUILayout.Button("MAX CURRENT MODE CORE UPGRADES", GUILayout.Height(58f)))
		{
			int total = MaxCoreUpgradesCurrentMode();
			SaveProfile("Current mode upgrades maxed: " + total);
		}
		if (GUILayout.Button("MAX CLASSIC CORE UPGRADES", GUILayout.Height(58f)))
		{
			int total = MaxCoreUpgradesForMode("classic");
			SaveProfile("Classic upgrades maxed: " + total);
		}
		if (GUILayout.Button("MAX ZOMBIES CORE UPGRADES", GUILayout.Height(58f)))
		{
			int total = MaxCoreUpgradesForMode("zombies");
			SaveProfile("Zombie upgrades maxed: " + total);
		}
		if (GUILayout.Button("MAX BOTH MODE CORE UPGRADES", GUILayout.Height(58f)))
		{
			int total = MaxCoreUpgradesForMode("classic") + MaxCoreUpgradesForMode("zombies");
			SaveProfile("Both mode upgrades maxed: " + total);
		}
	}

	private void DrawLoadoutTab()
	{
		Profile profile = Singleton<Profile>.instance;
		GUILayout.Box("Selected helpers: " + profile.GetSelectedHelpers().Count + "/" + profile.maxSelectedHelpers + " | Selected abilities: " + profile.GetSelectedAbilities().Count + "/" + profile.maxSelectedAbilities + " | Charm: " + profile.selectedCharm);
		GUILayout.Space(8f);

		GUILayout.Label("Visible/store-safe helpers and abilities only");
		if (GUILayout.Button("LEVEL 1 CURRENT MODE LOADOUT ITEMS", GUILayout.Height(58f)))
		{
			int total = SetCurrentModeStoreItemsToLevel(false);
			SaveProfile("Loadout items set to level 1: " + total);
		}
		if (GUILayout.Button("MAX CURRENT MODE LOADOUT ITEMS", GUILayout.Height(58f)))
		{
			int total = SetCurrentModeStoreItemsToLevel(true);
			SaveProfile("Loadout items maxed: " + total);
		}
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("AUTO-FILL SELECTED HELPERS", GUILayout.Height(54f)))
		{
			int total = AutoFillSelectedHelpers();
			SaveProfile("Selected helpers filled: " + total);
		}
		if (GUILayout.Button("AUTO-FILL SELECTED ABILITIES", GUILayout.Height(54f)))
		{
			int total = AutoFillSelectedAbilities();
			SaveProfile("Selected abilities filled: " + total);
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("CLEAR SELECTED HELPERS", GUILayout.Height(54f)))
		{
			profile.SetSelectedHelpers(new List<string>());
			SaveProfile("Selected helpers cleared");
		}
		if (GUILayout.Button("CLEAR SELECTED ABILITIES", GUILayout.Height(54f)))
		{
			profile.SetSelectedAbilities(new List<string>());
			SaveProfile("Selected abilities cleared");
		}
		if (GUILayout.Button("CLEAR CHARM", GUILayout.Height(54f)))
		{
			profile.selectedCharm = string.Empty;
			SaveProfile("Selected charm cleared");
		}
		GUILayout.EndHorizontal();
	}

	private void DrawPerformanceTab()
	{
		Profile profile = Singleton<Profile>.instance;
		GUILayout.Box(PerformanceSettings.GetDebugSummary() + "\nSave path: " + profile.saveFilePath);
		GUILayout.Space(8f);

		GUILayout.Label("Graphics presets");
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("LOW", GUILayout.Height(52f)))
		{
			ApplyPerformancePresetFromDev(0);
		}
		if (GUILayout.Button("MEDIUM", GUILayout.Height(52f)))
		{
			ApplyPerformancePresetFromDev(1);
		}
		if (GUILayout.Button("HIGH", GUILayout.Height(52f)))
		{
			ApplyPerformancePresetFromDev(2);
		}
		if (GUILayout.Button("ULTRA", GUILayout.Height(52f)))
		{
			ApplyPerformancePresetFromDev(3);
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(10f);
		GUILayout.Label("FPS test limits");
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("60 FPS", GUILayout.Height(52f)))
		{
			SetFPSFromDev(1);
		}
		if (GUILayout.Button("120 FPS", GUILayout.Height(52f)))
		{
			SetFPSFromDev(3);
		}
		if (GUILayout.Button("UNLIMITED", GUILayout.Height(52f)))
		{
			SetFPSFromDev(5);
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("VSYNC OFF", GUILayout.Height(52f)))
		{
			SetVSyncFromDev(false);
		}
		if (GUILayout.Button("VSYNC ON", GUILayout.Height(52f)))
		{
			SetVSyncFromDev(true);
		}
		if (GUILayout.Button(FPSCounter.showFPS ? "HIDE FPS" : "SHOW FPS", GUILayout.Height(52f)))
		{
			FPSCounter.showFPS = !FPSCounter.showFPS;
			mStatus = "FPS overlay " + (FPSCounter.showFPS ? "enabled" : "disabled");
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(10f);
		if (GUILayout.Button("APPLY SAVED PERFORMANCE SETTINGS", GUILayout.Height(56f)))
		{
			PerformanceSettings.ApplySavedSettings();
			mStatus = "Saved performance settings applied";
		}
		if (GUILayout.Button("COPY BUILD CHECK SUMMARY", GUILayout.Height(56f)))
		{
			GUIUtility.systemCopyBuffer = GetBuildCheckSummary();
			mStatus = "Build/performance summary copied";
		}
		if (GUILayout.Button("COPY ZOMBIE 50-100 LOAD REPORT", GUILayout.Height(56f)))
		{
			GUIUtility.systemCopyBuffer = GetZombieWaveLoadReport();
			mStatus = "Zombie wave load report copied";
		}
		if (GUILayout.Button("COPY ENDGAME CHECKPOINT REPORT", GUILayout.Height(56f)))
		{
			GUIUtility.systemCopyBuffer = GetZombieEndgameCheckpointReport();
			mStatus = "Zombie endgame checkpoint report copied";
		}
		GUILayout.Box("High FPS automatically keeps VSync OFF. On 60 Hz Android screens, 60 FPS can still be the real device ceiling.");
	}

	private void DrawProfileTab()
	{
		Profile profile = Singleton<Profile>.instance;
		GUILayout.Box("Slot " + (profile.currentSlot + 1) + " | Mode: " + Singleton<PlayModesManager>.instance.selectedMode + " | Save: " + profile.saveFilePath);
		GUILayout.Box(GetProfileSummary());
		GUILayout.Space(8f);

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("SLOT 1", GUILayout.Height(50f)))
		{
			SetSlot(0);
		}
		if (GUILayout.Button("SLOT 2", GUILayout.Height(50f)))
		{
			SetSlot(1);
		}
		if (GUILayout.Button("SLOT 3", GUILayout.Height(50f)))
		{
			SetSlot(2);
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(12f);
		if (GUILayout.Button("SAVE NOW", GUILayout.Height(54f)))
		{
			SaveProfile("Profile save requested");
		}
		if (GUILayout.Button("COPY PROFILE SUMMARY", GUILayout.Height(54f)))
		{
			GUIUtility.systemCopyBuffer = GetProfileSummary();
			mStatus = "Profile summary copied";
		}
		if (GUILayout.Button("COPY SAVE EXPORT", GUILayout.Height(54f)))
		{
			GUIUtility.systemCopyBuffer = profile.ExportSaveData();
			mStatus = "Save export copied";
		}
		if (GUILayout.Button("IMPORT SAVE FROM CLIPBOARD", GUILayout.Height(54f)))
		{
			string raw = GUIUtility.systemCopyBuffer;
			if (string.IsNullOrEmpty(raw))
			{
				mStatus = "Clipboard is empty";
			}
			else
			{
				profile.ImportSaveData(raw);
				SaveProfile("Save imported from clipboard");
			}
		}
		DrawSaveTools();
	}

	private void DrawSaveTools()
	{
		Profile profile = Singleton<Profile>.instance;
		GUILayout.Space(14f);
		GUILayout.Box("Save Tools\n" + GetSaveFilesSummary(profile.saveFilePath));
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("CREATE MANUAL BACKUP", GUILayout.Height(54f)))
		{
			if (profile.CreateManualSaveBackup())
			{
				mStatus = "Manual save backup created";
			}
			else
			{
				mStatus = "Manual backup failed";
			}
		}
		if (GUILayout.Button("VERIFY SAVE HEALTH", GUILayout.Height(54f)))
		{
			string error = string.Empty;
			if (profile.VerifySaveHealth(out error))
			{
				mStatus = "Save health OK";
			}
			else
			{
				mStatus = "Save health failed: " + error;
			}
		}
		GUILayout.EndHorizontal();

		if (!mConfirmSaveRestore)
		{
			if (GUILayout.Button("RESTORE LATEST BACKUP", GUILayout.Height(54f)))
			{
				mConfirmSaveRestore = true;
				mStatus = "Press confirm restore to replace current save";
			}
		}
		else
		{
			GUILayout.Box("Restore will replace the current slot from the first healthy backup.");
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("CONFIRM RESTORE", GUILayout.Height(54f)))
			{
				if (profile.RestoreLatestSaveBackup())
				{
					mStatus = "Latest healthy backup restored";
				}
				else
				{
					mStatus = "No healthy backup found";
				}
				mConfirmSaveRestore = false;
			}
			if (GUILayout.Button("CANCEL RESTORE", GUILayout.Height(54f)))
			{
				mConfirmSaveRestore = false;
				mStatus = "Restore cancelled";
			}
			GUILayout.EndHorizontal();
		}
	}

	private void DrawDangerTab()
	{
		GUILayout.Box("Danger actions can overwrite current progress. Current save is copied before reset.");
		GUILayout.Space(8f);

		if (!mConfirmReset)
		{
			if (GUILayout.Button("RESET CURRENT PROFILE SLOT", GUILayout.Height(58f)))
			{
				GUIUtility.systemCopyBuffer = Singleton<Profile>.instance.ExportSaveData();
				mConfirmReset = true;
				mStatus = "Backup copied. Press confirm reset to erase current slot";
			}
		}
		else
		{
			GUILayout.Box("Backup copied to clipboard. Confirm reset for slot " + (Singleton<Profile>.instance.currentSlot + 1));
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("CONFIRM RESET", GUILayout.Height(58f)))
			{
				Singleton<Profile>.instance.ResetData();
				SaveProfile("Current slot reset. Previous save export is on clipboard");
				mConfirmReset = false;
			}
			if (GUILayout.Button("CANCEL", GUILayout.Height(58f)))
			{
				mConfirmReset = false;
				mStatus = "Reset cancelled";
			}
			GUILayout.EndHorizontal();
		}
	}

	private void SubmitPassword()
	{
		if (mPasswordInput == kAccessCode)
		{
			mUnlocked = true;
			mSelectedTab = 0;
			mScroll = Vector2.zero;
			mConfirmReset = false;
			mConfirmSaveRestore = false;
			mStatus = "Developer debug panel";
		}
		else
		{
			mPasswordInput = string.Empty;
			mStatus = "Wrong password";
		}
	}

	private void ResetLogin()
	{
		mUnlocked = true;
		mPasswordInput = string.Empty;
		mSelectedTab = 0;
		mScroll = Vector2.zero;
		mConfirmReset = false;
		mConfirmSaveRestore = false;
		mStatus = "DEV TOOLS & CHEATS (KILITSIZ - REBALANCED)";
	}

	private void RestoreFullProgress()
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		UnlockModeProgress("classic", kClassicTargetWave);
		int classicUnlocks = UnlockCurrentStoreItems();
		MaxCoreUpgradesCurrentMode();
		UnlockAllChaptersCurrentMode();
		UnlockModeProgress("zombies", kZombiesTargetWave);
		int zombieUnlocks = UnlockCurrentStoreItems();
		MaxCoreUpgradesCurrentMode();
		UnlockAllChaptersCurrentMode();
		AddResources();
		CompleteKnownTutorials();
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
		SaveProfile("Full progress restored. Store unlocks: " + (classicUnlocks + zombieUnlocks));
	}

	private void UnlockModeProgressAndRestore(string modeId, int targetWave)
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		UnlockModeProgress(modeId, targetWave);
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
	}

	private void UnlockModeProgress(string modeId, int targetWave)
	{
		PlayModesManager playModes = Singleton<PlayModesManager>.instance;
		if (playModes.GetModeData(modeId) == null)
		{
			mStatus = "Unknown mode: " + modeId;
			return;
		}

		playModes.selectedMode = modeId;
		Profile profile = Singleton<Profile>.instance;
		int maxWave = Mathf.Clamp(targetWave, 1, profile.maxBaseWave);
		for (int i = 1; i < maxWave; i++)
		{
			profile.SetWaveLevel(i, 2);
		}
		profile.SetWaveLevel(maxWave, 1);
		profile.waveToBeat = maxWave;
	}

	private int PrepareWaveForMode(string modeId, int targetWave, bool exactProgress)
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		Singleton<PlayModesManager>.instance.selectedMode = modeId;
		int target = exactProgress ? SetExactProgressCurrentMode(targetWave) : PrepareWaveCurrentMode(targetWave);
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
		return target;
	}

	private void PrepareZombieBonusTest(int bonusIndex)
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		Singleton<PlayModesManager>.instance.selectedMode = "zombies";
		Profile profile = Singleton<Profile>.instance;
		int completedWave = Mathf.Clamp(bonusIndex * 5, 5, 50);
		for (int i = 1; i <= completedWave; i++)
		{
			profile.SetWaveLevel(i, 2);
		}
		if (completedWave < profile.maxBaseWave)
		{
			profile.SetWaveLevel(completedWave + 1, 1);
			profile.waveToBeat = completedWave + 1;
		}
		else
		{
			profile.waveToBeat = completedWave;
		}
		profile.PrepareBonusWaveForCompletedWave(completedWave);
		mWaveLabModeIndex = 1;
		mWaveLabWaveInput = profile.waveToBeat.ToString();
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
	}

	private int PrepareWaveCurrentMode(int targetWave)
	{
		Profile profile = Singleton<Profile>.instance;
		int target = Mathf.Clamp(targetWave, 1, profile.maxBaseWave);
		for (int i = 1; i < target; i++)
		{
			profile.SetWaveLevel(i, 2);
		}
		SetWaveLevelDirect(target, 1);
		profile.waveToBeat = target;
		mWaveLabWaveInput = target.ToString();
		return target;
	}

	private int SetExactProgressCurrentMode(int targetWave)
	{
		Profile profile = Singleton<Profile>.instance;
		int target = Mathf.Clamp(targetWave, 1, profile.maxBaseWave);
		for (int i = 1; i <= profile.maxBaseWave; i++)
		{
			int level = 0;
			if (i < target)
			{
				level = 2;
			}
			else if (i == target)
			{
				level = 1;
			}
			SetWaveLevelDirect(i, level);
		}
		profile.waveToBeat = target;
		mWaveLabWaveInput = target.ToString();
		return target;
	}

	private void SetWaveLevelDirect(int waveIndex, int level)
	{
		Profile profile = Singleton<Profile>.instance;
		profile.saveData.SetDictionaryValue("waves", "w" + waveIndex, Mathf.Clamp(level, 0, 99), profile.playModeSubSection);
	}

	private int ApplyBalancedTestPreset(string modeId)
	{
		return RunForMode(modeId, ApplyBalancedTestPresetCurrentMode);
	}

	private int ApplyBalancedTestPresetCurrentMode()
	{
		int count = 0;
		count += UnlockCurrentStoreItems();
		count += MaxCoreUpgradesCurrentMode();
		AddResources();
		return count;
	}

	private int ParseWaveInput(string input, int fallback)
	{
		int result = fallback;
		if (!int.TryParse(input, out result))
		{
			result = fallback;
		}
		return Mathf.Max(1, result);
	}

	private string waveLabMode
	{
		get
		{
			mWaveLabModeIndex = Mathf.Clamp(mWaveLabModeIndex, 0, kWaveLabModes.Length - 1);
			return kWaveLabModes[mWaveLabModeIndex];
		}
	}

	private int GetWaveLabFallbackWave()
	{
		return waveLabMode == "zombies" ? 31 : 50;
	}

	private void AddResources()
	{
		Profile profile = Singleton<Profile>.instance;
		profile.coins = Mathf.Max(profile.coins, kCoinsGrant);
		profile.gems = Mathf.Max(profile.gems, kGemsGrant);
		profile.pachinkoBalls = Mathf.Max(profile.pachinkoBalls, kPachinkoGrant);
		profile.freeBoosterPacks = Mathf.Max(profile.freeBoosterPacks, 25);
	}

	private void AddCoins(int amount)
	{
		Singleton<Profile>.instance.coins = ClampResource(Singleton<Profile>.instance.coins + amount);
		SaveProfile("Coins added: " + amount);
	}

	private void AddGems(int amount)
	{
		Singleton<Profile>.instance.gems = ClampResource(Singleton<Profile>.instance.gems + amount);
		SaveProfile("Gems added: " + amount);
	}

	private void AddPachinko(int amount)
	{
		Singleton<Profile>.instance.pachinkoBalls = ClampResource(Singleton<Profile>.instance.pachinkoBalls + amount);
		SaveProfile("Pachinko added: " + amount);
	}

	private void AddFreeBoosters(int amount)
	{
		Singleton<Profile>.instance.freeBoosterPacks = ClampResource(Singleton<Profile>.instance.freeBoosterPacks + amount);
		SaveProfile("Free booster packs added: " + amount);
	}

	private void SetCoinsMax()
	{
		Singleton<Profile>.instance.coins = kMaxResource;
		SaveProfile("Coins set to max");
	}

	private void SetGemsMax()
	{
		Singleton<Profile>.instance.gems = kMaxResource;
		SaveProfile("Gems set to max");
	}

	private void SetPachinkoMax()
	{
		Singleton<Profile>.instance.pachinkoBalls = kMaxResource;
		SaveProfile("Pachinko set to max");
	}

	private void SetFreeBoostersMax()
	{
		Singleton<Profile>.instance.freeBoosterPacks = kMaxResource;
		SaveProfile("Free booster packs set to max");
	}

	private int ClampResource(int value)
	{
		return Mathf.Clamp(value, 0, kMaxResource);
	}

	private int AddAllPotions(int amount)
	{
		int count = 0;
		foreach (string id in Singleton<PotionsDatabase>.instance.allIDs)
		{
			int value = Singleton<Profile>.instance.GetNumPotions(id);
			Singleton<Profile>.instance.SetNumPotions(id, ClampResource(value + amount));
			count++;
		}
		return count;
	}

	private int SetAllPotions(int value)
	{
		int count = 0;
		foreach (string id in Singleton<PotionsDatabase>.instance.allIDs)
		{
			Singleton<Profile>.instance.SetNumPotions(id, value);
			count++;
		}
		return count;
	}

	private int AddStoreCharms(int amount)
	{
		int count = 0;
		foreach (string id in Singleton<CharmsDatabase>.instance.allIDs)
		{
			if (!IsStoreCharm(id))
			{
				continue;
			}
			int value = Singleton<Profile>.instance.GetNumCharms(id);
			Singleton<Profile>.instance.SetNumCharms(id, ClampResource(value + amount));
			count++;
		}
		return count;
	}

	private int SetStoreCharms(int value)
	{
		int count = 0;
		foreach (string id in Singleton<CharmsDatabase>.instance.allIDs)
		{
			if (!IsStoreCharm(id))
			{
				continue;
			}
			Singleton<Profile>.instance.SetNumCharms(id, value);
			count++;
		}
		return count;
	}

	private bool IsStoreCharm(string id)
	{
		return Singleton<CharmsDatabase>.instance.GetAttribute(id, "store").ToLower() == "yes";
	}

	private int UnlockStoreForMode(string modeId)
	{
		return RunForMode(modeId, UnlockCurrentStoreItems);
	}

	private int UnlockCurrentStoreItems()
	{
		int count = 0;
		count += UnlockHelpers();
		count += UnlockAbilities();
		return count;
	}

	private int UnlockHelpers()
	{
		Profile profile = Singleton<Profile>.instance;
		Dictionary<string, int> beforeLevels = new Dictionary<string, int>();
		string[] allIDs = Singleton<HelpersDatabase>.instance.allIDs;
		foreach (string id in allIDs)
		{
			beforeLevels[id] = profile.GetHelperLevel(id);
		}

		List<StoreData.Item> items = new List<StoreData.Item>();
		StoreAvailability_Helpers.Get(items);

		foreach (StoreData.Item item in items)
		{
			if (!CanForceUnlock(item))
			{
				continue;
			}
			if (profile.GetHelperLevel(item.id) == 0)
			{
				profile.SetHelperLevel(item.id, 1, true);
			}
		}

		int count = 0;
		foreach (string id in allIDs)
		{
			if (beforeLevels[id] == 0 && profile.GetHelperLevel(id) > 0)
			{
				count++;
			}
		}
		return count;
	}

	private int UnlockAbilities()
	{
		Profile profile = Singleton<Profile>.instance;
		Dictionary<string, int> beforeLevels = new Dictionary<string, int>();
		string[] allIDs = Singleton<AbilitiesDatabase>.instance.allIDs;
		foreach (string id in allIDs)
		{
			beforeLevels[id] = profile.GetAbilityLevel(id);
		}

		List<StoreData.Item> items = new List<StoreData.Item>();
		StoreAvailability_Abilities.GetAll(items);

		foreach (StoreData.Item item in items)
		{
			if (!CanForceUnlock(item))
			{
				continue;
			}
			if (profile.GetAbilityLevel(item.id) == 0)
			{
				profile.SetAbilityLevel(item.id, 1);
				AutoSelectAbility(item.id);
			}
		}

		int count = 0;
		foreach (string id in allIDs)
		{
			if (beforeLevels[id] == 0 && profile.GetAbilityLevel(id) > 0)
			{
				count++;
			}
		}
		return count;
	}

	private bool CanForceUnlock(StoreData.Item item)
	{
		return item != null && !string.IsNullOrEmpty(item.id) && !item.locked && item.unlockAtWave < 999;
	}

	private int UnlockSpecialHelpersForMode(string modeId)
	{
		return RunForMode(modeId, UnlockSpecialHelpersCurrentMode);
	}

	private int UnlockSpecialHelpersCurrentMode()
	{
		Profile profile = Singleton<Profile>.instance;
		int count = 0;
		foreach (string id in Singleton<HelpersDatabase>.instance.allIDs)
		{
			if (!CanForceUnlockSpecialHelper(id))
			{
				continue;
			}
			if (profile.GetHelperLevel(id) == 0)
			{
				profile.SetHelperLevel(id, 1, true);
				count++;
			}
		}
		return count;
	}

	private bool CanForceUnlockSpecialHelper(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return false;
		}
		SDFTreeNode node = SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Helpers/" + id);
		if (node == null)
		{
			return false;
		}
		if (GetRegistryBool(node, "hideInStore") || GetRegistryBool(node, "hidden"))
		{
			return false;
		}
		if (node.hasAttribute("levelMatchOtherHelper"))
		{
			return false;
		}
		int waveToUnlock = GetRegistryInt(node, "waveToUnlock", 0);
		if (waveToUnlock < 999)
		{
			return false;
		}
		return node.hasAttribute("availableAtWave") || node.hasAttribute("specialUnlockText") || node.hasAttribute("HUDIcon");
	}

	private bool GetRegistryBool(SDFTreeNode node, string attribute)
	{
		if (node == null || !node.hasAttribute(attribute))
		{
			return false;
		}
		return SUILayoutConv.GetBool(node[attribute]);
	}

	private int GetRegistryInt(SDFTreeNode node, string attribute, int fallback)
	{
		if (node == null || !node.hasAttribute(attribute))
		{
			return fallback;
		}
		int value;
		if (int.TryParse(node[attribute], out value))
		{
			return value;
		}
		return fallback;
	}

	private int SetCurrentModeStoreItemsToLevel(bool maxLevel)
	{
		int count = 0;
		count += SetVisibleHelpersToLevel(maxLevel);
		count += SetVisibleAbilitiesToLevel(maxLevel);
		return count;
	}

	private int SetVisibleHelpersToLevel(bool maxLevel)
	{
		List<StoreData.Item> items = new List<StoreData.Item>();
		StoreAvailability_Helpers.Get(items);
		int count = 0;
		foreach (StoreData.Item item in items)
		{
			if (!CanForceUnlock(item))
			{
				continue;
			}
			int target = maxLevel ? Singleton<HelpersDatabase>.instance.GetMaxLevel(item.id) : 1;
			if (Singleton<Profile>.instance.GetHelperLevel(item.id) != target)
			{
				Singleton<Profile>.instance.SetHelperLevel(item.id, target, true);
				count++;
			}
		}
		return count;
	}

	private int SetVisibleAbilitiesToLevel(bool maxLevel)
	{
		List<StoreData.Item> items = new List<StoreData.Item>();
		StoreAvailability_Abilities.GetAll(items);
		int count = 0;
		foreach (StoreData.Item item in items)
		{
			if (!CanForceUnlock(item))
			{
				continue;
			}
			int target = maxLevel ? GetAbilityMaxLevel(item.id) : 1;
			if (Singleton<Profile>.instance.GetAbilityLevel(item.id) != target)
			{
				Singleton<Profile>.instance.SetAbilityLevel(item.id, target);
				AutoSelectAbility(item.id);
				count++;
			}
		}
		return count;
	}

	private int AutoFillSelectedHelpers()
	{
		Profile profile = Singleton<Profile>.instance;
		List<string> selected = profile.GetSelectedHelpers();
		List<StoreData.Item> items = new List<StoreData.Item>();
		StoreAvailability_Helpers.Get(items);
		foreach (StoreData.Item item in items)
		{
			if (selected.Count >= profile.maxSelectedHelpers)
			{
				break;
			}
			if (!CanForceUnlock(item) || selected.Contains(item.id))
			{
				continue;
			}
			if (profile.GetHelperLevel(item.id) <= 0)
			{
				profile.SetHelperLevel(item.id, 1, false);
			}
			selected.Add(item.id);
		}
		profile.SetSelectedHelpers(selected);
		return selected.Count;
	}

	private int AutoFillSelectedAbilities()
	{
		Profile profile = Singleton<Profile>.instance;
		List<string> selected = profile.GetSelectedAbilities();
		List<StoreData.Item> items = new List<StoreData.Item>();
		StoreAvailability_Abilities.GetAll(items);
		foreach (StoreData.Item item in items)
		{
			if (selected.Count >= profile.maxSelectedAbilities)
			{
				break;
			}
			if (!CanForceUnlock(item) || selected.Contains(item.id))
			{
				continue;
			}
			if (profile.GetAbilityLevel(item.id) <= 0)
			{
				profile.SetAbilityLevel(item.id, 1);
			}
			selected.Add(item.id);
		}
		profile.SetSelectedAbilities(selected);
		return selected.Count;
	}

	private void AutoSelectAbility(string abilityId)
	{
		Profile profile = Singleton<Profile>.instance;
		List<string> selected = profile.GetSelectedAbilities();
		if (selected.Count >= profile.maxSelectedAbilities || selected.Contains(abilityId))
		{
			return;
		}
		selected.Add(abilityId);
		profile.SetSelectedAbilities(selected);
	}

	private int MaxCoreUpgradesForMode(string modeId)
	{
		return RunForMode(modeId, MaxCoreUpgradesCurrentMode);
	}

	private int MaxCoreUpgradesCurrentMode()
	{
		Profile profile = Singleton<Profile>.instance;
		int count = 0;
		int target = GetNodeChildCount("Registry/Heroes/" + profile.heroID);
		if (profile.heroLevel < target)
		{
			profile.heroLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/Weapons/" + profile.swordID);
		if (profile.swordLevel < target)
		{
			profile.swordLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/Weapons/" + profile.bowID);
		if (profile.bowLevel < target)
		{
			profile.bowLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/Base");
		if (profile.baseLevel < target)
		{
			profile.baseLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/Smithy");
		if (profile.initialSmithyLevel < target)
		{
			profile.initialSmithyLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/Bell");
		if (profile.bellLevel < target)
		{
			profile.bellLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/VillageArchers");
		if (profile.archerLevel < target)
		{
			profile.archerLevel = target;
			count++;
		}
		target = GetNodeChildCount("Registry/Undeath");
		if (profile.undeathLevel < target)
		{
			profile.undeathLevel = target;
			count++;
		}
		return count;
	}

	private int GetNodeChildCount(string path)
	{
		SDFTreeNode node = SingletonMonoBehaviour<ResourcesManager>.instance.Open(path);
		if (node == null)
		{
			return 1;
		}
		return Mathf.Max(1, node.childCount);
	}

	private int GetAbilityMaxLevel(string id)
	{
		return GetNodeChildCount("Registry/Abilities/" + id);
	}

	private int UnlockAllChaptersForBothModes()
	{
		return RunForMode("classic", UnlockAllChaptersCurrentMode) + RunForMode("zombies", UnlockAllChaptersCurrentMode);
	}

	private int UnlockAllChaptersCurrentMode()
	{
		int count = 0;
		foreach (string id in Singleton<ChaptersDatabase>.instance.allChapterIDs)
		{
			if (!Singleton<Profile>.instance.IsChapterAvailable(id))
			{
				count++;
			}
			Singleton<Profile>.instance.SetChapterAvailable(id, true);
		}
		return count;
	}

	private void CompleteKnownTutorials()
	{
		for (int i = 0; i < kKnownTutorials.GetLength(0); i++)
		{
			Singleton<Profile>.instance.SetTutorialDone(kKnownTutorials[i, 0], kKnownTutorials[i, 1]);
		}
		Singleton<Profile>.instance.hasWonPachinkoBefore = true;
		Singleton<Profile>.instance.zombieModeUnlockedMessageShown = true;
	}

	private int RunForMode(string modeId, ModeAction callback)
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		Singleton<PlayModesManager>.instance.selectedMode = modeId;
		int result = callback();
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
		return result;
	}

	private void ApplyPerformancePresetFromDev(int preset)
	{
		int quality;
		int shadow;
		int aa;
		int fps;
		bool vSync;
		PerformanceSettings.ApplyPreset(preset, out quality, out shadow, out aa, out fps, out vSync);
		PerformanceSettings.SavePrefs(quality, shadow, aa, fps, vSync);
		PlayerPrefs.Save();
		mStatus = "Performance preset applied: " + PerformanceSettings.GetQualityLabel(quality);
	}

	private void SetFPSFromDev(int fpsIndex)
	{
		int quality = PlayerPrefs.GetInt(PerformanceSettings.PREF_QUALITY, 2);
		int shadow = PlayerPrefs.GetInt(PerformanceSettings.PREF_SHADOW, 2);
		int aa = PlayerPrefs.GetInt(PerformanceSettings.PREF_AA, 0);
		bool vSync = PlayerPrefs.GetInt(PerformanceSettings.PREF_VSYNC, 0) == 1;
		PerformanceSettings.NormalizeFramePrefs(ref fpsIndex, ref vSync);
		PerformanceSettings.ApplySettings(quality, shadow, aa, fpsIndex, vSync);
		PerformanceSettings.SavePrefs(quality, shadow, aa, fpsIndex, vSync);
		PlayerPrefs.Save();
		mStatus = "FPS limit applied: " + PerformanceSettings.GetFPSLabel(fpsIndex);
	}

	private void SetVSyncFromDev(bool enabled)
	{
		int quality = PlayerPrefs.GetInt(PerformanceSettings.PREF_QUALITY, 2);
		int shadow = PlayerPrefs.GetInt(PerformanceSettings.PREF_SHADOW, 2);
		int aa = PlayerPrefs.GetInt(PerformanceSettings.PREF_AA, 0);
		int fps = PerformanceSettings.GetSavedFPSLimitIndex();
		bool vSync = enabled;
		PerformanceSettings.NormalizeFramePrefs(ref fps, ref vSync);
		PerformanceSettings.ApplySettings(quality, shadow, aa, fps, vSync);
		PerformanceSettings.SavePrefs(quality, shadow, aa, fps, vSync);
		PlayerPrefs.Save();
		mStatus = "VSync " + (vSync ? "enabled" : "disabled");
	}

	private string GetBuildCheckSummary()
	{
		return "SVZ Build Check\n" + PerformanceSettings.GetDebugSummary() +
			"\nDevice: " + SVZDeviceCompatibility.GetDiagnosticSummary() +
			"\nProfile: " + GetProfileSummary() +
			"\nSave: " + Singleton<Profile>.instance.saveFilePath +
			"\nCheat enabled: " + IsEnabled +
			"\nRelease builds should hide this panel automatically.";
	}

	private string GetZombieWaveLoadReport()
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		Singleton<PlayModesManager>.instance.selectedMode = "zombies";
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("Zombie wave load report 50-100");
		builder.AppendLine("Score is spawn count + short delays + boss pressure. HIGH means test on Android.");
		int maxWave = Mathf.Min(100, Singleton<Profile>.instance.maxBaseWave);
		for (int wave = 50; wave <= maxWave; wave++)
		{
			SDFTreeNode data = WaveManager.GetWaveData(wave);
			if (data == null)
			{
				builder.AppendLine("ZWave" + wave.ToString("000") + ": missing");
				continue;
			}
			SDFTreeNode commands = data.to("Commands");
			if (commands == null)
			{
				builder.AppendLine("ZWave" + wave.ToString("000") + ": missing Commands");
				continue;
			}
			SDFTreeNode rewards = data.to("rewards");
			int rewardCount = (rewards != null) ? rewards.attributeCount : 0;
			int spawns = 0;
			int delays = 0;
			int shortDelays = 0;
			int bossPressure = 0;
			for (int i = 0; i < commands.attributeCount; i++)
			{
				string command = commands[i];
				if (string.IsNullOrEmpty(command) || command[0] == '@' || command[0] == '(')
				{
					continue;
				}
				if (command.IndexOf(",") >= 0)
				{
					delays++;
					if (command == "1,1" || command == "1,2" || command == "2,2")
					{
						shortDelays++;
					}
					continue;
				}
				spawns++;
				string lower = command.ToLower();
				if (lower.IndexOf("hero") >= 0 || lower.IndexOf("nobunaga") >= 0 || lower.IndexOf("takeda") >= 0 || lower.IndexOf("cart") >= 0)
				{
					bossPressure++;
				}
			}
			int score = spawns + shortDelays + bossPressure * 3;
			builder.AppendLine("ZWave" + wave.ToString("000") + ": spawns " + spawns + ", delays " + delays + ", short " + shortDelays + ", elite " + bossPressure + ", rewards " + rewardCount + ", score " + score + (score >= 70 ? " HIGH" : score >= 55 ? " MED" : " OK"));
		}
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
		return builder.ToString();
	}

	private string GetZombieEndgameCheckpointReport()
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		Singleton<PlayModesManager>.instance.selectedMode = "zombies";
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("Zombie endgame checkpoint report");
		int[] waves = { 75, 80, 85, 90, 95, 100 };
		for (int i = 0; i < waves.Length; i++)
		{
			int wave = waves[i];
			SDFTreeNode data = WaveManager.GetWaveData(wave);
			if (data == null)
			{
				builder.AppendLine("ZWave" + wave.ToString("000") + ": missing");
				continue;
			}
			SDFTreeNode commands = data.to("Commands");
			SDFTreeNode rewards = data.to("rewards");
			int bosses = 0;
			int spawns = 0;
			bool hasBossAlert = false;
			if (commands != null)
			{
				for (int j = 0; j < commands.attributeCount; j++)
				{
					string command = commands[j];
					if (string.IsNullOrEmpty(command))
					{
						continue;
					}
					if (command == "@bossalert")
					{
						hasBossAlert = true;
						continue;
					}
					if (command[0] == '@' || command[0] == '(' || command.IndexOf(",") >= 0)
					{
						continue;
					}
					spawns++;
					string lower = command.ToLower();
					if (lower.IndexOf("boss") >= 0 || lower.IndexOf("hero") >= 0)
					{
						bosses++;
					}
				}
			}
			builder.AppendLine("ZWave" + wave.ToString("000") + ": bossAlert " + hasBossAlert + ", bosses " + bosses + ", spawns " + spawns + ", rewards " + ((rewards != null) ? rewards.attributeCount : 0));
		}
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
		return builder.ToString();
	}

	private string GetProfileSummary()
	{
		Profile profile = Singleton<Profile>.instance;
		string mode = Singleton<PlayModesManager>.instance.selectedMode;
		return "Slot " + (profile.currentSlot + 1) + " | Mode " + mode + " | Wave " + profile.waveToBeat + "/" + profile.maxBaseWave +
			" | Coins " + profile.coins + " | Gems " + profile.gems + " | Pachinko " + profile.pachinkoBalls +
			" | Helpers " + profile.GetSelectedHelpers().Count + "/" + profile.maxSelectedHelpers +
			" | Abilities " + profile.GetSelectedAbilities().Count + "/" + profile.maxSelectedAbilities;
	}

	private string GetSaveFilesSummary(string path)
	{
		string text = "Main: " + FormatSaveFile(path);
		string[] backupPaths = SDFTree.GetBackupPaths(path);
		for (int i = 0; i < backupPaths.Length; i++)
		{
			text += "\nBackup " + (i + 1) + ": " + FormatSaveFile(backupPaths[i]);
		}
		text += "\nManual: " + FormatSaveFile(SDFTree.GetManualBackupPath(path));
		text += "\nEmergency: " + FormatSaveFile(SDFTree.GetEmergencyBackupPath(path));
		text += "\nLegacy prev: " + FormatSaveFile(Profile.prevSaveFilePath);
		return text;
	}

	private string FormatSaveFile(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return "missing";
		}
		FileInfo fileInfo = new FileInfo(path);
		return fileInfo.Length + " bytes | " + fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
	}

	private string GetWaveLabStatus()
	{
		string activeMode = Singleton<PlayModesManager>.instance.selectedMode;
		return "Slot " + (Singleton<Profile>.instance.currentSlot + 1) + " | Active " + activeMode + " | " + GetModeWaveStatus(activeMode) +
			"\nTarget " + waveLabMode + " | " + GetModeWaveStatus(waveLabMode);
	}

	private string GetModeWaveStatus(string modeId)
	{
		string previousMode = Singleton<PlayModesManager>.instance.selectedMode;
		Singleton<PlayModesManager>.instance.selectedMode = modeId;
		Profile profile = Singleton<Profile>.instance;
		string status = "waveToBeat " + profile.waveToBeat + " | highest " + profile.highestUnlockedWave + " | max " + profile.maxBaseWave;
		Singleton<PlayModesManager>.instance.selectedMode = previousMode;
		return status;
	}

	private void SetSlot(int slot)
	{
		Singleton<Profile>.instance.currentSlot = slot;
		mConfirmReset = false;
		SaveProfile("Active slot set to " + (slot + 1));
	}

	private void SaveProfile(string status)
	{
		Singleton<Profile>.instance.Save();
		Singleton<Profile>.instance.Update();
		PlayerPrefs.Save();
		mStatus = status;
	}

	public static void Close()
	{
		if (sInstance != null)
		{
			Destroy(sInstance.gameObject);
			sInstance = null;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void InitHotkeyListener()
	{
		GameObject go = new GameObject("[DevCheatHotkeyListener]");
		DontDestroyOnLoad(go);
		go.AddComponent<DevCheatHotkeyListener>();
	}
}

public class DevCheatHotkeyListener : MonoBehaviour
{
	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.BackQuote))
		{
			if (DevCheatOverlay.IsOpen)
			{
				DevCheatOverlay.Close();
			}
			else
			{
				DevCheatOverlay.Open();
			}
		}
	}
}
