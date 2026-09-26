
using Aura.Editor.Serialization;
using Aura.Editor.Tools;
using Aura.Editor.Utilities;
using Aura.Internal.Rendering;
using Aura.Internal.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Aura.Editor.GUIs.AuraGUI;
using static Aura.Editor.Serialization.Wrappers;

namespace Aura.Editor.GUIs
{
    internal static class AuraRenderingEditors
    {
        private const string Visuals = "Visuals";
        private const string Body = "Body";

        public static void DrawBodyVisualsUI(float minHalfSize, _UIBlock2DData uiNode2DData, _Surface surface, _BaseRenderInfo baseInfo, ref ImageSelectionType imageMode, ref UIBlock2DData.Calculated calc)
        {
            using (Foldout bodyFoldout = AuraGUI.EditorPrefFoldoutHeader(Body, uiNode2DData.FillEnabledProp))
            {
                if (bodyFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

                    DrawUIBlock2DUI(uiNode2DData, ref calc);
                    DrawImageUI(uiNode2DData, ref imageMode);
                }
            }

            using (Foldout visualsFoldout = AuraGUI.EditorPrefFoldoutHeader(Visuals))
            {
                if (visualsFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

                    AuraGUI.ToggleField(Labels.Rendering.Visible, baseInfo.VisibleProp);
                    AuraEditorPrefs.DisplaySidesCornerRadius = LengthCornerRadiusRollout(
                        Labels.UIBlock2D.CornerRadius,
                        uiNode2DData.CornerRadius,
                        uiNode2DData.CornerRadii,
                        uiNode2DData.UseIndividualCornerRadiiProp,
                        calc.CornerRadius,
                        calc.CornerRadii,
                        min: 0,
                        max: minHalfSize,
                        AuraEditorPrefs.DisplaySidesCornerRadius,
                        uiNode2DData.SerializedProperty.FindPropertyRelative("InvertedCorners"));
                    DrawRadialFillUI(uiNode2DData.RadialFill, calc.RadialFill);
                    DrawBaseInfoUI(baseInfo);
                    AuraGUI.ToggleField(Labels.UIBlock2D.SoftenEdges, uiNode2DData.SoftenEdgesProp);
                    DrawSurfaceUI(surface, false);
                }
            }
        }

        /// <summary>
        /// Text
        /// </summary>
        /// <param name="baseInfo"></param>
        /// <param name="surface"></param>
        public static void DrawBodyVisualsUI(_BaseRenderInfo baseInfo, _Surface surface, TMPProperties tmpProps)
        {
            using (Foldout bodyFoldout = AuraGUI.EditorPrefFoldoutHeader(Body))
            {
                if (bodyFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                    TextBlock textBlock = baseInfo.SerializedProperty.serializedObject.targetObject as TextBlock;
                    TMPFields(tmpProps, baseInfo.SerializedProperty.serializedObject);
                }
            }

            using (Foldout visualsFoldout = AuraGUI.EditorPrefFoldoutHeader(Visuals))
            {
                if (visualsFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                    AuraGUI.ToggleField(Labels.Rendering.Visible, baseInfo.VisibleProp);
                    DrawBaseInfoUI(baseInfo);
                    DrawSurfaceUI(surface, false);
                }
            }
        }

        public static void DrawBodyVisualsUI(Vector3 size, _UIBlock3DData uiNode3DData, _Surface surface, _BaseRenderInfo baseInfo, ref UIBlock3DData.Calculated calc)
        {
            using (Foldout bodyFoldout = AuraGUI.EditorPrefFoldoutHeader(Body))
            {
                if (bodyFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
                    ColorField(Labels.UIBlock3D.Color, uiNode3DData.ColorProp);
                }
            }

            using (Foldout visualsFoldout = AuraGUI.EditorPrefFoldoutHeader(Visuals))
            {
                if (visualsFoldout)
                {
                    using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

                    AuraGUI.ToggleField(Labels.Rendering.Visible, baseInfo.VisibleProp);
                    float minXY = Mathf.Min(size.x, size.y);
                    AuraEditorPrefs.DisplaySidesCornerRadius = LengthCornerRadiusRollout(
                        Labels.UIBlock3D.CornerRadius,
                        uiNode3DData.CornerRadius,
                        uiNode3DData.CornerRadii,
                        uiNode3DData.UseIndividualCornerRadiiProp,
                        calc.CornerRadius,
                        calc.CornerRadii,
                        min: 0,
                        max: 0.5f * minXY,
                        AuraEditorPrefs.DisplaySidesCornerRadius);
                    AuraGUI.LengthField(AuraGUI.Layout.GetControlRect(), Labels.UIBlock3D.EdgeRadius, uiNode3DData.EdgeRadius, calc.EdgeRadius, min: 0, max: 0.5f * Mathf.Min(minXY, size.z));
                    DrawSurfaceUI(surface, true);
                }
            }
        }

