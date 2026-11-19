using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum GameDifficulty
{
    Kolay = 0,     // HP 0.75x, Dmg 0.75x, Drops 1.25x
    Normal = 1,    // HP 1.00x, Dmg 1.00x, Drops 1.00x
    Zor = 2,       // HP 1.35x, Dmg 1.25x, Drops 1.50x
    Kabus = 3      // HP 1.80x, Dmg 1.60x, Drops 2.00x
}

/// <summary>
/// PC Quality-of-Life (QoL) Suite for Samurai vs Zombies Defense - Gemini Edition
/// - Speed Control (1x / 1.5x / 2x / 3x) with [Tab] or [T]
/// - Auto-Smithy Level-Up with [U]
/// - Tactical Combat Dash with [LeftShift]
/// - Instant Loot Vacuum with [C] & Auto-Vacuum on Victory
/// - Dynamic Difficulty Modes (Kolay / Normal / Zor / Kabus) with [F2]
/// - Mini-Almanac / Bestiary with [B]
/// - Quick Restart on Defeat with [R] & UI Buttons
/// - Real-time Wave & Combat Analytics HUD with [F3]
/// - Open Isolated Save Folder in Explorer with [F4]
/// - PC Controls Help Overlay with [H]
/// </summary>
public class PCQoLManager : MonoBehaviour
{
    private static PCQoLManager sInstance;

    // Speed Control
    private static readonly float[] sSpeedSteps = { 1.0f, 1.5f, 2.0f, 3.0f };
    private int mSpeedIndex = 0;
    private float mCurrentTargetSpeed = 1.0f;

    // Auto Smithy
    private bool mAutoSmithy = false;
    private float mAutoSmithyCheckTimer = 0f;

    // Dash Mechanic
    private float mDashCooldown = 0f;
    private const float kDashCooldownMax = 1.5f;

    // Analytics HUD & Help
    private bool mShowAnalytics = false;
    private bool mShowHelp = false;

    // Notification message
    private string mToastMessage = string.Empty;
    private float mToastTimer = 0f;

    public static PCQoLManager Instance
    {
        get { return sInstance; }
    }

    public float currentSpeed
    {
        get { return mCurrentTargetSpeed; }
    }

    public bool autoSmithy
    {
        get { return mAutoSmithy; }
        set { mAutoSmithy = value; }
    }

    #region Difficulty System

    public static GameDifficulty CurrentDifficulty
    {
        get { return (GameDifficulty)PlayerPrefs.GetInt("SVZ_Difficulty", 1); }
        set { PlayerPrefs.SetInt("SVZ_Difficulty", (int)value); PlayerPrefs.Save(); }
    }

