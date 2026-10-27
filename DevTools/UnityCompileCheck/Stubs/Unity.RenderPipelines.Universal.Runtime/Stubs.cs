// Stand-ins for the parts of URP 17.6 (com.unity.render-pipelines.universal, Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef).
// Signatures are copied from the package source; nothing here runs.
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : ScriptableObject
    {
    }

    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;

        protected override RenderPipeline CreatePipeline() => null;
    }
}
