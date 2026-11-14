using UnityEngine;

public static class PerformanceSettings
{
	public const string PREF_QUALITY = "SVZ_QualityLevel";
	public const string PREF_SHADOW = "SVZ_ShadowQuality";
	public const string PREF_AA = "SVZ_AntiAliasing";
	public const string PREF_FPS_LIMIT = "SVZ_FPSLimit";
	public const string PREF_FPS_LIMIT_VERSION = "SVZ_FPSLimitV2";
	public const string PREF_VSYNC = "SVZ_VSync";

	private static readonly string[] kQualityNames = { "LOW", "MEDIUM", "HIGH", "ULTRA" };
	private static readonly string[] kShadowNames = { "LOW", "MEDIUM", "HIGH", "OFF" };
	private static readonly string[] kAANames = { "NONE", "FXAA", "SMAA" };
	private static readonly int[] kFPSOptions = { 30, 60, 90, 120, 144, 0 };
	private static readonly string[] kFPSLabels = { "30 FPS", "60 FPS", "90 FPS", "120 FPS", "144 FPS", "UNLIMITED" };

	public static int qualityCount { get { return kQualityNames.Length; } }
	public static int shadowCount { get { return kShadowNames.Length; } }
	public static int aaCount { get { return kAANames.Length; } }
	public static int fpsCount { get { return kFPSOptions.Length; } }

	public static string GetQualityLabel(int index)
	{
		return kQualityNames[Mathf.Clamp(index, 0, kQualityNames.Length - 1)];
	}

	public static string GetShadowLabel(int index)
	{
		return kShadowNames[Mathf.Clamp(index, 0, kShadowNames.Length - 1)];
	}

	public static string GetAALabel(int index)
	{
		return kAANames[Mathf.Clamp(index, 0, kAANames.Length - 1)];
	}

	public static string GetFPSLabel(int index)
	{
		return kFPSLabels[Mathf.Clamp(index, 0, kFPSLabels.Length - 1)];
	}

	public static int GetDefaultFPSLimitIndex()
	{
		return 1;
	}

	public static int GetSavedFPSLimitIndex()
	{
		int fpsIdx = PlayerPrefs.GetInt(PREF_FPS_LIMIT, GetDefaultFPSLimitIndex());
		if (Application.platform == RuntimePlatform.Android && PlayerPrefs.GetInt(PREF_FPS_LIMIT_VERSION, 0) == 0 && fpsIdx == 1)
		{
			fpsIdx = GetDefaultFPSLimitIndex();
		}
		return Mathf.Clamp(fpsIdx, 0, kFPSOptions.Length - 1);
	}

	public static int GetTargetFrameRateForIndex(int fpsIdx)
	{
		fpsIdx = Mathf.Clamp(fpsIdx, 0, kFPSOptions.Length - 1);
		return kFPSOptions[fpsIdx] > 0 ? kFPSOptions[fpsIdx] : -1;
	}

	public static bool WantsHighRefresh(int fpsIdx)
	{
		int target = GetTargetFrameRateForIndex(fpsIdx);
		return target < 0 || target > 60;
	}

	public static void NormalizeFramePrefs(ref int fpsIdx, ref bool vSync)
	{
		fpsIdx = Mathf.Clamp(fpsIdx, 0, kFPSOptions.Length - 1);
		if (WantsHighRefresh(fpsIdx))
		{
			vSync = false;
		}
	}

	public static void ApplySavedSettings()
	{
		int quality = Mathf.Clamp(PlayerPrefs.GetInt(PREF_QUALITY, 2), 0, qualityCount - 1);
		int shadow = Mathf.Clamp(PlayerPrefs.GetInt(PREF_SHADOW, 2), 0, shadowCount - 1);
		int aa = Mathf.Clamp(PlayerPrefs.GetInt(PREF_AA, 0), 0, aaCount - 1);
		int fps = GetSavedFPSLimitIndex();
		bool vSync = PlayerPrefs.GetInt(PREF_VSYNC, 0) == 1;
		NormalizeFramePrefs(ref fps, ref vSync);
		ApplySettings(quality, shadow, aa, fps, vSync);
	}

	public static void ApplySettings(int quality, int shadow, int aa, int fps, bool vSync)
	{
		NormalizeFramePrefs(ref fps, ref vSync);
		ApplyVisualSettings(quality, shadow, aa);
		ApplyFrameRate(fps, vSync);
	}

