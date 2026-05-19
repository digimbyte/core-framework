using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core.Registry.Editor
{
    [CustomEditor(typeof(Registry))]
    public class RegistryEditor : Sirenix.OdinInspector.Editor.OdinEditor
    {
        private bool importFoldout = true;
        private bool importRecursive = false;
        private DefaultAsset folderToImport;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);
            DrawImportSection();
        }

        private void DrawImportSection()
        {
            Registry registry = (Registry)target;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            importFoldout = EditorGUILayout.Foldout(importFoldout, "Import Assets from Folder", true, EditorStyles.foldoutHeader);
            
            if (importFoldout)
            {
                EditorGUILayout.Space(5);
                
                importRecursive = EditorGUILayout.Toggle("Recursive (Include Subfolders)", importRecursive);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Drag Folder Here:", EditorStyles.boldLabel);
                
                Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drop Folder to Import Assets", EditorStyles.helpBox);
                
                EditorGUI.BeginChangeCheck();
                folderToImport = (DefaultAsset)EditorGUI.ObjectField(
                    new Rect(dropArea.x + 5, dropArea.y + 15, dropArea.width - 10, 20),
                    folderToImport,
                    typeof(DefaultAsset),
                    false
                );
                
                HandleDragAndDrop(dropArea, registry);
                
                if (EditorGUI.EndChangeCheck() && folderToImport != null)
                {
                    string folderPath = AssetDatabase.GetAssetPath(folderToImport);
                    if (AssetDatabase.IsValidFolder(folderPath))
                    {
                        ImportAssetsFromFolder(registry, folderPath);
                        folderToImport = null;
                    }
                    else
                    {
                        folderToImport = null;
                    }
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"Import all {registry.AssetType} assets from a folder.\n" +
                    "UIDs will be generated from file names.\n" +
                    (importRecursive ? "Subfolder paths will be prefixed to UIDs (e.g., 'Subfolder/AssetName')." : "Only assets in the root folder will be imported."),
                    MessageType.Info
                );
            }
            
            EditorGUILayout.EndVertical();
        }

        private void HandleDragAndDrop(Rect dropArea, Registry registry)
        {
            Event evt = Event.current;
            
            if (!dropArea.Contains(evt.mousePosition))
                return;

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(draggedObject);
                    if (AssetDatabase.IsValidFolder(path))
                        ImportAssetsFromFolder(registry, path);
                }
            }
            
            evt.Use();
        }

        private void ImportAssetsFromFolder(Registry registry, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return;

            RegistryAssetType assetType = registry.AssetType;
            List<string> assetPaths = Registry.CollectAssetPathsInFolder(folderPath, importRecursive)
                .Where(path => Registry.IsCandidateAssetPath(path, assetType))
                .Distinct()
                .ToList();

            int importedCount = 0;
            int skippedDuplicates = 0;
            int matchingTypeCount = 0;

            Undo.RecordObject(registry, "Import Assets to Registry");

            foreach (string assetPath in assetPaths)
            {
                if (!Registry.MatchesRegistryAssetPath(assetPath, assetType))
                    continue;

                matchingTypeCount++;

                Object asset = Registry.LoadAssetAtPathForRegistry(assetPath, assetType);
                if (asset == null)
                    continue;

                string uid = GenerateUIDFromPath(assetPath, folderPath);

                if (registry.HasItem(uid))
                {
                    skippedDuplicates++;
                    continue;
                }

                ItemEntry newEntry = new ItemEntry
                {
                    uid = uid,
                    asset = asset,
                    description = $"Imported from {assetPath}",
                    tags = new List<string>(),
                    metadata = new SerializableDictionary<string, string>()
                };

                registry.AddItem(newEntry);
                importedCount++;
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            string duplicateNote = skippedDuplicates > 0 ? $" ({skippedDuplicates} duplicates skipped)" : string.Empty;
            Debug.Log(
                $"[RegistryEditor] Imported {importedCount} of {assetPaths.Count}, {matchingTypeCount} " +
                $"{Registry.GetRegistryTypeLabel(assetType)} found{duplicateNote}.");
        }

        private string GenerateUIDFromPath(string assetPath, string baseFolderPath)
        {
            assetPath = assetPath.Replace('\\', '/');
            baseFolderPath = baseFolderPath.Replace('\\', '/');
            
            string relativePath = assetPath.Substring(baseFolderPath.Length + 1);
            relativePath = Path.ChangeExtension(relativePath, null);
            
            if (importRecursive)
                return relativePath.Replace('\\', '/');

            return Path.GetFileNameWithoutExtension(assetPath);
        }
    }
}
