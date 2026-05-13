using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Enums
{
    /// <summary>
    /// Single asset that holds all string-based enum groups.
    /// Each group is key:[values], e.g. key = "ammo", values = ["small", "medium"].
    /// Generated constants are of the form key.value, e.g. ammo.small == "ammo.small".
    /// </summary>
    [CreateAssetMenu(fileName = "StringEnums", menuName = "Core/Data/String Enum Library")]
    public class StringEnumLibrary : ScriptableObject
    {
        [Serializable]
        public class EnumGroup
        {
            [Tooltip("Group key / namespace, e.g. 'ammo' or 'item.rarity'")]
            public string key;

            [Tooltip("Values for this group, e.g. 'small', 'medium', 'large'")]
            public List<string> values = new List<string>();
        }

        [SerializeField]
        public List<EnumGroup> groups = new List<EnumGroup>();

        /// <summary>
        /// Same normalization as generated constants: trim and replace spaces with underscores.
        /// </summary>
        public static string NormalizeKeyPart(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Trim().Replace(' ', '_');
        }

        /// <summary>
        /// Builds the string key used by <see cref="SerialEnum"/> for a group value, matching the code generator.
        /// </summary>
        public static string BuildSerialEnumKey(string groupKey, string value)
        {
            string prefix = NormalizeKeyPart(groupKey);
            string suffix = NormalizeKeyPart(value);
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
            {
                return string.Empty;
            }

            return prefix + "." + suffix;
        }

        /// <summary>
        /// All <see cref="SerialEnum"/> values for the first group whose key matches (trimmed, case-insensitive).
        /// </summary>
        public List<SerialEnum> GetSerialEnumsForGroup(string groupKey)
        {
            var result = new List<SerialEnum>();
            if (groups == null || string.IsNullOrWhiteSpace(groupKey))
            {
                return result;
            }

            string wanted = groupKey.Trim();
            for (int i = 0; i < groups.Count; i++)
            {
                EnumGroup g = groups[i];
                if (g == null || string.IsNullOrWhiteSpace(g.key))
                {
                    continue;
                }

                if (!string.Equals(g.key.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (g.values == null)
                {
                    break;
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string raw in g.values)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    string v = raw.Trim();
                    if (!seen.Add(v))
                    {
                        continue;
                    }

                    string full = BuildSerialEnumKey(g.key, v);
                    if (!string.IsNullOrEmpty(full))
                    {
                        result.Add(new SerialEnum(full));
                    }
                }

                break;
            }

            return result;
        }

#if UNITY_EDITOR
        // Snapshot of the last generated state, used to know if enums changed since last Save/Generate.
        [SerializeField, HideInInspector]
        public string lastGeneratedSnapshot;

        private const string DefaultOutputPath = "Assets/Core/Data/StringEnums.generated.cs";
        private const string DefaultNamespace = "Core.Enums";

        /// <summary>
        /// Manually regenerate the C# constants for this library.
        /// Right-click the asset and choose this from the context menu.
        /// </summary>
        [ContextMenu("Regenerate String Enum Constants")]
        public void Regenerate()
        {
            StringEnumCodeGenerator.GenerateAndStamp(this, DefaultOutputPath, DefaultNamespace);
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only code generator that turns the StringEnumLibrary into C# constants.
    /// </summary>
    public static class StringEnumCodeGenerator
    {
        /// <summary>
        /// Generate code and update the library's snapshot so we know it's in-sync.
        /// </summary>
        public static void GenerateAndStamp(StringEnumLibrary library, string outputPath, string namespaceName)
        {
            if (library == null) return;

            string snapshot = BuildSnapshot(library);
            GenerateInternal(library, outputPath, namespaceName);

            library.lastGeneratedSnapshot = snapshot;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Build a compact string representation of all prefixes and values.
        /// Used only to detect whether there are unsaved enum changes.
        /// </summary>
        public static string BuildSnapshot(StringEnumLibrary library)
        {
            if (library == null || library.groups == null)
                return string.Empty;

            var parts = new List<string>();

            foreach (var group in library.groups)
            {
                if (group == null) continue;
                if (string.IsNullOrWhiteSpace(group.key)) continue;

                string key = group.key.Trim();
                var vals = (group.values ?? new List<string>())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .OrderBy(v => v, StringComparer.Ordinal);

                parts.Add(key + ":" + string.Join(",", vals));
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        private static void GenerateInternal(StringEnumLibrary library, string outputPath, string namespaceName)
        {
            var sb = new StringBuilder();
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string assetPath = AssetDatabase.GetAssetPath(library);

            sb.AppendLine("// AUTO-GENERATED BY StringEnumLibrary - DO NOT EDIT BY HAND");
            sb.AppendLine($"// Source asset: {assetPath}");
            sb.AppendLine($"// Generated at: {time}");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");

            if (library.groups != null)
            {
                foreach (var group in library.groups)
                {
                    if (group == null) continue;
                    if (string.IsNullOrWhiteSpace(group.key)) continue;

                    string rawPrefix = group.key.Trim();
                    string safePrefix = NormalizeKey(rawPrefix); // spaces -> underscore, etc.
                    string className  = SanitizeIdentifier(safePrefix);
                    if (string.IsNullOrEmpty(className))
                        continue;

                    var valueList = (group.values ?? new List<string>())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(v => v, StringComparer.Ordinal)
                        .ToList();

                    if (valueList.Count == 0)
                        continue;

                    sb.AppendLine($"    public static class {className}");
                    sb.AppendLine("    {");

                    foreach (var value in valueList)
                    {
                        string safeValue  = NormalizeKey(value);      // spaces -> underscore
                        string fieldName  = SanitizeIdentifier(safeValue);
                        if (string.IsNullOrEmpty(fieldName))
                            continue;

                        string fullKey = safePrefix + "." + safeValue;
                        sb.AppendLine($"        public static readonly SerialEnum {fieldName} = new SerialEnum(\"{fullKey}\");");
                    }

                    sb.AppendLine("    }");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.Refresh();
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Convert arbitrary text into a C# identifier: letters/digits/underscore only.
            // We keep case but replace anything else (including spaces and '.') with '_'.
            var chars = name.Select(c =>
                char.IsLetterOrDigit(c) ? c :
                c == '_' ? '_' :
                '_').ToArray();

            if (chars.Length == 0)
                return null;

            // Cannot start with a digit.
            if (char.IsDigit(chars[0]))
            {
                return "_" + new string(chars);
            }

            return new string(chars);
        }

        /// <summary>
        /// Normalize user-entered keys/values into key-safe strings.
        /// Currently just trims and replaces spaces with underscores.
        /// </summary>
        private static string NormalizeKey(string text)
        {
            return StringEnumLibrary.NormalizeKeyPart(text);
        }
    }
#endif
}
