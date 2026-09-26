
using Aura.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(Length2))]
    internal class Length2Drawer : AuraPropertyDrawer<_Length2>
    {
        protected override void OnGUI(Rect position, GUIContent label)
        {
            EditorGUI.LabelField(position, label);

            position.ShiftAndResizeLabel();


            position.Split(out Rect xRect, out Rect yRect);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = AuraGUI.SingleCharacterGUIWidth;
            EditorGUI.PropertyField(xRect, wrapper.XProp);
            EditorGUI.PropertyField(yRect, wrapper.YProp);
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}