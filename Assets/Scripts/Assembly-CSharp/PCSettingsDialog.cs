using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PC Settings Dialog — v3.0
///
/// Dil butonu → LocalizationManager.CycleLanguage() → Localizer.Reload()
/// SADECE Localizer'ın text cache'ini sıfırlar.
/// Orijinal oyun sistemlerine (GameVersion, MultiLanguages, sprite/font) DOKUNMAZ.
///
/// NOT: Dil değişikliği yeni açılan ekranlarda/geçilen sahnelerde etkili olur.
/// Mevcut ekrandaki widget'ların metni değişmez çünkü SUILayout text'leri
/// oluşturma anında bir kere okur ve cache'ler.
/// </summary>
public class PCSettingsDialog : IDialog
{
    private const string PREF_FULLSCREEN = "SVZ_Fullscreen";
    private const string PREF_VOLUME = "SVZ_Volume";
    private const string PREF_RES_WIDTH = "SVZ_ResWidth";
    private const string PREF_RES_HEIGHT = "SVZ_ResHeight";
    private const int MIN_SHARP_WIDTH = 1280;
    private const int MIN_SHARP_HEIGHT = 720;

    private SUILayout mLayout;
    private bool mDone;
    private int mSelectedTab = 0;

    private int mSelectedQuality;
    private int mSelectedShadow;
    private int mSelectedAA;
    private int mSelectedFPS;
    private bool mVSyncEnabled;
    private bool mShowFPS;
    private int mFPSPosition;
    private bool mFullscreen;
    private int mVolumeInt;

    private List<Resolution> mResolutions = new List<Resolution>();
    private int mSelectedResIndex = 0;
    private PCInputManager.Action? mRebindingAction = null;
    private float _lastProcessedTime = 0f;
    private string mExportBtnFeedback = string.Empty;
    private string mImportBtnFeedback = string.Empty;
    private string mInitialLanguage = string.Empty;
    private bool mReloadMainMenuOnDestroy = false;
    private bool mDestroyed = false;

    public bool isDone { get { return mDone && !mLayout.isAnimating; } }
    public bool isBlocking { get { return true; } }
    public float priority { get { return mLayout.basePriority; } set { mLayout.basePriority = value; } }

    public PCSettingsDialog()
    {
        mSelectedQuality = Mathf.Clamp(PlayerPrefs.GetInt(PerformanceSettings.PREF_QUALITY, 2), 0, PerformanceSettings.qualityCount - 1);
        mSelectedShadow = Mathf.Clamp(PlayerPrefs.GetInt(PerformanceSettings.PREF_SHADOW, 2), 0, PerformanceSettings.shadowCount - 1);
        mSelectedAA = Mathf.Clamp(PlayerPrefs.GetInt(PerformanceSettings.PREF_AA, 0), 0, PerformanceSettings.aaCount - 1);
        mSelectedFPS = GetSavedFPSLimitIndex();
        mVSyncEnabled = PlayerPrefs.GetInt(PerformanceSettings.PREF_VSYNC, 0) == 1;
        PerformanceSettings.NormalizeFramePrefs(ref mSelectedFPS, ref mVSyncEnabled);
        mShowFPS = FPSCounter.showFPS;
        mFPSPosition = FPSCounter.positionIndex;
        mFullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;

        float savedVol = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);
        mVolumeInt = Mathf.Clamp(Mathf.RoundToInt(savedVol * 100f), 0, 100);

        InitResolutions();
        mLayout = new SUILayout("Layouts/PCSettingsDialog");

        BindButton("tabGraphicsBtn", delegate { SetTab(0); });
        BindButton("tabDisplayBtn", delegate { SetTab(1); });
        BindButton("tabAudioBtn", delegate { SetTab(2); });
        BindButton("tabControlsBtn", delegate { SetTab(3); });
        BindButton("tabProfileBtn", delegate { SetTab(4); });

