using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildSubmission
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DefaultOutput = "Build/Windows/LastLine.exe";

    public static void BuildWindows()
    {
        string output = GetOutputPath();
        if (!File.Exists(ScenePath))
            throw new BuildFailedException($"Required scene not found: {ScenePath}");

        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Build/Windows");
        PlayerSettings.companyName = "Zhang Xuhan";
        PlayerSettings.productName = "Last Line";
        PlayerSettings.defaultScreenWidth = 600;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"Windows build failed: {report.summary.result} ({report.summary.totalErrors} errors)");

        Debug.Log($"Windows build succeeded: {Path.GetFullPath(output)} ({report.summary.totalSize} bytes)");
    }

    private static string GetOutputPath()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index + 1 < args.Length; index++)
            if (string.Equals(args[index], "-submissionOutput", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(args[index + 1]);
        return Path.GetFullPath(DefaultOutput);
    }
}
