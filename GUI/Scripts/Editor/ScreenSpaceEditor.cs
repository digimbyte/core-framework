
using Aura.Editor.Serialization;
using UnityEditor;
using UnityEngine;

namespace Aura.Editor.GUIs
{
    [CustomEditor(typeof(ScreenSpace))]
    [CanEditMultipleObjects]
    internal class ScreenSpaceEditor : AuraEditor<ScreenSpace>
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            Undo.undoRedoPerformed += MarkDirty;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDirty;
        }

        public override void OnInspectorGUI()
        {
            // Target Camera
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.ObjectField(serializedObject.FindProperty(Names.ScreenSpace.targetCamera), typeof(Camera), Labels.ScreenSpace.TargetCamera);
            var fillModeProp = serializedObject.FindProperty(Names.ScreenSpace.fillMode);
            AuraGUI.EnumField(Labels.ScreenSpace.Mode, fillModeProp, targetComponents[0].Mode);

            var fillModeValue = (ScreenSpace.FillMode)fillModeProp.intValue;
            if (fillModeValue == ScreenSpace.FillMode.FixedWidth ||
                fillModeValue == ScreenSpace.FillMode.FixedHeight ||
                fillModeValue == ScreenSpace.FillMode.Adaptive)
            {
                AuraGUI.Vector2Field(Labels.ScreenSpace.ReferenceResolution, serializedObject.FindProperty(Names.ScreenSpace.referenceResolution));
            }

            AuraGUI.FloatFieldClamped(Labels.ScreenSpace.PlaneDistance, serializedObject.FindProperty(Names.ScreenSpace.planeDistance), 0f, float.MaxValue);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(Names.ScreenSpace.additionalCameras), Labels.ScreenSpace.AdditionalCameras);

            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
        }

        private void MarkDirty()
        {
            serializedObject.ApplyModifiedProperties();
            for (int i = 0; i < targetComponents.Count; ++i)
            {
                targetComponents[i].RegisterOrUpdate();
            }
        }
    }
}

