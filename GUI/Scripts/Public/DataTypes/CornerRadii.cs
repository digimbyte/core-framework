// Copyright (c) Supernova Technologies LLC
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Per-corner <see cref="Length"/> radii (TL, TR, BR, BL), same authoring pattern as <see cref="LengthBounds"/> / <see cref="LengthRect"/>:
    /// set <see cref="Value"/> or <see cref="Percent"/> to apply one value to all corners; assign individual corners for overrides.
    /// </summary>
    /// <remarks>
    /// Percent on each corner matches single <see cref="UIBlock2D.CornerRadius"/> / <see cref="UIBlock3D.CornerRadius"/> semantics (relative to half the minimum in-plane dimension).
    /// </remarks>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct CornerRadii : IEquatable<CornerRadii>
    {
        internal const int SizeOf = 4 * Length.SizeOf;

        [SerializeField]
        public Length TopLeft;

        [SerializeField]
        public Length TopRight;

        [SerializeField]
        public Length BottomRight;

        [SerializeField]
        public Length BottomLeft;

        /// <summary>Sets all four corners to a fixed <see cref="LengthType.Value"/> length.</summary>
        public float Value
        {
            set
            {
                Length v = Length.FixedValue(value);
                TopLeft = v;
                TopRight = v;
                BottomRight = v;
                BottomLeft = v;
            }
        }

        /// <summary>Sets all four corners to a <see cref="LengthType.Percent"/> length; <c>1 == 100%</c>.</summary>
        public float Percent
        {
            set
            {
                Length v = Length.Percentage(value);
                TopLeft = v;
                TopRight = v;
                BottomRight = v;
                BottomLeft = v;
            }
        }

        public static bool operator ==(CornerRadii lhs, CornerRadii rhs) =>
            lhs.TopLeft == rhs.TopLeft &&
            lhs.TopRight == rhs.TopRight &&
            lhs.BottomRight == rhs.BottomRight &&
            lhs.BottomLeft == rhs.BottomLeft;

        public static bool operator !=(CornerRadii lhs, CornerRadii rhs) => !(lhs == rhs);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + TopLeft.GetHashCode();
            hash = (hash * 7) + TopRight.GetHashCode();
            hash = (hash * 7) + BottomRight.GetHashCode();
            hash = (hash * 7) + BottomLeft.GetHashCode();
            return hash;
        }

        public override bool Equals(object other) => other is CornerRadii cr && this == cr;

        public bool Equals(CornerRadii other) => this == other;

        public override string ToString()
        {
            if (TopLeft == TopRight && TopLeft == BottomRight && TopLeft == BottomLeft)
            {
                return $"CornerRadii(All: {TopLeft})";
            }

            return $"CornerRadii(TL: {TopLeft}, TR: {TopRight}, BR: {BottomRight}, BL: {BottomLeft})";
        }

        public static implicit operator CornerRadii(float uniformValue)
        {
            Length v = Length.FixedValue(uniformValue);
            return new CornerRadii
            {
                TopLeft = v,
                TopRight = v,
                BottomRight = v,
                BottomLeft = v,
            };
        }

        public static implicit operator CornerRadii(Length uniformLength) => new CornerRadii
        {
            TopLeft = uniformLength,
            TopRight = uniformLength,
            BottomRight = uniformLength,
            BottomLeft = uniformLength,
        };

        [Obfuscation]
        internal readonly struct Calculated
        {
            public readonly Length.Calculated TopLeft;
            public readonly Length.Calculated TopRight;
            public readonly Length.Calculated BottomRight;
            public readonly Length.Calculated BottomLeft;

            internal Calculated(CornerRadii data, float relative1D)
            {
                var minMax = Internal.Length.MinMax.Positive;

                Internal.Length.Calculated tl = new Internal.Length.Calculated(data.TopLeft.ToInternal(), minMax, relative1D);
                TopLeft = tl.ToPublic();

                Internal.Length.Calculated tr = new Internal.Length.Calculated(data.TopRight.ToInternal(), minMax, relative1D);
                TopRight = tr.ToPublic();

                Internal.Length.Calculated br = new Internal.Length.Calculated(data.BottomRight.ToInternal(), minMax, relative1D);
                BottomRight = br.ToPublic();

                Internal.Length.Calculated bl = new Internal.Length.Calculated(data.BottomLeft.ToInternal(), minMax, relative1D);
                BottomLeft = bl.ToPublic();
            }

            public readonly float MaxValue => math.cmax(new float4(
                TopLeft.Value,
                TopRight.Value,
                BottomRight.Value,
                BottomLeft.Value));
        }
    }
}
