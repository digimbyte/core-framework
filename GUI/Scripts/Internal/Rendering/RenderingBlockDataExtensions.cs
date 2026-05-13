// Copyright (c) Supernova Technologies LLC
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    internal static class BlockDataExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetClampedCornerLength(ref global::Nova.Length length, float halfMinBlockDimension)
        {
            float rel = halfMinBlockDimension;
            ref Length il = ref global::Nova.LengthExtensions.ToInternal(ref length);
            return math.clamp(GetLengthValue(ref il, ref rel), 0f, halfMinBlockDimension);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetResolvedCornerRadii(ref this global::Nova.UIBlock2DData data, float halfMinBlockDimension)
        {
            if (!data.UseIndividualCornerRadii)
            {
                float u = GetClampedCornerLength(ref data.CornerRadius, halfMinBlockDimension);
                return new float4(u, u, u, u);
            }

            ref global::Nova.CornerRadii cr = ref data.CornerRadii;
            return new float4(
                GetClampedCornerLength(ref cr.TopLeft, halfMinBlockDimension),
                GetClampedCornerLength(ref cr.TopRight, halfMinBlockDimension),
                GetClampedCornerLength(ref cr.BottomRight, halfMinBlockDimension),
                GetClampedCornerLength(ref cr.BottomLeft, halfMinBlockDimension));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetResolvedCornerRadii(ref this global::Nova.UIBlock3DData data, float halfMinXY)
        {
            if (!data.UseIndividualCornerRadii)
            {
                float u = GetClampedCornerLength(ref data.CornerRadius, halfMinXY);
                return new float4(u, u, u, u);
            }

            ref global::Nova.CornerRadii cr = ref data.CornerRadii;
            return new float4(
                GetClampedCornerLength(ref cr.TopLeft, halfMinXY),
                GetClampedCornerLength(ref cr.TopRight, halfMinXY),
                GetClampedCornerLength(ref cr.BottomRight, halfMinXY),
                GetClampedCornerLength(ref cr.BottomLeft, halfMinXY));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetCornerRadius(ref this global::Nova.UIBlock2DData data, float halfMinBlockDimension)
        {
            return math.cmax(data.GetResolvedCornerRadii(halfMinBlockDimension));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetCornerRadius(ref this global::Nova.UIBlock3DData data, float halfMinBlockDimension)
        {
            return math.cmax(data.GetResolvedCornerRadii(halfMinBlockDimension));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetResolvedCornerRadii(ref this global::Nova.Internal.UIBlock2DData data, float halfMinBlockDimension)
        {
            ref global::Nova.UIBlock2DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock2DData, global::Nova.UIBlock2DData>(ref data);
            return pub.GetResolvedCornerRadii(halfMinBlockDimension);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 GetResolvedCornerRadii(ref this global::Nova.Internal.UIBlock3DData data, float halfMinXY)
        {
            ref global::Nova.UIBlock3DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock3DData, global::Nova.UIBlock3DData>(ref data);
            return pub.GetResolvedCornerRadii(halfMinXY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetCornerRadius(ref this global::Nova.Internal.UIBlock2DData data, float halfMinBlockDimension)
        {
            ref global::Nova.UIBlock2DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock2DData, global::Nova.UIBlock2DData>(ref data);
            return pub.GetCornerRadius(halfMinBlockDimension);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetCornerRadius(ref this global::Nova.Internal.UIBlock3DData data, float halfMinBlockDimension)
        {
            ref global::Nova.UIBlock3DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock3DData, global::Nova.UIBlock3DData>(ref data);
            return pub.GetCornerRadius(halfMinBlockDimension);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetEdgeRadius(ref this global::Nova.Internal.UIBlock3DData data, float zSize, float clampedCornerRadius)
        {
            ref global::Nova.UIBlock3DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock3DData, global::Nova.UIBlock3DData>(ref data);
            return pub.GetEdgeRadius(zSize, clampedCornerRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetEdgeRadius(ref this global::Nova.UIBlock3DData data, float zSize, float clampedCornerRadius)
        {
            float maxEdgeRadius = math.min(0.5f * zSize, clampedCornerRadius);
            ref Length il = ref global::Nova.LengthExtensions.ToInternal(ref data.EdgeRadius);
            float toClamp = GetLengthValue(ref il, ref maxEdgeRadius);
            return math.clamp(toClamp, 0, maxEdgeRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetWidth(ref this Border data, float halfMinBlockDimension)
        {
            float toClamp = data.Width.GetLengthValue(ref halfMinBlockDimension);

            float clampMax = float.PositiveInfinity;
            if (data.Direction == BorderDirection.In)
            {
                clampMax = halfMinBlockDimension;
            }
            else if (data.Direction == BorderDirection.Center)
            {
                clampMax = 2f * halfMinBlockDimension;
            }

            return math.clamp(toClamp, 0, clampMax);
        }

        /// <summary>
        /// Width can be negative, blur cannot
        /// </summary>
        private static readonly float2 minShadowValues = new float2(float.NegativeInfinity, 0f);

        /// <summary>
        /// Returns width and blur in x and y respectively
        /// </summary>
        /// <param name="data"></param>
        /// <param name="halfMinBlockDimension"></param>
        /// <param name="blur"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetWidths(ref this Shadow data, float halfMinBlockDimension)
        {
            float2 toRet = new float2(
                data.Width.GetLengthValue(ref halfMinBlockDimension),
                data.Blur.GetLengthValue(ref halfMinBlockDimension));

            if (data.Direction == ShadowDirection.Out)
            {
                return math.max(toRet, minShadowValues);
            }
            else
            {
                return math.clamp(toRet, minShadowValues, halfMinBlockDimension);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetCenter(ref this RadialFill data, ref float2 nodeSize)
        {
            // Fudge it here by a little bit to prevent a couple pixels of gap
            return data.Center.GetLength2Value(ref nodeSize) * new float2(1.01f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetSize(ref this RadialGradient data, ref float2 nodeSize)
        {
            return data.Radius.GetLength2Value(ref nodeSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetCenter(ref this RadialGradient data, ref float2 nodeSize)
        {
            return data.Center.GetLength2Value(ref nodeSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetOffset(ref this Shadow data, ref float2 nodeSize)
        {
            return data.Offset.GetLength2Value(ref nodeSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 GetLength2Value(ref this Length2 length, ref float2 nodeSize)
        {
            return new float2(
               length.X.GetLengthValue(ref nodeSize.x),
               length.Y.GetLengthValue(ref nodeSize.y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetLengthValue(ref this Length length, ref float relativeTo)
        {
            return length.Type == LengthType.Value ? length.Raw : length.Raw * relativeTo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AdjustSizeForImage(this ref global::Nova.UIBlock2DData data, ref float2 size, float aspectRatio)
        {
            if (data.Image.Adjustment.ScaleMode == global::Nova.ImageScaleMode.Fit)
            {
                float nodeAspectRatio = size.x / size.y;
                float relativeAspectRatio = aspectRatio / nodeAspectRatio;
                if (relativeAspectRatio > 1)
                {
                    size.y /= relativeAspectRatio;
                }
                else
                {
                    size.x *= relativeAspectRatio;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AdjustSizeForImage(this ref global::Nova.Internal.UIBlock2DData data, ref float2 size, float aspectRatio)
        {
            ref global::Nova.UIBlock2DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock2DData, global::Nova.UIBlock2DData>(ref data);
            pub.AdjustSizeForImage(ref size, aspectRatio);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetBorderRadii(ref this Border border, float bodyCornerRadius, float borderWidth, ref float2 nodeSize, out float innerRadius, out float outerRadius)
        {
            innerRadius = 0f;
            outerRadius = 0f;
            switch (border.Direction)
            {
                case BorderDirection.In:
                    innerRadius = math.max(0, bodyCornerRadius - borderWidth);
                    outerRadius = bodyCornerRadius;
                    break;
                case BorderDirection.Out:
                    nodeSize += 2f * borderWidth;
                    innerRadius = bodyCornerRadius;
                    outerRadius = bodyCornerRadius + borderWidth;
                    break;
                case BorderDirection.Center:
                    nodeSize += borderWidth;
                    float halfBorderWidth = .5f * borderWidth;
                    innerRadius = math.max(0, bodyCornerRadius - halfBorderWidth);
                    outerRadius = bodyCornerRadius + halfBorderWidth;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ModifySizeForBorder(ref this Border border, ref float bodyCornerRadius, ref float2 bodySize, float borderWidth)
        {
            switch (border.Direction)
            {
                case BorderDirection.Out:
                    bodySize += 2f * borderWidth;
                    bodyCornerRadius = math.select(0f, bodyCornerRadius + borderWidth, bodyCornerRadius > Math.Epsilon);
                    break;
                case BorderDirection.Center:
                    bodySize += borderWidth;
                    float halfBorderWidth = .5f * borderWidth;
                    bodyCornerRadius = math.select(0f, bodyCornerRadius + halfBorderWidth, bodyCornerRadius > Math.Epsilon);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ModifySizeForBorder(ref this Border border, ref float4 bodyCornerRadii, ref float2 bodySize, float borderWidth)
        {
            switch (border.Direction)
            {
                case BorderDirection.Out:
                    bodySize += 2f * borderWidth;
                    bodyCornerRadii = math.select(float4.zero, bodyCornerRadii + borderWidth, bodyCornerRadii > Math.Epsilon);
                    break;
                case BorderDirection.Center:
                    bodySize += borderWidth;
                    float halfBorderWidth = .5f * borderWidth;
                    bodyCornerRadii = math.select(float4.zero, bodyCornerRadii + halfBorderWidth, bodyCornerRadii > Math.Epsilon);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 GetPositionalOffset(ref this TextBlockData data, bool2 layoutShrinkMask)
        {
            return math.select(float2.zero, -data.TextBounds.GetCenter().xy, layoutShrinkMask);
        }
    }
}

