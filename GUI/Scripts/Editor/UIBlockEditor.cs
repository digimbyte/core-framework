
using Aura.Editor.GUIs;
using UnityEditor;

namespace Aura.Editor.Layouts
{
    [CustomEditor(typeof(UIBlock)), CanEditMultipleObjects]
    internal class UIBlockEditor : BlockEditor<UIBlock>
    {
        protected override void DoGui(UIBlock uiBlock)
        {
            AuraLayoutEditors.DrawAutoLayoutUI(autoLayout, uiBlock);
            AuraLayoutEditors.DrawPositionUI(layout, uiBlock);
            AuraLayoutEditors.DrawSizeUI(layout, uiBlock, previewSizeProperty);
            AuraLayoutEditors.DrawPaddingMarginUI(layout, uiBlock);
        }
    }
}
