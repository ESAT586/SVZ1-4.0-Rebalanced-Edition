using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Next-Level" Responsive Visual GUI for Samurai vs Zombies 1v1 Multiplayer
/// Provides universal aspect-ratio scaling (1280x720 virtual canvas), MainMenu Quick PvP Button,
/// Complete 48-Unit Scrollable Catalog, Cross-Faction Mixed Armies, Side Selection,
/// Real-time Tactical Minimap Radar, and In-Game Touch/Key Summoning for both players.
/// </summary>
public class SVZMultiplayerUI : MonoBehaviour
{
    private static SVZMultiplayerUI sInstance;

    private bool mShowLobby = false;
    private int mSelectedTab = 0; // 0 = Ayarlar, 1 = Ordu & Deste, 2 = Savas Baslat / Online
    private int mSelectedP1Slot = 0; // Slot currently being edited in P1 deck (0..4)
    private int mSelectedP2Slot = -1; // Slot currently being edited in P2 deck (0..4)
    private int mSelectedP1SpellSlot = -1; // Spell slot being edited for P1 (0..1)
    private int mSelectedP2SpellSlot = -1; // Spell slot being edited for P2 (0..1)
    private int mCatalogFilter = 0; // 0 = Tumu (48), 1 = Samuray (20), 2 = Zombi (28), 3 = Buyuler (13)
    private Vector2 mCatalogScrollPos = Vector2.zero;
    private string mDeckWarningMessage = string.Empty;
    private float mDeckWarningTimer = 0f;

    private bool mShowQuickChat = false;
    private string mChatMessageInput = "";
    private bool mInCustomLobbyRoom = false;
    private string mJoinIpInput = "127.0.0.1";
    private int mJoinPortInput = 7777;
    private string mLobbyChatInput = "";
    private Vector2 mLobbyChatScrollPos = Vector2.zero;
    private Matrix4x4 mPrevMatrix;

    // Fast Texture Cache
    private Texture2D mTexGreen;
    private Texture2D mTexRed;
    private Texture2D mTexDarkBg;
    private Texture2D mTexPanelBg;
    private Texture2D mTexCardBg;
    private Dictionary<string, Texture2D> mUnitIconCache = new Dictionary<string, Texture2D>();

    private Texture2D GetUnitIconTexture(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return null;
        Texture2D tex;
        if (mUnitIconCache.TryGetValue(unitId, out tex) && tex != null)
        {
            return tex;
        }
        string path = SVZMultiplayerManager.GetUnitIconPath(unitId);
        if (!string.IsNullOrEmpty(path))
        {
            tex = SUISprite.LoadOverrideTexture(path);
            if (tex == null)
            {
                tex = Resources.Load<Texture2D>(path);
            }
            if (tex == null)
            {
                tex = Resources.Load<Texture2D>(path.ToLower());
            }
            if (tex != null)
            {
                mUnitIconCache[unitId] = tex;
            }
            return tex;
        }
        return null;
    }

    private Texture2D GetSpellIconTexture(string spellId)
    {
        if (string.IsNullOrEmpty(spellId)) return null;
        string cacheKey = "SpellIcon_" + spellId;
        Texture2D tex;
        if (mUnitIconCache.TryGetValue(cacheKey, out tex) && tex != null)
        {
            return tex;
        }
        string path = SVZMultiplayerManager.GetSpellIconPath(spellId);
        if (!string.IsNullOrEmpty(path))
        {
            tex = Resources.Load<Texture2D>(path);
            if (tex != null) mUnitIconCache[cacheKey] = tex;
            return tex;
        }
        return null;
    }

    private Texture2D GetHeroIconTexture(SVZMultiplayerManager.HeroType heroType)
    {
        string key = (heroType == SVZMultiplayerManager.HeroType.Samurai) ? "HeroIcon_Samurai_Unique" : "HeroIcon_Zombie_Unique";
        Texture2D tex;
        if (mUnitIconCache.TryGetValue(key, out tex) && tex != null) return tex;

        if (heroType == SVZMultiplayerManager.HeroType.Samurai)
        {
            tex = Resources.Load<Texture2D>("Sprites/Icons/hero01");
            if (tex == null) tex = Resources.Load<Texture2D>("sprites/icons/hero01");
        }
        else
        {
            tex = Resources.Load<Texture2D>("Sprites/Icons/hero_zombie01");
            if (tex == null) tex = Resources.Load<Texture2D>("sprites/icons/hero_zombie01");
        }
        if (tex != null) mUnitIconCache[key] = tex;
        return tex;
    }

