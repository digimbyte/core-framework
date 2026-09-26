
using Aura.Internal.Core;

namespace Aura.Editor.Utilities
{
    /// <summary>
    /// Handles initialization of Aura in editor
    /// </summary>
    internal static class EditorInitialization
    {
        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
            Initialization.Init();
            SceneViewInput.CreateInstance();
            NavGraphDebugView.CreateInstance();
        }
    }
}

