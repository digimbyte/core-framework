
using Aura.Editor.Serialization;
using UnityEditor;
using UnityEngine;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    internal enum ImageSelectionType
    {
        Texture,
        Sprite
    }

    [CustomEditor(typeof(UIBlock2D)), CanEditMultipleObjects]
    internal class UIBlock2DEditor : BlockEditor<UIBlock2D>
    {
        _UIBlock2DData renderData = new _UIBlock2DData();
        private ImageSelectionType imageMode = ImageSelectionType.Texture;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (target == null) return;

            renderData.SerializedProperty = serializedObject.FindProperty(Names.UIBlock2D.visuals);

            if (serializedObject.FindProperty(Names.UIBlock2D.sprite).objectReferenceValue != null)
            {
                imageMode = ImageSelectionType.Sprite;
            }
        }

        protected override void OnPropertiesCopied()
        {
            if (serializedObject.FindProperty(Names.UIBlock2D.sprite).objectReferenceValue != null)
                imageMode = ImageSelectionType.Sprite;
            else if (serializedObject.FindProperty(Names.UIBlock2D.texture).objectReferenceValue != null)
                imageMode = ImageSelectionType.Texture;
        }

        protected override void DoGui(UIBlock2D uiBlock)
        {
            Vector3 size = uiBlock.CalculatedSize.Value;
            float minHalfSize = .5f * Mathf.Min(size.x, size.y);

            UIBlock2DData.Calculated calc = serializedObject.isEditingMultipleObjects ? default : TargetBlock.CalculatedVisuals;

            AuraLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);
            AuraLayoutEditors.DrawPositionUI(layout, uiBlock);
            AuraLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);
            AuraRenderingEditors.DrawBodyVisualsUI(minHalfSize, renderData, surfaceInfo, baseRenderInfo, ref imageMode, ref calc);
            AuraRenderingEditors.DrawBorderUI(renderData.Border, calc.Border);

            AuraRenderingEditors.DrawShadowUI(renderData, calc.Shadow);
            AuraLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);
        }
    }
}

