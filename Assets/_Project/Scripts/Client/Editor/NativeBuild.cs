using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Client.Editor
{
    // Builds the game to run on its own, off the web (Docs/NativeBuild.md): Windows (x64) into Builds/Windows and
    // Android (an APK) into Builds/Android. The menu's Game > Build Windows / Build Android, or from a command line
    // RunBatchWindows and RunBatchAndroid through -executeMethod, with -customBuildPath for another folder or file.
    // The player settings each needs are set here as the build starts, not kept by hand in ProjectSettings, so the
    // project stays as the setup script leaves it until someone builds.
    //
    // Windows uses Mono: the Windows editor brings what it needs (the "Windows Build Support (Mono)" module comes with
    // it), while IL2CPP would also want the "Windows Build Support (IL2CPP)" module and Visual Studio's C++ tools.
    // Android uses IL2CPP for ARM64, which Google Play requires (Mono builds only the old 32-bit ARM); Unity Hub's
    // "Android Build Support" module brings the SDK, NDK and JDK it needs.
    public static class NativeBuild
    {
        public const string WindowsPath = "Builds/Windows/pray-to.exe";
        public const string AndroidPath = "Builds/Android/pray-to.apk";
        // The app's name on Android (Google Play's id, if it ever goes there; change it before the first upload).
        public const string AndroidId = "com.prayto.game";
        // Android 8.0: every phone of the last several years, and what Unity 6 supports well.
        const AndroidSdkVersions MinAndroid = AndroidSdkVersions.AndroidApiLevel26;

        [MenuItem("Game/Build Windows")]
        public static void BuildWindowsFromMenu() => FromMenu(BuildWindows(WindowsPath));

        [MenuItem("Game/Build Android")]
        public static void BuildAndroidFromMenu() => FromMenu(BuildAndroid(AndroidPath));

        // Entry points for batch mode. Set the project up first, as the web build does, so a build never runs on a
        // stale scene or sheet list. Exit 0 when built, 1 when the build failed, 2 when the editor lacks the platform's
        // module (Unity Hub > Installs > the editor > Add modules).
        public static void RunBatchWindows() => RunBatch(BuildTarget.StandaloneWindows64, () => BuildWindows(Argument("-customBuildPath") ?? WindowsPath));

        public static void RunBatchAndroid() => RunBatch(BuildTarget.Android, () => BuildAndroid(Argument("-customBuildPath") ?? AndroidPath));

        static void RunBatch(BuildTarget target, Func<BuildReport> build)
        {
            try
            {
                if (!Supported(target))
                {
                    Debug.LogError($"Native build: this editor cannot build for {target}. Add its module in Unity Hub (Docs/NativeBuild.md).");
                    EditorApplication.Exit(2);
                    return;
                }
                ProjectSetup.SetUp();
                var report = build();
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void FromMenu(BuildReport report)
        {
            if (report != null && report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        static bool Supported(BuildTarget target) => BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target);

        // A window on the desktop (full screen in a window, its size changeable), Mono, x64.
        public static BuildReport BuildWindows(string path)
        {
            if (!Supported(BuildTarget.StandaloneWindows64))
            {
                Debug.LogError("Native build: add \"Windows Build Support\" in Unity Hub to build for Windows.");
                return null;
            }
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            return Build(path, BuildTarget.StandaloneWindows64, BuildOptions.None);
        }

        // An APK to put on a phone (adb install, or copied over): IL2CPP for ARM64, upright or sideways as the phone is
        // held, drawn under a notch or camera hole too (the screens keep to the safe area themselves, Ui.Inset). Signed
        // with the editor's debug key: enough for one's own phones, not for a store.
        public static BuildReport BuildAndroid(string path)
        {
            if (!Supported(BuildTarget.Android))
            {
                Debug.LogError("Native build: add \"Android Build Support\" (with the SDK, NDK and OpenJDK) in Unity Hub to build for Android.");
                return null;
            }
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidId);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = MinAndroid;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            // Online play (the relay and the cloud saves) needs the network.
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
            return Build(path, BuildTarget.Android, BuildOptions.None);
        }

        static BuildReport Build(string path, BuildTarget target, BuildOptions options)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes in the build list. Run Game > Set Up Project first.");
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = options,
            });
            var summary = report.summary;
            Debug.Log($"Native build ({target}) {summary.result} at {path}: {summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalErrors} errors, took {summary.totalTime}");
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
