// Copyright (c) Supernova Technologies LLC
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Nova.Internal
{
    internal enum ImageScaleMode
    {
        Manual = 0,
        Fit = 1,
        Envelope = 2,
        Sliced = 3,
        Tiled = 4,
        Fill = 5,
    }

    internal enum ImageFillAxis
    {
        All = 0,
        Horizontal = 1,
        Vertical = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageAdjustment : System.IEquatable<ImageAdjustment>
    {
        public float2 CenterUV;
        public float2 UVScale;
        public float Rotation;
        public float PixelsPerUnitMultiplier;
        public ImageScaleMode ScaleMode;
        public ImageFillAxis FillAxis;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ImageAdjustment other)
        {
            return
                CenterUV.Equals(other.CenterUV) &&
                UVScale.Equals(other.UVScale) &&
                Rotation == other.Rotation &&
                ScaleMode == other.ScaleMode &&
                PixelsPerUnitMultiplier == other.PixelsPerUnitMultiplier &&
                FillAxis == other.FillAxis;
        }
    }
}