#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Core.Animator
{
    // Custom layout for a single tween entry.
    // Keeps the inspector top-down: target/type -> custom property -> values -> options -> timing.
    // Also keeps expensive reflection listing behind an explicit Browse button.
    public sealed class TweenEntryOdinDrawer : OdinValueDrawer<Animate.TweenEntry>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            // List index: Odin's Property.Index is usually correct; if not, parse configuredTweens.Array.data[i] from the Unity property path
            // so serialized propertyName / detectedPropertyType writes in Browse & Refresh work reliably.
            int index = GetTweenListIndexForProperty(Property);

            var nameProp = Find("name");
            var typeProp = Find("type");
            var targetObjectProp = Find("targetObject");
            var targetComponentProp = Find("targetComponent");

            var propertyNameProp = Find("propertyName");
            var detectedTypeProp = Find("detectedPropertyType");
            var propertyModeProp = Find("propertyMode");
            var methodInvokeTimingProp = Find("methodInvokeTiming");

            var startSourceProp = Find("startSource");
            var localProp = Find("local");

            var fromVec3Prop = Find("fromVec3");
            var toVec3Prop = Find("toVec3");
            var fromFloatProp = Find("fromFloat");
            var toFloatProp = Find("toFloat");
            var fromColorProp = Find("fromColor");
            var toColorProp = Find("toColor");
            var fromBoolProp = Find("fromBool");
            var toBoolProp = Find("toBool");
            var fromStringProp = Find("fromString");
            var toStringProp = Find("toString");
            var typerAppendProp = Find("typerAppend");

            var vectorMaskProp = Find("vectorMask");
            var enumFieldMaskProp = Find("enumFieldMask");
            var materialIndexProp = Find("materialIndex");
            var materialPropertyProp = Find("materialProperty");
            var materialColorPropertiesProp = Find("materialColorProperties");

            var delayModeProp = Find("delayMode");
            var delayValueProp = Find("delayValue");
            var durationProp = Find("duration");
            var curveProp = Find("curve");

            var chainProp = Find("chainAfterPrevious");
            var siblingShiftProp = Find("siblingShift");
            var siblingTimingProp = Find("siblingTiming");
            var debugLoggingProp = Find("debugLogging");

            var tweenType = GetEnum<Animate.TweenType>(typeProp, Animate.TweenType.Position);
            bool isCustom = tweenType == Animate.TweenType.CustomProperty;
            bool isRendererColor = tweenType == Animate.TweenType.RendererColor;
            bool isMaterialFloat = tweenType == Animate.TweenType.MaterialFloat;
            bool isCanvasAlpha = tweenType == Animate.TweenType.CanvasGroupAlpha;
            bool isSiblingOrder = tweenType == Animate.TweenType.SiblingOrder;

            string det = GetResolvedDetectedTypeString(detectedTypeProp, index);
            var mode = GetEnum<Animate.CustomPropertyMode>(propertyModeProp, Animate.CustomPropertyMode.AutoTween);
            var invokeTiming = GetEnum<Animate.MethodInvokeTiming>(methodInvokeTimingProp, Animate.MethodInvokeTiming.OnEnd);

            // Determine chained (and force off for index 0)
            bool chained = GetBool(chainProp);
            if (index == 0 && chained)
            {
                SetBool(chainProp, false);
                chained = false;
            }

            using (new ChainedIndentScope(chained))
            {
                bool expanded = DrawHeaderWithFoldout(nameProp);

                // Chain is positional (previous item in list). Put it at the top so it reads as context/priority.
                DrawChainRow(chainProp, index);

                DrawActionRow(index);

                if (!expanded)
                {
                    return;
                }

                DrawSection("Target", () =>
                {
                    DrawIf(typeProp);
                    DrawIf(targetObjectProp);
                    if (isCustom) DrawIf(targetComponentProp);
                });

                if (isCustom)
                {
                    DrawSection("Custom Property", () =>
                    {
                        DrawIf(propertyNameProp, "Property");
                        DrawBrowseRefreshRow(index, targetObjectProp, targetComponentProp, propertyNameProp, detectedTypeProp);

                        DrawDetectedTypeDisplayOnly(detectedTypeProp, index);

                        // propertyMode is not meaningful for strings; methods use methodInvokeTiming.
                        // Booleans show Property Mode in Values (with Start/End) for trigger/threshold curve behaviour.
                        if (!string.Equals(det, "Void", StringComparison.Ordinal) && !string.Equals(det, "String", StringComparison.Ordinal) && !IsBooleanDetectedType(det))
                        {
                            DrawIf(propertyModeProp, "Property Mode");
                        }

                        if (string.Equals(det, "Void", StringComparison.Ordinal))
                        {
                            DrawIf(methodInvokeTimingProp, "Invoke Timing");
                        }
                    });
                }

                DrawSection("Values", () =>
                {
                    Component comp = null;
                    try { comp = targetComponentProp?.ValueEntry?.WeakSmartValue as Component; } catch { }
                    bool isTyper = isCustom && comp is Typer;

                    // Start source is relevant only for continuous value tweens.
                    bool startSourceRelevant =
                        (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale || isCanvasAlpha || isRendererColor || isMaterialFloat)
                        || (isCustom && !isTyper && det != "Void" && det != "String" && !IsBooleanDetectedType(det));
                    startSourceRelevant = startSourceRelevant && !isSiblingOrder;

                    if (startSourceRelevant)
                    {
                        DrawIf(startSourceProp, "Initial Value");
                    }

                    // Local flag for world/local position/rotation modes
                    if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler)
                    {
                        DrawIf(localProp, "Local");
                    }

                    if (tweenType == Animate.TweenType.SiblingOrder)
                    {
                        DrawIf(propertyModeProp, "Property Mode");
                        DrawIf(siblingShiftProp, "Shift Direction");
                        if (mode == Animate.CustomPropertyMode.ToggleAtHalf)
                        {
                            DrawIf(fromFloatProp, "Start (order delta)");
                            DrawIf(toFloatProp, "End (order delta)");
                        }
                        else
                        {
                            DrawIf(toFloatProp, "Order delta");
                        }
                    }
                    // Value fields are conditional based on tween type / detected type
                    else if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale)
                    {
                        DrawIf(fromVec3Prop, "Start Value");
                        DrawIf(toVec3Prop, "End Value");
                    }
                    else if (isCanvasAlpha || isMaterialFloat)
                    {
                        DrawIf(fromFloatProp, "Start Value");
                        DrawIf(toFloatProp, "End Value");
                    }
                    else if (isRendererColor)
                    {
                        DrawIf(fromColorProp, "Start Color");
                        DrawIf(toColorProp, "End Color");
                    }
                    else if (isCustom)
                    {
                        // Typer: explicit UI when selecting StartTyping.
                        // NOTE: toString has [ShowIf(UsesString)] on the data model, which is false for methods (Void).
                        // So we draw the value manually here.
                        if (isTyper)
                        {
                            string selectedPath = null;
                            try { selectedPath = propertyNameProp?.ValueEntry?.WeakSmartValue as string; } catch { }

                            if (string.Equals(selectedPath, "StartTyping", StringComparison.Ordinal))
                            {
                                DrawWrappedTextArea(toStringProp, "Text", minLines: 3f);
                                DrawIf(typerAppendProp, "Append");
                            }
                        }
                        else if (det == "Color")
                        {
                            DrawIf(fromColorProp, "Start Color");
                            DrawIf(toColorProp, "End Color");
                        }
                        else if (IsBooleanDetectedType(det))
                        {
                            // SetAtEnd: only toBool is applied. ToggleAtHalf: from at 0, to at 0.5*duration. AutoTween: from/to = states either side of curve thresholds.
                            DrawIf(propertyModeProp, "Property Mode");
                            if (mode == Animate.CustomPropertyMode.SetAtEnd)
                            {
                                DrawIf(toBoolProp, "Set to (when duration elapses)");
                            }
                            else if (mode == Animate.CustomPropertyMode.ToggleAtHalf)
                            {
                                DrawIf(fromBoolProp, "At t = 0");
                                DrawIf(toBoolProp, "At t = 50% of duration");
                            }
                            else
                            {
                                DrawIf(fromBoolProp, "Value when curve is low (start side)");
                                DrawIf(toBoolProp, "Value when curve is high (end side)");
                            }
                        }
                        else if (det == "String")
                        {
                            // Strings are set at end; no meaningful start value.
                            // Draw manually to ensure long lines word-wrap and don't blow out the inspector width.
                            DrawWrappedTextArea(toStringProp, "End Value", minLines: 3f);
                        }
                        else if (det == "Single" || det == "Double" || det == "Int32")
                        {
                            DrawIf(fromFloatProp, "Start Value");
                            DrawIf(toFloatProp, "End Value");
                        }
                        else if (!string.IsNullOrEmpty(det) && det != "Void")
                        {
                            // Vector3, Vector4, Quaternion, enum-struct helper, etc.
                            DrawIf(fromVec3Prop, "Start Value");
                            DrawIf(toVec3Prop, "End Value");
                        }
                    }
                });

                DrawSection("Options", () =>
                {
                    if (isCustom)
                    {
                        if (!string.IsNullOrEmpty(det) && det != "Void" && det != "Color" && !IsBooleanDetectedType(det) && det != "Single" && det != "Double" && det != "Int32")
                        {
                            // Vector-ish & enum-struct helpers
                            DrawIf(vectorMaskProp, "Component Mask");

                            // Only show enum-field mask for the enum-struct case.
                            if (det != "Vector3" && det != "Vector4" && det != "Quaternion")
                                DrawIf(enumFieldMaskProp, "Enum Field Mask");
                        }
                        else if (det == "Vector3" || det == "Vector4" || det == "Quaternion")
                        {
                            DrawIf(vectorMaskProp, "Component Mask");
                        }
                    }

                    if (isRendererColor)
                    {
                        DrawIf(materialIndexProp, "Material Index");
                        DrawIf(materialPropertyProp, "Material Property");
                        DrawIf(materialColorPropertiesProp, "Extra Color Properties");
                    }
                    else if (isMaterialFloat)
                    {
                        DrawIf(materialPropertyProp, "Material Property");
                    }
                });

                DrawSection("Timing", () =>
                {
                    DrawIf(delayModeProp, "Delay Mode");

                    var delayMode = GetEnum<Animate.DelayMode>(delayModeProp, Animate.DelayMode.None);
                    if (delayMode == Animate.DelayMode.Frames)
                        DrawIf(delayValueProp, "Delay (frames)");
                    else if (delayMode == Animate.DelayMode.Seconds)
                        DrawIf(delayValueProp, "Delay (s)");

                    var siblingInvoke = GetEnum<Animate.MethodInvokeTiming>(siblingTimingProp, Animate.MethodInvokeTiming.OnEnd);

                    if (tweenType == Animate.TweenType.SiblingOrder && mode == Animate.CustomPropertyMode.AutoTween)
                        DrawIf(siblingTimingProp, "Apply Timing");

                    // For void methods invoked OnStart, duration is meaningless. Sibling/Auto/OnStart same idea.
                    bool durationRelevant = !(isCustom && string.Equals(det, "Void", StringComparison.Ordinal) && invokeTiming == Animate.MethodInvokeTiming.OnStart);
                    if (tweenType == Animate.TweenType.SiblingOrder)
                    {
                        if (mode == Animate.CustomPropertyMode.AutoTween && siblingInvoke == Animate.MethodInvokeTiming.OnStart)
                            durationRelevant = false;
                    }
                    if (durationRelevant)
                    {
                        DrawIf(durationProp, "Duration (s)");
                    }

                    // Curve isn't applicable for strings (set-at-end).
                    bool curveRelevant = !string.Equals(det, "String", StringComparison.Ordinal) && UsesCurve(tweenType, det, mode, invokeTiming, siblingInvoke);
                    if (curveRelevant)
                    {
                        DrawIf(curveProp, "Curve");
                    }

                    DrawIf(debugLoggingProp, "Debug Logging");
                });

            }
        }

        private InspectorProperty Find(string name) => Property.FindChild(p => p != null && p.Name == name, true);

        private static void DrawSection(string title, Action draw)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            draw?.Invoke();
            EditorGUILayout.EndVertical();
        }

        private bool DrawHeaderWithFoldout(InspectorProperty nameProp)
        {
            // Persist expanded state per entry without relying on Odin Context APIs (version differences).
            var animate = GetAnimateTarget();
            int ownerId = animate != null ? animate.GetInstanceID() : 0;
            string key = $"Animate.TweenEntry.Expanded.{ownerId}.{Property.Path}";

            bool expanded = SessionState.GetBool(key, true);

            EditorGUILayout.BeginHorizontal();
            {
                var foldoutRect = GUILayoutUtility.GetRect(14f, EditorGUIUtility.singleLineHeight, GUILayout.Width(14f));
                bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                if (newExpanded != expanded)
                {
                    expanded = newExpanded;
                    SessionState.SetBool(key, expanded);
                }

                if (nameProp != null)
                {
                    // Draw name on the same row
                    nameProp.Draw(GUIContent.none);
                }
            }
            EditorGUILayout.EndHorizontal();

            return expanded;
        }

        private void DrawActionRow(int index)
        {
            var animate = GetAnimateTarget();

            EditorGUILayout.BeginHorizontal();
            {
                GUI.enabled = animate != null;

                if (GUILayout.Button("Play", GUILayout.Width(60)))
                {
                    animate?.PlayByIndex(index);
                }

                if (GUILayout.Button("Clone", GUILayout.Width(70)))
                {
                    animate?.CloneConfiguredTween(index);
                    GUIHelper.RequestRepaint();
                }

                GUI.enabled = animate != null && index >= 0;
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    animate?.RemoveConfiguredTween(index);
                    GUIHelper.RequestRepaint();
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private Animate GetAnimateTarget()
        {
            try
            {
                // Most reliable: UnitySerializedObject target for this inspector
                var uso = Property?.Tree?.UnitySerializedObject;
                if (uso != null)
                    return uso.targetObject as Animate;
            }
            catch { }

            try
            {
                return Property?.SerializationRoot?.ValueEntry?.WeakSmartValue as Animate;
            }
            catch { }

            return null;
        }

        private static void DrawIf(InspectorProperty prop, string labelOverride = null)
        {
            if (prop == null) return;
            if (string.IsNullOrEmpty(labelOverride))
                prop.Draw();
            else
                prop.Draw(new GUIContent(labelOverride));
        }

        private static void DrawWrappedTextArea(InspectorProperty prop, string label, float minLines = 3f)
        {
            if (prop?.ValueEntry == null) return;

            string current = string.Empty;
            try { current = prop.ValueEntry.WeakSmartValue as string ?? string.Empty; } catch { }

            EditorGUILayout.LabelField(label);

            // Word-wrap only affects display. The stored string is unchanged.
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            float minHeight = EditorGUIUtility.singleLineHeight * Mathf.Max(1f, minLines);
            string next = EditorGUILayout.TextArea(current, style, GUILayout.MinHeight(minHeight), GUILayout.ExpandWidth(true));

            if (!string.Equals(next, current, StringComparison.Ordinal))
            {
                try { prop.ValueEntry.WeakSmartValue = next; } catch { }
            }
        }

        private static string GetString(InspectorProperty prop)
        {
            try
            {
                return prop?.ValueEntry?.WeakSmartValue as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Prefer Unity serialized string (authoritative) over Odin’s value entry so display matches Browse/Refresh.</summary>
        private static string GetResolvedDetectedTypeString(InspectorProperty detectedTypeProp, int listIndex)
        {
            if (listIndex >= 0)
            {
                var s = GetTweenEntryMemberString(detectedTypeProp, listIndex, "detectedPropertyType");
                if (!string.IsNullOrEmpty(s))
                    return s;
            }
            return GetString(detectedTypeProp);
        }

        private static void DrawDetectedTypeDisplayOnly(InspectorProperty detectedTypeProp, int listIndex)
        {
            // Do not use LabelField(label, value) here: a long label + "Boolean" clips into garbage (e.g. "editaBoolean") in tight layouts.
            string s = GetResolvedDetectedTypeString(detectedTypeProp, listIndex);
            EditorGUILayout.LabelField("Resolved member type (set via Browse / Refresh Type)", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(s) ? "— not set —" : s, EditorStyles.wordWrappedLabel);
        }

        private static bool GetBool(InspectorProperty prop)
        {
            try { return prop?.ValueEntry != null && (bool)prop.ValueEntry.WeakSmartValue; }
            catch { return false; }
        }

        private static void SetBool(InspectorProperty prop, bool v)
        {
            try { if (prop?.ValueEntry != null) prop.ValueEntry.WeakSmartValue = v; }
            catch { }
        }

        private static TEnum GetEnum<TEnum>(InspectorProperty prop, TEnum fallback) where TEnum : struct
        {
            try
            {
                if (prop?.ValueEntry == null) return fallback;
                object v = prop.ValueEntry.WeakSmartValue;
                if (v is TEnum e) return e;
                if (v is int i) return (TEnum)Enum.ToObject(typeof(TEnum), i);
                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static bool IsBooleanDetectedType(string det)
        {
            if (string.IsNullOrEmpty(det)) return false;
            if (det.Equals("Boolean", StringComparison.OrdinalIgnoreCase)) return true;
            if (det.Equals("System.Boolean", StringComparison.Ordinal)) return true;
            return det.EndsWith(".Boolean", StringComparison.Ordinal);
        }

        private static bool UsesCurve(Animate.TweenType tweenType, string det, Animate.CustomPropertyMode mode, Animate.MethodInvokeTiming methodInvoke, Animate.MethodInvokeTiming siblingInvoke)
        {
            if (tweenType == Animate.TweenType.SiblingOrder)
            {
                if (mode != Animate.CustomPropertyMode.AutoTween) return false;
                return siblingInvoke == Animate.MethodInvokeTiming.OnCurve;
            }
            if (tweenType == Animate.TweenType.CustomProperty)
            {
                // Methods only use the curve when timing is OnCurve.
                if (string.Equals(det, "Void", StringComparison.Ordinal))
                    return methodInvoke == Animate.MethodInvokeTiming.OnCurve;

                // SetAtEnd / ToggleAtHalf don't evaluate the curve.
                if (mode != Animate.CustomPropertyMode.AutoTween)
                    return false;
            }
            return true;
        }

        private const string TweenListPropertyName = "configuredTweens";

        private static int GetTweenListIndexForProperty(InspectorProperty property)
        {
            if (property == null) return -1;
            int idx = -1;
            try { idx = property.Index; } catch { }
            if (idx >= 0) return idx;
            return ParseConfiguredTweensListIndexFromUnityPath(property.UnityPropertyPath);
        }

        private static int ParseConfiguredTweensListIndexFromUnityPath(string unityPath)
        {
            if (string.IsNullOrEmpty(unityPath)) return -1;
            string marker = TweenListPropertyName + ".Array.data[";
            int m = unityPath.IndexOf(marker, StringComparison.Ordinal);
            if (m < 0) return -1;
            int start = m + marker.Length;
            int end = unityPath.IndexOf(']', start);
            if (end < 0) return -1;
            return int.TryParse(unityPath.Substring(start, end - start), out int n) ? n : -1;
        }

        private static string GetTweenEntryMemberString(InspectorProperty anyProp, int listIndex, string fieldName)
        {
            if (anyProp == null || listIndex < 0 || string.IsNullOrEmpty(fieldName)) return null;
            var so = anyProp.Tree?.UnitySerializedObject;
            if (so == null) return null;
            var p = TweenListPropertyName + ".Array.data[" + listIndex + "]." + fieldName;
            var sp = so.FindProperty(p);
            if (sp == null || sp.propertyType != UnityEditor.SerializedPropertyType.String) return null;
            return sp.stringValue;
        }

        private static bool TrySetTweenEntryMemberString(InspectorProperty anyProp, int listIndex, string fieldName, string value)
        {
            if (anyProp == null || listIndex < 0 || string.IsNullOrEmpty(fieldName)) return false;
            var so = anyProp.Tree?.UnitySerializedObject;
            if (so == null) return false;
            var p = TweenListPropertyName + ".Array.data[" + listIndex + "]." + fieldName;
            var sp = so.FindProperty(p);
            if (sp == null || sp.propertyType != UnityEditor.SerializedPropertyType.String) return false;
            sp.stringValue = value ?? string.Empty;
            so.ApplyModifiedProperties();
            if (so.targetObject is UnityEngine.Object uo)
                EditorUtility.SetDirty(uo);
            return true;
        }

        private static string ResolvePropertyPath(InspectorProperty propertyNameProp, int listIndex, InspectorProperty anyForSo)
        {
            // Prefer Unity serialized value (reliable) over Odin WeakSmartValue, which can lag behind.
            if (listIndex >= 0)
            {
                var s = GetTweenEntryMemberString(anyForSo, listIndex, "propertyName");
                if (!string.IsNullOrEmpty(s)) return s;
            }
            try
            {
                return propertyNameProp?.ValueEntry?.WeakSmartValue as string;
            }
            catch
            {
                return null;
            }
        }

        private static void WritePropertyAndDetected(InspectorProperty propertyNameProp, InspectorProperty detectedTypeProp, int listIndex, string newPath, string newTypeName)
        {
            if (listIndex >= 0)
            {
                if (!string.IsNullOrEmpty(newPath))
                    TrySetTweenEntryMemberString(propertyNameProp, listIndex, "propertyName", newPath);
                if (newTypeName != null)
                    TrySetTweenEntryMemberString(detectedTypeProp, listIndex, "detectedPropertyType", newTypeName);
            }
            if (propertyNameProp?.ValueEntry != null)
            {
                try { propertyNameProp.ValueEntry.WeakSmartValue = newPath; } catch { }
            }
            // detectedPropertyType is [ReadOnly] — do not assign WeakSmartValue; SerializedObject write already persists.
            if (!string.IsNullOrEmpty(newTypeName) && listIndex < 0)
            {
                var uso = detectedTypeProp?.Tree?.UnitySerializedObject;
                if (uso != null)
                {
                    try
                    {
                        var sp = uso.FindProperty(detectedTypeProp.Path);
                        if (sp != null)
                        {
                            sp.stringValue = newTypeName;
                            uso.ApplyModifiedProperties();
                        }
                    }
                    catch { }
                }
                if (listIndex < 0)
                {
                    try
                    {
                        var ownerProp = detectedTypeProp?.Parent;
                        var ownerObj = ownerProp?.ValueEntry?.WeakSmartValue;
                        if (ownerObj != null)
                        {
                            var f2 = ownerObj.GetType().GetField("detectedPropertyType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (f2 != null) f2.SetValue(ownerObj, newTypeName);
                        }
                        ownerProp?.Tree?.UnitySerializedObject?.ApplyModifiedProperties();
                    }
                    catch { }
                }
            }
        }

        /// <param name="debugText">If non-null, full MemberPathBrowser step trace (only for the Debug button; Browse/Refresh pass null to avoid log spam and extra work).</param>
        private static void LogMemberResolve(string context, UnityEngine.Object root, string path, System.Type resolvedType, string debugText, bool wrote)
        {
            var tName = resolvedType != null ? resolvedType.Name : "null";
            var tFull = resolvedType != null ? resolvedType.FullName : "null";
            var summary = "[Animate] " + context + " — Path='" + (path ?? "") + "' Root='" + (root != null ? root.GetType().Name : "null") + "' — Resolved: " + tName + " (" + tFull + "), wrote detectedPropertyType: " + wrote;
            if (string.IsNullOrEmpty(debugText))
            {
                Debug.Log(summary, root as UnityEngine.Object);
                return;
            }
            Debug.Log(
                "[MemberPathBrowser Debug] Path='" + (path ?? "") + "' Root='" + (root != null ? root.GetType().FullName : "null") + "'\n" +
                debugText +
                summary,
                root as UnityEngine.Object);
        }

        private static void DrawBrowseRefreshRow(int listIndex, InspectorProperty targetObjectProp, InspectorProperty targetComponentProp, InspectorProperty propertyNameProp, InspectorProperty detectedTypeProp)
        {
            GameObject go = null;
            Component comp = null;
            try { go = targetObjectProp?.ValueEntry?.WeakSmartValue as GameObject; } catch { }
            try { comp = targetComponentProp?.ValueEntry?.WeakSmartValue as Component; } catch { }

            // If a component is selected, browse ONLY that component.
            // If no component is selected, browse the target GameObject.
            UnityEngine.Object root = comp != null ? (UnityEngine.Object)comp : (UnityEngine.Object)go;

            EditorGUILayout.BeginHorizontal();
            {
                GUI.enabled = root != null;

                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    MemberPathBrowserWindow.Show(root, 3, selected =>
                    {
                        try
                        {
                            WritePropertyAndDetected(propertyNameProp, detectedTypeProp, listIndex, selected.path, selected.typeName);
                            if (listIndex < 0)
                            {
                                if (propertyNameProp?.ValueEntry != null)
                                {
                                    try { propertyNameProp.ValueEntry.WeakSmartValue = selected.path; } catch { }
                                }
                            }

                            var pathForLog = ResolvePropertyPath(propertyNameProp, listIndex, detectedTypeProp ?? propertyNameProp);
                            var t = (root != null && !string.IsNullOrEmpty(pathForLog)) ? MemberPathBrowser.ResolveMemberType(root, pathForLog) : null;
                            string expected = selected.typeName ?? string.Empty;
                            bool w = listIndex >= 0
                                && string.Equals(GetTweenEntryMemberString(detectedTypeProp, listIndex, "detectedPropertyType"), expected, StringComparison.Ordinal);
                            LogMemberResolve("Browse (member selected)", root, pathForLog, t, null, w);
                            GUIHelper.RequestRepaint();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    });
                }

                if (GUILayout.Button("Refresh Type", GUILayout.Width(110)))
                {
                    try
                    {
                        string path = ResolvePropertyPath(propertyNameProp, listIndex, detectedTypeProp ?? propertyNameProp);
                        var t = (root != null && !string.IsNullOrEmpty(path)) ? MemberPathBrowser.ResolveMemberType(root, path) : null;
                        string typeName = t != null ? t.Name : string.Empty;
                        bool wrote = listIndex >= 0 && TrySetTweenEntryMemberString(detectedTypeProp, listIndex, "detectedPropertyType", typeName);
                        if (!wrote)
                            wrote = TrySetDetectedViaOdinPathOrReflection(detectedTypeProp, t);
                        LogMemberResolve("Refresh Type", root, path, t, null, wrote);
                        GUIHelper.RequestRepaint();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }

                if (GUILayout.Button("Debug", GUILayout.Width(60)))
                {
                    try
                    {
                        string path = ResolvePropertyPath(propertyNameProp, listIndex, detectedTypeProp ?? propertyNameProp);
                        var debug = MemberPathBrowser.ResolveMemberTypeDebug(root, path);
                        var t = (root != null && !string.IsNullOrEmpty(path)) ? MemberPathBrowser.ResolveMemberType(root, path) : null;
                        LogMemberResolve("Debug (manual)", root, path, t, debug, false);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static bool TrySetDetectedViaOdinPathOrReflection(InspectorProperty detectedTypeProp, System.Type t)
        {
            if (detectedTypeProp == null) return false;
            var typeName = t != null ? t.Name : string.Empty;
            var uso = detectedTypeProp.Tree?.UnitySerializedObject;
            if (uso != null)
            {
                try
                {
                    var sp = uso.FindProperty(detectedTypeProp.Path);
                    if (sp != null)
                    {
                        sp.stringValue = typeName;
                        uso.ApplyModifiedProperties();
                        if (uso.targetObject is UnityEngine.Object uo)
                            EditorUtility.SetDirty(uo);
                        return true;
                    }
                }
                catch { }
            }
            try
            {
                var ownerProp = detectedTypeProp.Parent;
                var ownerObj = ownerProp?.ValueEntry?.WeakSmartValue;
                if (ownerObj != null)
                {
                    var f2 = ownerObj.GetType().GetField("detectedPropertyType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f2 != null) f2.SetValue(ownerObj, typeName);
                }
                ownerProp?.Tree?.UnitySerializedObject?.ApplyModifiedProperties();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void DrawChainRow(InspectorProperty chainProp, int index)
        {
            if (chainProp == null) return;

            EditorGUILayout.BeginHorizontal();
            {
                bool chained = GetBool(chainProp);

                if (index <= 0)
                {
                    // First item has no previous tween to chain to.
                    GUI.enabled = false;
                    EditorGUILayout.ToggleLeft(new GUIContent("⛓ Chain after previous", "First item has no previous tween. List order defines chaining priority."), false);
                    GUI.enabled = true;
                }
                else
                {
                    bool newChained = EditorGUILayout.ToggleLeft(new GUIContent("⛓ Chain after previous", "List order defines chaining priority."), chained);
                    if (newChained != chained)
                        SetBool(chainProp, newChained);
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
        }

        private readonly struct ChainedIndentScope : IDisposable
        {
            private readonly bool chained;
            public ChainedIndentScope(bool chained)
            {
                this.chained = chained;
                if (!chained) return;

                const float indentPx = 14f;
                GUILayout.BeginHorizontal();
                var lineRect = GUILayoutUtility.GetRect(indentPx, 0, GUILayout.Width(indentPx));

                if (Event.current.type == EventType.Repaint)
                {
                    var drawRect = new Rect(lineRect.x + indentPx * 0.5f, lineRect.y - 2f, 1f, EditorGUIUtility.singleLineHeight + 6f);
                    SirenixEditorGUI.DrawSolidRect(drawRect, new Color(1f, 1f, 1f, 0.20f));
                }

                GUILayout.BeginVertical();
            }

            public void Dispose()
            {
                if (!chained) return;
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }
    }
}
#endif
