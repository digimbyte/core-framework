using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Core.Enums;

namespace Core.Enums.Editor
{
    [CustomEditor(typeof(StringEnumLibrary))]
    public class StringEnumLibraryEditor : UnityEditor.Editor
    {
        private const string OutputPath = "Assets/Core/Data/StringEnums.generated.cs";
        private const string Namespace = "Core.Enums";

        private SerializedProperty groupsProp;
        private bool[] groupFoldouts;

        /// <summary>
        /// Reorderable lists for each group's values, keyed by <see cref="SerializedProperty.propertyPath"/>.
        /// Cleared when the groups array size changes so paths stay aligned with entries.
        /// </summary>
        private Dictionary<string, ReorderableList> valueListsByPropertyPath;

        private int cachedGroupsArraySize = -1;

        private void OnEnable()
        {
            groupsProp = serializedObject.FindProperty("groups");
            groupFoldouts = null;
            InvalidateValueReorderableLists();
        }

        private void InvalidateValueReorderableLists()
        {
            valueListsByPropertyPath?.Clear();
            cachedGroupsArraySize = -1;
        }

        private ReorderableList GetOrCreateValuesReorderableList(SerializedProperty valuesProp)
        {
            if (valuesProp == null)
                return null;

            string path = valuesProp.propertyPath;
            if (valueListsByPropertyPath != null &&
                valueListsByPropertyPath.TryGetValue(path, out var existing) &&
                existing != null &&
                existing.serializedProperty != null &&
                existing.serializedProperty.propertyPath == path)
            {
                return existing;
            }

            valueListsByPropertyPath ??= new Dictionary<string, ReorderableList>(System.StringComparer.Ordinal);

            var list = new ReorderableList(valuesProp.serializedObject, valuesProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Entries (drag rows to reorder)");
            list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2f;
                rect.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, element, GUIContent.none);
            };
            list.onAddCallback = reorderableList =>
            {
                int index = reorderableList.serializedProperty.arraySize;
                reorderableList.serializedProperty.InsertArrayElementAtIndex(index);
                reorderableList.serializedProperty.GetArrayElementAtIndex(index).stringValue = string.Empty;
            };

            valueListsByPropertyPath[path] = list;
            return list;
        }

        public override void OnInspectorGUI()
        {
            var library = (StringEnumLibrary)target;

            serializedObject.Update();

            DrawGroupsSection();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);

            DrawStatusPanel(library);
        }

        private void DrawGroupsSection()
        {
            if (groupsProp == null)
            {
                EditorGUILayout.HelpBox("groups property not found", MessageType.Error);
                return;
            }

            if (cachedGroupsArraySize != groupsProp.arraySize)
            {
                valueListsByPropertyPath?.Clear();
                cachedGroupsArraySize = groupsProp.arraySize;
            }

            EditorGUILayout.LabelField("Enum Groups", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each group is Name:[Entries], e.g. Name = 'Ammo', Entries = ['Small','Medium','Large'].", MessageType.None);

            const int removeButtonWidth = 22;

            // Ensure foldout array size
            int size = groupsProp.arraySize;
            if (groupFoldouts == null || groupFoldouts.Length != size)
            {
                var newFoldouts = new bool[size];
                for (int i = 0; i < size; i++)
                    newFoldouts[i] = groupFoldouts != null && i < groupFoldouts.Length ? groupFoldouts[i] : true;
                groupFoldouts = newFoldouts;
            }

            for (int i = 0; i < groupsProp.arraySize; i++)
            {
                var groupProp  = groupsProp.GetArrayElementAtIndex(i);
                var keyProp    = groupProp.FindPropertyRelative("key");
                var valuesProp = groupProp.FindPropertyRelative("values");

                EditorGUILayout.BeginVertical("box");

                // Group header: [ foldout ][ name ][ - ]
                EditorGUILayout.BeginHorizontal();
                groupFoldouts[i] = EditorGUILayout.Foldout(groupFoldouts[i], GUIContent.none, true, EditorStyles.foldout);
                keyProp.stringValue = EditorGUILayout.TextField("Enum Name", keyProp.stringValue ?? string.Empty);
                if (GUILayout.Button("-", GUILayout.Width(removeButtonWidth)))
                {
                    groupsProp.DeleteArrayElementAtIndex(i);
                    InvalidateValueReorderableLists();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                if (groupFoldouts[i])
                {
                    if (valuesProp != null)
                    {
                        EditorGUI.indentLevel++;
                        ReorderableList valueList = GetOrCreateValuesReorderableList(valuesProp);
                        if (valueList != null)
                        {
                            valueList.DoLayoutList();
                        }

                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            if (GUILayout.Button("+ Add Group", GUILayout.Height(22)))
            {
                int newIndex = groupsProp.arraySize;
                groupsProp.InsertArrayElementAtIndex(newIndex);
                var newGroup = groupsProp.GetArrayElementAtIndex(newIndex);
                newGroup.FindPropertyRelative("key").stringValue = string.Empty;
                var valuesProp = newGroup.FindPropertyRelative("values");
                if (valuesProp != null)
                {
                    valuesProp.ClearArray();
                }
            }
        }

        private void DrawStatusPanel(StringEnumLibrary library)
        {
            // Dirty indicator panel
            string currentSnapshot = StringEnumCodeGenerator.BuildSnapshot(library);
            bool isDirty = currentSnapshot != library.lastGeneratedSnapshot;

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = isDirty ? new Color(0.5f, 0.1f, 0.1f) : new Color(0.1f, 0.3f, 0.1f);

            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevColor; // reset for inner controls

            string statusLabel = isDirty ? "Enum changes not saved" : "Enums are up to date";
            EditorGUILayout.LabelField(statusLabel, EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                isDirty
                    ? "Changes to keys are not yet reflected in generated code.\nClick 'Save / Generate' to update StringEnums.generated.cs and trigger a compile."
                    : "The generated StringEnums.generated.cs matches the current data.\nYou can still force regeneration if needed.",
                isDirty ? MessageType.Warning : MessageType.Info);

            if (GUILayout.Button("Save / Generate String Enum Constants", GUILayout.Height(26)))
            {
                StringEnumCodeGenerator.GenerateAndStamp(library, OutputPath, Namespace);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