        BindButton("qualityBtn", delegate { OnButtonPressed("qualityBtn"); });
        BindButton("shadowBtn", delegate { OnButtonPressed("shadowBtn"); });
        BindButton("aaBtn", delegate { OnButtonPressed("aaBtn"); });
        BindButton("fpsBtn", delegate { OnButtonPressed("fpsBtn"); });
        BindButton("vsyncBtn", delegate { OnButtonPressed("vsyncBtn"); });
        BindButton("showFPSBtn", delegate { OnButtonPressed("showFPSBtn"); });
        BindButton("fpsPosBtn", delegate { OnButtonPressed("fpsPosBtn"); });

        BindButton("resPrevBtn", delegate { OnButtonPressed("resPrevBtn"); });
        BindButton("resNextBtn", delegate { OnButtonPressed("resNextBtn"); });
        BindButton("screenBtn", delegate { OnButtonPressed("screenBtn"); });
        BindButton("sharpBtn", delegate { OnButtonPressed("sharpBtn"); });

        BindButton("volDownBtn", delegate { OnButtonPressed("volDownBtn"); });
        BindButton("volUpBtn", delegate { OnButtonPressed("volUpBtn"); });
        BindButton("sndTestBtn", delegate { OnButtonPressed("sndTestBtn"); });

        BindButton("slotPrevBtn", delegate { OnButtonPressed("slotPrevBtn"); });
        BindButton("slotNextBtn", delegate { OnButtonPressed("slotNextBtn"); });
        BindButton("exportBtn", delegate { OnButtonPressed("exportBtn"); });
        BindButton("importBtn", delegate { OnButtonPressed("importBtn"); });

        BindButton("langBtn", delegate { OnButtonPressed("langBtn"); });
        BindButton("devToolsBtn", delegate { DevCheatOverlay.Open(); });

        BindButton("kbLeftBtn", delegate { StartRebinding(PCInputManager.Action.MoveLeft); });
        BindButton("kbRightBtn", delegate { StartRebinding(PCInputManager.Action.MoveRight); });
        BindButton("kbPotionBtn", delegate { StartRebinding(PCInputManager.Action.UsePotion); });
        BindButton("kbAbilityBtn", delegate { StartRebinding(PCInputManager.Action.UseAbility); });
        BindButton("kbAbility2Btn", delegate { StartRebinding(PCInputManager.Action.UseAbility2); });
        BindButton("kbH1Btn", delegate { StartRebinding(PCInputManager.Action.Helper1); });
        BindButton("kbH2Btn", delegate { StartRebinding(PCInputManager.Action.Helper2); });
        BindButton("kbH3Btn", delegate { StartRebinding(PCInputManager.Action.Helper3); });
        BindButton("kbH4Btn", delegate { StartRebinding(PCInputManager.Action.Helper4); });
        BindButton("kbH5Btn", delegate { StartRebinding(PCInputManager.Action.Helper5); });
        BindButton("kbResetBtn", delegate { PCInputManager.ResetToDefaults(); RefreshLabels(); });

        BindButton("settingsBackBtn", delegate { Close(); });

