// Copyright (c) Supernova Technologies LLC
using Nova.Editor.Serialization;
using Nova.Internal.Utilities;
using UnityEditor;
using UnityEngine;
using static Nova.Editor.Serialization.Wrappers;

namespace Nova.Editor.GUIs
{
    [CustomEditor(typeof(ClipMask))]
    [CanEditMultipleObjects]
    internal class ClipRectEditor : NovaEditor<ClipMask>
    {
        private _ClipMaskInfo wrapper = new _ClipMaskInfo();

        protected override void OnEnable()
        {
            base.OnEnable();
            wrapper.SerializedProperty = serializedObject.FindProperty(Names.ClipMask.info);
            Undo.undoRedoPerformed += RestoreUndoneRedoneProperties;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RestoreUndoneRedoneProperties;
        }

        private void RestoreUndoneRedoneProperties()
        {
            UpdateTargets();
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            NovaGUI.ColorField(Labels.ClipMask.Tint, wrapper.ColorProp, true);
            NovaGUI.ToggleField(Labels.ClipMask.Clip, wrapper.ClipProp);
            SerializedProperty textureProp = serializedObject.FindProperty(Names.ClipMask.maskTexture);
            EditorGUILayout.ObjectField(textureProp, Labels.ClipMask.Mask);

            // Procedural clip mask options (stored on the ClipMask component)
            SerializedProperty proceduralProp = serializedObject.FindProperty("procedural");
            SerializedProperty proceduralPercentProp = serializedObject.FindProperty("proceduralPercent");
            SerializedProperty proceduralRotationProp = serializedObject.FindProperty("proceduralRotation");

            if (proceduralProp != null)
            {
                EditorGUILayout.PropertyField(proceduralProp, new GUIContent("Procedural Mask", "Use an implicit gradient mask instead of a texture."));
            }

            if (proceduralProp != null && proceduralProp.boolValue)
            {
                if (proceduralPercentProp != null)
                {
                    EditorGUILayout.Slider(proceduralPercentProp, 0f, 1f, new GUIContent("Slice Percent", "Percentage across the mask's gradient to cut at."));
                }

                if (proceduralRotationProp != null)
                {
                    EditorGUILayout.PropertyField(proceduralRotationProp, new GUIContent("Rotation", "Rotation (degrees) of the procedural gradient."));
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                UpdateTargets();
            }
        }

        private void UpdateTargets()
        {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].RegisterOrUpdate();
            }
            EditModeUtils.QueueEditorUpdateNextFrame();
        }
    }
}