    private bool ShouldHideP2PicksFromLocal()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (mpMgr == null || !mpMgr.BlindPick) return false;
        if (mpMgr.IsSamePC || mpMgr.P2AutoBot || (net != null && !net.IsConnected)) return false;
        return (net != null && net.IsHost);
    }

    private bool ShouldHideP1PicksFromLocal()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        SVZNetworkManager net = SVZNetworkManager.Instance;
        if (mpMgr == null || !mpMgr.BlindPick) return false;
        if (mpMgr.IsSamePC || (net != null && !net.IsConnected)) return false;
        return (net != null && !net.IsHost && net.Role == SVZNetworkManager.NetworkRole.Client);
    }

    private void SendLobbyChatMessage()
    {
        if (string.IsNullOrEmpty(mLobbyChatInput.Trim())) return;
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        SVZNetworkManager net = SVZNetworkManager.Instance;
        string senderName = (net != null && !net.IsHost && net.Role == SVZNetworkManager.NetworkRole.Client) ? mpMgr.Player2Name : mpMgr.Player1Name;
        string msg = mLobbyChatInput.Trim();
        SVZMultiplayerManager.AddChatMessage(senderName, msg);
        if (net != null && net.IsConnected)
        {
            net.SendPacket("CHAT", senderName + ":" + msg);
        }
        mLobbyChatInput = "";
        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
    }

    public static SVZMultiplayerUI Instance
    {
        get
        {
            if (sInstance == null) EnsureExists();
            return sInstance;
        }
    }

    public bool ShowLobby { get { return mShowLobby; } set { mShowLobby = value; } }

    public static bool IsLobbyOpen
    {
        get { return sInstance != null && sInstance.mShowLobby; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInit()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (sInstance == null)
        {
            GameObject go = new GameObject("[SVZMultiplayerUI]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<SVZMultiplayerUI>();
        }
        SVZMultiplayerManager.EnsureExists();
    }

    public static void ToggleLobby()
    {
        EnsureExists();
        if (sInstance != null)
        {
            sInstance.mShowLobby = !sInstance.mShowLobby;
            if (sInstance.mShowLobby)
            {
                SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.8f);
            }
            else
            {
                SVZMultiplayerManager.PlaySFX("UI_Window_Close_01", 0.8f);
            }
        }
    }

    private void Awake()
    {
        if (sInstance != null && sInstance != this)
        {
            Destroy(gameObject);
            return;
        }
        sInstance = this;
        DontDestroyOnLoad(gameObject);
        InitTextures();
    }

    private void OnDestroy()
    {
        if (sInstance == this) sInstance = null;
    }

    private void InitTextures()
    {
        mTexGreen = MakeSolidTex(new Color(0.2f, 0.85f, 0.35f, 1f));
        mTexRed = MakeSolidTex(new Color(0.9f, 0.25f, 0.25f, 1f));
        mTexDarkBg = MakeSolidTex(new Color(0.06f, 0.07f, 0.10f, 0.94f));
        mTexPanelBg = MakeSolidTex(new Color(0.11f, 0.13f, 0.19f, 0.95f));
        mTexCardBg = MakeSolidTex(new Color(0.18f, 0.22f, 0.30f, 0.95f));
    }

    private Texture2D MakeSolidTex(Color col)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }

    private void Update()
    {
        if (mDeckWarningTimer > 0f) mDeckWarningTimer -= Time.deltaTime;

        // Toggle lobby with [M] key
        if (Input.GetKeyDown(KeyCode.M))
        {
            mShowLobby = !mShowLobby;
            if (mShowLobby) SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.8f);
            else SVZMultiplayerManager.PlaySFX("UI_Window_Close_01", 0.8f);
        }

        // Quick chat toggle with [T] key
        if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                mShowQuickChat = !mShowQuickChat;
            }
        }

        if (mShowLobby && Input.GetKeyDown(KeyCode.Escape))
        {
            mShowLobby = false;
            SVZMultiplayerManager.PlaySFX("UI_Window_Close_01", 0.8f);
        }
    }

    #region Universal Responsive GUI Scaler

    private Font mPrevLblFont;
    private Font mPrevBtnFont;

    private void BeginResponsiveGUI(float virtualW = 1280f, float virtualH = 720f)
    {
        float scaleX = (float)Screen.width / virtualW;
        float scaleY = (float)Screen.height / virtualH;
        float scale = Mathf.Min(scaleX, scaleY);

        float offsetX = (Screen.width - virtualW * scale) * 0.5f;
        float offsetY = (Screen.height - virtualH * scale) * 0.5f;

        mPrevMatrix = GUI.matrix;
        mPrevLblFont = GUI.skin.label.font;
        mPrevBtnFont = GUI.skin.button.font;
        Font dyn = LocalizationManager.GetDynamicOSFont(15);
        if (dyn != null)
        {
            GUI.skin.label.font = dyn;
            GUI.skin.button.font = dyn;
        }
        GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
    }

    private void EndResponsiveGUI()
    {
        GUI.skin.label.font = mPrevLblFont;
        GUI.skin.button.font = mPrevBtnFont;
        GUI.matrix = mPrevMatrix;
    }

    #endregion

    private bool mMinimizeP2Dock = false;
    private Texture2D mLoadingTimerTex;

    private void DrawLoadingScreenOverlay()
    {
        GUI.color = new Color(0.02f, 0.02f, 0.04f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (mLoadingTimerTex == null)
        {
            mLoadingTimerTex = Resources.Load<Texture2D>("Sprites/Localized/default/loading_timer");
            if (mLoadingTimerTex == null)
            {
                mLoadingTimerTex = Resources.Load<Texture2D>("Sprites/Localized/loading_timer");
            }
        }

        BeginResponsiveGUI();
        if (mLoadingTimerTex != null)
        {
            float tw = mLoadingTimerTex.width;
            float th = mLoadingTimerTex.height;
            GUI.DrawTexture(new Rect(640f - tw * 0.5f, 330f - th * 0.5f, tw, th), mLoadingTimerTex, ScaleMode.ScaleToFit);
        }

        GUIStyle loadStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 18,
            richText = true
        };
        GUI.Label(new Rect(340f, 440f, 600f, 36f), "<color=#FFDD44><b>SAVAS ALANI YUKLENIYOR...</b></color>", loadStyle);
        EndResponsiveGUI();
    }

    private void OnGUI()
    {
        GUI.depth = -20000;
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;

        if (mpMgr != null && mpMgr.IsLoadingScene)
        {
            DrawLoadingScreenOverlay();
            return;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        bool isInGameScene = activeScene.StartsWith("InGame") || activeScene.StartsWith("SelectWaveMultiplayer") || activeScene.StartsWith("MenuWith3DLevel_Multiplayer");
        bool isMainMenu = string.Equals(activeScene, "MainMenu", StringComparison.OrdinalIgnoreCase)
                       || activeScene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0
                       || WeakGlobalSceneBehavior<MainMenuImpl>.instance != null;

        // 1. Draw In-Game PvP Battle HUD ONLY if in an actual battle scene and match is active/over
        if (isInGameScene && mpMgr != null && (mpMgr.IsActive || mpMgr.IsMatchOver))
        {
            BeginResponsiveGUI();
            DrawInGamePvPHUD();

            if (mpMgr.IsMatchOver)
            {
                DrawMatchOverOverlay();
            }
            EndResponsiveGUI();
        }

        // 2. Draw Main Menu Dedicated PvP Button ONLY when on MainMenu and no dialog (Options/Settings/DevTools) is open
        if (isMainMenu && !mShowLobby && !MainMenuImpl.IsAnyMainMenuDialogOpen && !DevCheatOverlay.IsOpen)
        {
            DrawMainMenuPvPButton();
        }

        // 3. Draw Lobby Window if opened
        if (mShowLobby)
        {
            // Dim background
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            BeginResponsiveGUI();
            DrawLobbyWindow();
            EndResponsiveGUI();
        }
    }

    #region Main Menu PvP Quick Access Button

    private void DrawMainMenuPvPButton()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        // Calculate dynamic dimensions for clean placement well above Options
        float btnW = Mathf.Clamp(sw * 0.25f, 240f, 340f);
        float btnH = Mathf.Clamp(sh * 0.085f, 54f, 72f);
        // Options in SVZ start screen is at X ~ 17.5% from left, Y ~ 76.8% from top
        float btnX = Mathf.Max(25f, (0.175f * sw) - (btnW * 0.5f));
        float btnY = (0.768f * sh) - btnH - (sh * 0.085f);

        Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

        // High-contrast, rich themed dark background
        GUI.color = new Color(0.08f, 0.10f, 0.16f, 0.94f);
        GUI.DrawTexture(btnRect, Texture2D.whiteTexture);

        // Radiant Gold Border
        GUI.color = new Color(1.0f, 0.82f, 0.25f, 1f);
        GUI.DrawTexture(new Rect(btnX, btnY, btnW, 3), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, btnY + btnH - 3, btnW, 3), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX, btnY, 3, btnH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(btnX + btnW - 3, btnY, 3, btnH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle btnStyle = new GUIStyle(GUIStyle.none)
        {
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        int titleSize = Mathf.Clamp((int)(sh * 0.024f), 13, 18);
        int subtitleSize = Mathf.Clamp((int)(sh * 0.016f), 10, 13);

        string label = string.Format(
            "<b><size={0}><color=#FFDD44>[PvP] COK OYUNCULU MOD</color></size></b>\n<size={1}><color=#DDEEFF>1v1 Meydan Savasi & Birlik Arenasi [M]</color></size>",
            titleSize, subtitleSize);

        if (GUI.Button(btnRect, label, btnStyle))
        {
            mShowLobby = true;
            SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.9f);
        }
    }

    #endregion

    #region In-Game PvP HUD & Tactical Radar

    private void DrawInGamePvPHUD()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        float barWidth = 320f;
        float barHeight = 26f;

        // --- TOP GATE HEALTH BARS ---
        float g1Pct = Mathf.Clamp01(mpMgr.Gate1Health / Mathf.Max(1f, mpMgr.Gate1MaxHealth));
        GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
        GUI.DrawTexture(new Rect(240, 10, barWidth, barHeight), Texture2D.whiteTexture);
        GUI.color = (g1Pct > 0.3f) ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.3f, 0.2f);
        GUI.DrawTexture(new Rect(242, 12, (barWidth - 4) * g1Pct, barHeight - 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle hpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11 };
        string p1SideLabel = (mpMgr.P1Side == SVZMultiplayerManager.BattlefieldSide.LeftGate) ? "1. Oyuncu (Sol)" : "1. Oyuncu (Sag)";
        GUI.Label(new Rect(240, 10, barWidth, barHeight), string.Format("{0} (Lv.{1}): {2:0} HP", p1SideLabel, mpMgr.P1TroopLevel, mpMgr.Gate1Health), hpStyle);

        // --- TOP-LEFT MATCH TIMER & LIVE PING DISPLAY ---
        int mins = Mathf.FloorToInt(mpMgr.MatchTimer / 60f);
        int secs = Mathf.FloorToInt(mpMgr.MatchTimer % 60f);

        SVZNetworkManager net = SVZNetworkManager.Instance;
        string pingBadge = "";
        if (net != null && net.IsConnected && net.Role != SVZNetworkManager.NetworkRole.SamePC)
        {
            int p = net.CurrentPingMs;
            string pingColor = (p <= 55) ? "#55FF55" : ((p <= 120) ? "#FFEE55" : "#FF5555");
            pingBadge = string.Format(" | <color={0}>📶 {1} ms</color>", pingColor, p);
        }
        else if (net != null && net.Role == SVZNetworkManager.NetworkRole.SamePC)
        {
            pingBadge = " | <color=#55FF55>📶 YEREL</color>";
        }

        float timerBoxW = string.IsNullOrEmpty(pingBadge) ? 140f : 210f;
        Rect timerRect = new Rect(14f, 10f, timerBoxW, barHeight);
        GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
        GUI.DrawTexture(timerRect, Texture2D.whiteTexture);
        GUI.color = mpMgr.IsSuddenDeath ? new Color(0.95f, 0.45f, 0.2f, 0.95f) : new Color(0.35f, 0.75f, 0.95f, 0.85f);
        GUI.DrawTexture(new Rect(timerRect.x, timerRect.y, timerRect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(timerRect.x, timerRect.yMax - 2f, timerRect.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle timeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold, richText = true };
        string baseTimeText = mpMgr.IsSuddenDeath
            ? string.Format("<color=#FFAA33>🔥 {0:00}:{1:00} (2X)</color>", mins, secs)
            : string.Format("<color=#55FFFF>⏱ {0:00}:{1:00}</color>", mins, secs);
        GUI.Label(timerRect, baseTimeText + pingBadge, timeStyle);

        // P2 Gate HP Bar
        float g2Pct = Mathf.Clamp01(mpMgr.Gate2Health / Mathf.Max(1f, mpMgr.Gate2MaxHealth));
        GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);
        GUI.DrawTexture(new Rect(720, 10, barWidth, barHeight), Texture2D.whiteTexture);
        GUI.color = (g2Pct > 0.3f) ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.95f, 0.6f, 0.1f);
        GUI.DrawTexture(new Rect(722, 12, (barWidth - 4) * g2Pct, barHeight - 4), Texture2D.whiteTexture);
        GUI.color = Color.white;
        string p2SideLabel = (mpMgr.P1Side == SVZMultiplayerManager.BattlefieldSide.LeftGate)
            ? LocalizationManager.T("Player 2 (Right)", "2. Oyuncu (Sag)")
            : LocalizationManager.T("Player 2 (Left)", "2. Oyuncu (Sol)");
        GUI.Label(new Rect(720, 10, barWidth, barHeight), string.Format("{0} (Lv.{1}): {2:0} HP", p2SideLabel, mpMgr.P2TroopLevel, mpMgr.Gate2Health), hpStyle);

        // Top Banner for RTS ArmyOnly Mode with Quick Camera Snap & Auto-Frontline Toggle
        if (mpMgr.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
        {
            GUI.color = new Color(0.08f, 0.12f, 0.2f, 0.88f);
            GUI.DrawTexture(new Rect(280, 71, 720, 22), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle rtsBannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 10, richText = true };
            GUI.Label(new Rect(384, 71, 355, 22),
                LocalizationManager.T("<color=#55FFFF>10-UNIT HEROLESS (225 Smithy | 8/s)</color> | <b>[A/D]</b> Camera", "<color=#55FFFF>10 BIRLIK HEROSUZ (225 Smithy | 8/sn)</color> | <b>[A/D]</b> Kamera"),
                rtsBannerStyle);

            InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
            if (inGame != null)
            {
                GUI.color = new Color(0.2f, 0.65f, 0.35f, 0.9f);
                if (GUI.Button(new Rect(284, 72, 94, 20), LocalizationManager.T("<< Left Gate", "<< Sol Kapi"))) inGame.SnapRTSCamera(0);

                GUI.color = inGame.autoFollowFrontline ? new Color(0.2f, 0.78f, 0.45f, 0.95f) : new Color(0.25f, 0.28f, 0.36f, 0.95f);
                string camBtnLbl = inGame.autoFollowFrontline
                    ? LocalizationManager.T("[C] Auto-Cam: ON", "[C] Oto-Kamera: ACIK")
                    : LocalizationManager.T("[C] Auto-Cam: OFF", "[C] Oto-Kamera: KAPALI");
                if (GUI.Button(new Rect(742, 72, 152, 20), camBtnLbl))
                {
                    inGame.autoFollowFrontline = !inGame.autoFollowFrontline;
                }

                GUI.color = new Color(0.85f, 0.3f, 0.3f, 0.9f);
                if (GUI.Button(new Rect(900, 72, 94, 20), LocalizationManager.T("Right Gate >>", "Sag Kapi >>"))) inGame.SnapRTSCamera(2);
                GUI.color = Color.white;
            }
        }

        // Opponent Hero top HP bar and P2 bottom-right deck panel are intentionally hidden.
    }

    private void DrawOpponentHeroBar()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        if (mpMgr == null) return;

        OpponentHero hero = mpMgr.CurrentOpponentHero;
        float barW = 320f;
        float barH = 26f;
        float barX = 720f;
        float barY = 38f;

        // Frame
        GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.90f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.35f, 0.35f, 0.95f);
        GUI.DrawTexture(new Rect(barX, barY, barW, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(barX, barY + barH - 2f, barW, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(barX, barY, 2f, barH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(barX + barW - 2f, barY, 2f, barH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 10, alignment = TextAnchor.MiddleCenter };

        if (hero != null && !hero.IsDead)
        {
            float hpPct = Mathf.Clamp01(hero.health / Mathf.Max(1f, hero.maxHealth));
            // HP Bar Background
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(barX + 4f, barY + 13f, barW - 8f, 9f), Texture2D.whiteTexture);
            // HP Bar Fill
            GUI.color = Color.Lerp(new Color(0.95f, 0.25f, 0.25f), new Color(0.35f, 0.95f, 0.45f), hpPct);
            GUI.DrawTexture(new Rect(barX + 4f, barY + 13f, (barW - 8f) * hpPct, 9f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(barX, barY + 1f, barW, 14f),
                string.Format("<color=#FF6666>💀 {0}</color>: <b>{1:0} / {2:0} HP</b>", hero.DisplayName, hero.health, hero.maxHealth), style);
        }
        else
        {
            GUI.Label(new Rect(barX, barY + 4f, barW, 16f),
                LocalizationManager.T("<color=#FF4444>OPPONENT HERO DEFEATED!</color>", "<color=#FF4444>⚔️ RAKİP KAHRAMAN KATLEDİLDİ!</color>"), style);
        }

        // Show Same-PC manual hero control tips if 2P manual is enabled
        if (mpMgr.IsSamePC && !mpMgr.P2AutoBot && hero != null && !hero.IsDead)
        {
            GUIStyle tipStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Normal, fontSize = 9, alignment = TextAnchor.MiddleCenter };
            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
            GUI.DrawTexture(new Rect(barX, barY + barH + 2f, barW, 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY + barH + 2f, barW, 16f),
                LocalizationManager.T("<color=#AACCFF>P2: <b>[Arrows]</b>: Move | <b>[Numpad 1]</b>: Melee | <b>[Numpad 2]</b>: Bow</color>", "<color=#AACCFF>2.P: <b>[Oklar]</b>: Hareket | <b>[Numpad 1]</b>: Kılıç | <b>[Numpad 2]</b>: Yay</color>"), tipStyle);
        }
    }

    private void DrawPlayer2HotseatHUD()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        // Positioned on the right side (X=832..1268) directly ABOVE the 2 bottom-right ability icons (Y=432..564)
        float pW = 436f;
        float pH = mMinimizeP2Dock ? 28f : 132f;
        float pX = 832f;
        float pY = mMinimizeP2Dock ? 536f : 432f;

        // Command Dock Frame & Background
        GUI.color = new Color(0.08f, 0.07f, 0.11f, 0.94f);
        GUI.DrawTexture(new Rect(pX, pY, pW, pH), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.32f, 0.32f, 0.95f);
        GUI.DrawTexture(new Rect(pX, pY, pW, 2.0f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(pX, pY + pH - 2f, pW, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(pX, pY, 2f, pH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(pX + pW - 2f, pY, 2f, pH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Header: P2 Name, Level & Energy Bar
        float pct = Mathf.Clamp01(mpMgr.Player2Leadership / Mathf.Max(1f, mpMgr.Player2MaxLeadership));
        GUIStyle title = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 10, alignment = TextAnchor.MiddleLeft };
        GUI.Label(new Rect(pX + 8f, pY + 4f, 195f, 20f),
            string.Format("<color=#FF6666><b>{0} (Lv.{1})</b></color> | <color=#FFEE55><b>{2:0}/{3:0}</b></color>",
            mpMgr.Player2Name, mpMgr.P2TroopLevel, mpMgr.Player2Leadership, mpMgr.Player2MaxLeadership), title);

        // Energy Fill Bar
        float barX = pX + 202f;
        float barY = pY + 7f;
        float barW = mpMgr.IsSamePC ? 108f : 196f;
        float barH = 14f;
        GUI.color = new Color(0.16f, 0.16f, 0.22f, 1f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.28f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(barX + 1f, barY + 1f, (barW - 2f) * pct, barH - 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Live AI Bot Toggle for Local Mode
        if (mpMgr.IsSamePC)
        {
            Rect botBtnRect = new Rect(pX + 316f, pY + 4f, 84f, 20f);
            GUI.color = mpMgr.P2AutoBot ? new Color(0.2f, 0.78f, 0.38f, 0.95f) : new Color(0.25f, 0.25f, 0.32f, 0.95f);
            GUI.DrawTexture(botBtnRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle botStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = 9, fontStyle = FontStyle.Bold, richText = true };
            string botText = mpMgr.P2AutoBot
                ? LocalizationManager.T("<color=#FFFFFF>BOT: ON</color>", "<color=#FFFFFF>BOT: ACIK</color>")
                : LocalizationManager.T("<color=#CCCCCC>BOT: OFF</color>", "<color=#CCCCCC>BOT: KAPALI</color>");
            if (GUI.Button(botBtnRect, botText, botStyle))
            {
                mpMgr.P2AutoBot = !mpMgr.P2AutoBot;
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
        }

        // Collapse / Expand Button [-]/[+]
        Rect minBtnRect = new Rect(pX + 404f, pY + 4f, 24f, 20f);
        GUI.color = new Color(0.22f, 0.24f, 0.32f, 0.95f);
        GUI.DrawTexture(minBtnRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle minStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold, richText = true };
        if (GUI.Button(minBtnRect, mMinimizeP2Dock ? "<color=#FFEE55>[+]</color>" : "<color=#FFEE55>[-]</color>", minStyle))
        {
            mMinimizeP2Dock = !mMinimizeP2Dock;
            SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
        }

        if (mMinimizeP2Dock)
        {
            return;
        }

        // 5 Unit Portrait Cards for Player 2
        float cardW = (pW - 28f) / 5f;
        float cardH = 74f;
        float cardY = pY + 26f;

        GUIStyle keyStyle = new GUIStyle(GUIStyle.none) { fontSize = 8, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.UpperLeft };
        GUIStyle costStyle = new GUIStyle(GUIStyle.none) { fontSize = 8, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.UpperRight };
        GUIStyle nameStyle = new GUIStyle(GUIStyle.none) { fontSize = 8, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true };

        for (int i = 0; i < mpMgr.P2Deck.Count; i++)
        {
            string unitId = mpMgr.P2Deck[i];
            SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unitId);
            bool canAfford = mpMgr.IsP2SlotAvailable(i);
            float cdRatio = mpMgr.GetP2SlotCooldownRatio(i);

            float cardX = pX + 8f + i * (cardW + 3f);
            Rect cRect = new Rect(cardX, cardY, cardW, cardH);

            // Card Background
            GUI.color = canAfford ? new Color(0.24f, 0.14f, 0.20f, 0.96f) : new Color(0.13f, 0.12f, 0.16f, 0.80f);
            GUI.DrawTexture(cRect, Texture2D.whiteTexture);

            // Card Top Accent Border
            GUI.color = canAfford ? new Color(1.0f, 0.78f, 0.25f, 0.95f) : new Color(0.35f, 0.35f, 0.40f, 0.6f);
            GUI.DrawTexture(new Rect(cRect.x, cRect.y, cRect.width, 2f), Texture2D.whiteTexture);

            // Bottom Readiness / Cooldown Bar
            GUI.color = canAfford ? new Color(0.25f, 0.88f, 0.40f, 0.9f) : new Color(0.95f, 0.55f, 0.15f, 0.75f);
            GUI.DrawTexture(new Rect(cRect.x, cRect.yMax - 3f, cRect.width * cdRatio, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Unit Portrait Icon
            Texture2D iconTex = GetUnitIconTexture(unitId);
            if (iconTex != null)
            {
                GUI.color = canAfford ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.85f);
                float iconSize = 38f;
                GUI.DrawTexture(new Rect(cRect.x + (cRect.width - iconSize) * 0.5f, cRect.y + 13f, iconSize, iconSize), iconTex, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            // Hotkey [N1..N5] & Cost Badge
            string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Zombie) ? "B" : "L";
            string costCol = canAfford ? "#FFEE44" : "#999999";
            GUI.Label(new Rect(cRect.x + 2f, cRect.y + 2f, 34f, 12f), string.Format("<color=#55FFFF>[N{0}]</color>", i + 1), keyStyle);
            GUI.Label(new Rect(cRect.x + cRect.width - 38f, cRect.y + 2f, 36f, 12f), string.Format("<color={0}>{1}{2}</color>", costCol, meta.cost, costType), costStyle);

            // Unit Name at Bottom of Card
            string nameCol = canAfford ? "#FFFFFF" : "#888888";
            GUI.Label(new Rect(cRect.x + 2f, cRect.y + 50f, cRect.width - 4f, 21f), string.Format("<color={0}>{1}</color>", nameCol, meta.displayName), nameStyle);

            // Invisible Click Button over the entire card
            if (GUI.Button(cRect, GUIContent.none, GUIStyle.none))
            {
                mpMgr.SummonPlayer2Unit(i);
            }
        }

        // P2 Bottom Action Row: Forge Upgrade [U], Selected Spell 1 [Q/Enter], Selected Spell 2 [E/P]
        float abY = pY + 104f;
        float abH = 22f;

        GUIStyle abBtnStyle = new GUIStyle(GUIStyle.none)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            fontStyle = FontStyle.Bold,
            fontSize = 9
        };

        // 1. Forge Upgrade Button [U]
        int upgCost = mpMgr.GetP2ForgeUpgradeCost();
        bool canUpgrade = (mpMgr.P2ForgeLevel < 4 && mpMgr.Player2Leadership >= (float)upgCost);
        Rect upgBtnRect = new Rect(pX + 8f, abY, 124f, abH);
        GUI.color = (mpMgr.P2ForgeLevel >= 4)
            ? new Color(0.20f, 0.55f, 0.35f, 0.9f)
            : (canUpgrade ? new Color(0.20f, 0.68f, 0.85f, 0.95f) : new Color(0.18f, 0.20f, 0.26f, 0.85f));
        GUI.DrawTexture(upgBtnRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        string upgText = (mpMgr.P2ForgeLevel >= 4)
            ? "<color=#88FFAA>Ocak MAX (Lv.4)</color>"
            : string.Format("<color={0}>[U] Ocak Lv.{1} ({2})</color>", canUpgrade ? "#FFEE55" : "#888888", mpMgr.P2ForgeLevel + 1, upgCost);
        if (GUI.Button(upgBtnRect, upgText, abBtnStyle))
        {
            mpMgr.UpgradePlayer2Forge();
        }

        // 2 & 3. Player 2's 2 Selected Spells
        for (int spIdx = 0; spIdx < 2; spIdx++)
        {
            string spId = (mpMgr.P2Spells != null && spIdx < mpMgr.P2Spells.Count) ? mpMgr.P2Spells[spIdx] : (spIdx == 0 ? "DragonAttack" : "GraveHands");
            SVZMultiplayerManager.SpellMeta spMeta = SVZMultiplayerManager.GetSpellMeta(spId);
            bool spReady = mpMgr.IsP2SpellAvailable(spIdx);
            float spRem = mpMgr.GetP2SpellCooldownRemaining(spIdx);
            float spRatio = mpMgr.GetP2SpellCooldownRatio(spIdx);

            float btnX = (spIdx == 0) ? (pX + 136f) : (pX + 284f);
            Rect spBtnRect = new Rect(btnX, abY, 144f, abH);

            Color activeCol = (spIdx == 0) ? new Color(0.78f, 0.34f, 0.14f, 0.95f) : new Color(0.50f, 0.20f, 0.72f, 0.95f);
            GUI.color = spReady ? activeCol : new Color(0.18f, 0.16f, 0.22f, 0.85f);
            GUI.DrawTexture(spBtnRect, Texture2D.whiteTexture);

            // Cooldown fill bar at bottom of spell button
            GUI.color = spReady ? new Color(0.3f, 0.95f, 0.45f, 0.9f) : new Color(0.95f, 0.65f, 0.2f, 0.8f);
            GUI.DrawTexture(new Rect(spBtnRect.x, spBtnRect.yMax - 2f, spBtnRect.width * spRatio, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Texture2D spIcon = GetSpellIconTexture(spId);
            if (spIcon != null)
            {
                GUI.color = spReady ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
                GUI.DrawTexture(new Rect(spBtnRect.x + 3f, spBtnRect.y + 2f, 18f, 18f), spIcon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }

            string keyName = mpMgr.IsSamePC ? (spIdx == 0 ? "ENT" : "P") : (spIdx == 0 ? "Q" : "E");
            string spLabel = spReady
                ? string.Format("<color=#FFEE55>[{0}]</color> <color=#FFFFFF>{1}</color>", keyName, spMeta.displayName)
                : string.Format("<color=#999999>[{0}] {1} ({2:0}s)</color>", keyName, spMeta.displayName, spRem);

            GUI.Label(new Rect(spBtnRect.x + 22f, spBtnRect.y, spBtnRect.width - 24f, spBtnRect.height), spLabel, abBtnStyle);

            if (GUI.Button(spBtnRect, GUIContent.none, GUIStyle.none))
            {
                mpMgr.TriggerPlayer2Spell(spIdx);
            }
        }
    }

    private void DrawMinimapRadar(float rX, float rY, float rW, float rH)
    {
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        if (inGame == null) return;

        float leftZ = (inGame.heroWalkLeftEdge != null) ? inGame.heroWalkLeftEdge.position.z : 0f;
        float rightZ = (inGame.heroWalkRightEdge != null) ? inGame.heroWalkRightEdge.position.z : 1000f;
        float laneSpan = Mathf.Max(100f, Mathf.Abs(rightZ - leftZ));

        // Background Track
        Rect radarRect = new Rect(rX, rY, rW, rH);
        GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.90f);
        GUI.DrawTexture(radarRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.35f, 0.40f, 0.50f, 0.8f);
        GUI.DrawTexture(new Rect(rX, rY + rH * 0.5f - 1, rW, 2), Texture2D.whiteTexture);

        // Left Gate Marker
        GUI.color = new Color(0.25f, 0.85f, 0.35f);
        GUI.DrawTexture(new Rect(rX, rY, 6, rH), Texture2D.whiteTexture);
        // Right Gate Marker
        GUI.color = new Color(0.95f, 0.3f, 0.3f);
        GUI.DrawTexture(new Rect(rX + rW - 6, rY, 6, rH), Texture2D.whiteTexture);

        // Blips for all alive characters
        CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
        if (charMgr != null && charMgr.allAlive != null)
        {
            List<Character> chars = charMgr.allAlive;
            for (int i = 0; i < chars.Count; i++)
            {
                Character c = chars[i];
                if (c == null || c.health <= 0f || c.isBase) continue;

                float norm = Mathf.Clamp01((c.position.z - leftZ) / laneSpan);
                float blipX = rX + norm * (rW - 6);

                if (c.isPlayer)
                {
                    GUI.color = new Color(1.0f, 0.85f, 0.2f);
                    GUI.DrawTexture(new Rect(blipX - 1, rY + 3, 6, rH - 6), Texture2D.whiteTexture);
                }
                else if (c.isBoss)
                {
                    GUI.color = new Color(0.95f, 0.2f, 0.95f);
                    GUI.DrawTexture(new Rect(blipX - 1, rY + 2, 6, rH - 4), Texture2D.whiteTexture);
                }
                else if (!c.isEnemy)
                {
                    GUI.color = new Color(0.3f, 0.9f, 0.4f, 0.9f);
                    GUI.DrawTexture(new Rect(blipX, rY + 5, 4, rH - 10), Texture2D.whiteTexture);
                }
                else
                {
                    GUI.color = new Color(0.95f, 0.3f, 0.3f, 0.9f);
                    GUI.DrawTexture(new Rect(blipX, rY + 5, 4, rH - 10), Texture2D.whiteTexture);
                }
            }
        }

        // Camera Frustum Indicator
        if (Camera.main != null)
        {
            float camZ = Camera.main.transform.position.z;
            float camNorm = Mathf.Clamp01((camZ - leftZ) / laneSpan);
            float camBoxW = rW * 0.18f;
            float camBoxX = Mathf.Clamp(rX + camNorm * rW - camBoxW * 0.5f, rX, rX + rW - camBoxW);

            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(camBoxX, rY, camBoxW, rH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // Click anywhere on Minimap in ArmyOnly mode to jump RTS camera to that exact position
        if (SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
        {
            if (GUI.Button(radarRect, GUIContent.none, GUIStyle.none))
            {
                float clickRatio = Mathf.Clamp01((Event.current.mousePosition.x - rX) / rW);
                inGame.SetRTSCameraRatio(clickRatio);
            }
        }
        GUI.color = Color.white;
    }

    private void DrawCombatLog(float x, float y, float w, float h)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        List<string> msgs = mpMgr.ChatMessages;
        int start = Mathf.Max(0, msgs.Count - 6);
        GUIStyle logStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true };

        float curY = y + 4f;
        for (int i = start; i < msgs.Count; i++)
        {
            GUI.Label(new Rect(x + 6, curY, w - 12, 18), msgs[i], logStyle);
            curY += 17f;
        }
    }

    #endregion

    #region Match Over Overlay

    private void DrawMatchOverOverlay()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        float oW = 620f;
        float oH = 360f;
        float oX = 640f - oW * 0.5f;
        float oY = 360f - oH * 0.5f;

        // Frame
        GUI.color = new Color(0.08f, 0.09f, 0.14f, 0.98f);
        GUI.DrawTexture(new Rect(oX, oY, oW, oH), Texture2D.whiteTexture);
        GUI.color = (mpMgr.Winner == 1) ? new Color(0.3f, 0.85f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
        GUI.DrawTexture(new Rect(oX, oY, oW, 4), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(oX, oY + oH - 4, oW, 4), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle winTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };

        string winText = (mpMgr.Winner == 1)
            ? "<color=#55FF55>" + LocalizationManager.L("mod_mp_victory_p1", "PLAYER 1 VICTORY!", "1. OYUNCU ZAFER KAZANDI!") + "</color>"
            : "<color=#FF5555>" + LocalizationManager.L("mod_mp_victory_p2", "PLAYER 2 VICTORY!", "2. OYUNCU ZAFER KAZANDI!") + "</color>";

        GUI.Label(new Rect(oX + 10, oY + 16, oW - 20, 32), winText, winTitleStyle);

        // Detailed Match Statistics Box
        Rect statsBox = new Rect(oX + 24, oY + 54, oW - 48, 160);
        GUI.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);
        GUI.DrawTexture(statsBox, Texture2D.whiteTexture);
        GUI.color = Color.white;

        int mins = Mathf.FloorToInt(mpMgr.MatchTimer / 60f);
        int secs = Mathf.FloorToInt(mpMgr.MatchTimer % 60f);

        GUIStyle statHeader = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        GUI.Label(new Rect(statsBox.x, statsBox.y + 6, statsBox.width, 20),
            string.Format(LocalizationManager.T("<color=#FFEE77>MATCH STATISTICS</color> | Duration: <b>{0:00}:{1:00}</b>", "<color=#FFEE77>SAVAS ISTATISTIKLERI</color> | Savas Suresi: <b>{0:00}:{1:00}</b>"), mins, secs), statHeader);

        GUIStyle statBody = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 };
        float halfW = statsBox.width * 0.5f;

        // P1 Stats (Left)
        string p1Stats = string.Format(
            LocalizationManager.T(
                "<color=#55FF55><b>{0}</b></color>\n- Units Summoned: <b>{1}</b>\n- Abilities Used: <b>{2}</b>\n- Gate HP Left: <b>{3:0} HP</b>",
                "<color=#55FF55><b>{0}</b></color>\n- Cagrilan Birlik: <b>{1}</b>\n- Kullanilan Yetenek: <b>{2}</b>\n- Kalan Kale Cani: <b>{3:0} HP</b>"),
            mpMgr.Player1Name, mpMgr.P1UnitsSpawned, mpMgr.P1AbilitiesUsed, mpMgr.Gate1Health);
        GUI.Label(new Rect(statsBox.x + 12, statsBox.y + 30, halfW - 20, 80), p1Stats, statBody);

        // P2 Stats (Right)
        string p2Stats = string.Format(
            LocalizationManager.T(
                "<color=#FF7777><b>{0}</b></color>\n- Units Summoned: <b>{1}</b>\n- Abilities Used: <b>{2}</b>\n- Gate HP Left: <b>{3:0} HP</b>",
                "<color=#FF7777><b>{0}</b></color>\n- Cagrilan Birlik: <b>{1}</b>\n- Kullanilan Yetenek: <b>{2}</b>\n- Kalan Kale Cani: <b>{3:0} HP</b>"),
            mpMgr.Player2Name, mpMgr.P2UnitsSpawned, mpMgr.P2AbilitiesUsed, mpMgr.Gate2Health);
        GUI.Label(new Rect(statsBox.x + halfW + 12, statsBox.y + 30, halfW - 20, 80), p2Stats, statBody);

        // MVP Banner
        GUI.Label(new Rect(statsBox.x, statsBox.y + 115, statsBox.width, 30),
            string.Format(LocalizationManager.T("<color=#FFEE77><b>MATCH MVP UNIT:</b></color> <color=#55FFFF><b>{0}</b></color>", "<color=#FFEE77><b>MACIN YILDIZ BIRLIGI (MVP):</b></color> <color=#55FFFF><b>{0}</b></color>"), mpMgr.MvpUnit), statHeader);

        // Rematch Button
        GUI.color = new Color(0.25f, 0.85f, 0.4f);
        if (GUI.Button(new Rect(oX + 36, oY + 230, oW - 72, 44), LocalizationManager.L("mod_mp_rematch", "REMATCH NOW", "HEMEN ROVANS YAP (REMATCH)")))
        {
            mpMgr.Rematch();
        }

        // Return to Lobby / Main Menu Button
        GUI.color = new Color(0.85f, 0.3f, 0.25f);
        if (GUI.Button(new Rect(oX + 36, oY + 284, oW - 72, 38), LocalizationManager.L("mod_mp_return", "RETURN TO LOBBY / MAIN MENU", "LOBIYE / ANA MENUYE DON")))
        {
            mpMgr.ResetMatchState();
            Time.timeScale = 1.0f;
            mpMgr.LoadSceneWithLoadingScreen("MainMenu");
        }
        GUI.color = Color.white;
    }

    #endregion

    #region Lobby Window

    private void DrawLobbyWindow()
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        float w = 1200f;
        float h = 670f;
        float x = 640f - w * 0.5f;
        float y = 360f - h * 0.5f;

        // Window Background & Border
        GUI.color = new Color(0.08f, 0.09f, 0.13f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(1.0f, 0.8f, 0.2f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, w, 3), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y + h - 3, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Window Title
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        GUI.Label(new Rect(x, y + 8, w, 24), "<color=#FFDD44>SAMURAI VS ZOMBIES - " + LocalizationManager.L("mod_mp_lobby", "1v1 PvP CUSTOM GAME LOBBY", "1v1 PvP ÖZEL OYUN LOBİSİ") + "</color>", titleStyle);

        // Close Button [X]
        if (GUI.Button(new Rect(x + w - 38, y + 8, 30, 24), "X"))
        {
            mShowLobby = false;
            SVZMultiplayerManager.PlaySFX("UI_Window_Close_01", 0.8f);
        }

        // 4 Navigation Tabs (Added 4th Mode: 10-Unit Heroless 1v1 Mode!)
        float navY = y + 36f;
        float navH = 30f;
        float navW = (w - 32f) / 4f;
        string[] tabNames = new string[]
        {
            LocalizationManager.T("1. CUSTOM GAME LOBBY", "1. 🏰 ÖZEL OYUN LOBİSİ (LoL Stili)"),
            LocalizationManager.T("2. ARMY & DECK BUILDER", "2. ⚔️ ORDU & DESTE DÜZENLEYİCİ"),
            LocalizationManager.T("3. ARENA & MUTATORS", "3. ⚙️ ARENA & MUTATOR DETAYI"),
            LocalizationManager.T("4. 10-UNIT HEROLESS 1v1", "4. 🎖️ 10 BİRLİK MODU (HEROSUZ 1v1)")
        };

        for (int i = 0; i < 4; i++)
        {
            bool isActive = (mSelectedTab == i);
            GUI.color = isActive ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.18f, 0.2f, 0.26f);
            if (GUI.Button(new Rect(x + 16f + i * navW, navY, navW - 4f, navH), tabNames[i]))
            {
                mSelectedTab = i;
                if (i == 3)
                {
                    mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly;
                    mSelectedP1SpellSlot = -1;
                    mSelectedP2SpellSlot = -1;
                    if (mCatalogFilter == 3) mCatalogFilter = 0;
                    if (mSelectedP1Slot < 0 && mSelectedP2Slot < 0) mSelectedP1Slot = 0;
                    mpMgr.BroadcastLobbyConfig();
                }
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
        }
        GUI.color = Color.white;

        // Tab Content Area
        float cx = x + 16f;
        float cy = navY + navH + 8f;
        float cw = w - 32f;
        float ch = h - (cy - y) - 14f;

        switch (mSelectedTab)
        {
            case 0: DrawTab0CustomLobby(cx, cy, cw, ch); break;
            case 1: DrawTab1DeckBuilder(cx, cy, cw, ch); break;
            case 2: DrawTab0Settings(cx, cy, cw, ch); break;
            case 3: DrawTab3TenUnitArmyMode(cx, cy, cw, ch); break;
        }
    }

    private void DrawTab0CustomLobby(float cx, float cy, float cw, float ch)
    {
        SVZNetworkManager net = SVZNetworkManager.Instance;
        bool inRoom = mInCustomLobbyRoom || (net != null && (net.IsConnected || net.Role != SVZNetworkManager.NetworkRole.None));

        if (!inRoom)
        {
            DrawLobbyGateway(cx, cy, cw, ch);
        }
        else
        {
            DrawLoLCustomLobby(cx, cy, cw, ch);
        }
    }

    private void DrawLobbyGateway(float cx, float cy, float cw, float ch)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        SVZNetworkManager net = SVZNetworkManager.Instance;

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(cx, cy + 15f, cw, 40f),
            LocalizationManager.T(
                "<color=#FFDD44><b>SAMURAI VS ZOMBIES - CUSTOM GAME LOBBY</b></color>\n<size=11><color=#CCCCCC>Host your own room or join a friend via IP to battle 1v1!</color></size>",
                "<color=#FFDD44><b>🏰 SAMURAI VS ZOMBIES - ÖZEL OYUN LOBİSİ</b></color>\n<size=11><color=#CCCCCC>Kendi özel odanızı kurun veya arkadaşınızın odasına bağlanarak 1v1 mücadeleye başlayın!</color></size>"), headerStyle);

        float cardW = (cw - 32f) / 3f;
        float cardH = 370f;
        float cardY = cy + 70f;

        GUIStyle cardHeader = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold, fontSize = 13 };
        GUIStyle cardBody = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = true, fontSize = 11, wordWrap = true };

        // CARD 1: HOST LOBBY
        float c1x = cx;
        GUI.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(c1x, cardY, cardW, cardH), Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.75f, 1.0f, 0.9f);
        GUI.DrawTexture(new Rect(c1x, cardY, cardW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(c1x + 8f, cardY + 14f, cardW - 16f, 22f), LocalizationManager.T("<color=#55FFFF><b>HOST PRIVATE ROOM</b></color>", "<color=#55FFFF><b>👑 ÖZEL ODA OLUŞTUR (HOST)</b></color>"), cardHeader);
        string hostDesc = string.Format(
            LocalizationManager.T(
                "Create a private match room.\nFriends can connect using your IP.\n\n<b>Port:</b> {0}\n<b>Your Local IP:</b>\n<color=#FFEE55><b>{1}</b></color>\n\n<size=10><color=#AAAAAA>(If using Radmin VPN / Hamachi, share your 26.x.x.x IP)</color></size>",
                "Kendi özel odanızı kurun.\nArkadaşınız IP adresinizle odanıza bağlanabilir.\n\n<b>Oda Portu:</b> {0}\n<b>Yerel IP Adresiniz:</b>\n<color=#FFEE55><b>{1}</b></color>\n\n<size=10><color=#AAAAAA>(Radmin VPN / Hamachi kullanıyorsanız 26.x.x.x IP'nizi arkadaşınıza verin)</color></size>"),
            (net != null) ? net.Port : SVZNetworkManager.kDefaultPort,
            (net != null) ? net.GetLocalIPAddress() : "127.0.0.1");
        GUI.Label(new Rect(c1x + 12f, cardY + 45f, cardW - 24f, 180f), hostDesc, cardBody);

        GUI.color = new Color(0.25f, 0.85f, 0.45f);
        if (GUI.Button(new Rect(c1x + 24f, cardY + 250f, cardW - 48f, 44f), LocalizationManager.T("<b><size=13>CREATE & OPEN ROOM</size></b>", "<b><size=13>🏰 ODAYA KUR VE AÇ</size></b>")))
        {
            if (net != null) net.StartHost();
            mInCustomLobbyRoom = true;
            mSelectedP1Slot = 0;
            mSelectedP2Slot = -1;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.9f);
        }
        GUI.color = Color.white;

        // CARD 2: JOIN LOBBY
        float c2x = cx + cardW + 16f;
        GUI.color = new Color(0.18f, 0.12f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(c2x, cardY, cardW, cardH), Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.45f, 1.0f, 0.9f);
        GUI.DrawTexture(new Rect(c2x, cardY, cardW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(c2x + 8f, cardY + 14f, cardW - 16f, 22f), LocalizationManager.T("<color=#FF88FF><b>JOIN ROOM (CLIENT)</b></color>", "<color=#FF88FF><b>🔗 ODAYA KATIL (İSTEMCİ)</b></color>"), cardHeader);
        string joinDesc = LocalizationManager.T(
            "Join a room hosted by a friend.\nEnter the host IP address to connect immediately.\n",
            "Arkadaşınızın kurduğu odaya katılın.\nKurucunun IP adresini girerek anında lobiye bağlanın.\n");
        GUI.Label(new Rect(c2x + 12f, cardY + 45f, cardW - 24f, 50f), joinDesc, cardBody);

        GUI.Label(new Rect(c2x + 24f, cardY + 115f, 90f, 20f), LocalizationManager.T("<b>Target IP:</b>", "<b>Hedef IP:</b>"));
        mJoinIpInput = GUI.TextField(new Rect(c2x + 115f, cardY + 113f, cardW - 210f, 24f), mJoinIpInput);
        if (GUI.Button(new Rect(c2x + cardW - 90f, cardY + 113f, 66f, 24f), LocalizationManager.T("Paste", "Yapıştır")))
        {
            if (!string.IsNullOrEmpty(GUIUtility.systemCopyBuffer))
            {
                mJoinIpInput = GUIUtility.systemCopyBuffer.Trim();
            }
        }

        GUI.Label(new Rect(c2x + 24f, cardY + 155f, 90f, 20f), "<b>Port:</b>");
        string portStr = GUI.TextField(new Rect(c2x + 115f, cardY + 153f, 70f, 24f), mJoinPortInput.ToString());
        int.TryParse(portStr, out mJoinPortInput);

        GUI.color = new Color(0.35f, 0.70f, 1.0f);
        if (GUI.Button(new Rect(c2x + 24f, cardY + 250f, cardW - 48f, 44f), LocalizationManager.T("<b><size=13>CONNECT TO ROOM</size></b>", "<b><size=13>🚀 ODAYA BAĞLAN</size></b>")))
        {
            if (net != null) net.ConnectToHost(mJoinIpInput, mJoinPortInput);
            mInCustomLobbyRoom = true;
            mSelectedP1Slot = -1;
            mSelectedP2Slot = 0;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.9f);
        }
        GUI.color = Color.white;

        // CARD 3: LOCAL / BOT LOBBY
        float c3x = cx + (cardW + 16f) * 2f;
        GUI.color = new Color(0.18f, 0.16f, 0.10f, 0.95f);
        GUI.DrawTexture(new Rect(c3x, cardY, cardW, cardH), Texture2D.whiteTexture);
        GUI.color = new Color(1.0f, 0.8f, 0.25f, 0.9f);
        GUI.DrawTexture(new Rect(c3x, cardY, cardW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(c3x + 8f, cardY + 14f, cardW - 16f, 22f), LocalizationManager.T("<color=#FFDD44><b>LOCAL ROOM (BOT & HOTSEAT)</b></color>", "<color=#FFDD44><b>🤖 YEREL ODA (BOT & HOTSEAT)</b></color>"), cardHeader);
        string localDesc = LocalizationManager.T(
            "Practice solo offline against an AI Commander or play 2-player Hotseat on the same device.",
            "İnternet bağlantısı olmadan tek başınıza yapay zeka komutanına karşı pratik yapın ya da aynı PC'de arkadaşınızla çift kişilik kapışın.");
        GUI.Label(new Rect(c3x + 12f, cardY + 45f, cardW - 24f, 60f), localDesc, cardBody);

        GUI.color = new Color(0.95f, 0.65f, 0.2f);
        if (GUI.Button(new Rect(c3x + 24f, cardY + 145f, cardW - 48f, 40f), LocalizationManager.T("<b>AI BOT MATCH</b>", "<b>🤖 YAPAY ZEKA BOT ODASI</b>")))
        {
            mpMgr.P2AutoBot = true;
            mInCustomLobbyRoom = true;
            if (net != null) net.StartSamePCMode();
            SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.9f);
        }

        GUI.color = new Color(0.35f, 0.85f, 0.45f);
        if (GUI.Button(new Rect(c3x + 24f, cardY + 205f, cardW - 48f, 40f), LocalizationManager.T("<b>SAME PC 2-PLAYER</b>", "<b>👥 AYNI PC ÇİFT KİŞİLİK</b>")))
        {
            mpMgr.P2AutoBot = false;
            mInCustomLobbyRoom = true;
            if (net != null) net.StartSamePCMode();
            SVZMultiplayerManager.PlaySFX("UI_Window_Open_01", 0.9f);
        }
        GUI.color = Color.white;
    }

    private void DrawLoLCustomLobby(float cx, float cy, float cw, float ch)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        SVZNetworkManager net = SVZNetworkManager.Instance;

        bool isHost = (net != null && net.IsHost);
        bool isClient = (net != null && !net.IsHost && net.Role == SVZNetworkManager.NetworkRole.Client);
        bool isSamePC = mpMgr.IsSamePC || (net != null && net.Role == SVZNetworkManager.NetworkRole.SamePC);
        bool isLocalP1 = isSamePC || (net == null) || isHost;
        bool isLocalP2 = isSamePC || isClient || mpMgr.P2AutoBot;
        bool isTenUnitMode = (mpMgr.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly);
        bool isRemoteConnected = (net != null && net.IsConnected && net.Role != SVZNetworkManager.NetworkRole.SamePC);

        // 1. TOP HEADER & STATUS BAR
        GUI.color = new Color(0.10f, 0.12f, 0.18f, 0.94f);
        GUI.DrawTexture(new Rect(cx, cy, cw, 32f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string modeStr = isTenUnitMode
            ? LocalizationManager.T("10-Unit Heroless (225 Smithy)", "🎖️ 10 Birlik Herosuz (225 Smithy)")
            : LocalizationManager.T("Hero Arena (5 Units)", "🗡️ Kahramanlı Meydan (5 Birlik)");
        GUIStyle topTitle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 11, alignment = TextAnchor.MiddleLeft };
        GUI.Label(new Rect(cx + 8f, cy + 6f, 360f, 20f), string.Format(LocalizationManager.T("<b>CUSTOM GAME ROOM</b> | <color=#55FFFF>{0}</color> | <color=#FFEE55>{1}</color>", "🏰 <b>ÖZEL OYUN ODASI</b> | <color=#55FFFF>{0}</color> | <color=#FFEE55>{1}</color>"), mpMgr.SelectedArena, modeStr), topTitle);

        // Center Network Status Badge
        string pingStr = (net != null && net.CurrentPingMs > 0) ? string.Format(" | <color=#55FFFF>{0} ms</color>", net.CurrentPingMs) : "";
        string netStatus;
        if (isSamePC)
        {
            netStatus = string.Format(LocalizationManager.T("<color=#55FFFF><b>LOCAL 2P MATCH</b></color> (P1 vs {0})", "<color=#55FFFF><b>👥 YEREL ÇİFT KİŞİLİK</b></color> (P1 vs {0})"), mpMgr.P2AutoBot ? "Bot" : "P2 Hotseat");
        }
        else if (isHost)
        {
            netStatus = net.IsConnected
                ? string.Format(LocalizationManager.T("<color=#55FF55><b>PLAYER 2 CONNECTED!</b></color>{0}", "<color=#55FF55><b>🟢 2. OYUNCU BAĞLANDI!</b></color>{0}"), pingStr)
                : string.Format(LocalizationManager.T("<color=#FFAA33><b>WAITING FOR P2...</b></color> (IP: <color=#FFEE55>{0}:{1}</color>)", "<color=#FFAA33><b>🟡 2. OYUNCU BEKLENİYOR...</b></color> (IP: <color=#FFEE55>{0}:{1}</color>)"), net.GetLocalIPAddress(), net.Port);
        }
        else
        {
            netStatus = net.IsConnected
                ? string.Format(LocalizationManager.T("<color=#55FF55><b>CONNECTED TO HOST!</b></color>{0}", "<color=#55FF55><b>🟢 KURUCUYA BAĞLANDINIZ!</b></color>{0}"), pingStr)
                : string.Format(LocalizationManager.T("<color=#FF5555><b>CONNECTING...</b></color> (Target: {0}:{1})", "<color=#FF5555><b>🔴 BAĞLANTI BEKLENİYOR...</b></color> (Hedef: {0}:{1})"), net.RemoteIP, net.Port);
        }
        GUIStyle netBadgeStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold, fontSize = 11 };
        GUI.Label(new Rect(cx + 360f, cy + 6f, cw - 660f, 20f), netStatus, netBadgeStyle);

        // Right Quick Actions: [Copy IP / Reconnect] [Leave Lobby]
        float rx = cx + cw - 260f;
        if (isClient && !net.IsConnected)
        {
            GUI.color = new Color(0.3f, 0.8f, 1f);
            if (GUI.Button(new Rect(rx, cy + 4f, 110f, 24f), LocalizationManager.T("Reconnect", "🔄 Yeniden Bağlan")))
            {
                net.ConnectToHost(net.RemoteIP, net.Port);
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
            GUI.color = Color.white;
        }
        else
        {
            if (GUI.Button(new Rect(rx, cy + 4f, 110f, 24f), LocalizationManager.T("Copy IP", "📋 IP Kopyala")))
            {
                string ip = (net != null) ? net.GetLocalIPAddress() : "127.0.0.1";
                GUIUtility.systemCopyBuffer = ip;
                SVZMultiplayerManager.AddChatMessage("[SİSTEM]", "IP Panoya Kopyalandı: " + ip);
                SVZMultiplayerManager.PlaySFX("UI_Upgrade_01", 0.8f);
            }
        }
        if (GUI.Button(new Rect(rx + 115f, cy + 4f, 140f, 24f), "🚪 Lobiden Ayrıl"))
        {
            if (net != null) net.StopNetwork();
            mInCustomLobbyRoom = false;
            SVZMultiplayerManager.PlaySFX("UI_Window_Close_01", 0.8f);
        }

        // 2. MAIN 3-COLUMN ARENA LAYOUT (Blue Side, Center Room Rules, Red Side)
        float colY = cy + 38f;
        float colH = 265f;
        float sideColW = 340f;
        float centerColW = cw - (sideColW * 2f) - 16f;

        GUIStyle teamHeader = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 11, alignment = TextAnchor.MiddleLeft };
        GUIStyle slotTextStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleLeft, richText = true, fontSize = 9, wordWrap = false };

        // --- BLUE SIDE: TAKIM 1 (MAVİ KÖŞE - 1. OYUNCU / HOST) ---
        float p1X = cx;
        GUI.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(p1X, colY, sideColW, colH), Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.55f, 0.95f, 1f);
        GUI.DrawTexture(new Rect(p1X, colY, sideColW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        bool isRightSideP1 = (mpMgr.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate);
        string p1SideBadge = isRightSideP1 ? "<color=#FF9944>(SAĞ KALE)</color>" : "<color=#66CCFF>(SOL KALE)</color>";
        GUI.Label(new Rect(p1X + 8f, colY + 5f, sideColW - 110f, 18f), "<color=#55FF55><b>🔵 1. OYUNCU</b></color> " + p1SideBadge + " <color=#55FF55>🟢 HAZIR</color>", teamHeader);
        if (isLocalP1)
        {
            GUI.color = isRightSideP1 ? new Color(1f, 0.45f, 0.35f) : new Color(0.75f, 0.45f, 0.95f);
            string topSwapLbl = isRightSideP1 ? "🔄 Sağ Taraf" : "🔄 Taraf Değiştir";
            if (GUI.Button(new Rect(p1X + sideColW - 102f, colY + 4f, 96f, 20f), topSwapLbl))
            {
                mpMgr.SwapTeamsAndSide();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
            GUI.color = Color.white;
        }

        bool hideP1 = ShouldHideP1PicksFromLocal();

        if (isTenUnitMode)
        {
            // 10-UNIT HEROLESS MODE FOR P1
            float p1ModeBoxY = colY + 25f;
            GUI.color = new Color(0.14f, 0.24f, 0.34f, 0.95f);
            GUI.DrawTexture(new Rect(p1X + 6f, p1ModeBoxY, sideColW - 12f, 24f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(p1X + 10f, p1ModeBoxY + 3f, sideColW - 20f, 18f),
                "<color=#55FFFF><b>🎖️ HEROSUZ 10 BİRLİK ORDUSU</b></color> <size=9><color=#FFEE55>(Smithy: 225 | +8/sn)</color></size>", slotTextStyle);

            float gridY = p1ModeBoxY + 27f;
            float halfSlotW = (sideColW - 16f) * 0.5f;
            for (int i = 0; i < mpMgr.P1Deck.Count; i++)
            {
                string unit = mpMgr.P1Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP1Slot == i && mSelectedP2Slot == -1);
                int col = i / 5;
                int row = i % 5;

                Rect sRect = new Rect(p1X + 6f + col * (halfSlotW + 4f), gridY + row * 31f, halfSlotW, 29f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.14f, 0.18f, 0.26f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (hideP1)
                {
                    GUI.Label(new Rect(sRect.x + 6f, sRect.y + 5f, sRect.width - 10f, 18f),
                        string.Format("<color=#AAAAAA>S{0}:</color> <color=#FFD700><b>[🔒 GİZLİ]</b></color>", i + 1), slotTextStyle);
                }
                else
                {
                    Texture2D uIcon = GetUnitIconTexture(unit);
                    if (uIcon != null) GUI.DrawTexture(new Rect(sRect.x + 3f, sRect.y + 3f, 22f, 22f), uIcon, ScaleMode.ScaleToFit);

                    string costBadge = (meta.faction == SVZMultiplayerManager.UnitFaction.Samurai) ? string.Format("{0}L", meta.cost) : string.Format("{0}B", meta.cost);
                    string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                    string rowLabel = string.Format("<color=#FFEE55><b>S{0}</b></color>{1} <color=#FFFFFF><b>{2}</b></color>\n<size=8><color=#66DDFF>{3}</color></size>", i + 1, tierBadge, meta.displayName, costBadge);
                    GUI.Label(new Rect(sRect.x + 27f, sRect.y + 1f, sRect.width - 29f, 26f), rowLabel, slotTextStyle);

                    if (isLocalP1 && GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                    {
                        mSelectedP1Slot = i;
                        mSelectedP2Slot = -1;
                        mSelectedP1SpellSlot = -1;
                        mSelectedP2SpellSlot = -1;
                        if (mCatalogFilter == 3) mCatalogFilter = 0;
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
                    }
                }
            }
        }
        else
        {
            // P1 Hero Box
            float p1HeroY = colY + 26f;
            GUI.color = new Color(0.15f, 0.20f, 0.30f, 0.95f);
            GUI.DrawTexture(new Rect(p1X + 6f, p1HeroY, sideColW - 12f, 44f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (hideP1)
            {
                GUI.Label(new Rect(p1X + 12f, p1HeroY + 6f, sideColW - 24f, 32f),
                    "<color=#FFD700><b>[ ❓ GİZLİ KAHRAMAN ]</b></color>\n<size=9><color=#AAAAAA>Kör Seçim Modu: Savaş Başlayınca Görünür</color></size>", new GUIStyle(GUI.skin.label) { richText = true });
            }
            else
            {
                Texture2D hIcon = GetHeroIconTexture(mpMgr.P1HeroType);
                if (hIcon != null) GUI.DrawTexture(new Rect(p1X + 10f, p1HeroY + 4f, 36f, 36f), hIcon, ScaleMode.ScaleToFit);

                string hName = (mpMgr.P1HeroType == SVZMultiplayerManager.HeroType.Samurai) ? "🗡️ Samuray Kahramanı" : "💀 Zombi Kahramanı";
                string hSpellTag = (mpMgr.P1HeroType == SVZMultiplayerManager.HeroType.Samurai) ? "Samuray Büyüleri" : "Zombi Büyüleri";
                string hInfo = string.Format("<color=#FFEE55><b>{0}</b></color>\n<size=9><color=#66DDFF>4000 HP | {1}</color></size>", hName, hSpellTag);
                GUI.Label(new Rect(p1X + 50f, p1HeroY + 5f, sideColW - 188f, 34f), hInfo, new GUIStyle(GUI.skin.label) { richText = true });

                if (isLocalP1)
                {
                    if (GUI.Button(new Rect(p1X + sideColW - 136f, p1HeroY + 9f, 58f, 26f), "Hero"))
                    {
                        mpMgr.P1HeroType = (mpMgr.P1HeroType == SVZMultiplayerManager.HeroType.Samurai) ? SVZMultiplayerManager.HeroType.Zombie : SVZMultiplayerManager.HeroType.Samurai;
                        mpMgr.EnsureValidSpellsForPlayer(1);
                        mpMgr.BroadcastLobbyPick(1, "HERO", 0, mpMgr.P1HeroType.ToString());
                        for (int spIdx = 0; spIdx < mpMgr.P1Spells.Count; spIdx++)
                        {
                            mpMgr.BroadcastLobbyPick(1, "SPELL", spIdx, mpMgr.P1Spells[spIdx]);
                        }
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                    }
                    GUI.color = isRightSideP1 ? new Color(1f, 0.45f, 0.35f) : new Color(0.75f, 0.45f, 0.95f);
                    if (GUI.Button(new Rect(p1X + sideColW - 74f, p1HeroY + 9f, 64f, 26f), isRightSideP1 ? "Sağ Kale" : "Taraf Seç"))
                    {
                        mpMgr.SwapTeamsAndSide();
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                    }
                    GUI.color = Color.white;
                }
            }

            // P1 5-Deck Slots (compact 20px rows to fit 2 Spell Slots underneath)
            float p1SlotsY = p1HeroY + 46f;
            for (int i = 0; i < mpMgr.P1Deck.Count; i++)
            {
                string unit = mpMgr.P1Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP1Slot == i && mSelectedP2Slot == -1 && mSelectedP1SpellSlot == -1 && mSelectedP2SpellSlot == -1);

                Rect sRect = new Rect(p1X + 6f, p1SlotsY + i * 21f, sideColW - 12f, 20f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.14f, 0.18f, 0.26f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (hideP1)
                {
                    string hiddenTxt = string.Format("<color=#AAAAAA>Slot {0}:</color> <color=#FFD700><b>[ 🔒 GİZLİ BİRLİK ]</b></color>", i + 1);
                    GUI.Label(new Rect(sRect.x + 8f, sRect.y + 1f, sRect.width - 12f, 18f), hiddenTxt, slotTextStyle);
                }
                else
                {
                    Texture2D uIcon = GetUnitIconTexture(unit);
                    if (uIcon != null) GUI.DrawTexture(new Rect(sRect.x + 4f, sRect.y + 1f, 18f, 18f), uIcon, ScaleMode.ScaleToFit);

                    string costBadge = (meta.faction == SVZMultiplayerManager.UnitFaction.Samurai) ? string.Format("{0}L", meta.cost) : string.Format("{0}B", meta.cost);
                    string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                    string rowLabel = string.Format("<color=#FFEE55><b>S{0}</b></color> {1} <color=#FFFFFF><b>{2}</b></color> <color=#66DDFF>({3})</color>", i + 1, tierBadge, meta.displayName, costBadge);
                    GUI.Label(new Rect(sRect.x + 26f, sRect.y + 1f, sRect.width - 30f, 18f), rowLabel, slotTextStyle);

                    if (isLocalP1 && GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                    {
                        mSelectedP1Slot = i;
                        mSelectedP2Slot = -1;
                        mSelectedP1SpellSlot = -1;
                        mSelectedP2SpellSlot = -1;
                        if (mCatalogFilter == 3) mCatalogFilter = 0;
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
                    }
                }
            }

            // P1 2-Spell Slots (Büyü 1 & Büyü 2)
            float p1SpellsY = p1SlotsY + 106f;
            float spellSlotW = (sideColW - 16f) * 0.5f;
            for (int s = 0; s < 2; s++)
            {
                string spId = (s < mpMgr.P1Spells.Count) ? mpMgr.P1Spells[s] : "KatanaSlash";
                SVZMultiplayerManager.SpellMeta spMeta = SVZMultiplayerManager.GetSpellMeta(spId);
                bool isSpActive = (mSelectedP1SpellSlot == s && mSelectedP2SpellSlot == -1);

                Rect spRect = new Rect(p1X + 6f + s * (spellSlotW + 4f), p1SpellsY, spellSlotW, 22f);
                GUI.color = isSpActive ? new Color(0.95f, 0.75f, 0.2f, 0.98f) : new Color(0.22f, 0.16f, 0.36f, 0.95f);
                GUI.DrawTexture(spRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (hideP1)
                {
                    GUI.Label(new Rect(spRect.x + 4f, spRect.y + 2f, spRect.width - 8f, 18f), "<color=#FFD700><b>[ 🔒 Gizli Büyü ]</b></color>", slotTextStyle);
                }
                else
                {
                    Texture2D spIcon = GetSpellIconTexture(spId);
                    if (spIcon != null) GUI.DrawTexture(new Rect(spRect.x + 3f, spRect.y + 2f, 18f, 18f), spIcon, ScaleMode.ScaleToFit);

                    string spLabel = string.Format("<color=#DDA0FF><b>B{0}:</b></color> <color=#FFFFFF><b>{1}</b></color>", s + 1, spMeta.displayName);
                    GUI.Label(new Rect(spRect.x + 23f, spRect.y + 2f, spRect.width - 25f, 18f), spLabel, slotTextStyle);

                    if (isLocalP1 && GUI.Button(spRect, GUIContent.none, GUIStyle.none))
                    {
                        mSelectedP1SpellSlot = s;
                        mSelectedP2SpellSlot = -1;
                        mSelectedP1Slot = -1;
                        mSelectedP2Slot = -1;
                        mCatalogFilter = 3;
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
                    }
                }
            }
        }

        // P1 Perk & Quotas
        float p1PerkY = colY + 224f;
        if (hideP1)
        {
            GUI.Label(new Rect(p1X + 8f, p1PerkY, sideColW - 16f, 18f), "<color=#AAAAAA>Yetenek:</color> <color=#FFD700><b>[ ❓ Gizli Yetenek ]</b></color>", slotTextStyle);
        }
        else
        {
            string perkLabel = string.Format("<color=#AAAAAA>Perk:</color> <color=#55FF55><b>{0}</b></color>", SVZMultiplayerManager.sP1PerkNames[mpMgr.P1Perk]);
            GUI.Label(new Rect(p1X + 8f, p1PerkY + 1f, sideColW - 84f, 18f), perkLabel, slotTextStyle);
            if (isLocalP1 && GUI.Button(new Rect(p1X + sideColW - 74f, p1PerkY, 64f, 19f), "Değiştir"))
            {
                mpMgr.P1Perk = (mpMgr.P1Perk + 1) % SVZMultiplayerManager.sP1PerkNames.Length;
                mpMgr.BroadcastLobbyPick(1, "PERK", 0, mpMgr.P1Perk.ToString());
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
            }
        }

        int p1Leg, p1Elite, p1Core;
        SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P1Deck, out p1Leg, out p1Elite, out p1Core);
        int maxLegQ = isTenUnitMode ? 2 : 1;
        int maxEliteQ = isTenUnitMode ? 4 : 2;
        int maxTotalQ = isTenUnitMode ? 10 : 5;
        string p1QuotaStr = isTenUnitMode
            ? string.Format("[<color=#FFD700>👑 {0}/{1}</color> | <color=#66CCFF>🛡️ {2}/{3}</color> | <color=#88FF88>⚔️ {4}/{5} Birlik</color> | <color=#55FFFF>⚡ 225 Smithy</color>]", p1Leg, maxLegQ, p1Elite, maxEliteQ, mpMgr.P1Deck.Count, maxTotalQ)
            : string.Format("[<color=#FFD700>👑 {0}/1</color> | <color=#66CCFF>🛡️ {1}/2</color> | <color=#88FF88>⚔️ {2}/5</color> | <color=#DDA0FF>🔮 2 Büyü</color>]", p1Leg, p1Elite, p1Core);
        GUI.Label(new Rect(p1X + 8f, p1PerkY + 20f, sideColW - 16f, 18f), p1QuotaStr, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 9, alignment = TextAnchor.MiddleCenter });

        // --- CENTER COLUMN: ODA VE SAVAŞ AYARLARI (HOST KONTROLÜ) ---
        float cX = p1X + sideColW + 8f;
        GUI.color = new Color(0.11f, 0.13f, 0.19f, 0.95f);
        GUI.DrawTexture(new Rect(cX, colY, centerColW, colH), Texture2D.whiteTexture);
        GUI.color = new Color(1.0f, 0.8f, 0.25f, 0.9f);
        GUI.DrawTexture(new Rect(cX, colY, centerColW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cX + 8f, colY + 5f, centerColW - 16f, 18f), "⚙️ <b>ODA VE SAVAŞ AYARLARI (HOST KONTROLÜ)</b>", teamHeader);

        float curCY = colY + 25f;

        // 1. BLIND PICK / RAKİP SEÇİMLERİNİ GÖRME TOGGLE
        string blindPickText = mpMgr.BlindPick
            ? "<color=#FFAA33><b>🕶️ KÖR SEÇİM (BLIND PICK): AÇIK</b></color>\n<size=9><color=#CCCCCC>Rakip seçimleri maç başlayana kadar gizlidir ([ ? Gizli ])</color></size>"
            : "<color=#55FF55><b>👁️ AÇIK SEÇİM: CANLI GÖRÜNÜR</b></color>\n<size=9><color=#CCCCCC>İki tarafın seçtiği hero, birlik ve büyüler canlı izlenir</color></size>";

        GUI.color = mpMgr.BlindPick ? new Color(0.40f, 0.25f, 0.15f, 0.95f) : new Color(0.15f, 0.35f, 0.25f, 0.95f);
        GUI.DrawTexture(new Rect(cX + 6f, curCY, centerColW - 12f, 36f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle blindStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
        GUI.Label(new Rect(cX + 12f, curCY + 2f, centerColW - 140f, 32f), blindPickText, blindStyle);

        if (isHost || isSamePC)
        {
            GUI.color = mpMgr.BlindPick ? new Color(0.95f, 0.70f, 0.2f) : new Color(0.25f, 0.85f, 0.45f);
            if (GUI.Button(new Rect(cX + centerColW - 120f, curCY + 5f, 112f, 26f), mpMgr.BlindPick ? "Açık Yap" : "Kör Seçim Yap"))
            {
                mpMgr.BlindPick = !mpMgr.BlindPick;
                mpMgr.BroadcastLobbyConfig();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
            GUI.color = Color.white;
        }

        curCY += 40f;

        // 2. Arena Cycle Selector
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Harita:</b>", teamHeader);
        int arenaIdx = Array.IndexOf(SVZMultiplayerManager.sArenaScenes, mpMgr.SelectedArena);
        if (arenaIdx < 0) arenaIdx = 0;

        if (isHost || isSamePC)
        {
            if (GUI.Button(new Rect(cX + 85f, curCY, 28f, 20f), "<"))
            {
                arenaIdx = (arenaIdx - 1 + SVZMultiplayerManager.sArenaScenes.Length) % SVZMultiplayerManager.sArenaScenes.Length;
                mpMgr.SelectedArena = SVZMultiplayerManager.sArenaScenes[arenaIdx];
                mpMgr.BroadcastLobbyConfig();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
            }
        }

        GUI.color = new Color(0.16f, 0.20f, 0.28f);
        GUI.DrawTexture(new Rect(cX + 116f, curCY, centerColW - 150f, 20f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle arenaLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 10, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(cX + 116f, curCY, centerColW - 150f, 20f), string.Format("<color=#55FFFF>{0}</color>", SVZMultiplayerManager.sArenaNames[arenaIdx]), arenaLabelStyle);

        if (isHost || isSamePC)
        {
            if (GUI.Button(new Rect(cX + centerColW - 32f, curCY, 28f, 20f), ">"))
            {
                arenaIdx = (arenaIdx + 1) % SVZMultiplayerManager.sArenaScenes.Length;
                mpMgr.SelectedArena = SVZMultiplayerManager.sArenaScenes[arenaIdx];
                mpMgr.BroadcastLobbyConfig();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
            }
        }

        curCY += 24f;

        // 3. Game Mode Buttons (Kahramanlı 5 Birlik vs Herosuz 10 Birlik)
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Oyun Modu:</b>", slotTextStyle);
        bool isHeroMode = (mpMgr.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy);
        if (isHost || isSamePC)
        {
            GUI.color = isHeroMode ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.2f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cX + 85f, curCY, (centerColW - 95f) * 0.5f, 20f), "🗡️ Kahramanlı (5 Birlik)"))
            {
                mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy;
                if (mSelectedP1Slot >= 5) mSelectedP1Slot = 0;
                if (mSelectedP2Slot >= 5) mSelectedP2Slot = 0;
                mpMgr.BroadcastLobbyConfig();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
            GUI.color = !isHeroMode ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.2f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cX + 90f + (centerColW - 95f) * 0.5f, curCY, (centerColW - 95f) * 0.5f, 20f), "🎖️ 10 Birlik (Herosuz)"))
            {
                mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly;
                mSelectedP1SpellSlot = -1;
                mSelectedP2SpellSlot = -1;
                if (mCatalogFilter == 3) mCatalogFilter = 0;
                if (mSelectedP1Slot < 0 && mSelectedP2Slot < 0) mSelectedP1Slot = 0;
                mpMgr.BroadcastLobbyConfig();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
            GUI.color = Color.white;
        }
        else
        {
            GUI.Label(new Rect(cX + 85f, curCY, centerColW - 95f, 20f), isHeroMode ? "🗡️ Kahramanlı (5 Birlik)" : "🎖️ 10 Birlik Herosuz (225 Smithy)", slotTextStyle);
        }

        curCY += 24f;

        // 4. Gate Health & Energy Speed
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Kale Canı:</b>", slotTextStyle);
        float[] gateOpts = new float[] { 3000f, 5000f, 10000f, 15000f };
        float gw = (centerColW - 95f) / 4f;
        for (int g = 0; g < 4; g++)
        {
            bool isSel = Mathf.Approximately(mpMgr.GateMaxHealthSetting, gateOpts[g]);
            GUI.color = isSel ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.2f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cX + 85f + g * gw, curCY, gw - 3f, 20f), gateOpts[g].ToString("0")))
            {
                if (isHost || isSamePC)
                {
                    mpMgr.GateMaxHealthSetting = gateOpts[g];
                    mpMgr.BroadcastLobbyConfig();
                }
            }
        }
        GUI.color = Color.white;

        curCY += 23f;

        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Enerji Hızı:</b>", slotTextStyle);
        if (isTenUnitMode)
        {
            GUI.color = new Color(0.14f, 0.28f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(cX + 85f, curCY, centerColW - 95f, 20f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(cX + 90f, curCY + 1f, centerColW - 105f, 18f),
                "<color=#55FF55><b>⚡ SABİT MAKSİMUM: 225 Smithy | Saniyede +8 Dolum</b></color>", slotTextStyle);
        }
        else
        {
            float[] spdOpts = new float[] { 1.0f, 1.5f, 2.5f };
            string[] spdLbls = new string[] { "1.0x Normal", "1.5x Hızlı", "2.5x Turbo" };
            float sw = (centerColW - 95f) / 3f;
            for (int s = 0; s < 3; s++)
            {
                bool isSel = Mathf.Approximately(mpMgr.EnergySpeed, spdOpts[s]);
                GUI.color = isSel ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.2f, 0.22f, 0.28f);
                if (GUI.Button(new Rect(cX + 85f + s * sw, curCY, sw - 3f, 20f), spdLbls[s]))
                {
                    if (isHost || isSamePC)
                    {
                        mpMgr.EnergySpeed = spdOpts[s];
                        mpMgr.BroadcastLobbyConfig();
                    }
                }
            }
            GUI.color = Color.white;
        }

        curCY += 23f;

        // 5. Equal Troop Level Selector (Lv.1 / Lv.5 / Lv.10 - Karşı Taraf Onaylı Eşit Seviye)
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Birlik Lvl:</b>", slotTextStyle);
        int localPlayerNum = isClient ? 2 : 1;
        if (mpMgr.PendingTroopLevelProposal > 0 && mpMgr.PendingTroopLevelProposer != localPlayerNum)
        {
            float approveW = (centerColW - 95f) * 0.68f;
            GUI.color = new Color(0.20f, 0.85f, 0.40f);
            if (GUI.Button(new Rect(cX + 85f, curCY, approveW, 20f), string.Format("✅ Rakip Teklifi: Lv.{0} ONAYLA", mpMgr.PendingTroopLevelProposal)))
            {
                mpMgr.ApproveTroopLevelProposal();
            }
            GUI.color = new Color(0.90f, 0.30f, 0.30f);
            if (GUI.Button(new Rect(cX + 88f + approveW, curCY, (centerColW - 95f) - approveW - 3f, 20f), "❌ Reddet"))
            {
                mpMgr.RejectTroopLevelProposal();
            }
            GUI.color = Color.white;
        }
        else
        {
            int[] lvlOpts = new int[] { 1, 5, 10 };
            float lw = (centerColW - 95f) / 3f;
            for (int l = 0; l < 3; l++)
            {
                int optLvl = lvlOpts[l];
                bool isAgreed = (mpMgr.SharedTroopLevel == optLvl && mpMgr.PendingTroopLevelProposal == 0);
                bool isProposedByMe = (mpMgr.PendingTroopLevelProposal == optLvl && mpMgr.PendingTroopLevelProposer == localPlayerNum);
                GUI.color = isAgreed
                    ? new Color(0.25f, 0.85f, 0.45f)
                    : (isProposedByMe ? new Color(0.95f, 0.65f, 0.20f) : new Color(0.2f, 0.22f, 0.28f));
                string lvlBtnText = isProposedByMe
                    ? string.Format("⏳ Lv.{0} (Onay Bekliyor)", optLvl)
                    : (isAgreed ? string.Format("⚔️ Lv.{0} (Eşit)", optLvl) : string.Format("Lv.{0} Seç", optLvl));
                if (GUI.Button(new Rect(cX + 85f + l * lw, curCY, lw - 3f, 20f), lvlBtnText))
                {
                    mpMgr.RequestOrSetTroopLevel(optLvl, localPlayerNum);
                }
            }
            GUI.color = Color.white;
        }

        curCY += 23f;

        // 6. Quick Deck Presets
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Şablonlar:</b>", slotTextStyle);
        float pw = (centerColW - 95f) / 5f;
        string[] pNames = new string[] { "Dengeli", "Zırhlı", "Hızlı", "Zombi", "Karma" };
        for (int p = 0; p < 5; p++)
        {
            if (GUI.Button(new Rect(cX + 85f + p * pw, curCY, pw - 3f, 20f), pNames[p]))
            {
                int targetP = isLocalP2 && !isLocalP1 ? 2 : 1;
                mpMgr.ApplyDeckPreset(p);
                int count = (targetP == 1) ? mpMgr.P1Deck.Count : mpMgr.P2Deck.Count;
                for (int slot = 0; slot < count; slot++)
                {
                    string uid = (targetP == 1) ? mpMgr.P1Deck[slot] : mpMgr.P2Deck[slot];
                    mpMgr.BroadcastLobbyPick(targetP, "SLOT", slot, uid);
                }
                for (int spSlot = 0; spSlot < 2; spSlot++)
                {
                    string spId = (targetP == 1) ? mpMgr.P1Spells[spSlot] : mpMgr.P2Spells[spSlot];
                    mpMgr.BroadcastLobbyPick(targetP, "SPELL", spSlot, spId);
                }
                SVZMultiplayerManager.PlaySFX("UI_Equip_01", 0.8f);
            }
        }

        curCY += 23f;

        // 7. Back-Attack Toggle (Arkaya Dönüp Vurma Ayarı)
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Arkaya Vurma:</b>", slotTextStyle);
        GUI.color = mpMgr.AllowBackAttack ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.22f, 0.75f, 0.42f);
        string backAtkLbl = mpMgr.AllowBackAttack
            ? "🔄 AÇIK (Arkasını Dönüp Vurabilir)"
            : "🚫 KAPALI (Sadece Önündekine Vurur)";
        if (GUI.Button(new Rect(cX + 85f, curCY, centerColW - 95f, 20f), backAtkLbl))
        {
            if (isHost || isSamePC)
            {
                mpMgr.AllowBackAttack = !mpMgr.AllowBackAttack;
                mpMgr.BroadcastLobbyConfig();
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
        }
        GUI.color = Color.white;

        curCY += 22f;

        // 8. Elite Unit Limits Removal Toggle (Elit Birlik Sınırlamaları)
        GUI.Label(new Rect(cX + 8f, curCY, 80f, 20f), "<b>Elit Sınırı:</b>", slotTextStyle);
        GUI.color = mpMgr.DisableEliteLimits ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.22f, 0.65f, 0.85f);
        string eliteLimitLbl = mpMgr.DisableEliteLimits
            ? "⚡ KALDIRILDI (Sınırsız Elit/Boss)"
            : "🛡️ STANDART (Maks 1 Boss / 2 Elit)";
        if (GUI.Button(new Rect(cX + 85f, curCY, centerColW - 95f, 20f), eliteLimitLbl))
        {
            if (isHost || isSamePC)
            {
                mpMgr.ToggleDisableEliteLimits();
            }
        }
        GUI.color = Color.white;

        curCY += 24f;

        // 9. True Random Set Generator Buttons (Rastgele Set Oluşturucu)
        GUI.color = new Color(0.95f, 0.55f, 0.10f);
        if (GUI.Button(new Rect(cX + 8f, curCY, centerColW - 16f, 24f), "<b>🎲 2 TARAFA RASTGELE SET OLUŞTUR</b>", new GUIStyle(GUI.skin.button) { richText = true, fontStyle = FontStyle.Bold }))
        {
            if (isHost || isSamePC)
            {
                mpMgr.GenerateRandomSetsForBothSides();
            }
        }
        GUI.color = Color.white;

        curCY += 26f;
        float halfCenterW = (centerColW - 20f) * 0.5f;
        GUI.color = new Color(0.22f, 0.75f, 0.42f);
        if (GUI.Button(new Rect(cX + 8f, curCY, halfCenterW, 20f), "🎲 Rastgele P1 Seti"))
        {
            if (isLocalP1 || isHost || isSamePC)
            {
                mpMgr.GenerateRandomSet(1);
            }
        }
        GUI.color = new Color(0.85f, 0.35f, 0.35f);
        if (GUI.Button(new Rect(cX + 12f + halfCenterW, curCY, halfCenterW, 20f), "🎲 Rastgele P2 Seti"))
        {
            if (isLocalP2 || isHost || isSamePC)
            {
                mpMgr.GenerateRandomSet(2);
            }
        }
        GUI.color = Color.white;

        curCY += 22f;

        // Warning banner
        if (mDeckWarningTimer > 0f && !string.IsNullOrEmpty(mDeckWarningMessage))
        {
            GUI.Label(new Rect(cX + 8f, curCY, centerColW - 16f, 20f), "<color=#FF4444><b>⚠️ " + mDeckWarningMessage + "</b></color>", new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 10 });
        }

        // --- RED SIDE: TAKIM 2 (KIRMIZI KÖŞE - 2. OYUNCU / CLIENT / BOT) ---
        float p2X = cx + cw - sideColW;
        GUI.color = new Color(0.20f, 0.10f, 0.12f, 0.95f);
        GUI.DrawTexture(new Rect(p2X, colY, sideColW, colH), Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.35f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(p2X, colY, sideColW, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string p2Title;
        if (isRemoteConnected && isHost)
        {
            p2Title = mpMgr.IsP2Ready
                ? "<color=#FF7777><b>🔴 2. OYUNCU (ONLINE)</b></color> <color=#55FF55>🟢 BAĞLI | HAZIR!</color>"
                : "<color=#FF7777><b>🔴 2. OYUNCU (ONLINE)</b></color> <color=#55FF55>🟢 BAĞLI</color> <color=#FFAA33>(⏳ Seçim Yapıyor...)</color>";
        }
        else if (isClient)
        {
            p2Title = (net != null && net.IsConnected)
                ? "<color=#FF7777><b>🔴 2. OYUNCU (SİZSİNİZ)</b></color> <color=#55FF55>🟢 ODA BAĞLI</color>"
                : "<color=#FF5555><b>🔴 2. OYUNCU (BAĞLANTI KESİLDİ)</b></color>";
        }
        else if (mpMgr.P2AutoBot)
        {
            p2Title = "<color=#FFAA33><b>🤖 2. OYUNCU (YAPAY ZEKA BOT)</b></color>";
        }
        else if (isSamePC)
        {
            p2Title = "<color=#FF7777><b>🔴 2. OYUNCU (P2 HOTSEAT)</b></color>";
        }
        else
        {
            p2Title = "<color=#FFAA33><b>🔴 2. OYUNCU (BEKLENİYOR...)</b></color>";
        }

        GUI.Label(new Rect(p2X + 8f, colY + 5f, sideColW - 100f, 18f), p2Title, teamHeader);

        // If waiting and no remote player connected, show [Bot Ata] button
        if (!isClient && !mpMgr.P2AutoBot && !isSamePC && !isRemoteConnected)
        {
            GUI.color = new Color(0.95f, 0.65f, 0.2f);
            if (GUI.Button(new Rect(p2X + sideColW - 90f, colY + 3f, 82f, 22f), "🤖 Bot Ata"))
            {
                mpMgr.P2AutoBot = true;
                SVZMultiplayerManager.PlaySFX("UI_Upgrade_01", 0.8f);
            }
            GUI.color = Color.white;
        }

        bool hideP2 = ShouldHideP2PicksFromLocal();

        if (isTenUnitMode)
        {
            // 10-UNIT HEROLESS MODE FOR P2
            float p2ModeBoxY = colY + 25f;
            GUI.color = new Color(0.30f, 0.14f, 0.18f, 0.95f);
            GUI.DrawTexture(new Rect(p2X + 6f, p2ModeBoxY, sideColW - 12f, 24f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(p2X + 10f, p2ModeBoxY + 3f, sideColW - 20f, 18f),
                "<color=#FF8888><b>🎖️ HEROSUZ 10 BİRLİK ORDUSU</b></color> <size=9><color=#FFEE55>(Smithy: 225 | +8/sn)</color></size>", slotTextStyle);

            float gridY = p2ModeBoxY + 27f;
            float halfSlotW = (sideColW - 16f) * 0.5f;
            for (int i = 0; i < mpMgr.P2Deck.Count; i++)
            {
                string unit = mpMgr.P2Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP2Slot == i && mSelectedP1Slot == -1);
                int col = i / 5;
                int row = i % 5;

                Rect sRect = new Rect(p2X + 6f + col * (halfSlotW + 4f), gridY + row * 31f, halfSlotW, 29f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.24f, 0.15f, 0.18f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (hideP2)
                {
                    GUI.Label(new Rect(sRect.x + 6f, sRect.y + 5f, sRect.width - 10f, 18f),
                        string.Format("<color=#AAAAAA>S{0}:</color> <color=#FFD700><b>[🔒 GİZLİ]</b></color>", i + 1), slotTextStyle);
                }
                else
                {
                    Texture2D uIcon = GetUnitIconTexture(unit);
                    if (uIcon != null) GUI.DrawTexture(new Rect(sRect.x + 3f, sRect.y + 3f, 22f, 22f), uIcon, ScaleMode.ScaleToFit);

                    string costBadge = (meta.faction == SVZMultiplayerManager.UnitFaction.Zombie) ? string.Format("{0}B", meta.cost) : string.Format("{0}L", meta.cost);
                    string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                    string rowLabel = string.Format("<color=#FFEE55><b>S{0}</b></color>{1} <color=#FFFFFF><b>{2}</b></color>\n<size=8><color=#FF8888>{3}</color></size>", i + 1, tierBadge, meta.displayName, costBadge);
                    GUI.Label(new Rect(sRect.x + 27f, sRect.y + 1f, sRect.width - 29f, 26f), rowLabel, slotTextStyle);

                    if (isLocalP2 && GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                    {
                        mSelectedP2Slot = i;
                        mSelectedP1Slot = -1;
                        mSelectedP1SpellSlot = -1;
                        mSelectedP2SpellSlot = -1;
                        if (mCatalogFilter == 3) mCatalogFilter = 0;
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
                    }
                }
            }
        }
        else
        {
            // P2 Hero Box
            float p2HeroY = colY + 26f;
            GUI.color = new Color(0.28f, 0.15f, 0.18f, 0.95f);
            GUI.DrawTexture(new Rect(p2X + 6f, p2HeroY, sideColW - 12f, 44f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (hideP2)
            {
                GUI.Label(new Rect(p2X + 12f, p2HeroY + 6f, sideColW - 24f, 32f),
                    "<color=#FFD700><b>[ ❓ GİZLİ KAHRAMAN ]</b></color>\n<size=9><color=#AAAAAA>Kör Seçim Modu: Savaş Başlayınca Görünür</color></size>", new GUIStyle(GUI.skin.label) { richText = true });
            }
            else
            {
                Texture2D h2Icon = GetHeroIconTexture(mpMgr.P2HeroType);
                if (h2Icon != null) GUI.DrawTexture(new Rect(p2X + 10f, p2HeroY + 4f, 36f, 36f), h2Icon, ScaleMode.ScaleToFit);

                string h2Name = (mpMgr.P2HeroType == SVZMultiplayerManager.HeroType.Samurai) ? "🗡️ Samuray Şampiyonu" : "💀 Zombi Kahramanı";
                string h2SpellTag = (mpMgr.P2HeroType == SVZMultiplayerManager.HeroType.Samurai) ? "Samuray Büyüleri" : "Zombi Büyüleri";
                string h2Info = string.Format("<color=#FFEE55><b>{0}</b></color>\n<size=9><color=#FF8888>4000 HP | {1}</color></size>", h2Name, h2SpellTag);
                GUI.Label(new Rect(p2X + 52f, p2HeroY + 5f, sideColW - 130f, 34f), h2Info, new GUIStyle(GUI.skin.label) { richText = true });

                if (isLocalP2)
                {
                    if (GUI.Button(new Rect(p2X + sideColW - 74f, p2HeroY + 9f, 64f, 26f), "Değiştir"))
                    {
                        mpMgr.P2HeroType = (mpMgr.P2HeroType == SVZMultiplayerManager.HeroType.Samurai) ? SVZMultiplayerManager.HeroType.Zombie : SVZMultiplayerManager.HeroType.Samurai;
                        mpMgr.EnsureValidSpellsForPlayer(2);
                        mpMgr.BroadcastLobbyPick(2, "HERO", 0, mpMgr.P2HeroType.ToString());
                        for (int spIdx = 0; spIdx < mpMgr.P2Spells.Count; spIdx++)
                        {
                            mpMgr.BroadcastLobbyPick(2, "SPELL", spIdx, mpMgr.P2Spells[spIdx]);
                        }
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                    }
                }
            }

            // P2 5-Deck Slots (compact 20px rows to fit 2 Spell Slots underneath)
            float p2SlotsY = p2HeroY + 46f;
            for (int i = 0; i < mpMgr.P2Deck.Count; i++)
            {
                string unit = mpMgr.P2Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP2Slot == i && mSelectedP1SpellSlot == -1 && mSelectedP2SpellSlot == -1);

                Rect sRect = new Rect(p2X + 6f, p2SlotsY + i * 21f, sideColW - 12f, 20f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.24f, 0.15f, 0.18f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (hideP2)
                {
                    string hiddenTxt = string.Format("<color=#AAAAAA>Slot {0}:</color> <color=#FFD700><b>[ 🔒 GİZLİ BİRLİK ]</b></color>", i + 1);
                    GUI.Label(new Rect(sRect.x + 8f, sRect.y + 1f, sRect.width - 12f, 18f), hiddenTxt, slotTextStyle);
                }
                else
                {
                    Texture2D uIcon = GetUnitIconTexture(unit);
                    if (uIcon != null) GUI.DrawTexture(new Rect(sRect.x + 4f, sRect.y + 1f, 18f, 18f), uIcon, ScaleMode.ScaleToFit);

                    string costBadge = (meta.faction == SVZMultiplayerManager.UnitFaction.Zombie) ? string.Format("{0}B", meta.cost) : string.Format("{0}L", meta.cost);
                    string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                    string rowLabel = string.Format("<color=#FFEE55><b>S{0}</b></color> {1} <color=#FFFFFF><b>{2}</b></color> <color=#FF8888>({3})</color>", i + 1, tierBadge, meta.displayName, costBadge);
                    GUI.Label(new Rect(sRect.x + 26f, sRect.y + 1f, sRect.width - 30f, 18f), rowLabel, slotTextStyle);

                    if (isLocalP2 && GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                    {
                        mSelectedP2Slot = i;
                        mSelectedP1Slot = -1;
                        mSelectedP1SpellSlot = -1;
                        mSelectedP2SpellSlot = -1;
                        if (mCatalogFilter == 3) mCatalogFilter = 0;
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
                    }
                }
            }

            // P2 2-Spell Slots (Büyü 1 & Büyü 2)
            float p2SpellsY = p2SlotsY + 106f;
            float spellSlotW = (sideColW - 16f) * 0.5f;
            for (int s = 0; s < 2; s++)
            {
                string spId = (s < mpMgr.P2Spells.Count) ? mpMgr.P2Spells[s] : "DragonAttack";
                SVZMultiplayerManager.SpellMeta spMeta = SVZMultiplayerManager.GetSpellMeta(spId);
                bool isSpActive = (mSelectedP2SpellSlot == s);

                Rect spRect = new Rect(p2X + 6f + s * (spellSlotW + 4f), p2SpellsY, spellSlotW, 22f);
                GUI.color = isSpActive ? new Color(0.95f, 0.75f, 0.2f, 0.98f) : new Color(0.32f, 0.16f, 0.28f, 0.95f);
                GUI.DrawTexture(spRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                if (hideP2)
                {
                    GUI.Label(new Rect(spRect.x + 4f, spRect.y + 2f, spRect.width - 8f, 18f), "<color=#FFD700><b>[ 🔒 Gizli Büyü ]</b></color>", slotTextStyle);
                }
                else
                {
                    Texture2D spIcon = GetSpellIconTexture(spId);
                    if (spIcon != null) GUI.DrawTexture(new Rect(spRect.x + 3f, spRect.y + 2f, 18f, 18f), spIcon, ScaleMode.ScaleToFit);

                    string spLabel = string.Format("<color=#FFAAEE><b>B{0}:</b></color> <color=#FFFFFF><b>{1}</b></color>", s + 1, spMeta.displayName);
                    GUI.Label(new Rect(spRect.x + 23f, spRect.y + 2f, spRect.width - 25f, 18f), spLabel, slotTextStyle);

                    if (isLocalP2 && GUI.Button(spRect, GUIContent.none, GUIStyle.none))
                    {
                        mSelectedP2SpellSlot = s;
                        mSelectedP1SpellSlot = -1;
                        mSelectedP1Slot = -1;
                        mSelectedP2Slot = -1;
                        mCatalogFilter = 3;
                        SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
                    }
                }
            }
        }

        // P2 Perk & Ready Status
        float p2PerkY = colY + 224f;
        if (hideP2)
        {
            GUI.Label(new Rect(p2X + 8f, p2PerkY, sideColW - 16f, 18f), "<color=#AAAAAA>Yetenek:</color> <color=#FFD700><b>[ ❓ Gizli Yetenek ]</b></color>", slotTextStyle);
        }
        else
        {
            string perkLabel = string.Format("<color=#AAAAAA>Perk:</color> <color=#FF8888><b>{0}</b></color>", SVZMultiplayerManager.sP2PerkNames[mpMgr.P2Perk]);
            GUI.Label(new Rect(p2X + 8f, p2PerkY + 1f, sideColW - 84f, 18f), perkLabel, slotTextStyle);
            if (isLocalP2 && GUI.Button(new Rect(p2X + sideColW - 74f, p2PerkY, 64f, 19f), "Değiştir"))
            {
                mpMgr.P2Perk = (mpMgr.P2Perk + 1) % SVZMultiplayerManager.sP2PerkNames.Length;
                mpMgr.BroadcastLobbyPick(2, "PERK", 0, mpMgr.P2Perk.ToString());
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.7f);
            }
        }

        string p2ReadyStr;
        if (isRemoteConnected)
        {
            p2ReadyStr = mpMgr.IsP2Ready
                ? LocalizationManager.T("<color=#55FF55><b>P2 READY (CAN START)</b></color>", "<color=#55FF55><b>✅ 2. OYUNCU HAZIR (SAVAŞA GİREBİLİR)</b></color>")
                : LocalizationManager.T("<color=#FFAA33><b>P2 PICKING (NOT READY)</b></color>", "<color=#FFAA33><b>⏳ 2. OYUNCU SEÇİM YAPIYOR (HAZIR DEĞİL)</b></color>");
        }
        else if (isClient)
        {
            p2ReadyStr = mpMgr.IsP2Ready
                ? LocalizationManager.T("<color=#55FF55><b>YOU ARE READY</b></color>", "<color=#55FF55><b>✅ HAZIRSINIZ (KURUCU BAŞLATABİLİR)</b></color>")
                : LocalizationManager.T("<color=#FFAA33><b>PRESS READY WHEN DONE</b></color>", "<color=#FFAA33><b>⏳ HAZIRLANIN ('HAZIR' BUTONUNA BASIN)</b></color>");
        }
        else if (mpMgr.P2AutoBot)
        {
            p2ReadyStr = LocalizationManager.T("<color=#55FF55><b>AI BOT READY</b></color>", "<color=#55FF55><b>✅ YAPAY ZEKA BOT HAZIR</b></color>");
        }
        else if (isSamePC)
        {
            p2ReadyStr = LocalizationManager.T("<color=#55FF55><b>P2 HOTSEAT READY</b></color>", "<color=#55FF55><b>✅ P2 HOTSEAT HAZIR</b></color>");
        }
        else
        {
            p2ReadyStr = LocalizationManager.T("<color=#FF6666><b>WAITING FOR PLAYER 2...</b></color>", "<color=#FF6666><b>❌ 2. OYUNCU BEKLENİYOR...</b></color>");
        }
        GUI.Label(new Rect(p2X + 8f, p2PerkY + 20f, sideColW - 16f, 18f), p2ReadyStr, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10, alignment = TextAnchor.MiddleCenter });

        // 3. LOWER SECTION: CATALOG (PICK PHASE) + LOBBY CHAT & LAUNCH BUTTON
        float botY = colY + colH + 8f;
        float botH = ch - (botY - cy);
        float catW = cw - 440f;
        float chatW = 430f;

        // --- CATALOG PICKER (Left side) ---
        DrawInteractiveCatalog(cx, botY, catW, botH);

        // --- CHAT & LAUNCH (Right side) ---
        float chX = cx + catW + 10f;
        GUI.color = new Color(0.10f, 0.12f, 0.18f, 0.95f);
        GUI.DrawTexture(new Rect(chX, botY, chatW, botH), Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.7f, 1.0f, 0.8f);
        GUI.DrawTexture(new Rect(chX, botY, chatW, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(chX + 8f, botY + 4f, chatW - 16f, 16f), LocalizationManager.T("<b>LOBBY CHAT & ALERTS</b>", "💬 <b>LOBİ SOHBETİ VE BİLDİRİMLER</b>"), teamHeader);

        // Chat Log ScrollView
        float chatBoxH = botH - 102f;
        Rect chatScrollRect = new Rect(chX + 6f, botY + 22f, chatW - 12f, chatBoxH);
        float chatContentH = Mathf.Max(chatBoxH, mpMgr.ChatMessages.Count * 17f + 10f);
        Rect chatContentRect = new Rect(0, 0, chatW - 28f, chatContentH);

        mLobbyChatScrollPos = GUI.BeginScrollView(chatScrollRect, mLobbyChatScrollPos, chatContentRect);
        GUIStyle chatStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true, wordWrap = true };
        for (int m = 0; m < mpMgr.ChatMessages.Count; m++)
        {
            GUI.Label(new Rect(4f, m * 17f, chatW - 36f, 18f), mpMgr.ChatMessages[m], chatStyle);
        }
        GUI.EndScrollView();

        // Chat Input Bar
        float chatInputY = botY + chatBoxH + 26f;
        GUI.SetNextControlName("LobbyChatInput");
        mLobbyChatInput = GUI.TextField(new Rect(chX + 6f, chatInputY, chatW - 84f, 22f), mLobbyChatInput);
        if (GUI.Button(new Rect(chX + chatW - 74f, chatInputY, 68f, 22f), LocalizationManager.T("Send", "Gönder")) ||
            (Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "LobbyChatInput"))
        {
            SendLobbyChatMessage();
        }

        // BIG LAUNCH / READY BUTTON
        float launchY = chatInputY + 28f;
        float launchH = botH - (launchY - botY) - 6f;

        if (isClient)
        {
            // Client Ready Check Button
            GUI.color = mpMgr.IsP2Ready ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.95f, 0.70f, 0.2f);
            string clientReadyLbl = mpMgr.IsP2Ready
                ? LocalizationManager.T("<b><size=15>READY!</size></b>\n<size=10>Waiting for host to start... (Click to cancel)</size>", "<b><size=15>✅ HAZIRSINIZ!</size></b>\n<size=10>Kurucunun oyunu başlatması bekleniyor... (İptal için tıkla)</size>")
                : LocalizationManager.T("<b><size=15>MARK READY</size></b>\n<size=10>Click when finished picking your deck</size>", "<b><size=15>⏳ HAZIR VER (HAZIRIM)</size></b>\n<size=10>Seçimleriniz bitince hazır olmak için tıklayın</size>");

            if (GUI.Button(new Rect(chX + 6f, launchY, chatW - 12f, launchH), clientReadyLbl))
            {
                mpMgr.SendReadyState(!mpMgr.IsP2Ready);
            }
            GUI.color = Color.white;
        }
        else
        {
            // Host / Local Launch Match Button
            bool canLaunch = false;
            string hostLaunchLbl = "";

            if (isRemoteConnected)
            {
                if (mpMgr.IsP2Ready)
                {
                    canLaunch = true;
                    GUI.color = new Color(0.25f, 0.85f, 0.45f);
                    hostLaunchLbl = LocalizationManager.T("<b><size=16>START MATCH (BOTH READY!)</size></b>\n<size=10>Enter the 1v1 Arena!</size>", "<b><size=16>⚔️ OYUNU BAŞLAT (HER İKİ OYUNCU HAZIR!)</size></b>\n<size=10>Savaşı Başlat ve 1v1 Arenaya Gir!</size>");
                }
                else
                {
                    canLaunch = false;
                    GUI.color = new Color(0.70f, 0.55f, 0.20f, 0.85f);
                    hostLaunchLbl = LocalizationManager.T("<b><size=14>P2 STILL PICKING...</size></b>\n<size=10>(Waiting for opponent to press Ready)</size>", "<b><size=14>⏳ 2. OYUNCU HAZIRLANIYOR...</size></b>\n<size=10>(Katılan oyuncu 'HAZIR' butonuna basmadan başlatılamaz)</size>");
                }
            }
            else if (isSamePC || mpMgr.P2AutoBot)
            {
                canLaunch = true;
                GUI.color = new Color(0.25f, 0.85f, 0.45f);
                hostLaunchLbl = LocalizationManager.T("<b><size=16>START MATCH</size></b>\n<size=10>Enter the Battlefield!</size>", "<b><size=16>⚔️ OYUNU BAŞLAT</size></b>\n<size=10>Savaşı Başlat ve Sahaya Gir!</size>");
            }
            else
            {
                canLaunch = false;
                GUI.color = new Color(0.65f, 0.65f, 0.65f, 0.8f);
                hostLaunchLbl = string.Format(LocalizationManager.T("<b><size=13>WAITING FOR P2...</size></b>\n<size=9>(Connect via {0})</size>", "<b><size=13>🔴 2. OYUNCU BEKLENİYOR...</size></b>\n<size=9>(Arkadaşınız {0} ile bağlandığında aktifleşir)</size>"), (net != null) ? net.GetLocalIPAddress() : "127.0.0.1");
            }

            if (GUI.Button(new Rect(chX + 6f, launchY, chatW - 12f, launchH), hostLaunchLbl))
            {
                if (!canLaunch)
                {
                    if (isRemoteConnected && !mpMgr.IsP2Ready)
                    {
                        mDeckWarningMessage = LocalizationManager.T("Player 2 is not Ready yet! Please wait.", "2. Oyuncu henüz 'HAZIR' vermedi! Lütfen hazır olmasını bekleyin.");
                        mDeckWarningTimer = 3.5f;
                        SVZMultiplayerManager.PlaySFX("UI_Cannot_Afford_01", 0.9f);
                    }
                    else
                    {
                        mDeckWarningMessage = LocalizationManager.T("No opponent connected yet!", "Odaya henüz rakip oyuncu bağlanmadı! Arkadaşınız bağlanınca başlatabilirsiniz.");
                        mDeckWarningTimer = 3.5f;
                        SVZMultiplayerManager.PlaySFX("UI_Cannot_Afford_01", 0.9f);
                    }
                }
                else
                {
                    mShowLobby = false;
                    mpMgr.StartMultiplayerMatch(!isRemoteConnected || isSamePC);
                }
            }
            GUI.color = Color.white;
        }
    }

    private void DrawInteractiveCatalog(float cx, float curY, float cw, float ch)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        bool isTenUnitMode = (mpMgr.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly);
        if (isTenUnitMode)
        {
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            if (mCatalogFilter == 3) mCatalogFilter = 0;
            if (mSelectedP1Slot < 0 && mSelectedP2Slot < 0) mSelectedP1Slot = 0;
        }

        GUI.color = new Color(0.10f, 0.12f, 0.18f, 0.95f);
        GUI.DrawTexture(new Rect(cx, curY, cw, ch), Texture2D.whiteTexture);
        GUI.color = new Color(1.0f, 0.8f, 0.25f, 0.8f);
        GUI.DrawTexture(new Rect(cx, curY, cw, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        bool isSpellMode = !isTenUnitMode && (mCatalogFilter == 3 || mSelectedP1SpellSlot >= 0 || mSelectedP2SpellSlot >= 0);
        bool isP1Spell = (mSelectedP1SpellSlot >= 0 || (mSelectedP2SpellSlot < 0 && mSelectedP2Slot < 0));
        SVZMultiplayerManager.HeroType activeSpellHero = isP1Spell ? mpMgr.P1HeroType : mpMgr.P2HeroType;
        List<SVZMultiplayerManager.SpellMeta> heroSpellList = SVZMultiplayerManager.GetSpellsForHero(activeSpellHero);

        string sideTitle;
        if (isSpellMode)
        {
            int spSlotNum = isP1Spell ? Mathf.Max(0, mSelectedP1SpellSlot) + 1 : Mathf.Max(0, mSelectedP2SpellSlot) + 1;
            string heroTag = (activeSpellHero == SVZMultiplayerManager.HeroType.Samurai)
                ? LocalizationManager.T("Samurai Spells Only", "🗡️ Sadece Samuray Büyüleri")
                : LocalizationManager.T("Zombie Spells Only", "💀 Sadece Zombi Büyüleri");
            sideTitle = isP1Spell
                ? string.Format(LocalizationManager.T("<color=#DDA0FF>PLAYER 1 - SPELL SLOT {0}</color> [{1} ({2})]", "<color=#DDA0FF>🔮 1. OYUNCU - BÜYÜ SLOT {0}</color> [{1} ({2})]"), spSlotNum, heroTag, heroSpellList.Count)
                : string.Format(LocalizationManager.T("<color=#FFAAEE>PLAYER 2 - SPELL SLOT {0}</color> [{1} ({2})]", "<color=#FFAAEE>🔮 2. OYUNCU - BÜYÜ SLOT {0}</color> [{1} ({2})]"), spSlotNum, heroTag, heroSpellList.Count);
        }
        else
        {
            bool isEditingP1 = (mSelectedP1Slot >= 0);
            string quotaHint = mpMgr.DisableEliteLimits
                ? LocalizationManager.T("<color=#FFEE55>[UNLIMITED ELITE/BOSS MODE]</color>", "<color=#FFEE55>[⚡ LİMİTSİZ ELİT/BOSS MODU]</color>")
                : (isTenUnitMode
                    ? LocalizationManager.T("[10-Unit Mode: Max 2 Legend, 4 Elite]", "[10 Birlik Modu: Maks. 2 Efsanevi, 4 Elit]")
                    : LocalizationManager.T("[5-Unit Mode: Max 1 Legend, 2 Elite]", "[5 Birlik Modu: Maks. 1 Efsanevi, 2 Elit]"));
            sideTitle = isEditingP1
                ? string.Format(LocalizationManager.T("<color=#55FF55>PLAYER 1 - SLOT {0} PICK</color> - {1}", "<color=#55FF55>1. OYUNCU - SLOT {0} SEÇİMİ</color> - {1}"), mSelectedP1Slot + 1, quotaHint)
                : string.Format(LocalizationManager.T("<color=#FF7777>PLAYER 2 - SLOT {0} PICK</color> - {1}", "<color=#FF7777>2. OYUNCU - SLOT {0} SEÇİMİ</color> - {1}"), mSelectedP2Slot + 1, quotaHint);
        }

        GUIStyle secTitle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 10 };
        GUI.Label(new Rect(cx + 8f, curY + 4f, 430f, 18f), sideTitle, secTitle);

        // Filter Tabs: [All] [Samurai] [Zombie] [Spells]
        float fx = cx + cw - 412f;
        GUI.color = (mCatalogFilter == 0 && !isSpellMode) ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.2f, 0.22f, 0.28f);
        if (GUI.Button(new Rect(fx, curY + 3f, 92f, 20f), string.Format(LocalizationManager.T("All ({0})", "Tümü ({0})"), SVZMultiplayerManager.sAllUnitMetas.Count)))
        {
            mCatalogFilter = 0;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            if (mSelectedP1Slot < 0 && mSelectedP2Slot < 0) mSelectedP1Slot = 0;
        }
        GUI.color = (mCatalogFilter == 1 && !isSpellMode) ? new Color(0.25f, 0.85f, 0.35f) : new Color(0.2f, 0.22f, 0.28f);
        if (GUI.Button(new Rect(fx + 96f, curY + 3f, 98f, 20f), string.Format(LocalizationManager.T("Samurai ({0})", "Samuray ({0})"), SVZMultiplayerManager.sUnitMetaP1.Count)))
        {
            mCatalogFilter = 1;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            if (mSelectedP1Slot < 0 && mSelectedP2Slot < 0) mSelectedP1Slot = 0;
        }
        GUI.color = (mCatalogFilter == 2 && !isSpellMode) ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.2f, 0.22f, 0.28f);
        if (GUI.Button(new Rect(fx + 198f, curY + 3f, 95f, 20f), string.Format(LocalizationManager.T("Zombie ({0})", "Zombi ({0})"), SVZMultiplayerManager.sUnitMetaP2.Count)))
        {
            mCatalogFilter = 2;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            if (mSelectedP1Slot < 0 && mSelectedP2Slot < 0) mSelectedP1Slot = 0;
        }
        if (!isTenUnitMode)
        {
            GUI.color = isSpellMode ? new Color(0.75f, 0.35f, 0.95f) : new Color(0.2f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(fx + 297f, curY + 3f, 110f, 20f), string.Format(LocalizationManager.T("Spells ({0})", "🔮 Büyüler ({0})"), heroSpellList.Count)))
            {
                mCatalogFilter = 3;
                if (mSelectedP1SpellSlot < 0 && mSelectedP2SpellSlot < 0)
                {
                    if (mSelectedP2Slot >= 0) mSelectedP2SpellSlot = 0;
                    else mSelectedP1SpellSlot = 0;
                }
            }
        }
        GUI.color = Color.white;

        if (isSpellMode)
        {
            List<SVZMultiplayerManager.SpellMeta> spellList = heroSpellList;
            int sCols = 5;
            int sRows = Mathf.CeilToInt((float)spellList.Count / sCols);
            float sCardW = (cw - 32f) / (float)sCols;
            float sCardH = 84f;
            float sViewH = ch - 30f;
            float sContentH = sRows * (sCardH + 4f) + 6f;

            Rect sScrollRect = new Rect(cx + 4f, curY + 26f, cw - 8f, sViewH);
            Rect sViewContentRect = new Rect(0, 0, cw - 26f, sContentH);

            mCatalogScrollPos = GUI.BeginScrollView(sScrollRect, mCatalogScrollPos, sViewContentRect);
            GUIStyle spCardStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 9, wordWrap = true };

            int targetSpellSlot = isP1Spell ? Mathf.Max(0, mSelectedP1SpellSlot) : Mathf.Max(0, mSelectedP2SpellSlot);
            List<string> activeSpells = isP1Spell ? mpMgr.P1Spells : mpMgr.P2Spells;

            for (int i = 0; i < spellList.Count; i++)
            {
                SVZMultiplayerManager.SpellMeta sp = spellList[i];
                int col = i % sCols;
                int row = i / sCols;

                Rect cRect = new Rect(col * (sCardW + 4f), row * (sCardH + 4f), sCardW, sCardH);
                bool isEquipped = activeSpells.Contains(sp.id);

                GUI.color = isEquipped ? new Color(0.32f, 0.20f, 0.48f, 0.98f) : new Color(0.16f, 0.14f, 0.26f, 0.96f);
                GUI.DrawTexture(cRect, Texture2D.whiteTexture);
                GUI.color = isEquipped ? new Color(0.95f, 0.75f, 0.25f, 0.95f) : new Color(0.75f, 0.40f, 0.95f, 0.85f);
                GUI.DrawTexture(new Rect(cRect.x, cRect.y, cRect.width, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D spIcon = GetSpellIconTexture(sp.id);
                if (spIcon != null)
                {
                    GUI.DrawTexture(new Rect(cRect.x + 3f, cRect.y + 14f, 44f, 44f), spIcon, ScaleMode.ScaleToFit);
                }

                string spText = string.Format("🔮 <color=#FFFFFF><b>{0}</b></color>\n<size=8><color=#FFEE55>Dolum: {1:0} sn</color> | <color=#66EEFF>{2}</color></size>\n<size=8><color=#CCCCCC>{3}</color></size>",
                    sp.displayName, sp.cooldown, sp.role, sp.description);
                GUI.Label(new Rect(cRect.x + 50f, cRect.y + 3f, cRect.width - 52f, cRect.height - 6f), spText, spCardStyle);

                if (GUI.Button(cRect, GUIContent.none, GUIStyle.none))
                {
                    int otherSlot = (targetSpellSlot == 0) ? 1 : 0;
                    if (otherSlot < activeSpells.Count && activeSpells[otherSlot] == sp.id)
                    {
                        // Swap slots so there is no duplicate spell collision
                        activeSpells[otherSlot] = activeSpells[targetSpellSlot];
                        mpMgr.BroadcastLobbyPick(isP1Spell ? 1 : 2, "SPELL", otherSlot, activeSpells[otherSlot]);
                    }
                    activeSpells[targetSpellSlot] = sp.id;
                    mpMgr.BroadcastLobbyPick(isP1Spell ? 1 : 2, "SPELL", targetSpellSlot, sp.id);
                    SVZMultiplayerManager.PlaySFX("UI_Equip_01", 0.9f);
                }
            }

            GUI.EndScrollView();
            GUI.color = Color.white;
            return;
        }

        bool isEditingP1Unit = (mSelectedP1Slot >= 0);

        // Filter units
        List<SVZMultiplayerManager.UnitMeta> displayList = new List<SVZMultiplayerManager.UnitMeta>();
        if (mCatalogFilter == 0) displayList = SVZMultiplayerManager.sAllUnitMetas;
        else if (mCatalogFilter == 1) displayList = SVZMultiplayerManager.sUnitMetaP1;
        else displayList = SVZMultiplayerManager.sUnitMetaP2;

        int cols = 5;
        int totalRows = Mathf.CeilToInt((float)displayList.Count / cols);
        float cardW = (cw - 32f) / (float)cols;
        float cardH = 88f;
        float viewH = ch - 30f;
        float contentH = totalRows * (cardH + 4f) + 6f;

        Rect scrollRect = new Rect(cx + 4f, curY + 26f, cw - 8f, viewH);
        Rect viewContentRect = new Rect(0, 0, cw - 26f, contentH);

        mCatalogScrollPos = GUI.BeginScrollView(scrollRect, mCatalogScrollPos, viewContentRect);
        GUIStyle cardTextStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.UpperLeft, richText = true, fontSize = 9, wordWrap = true };

        for (int i = 0; i < displayList.Count; i++)
        {
            SVZMultiplayerManager.UnitMeta u = displayList[i];
            int col = i % cols;
            int row = i / cols;

            float cardX = col * (cardW + 4f);
            float cardY = row * (cardH + 4f);
            Rect cRect = new Rect(cardX, cardY, cardW, cardH);

            bool isSamurai = (u.faction == SVZMultiplayerManager.UnitFaction.Samurai);
            GUI.color = isSamurai ? new Color(0.14f, 0.20f, 0.30f, 0.96f) : new Color(0.24f, 0.14f, 0.18f, 0.96f);
            GUI.DrawTexture(cRect, Texture2D.whiteTexture);
            GUI.color = isSamurai ? new Color(0.3f, 0.75f, 1.0f, 0.8f) : new Color(0.95f, 0.35f, 0.4f, 0.8f);
            GUI.DrawTexture(new Rect(cRect.x, cRect.y, cRect.width, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Texture2D uIcon = GetUnitIconTexture(u.id);
            if (uIcon != null)
            {
                GUI.DrawTexture(new Rect(cRect.x + 3f, cRect.y + 14f, 44f, 44f), uIcon, ScaleMode.ScaleToFit);
            }

            string tierBadge = SVZMultiplayerManager.GetTierBadge(u.tier);
            string costBadge = isSamurai ? string.Format("{0}L", u.cost) : string.Format("{0}B", u.cost);
            string cardLabel = string.Format("{0} <color=#FFFFFF><b>{1}</b></color> <color=#FFEE55>({2})</color>\n<size=8><color=#66EEFF>{3}</color></size>\n<size=8><color=#DDDDDD>HP:{4} ATK:{5} SPD:{6}</color></size>\n<size=8><color=#AAAAAA>{7}</color></size>",
                tierBadge, u.displayName, costBadge, u.role, u.hp, u.damage, u.speed, u.description);

            GUI.Label(new Rect(cRect.x + 50f, cRect.y + 3f, cRect.width - 52f, cRect.height - 6f), cardLabel, cardTextStyle);

            if (GUI.Button(cRect, GUIContent.none, GUIStyle.none))
            {
                List<string> targetDeck = isEditingP1Unit ? mpMgr.P1Deck : mpMgr.P2Deck;
                int targetSlot = isEditingP1Unit ? mSelectedP1Slot : mSelectedP2Slot;

                if (targetSlot >= 0 && targetSlot < targetDeck.Count)
                {
                    string failureReason;
                    if (!SVZMultiplayerManager.CanEquipUnitToDeck(targetDeck, targetSlot, u.id, out failureReason))
                    {
                        mDeckWarningMessage = failureReason;
                        mDeckWarningTimer = 4.5f;
                        SVZMultiplayerManager.PlaySFX("Hero_Samurai_Sword_Block_01", 1.0f);
                    }
                    else
                    {
                        targetDeck[targetSlot] = u.id;
                        mDeckWarningMessage = string.Empty;
                        mDeckWarningTimer = 0f;
                        SVZMultiplayerManager.PlaySFX("UI_Equip_01", 0.9f);

                        // Broadcast pick live to opponent!
                        int playerNum = isEditingP1Unit ? 1 : 2;
                        mpMgr.BroadcastLobbyPick(playerNum, "SLOT", targetSlot, u.id);
                    }
                }
            }
        }

        GUI.EndScrollView();
        GUI.color = Color.white;
    }

    #endregion

    #region Tab 0: Mac ve Arena Ayarlari

    private void DrawTab0Settings(float cx, float cy, float cw, float ch)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        float curY = cy;

        GUIStyle secTitle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 11 };

        // 1. GAME MODE & STARTING SIDE SELECTION
        bool isHeroMode = (mpMgr.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy);
        float sec1Height = isHeroMode ? 122f : 68f;

        GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.92f);
        GUI.DrawTexture(new Rect(cx, curY, cw, sec1Height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx + 8, curY + 4, cw - 16, 18), "<color=#FFEE55>1. OYUN MODU VE BASLANGIC CEPHESI SECIMI:</color>", secTitle);

        float modeBtnW = (cw - 36f) * 0.25f;
        float modeBtnH = 36f;

        // Mode: HeroAndArmy
        GUI.color = isHeroMode ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.22f, 0.25f, 0.32f);
        if (GUI.Button(new Rect(cx + 8, curY + 22, modeBtnW, modeBtnH),
            LocalizationManager.T("HERO MODE (5 Units)\n(Hero + 5 Units + 2 Spells)", "🗡️ KAHRAMANLI MOD (5 Birlik)\n(Kahraman + 5 Birlik + 2 Büyü)")))
        {
            mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy;
            if (mSelectedP1Slot >= 5) mSelectedP1Slot = 0;
            if (mSelectedP2Slot >= 5) mSelectedP2Slot = 0;
        }

        // Mode: ArmyOnly (10-Unit Heroless Mode)
        GUI.color = !isHeroMode ? new Color(0.2f, 0.85f, 0.4f) : new Color(0.22f, 0.25f, 0.32f);
        if (GUI.Button(new Rect(cx + 12 + modeBtnW, curY + 22, modeBtnW, modeBtnH),
            LocalizationManager.T("10-UNIT HEROLESS\n(10 Units | 225 Smithy | +8/s)", "🎖️ 10 BİRLİK HEROSUZ MOD\n(10 Birlik | 225 Smithy | +8/sn Hız)")))
        {
            mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            if (mCatalogFilter == 3) mCatalogFilter = 0;
        }

        // Side: Sol Kale
        bool isLeftGate = (mpMgr.P1Side == SVZMultiplayerManager.BattlefieldSide.LeftGate);
        GUI.color = isLeftGate ? new Color(0.3f, 0.75f, 0.95f) : new Color(0.22f, 0.25f, 0.32f);
        if (GUI.Button(new Rect(cx + 20 + modeBtnW * 2, curY + 22, modeBtnW, modeBtnH),
            LocalizationManager.T("LEFT GATE (Classic)\n(P1 Left Gate, P2 Right)", "SOL KALE (Klasik)\n(P1 Sol Kalede, P2 Sagda)")))
        {
            mpMgr.P1Side = SVZMultiplayerManager.BattlefieldSide.LeftGate;
        }

        // Side: Sag Kale
        GUI.color = !isLeftGate ? new Color(0.3f, 0.75f, 0.95f) : new Color(0.22f, 0.25f, 0.32f);
        if (GUI.Button(new Rect(cx + 24 + modeBtnW * 3, curY + 22, modeBtnW, modeBtnH),
            LocalizationManager.T("RIGHT GATE (Invader)\n(P1 Right Gate, P2 Left)", "SAG KALE (Istilaci)\n(P1 Sag Kalede, P2 Solda)")))
        {
            mpMgr.P1Side = SVZMultiplayerManager.BattlefieldSide.RightGate;
        }
        GUI.color = Color.white;

        if (isHeroMode)
        {
            float heroRowY = curY + 62f;
            GUIStyle heroBadgeStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 10, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(cx + 8, heroRowY, cw - 16, 16),
                LocalizationManager.T("<color=#55FFFF>HERO SELECT (4000 HP | Each Hero uses its own Faction Spells):</color>", "<color=#55FFFF>👑 KAHRAMAN SEÇİMİ (Sabit 4000 HP | Her Kahraman Yalnızca Kendi Büyülerini Kullanır!):</color>"), heroBadgeStyle);

            heroRowY += 18f;

            // P1 Hero: Samurai
            bool p1Samurai = (mpMgr.P1HeroType == SVZMultiplayerManager.HeroType.Samurai);
            GUI.color = p1Samurai ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.20f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cx + 8, heroRowY, modeBtnW, 34f),
                LocalizationManager.T("1P: Samurai Hero\n<size=9>Samurai Spells Only</size>", "1P: 🗡️ Samuray Kahramanı\n<size=9>Sadece Samuray Büyüleri</size>")))
            {
                mpMgr.P1HeroType = SVZMultiplayerManager.HeroType.Samurai;
                mpMgr.EnsureValidSpellsForPlayer(1);
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }

            // P1 Hero: Zombie
            GUI.color = !p1Samurai ? new Color(0.85f, 0.35f, 0.75f) : new Color(0.20f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cx + 12 + modeBtnW, heroRowY, modeBtnW, 34f),
                LocalizationManager.T("1P: Zombie Hero\n<size=9>Zombie Spells Only</size>", "1P: 💀 Zombi Kahramanı\n<size=9>Sadece Zombi Büyüleri</size>")))
            {
                mpMgr.P1HeroType = SVZMultiplayerManager.HeroType.Zombie;
                mpMgr.EnsureValidSpellsForPlayer(1);
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }

            // P2 Hero: Zombie
            bool p2Zombie = (mpMgr.P2HeroType == SVZMultiplayerManager.HeroType.Zombie);
            GUI.color = p2Zombie ? new Color(0.85f, 0.35f, 0.75f) : new Color(0.20f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cx + 20 + modeBtnW * 2, heroRowY, modeBtnW, 34f),
                LocalizationManager.T("2P: Zombie Hero\n<size=9>Zombie Spells Only</size>", "2P: 💀 Zombi Kahramanı\n<size=9>Sadece Zombi Büyüleri</size>")))
            {
                mpMgr.P2HeroType = SVZMultiplayerManager.HeroType.Zombie;
                mpMgr.EnsureValidSpellsForPlayer(2);
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }

            // P2 Hero: Samurai
            GUI.color = !p2Zombie ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.20f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(cx + 24 + modeBtnW * 3, heroRowY, modeBtnW, 34f),
                LocalizationManager.T("2P: Samurai Champion\n<size=9>Samurai Spells Only</size>", "2P: 🗡️ Samuray Şampiyonu\n<size=9>Sadece Samuray Büyüleri</size>")))
            {
                mpMgr.P2HeroType = SVZMultiplayerManager.HeroType.Samurai;
                mpMgr.EnsureValidSpellsForPlayer(2);
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
            GUI.color = Color.white;
        }

        curY += sec1Height + 6f;

        // 2. ARENA / MAP SELECTOR GRID (10 Maps, 5 cols x 2 rows)
        GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.92f);
        GUI.DrawTexture(new Rect(cx, curY, cw, 172f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx + 8, curY + 4, cw - 16, 18),
            string.Format(LocalizationManager.T("<color=#FFEE55>2. SELECT ARENA:</color> (Active: <color=#55FFFF>{0}</color>)", "<color=#FFEE55>2. SAVAS ARENASI SEC:</color> (Aktif: <color=#55FFFF>{0}</color>)"), mpMgr.SelectedArena), secTitle);

        float mapBtnW = (cw - 36f) / 5f;
        float mapBtnH = 65f;
        for (int i = 0; i < SVZMultiplayerManager.sArenaScenes.Length; i++)
        {
            int col = i % 5;
            int row = i / 5;
            float bx = cx + 8 + col * (mapBtnW + 4);
            float by = curY + 24 + row * (mapBtnH + 6);

            bool isSelected = (mpMgr.SelectedArena == SVZMultiplayerManager.sArenaScenes[i]);
            GUI.color = isSelected ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.2f, 0.24f, 0.32f);

            string btnText = string.Format("<b>{0}</b>\n<size=9><color=#AAAAAA>{1}</color></size>",
                SVZMultiplayerManager.sArenaNames[i], SVZMultiplayerManager.sArenaScenes[i]);

            if (GUI.Button(new Rect(bx, by, mapBtnW, mapBtnH), btnText))
            {
                mpMgr.SelectedArena = SVZMultiplayerManager.sArenaScenes[i];
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
        }
        GUI.color = Color.white;
        curY += 178f;

        // 3. MATCH MUTATORS & COMMANDER PERKS
        GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.92f);
        GUI.DrawTexture(new Rect(cx, curY, cw, 195f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx + 8, curY + 4, cw - 16, 18),
            LocalizationManager.T("<color=#FFEE55>3. MATCH MUTATORS & COMMANDER PERKS:</color>", "<color=#FFEE55>3. MAC MUTATORLERI VE KOMUTAN YETENEKLERI (PERKS):</color>"), secTitle);

        float halfW = (cw - 24f) * 0.5f;

        // Left Column: Mutators & AI Bot Difficulty
        float my = curY + 26f;
        GUI.Label(new Rect(cx + 12, my, 130, 20), LocalizationManager.T("<b>Gate Health (HP):</b>", "<b>Kale Cani (HP):</b>"));
        if (GUI.Button(new Rect(cx + 145, my, 55, 22), "3,000")) mpMgr.GateMaxHealthSetting = 3000f;
        if (GUI.Button(new Rect(cx + 205, my, 55, 22), "5,000")) mpMgr.GateMaxHealthSetting = 5000f;
        if (GUI.Button(new Rect(cx + 265, my, 55, 22), "10,000")) mpMgr.GateMaxHealthSetting = 10000f;
        if (GUI.Button(new Rect(cx + 325, my, 55, 22), "15,000")) mpMgr.GateMaxHealthSetting = 15000f;

        my += 30f;
        GUI.Label(new Rect(cx + 12, my, 130, 20), LocalizationManager.T("<b>Energy Regen:</b>", "<b>Enerji Uretimi:</b>"));
        if (GUI.Button(new Rect(cx + 145, my, 65, 22), "1.0x Norm")) mpMgr.EnergySpeed = 1.0f;
        if (GUI.Button(new Rect(cx + 215, my, 65, 22), LocalizationManager.T("1.5x Fast", "1.5x Hizli"))) mpMgr.EnergySpeed = 1.5f;
        if (GUI.Button(new Rect(cx + 285, my, 65, 22), "2.5x Turbo")) mpMgr.EnergySpeed = 2.5f;

        my += 30f;
        GUI.Label(new Rect(cx + 12, my, 130, 20), LocalizationManager.T("<b>Bot Difficulty:</b>", "<b>Bot Zorlugu:</b>"));
        for (int b = 0; b < SVZMultiplayerManager.sBotDifficultyNames.Length; b++)
        {
            GUI.color = (mpMgr.BotDifficulty == b) ? new Color(0.95f, 0.68f, 0.18f) : new Color(0.22f, 0.24f, 0.30f);
            if (GUI.Button(new Rect(cx + 145 + b * 72f, my, 68f, 22f), SVZMultiplayerManager.sBotDifficultyNames[b]))
            {
                mpMgr.BotDifficulty = b;
            }
        }
        GUI.color = Color.white;

        my += 30f;
        GUI.Label(new Rect(cx + 12, my, 130, 20), LocalizationManager.T("<b>Elite Unit Limit:</b>", "<b>Elit Birlik Limiti:</b>"));
        GUI.color = !mpMgr.DisableEliteLimits ? new Color(0.22f, 0.65f, 0.85f) : new Color(0.22f, 0.24f, 0.30f);
        if (GUI.Button(new Rect(cx + 145, my, 100, 22), LocalizationManager.T("Standard", "🛡️ Standart"))) mpMgr.DisableEliteLimits = false;
        GUI.color = mpMgr.DisableEliteLimits ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.22f, 0.24f, 0.30f);
        if (GUI.Button(new Rect(cx + 250, my, 140, 22), LocalizationManager.T("Unlimited", "⚡ Sınırsız (Kaldır)"))) mpMgr.DisableEliteLimits = true;
        GUI.color = Color.white;

        // Right Column: Perks
        float px = cx + halfW + 12f;
        my = curY + 26f;
        GUI.Label(new Rect(px, my, halfW, 18), LocalizationManager.T("<color=#55FF55><b>Player 1 Commander Perk:</b></color>", "<color=#55FF55><b>1. Oyuncu Yetenegi (Perk):</b></color>"), new GUIStyle(GUI.skin.label) { richText = true });
        my += 22f;
        for (int p = 0; p < SVZMultiplayerManager.sP1PerkNames.Length; p++)
        {
            GUI.color = (mpMgr.P1Perk == p) ? new Color(0.2f, 0.85f, 0.35f) : new Color(0.2f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(px + p * (halfW / 3f), my, (halfW / 3f) - 4f, 28), SVZMultiplayerManager.sP1PerkNames[p]))
            {
                mpMgr.P1Perk = p;
            }
        }

        my += 36f;
        GUI.Label(new Rect(px, my, halfW, 18), LocalizationManager.T("<color=#FF7777><b>Player 2 Commander Perk:</b></color>", "<color=#FF7777><b>2. Oyuncu Yetenegi (Perk):</b></color>"), new GUIStyle(GUI.skin.label) { richText = true });
        my += 22f;
        for (int p = 0; p < SVZMultiplayerManager.sP2PerkNames.Length; p++)
        {
            GUI.color = (mpMgr.P2Perk == p) ? new Color(0.9f, 0.3f, 0.3f) : new Color(0.2f, 0.22f, 0.28f);
            if (GUI.Button(new Rect(px + p * (halfW / 3f), my, (halfW / 3f) - 4f, 28), SVZMultiplayerManager.sP2PerkNames[p]))
            {
                mpMgr.P2Perk = p;
            }
        }
        GUI.color = Color.white;
    }

    #endregion

    #region Tab 1: Ordu ve Deste Duzenleyici (Tum Birlikler)

    private void DrawTab1DeckBuilder(float cx, float cy, float cw, float ch)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        bool isTenUnitMode = (mpMgr.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly);
        float curY = cy;

        // 1. TROOP LEVEL CONTROLLERS & MODE SWITCH
        GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.92f);
        GUI.DrawTexture(new Rect(cx, curY, cw, 42f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle secTitle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 11 };
        GUI.Label(new Rect(cx + 8, curY + 11, 140, 20), "<color=#FFEE55>BIRLIK SEVIYELERI:</color>", secTitle);

        // P1 Level
        float lx = cx + 150f;
        GUI.Label(new Rect(lx, curY + 11, 75, 20), "<color=#55FF55><b>1. Oyuncu:</b></color>", new GUIStyle(GUI.skin.label) { richText = true });
        lx += 78f;
        if (GUI.Button(new Rect(lx, curY + 8, 24, 24), "-")) mpMgr.P1TroopLevel--;
        lx += 28f;
        GUI.Label(new Rect(lx, curY + 10, 42, 20), string.Format("<b>Lv.{0}</b>", mpMgr.P1TroopLevel), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });
        lx += 44f;
        if (GUI.Button(new Rect(lx, curY + 8, 24, 24), "+")) mpMgr.P1TroopLevel++;

        // P2 Level
        lx += 45f;
        GUI.Label(new Rect(lx, curY + 11, 75, 20), "<color=#FF7777><b>2. Oyuncu:</b></color>", new GUIStyle(GUI.skin.label) { richText = true });
        lx += 78f;
        if (GUI.Button(new Rect(lx, curY + 8, 24, 24), "-")) mpMgr.P2TroopLevel--;
        lx += 28f;
        GUI.Label(new Rect(lx, curY + 10, 42, 20), string.Format("<b>Lv.{0}</b>", mpMgr.P2TroopLevel), new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });
        lx += 44f;
        if (GUI.Button(new Rect(lx, curY + 8, 24, 24), "+")) mpMgr.P2TroopLevel++;

        // Presets Lv1, Lv5, Lv10
        lx += 30f;
        if (GUI.Button(new Rect(lx, curY + 8, 50, 24), "Lv.1")) { mpMgr.P1TroopLevel = 1; mpMgr.P2TroopLevel = 1; }
        if (GUI.Button(new Rect(lx + 54f, curY + 8, 50, 24), "Lv.5")) { mpMgr.P1TroopLevel = 5; mpMgr.P2TroopLevel = 5; }
        if (GUI.Button(new Rect(lx + 108f, curY + 8, 72, 24), "Lv.10 MAX")) { mpMgr.P1TroopLevel = 10; mpMgr.P2TroopLevel = 10; }

        // Mode Switch Buttons on the right of Top Bar
        GUI.color = !isTenUnitMode ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.2f, 0.22f, 0.28f);
        if (GUI.Button(new Rect(cx + cw - 335f, curY + 8, 155f, 24f), "🗡️ 5 Birlik (Kahramanlı)"))
        {
            mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.HeroAndArmy;
            if (mSelectedP1Slot >= 5) mSelectedP1Slot = 0;
            if (mSelectedP2Slot >= 5) mSelectedP2Slot = 0;
        }
        GUI.color = isTenUnitMode ? new Color(0.25f, 0.85f, 0.45f) : new Color(0.2f, 0.22f, 0.28f);
        if (GUI.Button(new Rect(cx + cw - 175f, curY + 8, 168f, 24f), "🎖️ 10 Birlik (Herosuz 225)"))
        {
            mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly;
            mSelectedP1SpellSlot = -1;
            mSelectedP2SpellSlot = -1;
            if (mCatalogFilter == 3) mCatalogFilter = 0;
        }
        GUI.color = Color.white;

        curY += 48f;

        // 2. DECKS DISPLAY (5-Slot + 2 Spells OR 10-Slot Heroless Grid)
        float deckBoxH = isTenUnitMode ? 128f : 106f;
        GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.92f);
        GUI.DrawTexture(new Rect(cx, curY, cw, deckBoxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx + 8, curY + 3, 140, 18), "<color=#FFEE55>DESTELERI DUZENLE:</color>", secTitle);

        // Deck Quick Presets & Randomize Buttons
        if (GUI.Button(new Rect(cx + 150, curY + 2, 60, 18), "Dengeli")) mpMgr.ApplyDeckPreset(0);
        if (GUI.Button(new Rect(cx + 213, curY + 2, 70, 18), "Zırhlı Boss")) mpMgr.ApplyDeckPreset(1);
        if (GUI.Button(new Rect(cx + 286, curY + 2, 70, 18), "Hızlı Hücum")) mpMgr.ApplyDeckPreset(2);
        if (GUI.Button(new Rect(cx + 359, curY + 2, 75, 18), "Zombi Sürü")) mpMgr.ApplyDeckPreset(3);
        if (GUI.Button(new Rect(cx + 437, curY + 2, 70, 18), "Karma")) mpMgr.ApplyDeckPreset(4);

        GUI.color = new Color(0.95f, 0.55f, 0.10f);
        if (GUI.Button(new Rect(cx + 510, curY + 2, 130, 18), "🎲 2 Tarafa Rastgele")) mpMgr.GenerateRandomSetsForBothSides();
        GUI.color = new Color(0.22f, 0.68f, 0.42f);
        if (GUI.Button(new Rect(cx + 643, curY + 2, 85, 18), "🎲 Rastgele P1")) mpMgr.GenerateRandomSet(1);
        GUI.color = new Color(0.85f, 0.35f, 0.35f);
        if (GUI.Button(new Rect(cx + 731, curY + 2, 85, 18), "🎲 Rastgele P2")) mpMgr.GenerateRandomSet(2);
        GUI.color = mpMgr.DisableEliteLimits ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.25f, 0.55f, 0.85f);
        if (GUI.Button(new Rect(cx + 819, curY + 2, 140, 18), mpMgr.DisableEliteLimits ? "⚡ Limitsiz Elit (Açık)" : "🛡️ Elit Sınırı (Aç)")) mpMgr.ToggleDisableEliteLimits();
        GUI.color = Color.white;

        float halfW = (cw - 20f) * 0.5f;
        GUIStyle slotTextStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleLeft, richText = true, fontSize = 8, wordWrap = true };

        if (isTenUnitMode)
        {
            float tenSlotW = (halfW - 24f) / 5f;
            int p1Leg, p1Elite, p1Core;
            SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P1Deck, out p1Leg, out p1Elite, out p1Core);
            string p1Header = mpMgr.DisableEliteLimits
                ? string.Format("<color=#55FF55><b>1. Oyuncu (10 Birlik)</b></color> [<color=#FFD700>⚡ LİMİTSİZ ELİT/BOSS ({0} Boss, {1} Elit)</color> | <color=#55FFFF>⚡ 225 Smithy</color>]", p1Leg, p1Elite)
                : string.Format("<color=#55FF55><b>1. Oyuncu (10 Birlik - Herosuz)</b></color> [<color=#FFD700>👑 {0}/2</color> | <color=#66CCFF>🛡️ {1}/4</color> | <color=#55FFFF>⚡ 225 Smithy / +8s</color>]", p1Leg, p1Elite);
            GUI.Label(new Rect(cx + 8, curY + 22, halfW, 16), p1Header, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });

            for (int i = 0; i < mpMgr.P1Deck.Count; i++)
            {
                string unit = mpMgr.P1Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP1Slot == i && mSelectedP2Slot == -1);
                int col = i % 5;
                int row = i / 5;

                Rect sRect = new Rect(cx + 8 + col * (tenSlotW + 3f), curY + 40 + row * 42f, tenSlotW, 39f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.18f, 0.24f, 0.32f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D sIcon = GetUnitIconTexture(unit);
                if (sIcon != null) GUI.DrawTexture(new Rect(sRect.x + 2f, sRect.y + 6f, 26f, 26f), sIcon, ScaleMode.ScaleToFit);

                string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Samurai) ? "L" : "B";
                string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                string label = string.Format("{0}<color=#FFEE55><b>S{1}({2}{3})</b></color>\n<color=#FFFFFF>{4}</color>", tierBadge, i + 1, meta.cost, costType, meta.displayName);
                GUI.Label(new Rect(sRect.x + 30f, sRect.y + 2f, sRect.width - 32f, 35f), label, slotTextStyle);

                if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                {
                    mSelectedP1Slot = i;
                    mSelectedP2Slot = -1;
                    SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                }
            }

            float p2X = cx + halfW + 12f;
            int p2Leg, p2Elite, p2Core;
            SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P2Deck, out p2Leg, out p2Elite, out p2Core);
            string p2Header = mpMgr.DisableEliteLimits
                ? string.Format("<color=#FF7777><b>2. Oyuncu (10 Birlik)</b></color> [<color=#FFD700>⚡ LİMİTSİZ ELİT/BOSS ({0} Boss, {1} Elit)</color> | <color=#55FFFF>⚡ 225 Smithy</color>]", p2Leg, p2Elite)
                : string.Format("<color=#FF7777><b>2. Oyuncu (10 Birlik - Herosuz)</b></color> [<color=#FFD700>👑 {0}/2</color> | <color=#66CCFF>🛡️ {1}/4</color> | <color=#55FFFF>⚡ 225 Smithy / +8s</color>]", p2Leg, p2Elite);
            GUI.Label(new Rect(p2X, curY + 22, halfW, 16), p2Header, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });

            for (int i = 0; i < mpMgr.P2Deck.Count; i++)
            {
                string unit = mpMgr.P2Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP2Slot == i && mSelectedP1Slot == -1);
                int col = i % 5;
                int row = i / 5;

                Rect sRect = new Rect(p2X + col * (tenSlotW + 3f), curY + 40 + row * 42f, tenSlotW, 39f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.28f, 0.18f, 0.22f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D sIcon = GetUnitIconTexture(unit);
                if (sIcon != null) GUI.DrawTexture(new Rect(sRect.x + 2f, sRect.y + 6f, 26f, 26f), sIcon, ScaleMode.ScaleToFit);

                string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Zombie) ? "B" : "L";
                string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                string label = string.Format("{0}<color=#FFEE55><b>S{1}({2}{3})</b></color>\n<color=#FFFFFF>{4}</color>", tierBadge, i + 1, meta.cost, costType, meta.displayName);
                GUI.Label(new Rect(sRect.x + 30f, sRect.y + 2f, sRect.width - 32f, 35f), label, slotTextStyle);

                if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                {
                    mSelectedP2Slot = i;
                    mSelectedP1Slot = -1;
                    SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                }
            }
        }
        else
        {
            float slotW = (halfW - 24f) / 7f;

            // --- P1 DECK + SPELL ROW (LEFT) ---
            int p1Leg, p1Elite, p1Core;
            SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P1Deck, out p1Leg, out p1Elite, out p1Core);
            string p1HeroSpellLbl = (mpMgr.P1HeroType == SVZMultiplayerManager.HeroType.Samurai) ? "🗡️ Samuray Büyüleri" : "💀 Zombi Büyüleri";
            string p1Header = mpMgr.DisableEliteLimits
                ? string.Format("<color=#55FF55><b>1. Oyuncu (5 Birlik + 2 Büyü)</b></color> [<color=#FFD700>⚡ LİMİTSİZ ELİT/BOSS ({0} Boss, {1} Elit)</color> | <color=#DDA0FF>🔮 {2}</color>]", p1Leg, p1Elite, p1HeroSpellLbl)
                : string.Format("<color=#55FF55><b>1. Oyuncu (5 Birlik + 2 Büyü)</b></color> [<color=#FFD700>👑 {0}/1</color> | <color=#66CCFF>🛡️ {1}/2</color> | <color=#DDA0FF>🔮 {2}</color>]", p1Leg, p1Elite, p1HeroSpellLbl);
            GUI.Label(new Rect(cx + 8, curY + 22, halfW, 16), p1Header, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });

            for (int i = 0; i < mpMgr.P1Deck.Count; i++)
            {
                string unit = mpMgr.P1Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP1Slot == i && mSelectedP2Slot == -1 && mSelectedP1SpellSlot == -1 && mSelectedP2SpellSlot == -1);

                Rect sRect = new Rect(cx + 8 + i * (slotW + 3f), curY + 40, slotW, 58f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.18f, 0.24f, 0.32f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D sIcon = GetUnitIconTexture(unit);
                if (sIcon != null)
                {
                    GUI.DrawTexture(new Rect(sRect.x + 2f, sRect.y + 14f, 28f, 28f), sIcon, ScaleMode.ScaleToFit);
                }

                string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Samurai) ? "L" : "B";
                string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                string label = string.Format("{0}<color=#FFEE55><b>S{1}({2}{3})</b></color>\n<color=#FFFFFF>{4}</color>", tierBadge, i + 1, meta.cost, costType, meta.displayName);
                GUI.Label(new Rect(sRect.x + 31f, sRect.y + 4f, sRect.width - 33f, 50f), label, slotTextStyle);

                if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                {
                    mSelectedP1Slot = i;
                    mSelectedP2Slot = -1;
                    mSelectedP1SpellSlot = -1;
                    mSelectedP2SpellSlot = -1;
                    if (mCatalogFilter == 3) mCatalogFilter = 0;
                    SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                }
            }

            for (int s = 0; s < 2; s++)
            {
                string spId = (s < mpMgr.P1Spells.Count) ? mpMgr.P1Spells[s] : "KatanaSlash";
                SVZMultiplayerManager.SpellMeta spMeta = SVZMultiplayerManager.GetSpellMeta(spId);
                bool isSpActive = (mSelectedP1SpellSlot == s && mSelectedP2SpellSlot == -1);

                Rect spRect = new Rect(cx + 8 + (5 + s) * (slotW + 3f), curY + 40, slotW, 58f);
                GUI.color = isSpActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.26f, 0.16f, 0.38f, 0.95f);
                GUI.DrawTexture(spRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D spIcon = GetSpellIconTexture(spId);
                if (spIcon != null)
                {
                    GUI.DrawTexture(new Rect(spRect.x + 2f, spRect.y + 14f, 28f, 28f), spIcon, ScaleMode.ScaleToFit);
                }

                string spLbl = string.Format("🔮<color=#DDA0FF><b>B{0}</b></color>\n<color=#FFFFFF>{1}</color>", s + 1, spMeta.displayName);
                GUI.Label(new Rect(spRect.x + 31f, spRect.y + 4f, spRect.width - 33f, 50f), spLbl, slotTextStyle);

                if (GUI.Button(spRect, GUIContent.none, GUIStyle.none))
                {
                    mSelectedP1SpellSlot = s;
                    mSelectedP2SpellSlot = -1;
                    mSelectedP1Slot = -1;
                    mSelectedP2Slot = -1;
                    mCatalogFilter = 3;
                    SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                }
            }

            // --- P2 DECK + SPELL ROW (RIGHT) ---
            float p2X = cx + halfW + 12f;
            int p2Leg, p2Elite, p2Core;
            SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P2Deck, out p2Leg, out p2Elite, out p2Core);
            string p2HeroSpellLbl = (mpMgr.P2HeroType == SVZMultiplayerManager.HeroType.Samurai) ? "🗡️ Samuray Büyüleri" : "💀 Zombi Büyüleri";
            string p2Header = mpMgr.DisableEliteLimits
                ? string.Format("<color=#FF7777><b>2. Oyuncu (5 Birlik + 2 Büyü)</b></color> [<color=#FFD700>⚡ LİMİTSİZ ELİT/BOSS ({0} Boss, {1} Elit)</color> | <color=#FFAAEE>🔮 {2}</color>]", p2Leg, p2Elite, p2HeroSpellLbl)
                : string.Format("<color=#FF7777><b>2. Oyuncu (5 Birlik + 2 Büyü)</b></color> [<color=#FFD700>👑 {0}/1</color> | <color=#66CCFF>🛡️ {1}/2</color> | <color=#FFAAEE>🔮 {2}</color>]", p2Leg, p2Elite, p2HeroSpellLbl);
            GUI.Label(new Rect(p2X, curY + 22, halfW, 16), p2Header, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });

            for (int i = 0; i < mpMgr.P2Deck.Count; i++)
            {
                string unit = mpMgr.P2Deck[i];
                SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
                bool isSlotActive = (mSelectedP2Slot == i && mSelectedP1SpellSlot == -1 && mSelectedP2SpellSlot == -1);

                Rect sRect = new Rect(p2X + i * (slotW + 3f), curY + 40, slotW, 58f);
                GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.28f, 0.18f, 0.22f, 0.95f);
                GUI.DrawTexture(sRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D sIcon = GetUnitIconTexture(unit);
                if (sIcon != null)
                {
                    GUI.DrawTexture(new Rect(sRect.x + 2f, sRect.y + 14f, 28f, 28f), sIcon, ScaleMode.ScaleToFit);
                }

                string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Zombie) ? "B" : "L";
                string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
                string label = string.Format("{0}<color=#FFEE55><b>S{1}({2}{3})</b></color>\n<color=#FFFFFF>{4}</color>", tierBadge, i + 1, meta.cost, costType, meta.displayName);
                GUI.Label(new Rect(sRect.x + 31f, sRect.y + 4f, sRect.width - 33f, 50f), label, slotTextStyle);

                if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
                {
                    mSelectedP2Slot = i;
                    mSelectedP1Slot = -1;
                    mSelectedP1SpellSlot = -1;
                    mSelectedP2SpellSlot = -1;
                    if (mCatalogFilter == 3) mCatalogFilter = 0;
                    SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                }
            }

            for (int s = 0; s < 2; s++)
            {
                string spId = (s < mpMgr.P2Spells.Count) ? mpMgr.P2Spells[s] : "DragonAttack";
                SVZMultiplayerManager.SpellMeta spMeta = SVZMultiplayerManager.GetSpellMeta(spId);
                bool isSpActive = (mSelectedP2SpellSlot == s);

                Rect spRect = new Rect(p2X + (5 + s) * (slotW + 3f), curY + 40, slotW, 58f);
                GUI.color = isSpActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.35f, 0.16f, 0.30f, 0.95f);
                GUI.DrawTexture(spRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                Texture2D spIcon = GetSpellIconTexture(spId);
                if (spIcon != null)
                {
                    GUI.DrawTexture(new Rect(spRect.x + 2f, spRect.y + 14f, 28f, 28f), spIcon, ScaleMode.ScaleToFit);
                }

                string spLbl = string.Format("🔮<color=#FFAAEE><b>B{0}</b></color>\n<color=#FFFFFF>{1}</color>", s + 1, spMeta.displayName);
                GUI.Label(new Rect(spRect.x + 31f, spRect.y + 4f, spRect.width - 33f, 50f), spLbl, slotTextStyle);

                if (GUI.Button(spRect, GUIContent.none, GUIStyle.none))
                {
                    mSelectedP2SpellSlot = s;
                    mSelectedP1SpellSlot = -1;
                    mSelectedP1Slot = -1;
                    mSelectedP2Slot = -1;
                    mCatalogFilter = 3;
                    SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
                }
            }
        }
        GUI.color = Color.white;
        curY += deckBoxH + 6f;

        // 3. FULL INTERACTIVE UNIT & SPELL CATALOG WITH SCROLLVIEW
        DrawInteractiveCatalog(cx, curY, cw, ch - (curY - cy));
    }

    #endregion

    #region Tab 3: 4. Mod - 10 Birlik Herosuz 1v1 Modu (225 Smithy / +8 Hiz)

    private void DrawTab3TenUnitArmyMode(float cx, float cy, float cw, float ch)
    {
        SVZMultiplayerManager mpMgr = SVZMultiplayerManager.Instance;
        if (mpMgr.CurrentGameMode != SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
        {
            mpMgr.CurrentGameMode = SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly;
        }

        float curY = cy;
        GUIStyle secTitle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 11 };

        // 1. TOP BANNER: 10-UNIT HEROLESS MODE RULES + QUICK LAUNCH BUTTONS
        GUI.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(cx, curY, cw, 52f), Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.85f, 0.45f, 0.9f);
        GUI.DrawTexture(new Rect(cx, curY, cw, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx + 10f, curY + 5f, 620f, 22f),
            "<color=#55FFFF><b>🎖️ 4. MOD: 10 BİRLİK HEROSUZ ORDU SAVAŞI (1v1)</b></color> | <color=#FFEE55><b>⚡ Smithy: 225 MAX | Saniyede +8 Dolum!</b></color>", secTitle);
        GUI.Label(new Rect(cx + 10f, curY + 26f, 620f, 20f),
            "<size=10><color=#CCCCCC>Kahraman olmadan 10 Samuray veya Zombi birliği seçip kapışın! Kısayollar: <b>[1-9 ve 0]</b> | Kamera: <b>[A/D]</b></color></size>",
            new GUIStyle(GUI.skin.label) { richText = true });

        // Quick Launch Buttons on the Right of Banner
        GUI.color = new Color(0.95f, 0.65f, 0.2f);
        if (GUI.Button(new Rect(cx + cw - 680f, curY + 8f, 160f, 36f), "<b>🤖 BOT'A KARŞI BAŞLAT</b>\n<size=9>10 Birlik vs Yapay Zeka</size>"))
        {
            mpMgr.P2AutoBot = true;
            mShowLobby = false;
            mpMgr.StartMultiplayerMatch(true);
        }

        GUI.color = new Color(0.25f, 0.85f, 0.45f);
        if (GUI.Button(new Rect(cx + cw - 512f, curY + 8f, 160f, 36f), "<b>👥 2 KİŞİLİK BAŞLAT</b>\n<size=9>Aynı PC'de 10v10 Kapış</size>"))
        {
            mpMgr.P2AutoBot = false;
            mShowLobby = false;
            mpMgr.StartMultiplayerMatch(true);
        }

        bool isRightSideTab3 = (mpMgr.P1Side == SVZMultiplayerManager.BattlefieldSide.RightGate);
        GUI.color = isRightSideTab3 ? new Color(1f, 0.4f, 0.4f) : new Color(0.85f, 0.55f, 0.95f);
        string swapLabel = isRightSideTab3
            ? "<b>🔄 2. TARAF (SAĞ) AKTİF</b>\n<size=9>Sen: Sağ Kale | Bot: Sol Kale</size>"
            : "<b>🔄 TARAF DEĞİŞTİR</b>\n<size=9>2. Takıma (Sağ Kaleye) Geç</size>";
        if (GUI.Button(new Rect(cx + cw - 344f, curY + 8f, 170f, 36f), swapLabel))
        {
            mpMgr.SwapTeamsAndSide();
            SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
        }

        GUI.color = new Color(0.35f, 0.70f, 1.0f);
        if (GUI.Button(new Rect(cx + cw - 164f, curY + 8f, 156f, 36f), "<b>🏰 ÖZEL LOBİDE AÇ</b>\n<size=9>Online / LoL Lobisi</size>"))
        {
            mSelectedTab = 0;
            mInCustomLobbyRoom = true;
            SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
        }
        GUI.color = Color.white;

        curY += 58f;

        // 2. 10-SLOT DECK SELECTION PANEL FOR PLAYER 1 & PLAYER 2
        float deckPanelH = 132f;
        GUI.color = new Color(0.10f, 0.12f, 0.18f, 0.94f);
        GUI.DrawTexture(new Rect(cx, curY, cw, deckPanelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(cx + 8, curY + 3, 150, 18), "<color=#FFEE55>10'LU ORDU DİZİLİŞİ:</color>", secTitle);

        if (GUI.Button(new Rect(cx + 150, curY + 2, 60, 18), "Dengeli 10")) mpMgr.ApplyDeckPreset(0);
        if (GUI.Button(new Rect(cx + 213, curY + 2, 70, 18), "Zırhlı 10")) mpMgr.ApplyDeckPreset(1);
        if (GUI.Button(new Rect(cx + 286, curY + 2, 70, 18), "Hızlı 10")) mpMgr.ApplyDeckPreset(2);
        if (GUI.Button(new Rect(cx + 359, curY + 2, 75, 18), "Zombi 10")) mpMgr.ApplyDeckPreset(3);
        if (GUI.Button(new Rect(cx + 437, curY + 2, 70, 18), "Karma 10")) mpMgr.ApplyDeckPreset(4);

        GUI.color = new Color(0.95f, 0.55f, 0.10f);
        if (GUI.Button(new Rect(cx + 510, curY + 2, 130, 18), "🎲 2 Tarafa Rastgele")) mpMgr.GenerateRandomSetsForBothSides();
        GUI.color = new Color(0.22f, 0.68f, 0.42f);
        if (GUI.Button(new Rect(cx + 643, curY + 2, 85, 18), "🎲 Rastgele P1")) mpMgr.GenerateRandomSet(1);
        GUI.color = new Color(0.85f, 0.35f, 0.35f);
        if (GUI.Button(new Rect(cx + 731, curY + 2, 85, 18), "🎲 Rastgele P2")) mpMgr.GenerateRandomSet(2);
        GUI.color = mpMgr.DisableEliteLimits ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.25f, 0.55f, 0.85f);
        if (GUI.Button(new Rect(cx + 819, curY + 2, 140, 18), mpMgr.DisableEliteLimits ? "⚡ Limitsiz Elit (Açık)" : "🛡️ Elit Sınırı (Aç)")) mpMgr.ToggleDisableEliteLimits();
        GUI.color = Color.white;

        // Warning banner if quota exceeded
        if (mDeckWarningTimer > 0f && !string.IsNullOrEmpty(mDeckWarningMessage))
        {
            GUI.Label(new Rect(cx + 875, curY + 2, cw - 880, 18), "<color=#FF4444><b>⚠️ " + mDeckWarningMessage + "</b></color>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 9 });
        }

        float halfW = (cw - 20f) * 0.5f;
        float tenSlotW = (halfW - 24f) / 5f;
        GUIStyle slotTextStyle = new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleLeft, richText = true, fontSize = 8, wordWrap = true };

        // P1 10-Unit Grid (Left)
        int p1Leg, p1Elite, p1Core;
        SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P1Deck, out p1Leg, out p1Elite, out p1Core);
        string p1Header = mpMgr.DisableEliteLimits
            ? string.Format("<color=#55FF55><b>🔵 1. OYUNCU (10 BİRLİK)</b></color> [<color=#FFD700>⚡ LİMİTSİZ ELİT/BOSS ({0} Boss, {1} Elit, {2} Temel)</color>]", p1Leg, p1Elite, p1Core)
            : string.Format("<color=#55FF55><b>🔵 1. OYUNCU (10 BİRLİK)</b></color> [<color=#FFD700>👑 Efsanevi: {0}/2</color> | <color=#66CCFF>🛡️ Elit: {1}/4</color> | <color=#88FF88>⚔️ Temel: {2}</color>]", p1Leg, p1Elite, p1Core);
        GUI.Label(new Rect(cx + 8, curY + 22, halfW, 16), p1Header, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });

        for (int i = 0; i < mpMgr.P1Deck.Count; i++)
        {
            string unit = mpMgr.P1Deck[i];
            SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
            bool isSlotActive = (mSelectedP1Slot == i && mSelectedP2Slot == -1);
            int col = i % 5;
            int row = i / 5;

            Rect sRect = new Rect(cx + 8 + col * (tenSlotW + 3f), curY + 42 + row * 43f, tenSlotW, 40f);
            GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.18f, 0.24f, 0.32f, 0.95f);
            GUI.DrawTexture(sRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Texture2D sIcon = GetUnitIconTexture(unit);
            if (sIcon != null) GUI.DrawTexture(new Rect(sRect.x + 2f, sRect.y + 6f, 28f, 28f), sIcon, ScaleMode.ScaleToFit);

            string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Samurai) ? "L" : "B";
            string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
            string label = string.Format("{0}<color=#FFEE55><b>S{1}({2}{3})</b></color>\n<color=#FFFFFF>{4}</color>", tierBadge, i + 1, meta.cost, costType, meta.displayName);
            GUI.Label(new Rect(sRect.x + 31f, sRect.y + 2f, sRect.width - 33f, 36f), label, slotTextStyle);

            if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
            {
                mSelectedP1Slot = i;
                mSelectedP2Slot = -1;
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
        }

        // P2 10-Unit Grid (Right)
        float p2X = cx + halfW + 12f;
        int p2Leg, p2Elite, p2Core;
        SVZMultiplayerManager.GetDeckTierCounts(mpMgr.P2Deck, out p2Leg, out p2Elite, out p2Core);
        string p2Header = mpMgr.DisableEliteLimits
            ? string.Format("<color=#FF7777><b>🔴 2. OYUNCU / BOT (10 BİRLİK)</b></color> [<color=#FFD700>⚡ LİMİTSİZ ELİT/BOSS ({0} Boss, {1} Elit, {2} Temel)</color>]", p2Leg, p2Elite, p2Core)
            : string.Format("<color=#FF7777><b>🔴 2. OYUNCU / BOT (10 BİRLİK)</b></color> [<color=#FFD700>👑 Efsanevi: {0}/2</color> | <color=#66CCFF>🛡️ Elit: {1}/4</color> | <color=#88FF88>⚔️ Temel: {2}</color>]", p2Leg, p2Elite, p2Core);
        GUI.Label(new Rect(p2X, curY + 22, halfW, 16), p2Header, new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });

        for (int i = 0; i < mpMgr.P2Deck.Count; i++)
        {
            string unit = mpMgr.P2Deck[i];
            SVZMultiplayerManager.UnitMeta meta = SVZMultiplayerManager.GetUnitMeta(unit);
            bool isSlotActive = (mSelectedP2Slot == i && mSelectedP1Slot == -1);
            int col = i % 5;
            int row = i / 5;

            Rect sRect = new Rect(p2X + col * (tenSlotW + 3f), curY + 42 + row * 43f, tenSlotW, 40f);
            GUI.color = isSlotActive ? new Color(0.95f, 0.75f, 0.2f, 0.95f) : new Color(0.28f, 0.18f, 0.22f, 0.95f);
            GUI.DrawTexture(sRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            Texture2D sIcon = GetUnitIconTexture(unit);
            if (sIcon != null) GUI.DrawTexture(new Rect(sRect.x + 2f, sRect.y + 6f, 28f, 28f), sIcon, ScaleMode.ScaleToFit);

            string costType = (meta.faction == SVZMultiplayerManager.UnitFaction.Zombie) ? "B" : "L";
            string tierBadge = SVZMultiplayerManager.GetTierBadge(meta.tier);
            string label = string.Format("{0}<color=#FFEE55><b>S{1}({2}{3})</b></color>\n<color=#FFFFFF>{4}</color>", tierBadge, i + 1, meta.cost, costType, meta.displayName);
            GUI.Label(new Rect(sRect.x + 31f, sRect.y + 2f, sRect.width - 33f, 36f), label, slotTextStyle);

            if (GUI.Button(sRect, GUIContent.none, GUIStyle.none))
            {
                mSelectedP2Slot = i;
                mSelectedP1Slot = -1;
                SVZMultiplayerManager.PlaySFX("UI_Click_01", 0.8f);
            }
        }

        curY += deckPanelH + 6f;

        // 3. INTERACTIVE 49-UNIT CATALOG (SAMURAI + ZOMBIE)
        DrawInteractiveCatalog(cx, curY, cw, ch - (curY - cy));
    }

    #endregion
}
