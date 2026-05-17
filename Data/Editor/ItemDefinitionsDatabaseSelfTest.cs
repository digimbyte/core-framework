#if UNITY_EDITOR
using System;
using Core.Data.ItemDefinitions;
using UnityEditor;
using UnityEngine;

namespace Core.Data.ItemDefinitions.Editor
{
    /// <summary>
    /// Editor smoke checks for display-name uniqueness and lookups (no Unity Test Framework asmdef in this repo).
    /// </summary>
    internal static class ItemDefinitionsDatabaseSelfTest
    {
        [MenuItem("Core/Data/Item Definitions/Self-test database helpers")]
        private static void RunSelfTest()
        {
            int failures = 0;

            void fail(string message)
            {
                failures++;
                Debug.LogError("[ItemDefinitionsDatabaseSelfTest] " + message);
            }

            ItemDefinitionsDatabase db = ScriptableObject.CreateInstance<ItemDefinitionsDatabase>();

            try
            {
                ItemDefinitionEntry a = db.AddEntry();
                ItemDefinitionEntry b = db.AddEntry();
                if (!string.Equals(a.DisplayName, "New item", StringComparison.Ordinal))
                {
                    fail($"Expected first default name 'New item', got '{a.DisplayName}'.");
                }

                if (!string.Equals(b.DisplayName, "New item (2)", StringComparison.Ordinal))
                {
                    fail($"Expected second default name disambiguated to 'New item (2)', got '{b.DisplayName}'.");
                }

                if (db.GetEntryByDisplayName("NEW ITEM") != a)
                {
                    fail("GetEntryByDisplayName should be case-insensitive and return the first 'New item'.");
                }

                if (db.GetEntryByDisplayName("new item (2)") != b)
                {
                    fail("GetEntryByDisplayName should find 'New item (2)' case-insensitively.");
                }

                if (db.GetEntryByDisplayName("new item (2)") != b)
                {
                    fail("GetEntryByDisplayName should find 'New item (2)' case-insensitively.");
                }

                if (db.GetEntryByDisplayName("  ") != null)
                {
                    fail("Whitespace-only name query should return null.");
                }

                string uuid = a.ItemUuid;
                if (string.IsNullOrEmpty(uuid))
                {
                    fail("AddEntry row should have a non-empty ItemUuid.");
                }
                else if (db.GetEntryByUuid(uuid.ToUpperInvariant()) != a)
                {
                    fail("GetEntryByUuid should match case-insensitively.");
                }

                ItemDefinitionAssets assets = a.ToAssets();
                if (assets.ThumbnailTexture != null || assets.MainImageTexture != null ||
                    assets.MainPrefab != null || assets.SecondPrefab != null)
                {
                    fail("Fresh row ToAssets() should be all null.");
                }

                db.AppendEntriesFromDisplayNames(new[] { "Alpha", "alpha", "Beta" });
                int n = db.EntryCount;
                if (n != 5)
                {
                    fail($"Expected 5 entries after append (2 + 3), got {n}.");
                }

                ItemDefinitionEntry alpha2 = db.GetEntryByDisplayName("alpha (2)");
                if (alpha2 == null)
                {
                    fail("Third append 'alpha' should disambiguate to 'alpha (2)' and be findable.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(db);
            }

            if (failures == 0)
            {
                Debug.Log("[ItemDefinitionsDatabaseSelfTest] All checks passed.");
            }
        }
    }
}
#endif
