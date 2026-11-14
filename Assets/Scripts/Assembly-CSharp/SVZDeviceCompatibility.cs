using System;
using UnityEngine;

/// <summary>
/// Universal Device Compatibility & Mobile/Tablet Optimization Manager (Android 5.0 -> Android 16 / Galaxy S24-S25 / Tablets / Foldables / TV / PC).
/// Handles multi-touch, 120Hz high refresh rate, landscape lock, cutout/notch & edge-to-edge safe margins via native bridge, and sleep prevention.
/// </summary>
public class SVZDeviceCompatibility : MonoBehaviour
{
    private const string PREF_FPS_LIMIT = "SVZ_FPSLimit";
    private const string PREF_RES_WIDTH = "SVZ_ResWidth";

    private static SVZDeviceCompatibility sInstance;
    private static bool sIsTvEvaluated;
    private static bool sCachedIsTv;
    private static bool sIsDesktopEvaluated;
    private static bool sCachedIsDesktop;
    private static int sCachedApiLevel = -1;
    private static string sCachedAndroidVersionLabel;
    private static int[] sCachedNativeInsets = new int[4];
    private static float sNextInsetPollTime;

    private int mLastWidth;
    private int mLastHeight;
    private Rect mLastSafeArea;

    public static bool IsMobileOrTablet
    {
        get
        {
            return Application.isMobilePlatform ||
                   Application.platform == RuntimePlatform.Android ||
                   Application.platform == RuntimePlatform.IPhonePlayer;
        }
    }

