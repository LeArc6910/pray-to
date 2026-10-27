// Stand-ins for the parts of URP 17.6's 2D renderer, a separate assembly (Runtime/2D/Unity.RenderPipelines.Universal.2D.Runtime.asmdef)
// with the same namespace as the rest of URP.
// Signatures are copied from the package source; nothing here runs.
namespace UnityEngine.Rendering.Universal
{
    public class Renderer2DData : ScriptableRendererData
    {
    }

    public sealed class Light2D : MonoBehaviour
    {
        public enum LightType
        {
            Parametric = 0,
            Freeform = 1,
            Sprite = 2,
            Point = 3,
            Global = 4,
        }

        public LightType lightType { get; set; }
        public float intensity { get; set; }
        public Color color { get; set; }
    }
}
