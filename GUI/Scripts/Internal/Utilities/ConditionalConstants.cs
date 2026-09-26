
using UnityEngine.Rendering;

namespace Aura.Internal.Utilities
{
    internal static class ConditionalConstants
    {
        public const int DefaultOverlayRenderQueue =
#if HIGH_DEF_RENDER_PIPELINE
            3050;
#else
            (int)RenderQueue.Overlay;
#endif
    }
}
