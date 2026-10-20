using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HouseEdge.Editor
{
    // command-line builds, e.g.
    //   Unity -batchmode -quit -projectPath . -executeMethod HouseEdge.Editor.BuildScript.WebGL
    public static class BuildScript
    {
        [MenuItem("House Edge/Build WebGL")]
        public static void WebGL()
        {
            // Brotli plus the decompression fallback runs on any static host, itch.io included
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        [MenuItem("House Edge/Build macOS")]
        public static void MacOS() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/HouseEdge.app");

        private static void Build(BuildTarget target, string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, path, target, BuildOptions.None);
            Debug.Log($"Build {target}: {report.summary.result}, {report.summary.totalSize / 1024} KB, {report.summary.totalErrors} errors");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