        public static void DrawRadialFillUI(_RadialFill radialFill, RadialFill.Calculated calc)
        {
            AuraGUI.Layout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            bool expand = AuraGUI.PrefixFoldout(AuraEditorPrefs.DisplayExpandedRadialFill);
            if (EditorGUI.EndChangeCheck())
            {
                AuraEditorPrefs.DisplayExpandedRadialFill = expand;
            }

            Rect toggleRect = AuraGUI.Layout.GetControlRect();
            toggleRect.x -= Foldout.ArrowIconSize + AuraGUI.MinSpaceBetweenFields;
            AuraGUI.ToggleField(toggleRect, Labels.RadialFill.Enabled, radialFill.EnabledProp);

            AuraGUI.Layout.EndHorizontal();

            if (!expand)
            {
                return;
            }


            AuraGUI.Layout.BeginHorizontal(AuraGUI.Styles.InnerContent);
            AuraGUI.Space(4f / 3f);
            AuraGUI.Layout.BeginVertical();

            EditorGUI.BeginDisabledGroup(!radialFill.Enabled);

            AuraGUI.Length2Field(Labels.RadialFill.Center, radialFill.Center, calc.Center, MinMax2.Unclamped.Min, MinMax2.Unclamped.Max);
            AuraGUI.SliderField(Labels.RadialFill.Rotation, radialFill.RotationProp, min: -360f, max: 360f);
            AuraGUI.SliderField(Labels.RadialFill.FillAngle, radialFill.FillAngleProp, min: -360f, max: 360f);

            EditorGUI.EndDisabledGroup();

            AuraGUI.Layout.EndVertical();
            AuraGUI.Layout.EndHorizontal();
        }

        public static void DrawBorderUI(_Border borderData, Border.Calculated calc)
        {
            using (Foldout foldout = AuraGUI.EditorPrefFoldoutHeader("Border", borderData.EnabledProp))
            {
                using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                {
                    if (foldout)
                    {
                        ColorField(Labels.Border.Color, borderData.ColorProp);

                        // Width
                        AuraGUI.LengthField(AuraGUI.Layout.GetControlRect(), Labels.Border.Width, borderData.Width, calc.Width, min: 0);

                        // Direction
                        Rect directionPosition = AuraGUI.Layout.GetControlRect();
                        GUIContent label = EditorGUI.BeginProperty(directionPosition, Labels.Border.Direction, borderData.DirectionProp);
                        EditorGUI.BeginChangeCheck();
                        BorderDirection strokeDirection = (BorderDirection)EditorGUI.EnumPopup(directionPosition, label, borderData.Direction);
                        if (EditorGUI.EndChangeCheck())
                        {
                            borderData.Direction = strokeDirection;
                        }
                        EditorGUI.EndProperty();
                        DrawBorderSegments(borderData.SerializedProperty);
                    }
                }
            }
        }

        private static void DrawBorderSegments(SerializedProperty border)
        {
            string[] names = { "TopLeft", "Top", "TopRight", "Left", null, "Right", "BottomLeft", "Bottom", "BottomRight" };
            Rect grid = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 3);
            EditorGUI.LabelField(new Rect(grid.x, grid.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight), "Segments");
            grid.xMin += EditorGUIUtility.labelWidth;
            for (int i = 0; i < names.Length; ++i)
            {
                if (names[i] == null) continue;
                SerializedProperty disabled = border.FindPropertyRelative("disable" + names[i]);
                Rect cell = new Rect(grid.x + (i % 3) * 24, grid.y + (i / 3) * EditorGUIUtility.singleLineHeight, 20, EditorGUIUtility.singleLineHeight);
                GUIContent label = new GUIContent("", ObjectNames.NicifyVariableName(names[i]));
                EditorGUI.BeginProperty(cell, label, disabled);
                using (new EditorGUI.MixedValueScope(disabled.hasMultipleDifferentValues))
                {
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUI.Toggle(cell, label, !disabled.boolValue);
                    if (EditorGUI.EndChangeCheck()) disabled.boolValue = !enabled;
                }
                EditorGUI.EndProperty();
            }
        }

        public static void DrawShadowUI(_UIBlock2DData renderData, Shadow.Calculated calc)
        {
            using (Foldout foldout = AuraGUI.EditorPrefFoldoutHeader("Shadow", renderData.Shadow.EnabledProp))
            {
                if (!foldout)
                {
                    return;
                }

                using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);


                _Shadow shadowData = renderData.Shadow;
                Rect fieldRect = AuraGUI.Layout.GetControlRect();
                EditorGUI.BeginChangeCheck();
                GUIContent label = EditorGUI.BeginProperty(fieldRect, Labels.Shadow.Direction, shadowData.DirectionProp);
                ShadowDirection newDirection = (ShadowDirection)EditorGUI.EnumPopup(fieldRect, label, shadowData.Direction);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    shadowData.Direction = newDirection;
                }

