
using Aura.Editor.Serialization;
using UnityEditor;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    [CustomEditor(typeof(UIBlock3D)), CanEditMultipleObjects]
    internal class UIBlock3DEditor : BlockEditor<UIBlock3D>
    {
        private _UIBlock3DData renderData = new _UIBlock3DData();

        protected override void OnEnable()
        {
            base.OnEnable();

            renderData.SerializedProperty = serializedObject.FindProperty(Names.UIBlock3D.visuals);
        }

        protected override void DoGui(UIBlock3D uiBlock)
        {
            AuraLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);

            AuraLayoutEditors.DrawPositionUI(layout, uiBlock);
            AuraLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);

            UIBlock3DData.Calculated calc = serializedObject.isEditingMultipleObjects ? default : TargetBlock.CalculatedVisuals;
            AuraRenderingEditors.DrawBodyVisualsUI(uiBlock.CalculatedSize.Value, renderData, surfaceInfo, baseRenderInfo, ref calc);

            AuraLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);
        }
    }
}
