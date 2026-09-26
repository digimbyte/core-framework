
using Aura.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    [CustomPropertyDrawer(typeof(NavNode))]
    internal class NavNodeDrawer : AuraPropertyDrawer<_NavNode>
    {
        protected override float GetPropertyHeight(GUIContent label)
        {
            if (!wrapper.SerializedProperty.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            int numSpaces = 5;
            float height = PropertyDrawerUtils.SingleLineHeight;

            height += NavLinkDrawer.GetPropertyHeight(wrapper.Up);
            height += Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Left), NavLinkDrawer.GetPropertyHeight(wrapper.Right));
            height += NavLinkDrawer.GetPropertyHeight(wrapper.Down);

            if (AuraEditorPrefs.DisplayNavigationZAxis)
            {
                numSpaces++;
                height += Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Back), NavLinkDrawer.GetPropertyHeight(wrapper.Forward));
            }

            return height + (numSpaces * AuraGUI.MinSpaceBetweenFields);
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, wrapper.SerializedProperty);

            wrapper.SerializedProperty.isExpanded = EditorGUI.Foldout(position, wrapper.SerializedProperty.isExpanded, label);

            if (!wrapper.SerializedProperty.isExpanded)
            {
                return;
            }

            position.BumpLine();

            Rect box = position;
            box.height = GetPropertyHeight(label) - (PropertyDrawerUtils.SingleLineHeight + AuraGUI.MinSpaceBetweenFields);

            EditorGUI.HelpBox(box, string.Empty, MessageType.None);

            position.y += AuraGUI.MinSpaceBetweenFields;
            position.xMin += AuraGUI.MinSpaceBetweenFields;
            position.xMax -= AuraGUI.MinSpaceBetweenFields;

            Draw3DToggle(position);

            position.height = NavLinkDrawer.GetPropertyHeight(wrapper.Up);

            Rect center = position.Center(position.width * 0.5f);
            EditorGUI.PropertyField(center, wrapper.UpProp);

            position.Bump(position.height + AuraGUI.MinSpaceBetweenFields);
            position.height = Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Left), NavLinkDrawer.GetPropertyHeight(wrapper.Right));

            position.Split(out Rect left, out Rect right);
            EditorGUI.PropertyField(left, wrapper.LeftProp);
            EditorGUI.PropertyField(right, wrapper.RightProp);

            position.Bump(position.height + AuraGUI.MinSpaceBetweenFields);
            position.height = NavLinkDrawer.GetPropertyHeight(wrapper.Down);

            center = position.Center(position.width * 0.5f);
            EditorGUI.PropertyField(center, wrapper.DownProp);

            if (AuraEditorPrefs.DisplayNavigationZAxis)
            {
                position.Bump(position.height + AuraGUI.MinSpaceBetweenFields);
                position.height = Mathf.Max(NavLinkDrawer.GetPropertyHeight(wrapper.Back), NavLinkDrawer.GetPropertyHeight(wrapper.Forward));

                position.Split(out left, out right);
                EditorGUI.PropertyField(left, wrapper.BackProp);
                EditorGUI.PropertyField(right, wrapper.ForwardProp);
            }


            EditorGUI.EndFoldoutHeaderGroup();

            EditorGUI.EndProperty();
        }

        private void Draw3DToggle(Rect position)
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 2 * AuraGUI.SingleCharacterGUIWidth;

            float toggleWidth = 2 * AuraGUI.SingleCharacterGUIWidth;
            Rect zToggle = position.TopRight(toggleWidth, PropertyDrawerUtils.SingleLineHeight);
            AuraEditorPrefs.DisplayNavigationZAxis = GUI.Toggle(zToggle, AuraEditorPrefs.DisplayNavigationZAxis, Labels.NavNode.ThreeDToggle, AuraGUI.Styles.ToolbarButtonMid);
            EditorGUIUtility.labelWidth = labelWidth;
        }
    }
}
