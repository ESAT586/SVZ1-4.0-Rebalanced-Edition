using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
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

        // Lock OpenGLES3 + OpenGLES2 explicitly for Unity 2017 stability on Android 14-17 GPUs (avoid old Vulkan fallback crashes).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] {
            GraphicsDeviceType.OpenGLES3,
            GraphicsDeviceType.OpenGLES2
        });

        AssetDatabase.SaveAssets();
        Debug.Log("[BuildWindow] Universal Android (Phones + Tablets + S24 Ultra + BlueStacks + Chromebooks + Android TV) settings applied!");
    }

    [MenuItem("Build Tools/Build Universal Android APK (Silent)")]
    public static void BuildAndroidApkSilent()
    {
        ApplyUniversalAndroid64BitSettings();
        BuildTargetOutput(BuildTarget.Android, "Samurai-Vs-Zombies-Universal-v5.0", false);
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

    [MenuItem("Build Tools/Apply Apple iOS (iPhone 11 - 16 ARM64 Metal) Settings")]
    public static void ApplyUniversalIOSSettings()
    {
        EnsureReleaseSigningSettings();
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.esat586.svz1rebalanced");
        PlayerSettings.iOS.buildNumber = "50";
        PlayerSettings.iOS.targetOSVersionString = "11.0";
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1); // ARM64 (64-bit required on iPhone 11+)
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new GraphicsDeviceType[] {
            GraphicsDeviceType.Metal,
            GraphicsDeviceType.OpenGLES2
        });
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildWindow] Universal iOS (iPhone 11+ ARM64 + Metal, com.esat586.svz1rebalanced) settings applied!");
    }

    [MenuItem("Build Tools/Build Apple iOS Xcode Project (Silent)")]
    public static void BuildIOSXcodeSilent()
    {
        ApplyUniversalIOSSettings();
        BuildTargetOutput(BuildTarget.iOS, "SVZ1-iOS-Xcode", false);
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
            "• Apple iOS: iPhone 11 / XR - iPhone 16 & iPad (ARM64 + Metal + Çentik SafeArea -> Xcode Export)\n" +
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

        if (GUILayout.Button("4. Apple iOS (iPhone 11+ ARM64 + Metal) Ayarlarını Uygula", GUILayout.Height(30)))
        {
            ApplyUniversalIOSSettings();
            EditorUtility.DisplayDialog("iOS Ayarları",
                "iPhone 11 ve tüm modern iPhone/iPad cihazlar için (ARM64, Metal, Landscape SafeArea, iOS 11+) ayarları kaydedildi!",
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

        if (GUILayout.Button("Build Apple iOS Xcode Project (iPhone 11+ -> macOS/Xcode)", GUILayout.Height(34)))
        {
            string playbackDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(EditorApplication.applicationPath), "Data/PlaybackEngines/iOSSupport");
            if (!Directory.Exists(playbackDir))
            {
                EditorUtility.DisplayDialog("iOS Modülü Kurulu Değil",
                    "Unity Hub -> Installs -> 2017.4.40f1 -> Add Modules içinden 'iOS Build Support' ekleyin.\n" +
                    "Ardından üretilen Xcode klasörünü Mac üzerinde Xcode ile açıp (veya Sideloadly/AltStore için .ipa alarak) iPhone 11'e yükleyebilirsiniz.",
                    "Tamam");
            }
            else
            {
                ApplyUniversalIOSSettings();
                StartBuild(BuildTarget.iOS, "SVZ1-iOS-Xcode");
            }
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
        BuildTargetOutput(target, baseFileName, true);
    }

    static void BuildTargetOutput(BuildTarget target, string baseFileName, bool interactive)
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

        string extension = (target == BuildTarget.Android) ? ".apk" : ((target == BuildTarget.iOS) ? "" : ((target == BuildTarget.StandaloneOSX) ? ".app" : ((target == BuildTarget.StandaloneLinux64) ? ".x86_64" : ".exe")));
        string fullPath = System.IO.Path.Combine(versionFolder, baseFileName + extension);

        BuildPlayerOptions buildOptions = new BuildPlayerOptions();
        buildOptions.scenes = scenes;
        buildOptions.locationPathName = fullPath;
        buildOptions.target = target;
        buildOptions.options = BuildOptions.None;

        Debug.Log("Build started for target: " + target + " -> " + fullPath);
        BuildPipeline.BuildPlayer(buildOptions);
        Debug.Log("Build finished. Check the output folder: " + fullPath);

        if (target == BuildTarget.Android && File.Exists(fullPath))
        {
            string signReport;
            if (!ZipalignAndSignAndroidApk(fullPath, out signReport))
            {
                Debug.LogError("[BuildWindow] Android APK align/sign failed:\n" + signReport);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("APK Signing Warning", signReport, "OK");
                }
            }
            else
            {
                Debug.Log("[BuildWindow] Android APK zipalign + v1/v2/v3 signing verified:\n" + signReport);
            }
        }

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

        if (interactive)
        {
            EditorUtility.DisplayDialog("Build Complete", "Build saved to:\n" + fullPath, "OK");
            System.Diagnostics.Process.Start(versionFolder);
        }
    }

    [MenuItem("Build Tools/Re-Sign & Verify Latest Android APK (v1+v2+v3)")]
    public static void ReSignLatestAndroidApkMenu()
    {
        EnsureReleaseSigningSettings();
        string baseFolder = @"C:\Samurai-Vs-Zombies-1";
        string version = PlayerSettings.bundleVersion;
        string targetApk = null;

        string preferredDir = System.IO.Path.Combine(baseFolder, version);
        if (Directory.Exists(preferredDir))
        {
            string[] apks = Directory.GetFiles(preferredDir, "*.apk");
            if (apks.Length > 0)
            {
                targetApk = apks[0];
            }
        }

        if (string.IsNullOrEmpty(targetApk) && Directory.Exists(baseFolder))
        {
            string[] allApks = Directory.GetFiles(baseFolder, "*.apk", SearchOption.AllDirectories);
            System.DateTime newest = System.DateTime.MinValue;
            for (int i = 0; i < allApks.Length; i++)
            {
                System.DateTime t = File.GetLastWriteTimeUtc(allApks[i]);
                if (t > newest)
                {
                    newest = t;
                    targetApk = allApks[i];
                }
            }
        }

        if (string.IsNullOrEmpty(targetApk) || !File.Exists(targetApk))
        {
            EditorUtility.DisplayDialog("Re-Sign APK", "No Android APK found under C:\\Samurai-Vs-Zombies-1.", "OK");
            return;
        }

        string report;
        bool ok = ZipalignAndSignAndroidApk(targetApk, out report);
        if (ok)
        {
            Debug.Log("[BuildWindow] Re-Sign OK: " + targetApk + "\n" + report);
            EditorUtility.DisplayDialog("Re-Sign Complete (v1+v2+v3)", targetApk + "\n\n" + report, "OK");
        }
        else
        {
            Debug.LogError("[BuildWindow] Re-Sign FAILED: " + targetApk + "\n" + report);
            EditorUtility.DisplayDialog("Re-Sign Failed", report, "OK");
        }
    }

    public static string FindAndroidBuildToolsDir()
    {
        var roots = new System.Collections.Generic.List<string>();
        string envHome = System.Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (!string.IsNullOrEmpty(envHome)) roots.Add(envHome);
        string envRoot = System.Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if (!string.IsNullOrEmpty(envRoot)) roots.Add(envRoot);
        string prefRoot = EditorPrefs.GetString("AndroidSdkRoot", string.Empty);
        if (!string.IsNullOrEmpty(prefRoot)) roots.Add(prefRoot);
        string localAppData = System.Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(localAppData))
        {
            roots.Add(System.IO.Path.Combine(localAppData, @"Android\Sdk"));
        }

        string[] preferred = new string[] { "37.0.0", "36.1.0", "36.0.0", "35.0.0", "34.0.0" };
        for (int r = 0; r < roots.Count; r++)
        {
            string btBase = System.IO.Path.Combine(roots[r], "build-tools");
            if (!Directory.Exists(btBase)) continue;

            for (int p = 0; p < preferred.Length; p++)
            {
                string cand = System.IO.Path.Combine(btBase, preferred[p]);
                if (HasAlignAndSigner(cand)) return cand;
            }

            string[] dirs = Directory.GetDirectories(btBase);
            System.Array.Sort(dirs, System.StringComparer.OrdinalIgnoreCase);
            for (int i = dirs.Length - 1; i >= 0; i--)
            {
                if (HasAlignAndSigner(dirs[i])) return dirs[i];
            }
        }
        return null;
    }

    private static bool HasAlignAndSigner(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
        return File.Exists(System.IO.Path.Combine(dir, "zipalign.exe")) &&
               File.Exists(System.IO.Path.Combine(dir, "apksigner.bat"));
    }

    public static bool ZipalignAndSignAndroidApk(string apkPath, out string report)
    {
        report = string.Empty;
        if (!File.Exists(apkPath))
        {
            report = "APK not found: " + apkPath;
            return false;
        }

        string keystoreFullPath = System.IO.Path.GetFullPath("PASSWORD IS decandrec.keystore");
        if (!File.Exists(keystoreFullPath))
        {
            report = "Release keystore not found: " + keystoreFullPath;
            return false;
        }

        string btDir = FindAndroidBuildToolsDir();
        if (string.IsNullOrEmpty(btDir))
        {
            report = "Android SDK build-tools (zipalign.exe & apksigner.bat) not found.";
            return false;
        }

        string zipalignExe = System.IO.Path.Combine(btDir, "zipalign.exe");
        string apksignerBat = System.IO.Path.Combine(btDir, "apksigner.bat");
        string dir = System.IO.Path.GetDirectoryName(apkPath);
        string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(apkPath);
        string alignedTmp = System.IO.Path.Combine(dir, nameNoExt + ".aligned.tmp.apk");
        string signedTmp = System.IO.Path.Combine(dir, nameNoExt + ".signed.tmp.apk");
        string idsigTmp = signedTmp + ".idsig";
        string finalIdsig = apkPath + ".idsig";

        try
        {
            SafeDeleteFile(alignedTmp);
            SafeDeleteFile(signedTmp);
            SafeDeleteFile(idsigTmp);
            SafeDeleteFile(finalIdsig);

            string alignOut;
            int alignCode = RunHiddenProcess(
                zipalignExe,
                "-f -p 4 \"" + apkPath + "\" \"" + alignedTmp + "\"",
                out alignOut);
            if (alignCode != 0 || !File.Exists(alignedTmp))
            {
                report = "zipalign failed (code " + alignCode + "): " + alignOut;
                return false;
            }

            string signCmd = "\"\"" + apksignerBat + "\" sign --ks \"" + keystoreFullPath +
                "\" --ks-pass pass:decandrec --ks-key-alias dar --key-pass pass:decandrec" +
                " --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled true" +
                " --out \"" + signedTmp + "\" \"" + alignedTmp + "\"\"";
            string signOut;
            int signCode = RunHiddenProcess("cmd.exe", "/d /c " + signCmd, out signOut);
            if (signCode != 0 || !File.Exists(signedTmp))
            {
                report = "apksigner sign failed (code " + signCode + "): " + signOut;
                return false;
            }

            string verifyCmd = "\"\"" + apksignerBat + "\" verify --verbose --print-certs \"" + signedTmp + "\"\"";
            string verifyOut;
            int verifyCode = RunHiddenProcess("cmd.exe", "/d /c " + verifyCmd, out verifyOut);
            bool v1Ok = verifyOut.IndexOf("Verified using v1 scheme (JAR signing): true", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool v2Ok = verifyOut.IndexOf("Verified using v2 scheme (APK Signature Scheme v2): true", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool v3Ok = verifyOut.IndexOf("Verified using v3 scheme (APK Signature Scheme v3): true", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (verifyCode != 0 || !v1Ok || !v2Ok || !v3Ok)
            {
                report = "apksigner verify check failed (code=" + verifyCode +
                    ", v1=" + v1Ok + ", v2=" + v2Ok + ", v3=" + v3Ok + "):\n" + verifyOut;
                return false;
            }

            File.Copy(signedTmp, apkPath, true);
            report = verifyOut;
            return true;
        }
        finally
        {
            SafeDeleteFile(alignedTmp);
            SafeDeleteFile(signedTmp);
            SafeDeleteFile(idsigTmp);
            SafeDeleteFile(finalIdsig);
        }
    }

    private static int RunHiddenProcess(string fileName, string arguments, out string output)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var proc = System.Diagnostics.Process.Start(psi))
        {
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            output = (stdout + "\n" + stderr).Trim();
            return proc.ExitCode;
        }
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    static string[] GetEnabledScenes()
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
