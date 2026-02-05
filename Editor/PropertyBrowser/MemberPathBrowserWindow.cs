#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// NOTE: This file is intentionally not in the Animator.Editor assembly.
// Animator tools use the implementation in:
// Assets/Core/Animator/Editor/PropertyBrowser/MemberPathBrowserWindow.cs

namespace Core.PropertyBrowser
{
    public struct NestedEntry
    {
        public string path;
        public string display;
        public string typeName;
    }

    public static class MemberPathBrowser
    {
        public static List<NestedEntry> CollectNestedMembers(Component root, int maxDepth)
        {
            var results = new List<NestedEntry>();
            if (root == null) return results;

            void Recurse(object owner, string prefix, int depth, HashSet<object> seen)
            {
                if (owner == null || depth > maxDepth) return;
                Type t = owner.GetType();

                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead
                                && p.PropertyType != typeof(Matrix4x4)
                                && !p.Name.Contains("Matrix"));
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => f.FieldType != typeof(Matrix4x4) && !f.Name.Contains("Matrix"));
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void));

                foreach (var p in props)
                {
                    string path = string.IsNullOrEmpty(prefix) ? p.Name : prefix + "." + p.Name;
                    object val = null;
                    Type propType = p.PropertyType;
                    Type recurseType = null;

                    try { val = p.GetValue(owner, null); }
                    catch { val = "<err>"; }

                    bool isRefReturn = propType.Name.EndsWith("&");
                    if (isRefReturn)
                    {
                        string baseTypeName = propType.Name.TrimEnd('&');
                        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        var targetAssemblies = new[]
                        {
                            propType.Assembly,
                            assemblies.FirstOrDefault(a => a.GetName().Name == "Nova")
                        }.Where(a => a != null).ToArray();

                        foreach (var asm in targetAssemblies)
                        {
                            recurseType = asm.GetType(propType.Namespace + "." + baseTypeName);
                            if (recurseType != null) break;
                        }
                    }
                    else if (!propType.IsPrimitive && propType != typeof(string))
                    {
                        recurseType = propType;
                    }

                    string valStr = val?.ToString() ?? "null";
                    string typeNameDisplay = propType.Name;
                    results.Add(new NestedEntry { path = path, display = $"{path} : {valStr} ({typeNameDisplay})", typeName = typeNameDisplay });

                    // For ref structs and complex types, enumerate their public properties (shallow)
                    if (recurseType != null && depth < maxDepth)
                    {
                        try
                        {
                            var nestedProps = recurseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                .Where(rp => rp.GetIndexParameters().Length == 0 && rp.CanRead && !rp.PropertyType.IsGenericType);

                            foreach (var nestedProp in nestedProps)
                            {
                                string nestedPath = path + "." + nestedProp.Name;
                                object nestedVal = null;
                                try
                                {
                                    if (!isRefReturn && val != null)
                                        nestedVal = nestedProp.GetValue(val);
                                }
                                catch { }

                                string nestedValStr = nestedVal?.ToString() ?? (isRefReturn ? "(ref)" : "null");
                                string nestedTypeName = nestedProp.PropertyType.Name;
                                results.Add(new NestedEntry { path = nestedPath, display = $"{nestedPath} : {nestedValStr} ({nestedTypeName})", typeName = nestedTypeName });
                            }
                        }
                        catch { }
                    }
                    else if (depth < maxDepth && !p.PropertyType.IsPrimitive && p.PropertyType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType))
                    {
                        if (val != null && !seen.Contains(val))
                        {
                            try
                            {
                                seen.Add(val);
                                Recurse(val, path, depth + 1, seen);
                            }
                            catch { }
                        }
                    }
                }

                foreach (var f in fields)
                {
                    string path = string.IsNullOrEmpty(prefix) ? f.Name : prefix + "." + f.Name;
                    object val = null;
                    try { val = f.GetValue(owner); } catch { val = "<err>"; }
                    string valStr = val?.ToString() ?? "null";
                    string typeName = f.FieldType.Name;
                    results.Add(new NestedEntry { path = path, display = $"{path} : {valStr} ({typeName})", typeName = typeName });

                    if (val != null && depth < maxDepth && !f.FieldType.IsPrimitive && f.FieldType != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                    {
                        if (!seen.Contains(val))
                        {
                            seen.Add(val);
                            Recurse(val, path, depth + 1, seen);
                        }
                    }
                }

                foreach (var m in methods)
                {
                    string methodPath = string.IsNullOrEmpty(prefix) ? m.Name : prefix + "." + m.Name;
                    results.Add(new NestedEntry { path = methodPath, display = $"{methodPath} () (Method)", typeName = "Void" });
                }
            }

            Recurse(root, string.Empty, 0, new HashSet<object>());
            return results;
        }

        public static Type ResolveMemberType(object root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            Type currentType = root.GetType();

            foreach (var segment in path.Split('.'))
            {
                if (string.IsNullOrEmpty(segment)) return null;

                var pi = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null)
                {
                    currentType = pi.PropertyType;

                    if (currentType.Name.EndsWith("&"))
                    {
                        string baseTypeName = currentType.Name.TrimEnd('&');
                        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        var targetAssemblies = new[]
                        {
                            currentType.Assembly,
                            assemblies.FirstOrDefault(a => a.GetName().Name == "Nova")
                        }.Where(a => a != null).ToArray();

                        foreach (var asm in targetAssemblies)
                        {
                            var refType = asm.GetType(currentType.Namespace + "." + baseTypeName);
                            if (refType != null)
                            {
                                currentType = refType;
                                break;
                            }
                        }
                    }
                    continue;
                }

                var fi = currentType.GetField(segment, BindingFlags.Public | BindingFlags.Instance);
                if (fi != null)
                {
                    currentType = fi.FieldType;
                    continue;
                }

                var mi = currentType.GetMethod(segment, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (mi != null && mi.GetParameters().Length == 0 && mi.ReturnType == typeof(void))
                {
                    return typeof(void);
                }

                return null;
            }

            return currentType;
        }
    }

    public sealed class MemberPathBrowserWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchText = "";
        private string lastSearchText = null;
        private List<NestedEntry> allEntries = new List<NestedEntry>();
        private List<NestedEntry> cachedFiltered = new List<NestedEntry>();
        private Action<NestedEntry> onSelected;

        public static void Show(Component component, int maxDepth, Action<NestedEntry> onSelect)
        {
            if (component == null) return;

            var window = GetWindow<MemberPathBrowserWindow>("Select Property");
            window.allEntries = MemberPathBrowser.CollectNestedMembers(component, maxDepth);
            window.cachedFiltered = new List<NestedEntry>(window.allEntries);
            window.onSelected = onSelect;
            window.minSize = new Vector2(520, 340);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Search Properties", EditorStyles.boldLabel);
            searchText = EditorGUILayout.TextField("Search:", searchText);
            EditorGUILayout.Space();

            if (searchText != lastSearchText)
            {
                lastSearchText = searchText;
                cachedFiltered.Clear();
                foreach (var p in allEntries)
                {
                    if (string.IsNullOrEmpty(searchText) || p.path.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        cachedFiltered.Add(p);
                }
                cachedFiltered.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));
            }

            EditorGUILayout.LabelField($"Found: {cachedFiltered.Count} entries");
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < cachedFiltered.Count; i++)
            {
                var entry = cachedFiltered[i];
                Rect rowRect = EditorGUILayout.BeginHorizontal();

                bool isHovered = rowRect.Contains(Event.current.mousePosition);
                if (isHovered)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.5f, 0.8f, 0.3f));
                    Repaint();
                }

                EditorGUILayout.LabelField(entry.display, GUILayout.ExpandWidth(true));

                if (isHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    onSelected?.Invoke(entry);
                    Close();
                    Event.current.Use();
                }

                if (GUILayout.Button("Select", GUILayout.Width(80)))
                {
                    onSelected?.Invoke(entry);
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
