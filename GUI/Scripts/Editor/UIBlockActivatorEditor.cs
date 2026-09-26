
using UnityEditor;
using UnityEngine;

namespace Aura.Editor
{
    [CustomEditor(typeof(UIBlockActivator))]
    internal class UIBlockActivatorEditor : UnityEditor.Editor
    {
        [InitializeOnLoadMethod]
        private static void InitializeSceneIconVisibility()
        {
            EditorApplication.delayCall += HideSceneIcon;
            EditorApplication.hierarchyChanged += HideSceneIcon;
        }

        private static void HideSceneIcon()
        {
            foreach (GizmoInfo info in GizmoUtility.GetGizmoInfo())
            {
                if (info.iconEnabled && info.script is MonoScript script &&
                    script.GetClass() == typeof(UIBlockActivator))
                {
                    info.iconEnabled = false;
                    GizmoUtility.ApplyGizmoInfo(info, false);
                }
            }
        }

        public override void OnInspectorGUI() { hideFlags = target.hideFlags | HideFlags.HideInInspector; }
        protected override void OnHeaderGUI() { }
        protected override bool ShouldHideOpenButton() => true;

        private void OnEnable()
        {
            hideFlags = target.hideFlags | HideFlags.HideInInspector;
        }
    }
}
