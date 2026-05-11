// Copyright (c) CoreFramework — Nova UI integration for Animate (ref-root binding, boxed leaf I/O,
// UIBlock Size/Position/Alignment fast paths). Kept isolated for a future Nova/UI overhaul.
using System;
using System.Collections;
using System.Reflection;
using Nova;
using UnityEngine;

namespace Core.Animator
{
    public partial class Animate
    {
        // --- UIBlock2D (visual blocks) -----------------------------------------------------------
        private delegate ref Border UIBlock2DBorderRef(UIBlock2D target);
        private delegate ref Shadow UIBlock2DShadowRef(UIBlock2D target);
        private delegate ref RadialGradient UIBlock2DGradientRef(UIBlock2D target);
        private delegate ref RadialFill UIBlock2DRadialFillRef(UIBlock2D target);
        private delegate ref Length UIBlock2DCornerRadiusRef(UIBlock2D target);

        // UIBlock (base) --------------------------------------------------------------------------
        private delegate ref Surface UIBlockSurfaceRef(UIBlock target);
        private delegate ref Layout UIBlockLayoutRef(UIBlock target);
        private delegate ref Length3 UIBlockRefLength3(UIBlock target);
        private delegate ref MinMax3 UIBlockRefMinMax3(UIBlock target);
        private delegate ref ThreeD<AutoSize> UIBlockRefThreeDAutoSize(UIBlock target);
        private delegate ref Axis UIBlockRefAxis(UIBlock target);
        private delegate ref LengthBounds UIBlockRefLengthBounds(UIBlock target);
        private delegate ref MinMaxBounds UIBlockRefMinMaxBounds(UIBlock target);
        private delegate ref Alignment UIBlockRefAlignment(UIBlock target);
        private delegate ref AutoLayout UIBlockRefAutoLayout(UIBlock target);

        private delegate ref Length UIBlock3DCornerRadiusRef(UIBlock3D target);
        private delegate ref Length UIBlock3DEdgeRadiusRef(UIBlock3D target);

        // UIBlock.Size + UIBlock2D.ImageAdjustment — ref-return delegates for CustomProperty markers
        private delegate ref Length3 SizeGetter(UIBlock target);
        private delegate ref Length3 SizeGetter2D(UIBlock2D target);
        private delegate ref Length3 SizeGetter3D(UIBlock3D target);
        private delegate ref ImageAdjustment ImageAdjustmentGetter2D(UIBlock2D target);

        private static float NovaUiBlock2ReadLeafAsSingle(object boxedStruct, MemberInfo leaf)
        {
            if (leaf is FieldInfo fi) return Convert.ToSingle(fi.GetValue(boxedStruct));
            if (leaf is PropertyInfo pi && pi.CanRead) return Convert.ToSingle(pi.GetValue(boxedStruct));
            return 0f;
        }

        private static void NovaUiBlock2WriteLeafFromSingle(object boxedStruct, MemberInfo leaf, float vIn, Type leafClrType)
        {
            object coerced = Convert.ChangeType(vIn, leafClrType);
            if (leaf is FieldInfo fi) fi.SetValue(boxedStruct, coerced);
            else if (leaf is PropertyInfo pi && pi.CanWrite) pi.SetValue(boxedStruct, coerced);
        }

        private static Color NovaUiBlock2ReadLeafAsColor(object boxedStruct, MemberInfo leaf)
        {
            if (leaf is FieldInfo fi) return fi.GetValue(boxedStruct) is Color c ? c : default;
            if (leaf is PropertyInfo pi && pi.CanRead && pi.GetValue(boxedStruct) is Color pc) return pc;
            return Color.white;
        }

        private static void NovaUiBlock2WriteLeafAsColor(object boxedStruct, MemberInfo leaf, Color v)
        {
            if (leaf is FieldInfo fi) fi.SetValue(boxedStruct, v);
            else if (leaf is PropertyInfo pi && pi.CanWrite) pi.SetValue(boxedStruct, v);
        }

        private static Vector3 NovaUiBlock2ReadLeafAsVector3(object boxedStruct, MemberInfo leaf)
        {
            if (leaf is FieldInfo fi) return (Vector3)fi.GetValue(boxedStruct);
            if (leaf is PropertyInfo pi && pi.CanRead) return (Vector3)pi.GetValue(boxedStruct);
            return Vector3.zero;
        }

