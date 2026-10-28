using System;
using UnityEditor;
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

        [MenuItem("Game/Set Up Project")]
        public static void SetUp()
        {
            bool inputChanged = UseNewInputSystem();
            PlayerSettings.productName = ProductName;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            UseWebSettings();
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
        // Set here rather than only when building, so a build leaves the committed settings as they are.
        public static void UseWebSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
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
