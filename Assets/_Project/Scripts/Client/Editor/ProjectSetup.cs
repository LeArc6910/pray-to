using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Client.Editor
{
    // The project settings Unity would otherwise have someone click through after the first open: the Input System
    // package as the only input backend, the product name, 2D editor mode, a URP asset with the 2D renderer for
    // every quality level, and the web build settings. Then it builds the scene with SceneSetup. Safe to run again.
    // The local setup script (DevTools/Local/setup.ps1, setup.sh) calls RunBatch through -executeMethod.
    public static class ProjectSetup
    {
        const string ProductName = "pray-to";
        const string SettingsFolder = "Settings";
        const string RendererPath = SceneSetup.ProjectFolder + "/" + SettingsFolder + "/Renderer2D.asset";
        const string PipelinePath = SceneSetup.ProjectFolder + "/" + SettingsFolder + "/URP-2D.asset";
        // Player settings value for "Input System Package (New)". 0 is the old Input Manager, 2 is both.
        const int NewInputSystem = 1;
        // The web page: Assets/WebGLTemplates/Fit.
        const string WebTemplate = "PROJECT:Fit";
        const string FontFolder = SceneSetup.ProjectFolder + "/Resources/Fonts";
        const string PixelFontPath = FontFolder + "/Galmuri11Bold-Game.ttf";
        const string SmoothFontPath = FontFolder + "/NotoSansKR-Game.ttf";
        // Galmuri11's design size in pixels, as Ui.PixelFontSize.
        const int PixelFontSize = 12;

        [MenuItem("Game/Set Up Project")]
        public static void SetUp()
        {
            bool inputChanged = UseNewInputSystem();
            PlayerSettings.productName = ProductName;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            UseWebSettings();
            UsePixelFont();
            UsePipeline(LoadOrCreatePipeline());
            AssetDatabase.SaveAssets();
            SceneSetup.SetUp();
            if (inputChanged && !Application.isBatchMode)
                Debug.LogWarning("Active Input Handling is now the Input System package. Restart the editor before pressing Play.");
            Debug.Log("Project setup done.");
        }

        // Entry point for the setup script. Exits with 1 when anything throws, so the script can tell it failed.
        public static void RunBatch()
        {
            try
            {
                SetUp();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        // Files gzipped with the decompression fallback on, because static hosts such as GitHub Pages do not send the
        // Content-Encoding header Unity's loader would otherwise need; cached in the browser after the first visit.
        // The page is the project's own template (Assets/WebGLTemplates/Fit), which fills the browser window on a PC
        // too. Set here rather than only when building, so a build leaves the committed settings as they are.
        public static void UseWebSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = WebTemplate;
            // Lighter and quicker to start (2026-11-09, Docs/WebDemo.md 로딩): no Unity splash before the menu (Unity 6
            // lets every plan turn it off; its logo was 2.7 MB and it held the menu back about 2.5 seconds), and
            // IL2CPP code made for size rather than speed, which the game does not need.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            StripUnusedPostProcessing();
        }

        // Unity 6's URP puts its ten film grain textures (2.5 MB) and the post-processing shader variants in every
        // build unless told to leave out what no volume uses, and the game has no post-processing. The switch is in
        // the URP global settings asset (URPShaderStrippingSetting); it is set by its serialized name, so nothing
        // hangs on a C# name that changes between URP versions, and nothing happens if it is not there.
        const string GlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";

        static void StripUnusedPostProcessing()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath(GlobalSettingsPath);
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            bool changed = false;
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.Boolean || property.boolValue) continue;
                if (property.name != "m_StripUnusedPostProcessingVariantsAndResources" && property.name != "m_StripUnusedPostProcessingVariants") continue;
                property.boolValue = true;
                changed = true;
            }
            if (!changed) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // The pixel font's import settings (Ui): its 12-pixel design size and no smoothing, so each of its pixels is a
        // whole number of screen pixels, and the smooth font behind it for any character it lacks. Reimports only
        // when something changed.
        static void UsePixelFont()
        {
            if (!(AssetImporter.GetAtPath(PixelFontPath) is TrueTypeFontImporter importer))
            {
                Debug.LogWarning($"No font at {PixelFontPath}; the game falls back to the smooth font.");
                return;
            }
            var smooth = AssetDatabase.LoadAssetAtPath<Font>(SmoothFontPath);
            var fallbacks = smooth != null ? new[] { smooth } : new Font[0];
            string[] names = smooth != null ? new[] { "Noto Sans KR" } : new string[0];
            if (importer.fontSize == PixelFontSize
                && importer.fontRenderingMode == FontRenderingMode.HintedRaster
                && importer.fontTextureCase == FontTextureCase.Dynamic
                && importer.includeFontData
                && importer.fontNames.SequenceEqual(names)
                && importer.fontReferences.SequenceEqual(fallbacks))
                return;
            importer.fontSize = PixelFontSize;
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.includeFontData = true;
            importer.fontNames = names;
            importer.fontReferences = fallbacks;
            importer.SaveAndReimport();
        }

        // Sets Project Settings > Player > Active Input Handling, which has no public API. This is the serialized
        // property the Input System package itself changes. Returns whether it changed.
        static bool UseNewInputSystem()
        {
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var handler = settings.FindProperty("activeInputHandler");
            if (handler == null)
                throw new InvalidOperationException("Player settings have no activeInputHandler. Set Active Input Handling to Input System Package (New) by hand.");
            if (handler.intValue == NewInputSystem) return false;
            handler.intValue = NewInputSystem;
            settings.ApplyModifiedProperties();
            return true;
        }

        // The same pair of assets as Create > Rendering > URP Asset (with 2D Renderer). Renderer2DData fills in its
        // light blend styles and default materials when it is created in the editor.
        static UniversalRenderPipelineAsset LoadOrCreatePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline != null) return pipeline;

            SceneSetup.EnsureFolder(SceneSetup.ProjectFolder, SettingsFolder);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            return pipeline;
        }

        // Project Settings > Graphics > Default Render Pipeline, and the Render Pipeline Asset of every quality level.
        static void UsePipeline(RenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }
    }
}
