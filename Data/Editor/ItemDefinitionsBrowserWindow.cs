#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Core.Data.ItemDefinitions;
using Core.Enums;
using UnityEditor;
using UnityEngine;

namespace Core.Data.ItemDefinitions.Editor
{
    /// <summary>
    /// Paginated database-style browser for <see cref="ItemDefinitionsDatabase"/>:
    /// toolbar, result grid with selection, detail inspector, and pagination.
    /// </summary>
    public sealed class ItemDefinitionsBrowserWindow : EditorWindow
    {
        private const string EditorPrefsDatabaseGuidKey = "Core.ItemDefinitionsBrowser.DatabaseGuid";

        private const float RowHeight = 40f;
        private const float ThumbColumn = 44f;
        private const float MinSplitLeft = 340f;
        private const float DetailMinWidth = 280f;

        [SerializeField]
        private ItemDefinitionsDatabase database;

        private SerializedObject serializedDatabase;
        private Vector2 tableScroll;
        private Vector2 detailScroll;
        private string filter = string.Empty;
        private int pageIndex;
        private int pageSize = 15;
        private int? pendingDeleteEntryIndex;
        private int selectedEntryIndex = -1;

        private SortKey sortKey = SortKey.Name;
        private bool sortAscending = true;

        private GUIStyle headerLabelStyle;
        private GUIStyle toolbarSearchStyle;
        private GUIStyle statsStyle;
        private GUIStyle rowNameStyle;
        private GUIStyle rowMutedStyle;
        private GUIStyle panelTitleStyle;
        private bool stylesReady;

        private enum SortKey
        {
            Name,
            Uuid,
            Tags,
        }

        [MenuItem("Core/Data/Item Definitions Browser")]
        public static void Open()
        {
            var window = GetWindow<ItemDefinitionsBrowserWindow>();
            window.titleContent = new GUIContent("Item definitions");
            window.minSize = new Vector2(920, 520);
            window.Show();
        }

        private void OnEnable()
        {
            if (database == null)
            {
                string guid = EditorPrefs.GetString(EditorPrefsDatabaseGuidKey, string.Empty);
                if (!string.IsNullOrEmpty(guid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        database = AssetDatabase.LoadAssetAtPath<ItemDefinitionsDatabase>(path);
                    }
                }
            }

            SyncSerializedObject();
        }

        private void OnDisable()
        {
            if (database != null)
            {
                string path = AssetDatabase.GetAssetPath(database);
                if (!string.IsNullOrEmpty(path))
                {
                    EditorPrefs.SetString(EditorPrefsDatabaseGuidKey, AssetDatabase.AssetPathToGUID(path));
                }
            }
        }

        private void SyncSerializedObject()
        {
            serializedDatabase = database != null ? new SerializedObject(database) : null;
        }

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }

