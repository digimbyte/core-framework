
using Aura.Internal.Rendering;
using UnityEditor;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    internal static class AuraSettingsEditors
    {
        // This is the longest label in the settings menu right now, don't need to overengineer and loop through to check them.
        private static readonly float MaxLabelWidth = EditorStyles.label.CalcSize(Labels.Settings.UIBlock3DCornerDivisions).x + AuraGUI.MinSpaceBetweenFields;

        public static void DrawGeneral(_SettingsConfig config)
        {
            using Foldout foldout = AuraGUI.EditorPrefFoldoutHeader("General");

            if (!foldout)
            {
                return;
            }

            AuraGUI.Layout.BeginHorizontal();

            // This is an indent
            AuraGUI.Space(AuraGUI.Layout.FoldoutArrowIndentSpace);

            float labelWidth = AuraGUI.LabelWidth;

            AuraGUI.LabelWidth = MaxLabelWidth;

            AuraGUI.EnumFlagsField(Labels.Settings.LogFlags, config.LogFlagsProp, AuraSettings.LogFlags);

            AuraGUI.LabelWidth = labelWidth;

            AuraGUI.Layout.EndHorizontal();
        }

        public static void DrawRendering(_SettingsConfig config)
        {
            using Foldout foldout = AuraGUI.EditorPrefFoldoutHeader("Rendering");

            if (!foldout)
            {
                return;
            }


            AuraGUI.Layout.BeginHorizontal();

            // This is an indent
            AuraGUI.Space(AuraGUI.Layout.FoldoutArrowIndentSpace);

            AuraGUI.Layout.BeginVertical();

            float labelWidth = AuraGUI.LabelWidth;

            AuraGUI.LabelWidth = MaxLabelWidth;

            AuraGUI.ToggleField(Labels.Settings.PackedImages, config.PackedImagesEnabledProp);
            AuraGUI.ToggleField(Labels.Settings.SuperSampleText, config.SuperSampleTextProp);
            AuraGUI.SliderField(Labels.Settings.EdgeSoftenWidth, config.EdgeSoftenWidthProp, 1f, 3f);

            AuraGUI.IntSlider(Labels.Settings.UIBlock3DCornerDivisions, config.UIBlock3DCornerDivisionsProp, 0, 20);
            AuraGUI.IntSlider(Labels.Settings.UIBlock3DEdgeDivisions, config.UIBlock3DEdgeDivisionsProp, 0, 20);

            AuraGUI.EnumField(Labels.Settings.PackedImageCopyMode, config.PackedImageCopyModeProp, AuraSettings.PackedImageCopyMode);

            if (SystemSettings.UsingScriptableRenderPipeline)
            {
                AuraGUI.WarningIcon(Labels.Surface.DisabledSurfaceSRPWarning);
            }

            EditorGUI.BeginDisabledGroup(SystemSettings.UsingScriptableRenderPipeline);

            AuraGUI.PrefixLabel(Labels.Settings.LightingModelsToBuild);

            EditorGUI.indentLevel++;
            AuraGUI.EnumFlagsField(Labels.Settings.UIBlock2DLightingModels, config.UIBlock2DLightingModelsProp, AuraSettings.UIBlock2DLightingModels);
            AuraGUI.EnumFlagsField(Labels.Settings.TextBlockLightingModels, config.TextBlockLightingModelsProp, AuraSettings.TextBlockLightingModels);
            AuraGUI.EnumFlagsField(Labels.Settings.UIBlock3DLightingModels, config.UIBlock3DLightingModelsProp, AuraSettings.UIBlock3DLightingModels);
            EditorGUI.indentLevel--;

            EditorGUI.EndDisabledGroup();

            AuraGUI.LabelWidth = labelWidth;
            AuraGUI.Layout.EndVertical();

            AuraGUI.Layout.EndHorizontal();
        }

        public static void DrawInput(_SettingsConfig config)
        {
            using Foldout foldout = AuraGUI.EditorPrefFoldoutHeader("Input");

            if (!foldout)
            {
                return;
            }

            AuraGUI.Layout.BeginHorizontal();

            // This is an indent
            AuraGUI.Space(AuraGUI.Layout.FoldoutArrowIndentSpace);

            AuraGUI.Layout.BeginVertical();

            float labelWidth = AuraGUI.LabelWidth;
            AuraGUI.LabelWidth = MaxLabelWidth;


            AuraGUI.IntSlider(Labels.Settings.ClickThreshold, config.ClickFrameDeltaThresholdProp, 0, 5);

            AuraGUI.LabelWidth = labelWidth;
            AuraGUI.Layout.EndVertical();

            AuraGUI.Layout.EndHorizontal();

        }

        public static void DrawEditor(SerializedObject serializedObject)
        {
            using Foldout foldout = AuraGUI.EditorPrefFoldoutHeader("Editor");

            if (!foldout)
            {
                return;
            }

            float labelWidth = AuraGUI.LabelWidth;

            AuraGUI.LabelWidth = MaxLabelWidth;

            AuraGUI.Layout.BeginHorizontal();

            // This is an indent
            AuraGUI.Space(AuraGUI.Layout.FoldoutArrowIndentSpace);

            EditorGUI.BeginChangeCheck();
            bool edgeSnappingEnabled = EditorGUILayout.Toggle(Labels.Settings.EdgeSnapping, AuraEditorPrefs.EdgeSnappingEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                AuraEditorPrefs.EdgeSnappingEnabled = edgeSnappingEnabled;
            }

            AuraGUI.Layout.EndHorizontal();

            AuraGUI.Layout.BeginHorizontal();

            // This is an indent
            AuraGUI.Space(AuraGUI.Layout.FoldoutArrowIndentSpace);

            EditorGUI.BeginChangeCheck();
            bool hierarchyGizmos = EditorGUILayout.Toggle(Labels.Settings.HierarchyGizmos, AuraEditorPrefs.HierarchyGizmosEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                AuraEditorPrefs.HierarchyGizmosEnabled = hierarchyGizmos;
            }

            AuraGUI.Layout.EndHorizontal();

            EditorGUILayout.Space();

            AuraGUI.Layout.BeginHorizontal();

            // This is an indent
            AuraGUI.Space(AuraGUI.Layout.FoldoutArrowIndentSpace);

            AuraGUI.Layout.BeginVertical();

            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.ButtonPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.TogglePrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.SliderPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.DropdownPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.TextFieldPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.ScrollViewPrefab)));
            EditorGUILayout.ObjectField(serializedObject.FindProperty(nameof(AuraSettings.UIRootPrefab)));

            AuraGUI.Layout.EndVertical();

            AuraGUI.Layout.EndHorizontal();

            AuraGUI.LabelWidth = labelWidth;
        }
    }
}

