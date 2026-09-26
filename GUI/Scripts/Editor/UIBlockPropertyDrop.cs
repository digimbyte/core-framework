using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aura.Editor.GUIs
{
    // Drops are collected while drawing and applied afterward, so local control values
    // cannot overwrite a copied value and a field drop cannot trigger unrelated controls.
    internal static class UIBlockPropertyDrop
    {
        private static UIBlock source;
        private static SerializedObject destination;
        private static string[] paths;
        private static bool group;
        private static int bitMask;

        public static bool IsObjectDrag
        {
            get
            {
                Event evt = Event.current;
                if (evt == null || (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)) return false;
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                    if (obj is GameObject || obj is Component) return true;
                return false;
            }
        }

        public static void Begin()
        {
            source = null;
            destination = null;
            paths = null;
        }

        public static GUIContent BeginProperty(Rect rect, GUIContent label, SerializedProperty property)
        {
            Field(rect, property);
            return EditorGUI.BeginProperty(rect, label, property);
        }

        public static void Field(Rect rect, SerializedProperty property, int mask = -1)
        {
            Handle(rect, false, mask, property);
        }

        public static void Group(Rect rect, params SerializedProperty[] properties)
        {
            Handle(rect, true, -1, properties);
        }

        public static void Fields(Rect rect, params SerializedProperty[] properties)
        {
            Handle(rect, false, -1, properties);
        }

        public static void LengthField(Rect rect, SerializedProperty length, float typeWidth, SerializedProperty inversions, int mask)
        {
            if (inversions != null)
            {
                Rect inversionRect = rect;
                inversionRect.x += Mathf.Max(AuraGUI.MinFloatFieldWidth, rect.width - typeWidth - 16);
                inversionRect.width = 16;
                Field(inversionRect, inversions, mask);
            }
            Field(rect, length);
        }

        private static void Handle(Rect rect, bool wholeGroup, int mask, params SerializedProperty[] properties)
        {
            if (!IsObjectDrag || !rect.Contains(Event.current.mousePosition) || properties == null || properties.Length == 0) return;
            if (properties[0] == null || !(properties[0].serializedObject.targetObject is UIBlock)) return;

            UIBlock candidate = null;
            if (DragAndDrop.objectReferences.Length == 1)
            {
                UnityEngine.Object obj = DragAndDrop.objectReferences[0];
                candidate = obj as UIBlock;
                if (obj is GameObject go)
                {
                    UIBlock[] blocks = go.GetComponents<UIBlock>();
                    if (blocks.Length == 1) candidate = blocks[0];
                }
            }

            bool valid = GUI.enabled && candidate != null;
            SerializedObject targetObject = properties[0].serializedObject;
            if (valid)
            {
                using (var sourceObject = new SerializedObject(candidate))
                {
                    foreach (UnityEngine.Object target in targetObject.targetObjects)
                    {
                        if (target == candidate || (target.hideFlags & HideFlags.NotEditable) != 0)
                        {
                            valid = false;
                            break;
                        }
                        foreach (SerializedProperty property in properties)
                        {
                            if (property == null || !property.editable ||
                                sourceObject.FindProperty(property.propertyPath) == null ||
                                PropertyType(candidate.GetType(), property.propertyPath) == null ||
                                PropertyType(candidate.GetType(), property.propertyPath) != PropertyType(target.GetType(), property.propertyPath))
                            {
                                valid = false;
                                break;
                            }
                        }
                        // Body/Visuals are composite sections, not the whole visuals storage.
                        // Require the same section shape rather than silently copying a subset.
                        if (wholeGroup && (properties.Length > 1 || properties[0].propertyPath == "visuals.Color"))
                        {
                            foreach (SerializedProperty property in properties)
                                if (property != null && property.propertyPath.StartsWith("visuals.") &&
                                    PropertyType(candidate.GetType(), "visuals") != PropertyType(target.GetType(), "visuals"))
                                    valid = false;
                        }
                        if (!valid) break;
                    }
                }
            }

            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (valid && Event.current.type == EventType.DragPerform)
            {
                source = candidate;
                destination = targetObject;
                paths = new string[properties.Length];
                for (int i = 0; i < properties.Length; ++i) paths[i] = properties[i].propertyPath;
                group = wholeGroup;
                bitMask = mask;
                DragAndDrop.AcceptDrag();
            }
            Event.current.Use();
        }

        private static Type PropertyType(Type type, string path)
        {
            foreach (string part in path.Split('.'))
            {
                FieldInfo field = null;
                for (Type declaring = type; declaring != null && field == null; declaring = declaring.BaseType)
                    field = declaring.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) type = field.FieldType;
                else
                {
                    // Unity serializes native value-type members such as Vector2Int.x
                    // by their public name, although their managed backing fields differ.
                    PropertyInfo property = type.GetProperty(part, BindingFlags.Instance | BindingFlags.Public);
                    if (property == null) return null;
                    type = property.PropertyType;
                }
            }
            return type;
        }

        public static bool Apply(SerializedObject inspector)
        {
            if (destination != inspector || source == null || paths == null) return false;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Copy UIBlock " + (group ? "group" : "property"));
            inspector.ApplyModifiedProperties();
            using (var sourceObject = new SerializedObject(source))
            {
                foreach (UnityEngine.Object target in inspector.targetObjects)
                {
                    using (var targetObject = new SerializedObject(target))
                    {
                        Axis oldPrimary = (Axis)targetObject.FindProperty("autoLayout.Axis").intValue;
                        Axis oldCross = (Axis)targetObject.FindProperty("autoLayout.Cross.Axis").intValue;
                        foreach (string path in paths)
                        {
                            SerializedProperty from = sourceObject.FindProperty(path);
                            SerializedProperty to = targetObject.FindProperty(path);
                            if (bitMask >= 0)
                                to.intValue = (to.intValue & ~bitMask) | (from.intValue & bitMask);
                            else
                                targetObject.CopyFromSerializedProperty(from);
                            EnableSourceSections(sourceObject, targetObject, path);
                            if (!group) FixFieldDependencies(sourceObject, targetObject, path);
                        }
                        AuraLayoutEditors.ApplyCopiedAutoLayoutChanges(targetObject, oldPrimary, oldCross,
                            !group && paths.Length == 1 && paths[0] == "autoLayout.Axis");
                        targetObject.ApplyModifiedProperties();
                    }
                }
            }
            Undo.CollapseUndoOperations(undoGroup);
            inspector.Update();
            Begin();
            return true;
        }

        private static void EnableSourceSections(SerializedObject from, SerializedObject to, string path)
        {
            // Only ancestors are enabled. Copying Enabled itself must preserve false.
            int dot = path.LastIndexOf('.');
            while (dot >= 0)
            {
                string parent = path.Substring(0, dot);
                string enabledPath = parent + ".Enabled";
                if (parent == "autoLayout" || parent == "autoLayout.Cross") enabledPath = parent + ".Axis";
                if (path != enabledPath) Enable(from, to, enabledPath);
                dot = parent.LastIndexOf('.');
            }
            if (path == "visuals.Color" || path.StartsWith("visuals.Gradient") ||
                path.StartsWith("visuals.Image") || path == "texture" || path == "sprite")
                Enable(from, to, "visuals.FillEnabled");
        }

        private static void Enable(SerializedObject from, SerializedObject to, string path)
        {
            SerializedProperty sourceProperty = from.FindProperty(path);
            SerializedProperty targetProperty = to.FindProperty(path);
            if (sourceProperty == null || targetProperty == null) return;
            if (sourceProperty.propertyType == SerializedPropertyType.Boolean)
            {
                if (sourceProperty.boolValue) targetProperty.boolValue = true;
            }
            else if (sourceProperty.intValue != 0 && targetProperty.intValue == 0)
                targetProperty.intValue = sourceProperty.intValue;
        }

        private static void FixFieldDependencies(SerializedObject from, SerializedObject to, string path)
        {
            if (path == "visuals.CornerRadius")
            {
                SerializedProperty master = to.FindProperty(path);
                foreach (string corner in new[] { "TopLeft", "TopRight", "BottomRight", "BottomLeft" })
                    to.FindProperty("visuals.CornerRadii." + corner).boxedValue = master.boxedValue;
                to.FindProperty("visuals.UseIndividualCornerRadii").boolValue = false;
            }
            else if (path == "visuals.CornerRadii" || path.StartsWith("visuals.CornerRadii."))
            {
                object master = to.FindProperty("visuals.CornerRadius").boxedValue;
                bool individual = false;
                foreach (string corner in new[] { "TopLeft", "TopRight", "BottomRight", "BottomLeft" })
                    individual |= !Equals(to.FindProperty("visuals.CornerRadii." + corner).boxedValue, master);
                to.FindProperty("visuals.UseIndividualCornerRadii").boolValue = individual;
            }
            else if (path == "layout.AspectRatioAxis")
            {
                // The ratio is the value governed by the axis lock, not another style field.
                to.CopyFromSerializedProperty(from.FindProperty("layout.AspectRatio"));
            }
            else if (path == "layout.AutoSize" || path.StartsWith("layout.AutoSize."))
            {
                foreach (string axis in new[] { "X", "Y", "Z" })
                {
                    if (path != "layout.AutoSize" && path != "layout.AutoSize." + axis) continue;
                    var length = new Serialization.Wrappers._Length { SerializedProperty = to.FindProperty("layout.Size." + axis) };
                    AuraLayoutEditors.ApplyAutosizeTypeChanges(length, (AutoSize)to.FindProperty("layout.AutoSize." + axis).intValue);
                }
            }
        }
    }
}