            stylesReady = true;
            headerLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            toolbarSearchStyle = new GUIStyle(EditorStyles.toolbarSearchField);
            statsStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = EditorStyles.miniLabel.fontSize,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.35f, 0.35f, 0.35f) },
            };
            rowNameStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
            };
            rowMutedStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = EditorStyles.miniLabel.fontSize,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.45f, 0.45f, 0.45f) },
            };
            panelTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
            };
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawDatabaseBar();

            if (database == null)
            {
                GUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Assign an Item Definitions Database asset, or create one: Create → Core → Data → Item Definitions Database.",
                    MessageType.Info);
                return;
            }

            serializedDatabase.Update();

            SerializedProperty entriesProp = serializedDatabase.FindProperty("entries");
            if (entriesProp == null || !entriesProp.isArray)
            {
                EditorGUILayout.HelpBox("Could not read entries on this asset.", MessageType.Error);
                serializedDatabase.ApplyModifiedProperties();
                return;
            }

            ProcessPendingDelete(entriesProp);

            List<int> filteredIndices = BuildFilteredIndices(entriesProp);
            SortFilteredIndices(entriesProp, filteredIndices);

            int totalInDb = entriesProp.arraySize;
            int totalFiltered = filteredIndices.Count;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(totalFiltered / (float)pageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);

            ProcessKeyboardNavigation(entriesProp, filteredIndices);

            ValidateSelection(filteredIndices);

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawStatsStrip(totalInDb, totalFiltered);
            DrawMainWorkspace(entriesProp, filteredIndices, totalFiltered);
            DrawPaginationFooter(totalFiltered, totalPages);
            EditorGUILayout.EndVertical();

            serializedDatabase.ApplyModifiedProperties();
        }

        private void DrawDatabaseBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.toolbar);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            database = (ItemDefinitionsDatabase)EditorGUILayout.ObjectField(
                GUIContent.none,
                database,
                typeof(ItemDefinitionsDatabase),
                false,
                GUILayout.MinWidth(180),
                GUILayout.MaxWidth(420));

            if (EditorGUI.EndChangeCheck())
            {
                selectedEntryIndex = -1;
                SyncSerializedObject();
            }

            GUILayout.Space(6);

            if (database != null)
            {
                if (GUILayout.Button(new GUIContent("Add row", "Append a new entry with a new UUID"), EditorStyles.toolbarButton, GUILayout.Width(72)))
                {
                    Undo.RecordObject(database, "Add item definition");
                    database.AddEntry();
                    database.EnsureUniqueUuids();
                    EditorUtility.SetDirty(database);
                    serializedDatabase.Update();
                    SerializedProperty entriesAfterAdd = serializedDatabase.FindProperty("entries");
                    if (entriesAfterAdd != null)
                    {
                        selectedEntryIndex = Mathf.Max(0, entriesAfterAdd.arraySize - 1);
                    }

                    int totalPagesNew = Mathf.Max(1, Mathf.CeilToInt(database.EntryCount / (float)pageSize));
                    pageIndex = totalPagesNew - 1;
                }

                if (GUILayout.Button(new GUIContent("Import rows…", "Paste a comma- or line-separated list of display names for new rows"), EditorStyles.toolbarButton, GUILayout.Width(96)))
                {
                    OpenImportNamesDialog();
                }

                if (GUILayout.Button(new GUIContent("Import prefabs…", "Create one row per prefab in a folder; thumbnails from RenderedPreviews when present"), EditorStyles.toolbarButton, GUILayout.Width(108)))
                {
                    OpenImportPrefabFolderDialog();
                }

                EditorGUI.BeginDisabledGroup(selectedEntryIndex < 0);
                if (GUILayout.Button(new GUIContent("Remove row", "Delete the selected row"), EditorStyles.toolbarButton, GUILayout.Width(88)))
                {
                    if (selectedEntryIndex >= 0 &&
                        EditorUtility.DisplayDialog(
                            "Remove item definition",
                            "Remove the selected entry from the database? You can undo with Ctrl+Z.",
                            "Remove",
                            "Cancel"))
                    {
                        pendingDeleteEntryIndex = selectedEntryIndex;
                    }
                }

                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button(new GUIContent("Repair UUIDs", "Fix empty or duplicate UUIDs"), EditorStyles.toolbarButton, GUILayout.Width(88)))
                {
                    Undo.RecordObject(database, "Repair item definition UUIDs");
                    database.EnsureUniqueUuids();
                    EditorUtility.SetDirty(database);
                    serializedDatabase.Update();
                }

                if (GUILayout.Button(new GUIContent("Ping", "Locate the database asset in the Project window"), EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    EditorGUIUtility.PingObject(database);
                }
            }

            GUILayout.FlexibleSpace();

            if (database != null)
            {
                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(44));
                filter = EditorGUILayout.TextField(filter, toolbarSearchStyle, GUILayout.MinWidth(160), GUILayout.MaxWidth(320));
                GUILayout.Space(8);
                GUILayout.Label("Rows / page", EditorStyles.miniLabel, GUILayout.Width(72));
                pageSize = EditorGUILayout.IntPopup(pageSize, new[] { "10", "15", "25", "50", "100" }, new[] { 10, 15, 25, 50, 100 }, GUILayout.Width(52));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void OpenImportPrefabFolderDialog()
        {
            ImportPrefabFolderPopup.Show(options =>
            {
                if (database == null || string.IsNullOrEmpty(options.FolderPath))
                {
                    return;
                }

                List<GameObject> prefabs = ImportPrefabFolderPopup.CollectPrefabAssetsInFolder(options.FolderPath, options.IncludeSubfolders);
                if (prefabs.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Import prefabs",
                        "No prefab assets were found in that folder (with the current subfolder option).",
                        "OK");
                    return;
                }

                var assignedPrefabs = new HashSet<GameObject>();
                if (options.SkipAssignedPrefabs)
                {
                    IReadOnlyList<ItemDefinitionEntry> existing = database.Entries;
                    for (int i = 0; i < existing.Count; i++)
                    {
                        ItemDefinitionEntry e = existing[i];
                        if (e != null && e.MainPrefab != null)
                        {
                            assignedPrefabs.Add(e.MainPrefab);
                        }
                    }
                }

                Undo.RecordObject(database, "Import item definitions from prefab folder");
                int added = 0;
                int skippedAssigned = 0;
                for (int i = 0; i < prefabs.Count; i++)
                {
                    GameObject prefab = prefabs[i];
                    if (prefab == null)
                    {
                        continue;
                    }

                    if (options.SkipAssignedPrefabs && assignedPrefabs.Contains(prefab))
                    {
                        skippedAssigned++;
                        continue;
                    }

                    TryFindRenderedPreviewTexture(prefab, out Texture2D previewTex);
                    Texture2D mainImg = options.CopyPreviewToMainImage ? previewTex : null;
                    string assetPath = AssetDatabase.GetAssetPath(prefab);
                    string rawFileName = string.IsNullOrEmpty(assetPath)
                        ? prefab.name
                        : Path.GetFileNameWithoutExtension(assetPath);
                    string displayName = ImportPrefabFolderPopup.FormatImportedPrefabDisplayName(
                        rawFileName,
                        options.StripNamePrefix,
                        options.UnderscoresToSpaces,
                        options.PreserveLeadingZerosInNumbers);

                    ItemDefinitionEntry row = database.AddEntryFromPrefabImport(prefab, displayName, previewTex, mainImg);
                    if (row != null)
                    {
                        added++;
                        if (options.SkipAssignedPrefabs)
                        {
                            assignedPrefabs.Add(prefab);
                        }
                    }
                }

                if (added == 0)
                {
                    EditorUtility.DisplayDialog(
                        "Import prefabs",
                        options.SkipAssignedPrefabs && skippedAssigned > 0
                            ? $"All {prefabs.Count} prefab(s) are already assigned as a main prefab on a row. Nothing was added."
                            : "No rows were added.",
                        "OK");
                    return;
                }

                EditorUtility.SetDirty(database);
                serializedDatabase.Update();
                SerializedProperty entriesAfterAdd = serializedDatabase.FindProperty("entries");
                if (entriesAfterAdd != null)
                {
                    selectedEntryIndex = Mathf.Max(0, entriesAfterAdd.arraySize - 1);
                    int totalPagesNew = Mathf.Max(1, Mathf.CeilToInt(entriesAfterAdd.arraySize / (float)pageSize));
                    pageIndex = totalPagesNew - 1;
                }

                Repaint();
            });
        }

        private void OpenImportNamesDialog()
        {
            ImportNamesPopup.Show(names =>
            {
                if (database == null || names == null || names.Count == 0)
                {
                    return;
                }

                Undo.RecordObject(database, "Import item definition names");
                int added = database.AppendEntriesFromDisplayNames(names);
                if (added == 0)
                {
                    return;
                }

                EditorUtility.SetDirty(database);
                serializedDatabase.Update();
                SerializedProperty entriesAfterAdd = serializedDatabase.FindProperty("entries");
                if (entriesAfterAdd != null)
                {
                    selectedEntryIndex = Mathf.Max(0, entriesAfterAdd.arraySize - 1);
                    int totalPagesNew = Mathf.Max(1, Mathf.CeilToInt(entriesAfterAdd.arraySize / (float)pageSize));
                    pageIndex = totalPagesNew - 1;
                }

                Repaint();
            });
        }

        private void ProcessPendingDelete(SerializedProperty entriesProp)
        {
            if (!pendingDeleteEntryIndex.HasValue)
            {
                return;
            }

            int idx = pendingDeleteEntryIndex.Value;
            pendingDeleteEntryIndex = null;
            if (idx < 0 || idx >= entriesProp.arraySize)
            {
                return;
            }

            int deleted = idx;
            Undo.RecordObject(database, "Remove item definition");
            entriesProp.DeleteArrayElementAtIndex(idx);
            EditorUtility.SetDirty(database);

            if (selectedEntryIndex == deleted)
            {
                selectedEntryIndex = -1;
            }
            else if (selectedEntryIndex > deleted)
            {
                selectedEntryIndex--;
            }
        }

        private void ProcessKeyboardNavigation(SerializedProperty entriesProp, List<int> filtered)
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || database == null)
            {
                return;
            }

            if (filtered.Count == 0)
            {
                return;
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)pageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            int start = pageIndex * pageSize;

            int posInFiltered = filtered.IndexOf(selectedEntryIndex);
            if (e.keyCode == KeyCode.DownArrow)
            {
                if (posInFiltered < 0 && filtered.Count > 0)
                {
                    selectedEntryIndex = filtered[start];
                }
                else
                {
                    int next = posInFiltered + 1;
                    if (next < filtered.Count)
                    {
                        selectedEntryIndex = filtered[next];
                        int rowOnPage = next - start;
                        if (rowOnPage >= pageSize)
                        {
                            pageIndex = Mathf.Min(totalPages - 1, pageIndex + 1);
                        }

                        tableScroll.y += RowHeight;
                    }
                }

                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                if (posInFiltered > 0)
                {
                    int prev = posInFiltered - 1;
                    selectedEntryIndex = filtered[prev];
                    int rowOnPage = prev - start;
                    if (rowOnPage < 0 && pageIndex > 0)
                    {
                        pageIndex--;
                    }

                    tableScroll.y = Mathf.Max(0, tableScroll.y - RowHeight);
                }

                e.Use();
                Repaint();
            }
        }

        private void ValidateSelection(List<int> filteredIndices)
        {
            if (selectedEntryIndex < 0)
            {
                return;
            }

            bool ok = false;
            for (int i = 0; i < filteredIndices.Count; i++)
            {
                if (filteredIndices[i] == selectedEntryIndex)
                {
                    ok = true;
                    break;
                }
            }

            if (!ok)
            {
                selectedEntryIndex = -1;
            }
        }

        private void DrawStatsStrip(int totalInDb, int totalFiltered)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            string filterNote = string.IsNullOrWhiteSpace(filter) ? "no filter" : $"filter '{filter.Trim()}'";
            EditorGUILayout.LabelField(
                $"{totalFiltered} shown · {totalInDb} total in database · {filterNote}",
                statsStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawMainWorkspace(
            SerializedProperty entriesProp,
            List<int> filteredIndices,
            int totalFiltered)
        {
            int start = pageIndex * pageSize;
            int end = Mathf.Min(start + pageSize, totalFiltered);

            float leftW = Mathf.Clamp(
                position.width * 0.56f,
                MinSplitLeft,
                Mathf.Max(MinSplitLeft, position.width - DetailMinWidth - 24f));

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(leftW), GUILayout.ExpandHeight(true));
            DrawTableSection(entriesProp, filteredIndices, start, end, totalFiltered);
            EditorGUILayout.EndVertical();

            GUILayout.Space(6);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawDetailSection(entriesProp);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTableSection(
            SerializedProperty entriesProp,
            List<int> filteredIndices,
            int start,
            int end,
            int totalFiltered)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Catalog", panelTitleStyle);
            GUILayout.Space(2);

            if (totalFiltered == 0)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    EditorGUILayout.HelpBox(
                        "No rows yet. Choose Add row or Import rows.",
                        MessageType.Info);
                    GUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(new GUIContent("Import rows…", "Paste a comma- or line-separated list of names"), GUILayout.Height(26)))
                        {
                            OpenImportNamesDialog();
                        }

                        if (GUILayout.Button(new GUIContent("Import prefabs…", "One row per prefab in a folder"), GUILayout.Height(26)))
                        {
                            OpenImportPrefabFolderDialog();
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("Add row", "Append one empty row"), GUILayout.Height(26), GUILayout.Width(88)))
                        {
                            Undo.RecordObject(database, "Add item definition");
                            database.AddEntry();
                            database.EnsureUniqueUuids();
                            EditorUtility.SetDirty(database);
                            serializedDatabase.Update();
                            SerializedProperty entriesAfterAdd = serializedDatabase.FindProperty("entries");
                            if (entriesAfterAdd != null)
                            {
                                selectedEntryIndex = Mathf.Max(0, entriesAfterAdd.arraySize - 1);
                            }

                            int totalPagesNew = Mathf.Max(1, Mathf.CeilToInt(database.EntryCount / (float)pageSize));
                            pageIndex = totalPagesNew - 1;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No rows match the search.", MessageType.Info);
                }

                return;
            }

            DrawColumnHeaders();

            tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = start; i < end; i++)
            {
                int entryIndex = filteredIndices[i];
                bool zebra = ((i - start) & 1) == 0;
                DrawDataRow(entriesProp, entryIndex, zebra);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawColumnHeaders()
        {
            Rect headerRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(headerRect, EditorGUIUtility.isProSkin ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.72f, 0.72f, 0.72f));
            }

            GUILayout.BeginArea(headerRect);
            GUILayout.BeginHorizontal();

            GUILayout.Space(ThumbColumn + 6);
            DrawSortableHeader("Name", SortKey.Name);
            DrawSortableHeader("Internal UUID", SortKey.Uuid);
            DrawSortableHeader("Tags", SortKey.Tags);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUILayout.Space(2);
        }

        private void DrawSortableHeader(string label, SortKey key)
        {
            string caret = sortKey == key ? (sortAscending ? " ▲" : " ▼") : string.Empty;
            var content = new GUIContent(label + caret);
            if (GUILayout.Button(content, headerLabelStyle, GUILayout.ExpandWidth(true)))
            {
                if (sortKey == key)
                {
                    sortAscending = !sortAscending;
                }
                else
                {
                    sortKey = key;
                    sortAscending = key != SortKey.Tags;
                }
            }
        }

        private void DrawDataRow(SerializedProperty entriesProp, int entryIndex, bool zebraEven)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(entryIndex);
            if (entry == null)
            {
                return;
            }

            SerializedProperty nameProp = entry.FindPropertyRelative("displayName");
            SerializedProperty uuidProp = entry.FindPropertyRelative("itemUuid");
            SerializedProperty tagsProp = entry.FindPropertyRelative("tagKeys");
            SerializedProperty thumbProp = entry.FindPropertyRelative("thumbnailTexture");

            string name = nameProp != null ? nameProp.stringValue : string.Empty;
            string uuid = uuidProp != null ? uuidProp.stringValue : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = string.IsNullOrWhiteSpace(uuid) ? "(unnamed)" : uuid;
            }

            string uuidShort = FormatUuidShort(uuid);
            string tagsShort = tagsProp != null ? FormatTagsShort(tagsProp) : "—";

            Rect rowRect = GUILayoutUtility.GetRect(1f, RowHeight, GUILayout.ExpandWidth(true));
            bool selected = selectedEntryIndex == entryIndex;

            if (Event.current.type == EventType.Repaint)
            {
                Color zebra = zebraEven
                    ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.45f) : new Color(1f, 1f, 1f, 0.35f))
                    : (EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f, 0.45f) : new Color(0.94f, 0.94f, 0.94f, 0.5f));
                EditorGUI.DrawRect(rowRect, zebra);
                if (selected)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.26f, 0.48f, 0.88f, 0.35f));
                }
            }

            if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
            {
                selectedEntryIndex = entryIndex;
                GUI.FocusControl(null);
                Repaint();
            }

            if (Event.current.type == EventType.Repaint)
            {
                const float pad = 4f;
                float innerH = Mathf.Max(1f, rowRect.height - pad * 2f);
                float x = rowRect.x + pad;
                Rect thumbRect = new Rect(x, rowRect.y + pad, ThumbColumn, innerH);
                x += ThumbColumn + 6f;

                float remaining = rowRect.width - (ThumbColumn + 6f + pad * 2f);
                float nameW = remaining * 0.46f;
                float uuidW = remaining * 0.28f;
                float tagsW = remaining * 0.26f;

                Rect nameRect = new Rect(x, rowRect.y + pad, nameW, innerH);
                x += nameW;
                Rect uuidRect = new Rect(x, rowRect.y + pad, uuidW, innerH);
                x += uuidW;
                Rect tagsRect = new Rect(x, rowRect.y + pad, tagsW, innerH);

                Texture2D thumb = thumbProp != null ? thumbProp.objectReferenceValue as Texture2D : null;
                if (thumb != null)
                {
                    GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
                }
                else
                {
                    EditorGUI.DrawRect(thumbRect, new Color(0.12f, 0.12f, 0.12f, 1f));
                    GUI.Label(thumbRect, "—", EditorStyles.centeredGreyMiniLabel);
                }

                GUI.Label(nameRect, name, rowNameStyle);
                GUI.Label(uuidRect, uuidShort, rowMutedStyle);
                GUI.Label(tagsRect, tagsShort, rowMutedStyle);
            }

            EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);

            GUILayout.Space(1);
        }

        private void DrawDetailSection(SerializedProperty entriesProp)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField("Row details", panelTitleStyle);
            GUILayout.Space(4);

            if (selectedEntryIndex < 0 || selectedEntryIndex >= entriesProp.arraySize)
            {
                EditorGUILayout.HelpBox("Select a row in the catalog to edit thumbnails, prefabs, tags, and copy.", MessageType.None);
                return;
            }

            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(selectedEntryIndex);
            if (entry == null)
            {
                return;
            }

            EditorGUILayout.LabelField($"Database index #{selectedEntryIndex}", EditorStyles.miniLabel);
            GUILayout.Space(6);

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            DrawEntryEditor(entry);
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryEditor(SerializedProperty entry)
        {
            SerializedProperty uuidProp = entry.FindPropertyRelative("itemUuid");
            SerializedProperty nameProp = entry.FindPropertyRelative("displayName");
            SerializedProperty descProp = entry.FindPropertyRelative("description");
            SerializedProperty tagsProp = entry.FindPropertyRelative("tagKeys");
            SerializedProperty thumbProp = entry.FindPropertyRelative("thumbnailTexture");
            SerializedProperty mainImgProp = entry.FindPropertyRelative("mainImageTexture");
            SerializedProperty mainPrefabProp = entry.FindPropertyRelative("mainPrefab");
            SerializedProperty secondPrefabProp = entry.FindPropertyRelative("secondPrefab");

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(uuidProp, new GUIContent("Internal UUID"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
            EditorGUILayout.PropertyField(descProp, new GUIContent("Description"), GUILayout.MinHeight(48));

            if (tagsProp != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
                IReadOnlyList<SerialEnum> tagOptions = database != null ? database.GetItemTagOptions() : Array.Empty<SerialEnum>();
                if (database != null && (tagOptions == null || tagOptions.Count == 0))
                {
                    EditorGUILayout.HelpBox(
                        "Assign Item Tags Enum Library and Item Tags Group Key on the database asset in the Inspector.",
                        MessageType.Info);
                }

                DrawTagChips(tagsProp, tagOptions);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                mainPrefabProp,
                new GUIContent(
                    "Main prefab",
                    "When this reference changes, the thumbnail is synced from RenderedPreviews next to the prefab ({name}_preview…). If nothing matches, the thumbnail is cleared so it never shows the wrong item."));
            if (EditorGUI.EndChangeCheck())
            {
                SyncThumbnailFromMainPrefab(thumbProp, mainPrefabProp.objectReferenceValue as GameObject);
            }

            EditorGUILayout.PropertyField(thumbProp, new GUIContent("Thumbnail texture"));
            EditorGUILayout.PropertyField(secondPrefabProp, new GUIContent("Second prefab"));
            EditorGUILayout.PropertyField(mainImgProp, new GUIContent("Main image texture"));

            GUILayout.Space(12);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove this row…", GUILayout.Width(140)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Remove item definition",
                            "Remove this entry from the database? You can undo with Ctrl+Z.",
                            "Remove",
                            "Cancel"))
                    {
                        pendingDeleteEntryIndex = selectedEntryIndex;
                    }
                }
            }
        }

        private void DrawPaginationFooter(int totalFiltered, int totalPages)
        {
            if (database == null || totalFiltered == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.BeginDisabledGroup(pageIndex <= 0);
            if (GUILayout.Button("First", GUILayout.Width(52)))
            {
                pageIndex = 0;
            }

            if (GUILayout.Button("Previous", GUILayout.Width(72)))
            {
                pageIndex = Mathf.Max(0, pageIndex - 1);
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            int lo = pageIndex * pageSize + 1;
            int hi = Mathf.Min(totalFiltered, (pageIndex + 1) * pageSize);
            EditorGUILayout.LabelField(
                $"Page {pageIndex + 1} of {totalPages}   ·   rows {lo}–{hi} of {totalFiltered}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(pageIndex >= totalPages - 1);
            if (GUILayout.Button("Next", GUILayout.Width(72)))
            {
                pageIndex = Mathf.Min(totalPages - 1, pageIndex + 1);
            }

            if (GUILayout.Button("Last", GUILayout.Width(52)))
            {
                pageIndex = totalPages - 1;
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        private List<int> BuildFilteredIndices(SerializedProperty entriesProp)
        {
            var result = new List<int>();
            string f = filter.Trim().ToLowerInvariant();
            int n = entriesProp.arraySize;
            for (int i = 0; i < n; i++)
            {
                SerializedProperty el = entriesProp.GetArrayElementAtIndex(i);
                if (el == null)
                {
                    continue;
                }

                SerializedProperty nameProp = el.FindPropertyRelative("displayName");
                SerializedProperty uuidProp = el.FindPropertyRelative("itemUuid");
                SerializedProperty descProp = el.FindPropertyRelative("description");
                SerializedProperty tagsProp = el.FindPropertyRelative("tagKeys");
                string name = nameProp != null ? nameProp.stringValue : string.Empty;
                string uuid = uuidProp != null ? uuidProp.stringValue : string.Empty;
                string desc = descProp != null ? descProp.stringValue : string.Empty;
                if (string.IsNullOrEmpty(f))
                {
                    result.Add(i);
                    continue;
                }

                bool tagMatch = false;
                if (tagsProp != null && tagsProp.isArray)
                {
                    for (int ti = 0; ti < tagsProp.arraySize; ti++)
                    {
                        SerializedProperty keyProp = tagsProp.GetArrayElementAtIndex(ti).FindPropertyRelative("key");
                        string tk = keyProp != null ? keyProp.stringValue : string.Empty;
                        if (!string.IsNullOrEmpty(tk) && tk.ToLowerInvariant().Contains(f))
                        {
                            tagMatch = true;
                            break;
                        }
                    }
                }

                if ((name != null && name.ToLowerInvariant().Contains(f)) ||
                    (uuid != null && uuid.ToLowerInvariant().Contains(f)) ||
                    (desc != null && desc.ToLowerInvariant().Contains(f)) ||
                    tagMatch)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private void SortFilteredIndices(SerializedProperty entriesProp, List<int> indices)
        {
            if (indices.Count <= 1)
            {
                return;
            }

            indices.Sort((a, b) =>
            {
                SerializedProperty ea = entriesProp.GetArrayElementAtIndex(a);
                SerializedProperty eb = entriesProp.GetArrayElementAtIndex(b);
                if (ea == null || eb == null)
                {
                    return 0;
                }

                int cmp = 0;
                switch (sortKey)
                {
                    case SortKey.Name:
                        string na = ea.FindPropertyRelative("displayName")?.stringValue ?? string.Empty;
                        string nb = eb.FindPropertyRelative("displayName")?.stringValue ?? string.Empty;
                        cmp = string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
                        break;
                    case SortKey.Uuid:
                        string ua = ea.FindPropertyRelative("itemUuid")?.stringValue ?? string.Empty;
                        string ub = eb.FindPropertyRelative("itemUuid")?.stringValue ?? string.Empty;
                        cmp = string.Compare(ua, ub, StringComparison.OrdinalIgnoreCase);
                        break;
                    case SortKey.Tags:
                        string ta = ConcatTagKeysForSort(ea);
                        string tb = ConcatTagKeysForSort(eb);
                        cmp = string.Compare(ta, tb, StringComparison.OrdinalIgnoreCase);
                        break;
                }

                if (!sortAscending)
                {
                    cmp = -cmp;
                }

                return cmp;
            });
        }

        private static string FormatUuidShort(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return "—";
            }

            return uuid.Length <= 14 ? uuid : uuid.Substring(0, 12) + "…";
        }

        private static string ConcatTagKeysForSort(SerializedProperty entry)
        {
            SerializedProperty tagsProp = entry.FindPropertyRelative("tagKeys");
            if (tagsProp == null || !tagsProp.isArray || tagsProp.arraySize == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(tagsProp.arraySize);
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty keyProp = tagsProp.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                string k = keyProp != null ? keyProp.stringValue : string.Empty;
                if (!string.IsNullOrEmpty(k))
                {
                    parts.Add(k);
                }
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            parts.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(",", parts);
        }

        private static string FormatTagsShort(SerializedProperty tagsProp)
        {
            if (tagsProp == null || !tagsProp.isArray || tagsProp.arraySize == 0)
            {
                return "—";
            }

            var parts = new List<string>(tagsProp.arraySize);
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty keyProp = tagsProp.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                string k = keyProp != null ? keyProp.stringValue : string.Empty;
                if (!string.IsNullOrEmpty(k))
                {
                    parts.Add(ShortTagLabel(k));
                }
            }

            if (parts.Count == 0)
            {
                return "—";
            }

            parts.Sort(StringComparer.OrdinalIgnoreCase);
            string s = string.Join(", ", parts);
            return s.Length > 42 ? s.Substring(0, 40) + "…" : s;
        }

        private static string ShortTagLabel(string fullKey)
        {
            if (string.IsNullOrEmpty(fullKey))
            {
                return string.Empty;
            }

            int dot = fullKey.LastIndexOf('.');
            return dot >= 0 && dot + 1 < fullKey.Length ? fullKey.Substring(dot + 1) : fullKey;
        }

        private static bool ListContainsTagKey(SerializedProperty listProp, string key)
        {
            if (listProp == null || !listProp.isArray || string.IsNullOrEmpty(key))
            {
                return false;
            }

            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty keyProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                if (keyProp != null && string.Equals(keyProp.stringValue, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetTagKeyPresent(SerializedProperty listProp, string key, bool present)
        {
            if (listProp == null || !listProp.isArray || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (present)
            {
                if (ListContainsTagKey(listProp, key))
                {
                    return;
                }

                int idx = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(idx);
                SerializedProperty el = listProp.GetArrayElementAtIndex(idx);
                SerializedProperty keyProp = el.FindPropertyRelative("key");
                if (keyProp != null)
                {
                    keyProp.stringValue = key;
                }
            }
            else
            {
                for (int i = listProp.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty keyProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                    if (keyProp == null || !string.Equals(keyProp.stringValue, key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    listProp.DeleteArrayElementAtIndex(i);
                    if (i < listProp.arraySize)
                    {
                        SerializedProperty k2 = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                        if (k2 != null && string.IsNullOrEmpty(k2.stringValue))
                        {
                            listProp.DeleteArrayElementAtIndex(i);
                        }
                    }

                    break;
                }
            }
        }

        private static void RemoveTagKey(SerializedProperty listProp, string key)
        {
            SetTagKeyPresent(listProp, key, false);
        }

        private static void SyncThumbnailFromMainPrefab(SerializedProperty thumbnailProp, GameObject prefab)
        {
            if (thumbnailProp == null)
            {
                return;
            }

            if (prefab == null)
            {
                thumbnailProp.objectReferenceValue = null;
                return;
            }

            if (TryFindRenderedPreviewTexture(prefab, out Texture2D preview))
            {
                thumbnailProp.objectReferenceValue = preview;
            }
            else
            {
                thumbnailProp.objectReferenceValue = null;
            }
        }

        private static bool TryFindRenderedPreviewTexture(GameObject prefab, out Texture2D texture)
        {
            texture = null;
            if (prefab == null)
            {
                return false;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return false;
            }

            string folder = Path.GetDirectoryName(prefabPath);
            if (string.IsNullOrEmpty(folder))
            {
                return false;
            }

            folder = folder.Replace('\\', '/');
            string previewFolder = folder + "/RenderedPreviews";
            if (!AssetDatabase.IsValidFolder(previewFolder))
            {
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(prefabPath);
            string namePrefix = baseName + "_preview";

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { previewFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(assetPath);
                if (fileNameNoExt.Length < namePrefix.Length ||
                    !fileNameNoExt.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null)
                {
                    continue;
                }

                texture = tex;
                return true;
            }

            return false;
        }

        private void DrawTagChips(SerializedProperty tagsProp, IReadOnlyList<SerialEnum> options)
        {
            var optionKeys = new HashSet<string>(StringComparer.Ordinal);
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    string k = options[i].Key;
                    if (!string.IsNullOrEmpty(k))
                    {
                        optionKeys.Add(k);
                    }
                }
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    string key = options[i].Key;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    bool on = ListContainsTagKey(tagsProp, key);
                    string label = ShortTagLabel(key);
                    bool next = GUILayout.Toggle(on, label, "MiniButton", GUILayout.ExpandWidth(false));
                    if (next != on)
                    {
                        SetTagKeyPresent(tagsProp, key, next);
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            var orphans = new List<string>();
            if (tagsProp != null && tagsProp.isArray)
            {
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    SerializedProperty keyProp = tagsProp.GetArrayElementAtIndex(i).FindPropertyRelative("key");
                    string key = keyProp != null ? keyProp.stringValue : string.Empty;
                    if (string.IsNullOrEmpty(key) || optionKeys.Contains(key))
                    {
                        continue;
                    }

                    orphans.Add(key);
                }
            }

            if (orphans.Count > 0)
            {
                orphans.Sort(StringComparer.OrdinalIgnoreCase);
                EditorGUILayout.LabelField("Not in library (remove or fix the group key)", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                for (int oi = 0; oi < orphans.Count; oi++)
                {
                    string key = orphans[oi];
                    if (GUILayout.Button(ShortTagLabel(key) + " ×", "MiniButton", GUILayout.ExpandWidth(false)))
                    {
                        RemoveTagKey(tagsProp, key);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// Options passed from <see cref="ImportPrefabFolderPopup"/> when the user confirms an import.
    /// </summary>
    internal readonly struct ImportPrefabFolderCommitOptions
    {
        public readonly string FolderPath;
        public readonly bool IncludeSubfolders;
        public readonly bool SkipAssignedPrefabs;
        public readonly bool CopyPreviewToMainImage;
        /// <summary>Resolved prefix to strip (from auto-detection or manual field).</summary>
        public readonly string StripNamePrefix;
        public readonly bool UnderscoresToSpaces;
        public readonly bool PreserveLeadingZerosInNumbers;

        public ImportPrefabFolderCommitOptions(
            string folderPath,
            bool includeSubfolders,
            bool skipAssignedPrefabs,
            bool copyPreviewToMainImage,
            string resolvedStripNamePrefix,
            bool underscoresToSpaces,
            bool preserveLeadingZerosInNumbers)
        {
            FolderPath = folderPath;
            IncludeSubfolders = includeSubfolders;
            SkipAssignedPrefabs = skipAssignedPrefabs;
            CopyPreviewToMainImage = copyPreviewToMainImage;
            StripNamePrefix = resolvedStripNamePrefix ?? string.Empty;
            UnderscoresToSpaces = underscoresToSpaces;
            PreserveLeadingZerosInNumbers = preserveLeadingZerosInNumbers;
        }
    }

    /// <summary>
    /// Pick a project folder and import every prefab inside as a new item definition row.
    /// </summary>
    internal sealed class ImportPrefabFolderPopup : EditorWindow
    {
        private DefaultAsset folderAsset;
        private bool includeSubfolders = true;
        private bool skipAssignedPrefabs = true;
        private bool copyPreviewToMainImage;
        private bool useAutoDetectedSharedPrefix = true;
        private string manualStripNamePrefix = string.Empty;
        private bool underscoresToSpaces = true;
        private bool preserveLeadingZerosInNumbers = true;
        private Action<ImportPrefabFolderCommitOptions> onCommit;

        public static void Show(Action<ImportPrefabFolderCommitOptions> onCommit)
        {
            var window = CreateInstance<ImportPrefabFolderPopup>();
            window.onCommit = onCommit;
            window.titleContent = new GUIContent("Import prefabs from folder");
            window.minSize = new Vector2(520, 420);
            window.ShowUtility();
        }

        private static int CommonPrefixLengthIgnoreCase(string a, string b)
        {
            int n = Mathf.Min(a.Length, b.Length);
            int i = 0;
            while (i < n && char.ToUpperInvariant(a[i]) == char.ToUpperInvariant(b[i]))
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// Longest prefix shared by every file name (letter case ignored), trimmed to end at the last underscore or hyphen when that yields a long enough prefix.
        /// Returns empty when there are fewer than two names, when there is no shared prefix, or when the result is shorter than <paramref name="minimumLength"/>.
        /// </summary>
        internal static string DetectSharedFilenamePrefix(IReadOnlyList<string> fileNamesWithoutExtension, int minimumLength = 2)
        {
            if (fileNamesWithoutExtension == null || fileNamesWithoutExtension.Count < 2)
            {
                return string.Empty;
            }

            var names = new List<string>(fileNamesWithoutExtension.Count);
            for (int i = 0; i < fileNamesWithoutExtension.Count; i++)
            {
                string s = fileNamesWithoutExtension[i]?.Trim() ?? string.Empty;
                if (s.Length > 0)
                {
                    names.Add(s);
                }
            }

            if (names.Count < 2)
            {
                return string.Empty;
            }

            string prefix = names[0];
            for (int i = 1; i < names.Count; i++)
            {
                int len = CommonPrefixLengthIgnoreCase(prefix, names[i]);
                if (len == 0)
                {
                    return string.Empty;
                }

                prefix = prefix.Substring(0, len);
            }

            int lastSep = -1;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (prefix[i] == '_' || prefix[i] == '-')
                {
                    lastSep = i;
                }
            }

            string refined = lastSep >= 0 ? prefix.Substring(0, lastSep + 1) : prefix;
            if (refined.Length >= minimumLength)
            {
                return refined;
            }

            if (prefix.Length >= minimumLength)
            {
                return prefix;
            }

            return string.Empty;
        }

        internal static string ResolveStripPrefixForImport(
            bool useAutoDetectedSharedPrefix,
            string manualStripNamePrefix,
            IReadOnlyList<GameObject> prefabsInFolder)
        {
            if (!useAutoDetectedSharedPrefix)
            {
                return manualStripNamePrefix?.Trim() ?? string.Empty;
            }

            if (prefabsInFolder == null || prefabsInFolder.Count < 2)
            {
                return string.Empty;
            }

            var names = new List<string>(prefabsInFolder.Count);
            for (int i = 0; i < prefabsInFolder.Count; i++)
            {
                GameObject prefab = prefabsInFolder[i];
                if (prefab == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(prefab);
                string baseName = string.IsNullOrEmpty(path)
                    ? prefab.name
                    : Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(baseName))
                {
                    names.Add(baseName);
                }
            }

            return DetectSharedFilenamePrefix(names, 2);
        }

        /// <summary>
        /// Build a catalog display name from a prefab asset file name (no extension) using the import naming rules.
        /// </summary>
        internal static string FormatImportedPrefabDisplayName(
            string fileNameWithoutExtension,
            string stripNamePrefix,
            bool underscoresToSpaces,
            bool preserveLeadingZerosInNumbers)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
            {
                return string.Empty;
            }

            string name = fileNameWithoutExtension.Trim();
            string prefix = stripNamePrefix?.Trim() ?? string.Empty;
            if (prefix.Length > 0 &&
                name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(prefix.Length);
                name = name.TrimStart('_', ' ', '-');
            }

            if (underscoresToSpaces)
            {
                name = name.Replace('_', ' ');
            }

            name = Regex.Replace(name, @"\s+", " ").Trim();

            if (!preserveLeadingZerosInNumbers && name.Length > 0)
            {
                name = Regex.Replace(name, @"\d+", StripLeadingZerosFromDigitRun);
            }

            return name;
        }

        private static string StripLeadingZerosFromDigitRun(Match match)
        {
            string digits = match.Value;
            int i = 0;
            while (i < digits.Length - 1 && digits[i] == '0')
            {
                i++;
            }

            return digits.Substring(i);
        }

        /// <summary>
        /// Prefab assets under <paramref name="folderAssetPath"/>; excludes assets under a <c>RenderedPreviews</c> path.
        /// </summary>
        internal static List<GameObject> CollectPrefabAssetsInFolder(string folderAssetPath, bool includeSubfolders)
        {
            var result = new List<GameObject>();
            if (string.IsNullOrEmpty(folderAssetPath))
            {
                return result;
            }

            string normalizedFolder = folderAssetPath.TrimEnd('/').Replace("\\", "/");
            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { normalizedFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (path.IndexOf("/RenderedPreviews/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (!AssetPathMatchesFolderImportScope(path, normalizedFolder, includeSubfolders))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    result.Add(prefab);
                }
            }

            result.Sort((a, b) =>
                string.Compare(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b), StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static bool AssetPathMatchesFolderImportScope(string assetPath, string folderPath, bool includeSubfolders)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(folderPath))
            {
                return false;
            }

            string normalizedFolder = folderPath.TrimEnd('/').Replace("\\", "/");
            string normalizedAsset = assetPath.Replace("\\", "/");
            if (includeSubfolders)
            {
                return normalizedAsset.StartsWith(normalizedFolder + "/", StringComparison.OrdinalIgnoreCase);
            }

            string parentDir = Path.GetDirectoryName(normalizedAsset)?.Replace("\\", "/");
            return parentDir != null &&
                   string.Equals(parentDir, normalizedFolder, StringComparison.OrdinalIgnoreCase);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Prefab folder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag a project folder that contains prefabs. Each prefab becomes one row with Main prefab set. " +
                "If the prefab folder has a sibling RenderedPreviews folder with textures named like MyPrefab_preview…, " +
                "the thumbnail is assigned the same way as when you pick Main prefab by hand.",
                MessageType.Info);

            folderAsset = (DefaultAsset)EditorGUILayout.ObjectField("Folder", folderAsset, typeof(DefaultAsset), false);

            includeSubfolders = EditorGUILayout.ToggleLeft("Include subfolders", includeSubfolders);
            skipAssignedPrefabs = EditorGUILayout.ToggleLeft("Skip prefabs already used as some row's main prefab", skipAssignedPrefabs);
            copyPreviewToMainImage = EditorGUILayout.ToggleLeft(
                "Also assign main image texture to the same preview texture (optional)",
                copyPreviewToMainImage);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Names from files", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "When automatic prefix is on, the importer looks at every prefab file name in the folder and removes the longest text they all share at the start (letter case ignored). " +
                "If that text contains underscores or hyphens, it prefers to cut at the last one so whole segments drop off together. " +
                "Turn it off to type a prefix yourself.",
                MessageType.None);

            useAutoDetectedSharedPrefix = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Detect shared file name prefix automatically",
                    "Needs at least two prefabs. If names do not line up, nothing is stripped."),
                useAutoDetectedSharedPrefix);

            if (!useAutoDetectedSharedPrefix)
            {
                manualStripNamePrefix = EditorGUILayout.TextField(
                    new GUIContent(
                        "Remove name prefix (manual)",
                        "If a prefab file name starts with this exact text (letter case ignored), that part is removed. Leave empty to skip."),
                    manualStripNamePrefix);
            }

            underscoresToSpaces = EditorGUILayout.ToggleLeft(
                "Replace underscores with spaces",
                underscoresToSpaces);
            preserveLeadingZerosInNumbers = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Keep leading zeros in numbers",
                    "When on, digit runs stay as in the file (0010 stays 0010). When off, leading zeros are dropped (0010 becomes 10, 001 becomes 1)."),
                preserveLeadingZerosInNumbers);

            string folderPath = folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : string.Empty;
            bool folderOk = !string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath);
            if (folderOk)
            {
                List<GameObject> listedPrefabs = CollectPrefabAssetsInFolder(folderPath, includeSubfolders);
                EditorGUILayout.LabelField($"{listedPrefabs.Count} prefab(s) will be imported with the current options.", EditorStyles.miniLabel);

                string effectivePrefix = ResolveStripPrefixForImport(
                    useAutoDetectedSharedPrefix,
                    manualStripNamePrefix,
                    listedPrefabs);

                if (useAutoDetectedSharedPrefix)
                {
                    if (listedPrefabs.Count < 2)
                    {
                        EditorGUILayout.LabelField("Shared prefix (auto): (needs at least two prefabs)", EditorStyles.miniLabel);
                    }
                    else if (string.IsNullOrEmpty(effectivePrefix))
                    {
                        EditorGUILayout.LabelField(
                            "Shared prefix (auto): (none — names do not share a long enough start, or none at all)",
                            EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Shared prefix (auto): \"{effectivePrefix}\"", EditorStyles.miniLabel);
                    }
                }

                if (listedPrefabs.Count > 0)
                {
                    string sampleRaw = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(listedPrefabs[0]));
                    string sampleFormatted = FormatImportedPrefabDisplayName(
                        sampleRaw,
                        effectivePrefix,
                        underscoresToSpaces,
                        preserveLeadingZerosInNumbers);
                    EditorGUILayout.LabelField(
                        $"First file name preview: \"{sampleRaw}\" → \"{sampleFormatted}\"",
                        EditorStyles.miniLabel);
                }
            }
            else if (folderAsset != null)
            {
                EditorGUILayout.HelpBox("Pick a folder asset from the Project window (not a file).", MessageType.Warning);
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(26)))
            {
                Close();
            }

            using (new EditorGUI.DisabledScope(!folderOk))
            {
                if (GUILayout.Button("Import", GUILayout.Height(26)))
                {
                    List<GameObject> prefabsForCommit = CollectPrefabAssetsInFolder(folderPath, includeSubfolders);
                    string resolvedPrefix = ResolveStripPrefixForImport(
                        useAutoDetectedSharedPrefix,
                        manualStripNamePrefix,
                        prefabsForCommit);

                    onCommit?.Invoke(new ImportPrefabFolderCommitOptions(
                        folderPath,
                        includeSubfolders,
                        skipAssignedPrefabs,
                        copyPreviewToMainImage,
                        resolvedPrefix,
                        underscoresToSpaces,
                        preserveLeadingZerosInNumbers));
                    Close();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>
    /// Modal-style utility window to paste many display names at once.
    /// </summary>
    internal sealed class ImportNamesPopup : EditorWindow
    {
        private string pasteBuffer = string.Empty;
        private Action<IReadOnlyList<string>> onCommit;
        private Vector2 pasteScroll;

        private const float ImportPopupScrollReserve = 132f;
        private const float ImportPopupScrollMinHeight = 160f;
        private const float ImportPopupScrollMaxHeight = 420f;

        public static void Show(Action<IReadOnlyList<string>> onCommit)
        {
            var window = CreateInstance<ImportNamesPopup>();
            window.onCommit = onCommit;
            window.titleContent = new GUIContent("Import rows");
            window.minSize = new Vector2(440, 380);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Paste row names", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One display name per row. Separate with commas or line breaks. Extra spaces are trimmed; empty entries are skipped. For prefabs and RenderedPreviews thumbnails, use Import prefabs in the browser toolbar instead.",
                MessageType.Info);

            float availableHeight = position.height > 10f ? position.height : minSize.y;
            float scrollHeight = Mathf.Clamp(
                availableHeight - ImportPopupScrollReserve,
                ImportPopupScrollMinHeight,
                ImportPopupScrollMaxHeight);

            pasteScroll = EditorGUILayout.BeginScrollView(
                pasteScroll,
                GUILayout.Height(scrollHeight),
                GUILayout.ExpandWidth(true));

            int lineCount = 1;
            for (int i = 0; i < pasteBuffer.Length; i++)
            {
                if (pasteBuffer[i] == '\n')
                {
                    lineCount++;
                }
            }

            float linePitch = EditorGUIUtility.singleLineHeight + 2f;
            float innerHeight = Mathf.Max(scrollHeight, lineCount * linePitch + 24f);
            pasteBuffer = EditorGUILayout.TextArea(pasteBuffer, GUILayout.Height(innerHeight), GUILayout.ExpandWidth(true));

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(26)))
            {
                Close();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(pasteBuffer)))
            {
                if (GUILayout.Button("Import", GUILayout.Height(26)))
                {
                    List<string> names = ParseNameList(pasteBuffer);
                    if (names.Count == 0)
                    {
                        EditorUtility.DisplayDialog(
                            "Import rows",
                            "No names found. Use commas or line breaks between names.",
                            "OK");
                    }
                    else
                    {
                        onCommit?.Invoke(names);
                        Close();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static List<string> ParseNameList(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            string normalized = raw.Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(',');
                for (int pi = 0; pi < parts.Length; pi++)
                {
                    string t = parts[pi]?.Trim();
                    if (!string.IsNullOrEmpty(t))
                    {
                        result.Add(t);
                    }
                }
            }

            return result;
        }
    }
}
#endif