        private static void NovaUiBlock2WriteLeafAsVector3(object boxedStruct, MemberInfo leaf, Vector3 v)
        {
            if (leaf is FieldInfo fi) fi.SetValue(boxedStruct, v);
            else if (leaf is PropertyInfo pi && pi.CanWrite) pi.SetValue(boxedStruct, v);
        }

        private static Quaternion NovaUiBlock2ReadLeafAsQuaternion(object boxedStruct, MemberInfo leaf)
        {
            if (leaf is FieldInfo fi) return (Quaternion)fi.GetValue(boxedStruct);
            if (leaf is PropertyInfo pi && pi.CanRead) return (Quaternion)pi.GetValue(boxedStruct);
            return Quaternion.identity;
        }

        private static void NovaUiBlock2WriteLeafAsQuaternion(object boxedStruct, MemberInfo leaf, Quaternion v)
        {
            if (leaf is FieldInfo fi) fi.SetValue(boxedStruct, v);
            else if (leaf is PropertyInfo pi && pi.CanWrite) pi.SetValue(boxedStruct, v);
        }

        /// <summary>
        /// Bind getters/setters for a leaf member on any Nova <see cref="UIBlock"/> /
        /// <see cref="UIBlock2D"/> / <see cref="UIBlock3D"/> <c>public ref T Foo =&gt; …</c> root that has no CLR property setter.
        /// </summary>
        private static bool TryBindNovaUIBlockGetterOnlyLeaf<T>(
            RefStructMarker marker,
            UIBlock block,
            MemberInfo leafMember,
            Func<object, MemberInfo, T> readLeaf,
            Action<object, MemberInfo, T> writeLeaf,
            out Func<T> getter,
            out Action<T> setter)
        {
            getter = null;
            setter = null;
            if (block == null || leafMember == null || marker.originalOwner != (object)block || marker.refProperty.CanWrite)
                return false;
            var gm = marker.refProperty.GetMethod;
            if (gm == null || !marker.refProperty.PropertyType.Name.EndsWith("&"))
                return false;

            switch (marker.refProperty.Name)
            {
                case nameof(UIBlock.Surface):
                {
                    var d = (UIBlockSurfaceRef)gm.CreateDelegate(typeof(UIBlockSurfaceRef));
                    var ub = block;
                    getter = () =>
                    {
                        ref Surface s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Surface s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Surface)box;
                    };
                    return true;
                }
                case nameof(UIBlock.Layout):
                {
                    var d = (UIBlockLayoutRef)gm.CreateDelegate(typeof(UIBlockLayoutRef));
                    var ub = block;
                    getter = () =>
                    {
                        ref Layout s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Layout s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Layout)box;
                    };
                    return true;
                }
                case nameof(UIBlock.Size):
                {
                    var d = (UIBlockRefLength3)gm.CreateDelegate(typeof(UIBlockRefLength3));
                    var ub = block;
                    getter = () =>
                    {
                        ref Length3 s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Length3 s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Length3)box;
                    };
                    return true;
                }
                case nameof(UIBlock.SizeMinMax):
                {
                    var d = (UIBlockRefMinMax3)gm.CreateDelegate(typeof(UIBlockRefMinMax3));
                    var ub = block;
                    getter = () =>
                    {
                        ref MinMax3 s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref MinMax3 s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (MinMax3)box;
                    };
                    return true;
                }
                case nameof(UIBlock.AutoSize):
                {
                    var d = (UIBlockRefThreeDAutoSize)gm.CreateDelegate(typeof(UIBlockRefThreeDAutoSize));
                    var ub = block;
                    getter = () =>
                    {
                        ref ThreeD<AutoSize> s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref ThreeD<AutoSize> s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (ThreeD<AutoSize>)box;
                    };
                    return true;
                }
                case nameof(UIBlock.AspectRatioAxis):
                {
                    var d = (UIBlockRefAxis)gm.CreateDelegate(typeof(UIBlockRefAxis));
                    var ub = block;
                    getter = () =>
                    {
                        ref Axis s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Axis s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Axis)box;
                    };
                    return true;
                }
                case nameof(UIBlock.Margin):
                {
                    var d = (UIBlockRefLengthBounds)gm.CreateDelegate(typeof(UIBlockRefLengthBounds));
                    var ub = block;
                    getter = () =>
                    {
                        ref LengthBounds s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref LengthBounds s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (LengthBounds)box;
                    };
                    return true;
                }
                case nameof(UIBlock.MarginMinMax):
                {
                    var d = (UIBlockRefMinMaxBounds)gm.CreateDelegate(typeof(UIBlockRefMinMaxBounds));
                    var ub = block;
                    getter = () =>
                    {
                        ref MinMaxBounds s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref MinMaxBounds s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (MinMaxBounds)box;
                    };
                    return true;
                }
                case nameof(UIBlock.Alignment):
                {
                    var d = (UIBlockRefAlignment)gm.CreateDelegate(typeof(UIBlockRefAlignment));
                    var ub = block;
                    getter = () =>
                    {
                        ref Alignment s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Alignment s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Alignment)box;
                    };
                    return true;
                }
                case nameof(UIBlock.Position):
                {
                    var d = (UIBlockRefLength3)gm.CreateDelegate(typeof(UIBlockRefLength3));
                    var ub = block;
                    getter = () =>
                    {
                        ref Length3 s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Length3 s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Length3)box;
                    };
                    return true;
                }
                case nameof(UIBlock.PositionMinMax):
                {
                    var d = (UIBlockRefMinMax3)gm.CreateDelegate(typeof(UIBlockRefMinMax3));
                    var ub = block;
                    getter = () =>
                    {
                        ref MinMax3 s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref MinMax3 s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (MinMax3)box;
                    };
                    return true;
                }
                case nameof(UIBlock.Padding):
                {
                    var d = (UIBlockRefLengthBounds)gm.CreateDelegate(typeof(UIBlockRefLengthBounds));
                    var ub = block;
                    getter = () =>
                    {
                        ref LengthBounds s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref LengthBounds s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (LengthBounds)box;
                    };
                    return true;
                }
                case nameof(UIBlock.PaddingMinMax):
                {
                    var d = (UIBlockRefMinMaxBounds)gm.CreateDelegate(typeof(UIBlockRefMinMaxBounds));
                    var ub = block;
                    getter = () =>
                    {
                        ref MinMaxBounds s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref MinMaxBounds s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (MinMaxBounds)box;
                    };
                    return true;
                }
                case nameof(UIBlock.AutoLayout):
                {
                    var d = (UIBlockRefAutoLayout)gm.CreateDelegate(typeof(UIBlockRefAutoLayout));
                    var ub = block;
                    getter = () =>
                    {
                        ref AutoLayout s = ref d(ub);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref AutoLayout s = ref d(ub);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (AutoLayout)box;
                    };
                    return true;
                }

                case "CornerRadius":
                {
                    if (block is UIBlock2D b2d)
                    {
                        var d = (UIBlock2DCornerRadiusRef)gm.CreateDelegate(typeof(UIBlock2DCornerRadiusRef));
                        getter = () =>
                        {
                            ref Length s = ref d(b2d);
                            object box = s;
                            return readLeaf(box, leafMember);
                        };
                        setter = v =>
                        {
                            ref Length s = ref d(b2d);
                            object box = s;
                            writeLeaf(box, leafMember, v);
                            s = (Length)box;
                        };
                        return true;
                    }
                    if (block is UIBlock3D b3dC)
                    {
                        var d = (UIBlock3DCornerRadiusRef)gm.CreateDelegate(typeof(UIBlock3DCornerRadiusRef));
                        getter = () =>
                        {
                            ref Length s = ref d(b3dC);
                            object box = s;
                            return readLeaf(box, leafMember);
                        };
                        setter = v =>
                        {
                            ref Length s = ref d(b3dC);
                            object box = s;
                            writeLeaf(box, leafMember, v);
                            s = (Length)box;
                        };
                        return true;
                    }
                    return false;
                }
                case nameof(UIBlock3D.EdgeRadius):
                {
                    if (block is not UIBlock3D b3e) return false;
                    var d = (UIBlock3DEdgeRadiusRef)gm.CreateDelegate(typeof(UIBlock3DEdgeRadiusRef));
                    getter = () =>
                    {
                        ref Length s = ref d(b3e);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Length s = ref d(b3e);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Length)box;
                    };
                    return true;
                }

                case nameof(UIBlock2D.Border):
                {
                    if (block is not UIBlock2D bB) return false;
                    var d = (UIBlock2DBorderRef)gm.CreateDelegate(typeof(UIBlock2DBorderRef));
                    getter = () =>
                    {
                        ref Border s = ref d(bB);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Border s = ref d(bB);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Border)box;
                    };
                    return true;
                }
                case nameof(UIBlock2D.Shadow):
                {
                    if (block is not UIBlock2D bS) return false;
                    var d = (UIBlock2DShadowRef)gm.CreateDelegate(typeof(UIBlock2DShadowRef));
                    getter = () =>
                    {
                        ref Shadow s = ref d(bS);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref Shadow s = ref d(bS);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (Shadow)box;
                    };
                    return true;
                }
                case nameof(UIBlock2D.Gradient):
                {
                    if (block is not UIBlock2D bG) return false;
                    var d = (UIBlock2DGradientRef)gm.CreateDelegate(typeof(UIBlock2DGradientRef));
                    getter = () =>
                    {
                        ref RadialGradient s = ref d(bG);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref RadialGradient s = ref d(bG);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (RadialGradient)box;
                    };
                    return true;
                }
                case nameof(UIBlock2D.RadialFill):
                {
                    if (block is not UIBlock2D bR) return false;
                    var d = (UIBlock2DRadialFillRef)gm.CreateDelegate(typeof(UIBlock2DRadialFillRef));
                    getter = () =>
                    {
                        ref RadialFill s = ref d(bR);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref RadialFill s = ref d(bR);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (RadialFill)box;
                    };
                    return true;
                }
                case nameof(UIBlock2D.ImageAdjustment):
                {
                    if (block is not UIBlock2D bI) return false;
                    var d = (ImageAdjustmentGetter2D)gm.CreateDelegate(typeof(ImageAdjustmentGetter2D));
                    getter = () =>
                    {
                        ref ImageAdjustment s = ref d(bI);
                        object box = s;
                        return readLeaf(box, leafMember);
                    };
                    setter = v =>
                    {
                        ref ImageAdjustment s = ref d(bI);
                        object box = s;
                        writeLeaf(box, leafMember, v);
                        s = (ImageAdjustment)box;
                    };
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool TryBindNovaUIBlockGetterOnlyLeafFloat(RefStructMarker marker, UIBlock block, MemberInfo leafMember, Type leafClrType, out Func<float> getter, out Action<float> setter)
        {
            return TryBindNovaUIBlockGetterOnlyLeaf(marker, block, leafMember, NovaUiBlock2ReadLeafAsSingle,
                (box, leaf, v) => NovaUiBlock2WriteLeafFromSingle(box, leaf, v, leafClrType), out getter, out setter);
        }

        private static bool TryBindNovaUIBlockGetterOnlyLeafColor(RefStructMarker marker, UIBlock block, MemberInfo leafMember, out Func<Color> getter, out Action<Color> setter)
        {
            return TryBindNovaUIBlockGetterOnlyLeaf(marker, block, leafMember, NovaUiBlock2ReadLeafAsColor,
                NovaUiBlock2WriteLeafAsColor, out getter, out setter);
        }

        private static bool TryBindNovaUIBlockGetterOnlyLeafVector3(RefStructMarker marker, UIBlock block, MemberInfo leafMember, out Func<Vector3> getter, out Action<Vector3> setter)
        {
            return TryBindNovaUIBlockGetterOnlyLeaf(marker, block, leafMember, NovaUiBlock2ReadLeafAsVector3,
                NovaUiBlock2WriteLeafAsVector3, out getter, out setter);
        }

        private static bool TryBindNovaUIBlockGetterOnlyLeafQuaternion(RefStructMarker marker, UIBlock block, MemberInfo leafMember, out Func<Quaternion> getter, out Action<Quaternion> setter)
        {
            return TryBindNovaUIBlockGetterOnlyLeaf(marker, block, leafMember, NovaUiBlock2ReadLeafAsQuaternion,
                NovaUiBlock2WriteLeafAsQuaternion, out getter, out setter);
        }

        private bool TryHandleUIBlockAlignment(TweenEntry e, Component comp)
        {
            return StartCoroutine(DriveEnumAlignment(e, comp)) != null;
        }

        private IEnumerator DriveEnumAlignment(TweenEntry e, Component comp)
        {
            Func<Alignment> getCurrentAlignment = () =>
            {
                if (comp is UIBlock3D ub3) return ub3.Alignment;
                if (comp is UIBlock2D ub2) return ub2.Alignment;
                if (comp is UIBlock ub) return ub.Alignment;
                return Alignment.Center;
            };

            var targetAlignment = new Alignment(
                (HorizontalAlignment)Mathf.RoundToInt(e.toVec3.x),
                (VerticalAlignment)Mathf.RoundToInt(e.toVec3.y),
                (DepthAlignment)Mathf.RoundToInt(e.toVec3.z));

            Alignment startAlignment;
            if (e.startSource == StartSource.Start)
            {
                startAlignment = getCurrentAlignment();
            }
            else if (e.startSource == StartSource.End)
            {
                startAlignment = targetAlignment;
                targetAlignment = new Alignment(
                    (HorizontalAlignment)Mathf.RoundToInt(e.fromVec3.x),
                    (VerticalAlignment)Mathf.RoundToInt(e.fromVec3.y),
                    (DepthAlignment)Mathf.RoundToInt(e.fromVec3.z));
            }
            else
            {
                startAlignment = new Alignment(
                    (HorizontalAlignment)Mathf.RoundToInt(e.fromVec3.x),
                    (VerticalAlignment)Mathf.RoundToInt(e.fromVec3.y),
                    (DepthAlignment)Mathf.RoundToInt(e.fromVec3.z));
            }

            ComponentMask mask = e.enumFieldMask;
            if (mask == ComponentMask.None)
                mask = ComponentMask.All;

            var current = startAlignment;
            if (mask.HasFlag(ComponentMask.X)) current.X = startAlignment.X;
            if (mask.HasFlag(ComponentMask.Y)) current.Y = startAlignment.Y;
            if (mask.HasFlag(ComponentMask.Z)) current.Z = startAlignment.Z;

            if (comp is UIBlock3D ub3) ub3.Alignment = current;
            else if (comp is UIBlock2D ub2) ub2.Alignment = current;
            else if (comp is UIBlock ub) ub.Alignment = current;

            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < e.duration)
                yield return new WaitForEndOfFrame();

            current = getCurrentAlignment();
            if (mask.HasFlag(ComponentMask.X)) current.X = targetAlignment.X;
            if (mask.HasFlag(ComponentMask.Y)) current.Y = targetAlignment.Y;
            if (mask.HasFlag(ComponentMask.Z)) current.Z = targetAlignment.Z;

            if (comp is UIBlock3D ub3b) ub3b.Alignment = current;
            else if (comp is UIBlock2D ub2b) ub2b.Alignment = current;
            else if (comp is UIBlock ubnb) ubnb.Alignment = current;

            yield return new WaitForEndOfFrame();
        }

        /// <summary>
        /// UIBlock-specific CustomProperty shortcuts (layout-friendly Size/Position paths, discrete Alignment delay).
        /// </summary>
        /// <returns><see langword="true"/> when this path handled the entry (result may still be null, e.g. Alignment).</returns>
        private bool TryNovaUIBlockCustomPropertyFastPath(TweenEntry e, Component comp, string resolvedPath, out Coroutine result)
        {
            result = null;

            if (comp is UIBlock sizeBlock)
            {
                string sizePath = resolvedPath;
                const string layoutPrefix = "Layout.";
                if (!string.IsNullOrEmpty(sizePath) && sizePath.StartsWith(layoutPrefix, StringComparison.Ordinal))
                    sizePath = sizePath.Substring(layoutPrefix.Length);

                if (sizePath == "Size.Percent" || sizePath == "Size.Raw")
                {
                    bool isPercent = sizePath.EndsWith("Percent", StringComparison.Ordinal);

                    Func<Vector3> getter = () => isPercent ? sizeBlock.GetSizePercentUI() : sizeBlock.GetSizeValueUnits();

                    Action<Vector3> setter = v =>
                    {
                        float? setX = e.vectorMask.HasFlag(ComponentMask.X) ? (float?)v.x : null;
                        float? setY = e.vectorMask.HasFlag(ComponentMask.Y) ? (float?)v.y : null;
                        float? setZ = e.vectorMask.HasFlag(ComponentMask.Z) ? (float?)v.z : null;

                        sizeBlock.SetSizeAxes(setX, setY, setZ,
                            isPercent ? Length3Extensions.LengthInputSpace.PercentUI_0_100 : Length3Extensions.LengthInputSpace.ValueUnits);
                    };

                    ComponentMask mask = e.vectorMask;
                    if (mask == ComponentMask.None)
                        mask = ComponentMask.All;

                    Action<Vector3> maskedSetter = v =>
                    {
                        Vector3 current = getter();
                        if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                        if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                        if (!mask.HasFlag(ComponentMask.Z)) v.z = current.z;
                        setter(v);
                    };

                    result = TweenVec3WithSourceDeferred(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
                    return true;
                }

                if (!string.IsNullOrEmpty(sizePath) && sizePath.StartsWith("Size.", StringComparison.Ordinal))
                {
                    string[] parts = sizePath.Split('.');
                    if (parts.Length == 3)
                    {
                        string axisPart = parts[1];
                        string modePart = parts[2];

                        int axis = axisPart == "X" ? 0 : axisPart == "Y" ? 1 : axisPart == "Z" ? 2 : -1;
                        bool isPercent = modePart == "Percent";
                        bool isRaw = modePart == "Raw";

                        if (axis >= 0 && (isPercent || isRaw))
                        {
                            Func<float> getter = () =>
                            {
                                Vector3 v = isPercent ? sizeBlock.GetSizePercentUI() : sizeBlock.GetSizeValueUnits();
                                return axis == 0 ? v.x : axis == 1 ? v.y : v.z;
                            };

                            Action<float> setter = v =>
                            {
                                float? setX = axis == 0 ? (float?)v : null;
                                float? setY = axis == 1 ? (float?)v : null;
                                float? setZ = axis == 2 ? (float?)v : null;

                                sizeBlock.SetSizeAxes(setX, setY, setZ,
                                    isPercent ? Length3Extensions.LengthInputSpace.PercentUI_0_100 : Length3Extensions.LengthInputSpace.ValueUnits);
                            };

                            if (e.propertyMode == CustomPropertyMode.SetAtEnd)
                            {
                                StartCoroutine(ApplyActionAfterSeconds(() => setter(e.toFloat), e.duration));
                                return true;
                            }

                            result = TweenFloatWithSourceDeferred(getter, setter, e.toFloat, e.duration, e.curve, e.startSource, e.fromFloat);
                            return true;
                        }
                    }
                }
            }

            if ((comp is UIBlock || comp is UIBlock2D || comp is UIBlock3D) && (resolvedPath == "Position.Percent" || resolvedPath == "Position.Raw"))
            {
                bool isPercent = resolvedPath.EndsWith("Percent");

                Func<Vector3> getter = () =>
                {
                    if (!(comp is UIBlock blockBase)) return Vector3.zero;
                    return isPercent ? blockBase.GetPositionPercentUI() : blockBase.GetPositionValueUnits();
                };

                Action<Vector3> setter = v =>
                {
                    float? setX = e.vectorMask.HasFlag(ComponentMask.X) ? (float?)v.x : null;
                    float? setY = e.vectorMask.HasFlag(ComponentMask.Y) ? (float?)v.y : null;
                    float? setZ = e.vectorMask.HasFlag(ComponentMask.Z) ? (float?)v.z : null;

                    var block = comp as UIBlock;
                    if (block == null) return;

                    block.SetPositionAxes(setX, setY, setZ,
                        isPercent ? Length3Extensions.LengthInputSpace.PercentUI_0_100 : Length3Extensions.LengthInputSpace.ValueUnits);
                };

                ComponentMask mask = e.vectorMask;
                if (mask == ComponentMask.None)
                    mask = ComponentMask.All;

                Action<Vector3> maskedSetter = v =>
                {
                    Vector3 current = getter();
                    if (!mask.HasFlag(ComponentMask.X)) v.x = current.x;
                    if (!mask.HasFlag(ComponentMask.Y)) v.y = current.y;
                    if (!mask.HasFlag(ComponentMask.Z)) v.z = current.z;
                    setter(v);
                };
                result = TweenVec3WithSource(getter, maskedSetter, e.toVec3, e.duration, e.curve, e.startSource, e.fromVec3);
                return true;
            }

            if ((comp is UIBlock || comp is UIBlock2D || comp is UIBlock3D) && resolvedPath == "Alignment")
            {
                TryHandleUIBlockAlignment(e, comp);
                return true;
            }

            return false;
        }
    }
}
