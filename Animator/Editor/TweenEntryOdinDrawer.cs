#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Animator
{
    // Custom layout for a single tween entry.
    // Keeps the inspector top-down: target/type -> custom property -> values -> options -> timing.
    // Also keeps expensive reflection listing behind an explicit Browse button.
    public sealed class TweenEntryOdinDrawer : OdinValueDrawer<Animate.TweenEntry>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            int index = -1;
            try { index = Property.Index; } catch { /* older Odin versions */ }

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

            var tweenType = GetEnum<Animate.TweenType>(typeProp, Animate.TweenType.Position);
            bool isCustom = tweenType == Animate.TweenType.CustomProperty;
            bool isRendererColor = tweenType == Animate.TweenType.RendererColor;
            bool isMaterialFloat = tweenType == Animate.TweenType.MaterialFloat;
            bool isCanvasAlpha = tweenType == Animate.TweenType.CanvasGroupAlpha;

            string det = GetString(detectedTypeProp);
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
                        DrawBrowseRefreshRow(targetObjectProp, targetComponentProp, propertyNameProp, detectedTypeProp);

                        DrawReadOnlyIf(detectedTypeProp, "Detected Type");

                        // propertyMode is only meaningful for value types. Methods use methodInvokeTiming.
                        if (!string.Equals(det, "Void", StringComparison.Ordinal))
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
                    // Start source is relevant for most value tweens.
                    if (isCustom || tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale || isCanvasAlpha || isRendererColor || isMaterialFloat)
                    {
                        DrawIf(startSourceProp, "Initial Value");
                    }

                    // Local flag for world/local position/rotation modes
                    if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler)
                    {
                        DrawIf(localProp, "Local");
                    }

                    // Value fields are conditional based on tween type / detected type
                    if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale)
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
                        Component comp = null;
                        try { comp = targetComponentProp?.ValueEntry?.WeakSmartValue as Component; } catch { }

                        // Typer: explicit UI (text + append) regardless of detected type / property selection.
                        if (comp is Typer)
                        {
                            DrawIf(toStringProp, "Text");
                            DrawIf(typerAppendProp, "Append");
                        }
                        else if (det == "Color")
                        {
                            DrawIf(fromColorProp, "Start Color");
                            DrawIf(toColorProp, "End Color");
                        }
                        else if (det == "Boolean")
                        {
                            DrawIf(fromBoolProp, "Start");
                            DrawIf(toBoolProp, "End");

                            // For bool, propertyMode matters (AutoTween vs SetAtEnd vs ToggleAtHalf)
                            DrawIf(propertyModeProp, "Property Mode");
                        }
                        else if (det == "String")
                        {
                            DrawIf(fromStringProp, "Start Value");
                            DrawIf(toStringProp, "End Value");
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
                        if (!string.IsNullOrEmpty(det) && det != "Void" && det != "Color" && det != "Boolean" && det != "Single" && det != "Double" && det != "Int32")
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

                    DrawIf(durationProp, "Duration (s)");

                    if (UsesCurve(tweenType, det, mode, invokeTiming))
                    {
                        DrawIf(curveProp, "Curve");
                    }
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

        private static void DrawReadOnlyIf(InspectorProperty prop, string labelOverride)
        {
            if (prop == null) return;
            GUI.enabled = false;
            prop.Draw(new GUIContent(labelOverride));
            GUI.enabled = true;
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

        private static bool UsesCurve(Animate.TweenType tweenType, string det, Animate.CustomPropertyMode mode, Animate.MethodInvokeTiming invokeTiming)
        {
            if (tweenType == Animate.TweenType.CustomProperty)
            {
                // Methods only use the curve when timing is OnCurve.
                if (string.Equals(det, "Void", StringComparison.Ordinal))
                    return invokeTiming == Animate.MethodInvokeTiming.OnCurve;

                // SetAtEnd / ToggleAtHalf don't evaluate the curve.
                if (mode != Animate.CustomPropertyMode.AutoTween)
                    return false;
            }
            return true;
        }

        private static void DrawBrowseRefreshRow(InspectorProperty targetObjectProp, InspectorProperty targetComponentProp, InspectorProperty propertyNameProp, InspectorProperty detectedTypeProp)
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
                            if (propertyNameProp?.ValueEntry != null)
                                propertyNameProp.ValueEntry.WeakSmartValue = selected.path;
                            if (detectedTypeProp?.ValueEntry != null)
                                detectedTypeProp.ValueEntry.WeakSmartValue = selected.typeName;
                        }
                        catch { }
                    });
                }

                if (GUILayout.Button("Refresh Type", GUILayout.Width(110)))
                {
                    try
                    {
                        string path = propertyNameProp?.ValueEntry?.WeakSmartValue as string;
                        var t = (root != null && !string.IsNullOrEmpty(path)) ? MemberPathBrowser.ResolveMemberType(root, path) : null;
                        if (detectedTypeProp?.ValueEntry != null)
                            detectedTypeProp.ValueEntry.WeakSmartValue = t != null ? t.Name : string.Empty;
                    }
                    catch { }
                }
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
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
