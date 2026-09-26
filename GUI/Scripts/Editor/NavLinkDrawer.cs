
using Aura.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(NavLink))]
    internal class NavLinkDrawer : AuraPropertyDrawer<_NavLink>
    {
        public const float Padding = AuraGUI.MinSpaceBetweenFields * 2;

        public static float GetPropertyHeight(_NavLink property)
        {
            int lineCount = property.Type == NavLinkType.Manual ? 4 : 3;
            return (lineCount * PropertyDrawerUtils.SingleLineHeight) + Padding + AuraGUI.MinSpaceBetweenFields;
        }

        protected override float GetPropertyHeight(GUIContent label) => GetPropertyHeight(wrapper);

        protected override void OnGUI(Rect position, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, wrapper.SerializedProperty);

            Rect box = position;
            box.height = GetPropertyHeight(label);

            EditorGUI.HelpBox(box, string.Empty, MessageType.None);

            position.y += AuraGUI.MinSpaceBetweenFields;

            Rect centerLabel = position.Center(EditorStyles.label.CalcSize(label).x);
            EditorGUI.LabelField(centerLabel, label);
            AuraGUI.Styles.DrawSeparator(position, useControlWidth: true);

            position.BumpLine();
            Rect background = position;
            background.height = box.height - (PropertyDrawerUtils.SingleLineHeight + AuraGUI.MinSpaceBetweenFields);
            AuraGUI.Styles.Draw(background, AuraGUI.Styles.OverlayColor);

            position.y += AuraGUI.MinSpaceBetweenFields;
            position.xMin += Padding;
            position.xMax -= Padding;

            float labelWidth = EditorGUIUtility.labelWidth;
            float propertyLabelWidth = EditorStyles.label.CalcSize(Labels.NavLink.FallbackLabel).x + AuraGUI.MinSpaceBetweenFields;

            EditorGUIUtility.labelWidth = propertyLabelWidth;
            EditorGUI.PropertyField(position, wrapper.TypeProp, Labels.NavLink.TypeLabel);

            if (wrapper.Type == NavLinkType.Manual)
            {
                position.BumpLine();
                Rect targetLabel = position;

                bool showTargetWarning = !wrapper.TargetProp.hasMultipleDifferentValues && wrapper.Target != null && !wrapper.Target.Navigable;

                if (showTargetWarning)
                {   
                    targetLabel.width = EditorStyles.label.CalcSize(Labels.NavLink.TargetLabel).x;
                    
                    Rect warning = targetLabel;
                    warning.x = warning.xMax;
                    warning.width = AuraGUI.ToggleBoxSize;

                    EditorGUIUtility.labelWidth = AuraGUI.ToggleBoxSize;
                    EditorGUI.LabelField(warning, Labels.NavLink.TargetNotNavigableWarningLabel);
                }

                EditorGUIUtility.labelWidth = targetLabel.width;
                EditorGUI.LabelField(targetLabel, Labels.NavLink.TargetLabel);

                EditorGUIUtility.labelWidth = 0;
                Rect targetField = position;
                targetField.xMin += propertyLabelWidth + AuraGUI.MinSpaceBetweenFields;
                EditorGUI.PropertyField(targetField, wrapper.TargetProp, GUIContent.none);

                EditorGUIUtility.labelWidth = propertyLabelWidth;
            }

            position.BumpLine();
            EditorGUI.PropertyField(position, wrapper.FallbackProp, Labels.NavLink.FallbackLabel);

            EditorGUIUtility.labelWidth = labelWidth;

            EditorGUI.EndProperty();
        }
    }
}