	public static void ApplyFrameRate(int fps, bool vSync)
	{
		NormalizeFramePrefs(ref fps, ref vSync);
		QualitySettings.vSyncCount = vSync ? 1 : 0;
		Application.targetFrameRate = GetTargetFrameRateForIndex(fps);
	}

	public static void ApplyVisualSettings(int quality, int shadow, int aa)
	{
		quality = Mathf.Clamp(quality, 0, qualityCount - 1);
		shadow = Mathf.Clamp(shadow, 0, shadowCount - 1);
		aa = Mathf.Clamp(aa, 0, aaCount - 1);
		QualitySettings.SetQualityLevel(quality, true);
		ApplyShadowQuality(shadow);
		ApplyAntiAliasing(aa);
		ApplyTexturePreset(quality);
	}

	public static void ApplyPreset(int preset, out int quality, out int shadow, out int aa, out int fps, out bool vSync)
	{
		preset = Mathf.Clamp(preset, 0, qualityCount - 1);
		quality = preset;
		switch (preset)
		{
		case 0:
			shadow = 3;
			aa = 0;
			fps = 0;
			vSync = false;
			break;
		case 1:
			shadow = 0;
			aa = 0;
			fps = 1;
			vSync = false;
			break;
		case 3:
			shadow = 2;
			aa = 2;
			fps = 3;
			vSync = false;
			break;
		default:
			shadow = 2;
			aa = 1;
			fps = 1;
			vSync = false;
			break;
		}
		ApplySettings(quality, shadow, aa, fps, vSync);
	}

	public static void SavePrefs(int quality, int shadow, int aa, int fps, bool vSync)
	{
		NormalizeFramePrefs(ref fps, ref vSync);
		PlayerPrefs.SetInt(PREF_QUALITY, Mathf.Clamp(quality, 0, qualityCount - 1));
		PlayerPrefs.SetInt(PREF_SHADOW, Mathf.Clamp(shadow, 0, shadowCount - 1));
		PlayerPrefs.SetInt(PREF_AA, Mathf.Clamp(aa, 0, aaCount - 1));
		PlayerPrefs.SetInt(PREF_FPS_LIMIT, Mathf.Clamp(fps, 0, fpsCount - 1));
		PlayerPrefs.SetInt(PREF_FPS_LIMIT_VERSION, 1);
		PlayerPrefs.SetInt(PREF_VSYNC, vSync ? 1 : 0);
	}

	public static string GetDebugSummary()
	{
		int fps = GetSavedFPSLimitIndex();
		bool vSync = PlayerPrefs.GetInt(PREF_VSYNC, 0) == 1;
		NormalizeFramePrefs(ref fps, ref vSync);
		string refresh = Screen.currentResolution.refreshRate > 0 ? Screen.currentResolution.refreshRate + " Hz" : "unknown Hz";
		return "Platform: " + Application.platform +
			"\nDebug build: " + Debug.isDebugBuild +
			"\nQuality: " + QualitySettings.GetQualityLevel() + " / " + GetQualityLabel(PlayerPrefs.GetInt(PREF_QUALITY, 2)) +
			"\nFPS limit: " + GetFPSLabel(fps) + " | targetFrameRate: " + Application.targetFrameRate +
			"\nVSync: " + (vSync ? "ON" : "OFF") + " | Unity vSyncCount: " + QualitySettings.vSyncCount +
			"\nScreen: " + Screen.width + "x" + Screen.height + " | Refresh: " + refresh +
			"\nAA: " + QualitySettings.antiAliasing + " | Textures: " + QualitySettings.masterTextureLimit + " | Aniso: " + QualitySettings.anisotropicFiltering;
	}

	private static void ApplyShadowQuality(int level)
	{
		switch (level)
		{
		case 0:
			QualitySettings.shadows = ShadowQuality.HardOnly;
			break;
		case 1:
			QualitySettings.shadows = ShadowQuality.All;
			break;
		case 2:
			QualitySettings.shadows = ShadowQuality.All;
			break;
		default:
			QualitySettings.shadows = ShadowQuality.Disable;
			break;
		}
	}

	private static void ApplyAntiAliasing(int level)
	{
		QualitySettings.antiAliasing = level == 0 ? 0 : level == 1 ? 2 : 8;
	}

	private static void ApplyTexturePreset(int quality)
	{
		if (quality <= 0)
		{
			QualitySettings.masterTextureLimit = 1;
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
		}
		else if (quality == 1)
		{
			QualitySettings.masterTextureLimit = 0;
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
		}
		else
		{
			QualitySettings.masterTextureLimit = 0;
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
		}
	}
}
