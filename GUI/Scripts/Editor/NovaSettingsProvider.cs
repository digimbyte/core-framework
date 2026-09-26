
using Aura.Editor.GUIs;
using Aura.Editor.Serialization;
using System.Collections.Generic;
using UnityEditor;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor
{
    internal class AuraSettingsProvider : SettingsProvider
    {
        private _SettingsConfig config = new _SettingsConfig();
        private SerializedObject serializedObject = null;

        public override void OnGUI(string searchContext)
        {
            if (serializedObject == null || config.SerializedProperty == null)
            {
                serializedObject = new SerializedObject(AuraSettings.Instance);
                config.SerializedProperty = serializedObject.FindProperty(Names.AuraSettings.settings);
            }

            serializedObject.UpdateIfRequiredOrScript();

            Foldout.InProjectSettings = true;
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

            Foldout.InProjectSettings = false;
        }

        private void MarkDirty(bool fireEvents)
        {
            serializedObject.ApplyModifiedProperties();
            if (fireEvents)
            {
                AuraSettings.Instance.MarkDirty(true, false);
            }
        }

        public AuraSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }

        [SettingsProvider]
        private static SettingsProvider CreateProjectSettingsProvider()
        {
            AuraSettingsProvider provider = new AuraSettingsProvider("Project/Aura", SettingsScope.Project, SettingsProvider.GetSearchKeywordsFromGUIContentProperties<Labels.Settings>());
            return provider;
        }
    }
}

