
using Aura.Editor.GUIs;
using Aura.Editor.Serialization;
using UnityEditor;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor
{
    [CustomEditor(typeof(AuraSettings))]
    internal class AuraSettingsEditor : AuraEditor
    {
        private _SettingsConfig config = new _SettingsConfig();

        private void OnEnable()
        {
            config.SerializedProperty = serializedObject.FindProperty(Names.AuraSettings.settings);
            Undo.undoRedoPerformed += RestoreUndoneRedoneProperties;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RestoreUndoneRedoneProperties;
        }

        private void RestoreUndoneRedoneProperties()
        {
            MarkDirty(true);
            serializedObject.UpdateIfRequiredOrScript();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUI.BeginChangeCheck();
            AuraSettingsEditors.DrawGeneral(config);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(false);
            }

            EditorGUI.BeginChangeCheck();
            AuraSettingsEditors.DrawRendering(config);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(true);
            }

            EditorGUI.BeginChangeCheck();
            AuraSettingsEditors.DrawInput(config);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(false);
            }

            EditorGUI.BeginChangeCheck();
            AuraSettingsEditors.DrawEditor(serializedObject);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty(false);
            }
        }

        private void MarkDirty(bool fireEvents)
        {
            serializedObject.ApplyModifiedProperties();
            if (fireEvents)
            {
                AuraSettings.Instance.MarkDirty(true, false);
            }
        }
    }
}
