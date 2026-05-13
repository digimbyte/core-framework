using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Core.Framework.Editor
{
    /// <summary>
    /// Hierarchy context menu: reorder direct children of the selected object(s) by name.
    /// </summary>
    public static class HierarchyChildSortMenu
    {
        private const string MenuRoot = "GameObject/Sort Children/";

        [MenuItem(MenuRoot + "Sort A-Z", false, 50)]
        private static void SortChildrenAscending()
        {
            SortSelectedChildren(StringComparer.OrdinalIgnoreCase, ascending: true);
        }

        [MenuItem(MenuRoot + "Sort Z-A", false, 51)]
        private static void SortChildrenDescending()
        {
            SortSelectedChildren(StringComparer.OrdinalIgnoreCase, ascending: false);
        }

        [MenuItem(MenuRoot + "Sort A-Z", true)]
        [MenuItem(MenuRoot + "Sort Z-A", true)]
        private static bool ValidateSortChildren()
        {
            return Selection.transforms.Length > 0 && AnySelectionHasChildren();
        }

        private static bool AnySelectionHasChildren()
        {
            foreach (Transform transform in Selection.transforms)
            {
                if (transform != null && transform.childCount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SortSelectedChildren(StringComparer comparer, bool ascending)
        {
            string undoLabel = ascending ? "Sort children A-Z" : "Sort children Z-A";

            foreach (Transform parent in Selection.transforms)
            {
                if (parent == null || parent.childCount == 0)
                {
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, undoLabel);

                var children = new List<Transform>(parent.childCount);
                for (int i = 0; i < parent.childCount; i++)
                {
                    children.Add(parent.GetChild(i));
                }

                if (ascending)
                {
                    children.Sort((a, b) => comparer.Compare(a.name, b.name));
                }
                else
                {
                    children.Sort((a, b) => comparer.Compare(b.name, a.name));
                }

                for (int i = 0; i < children.Count; i++)
                {
                    children[i].SetSiblingIndex(i);
                }
            }
        }
    }
}
