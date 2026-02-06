#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Animator
{
    /// <summary>
    /// Odin-backed inspector for <see cref="Animate"/>.
    /// The UI layout is driven by Odin attributes on Animate/TweenEntry and custom drawers.
    /// </summary>
    [CustomEditor(typeof(Animate))]
    public sealed class AnimateEditor : OdinEditor
    {
    }
}
#endif