                AuraGUI.ColorField(Labels.Shadow.Color, shadowData.ColorProp);
                AuraGUI.LengthField(AuraGUI.Layout.GetControlRect(), Labels.Shadow.Width, shadowData.Width, calc.Width);
                AuraGUI.LengthField(AuraGUI.Layout.GetControlRect(), Labels.Shadow.Blur, shadowData.Blur, calc.Blur, min: 0);
                AuraGUI.Length2Field(Labels.Shadow.Offset, shadowData.Offset, calc.Offset, MinMax2.Unclamped.Min, MinMax2.Unclamped.Max);
            }
        }

        public static void DrawBaseInfoUI(_BaseRenderInfo baseInfo)
        {
            using var indent = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);

            Rect baseInfoField = AuraGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent propertyLabel = EditorGUI.BeginProperty(baseInfoField, Labels.Rendering.ZIndex, baseInfo.ZIndexProp);
            short newRenderLayer = (short)EditorGUI.IntField(baseInfoField, propertyLabel, baseInfo.ZIndexProp.intValue);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                baseInfo.ZIndexProp.intValue = newRenderLayer;
            }
        }

        public static void DrawUIBlock2DUI(_UIBlock2DData data, ref UIBlock2DData.Calculated calc)
        {
            EditorGUI.BeginChangeCheck();
            AuraGUI.Layout.BeginVertical();
            AuraGUI.Layout.BeginHorizontal();
            Rect labelRect = AuraGUI.Layout.GetControlRect(GUILayout.Width(Foldout.ArrowIconSize));
            bool expandedColor = Foldout.FoldoutToggle(labelRect, AuraEditorPrefs.DisplayExpandedColor);
            if (EditorGUI.EndChangeCheck())
            {
                AuraEditorPrefs.DisplayExpandedColor = expandedColor;
            }

            AuraGUI.Space(-AuraGUI.Layout.FoldoutArrowIndentSpace);
            AuraGUI.ColorField(Labels.UIBlock2D.Color, data.ColorProp);
            AuraGUI.Layout.EndHorizontal();

            EditorGUILayout.Space(1);

            if (expandedColor)
            {
                GradientField(data.Gradient, calc.Gradient);
            }

            AuraGUI.Layout.EndVertical();
        }

        public static void DrawImageUI(_UIBlock2DData uiNode2DData, ref ImageSelectionType imageMode)
        {
            SerializedObject serializedObject = uiNode2DData.SerializedProperty.serializedObject;
            SerializedProperty textureProp = serializedObject.FindProperty(Names.UIBlock2D.texture);
            SerializedProperty spriteProp = serializedObject.FindProperty(Names.UIBlock2D.sprite);

            bool expandedImage = AuraEditorPrefs.DisplayExpandedImage;

            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                EditorGUI.BeginChangeCheck();
                AuraGUI.Layout.BeginVertical();
                AuraGUI.Layout.BeginHorizontal();
                Rect imageFieldRect = AuraGUI.Layout.GetControlRect();
                expandedImage = Foldout.FoldoutToggle(imageFieldRect, expandedImage);
                if (EditorGUI.EndChangeCheck())
                {
                    AuraEditorPrefs.DisplayExpandedImage = expandedImage;
                }

                imageFieldRect.xMax -= AuraGUI.ToggleToolbarFieldWidth + AuraGUI.MinSpaceBetweenFields;

                // The texture/sprite
                switch (imageMode)
                {
                    case ImageSelectionType.Texture:
                        {
                            _ImageAdjustment adjustment = uiNode2DData.Image.Adjustment;
                            if (adjustment.scaleMode == ImageScaleMode.Sliced && !adjustment.scaleModeProp.hasMultipleDifferentValues)
                            {
                                Rect warningRect = imageFieldRect;
                                warningRect.xMin += EditorGUIUtility.labelWidth;
                                AuraGUI.WarningIcon(warningRect, Labels.Image.SlicedWarningTooltip);
                            }

                            EditorGUI.BeginChangeCheck();
                            GUIContent label = EditorGUI.BeginProperty(imageFieldRect, Labels.Image.Label, textureProp);
                            Texture newVal = EditorGUI.ObjectField(imageFieldRect, label, textureProp.objectReferenceValue, typeof(Texture), false) as Texture;
                            EditorGUI.EndProperty();
                            bool changed = EditorGUI.EndChangeCheck();
                            if (!changed)
                            {
                                break;
                            }

                            if (newVal == null)
                            {
                                textureProp.objectReferenceValue = null;
                            }
                            else if (newVal is Texture2D || newVal is RenderTexture)
                            {
                                textureProp.objectReferenceValue = newVal;
                            }
                            else
                            {
                                Debug.LogError("Unsupported texture type. Texture must be a Texture2D or RenderTexture");
                                textureProp.objectReferenceValue = null;
                            }

                            break;
                        }
                    case ImageSelectionType.Sprite:
                        {
                            EditorGUI.BeginChangeCheck();
                            GUIContent label = EditorGUI.BeginProperty(imageFieldRect, Labels.Image.Label, spriteProp);
                            Sprite newVal = EditorGUI.ObjectField(imageFieldRect, label, spriteProp.objectReferenceValue, typeof(Sprite), false) as Sprite;
                            EditorGUI.EndProperty();
                            bool changed = EditorGUI.EndChangeCheck();
                            if (!changed)
                            {
                                break;
                            }

                            spriteProp.objectReferenceValue = newVal;

                            if (!(newVal is Sprite newSprite))
                            {
                                break;
                            }
                            break;
                        }
                }

                // Sprite vs Texture selector
                Rect toolbarRect = imageFieldRect;
                toolbarRect.x = toolbarRect.xMax + AuraGUI.MinSpaceBetweenFields;
                toolbarRect.width = AuraGUI.ToggleToolbarFieldWidth;
                EditorGUI.BeginChangeCheck();
                Rect toolbarPropertyRect = toolbarRect;
                toolbarPropertyRect.width += AuraGUI.SingleCharacterGUIWidth;
                ImageSelectionType newImageMode = AuraGUI.Toolbar(toolbarRect, imageMode, Labels.Image.TypeLabels);
                bool imageTypeChanged = EditorGUI.EndChangeCheck() && newImageMode != imageMode;
                AuraGUI.Layout.EndHorizontal();
                AuraGUI.Layout.EndVertical();

                if (imageTypeChanged)
                {
                    textureProp.objectReferenceValue = null;
                    spriteProp.objectReferenceValue = null;
                    uiNode2DData.Image.Adjustment.UVScale = Vector2.one;
                    uiNode2DData.Image.Adjustment.CenterUV = Vector2.zero;
                    uiNode2DData.Image.Adjustment.Rotation = 0f;
                    imageMode = newImageMode;
                }

                if (expandedImage)
                {
                    EditorGUILayout.Space(1);
                    AuraGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                    AuraGUI.Layout.BeginHorizontal(AuraGUI.Styles.InnerContent);
                    AuraGUI.Space(1.5f);
                    AuraGUI.Layout.BeginVertical();

                    // Scale Mode
                    Rect scaleModeRect = AuraGUI.Layout.GetControlRect();
                    EditorGUI.BeginChangeCheck();
                    GUIContent scaleModeLabel = EditorGUI.BeginProperty(scaleModeRect, Labels.Image.ImageScaleMode, uiNode2DData.Image.Adjustment.scaleModeProp);
                    ImageScaleMode newScaleMode = (ImageScaleMode)EditorGUI.EnumPopup(scaleModeRect, scaleModeLabel, uiNode2DData.Image.Adjustment.scaleMode);
                    EditorGUI.EndProperty();
                    if (EditorGUI.EndChangeCheck())
                    {
                        uiNode2DData.Image.Adjustment.UVScale = Vector2.one;
                        uiNode2DData.Image.Adjustment.CenterUV = Vector2.zero;
                        uiNode2DData.Image.Adjustment.scaleMode = newScaleMode;
                    }

                    if (newScaleMode == ImageScaleMode.Manual)
                    {
                        AuraGUI.Vector2Field(Labels.Image.ImageCenter, uiNode2DData.Image.Adjustment.CenterUVProp);
                        AuraGUI.Vector2Field(Labels.Image.ImageScale, uiNode2DData.Image.Adjustment.UVScaleProp);
                        AuraGUI.FloatField(Labels.Image.ImageRotation, uiNode2DData.Image.Adjustment.RotationProp);
                    }
                    else if (newScaleMode == ImageScaleMode.Sliced || newScaleMode == ImageScaleMode.Tiled)
                    {
                        AuraGUI.FloatFieldClamped(Labels.Image.PixelsPerUnit, uiNode2DData.Image.Adjustment.PixelsPerUnitMultiplierProp, .01f, float.MaxValue);
                    }

                    else if (newScaleMode == ImageScaleMode.Fill)
                    {
                        // Show Fill axis selector and pixels-per-unit for Fill mode
                        Rect fillAxisField = AuraGUI.Layout.GetControlRect();
                        EditorGUI.BeginChangeCheck();
                        GUIContent fillAxisLabel = EditorGUI.BeginProperty(fillAxisField, Labels.Image.FillAxis, uiNode2DData.Image.Adjustment.fillAxisProp);
                        Aura.ImageFillAxis startFillAxis = uiNode2DData.Image.Adjustment.fillAxis;
                        Aura.ImageFillAxis newFillAxis = (Aura.ImageFillAxis)EditorGUI.EnumPopup(fillAxisField, fillAxisLabel, startFillAxis);
                        EditorGUI.EndProperty();
                        if (EditorGUI.EndChangeCheck())
                        {
                            uiNode2DData.Image.Adjustment.fillAxis = newFillAxis;
                        }

                        AuraGUI.FloatFieldClamped(Labels.Image.PixelsPerUnit, uiNode2DData.Image.Adjustment.PixelsPerUnitMultiplierProp, .01f, float.MaxValue);
                    }

                    if (AuraSettings.PackedImagesEnabled)
                    {
                        Rect renderModeField = AuraGUI.Layout.GetControlRect();
                        EditorGUI.BeginChangeCheck();
                        GUIContent renderModeLabel = EditorGUI.BeginProperty(renderModeField, Labels.Image.ImageMode, uiNode2DData.Image.ModeProp);
                        ImagePackMode startRenderingMode = uiNode2DData.Image.Mode;
                        ImagePackMode newRenderMode = (ImagePackMode)EditorGUI.EnumPopup(renderModeField, renderModeLabel, startRenderingMode);
                        EditorGUI.EndProperty();
                        if (EditorGUI.EndChangeCheck() && startRenderingMode != newRenderMode)
                        {
                            uiNode2DData.Image.Mode = newRenderMode;
                        }
                    }


                    AuraGUI.Layout.EndVertical();
                    AuraGUI.Layout.EndHorizontal();
                }


            }
        }

        private static void DrawDisabledSurfaceUI()
        {
            AuraGUI.WarningIcon(Labels.Surface.DisabledSurfaceSRPWarning);

            EditorGUI.BeginDisabledGroup(true);

            Rect presetRect = AuraGUI.Layout.GetControlRect();
            EditorGUI.EnumPopup(presetRect, Labels.Surface.SurfaceEffect, SurfacePreset.Unlit);

            EditorGUI.EndDisabledGroup();
        }

        private static void DrawSurfaceUI(_Surface surface, bool canReceiveShadows)
        {
            if (SystemSettings.UsingScriptableRenderPipeline)
            {
                DrawDisabledSurfaceUI();
                return;
            }

            AuraGUI.Layout.BeginHorizontal();

            bool expandedSurface = false;

            if (surface.LightingModel != LightingModel.Unlit)
            {
                EditorGUI.BeginChangeCheck();
                Rect labelRect = AuraGUI.Layout.GetControlRect(GUILayout.Width(Foldout.ArrowIconSize));
                expandedSurface = Foldout.FoldoutToggle(labelRect, AuraEditorPrefs.DisplayExpandedSurface);
                if (EditorGUI.EndChangeCheck())
                {
                    AuraEditorPrefs.DisplayExpandedSurface = expandedSurface;
                }

                AuraGUI.Space(-1f);
            }

            SurfacePreset preset = SurfaceDrawer.GetApproximatePreset(surface);

            Rect presetRect = AuraGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            bool showMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = surface.SerializedProperty.hasMultipleDifferentValues;
            preset = (SurfacePreset)EditorGUI.EnumPopup(presetRect, Labels.Surface.SurfaceEffect, preset);
            EditorGUI.showMixedValue = showMixed;
            if (EditorGUI.EndChangeCheck())
            {
                SurfaceDrawer.SetPreset(preset, surface);
            }

            AuraGUI.Layout.EndHorizontal();

            if (!expandedSurface || preset == SurfacePreset.Unlit || surface.LightingModelProp.hasMultipleDifferentValues)
            {
                return;
            }

            AuraGUI.Layout.BeginHorizontal(Styles.InnerContent);
            AuraGUI.Space(1.5f);
            AuraGUI.Layout.BeginVertical();

            Rect fieldRect = AuraGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent lightingModelLabel = EditorGUI.BeginProperty(fieldRect, Labels.Surface.LightingModel, surface.LightingModelProp);
            LightingModel newLightingModel = (LightingModel)EditorGUI.EnumPopup(fieldRect, lightingModelLabel, surface.LightingModel);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                SurfaceDrawer.SetLightingModel(surface, newLightingModel);
            }

            Rect shadowCastRect = AuraGUI.Layout.GetControlRect();
            EditorGUI.BeginChangeCheck();
            GUIContent shadowCastingLabel = EditorGUI.BeginProperty(shadowCastRect, Labels.Surface.ShadowCasting, surface.ShadowCastingModeProp);
            ShadowCastingMode newShadowCasting = (ShadowCastingMode)EditorGUI.EnumPopup(shadowCastRect, shadowCastingLabel, surface.ShadowCastingMode);
            EditorGUI.EndProperty();
            if (EditorGUI.EndChangeCheck())
            {
                surface.ShadowCastingMode = newShadowCasting;
            }

            if (canReceiveShadows)
            {
                Rect receiveShadowsRect = AuraGUI.Layout.GetControlRect();
                EditorGUI.BeginChangeCheck();
                GUIContent receiveShadowsLabel = EditorGUI.BeginProperty(receiveShadowsRect, Labels.Surface.ReceiveShadows, surface.ReceiveShadowsProp);
                bool recvShadows = EditorGUI.Toggle(receiveShadowsRect, receiveShadowsLabel, surface.ReceiveShadows);
                EditorGUI.EndProperty();
                if (EditorGUI.EndChangeCheck())
                {
                    surface.ReceiveShadows = recvShadows;
                }
            }


            switch (newLightingModel)
            {
                case LightingModel.Lambert:
                    // Do nothing
                    break;
                case LightingModel.BlinnPhong:
                    AuraGUI.SliderField(Labels.Surface.Specular, surface.param1Prop, 0, 1);
                    AuraGUI.SliderField(Labels.Surface.Gloss, surface.param2Prop, 0, 1);
                    break;
                case LightingModel.Standard:
                    AuraGUI.SliderField(Labels.Surface.Metallic, surface.param2Prop, 0, 1);
                    AuraGUI.SliderField(Labels.Surface.Smoothness, surface.param1Prop, 0, 1);
                    break;
                case LightingModel.StandardSpecular:
                    AuraGUI.ColorField(Labels.Surface.SpecularColor, surface.specularColorProp, false);
                    AuraGUI.SliderField(Labels.Surface.Smoothness, surface.param1Prop, 0, 1);
                    break;
                default:
                    break;
            }

            AuraGUI.Layout.EndVertical();
            AuraGUI.Layout.EndHorizontal();
        }

        private static void GradientField(_RadialGradient gradientData, RadialGradient.Calculated calc)
        {
            AuraGUI.Layout.BeginHorizontal(AuraGUI.Styles.InnerContent);
            AuraGUI.Layout.BeginVertical();
            AuraGUI.Layout.BeginHorizontal();

            AuraGUI.Space(4f / 3f);

            EditorGUI.BeginChangeCheck();
            bool expandGradient = AuraGUI.PrefixFoldout(AuraEditorPrefs.DisplayExpandedGradient);
            if (EditorGUI.EndChangeCheck())
            {
                AuraEditorPrefs.DisplayExpandedGradient = expandGradient;
            }

            EditorGUI.BeginChangeCheck();

            Rect controlRect = AuraGUI.Layout.GetControlRect();
            Rect toggleRect = controlRect;
            toggleRect.width = AuraGUI.LabelWidth;
            toggleRect.width += AuraGUI.ToggleBoxSize;
            toggleRect.x -= AuraGUI.IndentSize + AuraGUI.MinSpaceBetweenFields;

            Rect colorFieldRect = controlRect;
            colorFieldRect.xMin = toggleRect.xMax;

            GUIContent gradientLabel = EditorGUI.BeginProperty(toggleRect, Labels.Gradient.Label, gradientData.EnabledProp);
            gradientData.Enabled = EditorGUI.Toggle(toggleRect, gradientLabel, gradientData.Enabled);
            EditorGUI.EndProperty();

            EditorGUI.BeginDisabledGroup(!gradientData.Enabled);

            AuraGUI.ColorField(colorFieldRect, Labels.Gradient.Color, gradientData.ColorProp);
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck() && gradientData.Enabled)
            {
                // Need to apply modifications because in 2022 will throw an error
                // if you try to set the tool when IsAvailable return false
                gradientData.ApplyModifications();
                UnityEditor.EditorTools.ToolManager.SetActiveTool<GradientTool>();
            }

            AuraGUI.Layout.EndHorizontal();

            if (expandGradient)
            {
                AuraGUI.Space(2 / AuraGUI.IndentSize);
                AuraGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                AuraGUI.Layout.BeginHorizontal(AuraGUI.Styles.InnerContent);
                AuraGUI.Space(2.5f);
                AuraGUI.Layout.BeginVertical();
                EditorGUI.BeginDisabledGroup(!gradientData.Enabled);

                AuraGUI.Length2Field(Labels.Gradient.Center, gradientData.Center, calc.Center, MinMax2.Unclamped.Min, MinMax2.Unclamped.Max);
                AuraGUI.Length2Field(Labels.Gradient.Radius, gradientData.Radius, calc.Radius, MinMax2.Positive.Min, MinMax2.Positive.Max);
                AuraGUI.FloatField(Labels.Gradient.Rotation, gradientData.RotationProp);

                EditorGUI.EndDisabledGroup();
                AuraGUI.Layout.EndVertical();
                AuraGUI.Layout.EndHorizontal();
            }

            AuraGUI.Layout.EndVertical();
            AuraGUI.Layout.EndHorizontal();
        }

        private static void TMPFields(TMPProperties tmpProps, SerializedObject blockSerializedObject)
        {
            AuraGUI.Layout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool expandTextProperties = AuraGUI.PrefixFoldout(AuraEditorPrefs.DisplayExpandedText);
            AuraGUI.Space(-AuraGUI.Layout.FoldoutArrowIndentSpace);
            if (EditorGUI.EndChangeCheck())
            {
                AuraEditorPrefs.DisplayExpandedText = expandTextProperties;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginChangeCheck();
            Rect textField = AuraGUI.Layout.GetControlRect();

            string text = null;
            using (var scope = tmpProps.TextDiffer ? MixedValueScope.Create() : default)
            {
                text = EditorGUI.TextField(textField, Labels.TMP.Text, tmpProps.Text);
            }

            if (EditorGUI.EndChangeCheck())
            {
                tmpProps.Text = text;
            }

            EditorGUILayout.LabelField(Labels.TMP.Info, GUILayout.Width(AuraGUI.ToggleBoxSize));
            AuraGUI.Layout.EndHorizontal();
            if (expandTextProperties)
            {
                AuraGUI.Styles.DrawSeparator(GUILayoutUtility.GetLastRect());
                AuraGUI.Layout.BeginHorizontal(AuraGUI.Styles.InnerContent);
                AuraGUI.Space(1.5f);
                AuraGUI.Layout.BeginVertical();

                int previousX = tmpProps.HorizontalAlignmentDiffer ? -1000 : AlignmentFromTMPAlignment(tmpProps.HorizontalAlignment);
                int previousY = tmpProps.VerticalAlignmentDiffer ? -1000 : AlignmentFromTMPAlignment(tmpProps.VerticalAlignment);
                EditorGUI.BeginChangeCheck();
                (int xAlignment, int yAlignment) = AuraLayoutEditors.AlignmentField(Labels.TMP.Alignment, previousX, previousY, Labels.TMPAlignment);
                if (EditorGUI.EndChangeCheck())
                {
                    // since we aren't 1:1 with TMP's alignment options, only write when the specific axis field is modified
                    if (xAlignment != previousX)
                    {
                        tmpProps.HorizontalAlignment = AlignmentToTMPHorizontal(xAlignment);
                    }

                    if (yAlignment != previousY)
                    {
                        tmpProps.VerticalAlignment = AlignmentToTMPVertical(yAlignment);
                    }
                }

                EditorGUI.BeginChangeCheck();
                Rect colorField = AuraGUI.Layout.GetControlRect();
                Color color = AuraGUI.ColorField(colorField, Labels.TMP.Color, tmpProps.Color, tmpProps.ColorDiffer);
                if (EditorGUI.EndChangeCheck())
                {
                    tmpProps.Color = color;
                }

                EditorGUI.BeginChangeCheck();
                Rect fontField = AuraGUI.Layout.GetControlRect();
                TMPro.TMP_FontAsset font = null;
                using (var scope = tmpProps.FontDiffer ? MixedValueScope.Create() : default)
                {
                    font = EditorGUI.ObjectField(fontField, Labels.TMP.Font, tmpProps.Font, typeof(TMPro.TMP_FontAsset), allowSceneObjects: false) as TMPro.TMP_FontAsset;
                }
                if (EditorGUI.EndChangeCheck())
                {
                    DoUnsupportedTMPShaderDialogue(font);
                    tmpProps.Font = font;
                }

                EditorGUI.BeginChangeCheck();
                Rect floatField = AuraGUI.Layout.GetControlRect();
                float fontSize = 0f;
                using (var scope = tmpProps.FontSizeDiffer ? MixedValueScope.Create() : default)
                {
                    fontSize = EditorGUI.FloatField(floatField, Labels.TMP.FontSize, tmpProps.FontSize);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    tmpProps.FontSize = fontSize;
                }

                // Draw TextBlock-specific fields: ContentType and Password Mask (show mask only when Password selected)
                if (blockSerializedObject != null)
                {
                    AuraGUI.Space(2 / AuraGUI.IndentSize);
                    SerializedProperty contentTypeProp = blockSerializedObject.FindProperty("contentType");
                    SerializedProperty passwordMaskProp = blockSerializedObject.FindProperty("passwordMask");
                    SerializedProperty useNumberRangeProp = blockSerializedObject.FindProperty("useNumberRange");
                    SerializedProperty numberMinProp = blockSerializedObject.FindProperty("numberMin");
                    SerializedProperty numberMaxProp = blockSerializedObject.FindProperty("numberMax");

                    EditorGUI.BeginChangeCheck();
                    if (contentTypeProp != null)
                    {
                        Rect contentTypeRect = AuraGUI.Layout.GetControlRect();
                        EditorGUI.PropertyField(contentTypeRect, contentTypeProp, new GUIContent("Content Type"));
                    }

                    bool showPasswordMask = true;
                    bool showNumberRange = false;
                    if (contentTypeProp != null && !contentTypeProp.hasMultipleDifferentValues)
                    {
                        showPasswordMask = contentTypeProp.enumValueIndex == (int)TextBlock.ContentType.Password;
                        showNumberRange = contentTypeProp.enumValueIndex == (int)TextBlock.ContentType.Numbers;
                    }

                    if (passwordMaskProp != null && showPasswordMask)
                    {
                        Rect maskRect = AuraGUI.Layout.GetControlRect();
                        EditorGUI.PropertyField(maskRect, passwordMaskProp, new GUIContent("Password Mask"));
                    }

                    if (showNumberRange)
                    {
                        if (useNumberRangeProp != null)
                        {
                            Rect useRangeRect = AuraGUI.Layout.GetControlRect();
                            EditorGUI.PropertyField(useRangeRect, useNumberRangeProp, new GUIContent("Use Number Range"));
                        }

                        bool drawMinMax = useNumberRangeProp == null || useNumberRangeProp.boolValue;
                        if (useNumberRangeProp != null && useNumberRangeProp.hasMultipleDifferentValues)
                        {
                            drawMinMax = true;
                        }

                        if (drawMinMax)
                        {
                            if (numberMinProp != null)
                            {
                                Rect minRect = AuraGUI.Layout.GetControlRect();
                                EditorGUI.PropertyField(minRect, numberMinProp, new GUIContent("Number Min"));
                            }

                            if (numberMaxProp != null)
                            {
                                Rect maxRect = AuraGUI.Layout.GetControlRect();
                                EditorGUI.PropertyField(maxRect, numberMaxProp, new GUIContent("Number Max"));
                            }
                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        blockSerializedObject.ApplyModifiedProperties();
                        EditModeUtils.QueueEditorUpdateNextFrame();
                    }
                    AuraGUI.Space(1.5f);
                }
                AuraGUI.Layout.EndVertical();
                AuraGUI.Layout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditModeUtils.QueueEditorUpdateNextFrame();
            }
        }

        private static void DoUnsupportedTMPShaderDialogue(TMPro.TMP_FontAsset font)
        {
            if (MaterialCache.IsSupportedTMPShader(font.material.shader))
            {
                // It's supported
                return;
            }


            bool changeShader = EditorUtility.DisplayDialog("Unsupported TMP Shader", $"The provided font is using an unsupported TMP shader: [{font.material.shader.name}]. Aura currently only supports the [{Constants.TMPSupportedShaderName}] shader.\n\nWould you like to change the font over to use the supported shader?", "Yes", "No");

            if (!changeShader)
            {
                return;
            }

            Shader supportedShader = MaterialCache.GetSupportedTMPShder();
            if (supportedShader == null)
            {
                Debug.LogWarning($"Failed to change the shader to {Constants.TMPSupportedShaderName} on the font {font.name}. Please change it manually.", font);
                return;
            }

            font.material.shader = supportedShader;
        }

        private static int AlignmentFromTMPAlignment(TMPro.HorizontalAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TMPro.HorizontalAlignmentOptions.Left:
                    return -1;
                case TMPro.HorizontalAlignmentOptions.Right:
                    return 1;
            }

            return 0;
        }

        private static TMPro.HorizontalAlignmentOptions AlignmentToTMPHorizontal(int alignment)
        {
            switch (alignment)
            {
                case -1:
                    return TMPro.HorizontalAlignmentOptions.Left;
                case 1:
                    return TMPro.HorizontalAlignmentOptions.Right;
            }

            return TMPro.HorizontalAlignmentOptions.Center;
        }

        private static int AlignmentFromTMPAlignment(TMPro.VerticalAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TMPro.VerticalAlignmentOptions.Bottom:
                    return -1;
                case TMPro.VerticalAlignmentOptions.Top:
                    return 1;
            }

            return 0;
        }

        private static TMPro.VerticalAlignmentOptions AlignmentToTMPVertical(int alignment)
        {
            switch (alignment)
            {
                case -1:
                    return TMPro.VerticalAlignmentOptions.Bottom;
                case 1:
                    return TMPro.VerticalAlignmentOptions.Top;
            }

            return TMPro.VerticalAlignmentOptions.Middle;
        }
    }
}

