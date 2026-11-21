using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BobosJourney.EditorTools
{
    /// <summary>
    /// One-click Windows build: Bobo > Build for Windows.
    /// Produces Builds/Windows (BobosJourney.exe + data) and Builds/BobosJourney-Windows.zip (share or upload this).
    /// </summary>
    public static class WindowsBuilder
    {
        public const string OutDir = "Builds/Windows";
        public const string ExeName = "BobosJourney.exe";
        public const string ZipPath = "Builds/BobosJourney-Windows.zip";

        [MenuItem("Bobo/Build for Windows")]
        public static string Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            PlayerSettings.productName = "Bobo's Journey";
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Disabled);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.visibleInBackground = false;
            PlayerSettings.usePlayerLog = true;
            EditorUserBuildSettings.overrideMaxTextureSize = 0; // full-res textures on desktop

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                var errors = report.steps
                    .SelectMany(step => step.messages)
                    .Where(m => m.type == LogType.Error)
                    .Take(5)
                    .Select(m => m.content);
                return $"BUILD {summary.result}: {summary.totalErrors} errors. " + string.Join(" | ", errors);
            }

            // the Burst debug folder only holds crash symbols
            foreach (var d in Directory.GetDirectories(OutDir, "*_DoNotShip")) Directory.Delete(d, true);

            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            ZipFile.CreateFromDirectory(OutDir, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false);
            long folderSize = new DirectoryInfo(OutDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            long zipSize = new FileInfo(ZipPath).Length;
            return $"BUILD OK in {summary.totalTime.TotalMinutes:0.0} min, folder {folderSize / 1048576} MB, zip {zipSize / 1048576} MB -> {Path.GetFullPath(ZipPath)}";
        }
    }
}
