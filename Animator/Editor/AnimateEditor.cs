#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Core.Animator
{
    /// <summary>
    /// Inspector for <see cref="Animate"/>.
    /// - When Odin Inspector is present, use the Odin-backed inspector and custom drawers.
    /// - Otherwise, use a best-effort legacy inspector with a custom list UI, browse/refresh for custom member paths,
    ///   and conditional fields approximating the Odin layout.
    /// </summary>
    [CustomEditor(typeof(Animate))]
#if ODIN_INSPECTOR
    public sealed class AnimateEditor : OdinEditor
    {
    }
#else
    public sealed class AnimateEditor : UnityEditor.Editor
    {
        private const float Pad = 6f;
        private const float Line = 18f;
        private const float Gap = 4f;

        private SerializedProperty playAllOnStartProp;
        private SerializedProperty configuredTweensProp;

        private ReorderableList tweensList;

        private Animate Target => (Animate)target;

        private void OnEnable()
        {
            playAllOnStartProp = serializedObject.FindProperty("playAllOnStart");
            configuredTweensProp = serializedObject.FindProperty("configuredTweens");

            if (configuredTweensProp != null)
            {
                tweensList = new ReorderableList(serializedObject, configuredTweensProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);
                tweensList.drawHeaderCallback = DrawTweensHeader;
                tweensList.drawElementCallback = DrawTweenElement;
                tweensList.elementHeightCallback = GetTweenElementHeight;
                tweensList.onAddCallback = OnAddTween;
                tweensList.onRemoveCallback = OnRemoveTween;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGlobalSection();

            EditorGUILayout.Space(6);

            if (tweensList != null)
            {
                tweensList.DoLayoutList();
            }
            else
            {
                EditorGUILayout.HelpBox("Unable to locate serialized field 'configuredTweens'.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGlobalSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playAllOnStartProp, new GUIContent("Play On Start"));

            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Play All"))
                {
                    Target.PlayAllConfigured();
                }
                if (GUILayout.Button("Stop All"))
                {
                    Target.StopAllTweens();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTweensHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Configured Tweens");
        }

        private void OnAddTween(ReorderableList list)
        {
            serializedObject.Update();

            int index = configuredTweensProp.arraySize;
            configuredTweensProp.InsertArrayElementAtIndex(index);

            var entry = configuredTweensProp.GetArrayElementAtIndex(index);
            ResetTweenEntry(entry);

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            list.index = Mathf.Clamp(index, 0, configuredTweensProp.arraySize - 1);
        }

        private void OnRemoveTween(ReorderableList list)
        {
            if (list.index < 0 || list.index >= configuredTweensProp.arraySize)
                return;

            // Keep behavior simple and predictable (Unity's delete semantics + Undo).
            serializedObject.Update();
            configuredTweensProp.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        private float GetTweenElementHeight(int index)
        {
            if (configuredTweensProp == null || index < 0 || index >= configuredTweensProp.arraySize)
                return Line + Pad * 2;

            var entry = configuredTweensProp.GetArrayElementAtIndex(index);
            bool expanded = GetExpanded(index);

            // Header row always.
            float h = Pad + Line + Gap;

            if (!expanded)
                return h + Pad;

            // Action row
            h += Line + Gap;

            // Target section
            h += SectionHeight(entry, index, section: Section.Target);
            // Custom property section
            h += SectionHeight(entry, index, section: Section.CustomProperty);
            // Values
            h += SectionHeight(entry, index, section: Section.Values);
            // Options
            h += SectionHeight(entry, index, section: Section.Options);
            // Timing
            h += SectionHeight(entry, index, section: Section.Timing);

            return h + Pad;
        }

        private enum Section { Target, CustomProperty, Values, Options, Timing }

        private float SectionHeight(SerializedProperty entry, int index, Section section)
        {
            if (entry == null) return 0f;

            var typeProp = entry.FindPropertyRelative("type");
            var detectedTypeProp = entry.FindPropertyRelative("detectedPropertyType");
            var propertyNameProp = entry.FindPropertyRelative("propertyName");
            var targetComponentProp = entry.FindPropertyRelative("targetComponent");
            var propertyModeProp = entry.FindPropertyRelative("propertyMode");
            var methodInvokeTimingProp = entry.FindPropertyRelative("methodInvokeTiming");
            var siblingTimingProp = entry.FindPropertyRelative("siblingTiming");

            var tweenType = (Animate.TweenType)typeProp.enumValueIndex;
            string det = detectedTypeProp != null ? detectedTypeProp.stringValue : string.Empty;
            string selectedPath = propertyNameProp != null ? propertyNameProp.stringValue : string.Empty;

            bool isCustom = tweenType == Animate.TweenType.CustomProperty;
            bool isRendererColor = tweenType == Animate.TweenType.RendererColor;
            bool isMaterialFloat = tweenType == Animate.TweenType.MaterialFloat;
            bool isCanvasAlpha = tweenType == Animate.TweenType.CanvasGroupAlpha;
            bool isSibling = tweenType == Animate.TweenType.SiblingOrder;

            bool isTyper = false;
            try
            {
                var comp = targetComponentProp != null ? targetComponentProp.objectReferenceValue as Component : null;
                isTyper = isCustom && comp is Typer;
            }
            catch { }

            var mode = propertyModeProp != null ? (Animate.CustomPropertyMode)propertyModeProp.enumValueIndex : Animate.CustomPropertyMode.AutoTween;
            var invokeTiming = methodInvokeTimingProp != null ? (Animate.MethodInvokeTiming)methodInvokeTimingProp.enumValueIndex : Animate.MethodInvokeTiming.OnEnd;
            var siblingInvoke = siblingTimingProp != null ? (Animate.MethodInvokeTiming)siblingTimingProp.enumValueIndex : Animate.MethodInvokeTiming.OnEnd;

            bool showSection = section switch
            {
                Section.Target => true,
                Section.CustomProperty => isCustom,
                Section.Values => true,
                Section.Options => true,
                Section.Timing => true,
                _ => true
            };

            if (!showSection)
                return 0f;

            float h = 0f;

            // section header
            h += Line + Gap;

            int lines = 0;
            float extra = 0f;

            switch (section)
            {
                case Section.Target:
                    // type + targetObject (+ targetComponent)
                    lines += 2;
                    if (isCustom) lines += 1;
                    break;

                case Section.CustomProperty:
                    // propertyName + browse/refresh row + detected type
                    lines += 3;
                    // propertyMode vs method timing
                    // (Boolean's propertyMode is shown alongside Start/End in the Values section to avoid duplication.)
                    if (!string.Equals(det, "Void", StringComparison.Ordinal) && !string.Equals(det, "String", StringComparison.Ordinal) && !IsBooleanDetectedType(det))
                        lines += 1;
                    if (string.Equals(det, "Void", StringComparison.Ordinal))
                        lines += 1;
                    break;

                case Section.Values:
                    {
                        bool startSourceRelevant =
                            (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale || isCanvasAlpha || isRendererColor || isMaterialFloat)
                            || (isCustom && !isTyper && det != "Void" && det != "String" && !IsBooleanDetectedType(det));
                        startSourceRelevant = startSourceRelevant && !isSibling;

                        if (startSourceRelevant) lines += 1;

                        if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler)
                            lines += 1; // local

                        if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale)
                            lines += 2; // from/to vec3
                        else if (isCanvasAlpha || isMaterialFloat)
                            lines += 2; // from/to float
                        else if (isRendererColor)
                            lines += 2; // from/to color
                        else if (isSibling)
                        {
                            lines += 1; // propertyMode
                            lines += 1; // siblingShift
                            if (mode == Animate.CustomPropertyMode.ToggleAtHalf)
                                lines += 2; // from + to
                            else
                                lines += 1; // toFloat order delta
                        }
                        else if (isCustom)
                        {
                            if (isTyper && string.Equals(selectedPath, "StartTyping", StringComparison.Ordinal))
                            {
                                // TextArea + Append
                                lines += 1;
                                extra += Mathf.Max(Line * 2f, 54f) + Gap; // textarea + spacing
                                lines += 1;
                            }
                            else if (det == "Color")
                                lines += 2;
                            else if (IsBooleanDetectedType(det))
                            {
                                if (mode == Animate.CustomPropertyMode.SetAtEnd)
                                    lines += 2; // propertyMode + toBool
                                else
                                    lines += 3; // propertyMode + from + to
                            }
                            else if (det == "String")
                            {
                                lines += 1;
                                extra += Mathf.Max(Line * 2f, 54f) + Gap;
                            }
                            else if (det == "Single" || det == "Double" || det == "Int32")
                                lines += 2;
                            else if (!string.IsNullOrEmpty(det) && det != "Void")
                                lines += 2; // vec3-backed
                        }
                    }
                    break;

                case Section.Options:
                    if (isCustom)
                    {
                        bool vectorMask = det == "Vector3" || det == "Vector4" || det == "Quaternion" || (!string.IsNullOrEmpty(det) && det != "Single" && det != "Double" && det != "Int32" && det != "Color" && !IsBooleanDetectedType(det) && det != "Void");
                        bool enumMask = !string.IsNullOrEmpty(det) && det != "Single" && det != "Double" && det != "Int32" && det != "Vector3" && det != "Vector4" && det != "Quaternion" && det != "Color" && !IsBooleanDetectedType(det) && det != "Void";

                        if (vectorMask) lines += 1;
                        if (enumMask) lines += 1;
                    }
                    if (isRendererColor)
                    {
                        lines += 2; // material index + property
                        // color properties array (variable)
                        var mcp = entry.FindPropertyRelative("materialColorProperties");
                        extra += (mcp != null ? EditorGUI.GetPropertyHeight(mcp, includeChildren: true) : 0f) + Gap;
                        lines += 0;
                    }
                    else if (isMaterialFloat)
                    {
                        lines += 1; // material property
                    }
                    break;

                case Section.Timing:
                    lines += 1; // delay mode
                    var delayModeProp = entry.FindPropertyRelative("delayMode");
                    var delayValueProp = entry.FindPropertyRelative("delayValue");
                    var durationProp = entry.FindPropertyRelative("duration");
                    var curveProp = entry.FindPropertyRelative("curve");

                    var delayMode = delayModeProp != null ? (Animate.DelayMode)delayModeProp.enumValueIndex : Animate.DelayMode.None;
                    if (delayMode == Animate.DelayMode.Frames || delayMode == Animate.DelayMode.Seconds)
                        lines += 1;

                    bool durationRelevant = !(isCustom && string.Equals(det, "Void", StringComparison.Ordinal) && invokeTiming == Animate.MethodInvokeTiming.OnStart);
                    if (tweenType == Animate.TweenType.SiblingOrder)
                    {
                        if (mode == Animate.CustomPropertyMode.AutoTween && siblingInvoke == Animate.MethodInvokeTiming.OnStart)
                            durationRelevant = false;
                    }
                    if (durationRelevant) lines += 1;

                    if (tweenType == Animate.TweenType.SiblingOrder && mode == Animate.CustomPropertyMode.AutoTween)
                        lines += 1; // apply timing (siblingTiming)

                    bool curveRelevant = !string.Equals(det, "String", StringComparison.Ordinal) && UsesCurve(tweenType, det, mode, invokeTiming, siblingInvoke);
                    if (curveRelevant) lines += 1;

                    lines += 1; // debugLogging

                    break;
            }

            h += lines * (Line + Gap) + extra;
            return h;
        }

        private void DrawTweenElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (configuredTweensProp == null || index < 0 || index >= configuredTweensProp.arraySize)
                return;

            var entry = configuredTweensProp.GetArrayElementAtIndex(index);
            if (entry == null)
                return;

            // Background
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            rect = Inset(rect, Pad);

            // Fetch key props
            var chainProp = entry.FindPropertyRelative("chainAfterPrevious");
            var nameProp = entry.FindPropertyRelative("name");
            var typeProp = entry.FindPropertyRelative("type");

            bool expanded = GetExpanded(index);

            // Header row: foldout + name
            {
                Rect header = NextLine(ref rect);

                // foldout
                var foldRect = header;
                foldRect.width = 14f;
                bool newExpanded = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
                if (newExpanded != expanded)
                {
                    expanded = newExpanded;
                    SetExpanded(index, expanded);
                }

                // name field
                var nameRect = header;
                nameRect.xMin += 16f;

                if (nameProp != null)
                {
                    EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
                }
                else
                {
                    EditorGUI.LabelField(nameRect, $"Tween {index}");
                }
            }

            // Chain row (visible even when collapsed)
            {
                Rect chainRow = NextLine(ref rect);

                bool chained = chainProp != null && chainProp.boolValue;
                using (new EditorGUI.DisabledScope(index == 0))
                {
                    bool next = EditorGUI.ToggleLeft(chainRow, new GUIContent("⛓ Chain after previous", "List order defines chaining priority."), chained);
                    if (index == 0 && chained)
                    {
                        // Force off for index 0.
                        if (chainProp != null) chainProp.boolValue = false;
                    }
                    else if (chainProp != null)
                    {
                        chainProp.boolValue = next;
                    }
                }

                // Simple visual indent marker
                if (index > 0 && chainProp != null && chainProp.boolValue)
                {
                    var mark = chainRow;
                    mark.x += 2f;
                    mark.width = 2f;
                    EditorGUI.DrawRect(mark, new Color(1f, 1f, 1f, 0.18f));
                }
            }

            if (!expanded)
                return;

            // Action row
            {
                Rect row = NextLine(ref rect);

                var playRect = row;
                playRect.width = 60f;

                var cloneRect = row;
                cloneRect.xMin = playRect.xMax + 4f;
                cloneRect.width = 70f;

                var removeRect = row;
                removeRect.xMin = cloneRect.xMax + 4f;
                removeRect.width = 70f;

                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (GUI.Button(playRect, "Play"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        Target.PlayByIndex(index);
                        serializedObject.Update();
                    }
                }

                if (GUI.Button(cloneRect, "Clone"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Target.CloneConfiguredTween(index);
                    serializedObject.Update();
                }

                if (GUI.Button(removeRect, "Remove"))
                {
                    serializedObject.ApplyModifiedProperties();
                    Target.RemoveConfiguredTween(index);
                    serializedObject.Update();
                }
            }

            DrawTargetSection(ref rect, entry);
            DrawCustomPropertySection(ref rect, entry);
            DrawValuesSection(ref rect, entry);
            DrawOptionsSection(ref rect, entry);
            DrawTimingSection(ref rect, entry);
        }

        private void DrawTargetSection(ref Rect rect, SerializedProperty entry)
        {
            if (entry == null) return;

            var typeProp = entry.FindPropertyRelative("type");
            var targetObjectProp = entry.FindPropertyRelative("targetObject");
            var targetComponentProp = entry.FindPropertyRelative("targetComponent");

            var tweenType = (Animate.TweenType)typeProp.enumValueIndex;
            bool isCustom = tweenType == Animate.TweenType.CustomProperty;

            DrawSectionHeader(ref rect, "Target");

            EditorGUI.PropertyField(NextLine(ref rect), typeProp);
            EditorGUI.PropertyField(NextLine(ref rect), targetObjectProp);

            if (isCustom)
            {
                EditorGUI.PropertyField(NextLine(ref rect), targetComponentProp, new GUIContent("Target component (optional)", "Optional. Leave empty to use Target Object (the GameObject) as the member path root. Set when the property lives on a specific component."));
            }
        }

        private void DrawCustomPropertySection(ref Rect rect, SerializedProperty entry)
        {
            if (entry == null) return;

            var typeProp = entry.FindPropertyRelative("type");
            var tweenType = (Animate.TweenType)typeProp.enumValueIndex;
            if (tweenType != Animate.TweenType.CustomProperty)
                return;

            var targetObjectProp = entry.FindPropertyRelative("targetObject");
            var targetComponentProp = entry.FindPropertyRelative("targetComponent");
            var propertyNameProp = entry.FindPropertyRelative("propertyName");
            var detectedTypeProp = entry.FindPropertyRelative("detectedPropertyType");
            var propertyModeProp = entry.FindPropertyRelative("propertyMode");
            var methodInvokeTimingProp = entry.FindPropertyRelative("methodInvokeTiming");

            string det = detectedTypeProp != null ? detectedTypeProp.stringValue : string.Empty;

            DrawSectionHeader(ref rect, "Custom Property");

            EditorGUI.PropertyField(NextLine(ref rect), propertyNameProp, new GUIContent("Property"));

            DrawBrowseRefreshRow(ref rect, targetObjectProp, targetComponentProp, propertyNameProp, detectedTypeProp);

            {
                var detLabel = "Detected type (Browse/Refresh only — not typed)";
                var detValue = detectedTypeProp != null ? (string.IsNullOrEmpty(detectedTypeProp.stringValue) ? "— not set —" : detectedTypeProp.stringValue) : "— not set —";
                EditorGUI.LabelField(NextLine(ref rect), new GUIContent(detLabel), new GUIContent(detValue));
            }

            if (!string.Equals(det, "Void", StringComparison.Ordinal) && !string.Equals(det, "String", StringComparison.Ordinal) && !IsBooleanDetectedType(det))
            {
                EditorGUI.PropertyField(NextLine(ref rect), propertyModeProp, new GUIContent("Property Mode"));
            }

            if (string.Equals(det, "Void", StringComparison.Ordinal))
            {
                EditorGUI.PropertyField(NextLine(ref rect), methodInvokeTimingProp, new GUIContent("Invoke Timing"));
            }
        }

        private void DrawValuesSection(ref Rect rect, SerializedProperty entry)
        {
            if (entry == null) return;

            var typeProp = entry.FindPropertyRelative("type");
            var tweenType = (Animate.TweenType)typeProp.enumValueIndex;

            var detectedTypeProp = entry.FindPropertyRelative("detectedPropertyType");
            var propertyNameProp = entry.FindPropertyRelative("propertyName");
            var targetComponentProp = entry.FindPropertyRelative("targetComponent");

            string det = detectedTypeProp != null ? detectedTypeProp.stringValue : string.Empty;
            string selectedPath = propertyNameProp != null ? propertyNameProp.stringValue : string.Empty;

            bool isCustom = tweenType == Animate.TweenType.CustomProperty;
            bool isRendererColor = tweenType == Animate.TweenType.RendererColor;
            bool isMaterialFloat = tweenType == Animate.TweenType.MaterialFloat;
            bool isCanvasAlpha = tweenType == Animate.TweenType.CanvasGroupAlpha;
            bool isSibling = tweenType == Animate.TweenType.SiblingOrder;

            bool isTyper = false;
            try
            {
                var comp = targetComponentProp != null ? targetComponentProp.objectReferenceValue as Component : null;
                isTyper = isCustom && comp is Typer;
            }
            catch { }

            var startSourceProp = entry.FindPropertyRelative("startSource");
            var localProp = entry.FindPropertyRelative("local");

            var fromVec3Prop = entry.FindPropertyRelative("fromVec3");
            var toVec3Prop = entry.FindPropertyRelative("toVec3");
            var fromFloatProp = entry.FindPropertyRelative("fromFloat");
            var toFloatProp = entry.FindPropertyRelative("toFloat");
            var fromColorProp = entry.FindPropertyRelative("fromColor");
            var toColorProp = entry.FindPropertyRelative("toColor");
            var fromBoolProp = entry.FindPropertyRelative("fromBool");
            var toBoolProp = entry.FindPropertyRelative("toBool");
            var toStringProp = entry.FindPropertyRelative("toString");
            var typerAppendProp = entry.FindPropertyRelative("typerAppend");
            var propertyModeProp = entry.FindPropertyRelative("propertyMode");
            var siblingShiftProp = entry.FindPropertyRelative("siblingShift");

            DrawSectionHeader(ref rect, "Values");

            bool startSourceRelevant =
                (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale || isCanvasAlpha || isRendererColor || isMaterialFloat)
                || (isCustom && !isTyper && det != "Void" && det != "String" && !IsBooleanDetectedType(det));
            startSourceRelevant = startSourceRelevant && !isSibling;

            if (startSourceRelevant)
                EditorGUI.PropertyField(NextLine(ref rect), startSourceProp, new GUIContent("Initial Value"));

            if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler)
                EditorGUI.PropertyField(NextLine(ref rect), localProp, new GUIContent("Local"));

            if (tweenType == Animate.TweenType.Position || tweenType == Animate.TweenType.LocalPosition || tweenType == Animate.TweenType.RotationEuler || tweenType == Animate.TweenType.LocalRotationEuler || tweenType == Animate.TweenType.Scale)
            {
                EditorGUI.PropertyField(NextLine(ref rect), fromVec3Prop, new GUIContent("Start Value"));
                EditorGUI.PropertyField(NextLine(ref rect), toVec3Prop, new GUIContent("End Value"));
            }
            else if (isCanvasAlpha || isMaterialFloat)
            {
                EditorGUI.PropertyField(NextLine(ref rect), fromFloatProp, new GUIContent("Start Value"));
                EditorGUI.PropertyField(NextLine(ref rect), toFloatProp, new GUIContent("End Value"));
            }
            else if (isRendererColor)
            {
                EditorGUI.PropertyField(NextLine(ref rect), fromColorProp, new GUIContent("Start Color"));
                EditorGUI.PropertyField(NextLine(ref rect), toColorProp, new GUIContent("End Color"));
            }
            else if (tweenType == Animate.TweenType.SiblingOrder)
            {
                if (propertyModeProp != null)
                    EditorGUI.PropertyField(NextLine(ref rect), propertyModeProp, new GUIContent("Property Mode"));
                if (siblingShiftProp != null)
                    EditorGUI.PropertyField(NextLine(ref rect), siblingShiftProp, new GUIContent("Shift Direction"));
                var sMode = propertyModeProp != null
                    ? (Animate.CustomPropertyMode)propertyModeProp.enumValueIndex
                    : Animate.CustomPropertyMode.AutoTween;
                if (sMode == Animate.CustomPropertyMode.ToggleAtHalf)
                {
                    EditorGUI.PropertyField(NextLine(ref rect), fromFloatProp, new GUIContent("Start (order delta)"));
                    EditorGUI.PropertyField(NextLine(ref rect), toFloatProp, new GUIContent("End (order delta)"));
                }
                else
                {
                    EditorGUI.PropertyField(NextLine(ref rect), toFloatProp, new GUIContent("Order delta"));
                }
            }
            else if (isCustom)
            {
                if (isTyper && string.Equals(selectedPath, "StartTyping", StringComparison.Ordinal))
                {
                    DrawWrappedTextArea(ref rect, toStringProp, label: "Text");
                    EditorGUI.PropertyField(NextLine(ref rect), typerAppendProp, new GUIContent("Append"));
                }
                else if (det == "Color")
                {
                    EditorGUI.PropertyField(NextLine(ref rect), fromColorProp, new GUIContent("Start Color"));
                    EditorGUI.PropertyField(NextLine(ref rect), toColorProp, new GUIContent("End Color"));
                }
                else if (IsBooleanDetectedType(det))
                {
                    var propertyModeForBool = entry.FindPropertyRelative("propertyMode");
                    var pMode = propertyModeForBool != null
                        ? (Animate.CustomPropertyMode)propertyModeForBool.enumValueIndex
                        : Animate.CustomPropertyMode.AutoTween;
                    EditorGUI.PropertyField(NextLine(ref rect), propertyModeForBool, new GUIContent("Property Mode"));
                    if (pMode == Animate.CustomPropertyMode.SetAtEnd)
                    {
                        EditorGUI.PropertyField(NextLine(ref rect), toBoolProp, new GUIContent("Set to (when duration elapses)"));
                    }
                    else if (pMode == Animate.CustomPropertyMode.ToggleAtHalf)
                    {
                        EditorGUI.PropertyField(NextLine(ref rect), fromBoolProp, new GUIContent("At t = 0"));
                        EditorGUI.PropertyField(NextLine(ref rect), toBoolProp, new GUIContent("At t = 50% of duration"));
                    }
                    else
                    {
                        EditorGUI.PropertyField(NextLine(ref rect), fromBoolProp, new GUIContent("Value when curve is low (start side)"));
                        EditorGUI.PropertyField(NextLine(ref rect), toBoolProp, new GUIContent("Value when curve is high (end side)"));
                    }
                }
                else if (det == "String")
                {
                    DrawWrappedTextArea(ref rect, toStringProp, label: "End Value");
                }
                else if (det == "Single" || det == "Double" || det == "Int32")
                {
                    EditorGUI.PropertyField(NextLine(ref rect), fromFloatProp, new GUIContent("Start Value"));
                    EditorGUI.PropertyField(NextLine(ref rect), toFloatProp, new GUIContent("End Value"));
                }
                else if (!string.IsNullOrEmpty(det) && det != "Void")
                {
                    EditorGUI.PropertyField(NextLine(ref rect), fromVec3Prop, new GUIContent("Start Value"));
                    EditorGUI.PropertyField(NextLine(ref rect), toVec3Prop, new GUIContent("End Value"));
                }
            }
        }

        private void DrawOptionsSection(ref Rect rect, SerializedProperty entry)
        {
            if (entry == null) return;

            var typeProp = entry.FindPropertyRelative("type");
            var tweenType = (Animate.TweenType)typeProp.enumValueIndex;

            var detectedTypeProp = entry.FindPropertyRelative("detectedPropertyType");
            string det = detectedTypeProp != null ? detectedTypeProp.stringValue : string.Empty;

            bool isCustom = tweenType == Animate.TweenType.CustomProperty;
            bool isRendererColor = tweenType == Animate.TweenType.RendererColor;
            bool isMaterialFloat = tweenType == Animate.TweenType.MaterialFloat;

            DrawSectionHeader(ref rect, "Options");

            if (isCustom)
            {
                bool showVectorMask = det == "Vector3" || det == "Vector4" || det == "Quaternion" || (!string.IsNullOrEmpty(det) && det != "Single" && det != "Double" && det != "Int32" && det != "Color" && !IsBooleanDetectedType(det) && det != "Void");
                bool showEnumMask = !string.IsNullOrEmpty(det) && det != "Single" && det != "Double" && det != "Int32" && det != "Vector3" && det != "Vector4" && det != "Quaternion" && det != "Color" && !IsBooleanDetectedType(det) && det != "Void";

                var vectorMaskProp = entry.FindPropertyRelative("vectorMask");
                var enumFieldMaskProp = entry.FindPropertyRelative("enumFieldMask");

                if (showVectorMask)
                    EditorGUI.PropertyField(NextLine(ref rect), vectorMaskProp, new GUIContent("Component Mask"));

                if (showEnumMask)
                    EditorGUI.PropertyField(NextLine(ref rect), enumFieldMaskProp, new GUIContent("Enum Field Mask"));
            }

            if (isRendererColor)
            {
                var materialIndexProp = entry.FindPropertyRelative("materialIndex");
                var materialPropertyProp = entry.FindPropertyRelative("materialProperty");
                var materialColorPropertiesProp = entry.FindPropertyRelative("materialColorProperties");

                EditorGUI.PropertyField(NextLine(ref rect), materialIndexProp, new GUIContent("Material Index"));
                EditorGUI.PropertyField(NextLine(ref rect), materialPropertyProp, new GUIContent("Material Property"));

                // array
                float h = materialColorPropertiesProp != null ? EditorGUI.GetPropertyHeight(materialColorPropertiesProp, includeChildren: true) : Line;
                var r = rect;
                r.height = h;
                if (materialColorPropertiesProp != null)
                    EditorGUI.PropertyField(r, materialColorPropertiesProp, new GUIContent("Extra Color Properties"), includeChildren: true);
                rect.y += h + Gap;
            }
            else if (isMaterialFloat)
            {
                var materialPropertyProp = entry.FindPropertyRelative("materialProperty");
                EditorGUI.PropertyField(NextLine(ref rect), materialPropertyProp, new GUIContent("Material Property"));
            }
        }

        private void DrawTimingSection(ref Rect rect, SerializedProperty entry)
        {
            if (entry == null) return;

            var typeProp = entry.FindPropertyRelative("type");
            var tweenType = (Animate.TweenType)typeProp.enumValueIndex;

            var detectedTypeProp = entry.FindPropertyRelative("detectedPropertyType");
            var propertyModeProp = entry.FindPropertyRelative("propertyMode");
            var methodInvokeTimingProp = entry.FindPropertyRelative("methodInvokeTiming");
            var siblingTimingProp = entry.FindPropertyRelative("siblingTiming");

            string det = detectedTypeProp != null ? detectedTypeProp.stringValue : string.Empty;
            var mode = propertyModeProp != null ? (Animate.CustomPropertyMode)propertyModeProp.enumValueIndex : Animate.CustomPropertyMode.AutoTween;
            var invokeTiming = methodInvokeTimingProp != null ? (Animate.MethodInvokeTiming)methodInvokeTimingProp.enumValueIndex : Animate.MethodInvokeTiming.OnEnd;
            var siblingInvoke = siblingTimingProp != null ? (Animate.MethodInvokeTiming)siblingTimingProp.enumValueIndex : Animate.MethodInvokeTiming.OnEnd;

            var delayModeProp = entry.FindPropertyRelative("delayMode");
            var delayValueProp = entry.FindPropertyRelative("delayValue");
            var durationProp = entry.FindPropertyRelative("duration");
            var curveProp = entry.FindPropertyRelative("curve");

            DrawSectionHeader(ref rect, "Timing");

            EditorGUI.PropertyField(NextLine(ref rect), delayModeProp, new GUIContent("Delay Mode"));

            var delayMode = delayModeProp != null ? (Animate.DelayMode)delayModeProp.enumValueIndex : Animate.DelayMode.None;
            if (delayMode == Animate.DelayMode.Frames)
                EditorGUI.PropertyField(NextLine(ref rect), delayValueProp, new GUIContent("Delay (frames)"));
            else if (delayMode == Animate.DelayMode.Seconds)
                EditorGUI.PropertyField(NextLine(ref rect), delayValueProp, new GUIContent("Delay (s)"));

            if (tweenType == Animate.TweenType.SiblingOrder && mode == Animate.CustomPropertyMode.AutoTween)
                EditorGUI.PropertyField(NextLine(ref rect), siblingTimingProp, new GUIContent("Apply Timing"));

            bool durationRelevant = !(tweenType == Animate.TweenType.CustomProperty && string.Equals(det, "Void", StringComparison.Ordinal) && invokeTiming == Animate.MethodInvokeTiming.OnStart);
            if (tweenType == Animate.TweenType.SiblingOrder)
            {
                if (mode == Animate.CustomPropertyMode.AutoTween && siblingInvoke == Animate.MethodInvokeTiming.OnStart)
                    durationRelevant = false;
            }
            if (durationRelevant)
                EditorGUI.PropertyField(NextLine(ref rect), durationProp, new GUIContent("Duration (s)"));

            bool curveRelevant = !string.Equals(det, "String", StringComparison.Ordinal) && UsesCurve(tweenType, det, mode, invokeTiming, siblingInvoke);
            if (curveRelevant)
                EditorGUI.PropertyField(NextLine(ref rect), curveProp, new GUIContent("Curve"));

            var debugLoggingProp = entry.FindPropertyRelative("debugLogging");
            if (debugLoggingProp != null)
                EditorGUI.PropertyField(NextLine(ref rect), debugLoggingProp, new GUIContent("Debug Logging", "Enable Console logs for this tween only (play start/stop + CustomProperty warnings)."));
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

        private static bool IsBooleanDetectedType(string det)
        {
            if (string.IsNullOrEmpty(det)) return false;
            if (det.Equals("Boolean", StringComparison.OrdinalIgnoreCase)) return true;
            if (det.Equals("System.Boolean", StringComparison.Ordinal)) return true;
            return det.EndsWith(".Boolean", StringComparison.Ordinal);
        }

        private void DrawBrowseRefreshRow(ref Rect rect, SerializedProperty targetObjectProp, SerializedProperty targetComponentProp, SerializedProperty propertyNameProp, SerializedProperty detectedTypeProp)
        {
            Rect row = NextLine(ref rect);

            GameObject go = null;
            Component comp = null;
            try { go = targetObjectProp != null ? targetObjectProp.objectReferenceValue as GameObject : null; } catch { }
            try { comp = targetComponentProp != null ? targetComponentProp.objectReferenceValue as Component : null; } catch { }

            UnityEngine.Object root = comp != null ? (UnityEngine.Object)comp : (UnityEngine.Object)go;

            var browseRect = row;
            browseRect.width = 80f;

            var refreshRect = row;
            refreshRect.xMin = browseRect.xMax + 4f;
            refreshRect.width = 110f;

            using (new EditorGUI.DisabledScope(root == null))
            {
                if (GUI.Button(browseRect, "Browse"))
                {
                    if (comp != null)
                    {
                        MemberPathBrowserWindow.Show(comp, 3, selected =>
                        {
                            try
                            {
                                serializedObject.Update();
                                if (propertyNameProp != null) propertyNameProp.stringValue = selected.path;
                                if (detectedTypeProp != null) detectedTypeProp.stringValue = selected.typeName;
                                serializedObject.ApplyModifiedProperties();
                            }
                            catch { }
                        });
                    }
                    else if (go != null)
                    {
                        var components = go.GetComponents<Component>();
                        if (components != null && components.Length > 0)
                        {
                            var menu = new GenericMenu();
                            foreach (var c in components)
                            {
                                var compLocal = c;
                                if (compLocal == null) continue;
                                menu.AddItem(new GUIContent(compLocal.GetType().Name), false, () =>
                                {
                                    MemberPathBrowserWindow.Show(compLocal, 3, selected =>
                                    {
                                        try
                                        {
                                            serializedObject.Update();
                                            if (targetComponentProp != null) targetComponentProp.objectReferenceValue = compLocal;
                                            if (propertyNameProp != null) propertyNameProp.stringValue = selected.path;
                                            if (detectedTypeProp != null) detectedTypeProp.stringValue = selected.typeName;
                                            serializedObject.ApplyModifiedProperties();
                                        }
                                        catch { }
                                    });
                                });
                            }
                            menu.ShowAsContext();
                        }
                    }
                }

                if (GUI.Button(refreshRect, "Refresh Type"))
                {
                    try
                    {
                        string path = propertyNameProp != null ? propertyNameProp.stringValue : null;
                        var t = (root != null && !string.IsNullOrEmpty(path)) ? MemberPathBrowser.ResolveMemberType(root, path) : null;
                        if (detectedTypeProp != null)
                            detectedTypeProp.stringValue = t != null ? t.Name : string.Empty;
                    }
                    catch { }
                }
            }
        }

        private static void DrawSectionHeader(ref Rect rect, string title)
        {
            var r = NextLine(ref rect);
            EditorGUI.LabelField(r, title, EditorStyles.boldLabel);
        }

        private static Rect NextLine(ref Rect rect)
        {
            var r = rect;
            r.height = Line;
            rect.y += Line + Gap;
            return r;
        }

        private static Rect Inset(Rect r, float pad)
        {
            r.xMin += pad;
            r.xMax -= pad;
            r.yMin += pad;
            r.yMax -= pad;
            return r;
        }

        private static void DrawWrappedTextArea(ref Rect rect, SerializedProperty stringProp, string label)
        {
            if (stringProp == null) return;

            // label line
            EditorGUI.LabelField(NextLine(ref rect), label);

            float h = Mathf.Max(Line * 2f, 54f);
            var area = rect;
            area.height = h;

            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            stringProp.stringValue = EditorGUI.TextArea(area, stringProp.stringValue ?? string.Empty, style);
            rect.y += h + Gap;
        }

        private void ResetTweenEntry(SerializedProperty entry)
        {
            if (entry == null) return;

            // Best-effort defaults; mirrors Animate.TweenEntry field initializers.
            SetBool(entry, "chainAfterPrevious", false);
            SetString(entry, "name", string.Empty);

            SetEnum(entry, "type", (int)Animate.TweenType.Position);

            SetObject(entry, "targetObject", null);
            SetObject(entry, "targetComponent", null);

            SetEnum(entry, "startSource", (int)Animate.StartSource.Ignore);
            SetBool(entry, "local", true);

            SetVector3(entry, "fromVec3", Vector3.zero);
            SetVector3(entry, "toVec3", Vector3.zero);

            SetColor(entry, "fromColor", Color.white);
            SetColor(entry, "toColor", Color.white);

            SetFloat(entry, "fromFloat", 0f);
            SetFloat(entry, "toFloat", 1f);

            SetBool(entry, "fromBool", false);
            SetBool(entry, "toBool", true);

            SetString(entry, "fromString", string.Empty);
            SetString(entry, "toString", string.Empty);
            SetBool(entry, "typerAppend", false);

            SetString(entry, "propertyName", string.Empty);
            SetEnum(entry, "propertyMode", (int)Animate.CustomPropertyMode.AutoTween);
            SetEnum(entry, "methodInvokeTiming", (int)Animate.MethodInvokeTiming.OnEnd);
            SetString(entry, "detectedPropertyType", string.Empty);

            SetEnum(entry, "vectorMask", (int)Animate.ComponentMask.All);
            SetEnum(entry, "enumFieldMask", (int)Animate.ComponentMask.None);

            SetInt(entry, "materialIndex", 0);
            SetString(entry, "materialProperty", "_Glossiness");
            var arr = entry.FindPropertyRelative("materialColorProperties");
            if (arr != null && arr.isArray)
                arr.arraySize = 0;

            SetEnum(entry, "delayMode", (int)Animate.DelayMode.None);
            SetFloat(entry, "delayValue", 0f);
            SetFloat(entry, "duration", 1f);

            SetEnum(entry, "siblingShift", (int)Animate.SiblingShift.Up);
            SetEnum(entry, "siblingTiming", (int)Animate.MethodInvokeTiming.OnEnd);

            SetBool(entry, "debugLogging", false);

            // curve: leave whatever Unity assigns; if null it will serialize as empty.
        }

        private static string ExpandedKey(UnityEngine.Object o, int index)
        {
            int id = o != null ? o.GetInstanceID() : 0;
            return $"Animate.LegacyEditor.Expanded.{id}.{index}";
        }

        private bool GetExpanded(int index)
        {
            return SessionState.GetBool(ExpandedKey(target, index), true);
        }

        private void SetExpanded(int index, bool expanded)
        {
            SessionState.SetBool(ExpandedKey(target, index), expanded);
        }

        private static void SetBool(SerializedProperty root, string rel, bool v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.boolValue = v;
        }

        private static void SetInt(SerializedProperty root, string rel, int v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.intValue = v;
        }

        private static void SetFloat(SerializedProperty root, string rel, float v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.floatValue = v;
        }

        private static void SetString(SerializedProperty root, string rel, string v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.stringValue = v;
        }

        private static void SetEnum(SerializedProperty root, string rel, int v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p == null) return;

            // Most enums use enumValueIndex.
            // Flag enums (e.g., ComponentMask) serialize as an int; enumValueIndex can't represent bitmasks.
            try
            {
                if (p.propertyType == SerializedPropertyType.Enum && p.enumNames != null && v >= 0 && v < p.enumNames.Length)
                {
                    p.enumValueIndex = v;
                }
                else
                {
                    // Best-effort: set the underlying value for flag enums / out-of-range indices.
                    p.intValue = v;
                }
            }
            catch
            {
                // Last resort: clamp to a valid index.
                try
                {
                    if (p.propertyType == SerializedPropertyType.Enum && p.enumNames != null && p.enumNames.Length > 0)
                        p.enumValueIndex = Mathf.Clamp(v, 0, p.enumNames.Length - 1);
                }
                catch { }
            }
        }

        private static void SetObject(SerializedProperty root, string rel, UnityEngine.Object v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.objectReferenceValue = v;
        }

        private static void SetVector3(SerializedProperty root, string rel, Vector3 v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.vector3Value = v;
        }

        private static void SetColor(SerializedProperty root, string rel, Color v)
        {
            var p = root.FindPropertyRelative(rel);
            if (p != null) p.colorValue = v;
        }
    }
#endif
}
#endif
