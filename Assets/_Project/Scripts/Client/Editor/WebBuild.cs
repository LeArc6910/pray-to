using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Client.Editor
{
    // Builds the web demo: the scenes in the build list, for WebGL, into Builds/Web. The GitHub Actions workflow
    // (web-demo.yml) calls RunBatch through GameCI, which passes the output folder as -customBuildPath.
    // Files are gzipped with the decompression fallback on, because static hosts such as GitHub Pages do not send
    // the Content-Encoding header Unity's loader would otherwise need.
    public static class WebBuild
    {
        const string DefaultPath = "Builds/Web";

        [MenuItem("Game/Build Web Demo")]
        public static void BuildFromMenu()
        {
            var report = Build(DefaultPath);
            if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(DefaultPath);
        }

        // Entry point for batch mode. Sets the project up first so a build never runs on a stale scene or sheet list.
        public static void RunBatch()
        {
            try
            {
                ProjectSetup.SetUp();
                var report = Build(Argument("-customBuildPath") ?? DefaultPath);
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static BuildReport Build(string path)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes in the build list. Run Game > Set Up Project first.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"Web build {summary.result} at {path}: {summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalErrors} errors, took {summary.totalTime}");
            return report;
        }

        // The value after a command line flag, or null.
        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
