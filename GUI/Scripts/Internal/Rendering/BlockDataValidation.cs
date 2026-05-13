// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Nova.Internal.Rendering
{
    internal static class BlockDataValidation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive100Public(ref global::Nova.Length length)
        {
            if (length.Type == global::Nova.LengthType.Value)
            {
                length.Raw = math.max(length.Raw, 0);
            }
            else
            {
                length.Raw = math.clamp(length.Raw, 0, 1f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CornerRadiiIsUninitialized(in global::Nova.CornerRadii cr)
        {
            return cr.TopLeft == global::Nova.Length.Zero &&
                   cr.TopRight == global::Nova.Length.Zero &&
                   cr.BottomRight == global::Nova.Length.Zero &&
                   cr.BottomLeft == global::Nova.Length.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllCornerRadiiMatch(in global::Nova.CornerRadii cr, global::Nova.Length master)
        {
            return cr.TopLeft == master && cr.TopRight == master &&
                   cr.BottomRight == master && cr.BottomLeft == master;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampCornerRadiiLengths(ref global::Nova.CornerRadii cr)
        {
            ClampPositive100Public(ref cr.TopLeft);
            ClampPositive100Public(ref cr.TopRight);
            ClampPositive100Public(ref cr.BottomRight);
            ClampPositive100Public(ref cr.BottomLeft);
        }

        // Same effect as internal Length.ClampPositive() for public mirror fields after UnsafeUtility.As.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampLengthRawNonNegative(ref global::Nova.Length length)
        {
            length.Raw = math.max(length.Raw, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampLengthRawNonNegative(ref global::Nova.Length2 length)
        {
            ClampLengthRawNonNegative(ref length.X);
            ClampLengthRawNonNegative(ref length.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SyncCornerRadiiFromMaster(ref global::Nova.UIBlock2DData data)
        {
            global::Nova.Length u = data.CornerRadius;
            data.CornerRadii.TopLeft = u;
            data.CornerRadii.TopRight = u;
            data.CornerRadii.BottomRight = u;
            data.CornerRadii.BottomLeft = u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SyncCornerRadiiFromMaster(ref global::Nova.UIBlock3DData data)
        {
            global::Nova.Length u = data.CornerRadius;
            data.CornerRadii.TopLeft = u;
            data.CornerRadii.TopRight = u;
            data.CornerRadii.BottomRight = u;
            data.CornerRadii.BottomLeft = u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Validate(ref this global::Nova.Internal.UIBlock2DData data)
        {
            ref global::Nova.UIBlock2DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock2DData, global::Nova.UIBlock2DData>(ref data);

            ClampPositive100Public(ref pub.CornerRadius);
            ClampCornerRadiiLengths(ref pub.CornerRadii);

            if (!pub.UseIndividualCornerRadii &&
                !CornerRadiiIsUninitialized(in pub.CornerRadii) &&
                !AllCornerRadiiMatch(in pub.CornerRadii, pub.CornerRadius))
            {
                pub.UseIndividualCornerRadii = true;
            }

            if (!pub.UseIndividualCornerRadii)
            {
                SyncCornerRadiiFromMaster(ref pub);
            }

            ClampLengthRawNonNegative(ref pub.Gradient.Radius);
            ClampLengthRawNonNegative(ref pub.Border.Width);
            ClampLengthRawNonNegative(ref pub.Shadow.Blur);
            pub.RadialFill.Rotation = math.clamp(pub.RadialFill.Rotation, -360f, 360f);
            pub.RadialFill.FillAngle = math.clamp(pub.RadialFill.FillAngle, -360f, 360f);
            pub.Image.Adjustment.PixelsPerUnitMultiplier = math.max(pub.Image.Adjustment.PixelsPerUnitMultiplier, .01f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Validate(ref this global::Nova.Internal.UIBlock3DData data)
        {
            ref global::Nova.UIBlock3DData pub = ref UnsafeUtility.As<global::Nova.Internal.UIBlock3DData, global::Nova.UIBlock3DData>(ref data);

            ClampPositive100Public(ref pub.CornerRadius);
            ClampCornerRadiiLengths(ref pub.CornerRadii);

            if (!pub.UseIndividualCornerRadii &&
                !CornerRadiiIsUninitialized(in pub.CornerRadii) &&
                !AllCornerRadiiMatch(in pub.CornerRadii, pub.CornerRadius))
            {
                pub.UseIndividualCornerRadii = true;
            }

            if (!pub.UseIndividualCornerRadii)
            {
                SyncCornerRadiiFromMaster(ref pub);
            }

            ClampPositive100Public(ref pub.EdgeRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Validate(ref this Surface data)
        {
            data.param1 = math.saturate(data.param1);
            data.param2 = math.saturate(data.param2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive100(ref this Length length)
        {
            if (length.Type == LengthType.Value)
            {
                length.Raw = math.max(length.Raw, 0);
            }
            else
            {
                length.Raw = math.clamp(length.Raw, 0, 1f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive(ref this Length length)
        {
            length.Raw = math.max(length.Raw, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ClampPositive(ref this Length2 length)
        {
            length.Raw = math.max(length.Raw, 0);
        }
    }
}

