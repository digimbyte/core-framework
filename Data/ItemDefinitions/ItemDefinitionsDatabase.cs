using System;
using System.Collections.Generic;
using Core.Enums;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Data.ItemDefinitions
{
    /// <summary>
    /// Scriptable catalog of item definitions (textures, prefabs, tags, stable UUIDs per row).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDefinitions", menuName = "Core/Data/Item Definitions Database")]
    public sealed class ItemDefinitionsDatabase : ScriptableObject
    {
        [SerializeField]
        [TextArea(1, 3)]
        private string notes = string.Empty;

        [SerializeField]
        [Tooltip("Library that defines allowed item tag values for this catalog.")]
        private StringEnumLibrary itemTagsEnumLibrary;

        [SerializeField]
        [Tooltip("Group key inside the library (same as the group key), for example item.category.")]
        private string itemTagsGroupKey = string.Empty;

        [SerializeField]
        private List<ItemDefinitionEntry> entries = new List<ItemDefinitionEntry>();

        public string Notes => notes;

        public StringEnumLibrary ItemTagsEnumLibrary => itemTagsEnumLibrary;

        public string ItemTagsGroupKey => itemTagsGroupKey;

        /// <summary>
        /// Tag options from <see cref="itemTagsEnumLibrary"/> for <see cref="itemTagsGroupKey"/> (empty if not configured).
        /// </summary>
        public IReadOnlyList<SerialEnum> GetItemTagOptions()
        {
            if (itemTagsEnumLibrary == null || string.IsNullOrWhiteSpace(itemTagsGroupKey))
            {
                return Array.Empty<SerialEnum>();
            }

            return itemTagsEnumLibrary.GetSerialEnumsForGroup(itemTagsGroupKey);
        }

        public IReadOnlyList<ItemDefinitionEntry> Entries => entries;

        public int EntryCount => entries.Count;

        /// <summary>
        /// Find an entry by its internal UUID string (case-insensitive).
        /// </summary>
        public ItemDefinitionEntry GetEntryByUuid(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return null;
            }

            string key = uuid.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && string.Equals(e.ItemUuid, key, StringComparison.OrdinalIgnoreCase))
                {
                    return e;
                }
            }

            return null;
        }

        /// <summary>
        /// Append a new row with a fresh UUID. Editor and tools should persist the asset after calling this.
        /// </summary>
        public ItemDefinitionEntry AddEntry()
        {
            var row = new ItemDefinitionEntry();
            row.SetUuidInternal(NewUuid());
            entries.Add(row);
            return row;
        }

        /// <summary>
        /// Append one row with a prefab and optional preview textures. When <paramref name="displayName"/> is empty or whitespace, uses the prefab object's name (usually matches the asset file name).
        /// </summary>
        /// <returns>The new row, or null if <paramref name="mainPrefab"/> is null.</returns>
        public ItemDefinitionEntry AddEntryFromPrefabImport(
            GameObject mainPrefab,
            string displayName,
            Texture2D thumbnailTexture,
            Texture2D mainImageTexture)
        {
            if (mainPrefab == null)
            {
                return null;
            }

            ItemDefinitionEntry row = AddEntry();
            row.SetDisplayNameInternal(string.IsNullOrWhiteSpace(displayName) ? mainPrefab.name : displayName.Trim());
            row.SetMainPrefabInternal(mainPrefab);
            row.SetThumbnailInternal(thumbnailTexture);
            row.SetMainImageInternal(mainImageTexture);
            EnsureUniqueUuids();
            return row;
        }

        /// <summary>
        /// Append one row per non-empty display name (trimmed). Each row gets a new UUID.
        /// </summary>
        /// <returns>How many rows were added.</returns>
        public int AppendEntriesFromDisplayNames(IReadOnlyList<string> displayNames)
        {
            if (displayNames == null || displayNames.Count == 0)
            {
                return 0;
            }

            int added = 0;
            for (int i = 0; i < displayNames.Count; i++)
            {
                string n = displayNames[i]?.Trim();
                if (string.IsNullOrEmpty(n))
                {
                    continue;
                }

                ItemDefinitionEntry row = AddEntry();
                row.SetDisplayNameInternal(n);
                added++;
            }

            EnsureUniqueUuids();
            return added;
        }

        /// <summary>
        /// Remove a row by index. Returns false if out of range.
        /// </summary>
        public bool RemoveEntryAt(int index)
        {
            if (index < 0 || index >= entries.Count)
            {
                return false;
            }

            entries.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Assign a new UUID if missing or duplicate (editor repair).
        /// </summary>
        public void EnsureUniqueUuids()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                {
                    continue;
                }

                string u = e.ItemUuid;
                if (string.IsNullOrWhiteSpace(u) || seen.Contains(u))
                {
                    e.SetUuidInternal(NewUuid());
                    u = e.ItemUuid;
                }

                seen.Add(u);
            }
        }

        private static string NewUuid()
        {
            return Guid.NewGuid().ToString("N");
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            bool migrated = false;
            for (int i = 0; i < entries.Count; i++)
            {
                ItemDefinitionEntry row = entries[i];
                if (row != null && row.ConsumeLegacyTagBits())
                {
                    migrated = true;
                }
            }

            if (migrated)
            {
                EditorUtility.SetDirty(this);
            }
#endif
            EnsureUniqueUuids();
        }
    }
}
