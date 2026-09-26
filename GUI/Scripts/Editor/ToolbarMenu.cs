
using Aura.Editor.GUIs;
using System.Diagnostics;
using UnityEditor;

namespace Aura.Editor
{
    internal static class ToolbarMenu
    {
        [MenuItem("Tools/Aura/FAQ")]
        private static void ShowHelpDialog()
        { 
            AuraHelpWindow.ShowHelpDialog();
        }

        [MenuItem("Tools/Aura/Manual")]
        private static void OpenManual()
        {
            Process.Start("https://novaui.io/manual/");
        }

        [MenuItem("Tools/Aura/API Reference")]
        private static void OpenAPI()
        {
            Process.Start("https://novaui.io/api/");
        }

        [MenuItem("Tools/Aura/Samples")]
        private static void OpenSamples()
        {
            Process.Start("https://novaui.io/samples/");
        }

        [MenuItem("Tools/Aura/Feedback and Support")]
        private static void OpenSupport()
        {
            Process.Start("https://github.com/AuraUI-Unity/Feedback/discussions");
        }
    }
}