        SetTab(0);
        ApplyAllSettings();
        mInitialLanguage = LocalizationManager.instance.GetCurrentLanguage();
        LocalizationManager.instance.OnLanguageChanged += OnLanguageChangedHandler;
        mLayout.AnimateIn();
    }

    private bool IsLayoutAlive()
    {
        if (mDestroyed || mLayout == null) return false;
        if (!mLayout.Exists("settingsBg")) return false;
        SUIWidget bg = mLayout["settingsBg"] as SUIWidget;
        return bg != null && bg.gameObject != null;
    }

    private void OnLanguageChangedHandler(string newLang)
    {
        if (!IsLayoutAlive())
        {
            if (LocalizationManager.IsAlive())
            {
                LocalizationManager.instance.OnLanguageChanged -= OnLanguageChangedHandler;
            }
            mLayout = null;
            mDestroyed = true;
            return;
        }
        RefreshLabels();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Oyun açılışında çağrılır — kayıtlı dili yükle
    //  Bunu oyunun başlangıç koduna ekleyin (örn. MainMenu veya AppStart)
    // ════════════════════════════════════════════════════════════════════════
    public static void ApplyStartupSettings()
    {
        PerformanceSettings.ApplySavedSettings();

        AudioListener.volume = PlayerPrefs.GetFloat(PREF_VOLUME, 1f);

        // ── Dil yükleme ──────────────────────────────────────────────────
        // Bu Localizer henüz hiç cache'lememişken çağrılırsa,
        // tüm oyun metinleri başından beri seçilen dilde yüklenir.
        LocalizationManager.instance.ApplySavedLanguage();

        PCInputManager.LoadBindings();
    }

    public static int GetDefaultFPSLimitIndex()
    {
        return PerformanceSettings.GetDefaultFPSLimitIndex();
    }

    public static int GetSavedFPSLimitIndex()
    {
        return PerformanceSettings.GetSavedFPSLimitIndex();
    }

    public static int GetTargetFrameRateForIndex(int fpsIdx)
    {
        return PerformanceSettings.GetTargetFrameRateForIndex(fpsIdx);
    }

    public static void MarkFPSLimitPrefsMigrated()
    {
        PlayerPrefs.SetInt(PerformanceSettings.PREF_FPS_LIMIT_VERSION, 1);
    }

    private void OnButtonPressed(string widgetId)
    {
        if (Time.unscaledTime - _lastProcessedTime < 0.15f) return;
        _lastProcessedTime = Time.unscaledTime;

        switch (widgetId)
        {
            case "langBtn":
                LocalizationManager.OpenLanguagePicker();
                break;

            case "qualityBtn":
                mSelectedQuality = (mSelectedQuality + 1) % PerformanceSettings.qualityCount;
                PerformanceSettings.ApplyPreset(mSelectedQuality, out mSelectedQuality, out mSelectedShadow, out mSelectedAA, out mSelectedFPS, out mVSyncEnabled);
                break;
            case "shadowBtn":
                mSelectedShadow = (mSelectedShadow + 1) % PerformanceSettings.shadowCount;
                PerformanceSettings.ApplySettings(mSelectedQuality, mSelectedShadow, mSelectedAA, mSelectedFPS, mVSyncEnabled);
                break;
            case "aaBtn":
                mSelectedAA = (mSelectedAA + 1) % PerformanceSettings.aaCount;
                PerformanceSettings.ApplySettings(mSelectedQuality, mSelectedShadow, mSelectedAA, mSelectedFPS, mVSyncEnabled);
                break;
            case "fpsBtn":
                mSelectedFPS = (mSelectedFPS + 1) % PerformanceSettings.fpsCount;
                PerformanceSettings.NormalizeFramePrefs(ref mSelectedFPS, ref mVSyncEnabled);
                PerformanceSettings.ApplyFrameRate(mSelectedFPS, mVSyncEnabled);
                break;
            case "vsyncBtn":
                mVSyncEnabled = !mVSyncEnabled;
                PerformanceSettings.NormalizeFramePrefs(ref mSelectedFPS, ref mVSyncEnabled);
                PerformanceSettings.ApplyFrameRate(mSelectedFPS, mVSyncEnabled);
                break;
            case "showFPSBtn":
                mShowFPS = !mShowFPS;
                FPSCounter.showFPS = mShowFPS;
                break;
            case "fpsPosBtn":
                mFPSPosition = (mFPSPosition + 1) % FPSCounter.positionCount;
                FPSCounter.positionIndex = mFPSPosition;
                break;

            case "resPrevBtn":
                mSelectedResIndex = (mSelectedResIndex <= 0) ? mResolutions.Count - 1 : mSelectedResIndex - 1;
                ApplyResolution();
                break;
            case "resNextBtn":
                mSelectedResIndex = (mSelectedResIndex + 1) % mResolutions.Count;
                ApplyResolution();
                break;
            case "screenBtn":
                mFullscreen = !mFullscreen;
                ApplyResolution();
                break;
            case "sharpBtn":
                RestoreSharpVisualDefaults();
                break;

            case "volDownBtn":
                mVolumeInt = Mathf.Max(0, mVolumeInt - 5);
                AudioListener.volume = mVolumeInt / 100f;
                break;
            case "volUpBtn":
                mVolumeInt = Mathf.Min(100, mVolumeInt + 5);
                AudioListener.volume = mVolumeInt / 100f;
                break;
            case "sndTestBtn":
                if (Singleton<SUISoundManager>.instance != null)
                    Singleton<SUISoundManager>.instance.Play("StartHorn");
                break;

            case "slotPrevBtn":
                int pS = Singleton<Profile>.instance.currentSlot - 1;
                Singleton<Profile>.instance.currentSlot = (pS < 0) ? 2 : pS;
                break;
            case "slotNextBtn":
                Singleton<Profile>.instance.currentSlot = (Singleton<Profile>.instance.currentSlot + 1) % 3;
                break;
            case "exportBtn":
                string exportedData = Singleton<Profile>.instance.ExportSaveData();
                if (!string.IsNullOrEmpty(exportedData))
                {
                    GUIUtility.systemCopyBuffer = exportedData;
                    mExportBtnFeedback = "COPIED!";
                }
                else
                {
                    mExportBtnFeedback = "EMPTY!";
                }
                mImportBtnFeedback = string.Empty;
                break;
            case "importBtn":
                string raw = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(raw))
                {
                    Singleton<Profile>.instance.ImportSaveData(raw);
                    mImportBtnFeedback = "IMPORTED!";
                }
                else
                {
                    mImportBtnFeedback = "EMPTY!";
                }
                mExportBtnFeedback = string.Empty;
                break;
        }

        SavePrefs();
        RefreshLabels();
    }

    public void Update()
    {
        if (!IsLayoutAlive() || DevCheatOverlay.IsOpen || LocalizationManager.IsPickerOpen)
        {
            return;
        }

        mLayout.Update();

        if (mRebindingAction.HasValue)
        {
            if (Input.anyKeyDown)
            {
                foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(k) && k != KeyCode.Escape)
                    {
                        PCInputManager.SetBinding(mRebindingAction.Value, k);
                        PCInputManager.SaveBindings();
                        mRebindingAction = null;
                        RefreshLabels();
                        break;
                    }
                }
            }
        }
        else if (Input.GetKeyUp(KeyCode.Escape))
        {
            Close();
        }
    }

    private void RefreshLabels()
    {
        if (!IsLayoutAlive()) return;
        bool tG = mSelectedTab == 0, tD = mSelectedTab == 1,
             tA = mSelectedTab == 2, tC = mSelectedTab == 3, tP = mSelectedTab == 4;

        SetWidgetVisible("qualityLbl", tG); SetWidgetVisible("qualityBtn", tG);
        SetWidgetVisible("shadowLbl", tG); SetWidgetVisible("shadowBtn", tG);
        SetWidgetVisible("aaLbl", tG); SetWidgetVisible("aaBtn", tG);
        SetWidgetVisible("fpsLbl", tG); SetWidgetVisible("fpsBtn", tG);
        SetWidgetVisible("vsyncLbl", tG); SetWidgetVisible("vsyncBtn", tG);
        SetWidgetVisible("showFPSLbl", tG); SetWidgetVisible("showFPSBtn", tG);
        SetWidgetVisible("fpsPosLbl", tG); SetWidgetVisible("fpsPosBtn", tG);

        SetWidgetVisible("resLbl", tD); SetWidgetVisible("resValueLbl", tD);
        SetWidgetVisible("resPrevBtn", tD); SetWidgetVisible("resNextBtn", tD);
        SetWidgetVisible("screenLbl", tD); SetWidgetVisible("screenBtn", tD);
        SetWidgetVisible("sharpLbl", tD); SetWidgetVisible("sharpBtn", tD);

        SetWidgetVisible("volLbl", tA); SetWidgetVisible("volValueLbl", tA);
        SetWidgetVisible("volDownBtn", tA); SetWidgetVisible("volUpBtn", tA);
        SetWidgetVisible("sndTestLbl", tA); SetWidgetVisible("sndTestBtn", tA);

        SetWidgetVisible("kbLeftLbl", tC); SetWidgetVisible("kbLeftBtn", tC);
        SetWidgetVisible("kbRightLbl", tC); SetWidgetVisible("kbRightBtn", tC);
        SetWidgetVisible("kbPotionLbl", tC); SetWidgetVisible("kbPotionBtn", tC);
        SetWidgetVisible("kbAbilityLbl", tC); SetWidgetVisible("kbAbilityBtn", tC);
        SetWidgetVisible("kbAbility2Lbl", tC); SetWidgetVisible("kbAbility2Btn", tC);
        SetWidgetVisible("kbH1Lbl", tC); SetWidgetVisible("kbH1Btn", tC);
        SetWidgetVisible("kbH2Lbl", tC); SetWidgetVisible("kbH2Btn", tC);
        SetWidgetVisible("kbH3Lbl", tC); SetWidgetVisible("kbH3Btn", tC);
        SetWidgetVisible("kbH4Lbl", tC); SetWidgetVisible("kbH4Btn", tC);
        SetWidgetVisible("kbH5Lbl", tC); SetWidgetVisible("kbH5Btn", tC);
        SetWidgetVisible("kbResetBtn", tC); SetWidgetVisible("kbInfoLbl", tC);

        SetWidgetVisible("slotLbl", tP); SetWidgetVisible("slotValueLbl", tP);
        SetWidgetVisible("slotPrevBtn", tP); SetWidgetVisible("slotNextBtn", tP);
        SetWidgetVisible("exportBtn", tP); SetWidgetVisible("importBtn", tP);
        SetWidgetVisible("langLbl", tP); SetWidgetVisible("langBtn", tP);
        SetWidgetVisible("devToolsLbl", tP && DevCheatOverlay.IsEnabled);
        SetWidgetVisible("devToolsBtn", tP && DevCheatOverlay.IsEnabled);

        if (mLayout.Exists("tabGraphicsBtn")) ((SUIButton)mLayout["tabGraphicsBtn"]).alpha = tG ? 1f : 0.5f;
        if (mLayout.Exists("tabDisplayBtn")) ((SUIButton)mLayout["tabDisplayBtn"]).alpha = tD ? 1f : 0.5f;
        if (mLayout.Exists("tabAudioBtn")) ((SUIButton)mLayout["tabAudioBtn"]).alpha = tA ? 1f : 0.5f;
        if (mLayout.Exists("tabControlsBtn")) ((SUIButton)mLayout["tabControlsBtn"]).alpha = tC ? 1f : 0.5f;
        if (mLayout.Exists("tabProfileBtn")) ((SUIButton)mLayout["tabProfileBtn"]).alpha = tP ? 1f : 0.5f;

        if (mLayout.Exists("qualityBtn")) ((SUIButton)mLayout["qualityBtn"]).text = PerformanceSettings.GetQualityLabel(mSelectedQuality);
        if (mLayout.Exists("shadowBtn")) ((SUIButton)mLayout["shadowBtn"]).text = PerformanceSettings.GetShadowLabel(mSelectedShadow);
        if (mLayout.Exists("aaBtn")) ((SUIButton)mLayout["aaBtn"]).text = PerformanceSettings.GetAALabel(mSelectedAA);
        if (mLayout.Exists("fpsBtn")) ((SUIButton)mLayout["fpsBtn"]).text = PerformanceSettings.GetFPSLabel(mSelectedFPS);
        if (mLayout.Exists("vsyncBtn")) ((SUIButton)mLayout["vsyncBtn"]).text = mVSyncEnabled ? "ON" : "OFF";
        if (mLayout.Exists("showFPSBtn")) ((SUIButton)mLayout["showFPSBtn"]).text = mShowFPS ? "ON" : "OFF";
        if (mLayout.Exists("fpsPosBtn")) ((SUIButton)mLayout["fpsPosBtn"]).text = FPSCounter.positionLabel;
        if (mLayout.Exists("screenBtn")) ((SUIButton)mLayout["screenBtn"]).text = mFullscreen ? "ON" : "OFF";
        if (mLayout.Exists("sharpBtn")) ((SUIButton)mLayout["sharpBtn"]).text = "RESTORE";

        // ── Dil butonu metni ──────────────────────────────────────────────
        if (mLayout.Exists("langBtn"))
            ((SUIButton)mLayout["langBtn"]).text =
                LocalizationManager.instance.GetCurrentLanguage().ToUpper();

        if (mLayout.Exists("devToolsBtn"))
            ((SUIButton)mLayout["devToolsBtn"]).text = "DEV TOOLS";

        if (mLayout.Exists("resValueLbl") && mResolutions.Count > 0)
            ((SUILabel)mLayout["resValueLbl"]).text =
                mResolutions[mSelectedResIndex].width + "x" + mResolutions[mSelectedResIndex].height;

        if (mLayout.Exists("volValueLbl"))
            ((SUILabel)mLayout["volValueLbl"]).text = "%" + Mathf.RoundToInt(AudioListener.volume * 100);

        if (mLayout.Exists("slotValueLbl"))
            ((SUILabel)mLayout["slotValueLbl"]).text = "SLOT " + (Singleton<Profile>.instance.currentSlot + 1);

        if (mLayout.Exists("exportBtn"))
            ((SUIButton)mLayout["exportBtn"]).text = string.IsNullOrEmpty(mExportBtnFeedback) ? "EXPORT SAVE" : mExportBtnFeedback;

        if (mLayout.Exists("importBtn"))
            ((SUIButton)mLayout["importBtn"]).text = string.IsNullOrEmpty(mImportBtnFeedback) ? "IMPORT SAVE" : mImportBtnFeedback;

        if (tC) UpdateControlLabels();
    }

    private void UpdateControlLabels()
    {
        SetKeyLabel("kbLeftBtn", PCInputManager.Action.MoveLeft);
        SetKeyLabel("kbRightBtn", PCInputManager.Action.MoveRight);
        SetKeyLabel("kbPotionBtn", PCInputManager.Action.UsePotion);
        SetKeyLabel("kbAbilityBtn", PCInputManager.Action.UseAbility);
        SetKeyLabel("kbAbility2Btn", PCInputManager.Action.UseAbility2);
        SetKeyLabel("kbH1Btn", PCInputManager.Action.Helper1);
        SetKeyLabel("kbH2Btn", PCInputManager.Action.Helper2);
        SetKeyLabel("kbH3Btn", PCInputManager.Action.Helper3);
        SetKeyLabel("kbH4Btn", PCInputManager.Action.Helper4);
        SetKeyLabel("kbH5Btn", PCInputManager.Action.Helper5);

        if (mLayout.Exists("kbInfoLbl"))
            ((SUILabel)mLayout["kbInfoLbl"]).text =
                mRebindingAction.HasValue ? "PRESS ANY KEY" : "CLICK TO REBIND";
    }

    private void SetKeyLabel(string widgetId, PCInputManager.Action action)
    {
        if (!mLayout.Exists(widgetId)) return;
        ((SUIButton)mLayout[widgetId]).text =
            (mRebindingAction == action) ? "???" : PCInputManager.GetBindingName(action);
    }

    private void SavePrefs()
    {
        PerformanceSettings.NormalizeFramePrefs(ref mSelectedFPS, ref mVSyncEnabled);
        PerformanceSettings.SavePrefs(mSelectedQuality, mSelectedShadow, mSelectedAA, mSelectedFPS, mVSyncEnabled);
        FPSCounter.showFPS = mShowFPS;
        FPSCounter.positionIndex = mFPSPosition;
        PlayerPrefs.SetInt(PREF_FULLSCREEN, mFullscreen ? 1 : 0);
        PlayerPrefs.SetFloat(PREF_VOLUME, AudioListener.volume);
        PlayerPrefs.Save();
    }

    private void BindButton(string id, OnSUIGenericCallback action)
    {
        if (mLayout.Exists(id)) ((SUIButton)mLayout[id]).onButtonPressed = action;
    }

    private void SetTab(int tabIndex)
    {
        mSelectedTab = tabIndex;
        mRebindingAction = null;
        mExportBtnFeedback = string.Empty;
        mImportBtnFeedback = string.Empty;
        RefreshLabels();
    }

    private void StartRebinding(PCInputManager.Action action)
    {
        mRebindingAction = action;
        RefreshLabels();
    }

    private void SetWidgetVisible(string id, bool visible)
    {
        if (mLayout != null && mLayout.Exists(id))
        {
            SUIWidget w = mLayout[id] as SUIWidget;
            if (w != null && w.gameObject != null) w.visible = visible;
        }
    }

    private void ApplyResolution()
    {
        if (mResolutions.Count > 0)
        {
            Resolution target = mResolutions[mSelectedResIndex];
            bool fs = SVZDeviceCompatibility.IsMobileOrTablet ? true : mFullscreen;
            if (target.width != Screen.width || target.height != Screen.height || Screen.fullScreen != fs)
            {
                Screen.SetResolution(target.width, target.height, fs);
            }
            PlayerPrefs.SetInt(PREF_RES_WIDTH, target.width);
            PlayerPrefs.SetInt(PREF_RES_HEIGHT, target.height);
        }
    }

    private void RestoreSharpVisualDefaults()
    {
        mSelectedQuality = Mathf.Clamp(2, 0, PerformanceSettings.qualityCount - 1);
        mSelectedShadow = Mathf.Clamp(2, 0, PerformanceSettings.shadowCount - 1);
        mSelectedAA = 0;
        mSelectedFPS = Mathf.Clamp(GetDefaultFPSLimitIndex(), 0, PerformanceSettings.fpsCount - 1);
        mVSyncEnabled = false;
        mFullscreen = true;
        mSelectedResIndex = FindBestResolutionIndex();
        ApplyAllSettings();
        SavePrefs();
    }

    private static void ApplyShadowQuality(int level)
    {
        switch (level)
        {
            case 0: QualitySettings.shadows = ShadowQuality.HardOnly; break;
            case 1: QualitySettings.shadows = ShadowQuality.All; break;
            case 2: QualitySettings.shadows = ShadowQuality.All; break;
            case 3: QualitySettings.shadows = ShadowQuality.Disable; break;
        }
    }

    private static void ApplyAntiAliasing(int level)
    {
        QualitySettings.antiAliasing = (level == 0) ? 0 : (level == 1) ? 2 : 8;
    }

    private void ApplyAllSettings()
    {
        ApplyResolution();
        PerformanceSettings.ApplySettings(mSelectedQuality, mSelectedShadow, mSelectedAA, mSelectedFPS, mVSyncEnabled);
        AudioListener.volume = mVolumeInt / 100f;
    }

    private void InitResolutions()
    {
        mResolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width >= 800)
            {
                bool duplicate = false;
                for (int j = 0; j < mResolutions.Count; j++)
                {
                    if (mResolutions[j].width == r.width && mResolutions[j].height == r.height)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) mResolutions.Add(r);
            }
        }

        // On Android / TV Box, ensure native current resolution AND standard landscape presets exist
        if (Screen.width >= 800 && Screen.height >= 480)
        {
            bool nativeExists = false;
            for (int j = 0; j < mResolutions.Count; j++)
            {
                if (mResolutions[j].width == Screen.width && mResolutions[j].height == Screen.height)
                {
                    nativeExists = true;
                    break;
                }
            }
            if (!nativeExists)
            {
                mResolutions.Add(new Resolution { width = Screen.width, height = Screen.height });
            }
        }

        // Add standard resolutions so players on TV Box and mobile can freely toggle quality!
        int[] standardHeights = { 1080, 900, 720, 540 };
        float aspect = (float)Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
        if (aspect < 1.0f) aspect = 16f / 9f; // default landscape

        foreach (int h in standardHeights)
        {
            int w = Mathf.RoundToInt(h * aspect);
            bool exists = false;
            for (int j = 0; j < mResolutions.Count; j++)
            {
                if (mResolutions[j].width == w && mResolutions[j].height == h)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                Resolution res = new Resolution { width = w, height = h };
                mResolutions.Add(res);
            }
        }

        if (mResolutions.Count == 0)
            mResolutions.Add(new Resolution { width = Screen.width, height = Screen.height });

        // Sort descending: highest resolution first (e.g. 1080p, 900p, 720p, 540p)
        mResolutions.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));

        mSelectedResIndex = FindBestResolutionIndex();
        int savedW = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.width);
        int savedH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.height);
        if (savedW >= MIN_SHARP_WIDTH && savedH >= MIN_SHARP_HEIGHT)
        {
            for (int i = 0; i < mResolutions.Count; i++)
            {
                if (mResolutions[i].width == savedW && mResolutions[i].height == savedH)
                {
                    mSelectedResIndex = i;
                    break;
                }
            }
        }
    }

    private int FindBestResolutionIndex()
    {
        if (mResolutions.Count == 0)
        {
            return 0;
        }

        int targetW = Screen.currentResolution.width;
        int targetH = Screen.currentResolution.height;
        if (targetW <= 0 || targetH <= 0)
        {
            targetW = Screen.width;
            targetH = Screen.height;
        }

        int bestIndex = 0;
        int bestScore = int.MaxValue;
        for (int i = 0; i < mResolutions.Count; i++)
        {
            Resolution r = mResolutions[i];
            int score = Mathf.Abs(r.width - targetW) + Mathf.Abs(r.height - targetH);
            if (score < bestScore || (score == bestScore && r.width * r.height > mResolutions[bestIndex].width * mResolutions[bestIndex].height))
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    public void Destroy()
    {
        mDestroyed = true;
        if (LocalizationManager.IsAlive())
        {
            LocalizationManager.instance.OnLanguageChanged -= OnLanguageChangedHandler;
        }
        LocalizationManager.CloseLanguagePicker();
        if (mLayout != null)
        {
            mLayout.Destroy();
            mLayout = null;
        }
        if (mReloadMainMenuOnDestroy && WeakGlobalSceneBehavior<MainMenuImpl>.instance != null)
        {
            Singleton<MenusFlow>.instance.LoadScene("MainMenu");
        }
    }

    private void Close()
    {
        if (LocalizationManager.IsAlive())
        {
            LocalizationManager.instance.OnLanguageChanged -= OnLanguageChangedHandler;
        }
        LocalizationManager.CloseLanguagePicker();
        SavePrefs();
        mDone = true;
        mLayout.AnimateOut();
        if (LocalizationManager.IsAlive()
            && LocalizationManager.instance.GetCurrentLanguage() != mInitialLanguage
            && WeakGlobalSceneBehavior<MainMenuImpl>.instance != null)
        {
            mReloadMainMenuOnDestroy = true;
        }
    }
}
