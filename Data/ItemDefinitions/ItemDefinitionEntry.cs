using System;
using System.Collections.Generic;
using Core.Enums;
using UnityEngine;
using UnityEngine.Serialization;

namespace Core.Data.ItemDefinitions
{
    /// <summary>
    /// One catalog row: visuals, prefabs, copy, tags, and a stable internal id (see <see cref="ItemDefinitionsDatabase"/>).
    /// </summary>
    [Serializable]
    public sealed class ItemDefinitionEntry
    {
        [SerializeField]
        private string itemUuid;

        [SerializeField]
        private string displayName = "New item";

        [SerializeField]
        [TextArea(2, 6)]
        private string description = string.Empty;

        [FormerlySerializedAs("tags")]
        [SerializeField, HideInInspector]
        private int legacyTagBits;

        [SerializeField]
        private List<SerialEnum> tagKeys = new List<SerialEnum>();

        [SerializeField]
        private Texture2D thumbnailTexture;

        [SerializeField]
        private Texture2D mainImageTexture;

        [SerializeField]
        private GameObject mainPrefab;

        [SerializeField]
        private GameObject secondPrefab;

        public string ItemUuid => itemUuid;
        public string DisplayName => displayName;
        public string Description => description;
        public IReadOnlyList<SerialEnum> Tags => tagKeys;
        public Texture2D ThumbnailTexture => thumbnailTexture;
        public Texture2D MainImageTexture => mainImageTexture;
        public GameObject MainPrefab => mainPrefab;
        public GameObject SecondPrefab => secondPrefab;

        internal void SetUuidInternal(string uuid) => itemUuid = uuid;

        internal void SetDisplayNameInternal(string value) => displayName = value;

        internal void SetDescriptionInternal(string value) => description = value;

        internal void SetTagsInternal(IReadOnlyList<SerialEnum> value) =>
            tagKeys = value == null ? new List<SerialEnum>() : new List<SerialEnum>(value);

        internal void SetThumbnailInternal(Texture2D value) => thumbnailTexture = value;

        internal void SetMainImageInternal(Texture2D value) => mainImageTexture = value;

        internal void SetMainPrefabInternal(GameObject value) => mainPrefab = value;

        internal void SetSecondPrefabInternal(GameObject value) => secondPrefab = value;

        /// <summary>
        /// Editor: one-time migration from the old bitfield tags. Returns true if data changed.
        /// </summary>
        internal bool ConsumeLegacyTagBits()
        {
            if (legacyTagBits == 0)
            {
                return false;
            }

            if (tagKeys == null)
            {
                tagKeys = new List<SerialEnum>();
            }

            if (tagKeys.Count > 0)
            {
                legacyTagBits = 0;
                return false;
            }

            MigrateLegacyTagBits();
            legacyTagBits = 0;
            return true;
        }

        /// <summary>
        /// Runtime-safe display name fallback when the name field is empty.
        /// </summary>
        public string ResolveDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            return string.IsNullOrEmpty(itemUuid) ? "Unnamed item" : itemUuid;
        }

        private void MigrateLegacyTagBits()
        {
            const string migrationGroup = "item.definition";
            int bits = legacyTagBits;
            void addIf(int mask, string name)
            {
                if ((bits & mask) == 0)
                {
                    return;
                }

                string full = StringEnumLibrary.BuildSerialEnumKey(migrationGroup, name);
                if (!string.IsNullOrEmpty(full))
                {
                    tagKeys.Add(new SerialEnum(full));
                }
            }

            addIf(1 << 0, "Weapon");
            addIf(1 << 1, "Tool");
            addIf(1 << 2, "Consumable");
            addIf(1 << 3, "Gear");
            addIf(1 << 4, "Quest");
            addIf(1 << 5, "Placeable");
            addIf(1 << 6, "Cosmetic");
            addIf(1 << 7, "Hidden");
        }
    }
}