    public static float DifficultyHealthMultiplier
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case GameDifficulty.Kolay: return 0.75f;
                case GameDifficulty.Zor: return 1.35f;
                case GameDifficulty.Kabus: return 1.80f;
                default: return 1.0f;
            }
        }
    }

    public static float DifficultyDamageMultiplier
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case GameDifficulty.Kolay: return 0.75f;
                case GameDifficulty.Zor: return 1.25f;
                case GameDifficulty.Kabus: return 1.60f;
                default: return 1.0f;
            }
        }
    }

    public static float DifficultyDropMultiplier
    {
        get
        {
            switch (CurrentDifficulty)
            {
                case GameDifficulty.Kolay: return 1.25f;
                case GameDifficulty.Zor: return 1.50f;
                case GameDifficulty.Kabus: return 2.00f;
                default: return 1.0f;
            }
        }
    }

    public static string GetDifficultyDisplayName(GameDifficulty diff)
    {
        switch (diff)
        {
            case GameDifficulty.Kolay: return LocalizationManager.T("<color=#55FF55>EASY</color>", "<color=#55FF55>KOLAY</color>");
            case GameDifficulty.Normal: return "<color=#FFEE55>NORMAL</color>";
            case GameDifficulty.Zor: return LocalizationManager.T("<color=#FF9933>HARD</color>", "<color=#FF9933>ZOR</color>");
            case GameDifficulty.Kabus: return LocalizationManager.T("<color=#FF3333>HELL</color>", "<color=#FF3333>KABUS (HELL)</color>");
            default: return "NORMAL";
        }
    }

    public static void CycleDifficulty()
    {
        int next = ((int)CurrentDifficulty + 1) % 4;
        CurrentDifficulty = (GameDifficulty)next;
    }

    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreInitUniversalCompat()
    {
        ApplyUniversalDeviceCompatibility();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        ApplyUniversalDeviceCompatibility();
        if (sInstance == null)
        {
            GameObject go = new GameObject("[PCQoLManager]");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<PCQoLManager>();
        }
    }

    private static bool sUniversalCompatApplied;

    private static void ApplyUniversalDeviceCompatibility()
    {
        if (sUniversalCompatApplied) return;
        sUniversalCompatApplied = true;
        try
        {
            // Guarantee invariant culture (period decimal '.') globally on all devices and locales (TR, DE, FR, RU, BR, etc.)
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (Application.isMobilePlatform || Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
                Input.multiTouchEnabled = true;
            }
            Application.lowMemory += OnLowMemoryCleanup;
            SVZDeviceCompatibility.EnsureExists();
            PerformanceSettings.ApplySavedSettings();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UniversalCompat] Init warning: " + ex.Message);
        }
    }

    private static void OnLowMemoryCleanup()
    {
        try
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
        catch
        {
        }
    }

    private void Awake()
    {
        if (sInstance == null)
        {
            sInstance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (sInstance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        bool isInBattle = inGame != null && inGame.hero != null;

        // 1. Speed Control [Tab] or [T] (Only during active battle)
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.T))
        {
            if (isInBattle)
            {
                CycleBattleSpeed(inGame);
            }
        }

        // Reset speed to 1x when outside combat
        if (!isInBattle && mSpeedIndex != 0)
        {
            mSpeedIndex = 0;
            mCurrentTargetSpeed = 1.0f;
            if (Time.timeScale > 0f)
            {
                Time.timeScale = 1.0f;
            }
        }

        // 2. Auto-Smithy [U] Toggle
        if (Input.GetKeyDown(KeyCode.U))
        {
            ToggleAutoSmithy();
        }

        // Run Auto-Smithy logic
        if (mAutoSmithy && isInBattle)
        {
            mAutoSmithyCheckTimer += Time.unscaledDeltaTime;
            if (mAutoSmithyCheckTimer >= 0.25f)
            {
                mAutoSmithyCheckTimer = 0f;
                Smithy smithy = WeakGlobalInstance<Smithy>.instance;
                if (smithy != null && smithy.isUpgradable)
                {
                    smithy.level++;
                    ShowToast("DEMIRCI SEVIYESI ARTIRILDI (Seviye " + smithy.level + ")");
                }
            }
        }

        // 3. Tactical Combat Dash [LeftShift]
        if (mDashCooldown > 0f)
        {
            mDashCooldown -= Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && isInBattle && mDashCooldown <= 0f)
        {
            PerformHeroDash(inGame.hero);
        }

        // 4. Instant Loot Vacuum [C] & Auto-Vacuum on Victory
        if (isInBattle)
        {
            if (Input.GetKeyDown(KeyCode.C) || inGame.playerWon)
            {
                CollectableManager cMgr = WeakGlobalInstance<CollectableManager>.instance;
                if (cMgr != null)
                {
                    int collected = cMgr.CollectAll();
                    if (collected > 0 && Input.GetKeyDown(KeyCode.C))
                    {
                        ShowToast(collected + LocalizationManager.T(" LOOT COLLECTED! [C]", " GANIMET TOPLANDI! [C]"), 1.2f);
                    }
                }
            }
        }

        // 5. Difficulty Mode Cycle [F2]
        if (Input.GetKeyDown(KeyCode.F2))
        {
            CycleDifficultyWithToast();
        }

        // 6. Defeat Screen Quick Restart [R]
        if (inGame != null && inGame.enemiesWon && !inGame.playerWon)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartCurrentWave();
            }
        }

        // 7. Mini-Almanac / Bestiary [B]
        if (Input.GetKeyDown(KeyCode.B))
        {
            SVZAlmanac.Toggle();
        }

        // 8. Analytics HUD [F3] Toggle
        if (Input.GetKeyDown(KeyCode.F3))
        {
            mShowAnalytics = !mShowAnalytics;
            ShowToast(mShowAnalytics
                ? LocalizationManager.T("BATTLE INFO: ON [F3]", "SAVAS BILGISI: ACIK [F3]")
                : LocalizationManager.T("BATTLE INFO: OFF [F3]", "SAVAS BILGISI: KAPALI [F3]"));
        }

        // 9. Quick Controls Help Overlay [H] Toggle
        if (Input.GetKeyDown(KeyCode.H))
        {
            mShowHelp = !mShowHelp;
        }

        // 10. Open Save Folder in Windows Explorer [F4]
        if (Input.GetKeyDown(KeyCode.F4))
        {
            OpenSaveFolder();
        }

        // Toast Timer countdown
        if (mToastTimer > 0f)
        {
            mToastTimer -= Time.unscaledDeltaTime;
        }
    }

    public static void RestartCurrentWave()
    {
        Time.timeScale = 1.0f;
        Singleton<MenusFlow>.instance.LoadScene("InGame");
    }

    private void PerformHeroDash(Hero hero)
    {
        if (SVZMultiplayerManager.Instance != null && SVZMultiplayerManager.Instance.IsActive &&
            SVZMultiplayerManager.Instance.CurrentGameMode == SVZMultiplayerManager.MultiplayerGameMode.ArmyOnly)
        {
            return;
        }
        if (hero == null || hero.controller == null || hero.health <= 0f)
        {
            return;
        }

        mDashCooldown = kDashCooldownMax;
        float dashDistance = 80f;
        Vector3 pos = hero.controller.position;

        // Dash in direction of movement or facing
        if (hero.controller.facing == FacingType.Right)
        {
            pos.z += dashDistance;
        }
        else
        {
            pos.z -= dashDistance;
        }

        // Clamp to hero movement bounds
        pos.z = Mathf.Clamp(pos.z, hero.controller.constraintLeft, hero.controller.constraintRight);
        hero.controller.position = pos;

        // Temporary i-frames (0.35s)
        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        if (inGame != null)
        {
            inGame.allAlliesInvincibleTimer = Mathf.Max(inGame.allAlliesInvincibleTimer, 0.35f);
        }

        ShowToast(LocalizationManager.T("TACTICAL DASH! [SHIFT]", "TAKTIKSEL ATILMA! [SHIFT]"), 1.2f);
    }

    private void CycleBattleSpeed(InGameImpl inGame)
    {
        if (inGame == null) return;
        mSpeedIndex = (mSpeedIndex + 1) % sSpeedSteps.Length;
        mCurrentTargetSpeed = sSpeedSteps[mSpeedIndex];
        inGame.baseTimeScale = mCurrentTargetSpeed;
        ShowToast(LocalizationManager.T("GAME SPEED: ", "OYUN HIZI: ") + mCurrentTargetSpeed.ToString("0.0") + "x");
    }

    private void ToggleAutoSmithy()
    {
        mAutoSmithy = !mAutoSmithy;
        ShowToast(mAutoSmithy
            ? LocalizationManager.T("AUTO-SMITHY: ON", "OTO-DEMIRCI: AKTIF")
            : LocalizationManager.T("AUTO-SMITHY: OFF", "OTO-DEMIRCI: KAPALI"));
    }

    private void CycleDifficultyWithToast()
    {
        CycleDifficulty();
        ShowToast(LocalizationManager.T("DIFFICULTY: ", "ZORLUK MODU: ") + GetDifficultyDisplayName(CurrentDifficulty));
    }

    private void OpenSaveFolder()
    {
        try
        {
            string path = Profile.saveDirectory;
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                if (Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", path);
                    ShowToast(LocalizationManager.T("SAVE FOLDER OPENED [F4]", "SAVE KLASORU ACILDI [F4]"));
                }
                else
                {
                    ShowToast(LocalizationManager.T("Save folder not found!", "Save klasoru bulunamadi!"));
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(path))
                {
                    GUIUtility.systemCopyBuffer = path;
                }
                ShowToast(LocalizationManager.T("Save path copied to clipboard (Settings > Export Save)", "Save yolu panoya kopyalandi (Ayarlar > Export Save)"), 3.0f);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[PCQoLManager] OpenSaveFolder failed: " + ex.Message);
        }
    }

    public void ShowToast(string msg, float duration = 2.0f)
    {
        mToastMessage = msg;
        mToastTimer = duration;
    }

    private void OnGUI()
    {
        if (SVZMultiplayerManager.Instance != null && (SVZMultiplayerManager.Instance.IsActive || SVZMultiplayerManager.Instance.IsMatchOver))
        {
            return;
        }

        InGameImpl inGame = WeakGlobalSceneBehavior<InGameImpl>.instance;
        bool isInBattle = inGame != null && inGame.hero != null;
        bool isTouchDevice = SVZDeviceCompatibility.IsMobileOrTablet;

        float scale, virtualW, virtualH;
        Rect safeRect;
        SVZDeviceCompatibility.GetSafeVirtualViewport(720f, 960f, out scale, out virtualW, out virtualH, out safeRect);

        Matrix4x4 prevMatrix = GUI.matrix;
        int oldLabelSize = GUI.skin.label.fontSize;
        int oldBtnSize = GUI.skin.button.fontSize;
        Font oldLblFont = GUI.skin.label.font;
        Font oldBtnFont = GUI.skin.button.font;

        try
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            Font dyn = LocalizationManager.GetDynamicOSFont(15);
            if (dyn != null)
            {
                GUI.skin.label.font = dyn;
                GUI.skin.button.font = dyn;
            }

            // Draw Defeat Restart Prompt if player lost
            if (inGame != null && inGame.enemiesWon && !inGame.playerWon)
            {
                DrawDefeatRestartUI(safeRect, isTouchDevice);
            }

            // Analytics Panel [F3]
            if (isInBattle && mShowAnalytics)
            {
                CharactersManager charMgr = WeakGlobalInstance<CharactersManager>.instance;
                int allies = charMgr != null ? charMgr.helpersCount : 0;
                int enemies = charMgr != null ? charMgr.enemiesCount : 0;
                int coins = Singleton<Profile>.instance.coins;
                int gems = Singleton<Profile>.instance.gems;

                float aWidth = 270f;
                float aHeight = 112f;
                Rect aRect = new Rect(safeRect.xMax - aWidth - 10f, safeRect.y + 8f, aWidth, aHeight);

                GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.88f);
                GUI.DrawTexture(aRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.richText = true;
                labelStyle.alignment = TextAnchor.MiddleLeft;
                labelStyle.fontSize = 13;

                string aText = string.Format(
                    LocalizationManager.T(
                        "<b>[BATTLE INFO]</b>\nAllies: <color=#55FFFF>{0}</color> | Enemies: <color=#FF5555>{1}</color>\nCoins: <color=#FFEE55>{2}</color> | Gems: <color=#FF55FF>{3}</color>\n<size=11><color=#88CCFF>{4}</color></size>",
                        "<b>[SAVAS BILGISI]</b>\nMuttefik: <color=#55FFFF>{0}</color> | Dusman: <color=#FF5555>{1}</color>\nAltin: <color=#FFEE55>{2}</color> | Elmas: <color=#FF55FF>{3}</color>\n<size=11><color=#88CCFF>{4}</color></size>"
                    ),
                    allies, enemies, coins, gems, SVZDeviceCompatibility.AndroidVersionLabel
                );

                GUI.Label(new Rect(aRect.x + 8f, aRect.y + 4f, aWidth - 16f, aHeight - 8f), aText, labelStyle);
            }

            // Draw Help Overlay [H]
            if (mShowHelp)
            {
                float hWidth = Mathf.Min(430f, safeRect.width * 0.9f);
                float hHeight = Mathf.Min(390f, safeRect.height * 0.9f);
                Rect hRect = new Rect(safeRect.x + (safeRect.width - hWidth) * 0.5f, safeRect.y + (safeRect.height - hHeight) * 0.5f, hWidth, hHeight);

                GUI.color = new Color(0.04f, 0.04f, 0.07f, 0.95f);
                GUI.DrawTexture(hRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUIStyle hStyle = new GUIStyle(GUI.skin.label);
                hStyle.richText = true;
                hStyle.fontSize = 13;
                hStyle.wordWrap = true;

                string helpContent = LocalizationManager.T(
                    "<size=16><b><color=#FFE040>CONTROLS GUIDE</color></b></size>\n\n" +
                    "<b>[A / D]</b> or <b>[Arrows]</b> : Move Hero\n" +
                    "<b>[Q]</b> : Ability 1 | <b>[E]</b> : Ability 2\n" +
                    "<b>[F]</b> : Use Health Potion (Sushi)\n" +
                    "<b>[1 - 5]</b> : Summon Allies\n" +
                    "<b>[TAB / T]</b> : Battle Speed (1x / 1.5x / 2x / 3x)\n" +
                    "<b>[L-SHIFT]</b> : Tactical Dash\n" +
                    "<b>[U]</b> : Auto-Upgrade Smithy\n" +
                    "<b>[C]</b> : Collect All Loot\n" +
                    "<b>[R]</b> : Quick Retry on Defeat\n" +
                    "<b>[M]</b> : 1v1 PvP Multiplayer Lobby\n" +
                    "<b>[B]</b> : Enemy & Ally Almanac\n" +
                    "<b>[F2]</b> : Difficulty | <b>[F3]</b> : Battle Info\n" +
                    "<b>[F1]</b> : Dev Tools | <b>[F4]</b> : Save Folder\n" +
                    "<b>[H]</b> : Toggle This Guide",
                    "<size=16><b><color=#FFE040>KONTROL REHBERI</color></b></size>\n\n" +
                    "<b>[A / D]</b> veya <b>[Ok Tuslari]</b> : Kahraman Hareketi\n" +
                    "<b>[Q]</b> : 1. Ozel Yetenek (Ability 1)\n" +
                    "<b>[E]</b> : 2. Ozel Yetenek (Ability 2)\n" +
                    "<b>[F]</b> : Can Iksiri Kullan (Sushi)\n" +
                    "<b>[1 - 5]</b> : Birlikleri Cagir (Ally Summon)\n" +
                    "<b>[TAB / T]</b> : Savas Hizi (1x / 1.5x / 2x / 3x)\n" +
                    "<b>[L-SHIFT]</b> : Taktiksel Atilma (Dash)\n" +
                    "<b>[U]</b> : Otomatik Demirci Yukseltme\n" +
                    "<b>[C]</b> : Ganimetleri Aninda Topla\n" +
                    "<b>[R]</b> : Yenilgi Ekraninda Tekrar Dene\n" +
                    "<b>[M]</b> : 1v1 PvP Cok Oyunculu Lobi\n" +
                    "<b>[B]</b> : Dusman & Muttefik Rehberi (Almanac)\n" +
                    "<b>[F2]</b> : Zorluk Secimi | <b>[F3]</b> : Savas Analizi\n" +
                    "<b>[F1]</b> : Gelistirici Paneli | <b>[F4]</b> : Save Klasoru\n" +
                    "<b>[H]</b> : Bu Rehberi Kapat / Ac"
                );

                GUI.Label(new Rect(hRect.x + 16f, hRect.y + 12f, hWidth - 32f, hHeight - 52f), helpContent, hStyle);
                if (GUI.Button(new Rect(hRect.xMax - 90f, hRect.yMax - 36f, 78f, 28f), LocalizationManager.T("CLOSE", "KAPAT")))
                {
                    mShowHelp = false;
                }
            }

            // Draw Toast notification banner in cutout-safe top center
            if (mToastTimer > 0f && !string.IsNullOrEmpty(mToastMessage))
            {
                GUI.skin.label.fontSize = 16;

                float toastWidth = Mathf.Min(460f, safeRect.width * 0.72f);
                float toastHeight = 36f;
                Rect toastRect = new Rect(safeRect.x + (safeRect.width - toastWidth) * 0.5f, safeRect.y + 10f, toastWidth, toastHeight);

                float alpha = Mathf.Clamp01(mToastTimer / 0.3f);
                GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.92f * alpha);
                GUI.DrawTexture(toastRect, Texture2D.whiteTexture);

                GUI.color = new Color(1f, 0.9f, 0.2f, alpha);
                GUIStyle toastStyle = new GUIStyle(GUI.skin.label);
                toastStyle.alignment = TextAnchor.MiddleCenter;
                toastStyle.fontStyle = FontStyle.Bold;
                toastStyle.richText = true;

                GUI.Label(toastRect, mToastMessage, toastStyle);
                GUI.color = Color.white;
            }
        }
        finally
        {
            GUI.skin.label.font = oldLblFont;
            GUI.skin.button.font = oldBtnFont;
            GUI.skin.label.fontSize = oldLabelSize;
            GUI.skin.button.fontSize = oldBtnSize;
            GUI.matrix = prevMatrix;
        }
    }

    private void DrawDefeatRestartUI(Rect safeRect, bool isTouchDevice)
    {
        float pWidth = Mathf.Min(440f, safeRect.width * 0.9f);
        float pHeight = 90f;
        float pX = safeRect.x + (safeRect.width - pWidth) * 0.5f;
        float pY = safeRect.yMax - pHeight - 32f;

        // Box
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        GUI.DrawTexture(new Rect(pX, pY, pWidth, pHeight), Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(new Rect(pX, pY, pWidth, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Buttons
        float btnW = (pWidth - 60f) * 0.5f;
        float btnH = 46f;
        float btnY = pY + 22f;

        // Green Restart Button
        GUI.color = new Color(0.2f, 0.8f, 0.3f, 1f);
        string retryLbl = LocalizationManager.L("mod_qol_retry", "RETRY", "TEKRAR DENE");
        if (GUI.Button(new Rect(pX + 20f, btnY, btnW, btnH), isTouchDevice ? retryLbl : ("[R] " + retryLbl)))
        {
            RestartCurrentWave();
        }

        // Dark Exit Button
        GUI.color = new Color(0.8f, 0.3f, 0.3f, 1f);
        string mapLbl = LocalizationManager.L("mod_qol_map", "MAP", "HARITA");
        if (GUI.Button(new Rect(pX + pWidth - btnW - 20f, btnY, btnW, btnH), isTouchDevice ? mapLbl : ("[ESC] " + mapLbl)))
        {
            Singleton<MenusFlow>.instance.LoadScene("Debriefing");
        }
        GUI.color = Color.white;
    }
}