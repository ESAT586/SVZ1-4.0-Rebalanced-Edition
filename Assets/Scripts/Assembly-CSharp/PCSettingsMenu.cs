using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PCSettingsMenu v2 - Premium Settings Interface
/// Contains Tabs for Display, Graphics, Audio, and Controls.
/// Integrates with PCPostProcessingManager and Application Settings.
/// </summary>
public class PCSettingsMenu : MonoBehaviour
{
    private const string PREF_QUALITY    = "SVZ_QualityLevel";
    private const string PREF_FPS_LIMIT  = "SVZ_FPSLimit";
    private const string PREF_VSYNC      = "SVZ_VSync";
    private const string PREF_FULLSCREEN = "SVZ_Fullscreen";
    private const string PREF_VOLUME     = "SVZ_Volume";
    private const string PREF_RES_WIDTH  = "SVZ_ResWidth";
    private const string PREF_RES_HEIGHT = "SVZ_ResHeight";

    private static readonly string[] kQualityNames = { "Low", "Medium", "High", "Ultra" };
    private static readonly int[]    kFPSOptions   = { 30, 60, 90, 120, 144, 0 };
    private static readonly string[] kFPSLabels    = { "30 FPS", "60 FPS", "90 FPS", "120 FPS", "144 FPS", "Uncapped" };
    private static readonly string[] kTabs         = { "GRAPHICS", "DISPLAY", "AUDIO", "CONTROLS" };

    private bool  mVisible;
    private Rect  mWindowRect;
    private int   mSelectedTab = 0;
    private Vector2 mScrollPos;

    // Graphics
    private int   mSelectedQuality;
    private int   mSelectedFPS;
    private bool  mVSync;
    private bool  mShowFPS;
    private int   mFPSPosition;

    // Audio
    private float mMasterVolume;

    // Display
    private List<Resolution> mResolutions = new List<Resolution>();
    private int mSelectedResIndex = 0;
    private bool mIsFullscreen;

    void Start()
    {
        float w = 500f, h = 600f;
        mWindowRect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        mSelectedQuality = Mathf.Clamp(PlayerPrefs.GetInt(PREF_QUALITY, 2), 0, kQualityNames.Length - 1);
        mSelectedFPS     = PCSettingsDialog.GetSavedFPSLimitIndex();
        mVSync           = PlayerPrefs.GetInt(PREF_VSYNC, 0) == 1;
        mShowFPS         = FPSCounter.showFPS;
        mFPSPosition     = FPSCounter.positionIndex;
        mMasterVolume    = Mathf.Clamp01(PlayerPrefs.GetFloat(PREF_VOLUME, 1f));
        mIsFullscreen    = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;

        int savedW = PlayerPrefs.GetInt(PREF_RES_WIDTH, Screen.width);
        int savedH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, Screen.height);

        Resolution[] allRes = Screen.resolutions;
        for (int i = 0; i < allRes.Length; i++)
        {
            if (allRes[i].refreshRate > 0 && allRes[i].width >= 800)
            {
                bool exists = false;
                foreach (var r in mResolutions)
                    if (r.width == allRes[i].width && r.height == allRes[i].height) exists = true;
                
                if (!exists)
                {
                    mResolutions.Add(allRes[i]);
                    if (allRes[i].width == savedW && allRes[i].height == savedH)
                        mSelectedResIndex = mResolutions.Count - 1;
                }
            }
        }

        if (mResolutions.Count == 0) // Fallback
        {
            Resolution fallback = new Resolution { width = Screen.width, height = Screen.height };
            mResolutions.Add(fallback);
        }

