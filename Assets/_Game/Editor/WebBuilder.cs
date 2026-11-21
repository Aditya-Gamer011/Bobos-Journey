using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BobosJourney.EditorTools
{
    /// <summary>
    /// One-click browser build for itch.io: Bobo > Build for itch.io (Web).
    /// Produces Builds/WebGL (playable folder) and Builds/BobosJourney-Web.zip (upload this to itch).
    /// </summary>
    public static class WebBuilder
    {
        public const string OutDir = "Builds/WebGL";
        public const string ZipPath = "Builds/BobosJourney-Web.zip";

        [MenuItem("Bobo/Build for itch.io (Web)")]
        public static string Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            PlayerSettings.productName = "Bobo's Journey";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            // itch.io does not send Content-Encoding headers, so let the loader decompress gzip itself
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            // keep the download small: no texture larger than 1024 in the browser build
            EditorUserBuildSettings.overrideMaxTextureSize = 1024;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            EditorUserBuildSettings.overrideMaxTextureSize = 0;

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

            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            ZipFile.CreateFromDirectory(OutDir, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false); // itch wants index.html at the zip root
            long zipSize = new FileInfo(ZipPath).Length;
            return $"BUILD OK in {summary.totalTime.TotalMinutes:0.0} min, folder {summary.totalSize / 1048576} MB, zip {zipSize / 1048576} MB -> {Path.GetFullPath(ZipPath)}";
        }
    }
}
