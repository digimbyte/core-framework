using Nova;
using NovaSamples.UIControls;
using UnityEditor;
using UnityEngine;

namespace NovaSamples.UIControls.Editor
{
    [CustomEditor(typeof(TextField))]
    [CanEditMultipleObjects]
    public class TextFieldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            DrawTextBlockContentSection();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTextBlockContentSection()
        {
            SerializedProperty textBlockProp = serializedObject.FindProperty("textBlock");
            if (textBlockProp == null)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Text Block Content", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Content type, password mask, and number range are stored on the linked Text Block.",
                MessageType.None);

            TextBlock textBlock = textBlockProp.objectReferenceValue as TextBlock;
            if (textBlock == null)
            {
                EditorGUILayout.HelpBox("Assign Text Block above to configure content filtering.", MessageType.Info);
                return;
            }

            SerializedObject blockObject = new SerializedObject(textBlock);
            blockObject.Update();

            SerializedProperty contentTypeProp = blockObject.FindProperty("contentType");
            SerializedProperty passwordMaskProp = blockObject.FindProperty("passwordMask");
            SerializedProperty useNumberRangeProp = blockObject.FindProperty("useNumberRange");
            SerializedProperty numberMinProp = blockObject.FindProperty("numberMin");
            SerializedProperty numberMaxProp = blockObject.FindProperty("numberMax");

            EditorGUI.BeginChangeCheck();

            if (contentTypeProp != null)
            {
                EditorGUILayout.PropertyField(contentTypeProp, new GUIContent("Content Type"));
            }

            bool showPasswordMask = true;
            bool showNumberRange = false;
            if (contentTypeProp != null && !contentTypeProp.hasMultipleDifferentValues)
            {
                showPasswordMask = contentTypeProp.enumValueIndex == (int)TextBlock.ContentType.Password;
                showNumberRange = contentTypeProp.enumValueIndex == (int)TextBlock.ContentType.Numbers;
            }

            if (passwordMaskProp != null && showPasswordMask)
            {
                EditorGUILayout.PropertyField(passwordMaskProp, new GUIContent("Password Mask"));
            }

            if (showNumberRange)
            {
                if (useNumberRangeProp != null)
                {
                    EditorGUILayout.PropertyField(useNumberRangeProp, new GUIContent("Use Number Range"));
                }

                bool drawMinMax = useNumberRangeProp == null || useNumberRangeProp.boolValue;
                if (useNumberRangeProp != null && useNumberRangeProp.hasMultipleDifferentValues)
                {
                    drawMinMax = true;
                }

                if (drawMinMax)
                {
                    if (numberMinProp != null)
                    {
                        EditorGUILayout.PropertyField(numberMinProp, new GUIContent("Number Min"));
                    }

                    if (numberMaxProp != null)
                    {
                        EditorGUILayout.PropertyField(numberMaxProp, new GUIContent("Number Max"));
                    }
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                blockObject.ApplyModifiedProperties();
            }
        }
    }
}
