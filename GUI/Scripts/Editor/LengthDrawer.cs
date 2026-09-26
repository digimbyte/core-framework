
using UnityEditor;
using UnityEngine;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(Length))]
    internal class LengthDrawer : AuraPropertyDrawer<_Length>
    {
        protected override void OnGUI(Rect position, GUIContent label)
        {
            GUIContent propertyLabel = EditorGUI.BeginProperty(position, label, wrapper.SerializedProperty);
            Rect floatField = position;
            floatField.width = Mathf.Max(AuraGUI.MinFloatFieldWidth, floatField.width - AuraGUI.ToggleToolbarFieldWidth) - AuraGUI.MinSpaceBetweenFields;

            EditorGUI.BeginChangeCheck();
            float raw = wrapper.Raw;
            float fieldValue = float.IsNaN(raw) ? 0 : wrapper.Type == LengthType.Value ? raw : raw * 100;
            fieldValue = EditorGUI.FloatField(floatField, propertyLabel, fieldValue);
            raw = fieldValue / (wrapper.Type == LengthType.Value ? 1 : 100);
            bool rawValueChanged = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            Rect lengthTypeField = floatField;
            lengthTypeField.width = AuraGUI.ToggleToolbarFieldWidth;
            lengthTypeField.x += floatField.width + AuraGUI.MinSpaceBetweenFields;
            LengthType newType = AuraGUI.LengthTypeField(lengthTypeField, wrapper.Type);
            bool typeChanged = EditorGUI.EndChangeCheck();
            EditorGUI.EndProperty();

            if (typeChanged)
            {
                wrapper.Type = newType;
            }

            if (rawValueChanged)
            {
                wrapper.Raw = raw;
            }
        }
    }
}