        ApplyAll();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            mVisible = !mVisible;
            Time.timeScale = mVisible ? 0f : 1f;
        }
    }

    void OnGUI()
    {
        if (!mVisible) return;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Draw modern window style
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.normal.background = Texture2D.whiteTexture; // Or dark custom texture
        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f); 

        mWindowRect = GUI.Window(8888, mWindowRect, DrawSettingsWindow, "", windowStyle);
        GUI.backgroundColor = Color.white;
    }

    private void DrawSettingsWindow(int id)
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Space(10);
        GUILayout.Label("SVZ1 - PREMIUM SETTINGS", titleStyle);
        GUILayout.Space(15);

        // Custom Tabs
        GUILayout.BeginHorizontal();
        for (int i = 0; i < kTabs.Length; i++)
        {
            GUIStyle tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 35 };
            if (mSelectedTab == i)
            {
                GUI.backgroundColor = Color.gray;
                tabStyle.fontStyle = FontStyle.Bold;
            }
            if (GUILayout.Button(kTabs[i], tabStyle)) mSelectedTab = i;
            GUI.backgroundColor = Color.white; // reset
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(15);

        mScrollPos = GUILayout.BeginScrollView(mScrollPos, GUILayout.Height(380));

        switch (mSelectedTab)
        {
            case 0: DrawGraphicsTab(); break;
            case 1: DrawDisplayTab(); break;
            case 2: DrawAudioTab(); break;
            case 3: DrawControlsTab(); break;
        }

        GUILayout.EndScrollView();
        GUILayout.FlexibleSpace();

        // Footer
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("RESUME GAME (ESC)", GUILayout.Height(40)))
        {
            mVisible = false;
            Time.timeScale = 1f;
        }
        if (GUILayout.Button("QUIT TO DESKTOP", GUILayout.Height(40)))
        {
            Application.Quit();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        GUI.DragWindow(new Rect(0, 0, 10000, 40));
    }

    private void DrawHeader(string title)
    {
        GUIStyle header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        header.normal.textColor = new Color(0.8f, 0.8f, 1f);
        GUILayout.Label(title, header);
        GUILayout.Space(5);
    }

    private void DrawGraphicsTab()
    {
        DrawHeader("Visual Quality");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < kQualityNames.Length; i++)
        {
            bool selected = (mSelectedQuality == i);
            if (GUILayout.Toggle(selected, kQualityNames[i], "Button", GUILayout.Height(30)))
            {
                if (!selected)
                {
                    mSelectedQuality = i;
                    ApplyQuality();
                    Save();
                }
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(20);

        DrawHeader("Performance Limits (FPS)");
        GUILayout.BeginHorizontal();
        for (int i = 0; i < kFPSLabels.Length; i++)
        {
            bool selected = (mSelectedFPS == i);
            if (GUILayout.Toggle(selected, kFPSLabels[i], "Button", GUILayout.Height(30)))
            {
                if (!selected)
                {
                    mSelectedFPS = i;
                    PerformanceSettings.NormalizeFramePrefs(ref mSelectedFPS, ref mVSync);
                    PerformanceSettings.ApplyFrameRate(mSelectedFPS, mVSync);
                    Save();
                }
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(20);

        DrawHeader("Advanced Rendering");
        bool newVsync = GUILayout.Toggle(mVSync, "  Enable Vertical Sync (VSync eliminate screen tearing)", GUILayout.Height(25));
        if (newVsync != mVSync)
        {
            mVSync = newVsync;
            PerformanceSettings.NormalizeFramePrefs(ref mSelectedFPS, ref mVSync);
            PerformanceSettings.ApplyFrameRate(mSelectedFPS, mVSync);
            Save();
        }

        GUILayout.Space(20);
        DrawHeader("FPS Overlay");
        bool newShowFPS = GUILayout.Toggle(mShowFPS, "  Show FPS counter", GUILayout.Height(25));
        if (newShowFPS != mShowFPS)
        {
            mShowFPS = newShowFPS;
            FPSCounter.showFPS = mShowFPS;
            Save();
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Position", GUILayout.Width(100));
        if (GUILayout.Button(FPSCounter.positionLabel, GUILayout.Height(30)))
        {
            mFPSPosition = (mFPSPosition + 1) % FPSCounter.positionCount;
            FPSCounter.positionIndex = mFPSPosition;
            Save();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawDisplayTab()
    {
        DrawHeader("Screen Resolution");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(40), GUILayout.Height(30)))
        {
            mSelectedResIndex--;
            if (mSelectedResIndex < 0) mSelectedResIndex = mResolutions.Count - 1;
            ApplyResolution();
        }
        string resText = string.Format("{0} × {1}", mResolutions[mSelectedResIndex].width, mResolutions[mSelectedResIndex].height);
        GUILayout.Label(resText, new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter }, GUILayout.ExpandWidth(true), GUILayout.Height(30));
        if (GUILayout.Button("▶", GUILayout.Width(40), GUILayout.Height(30)))
        {
            mSelectedResIndex++;
            if (mSelectedResIndex >= mResolutions.Count) mSelectedResIndex = 0;
            ApplyResolution();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(20);

        DrawHeader("Display Mode");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(mIsFullscreen, " Fullscreen", "Button", GUILayout.Height(30)))
        {
            if (!mIsFullscreen) { mIsFullscreen = true; ApplyResolution(); }
        }
        if (GUILayout.Toggle(!mIsFullscreen, " Windowed", "Button", GUILayout.Height(30)))
        {
            if (mIsFullscreen) { mIsFullscreen = false; ApplyResolution(); }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawAudioTab()
    {
        DrawHeader("Master Volume");
        GUILayout.Label("Controls the global sound volume of the game.", new GUIStyle(GUI.skin.label) { fontSize = 12 });
        GUILayout.Space(10);
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Volume: ", GUILayout.Width(60));
        float newVol = GUILayout.HorizontalSlider(mMasterVolume, 0f, 1f);
        GUILayout.Label(Mathf.RoundToInt(newVol * 100) + "%", GUILayout.Width(40));
        GUILayout.EndHorizontal();

        if (Mathf.Abs(newVol - mMasterVolume) > 0.01f)
        {
            mMasterVolume = newVol;
            AudioListener.volume = mMasterVolume;
            Save();
        }
        GUILayout.Space(20);
        
        DrawHeader("Audio Features");
        if (GUILayout.Button("Test Sound Effects", GUILayout.Height(30)))
        {
            if (Singleton<SUISoundManager>.instance != null)
                Singleton<SUISoundManager>.instance.Play("UI_Click");
        }
    }

    private void DrawControlsTab()
    {
        DrawHeader("Keyboard Mapping");
        GUIStyle infoStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10,10,10,10) };
        string controls = "A / D / Left / Right Arrow\n   - Move Samurai\n\n" +
                          "Mouse Scroll Wheel\n   - Scroll Units / Items Menu\n\n" +
                          "Mouse Click\n   - Deploy Troops / Use Potions\n\n" +
                          "ESC\n   - Pause / Settings Menu\n\n" +
                          "F / Z / C\n   - Reserved for PC specific macros (future sync)";
        GUILayout.Box(controls, infoStyle, GUILayout.ExpandWidth(true));
        
        GUILayout.Space(10);
        GUILayout.Label("Note: Custom bindings config is loaded from the Unity Launcher.", new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic });
    }

    private void ApplyResolution()
    {
        Resolution r = mResolutions[mSelectedResIndex];
        Screen.SetResolution(r.width, r.height, mIsFullscreen);
        PlayerPrefs.SetInt(PREF_RES_WIDTH, r.width);
        PlayerPrefs.SetInt(PREF_RES_HEIGHT, r.height);
        PlayerPrefs.SetInt(PREF_FULLSCREEN, mIsFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyQuality()
    {
        int unityLevels = QualitySettings.names.Length;
        if (unityLevels > 0)
        {
            int mapped = Mathf.Clamp(mSelectedQuality, 0, unityLevels - 1);
            QualitySettings.SetQualityLevel(mapped, true);
        }
        
        // PCPostProcessingManager removed for Unity 2017 compatibility
    }

    private void ApplyAll()
    {
        ApplyQuality();
        PerformanceSettings.ApplyFrameRate(mSelectedFPS, mVSync);
        AudioListener.volume        = mMasterVolume;
        
        if (mResolutions.Count > 0)
        {
            Resolution r = mResolutions[mSelectedResIndex];
            Screen.SetResolution(r.width, r.height, mIsFullscreen);
        }
    }

    private void Save()
    {
        PerformanceSettings.SavePrefs(mSelectedQuality, PlayerPrefs.GetInt(PerformanceSettings.PREF_SHADOW, 2), PlayerPrefs.GetInt(PerformanceSettings.PREF_AA, 0), mSelectedFPS, mVSync);
        FPSCounter.showFPS = mShowFPS;
        FPSCounter.positionIndex = mFPSPosition;
        PlayerPrefs.SetFloat(PREF_VOLUME,     mMasterVolume);
        PlayerPrefs.SetInt(PREF_FULLSCREEN,   mIsFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
}