    public static float AspectRatio
    {
        get
        {
            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);
            return (float)Mathf.Max(w, h) / (float)Mathf.Min(w, h);
        }
    }

    public static bool IsUltraWidePhone
    {
        get { return AspectRatio >= 1.95f; }
    }

    public static int AndroidApiLevel
    {
        get
        {
            if (Application.platform != RuntimePlatform.Android) return 0;
            if (sCachedApiLevel >= 0) return sCachedApiLevel;
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
                {
                    sCachedApiLevel = bridge.CallStatic<int>("getApiLevel");
                    return sCachedApiLevel;
                }
            }
            catch
            {
                sCachedApiLevel = 0;
                return 0;
            }
        }
    }

    public static string AndroidVersionLabel
    {
        get
        {
            if (Application.platform != RuntimePlatform.Android) return SystemInfo.operatingSystem;
            if (!string.IsNullOrEmpty(sCachedAndroidVersionLabel)) return sCachedAndroidVersionLabel;
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
                {
                    sCachedAndroidVersionLabel = bridge.CallStatic<string>("getAndroidVersionLabel");
                }
            }
            catch
            {
                sCachedAndroidVersionLabel = SystemInfo.operatingSystem;
            }
            return string.IsNullOrEmpty(sCachedAndroidVersionLabel) ? SystemInfo.operatingSystem : sCachedAndroidVersionLabel;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void AutoInitialize()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (sInstance != null) return;
        GameObject go = new GameObject("[SVZDeviceCompatibility]");
        DontDestroyOnLoad(go);
        sInstance = go.AddComponent<SVZDeviceCompatibility>();
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
        mLastWidth = Screen.width;
        mLastHeight = Screen.height;
        mLastSafeArea = Screen.safeArea;
        ApplyUniversalDeviceSettings();
    }

    public static bool IsTVDevice
    {
        get
        {
            if (sIsTvEvaluated) return sCachedIsTv;
            sIsTvEvaluated = true;

            if (SystemInfo.deviceType == DeviceType.Console)
            {
                sCachedIsTv = true;
                return true;
            }

            if (Application.platform == RuntimePlatform.Android)
            {
                try
                {
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
                    {
                        if (activity != null && bridge.CallStatic<bool>("isAndroidTvDevice", activity))
                        {
                            sCachedIsTv = true;
                            return true;
                        }
                    }
                }
                catch { }

                string gpu = (SystemInfo.graphicsDeviceName != null) ? SystemInfo.graphicsDeviceName.ToLower() : "";
                string model = (SystemInfo.deviceModel != null) ? SystemInfo.deviceModel.ToLower() : "";
                if (gpu.Contains("mali-g31") || gpu.Contains("mali-4") ||
                    model.Contains("android tv") || model.Contains("mibox") || model.Contains("mi box") ||
                    model.Contains("chromecast") || model.Contains("shield android tv") ||
                    model.Contains("firetv") || model.Contains("tvbox") || model.Contains("tv box"))
                {
                    sCachedIsTv = true;
                    return true;
                }
            }

            sCachedIsTv = false;
            return false;
        }
    }

    public static bool IsChromebookOrDesktopAndroid
    {
        get
        {
            if (Application.platform != RuntimePlatform.Android) return false;
            if (sIsDesktopEvaluated) return sCachedIsDesktop;
            sIsDesktopEvaluated = true;
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
                {
                    sCachedIsDesktop = activity != null && bridge.CallStatic<bool>("isChromebookOrDesktop", activity);
                }
            }
            catch
            {
                sCachedIsDesktop = false;
            }
            return sCachedIsDesktop;
        }
    }

    public static void ApplyNativeAndroidWindowMode()
    {
        if (Application.platform != RuntimePlatform.Android) return;
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
            {
                if (activity != null)
                {
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(delegate
                    {
                        try
                        {
                            using (AndroidJavaClass innerBridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
                            {
                                innerBridge.CallStatic("applyWindowCompatibility", activity);
                            }
                        }
                        catch { }
                    }));
                }
            }
        }
        catch { }
    }

    public static string GetAndroidLanIPv4()
    {
        if (Application.platform != RuntimePlatform.Android) return string.Empty;
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
            {
                if (activity != null)
                {
                    string ip = bridge.CallStatic<string>("getLanIpv4Address", activity);
                    return ip ?? string.Empty;
                }
            }
        }
        catch { }
        return string.Empty;
    }

    public static string GetDiagnosticSummary()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            return string.Format("{0} | {1}x{2} @ {3}Hz", Application.platform, Screen.width, Screen.height, Screen.currentResolution.refreshRate);
        }

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
            {
                return bridge.CallStatic<string>("getDeviceDiagnosticSummary", activity);
            }
        }
        catch
        {
            return string.Format("{0} | {1}x{2}", SystemInfo.operatingSystem, Screen.width, Screen.height);
        }
    }

    public static void RefreshNativeSafeInsets(bool force = false)
    {
        if (Application.platform != RuntimePlatform.Android) return;
        if (!force && Time.unscaledTime < sNextInsetPollTime) return;
        sNextInsetPollTime = Time.unscaledTime + 0.5f;

        if (AndroidApiLevel < 28) return;

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass bridge = new AndroidJavaClass("com.svzrebalanced.compat.SVZAndroidBridge"))
            {
                if (activity != null)
                {
                    int[] arr = bridge.CallStatic<int[]>("getSafeInsetsPx", activity);
                    if (arr != null && arr.Length == 4)
                    {
                        sCachedNativeInsets[0] = Mathf.Max(0, arr[0]);
                        sCachedNativeInsets[1] = Mathf.Max(0, arr[1]);
                        sCachedNativeInsets[2] = Mathf.Max(0, arr[2]);
                        sCachedNativeInsets[3] = Mathf.Max(0, arr[3]);
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Returns safe padding insets in screen pixels: (left, top, right, bottom).
    /// Combines Unity Screen.safeArea with native Android DisplayCutout / WindowInsets (API 28-36).
    /// </summary>
    public static void GetSafeInsetsPx(out float left, out float top, out float right, out float bottom)
    {
        RefreshNativeSafeInsets(false);

        int sw = Mathf.Max(1, Screen.width);
        int sh = Mathf.Max(1, Screen.height);
        Rect sa = Screen.safeArea;

        float saLeft = Mathf.Clamp(sa.xMin, 0f, sw * 0.25f);
        float saRight = (sa.width > 1f) ? Mathf.Clamp(sw - sa.xMax, 0f, sw * 0.25f) : 0f;
        float saBottom = Mathf.Clamp(sa.yMin, 0f, sh * 0.25f);
        float saTop = (sa.height > 1f) ? Mathf.Clamp(sh - sa.yMax, 0f, sh * 0.25f) : 0f;

        left = Mathf.Clamp(Mathf.Max(saLeft, sCachedNativeInsets[0]), 0f, sw * 0.20f);
        top = Mathf.Clamp(Mathf.Max(saTop, sCachedNativeInsets[1]), 0f, sh * 0.20f);
        right = Mathf.Clamp(Mathf.Max(saRight, sCachedNativeInsets[2]), 0f, sw * 0.20f);
        bottom = Mathf.Clamp(Mathf.Max(saBottom, sCachedNativeInsets[3]), 0f, sh * 0.20f);
    }

    /// <summary>
    /// Computes uniform virtual IMGUI scaling + notch/cutout-safe virtual rectangle in GUI coordinates.
    /// </summary>
    public static void GetSafeVirtualViewport(float refHeight, float minVirtualWidth, out float scale, out float virtualWidth, out float virtualHeight, out Rect safeVirtualRect)
    {
        float sw = Mathf.Max(1, Screen.width);
        float sh = Mathf.Max(1, Screen.height);

        scale = Mathf.Max(0.45f, sh / refHeight);
        virtualWidth = sw / scale;
        if (virtualWidth < minVirtualWidth)
        {
            scale = Mathf.Max(0.40f, sw / minVirtualWidth);
            virtualWidth = sw / scale;
        }
        virtualHeight = sh / scale;

        float l, t, r, b;
        GetSafeInsetsPx(out l, out t, out r, out b);
        float vl = l / scale;
        float vt = t / scale;
        float vr = r / scale;
        float vb = b / scale;

        safeVirtualRect = new Rect(
            vl,
            vt,
            Mathf.Max(200f, virtualWidth - vl - vr),
            Mathf.Max(160f, virtualHeight - vt - vb)
        );
    }

    public static void ApplyUniversalDeviceSettings()
    {
        try
        {
            // 1. Enable Multi-Touch for simultaneous Hero movement + Unit/Spell tapping on phones & tablets
            Input.multiTouchEnabled = true;

            // 2. Prevent screen sleep/dimming during waves and online 1v1 matches
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // 3. Landscape orientation lock with sensor flip support on mobile & tablets
            if (IsMobileOrTablet)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }

            // 4. Respect saved FPS choice if configured; otherwise pick ideal hardware default (60Hz TV vs 120Hz phones)
            if (!PlayerPrefs.HasKey(PREF_FPS_LIMIT))
            {
                if (IsTVDevice || Screen.currentResolution.refreshRate == 60 || Screen.currentResolution.refreshRate == 50)
                {
                    Application.targetFrameRate = 60;
                    QualitySettings.vSyncCount = 1;
                }
                else
                {
                    Application.targetFrameRate = 120;
                    QualitySettings.vSyncCount = 0;
                }
            }

            // 5. Targeted Android Resolution Auto-Scaler:
            // Downscale ONLY on slow Android TV / low-VRAM legacy devices (>1080p -> 1080p) so modern 1440p flagships/tablets keep full native sharpness!
            bool isLowEndGpu = Application.platform == RuntimePlatform.Android &&
                               ((SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize <= 1536) ||
                                (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 256));
            if (Application.platform == RuntimePlatform.Android && !PlayerPrefs.HasKey(PREF_RES_WIDTH) && Screen.height > 1080 && (IsTVDevice || isLowEndGpu))
            {
                int targetH = 1080;
                int targetW = Mathf.RoundToInt(targetH * AspectRatio);
                Screen.SetResolution(targetW, targetH, true);
                Debug.Log(string.Format("[SVZDeviceCompatibility] Auto-scaled buffer to 1080p ({0}x{1}) for TV/low-VRAM GPU.", targetW, targetH));
            }

            // 6. Bluetooth Headphone / Audio Buffer Optimization
            try
            {
                AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
                if (audioConfig.dspBufferSize < 512)
                {
                    audioConfig.dspBufferSize = 512;
                    AudioSettings.Reset(audioConfig);
                }
            }
            catch { }

            // 7. Keep network alive in background during online lobbies
            Application.runInBackground = true;

            // 8. Enforce Android 5-16 native window/cutout compatibility
            ApplyNativeAndroidWindowMode();
            RefreshNativeSafeInsets(true);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SVZDeviceCompatibility] Init warning: " + e.Message);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && Application.platform == RuntimePlatform.Android)
        {
            ApplyNativeAndroidWindowMode();
            RefreshNativeSafeInsets(true);
        }
    }

    private void Update()
    {
        Rect currentSafeArea = Screen.safeArea;
        if (Screen.width != mLastWidth || Screen.height != mLastHeight || currentSafeArea != mLastSafeArea)
        {
            mLastWidth = Screen.width;
            mLastHeight = Screen.height;
            mLastSafeArea = currentSafeArea;
            RefreshNativeSafeInsets(true);
            if (WeakGlobalInstance<SUIScreen>.instance != null && WeakGlobalInstance<SUIScreen>.instance.autoScaler != null)
            {
                WeakGlobalInstance<SUIScreen>.instance.autoScaler.RefreshIfResolutionChanged();
            }
        }
    }
}
