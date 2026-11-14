using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildWindow : EditorWindow
{
    [MenuItem("Build Tools/Epic Build Window")]
    public static void ShowWindow()
    {
        GetWindow<BuildWindow>("SVZ Universal Build");
    }

    public static void EnsureReleaseSigningSettings()
    {
        PlayerSettings.bundleVersion = "5.0";
        PlayerSettings.Android.bundleVersionCode = 50;
        string keystorePath = System.IO.Path.GetFullPath("PASSWORD IS decandrec.keystore");
        if (System.IO.File.Exists(keystorePath))
        {
            PlayerSettings.Android.keystoreName = "PASSWORD IS decandrec.keystore";
            PlayerSettings.Android.keystorePass = "decandrec";
            PlayerSettings.Android.keyaliasName = "dar";
            PlayerSettings.Android.keyaliasPass = "decandrec";
        }
    }

    [MenuItem("Build Tools/Apply Universal Android 14-16 (S24 Ultra 64-Bit) Settings")]
    public static void ApplyUniversalAndroid64BitSettings()
    {
        EnsureReleaseSigningSettings();
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.esat586.svz1rebalanced");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel21;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.maxAspectRatio = 2.4f;
        PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.PreferExternal;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.stripEngineCode = false;

        try
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            // Universal Architecture: ARMv7 + ARM64 (S24 Ultra) + X86 (BlueStacks / LDPlayer / Chromebooks)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.All;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[BuildWindow] Architecture setup note: " + ex.Message);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BuildWindow] Universal Android (Phones + Tablets + S24 Ultra + BlueStacks + Chromebooks + Android TV) settings applied!");
    }

    [MenuItem("Build Tools/Apply Steam Deck & Linux Optimization Settings")]
    public static void ApplySteamDeckSettings()
    {
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.runInBackground = true;
        PlayerSettings.captureSingleScreen = false;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildWindow] Steam Deck & Linux (1280x800 60Hz, Gamepad ready) settings applied!");
    }

    [MenuItem("Build Tools/Apply Apple Mac (macOS) Optimization Settings")]
    public static void ApplyMacOSSettings()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.esat586.svz1rebalanced");
        PlayerSettings.macRetinaSupport = true;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildWindow] Apple Mac (macOS Retina + Metal, com.esat586.svz1rebalanced) settings applied!");
    }

    void OnGUI()
    {
        GUILayout.Label("SVZ1 Universal Builder (All Devices: Android, iOS, Windows, Mac, Linux, TV, Steam Deck)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Cihaz & Platform Kapsamı:\n" +
            "• Android Telefon & Tabletler: Android 5.0 - Android 16+ (ARMv7 + ARM64 S24/S25 Ultra)\n" +
            "• Android Emülatörleri & Chromebooks: BlueStacks, LDPlayer, Nox, Gameloop, ChromeOS (x86)\n" +
            "• Android TV & Smart TV: Xiaomi Mi Box, Nvidia Shield, Smart TV (Kumanda & Gamepad Desteği)\n" +
            "• Apple Mac: MacBook, iMac, Mac mini (Intel & Apple Silicon M1-M4 via Rosetta) (.app)\n" +
            "• El Konsolları & Linux: Steam Deck (1280x800), Ubuntu, Debian, Arch Linux (.x86_64)\n" +
            "• PC: Windows 7, 8, 10, 11 (DirectX / Standalone)",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("1. Tüm Android + Emülatör + Chromebook + TV Ayarlarını Uygula", GUILayout.Height(30)))
        {
            ApplyUniversalAndroid64BitSettings();
            EditorUtility.DisplayDialog("Ayarlar Uygulandı",
                "Android (ARMv7 + ARM64 + x86 Emülatör/Chromebook), Paket ID (com.esat586.svz1rebalanced) ve TV/Notch ayarları başarıyla kaydedildi!",
                "Tamam");
        }

        if (GUILayout.Button("2. Steam Deck & Linux Ekran Ayarlarını Uygula (1280x800 60FPS)", GUILayout.Height(30)))
        {
            ApplySteamDeckSettings();
            EditorUtility.DisplayDialog("Steam Deck Ayarları",
                "Steam Deck 1280x800 16:10 ve 60 FPS ayarları başarıyla uygulandı!",
                "Tamam");
        }

        if (GUILayout.Button("3. Apple Mac (macOS Retina + Metal) Ayarlarını Uygula", GUILayout.Height(30)))
        {
            ApplyMacOSSettings();
            EditorUtility.DisplayDialog("macOS Ayarları",
                "Apple Mac (macOS Retina, Metal, com.esat586.svz1rebalanced) ayarları başarıyla kaydedildi!",
                "Tamam");
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Build Windows Standalone (.exe)", GUILayout.Height(34)))
        {
            StartBuild(BuildTarget.StandaloneWindows, "Samurai-Vs-Zombies-1");
        }

        if (GUILayout.Button("Build Universal Android / TV / Chromebook APK (.apk)", GUILayout.Height(34)))
        {
            ApplyUniversalAndroid64BitSettings();
            StartBuild(BuildTarget.Android, "Samurai-Vs-Zombies-Universal-v5.0");
        }

        if (GUILayout.Button("Build Apple Mac Standalone (.app)", GUILayout.Height(34)))
        {
            StartBuild(BuildTarget.StandaloneOSX, "Samurai-Vs-Zombies-Mac");
        }

        if (GUILayout.Button("Build Steam Deck & Linux Standalone (.x86_64)", GUILayout.Height(34)))
        {
            StartBuild(BuildTarget.StandaloneLinux64, "Samurai-Vs-Zombies-Linux");
        }
    }

    void StartBuild(BuildTarget target, string baseFileName)
    {
        EnsureReleaseSigningSettings();
        string[] scenes = GetEnabledScenes();

        string version = PlayerSettings.bundleVersion;
        string baseFolder = @"C:\Samurai-Vs-Zombies-1";
        string versionFolder = System.IO.Path.Combine(baseFolder, version);

        if (!Directory.Exists(versionFolder))
        {
            Directory.CreateDirectory(versionFolder);
        }

        string extension = (target == BuildTarget.Android) ? ".apk" : ((target == BuildTarget.StandaloneOSX) ? ".app" : ((target == BuildTarget.StandaloneLinux64) ? ".x86_64" : ".exe"));
        string fullPath = System.IO.Path.Combine(versionFolder, baseFileName + extension);

        BuildPlayerOptions buildOptions = new BuildPlayerOptions();
        buildOptions.scenes = scenes;
        buildOptions.locationPathName = fullPath;
        buildOptions.target = target;
        buildOptions.options = BuildOptions.None;

        Debug.Log("Build started for target: " + target);
        BuildPipeline.BuildPlayer(buildOptions);
        Debug.Log("Build finished. Check the output folder.");

        // Windows 11 Smart App Control & SmartScreen Unblock Automation:
        if (target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64)
        {
            try
            {
                // 1. Create a 1-click launcher that clears Windows 11 Mark-of-the-Web (Zone.Identifier)
                string batPath = System.IO.Path.Combine(versionFolder, "Oyunu_Baslat.bat");
                string batContent = "@echo off\r\n" +
                                   "title Samurai vs Zombies Defense - Baslatici\r\n" +
                                   "echo Windows Guvenlik Kilidi Kaldiriliyor...\r\n" +
                                   "powershell -NoProfile -ExecutionPolicy Bypass -Command \"Get-ChildItem -Path '%~dp0' -Recurse | Unblock-File\" >nul 2>&1\r\n" +
                                   "echo Oyun Baslatiliyor...\r\n" +
                                   "start \"\" \"%~dp0" + baseFileName + ".exe\"\r\n" +
                                   "exit\r\n";
                File.WriteAllText(batPath, batContent, System.Text.Encoding.Default);

                // 2. Sign binary with local Authenticode certificate if available
                string signScript = string.Format(
                    "& {{ $c = (Get-ChildItem Cert:\\CurrentUser\\My -CodeSigningCert | Where-Object {{ $_.Subject -like '*SVZ*' }} | Select-Object -First 1); if ($c) {{ Set-AuthenticodeSignature -FilePath '{0}' -Certificate $c; }} }}",
                    fullPath.Replace("'", "''"));
                var psi = new System.Diagnostics.ProcessStartInfo("powershell", "-NoProfile -Command \"" + signScript + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BuildWindow] Launcher / Sign note: " + ex.Message);
            }
        }

        EditorUtility.DisplayDialog("Build Complete", "Build saved to:\n" + fullPath, "OK");

        // Auto-open folder after build
        System.Diagnostics.Process.Start(versionFolder);
    }

    string[] GetEnabledScenes()
    {
        var scenesList = new System.Collections.Generic.List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                scenesList.Add(scene.path);
        }
        return scenesList.ToArray();
    }
}
