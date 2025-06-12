using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildWindow : EditorWindow
{
    [MenuItem("Build Tools/Epic Build Window")]
    public static void ShowWindow()
    {
        GetWindow<BuildWindow>("Custom Build");
    }

    void OnGUI()
    {
        GUILayout.Label("Build Your Project. Made By Nick :)", EditorStyles.boldLabel);

        if (GUILayout.Button("Build Windows Standalone"))
        {
            StartBuild(BuildTarget.StandaloneWindows, "Samurai-Vs-Zombies-1");
        }

        if (GUILayout.Button("Build Android APK"))
        {
            StartBuild(BuildTarget.Android, "Samurai-Vs-Zombies-");
        }
    }

    void StartBuild(BuildTarget target, string baseFileName)
    {
        string[] scenes = GetEnabledScenes();

        string version = PlayerSettings.bundleVersion;
        string baseFolder = @"C:\Samurai-Vs-Zombies-1";
        string versionFolder = System.IO.Path.Combine(baseFolder, version);

        if (!Directory.Exists(versionFolder))
        {
            Directory.CreateDirectory(versionFolder);
        }

        string extension = (target == BuildTarget.Android) ? ".apk" : ".exe";
        string fullPath = System.IO.Path.Combine(versionFolder, baseFileName + extension);

        BuildPlayerOptions buildOptions = new BuildPlayerOptions();
        buildOptions.scenes = scenes;
        buildOptions.locationPathName = fullPath;
        buildOptions.target = target;
        buildOptions.options = BuildOptions.None;

        Debug.Log("Build started for target: " + target);
        BuildPipeline.BuildPlayer(buildOptions);
        Debug.Log("Build finished. Check the output folder.");

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
