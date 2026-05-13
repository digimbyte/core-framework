// Copyright (c) Supernova Technologies LLC
using Nova.Compat;
using Nova.Internal.Utilities;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal partial struct SubQuadShaderDataJob : INovaJobParallelFor
    {
        private interface IBoundsConverter
        {
            /// <summary>Resolved radii: x=TL, y=TR, z=BR, w=BL. Border paths use uniform outer radius on all components.</summary>
            float4 CornerRadii { get; }
            float MaxCoverageRemainder { get; }

            RotationSpaceBounds MaxCoverageBounds { get; }
            ref RotationSpaceBounds Bounds { get; }

            /// <summary>
            /// Index => R, T, L, B
            /// </summary>
            /// <param name="cornerIndex"></param>
            /// <returns></returns>
            RotationSpaceBounds GetEdgeBounds(int edgeIndex);


            /// <summary>
            /// Index => BL, TL, TR, BR
            /// </summary>
            /// <param name="cornerIndex"></param>
            /// <returns></returns>
            RotationSpaceBounds GetCornerBounds(int cornerIndex);
        }

        private struct BodyOnly : IBoundsConverter
        {
            private QuadBoundsDescriptor descriptor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BodyOnly(ref QuadBoundsDescriptor quadBoundsDescriptor)
            {

                descriptor = quadBoundsDescriptor;
            }

            public float4 CornerRadii
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => descriptor.CornerRadii;
            }

            public float MaxCoverageRemainder
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Math.OneMinusSin45 * math.cmax(descriptor.CornerRadii);
            }

            public RotationSpaceBounds MaxCoverageBounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float halfPoint = MaxCoverageRemainder;
                    return new RotationSpaceBounds()
                    {
                        BL = descriptor.Bounds.BL + halfPoint,
                        TR = descriptor.Bounds.TR - halfPoint
                    };
                }
            }

            public unsafe ref RotationSpaceBounds Bounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    fixed (BodyOnly* ptr = &this)
                    {
                        return ref ptr->descriptor.Bounds;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetCornerBounds(int cornerIndex)
            {
                return SubQuadShaderDataJob.GetCornerBounds(cornerIndex, ref descriptor.Bounds, descriptor.CornerRadii);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetEdgeBounds(int edgeIndex)
            {
                return SubQuadShaderDataJob.GetEdgeBounds(edgeIndex, ref descriptor.Bounds, descriptor.CornerRadii);
            }
        }

        private struct BodyAndBorder : IBoundsConverter
        {
            private QuadBoundsDescriptor descriptor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BodyAndBorder(ref QuadBoundsDescriptor quadBoundsDescriptor)
            {
                descriptor = quadBoundsDescriptor;
            }

            public float4 CornerRadii
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new float4(descriptor.Border.OuterRadius);
            }

            public float MaxCoverageRemainder
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Math.OneMinusSin45 * descriptor.Border.OuterRadius;
            }

            public RotationSpaceBounds MaxCoverageBounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float halfPoint = MaxCoverageRemainder;
                    return new RotationSpaceBounds()
                    {
                        BL = descriptor.Border.Bounds.BL + halfPoint,
                        TR = descriptor.Border.Bounds.TR - halfPoint
                    };
                }
            }

            public unsafe ref RotationSpaceBounds Bounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    fixed (BodyAndBorder* ptr = &this)
                    {
                        return ref ptr->descriptor.Border.Bounds;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetCornerBounds(int cornerIndex)
            {
                return SubQuadShaderDataJob.GetCornerBounds(cornerIndex, ref descriptor.Border.Bounds, CornerRadii);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetEdgeBounds(int edgeIndex)
            {
                return SubQuadShaderDataJob.GetEdgeBounds(edgeIndex, ref descriptor.Border.Bounds, CornerRadii);
            }
        }

        private struct BorderOnly : IBoundsConverter
        {
            private QuadBoundsDescriptor descriptor;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BorderOnly(ref QuadBoundsDescriptor quadBoundsDescriptor)
            {
                descriptor = quadBoundsDescriptor;
            }

            public float4 CornerRadii
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new float4(descriptor.Border.OuterRadius);
            }

            public float MaxCoverageRemainder
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Math.OneMinusSin45 * descriptor.Border.OuterRadius;
            }

            public RotationSpaceBounds MaxCoverageBounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    float halfPoint = MaxCoverageRemainder;
                    return new RotationSpaceBounds()
                    {
                        BL = descriptor.Border.Bounds.BL + halfPoint,
                        TR = descriptor.Border.Bounds.TR - halfPoint
                    };
                }
            }

            public unsafe ref RotationSpaceBounds Bounds
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    fixed (BorderOnly* ptr = &this)
                    {
                        return ref ptr->descriptor.Border.Bounds;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetCornerBounds(int cornerIndex)
            {
                return SubQuadShaderDataJob.GetCornerBounds(cornerIndex, ref descriptor.Border.Bounds, CornerRadii);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RotationSpaceBounds GetEdgeBounds(int edgeIndex)
            {
                RotationSpaceBounds cornerCenters = new RotationSpaceBounds()
                {
                    BL = descriptor.Border.Bounds.BL + descriptor.Border.OuterRadius,
                    TR = descriptor.Border.Bounds.TR - descriptor.Border.OuterRadius
                };

                switch (edgeIndex)
                {
                    case 0:
                        // Right
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(descriptor.Border.Bounds.TR.x - descriptor.Border.BorderWidth, cornerCenters.BL.y),
                            TR = new float2(descriptor.Border.Bounds.TR.x, cornerCenters.TR.y)
                        };
                    case 1:
                        // Top
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(cornerCenters.BL.x, descriptor.Border.Bounds.TR.y - descriptor.Border.BorderWidth),
                            TR = new float2(cornerCenters.TR.x, descriptor.Border.Bounds.TR.y)
                        };
                    case 2:
                        // Left
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(descriptor.Border.Bounds.BL.x, cornerCenters.BL.y),
                            TR = new float2(descriptor.Border.Bounds.BL.x + descriptor.Border.BorderWidth, cornerCenters.TR.y)
                        };
                    case 3:
                        // Bottom
                        return new RotationSpaceBounds()
                        {
                            BL = new float2(cornerCenters.BL.x, descriptor.Border.Bounds.BL.y),
                            TR = new float2(cornerCenters.TR.x, descriptor.Border.Bounds.BL.y + descriptor.Border.BorderWidth)
                        };
                    default:
                        Debug.LogError("Invalid edge index");
                        return default;
                }
            }
        }

        // cornerIndex: 0=BL, 1=TL, 2=TR, 3=BR — same as RotationSpaceBounds.GetCorner. Radii: x=TL, y=TR, z=BR, w=BL.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvedRadiusAtCorner(int cornerIndex, float4 radii)
        {
            switch (cornerIndex)
            {
                case 0:
                    return radii.w;
                case 1:
                    return radii.x;
                case 2:
                    return radii.y;
                case 3:
                    return radii.z;
                default:
                    return 0f;
            }
        }

        // edgeIndex: 0=R, 1=T, 2=L, 3=B — smaller adjoining corner radius along that edge.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvedRadiusAtEdge(int edgeIndex, float4 radii)
        {
            switch (edgeIndex)
            {
                case 0:
                    return math.min(radii.y, radii.z);
                case 1:
                    return math.min(radii.x, radii.y);
                case 2:
                    return math.min(radii.w, radii.x);
                case 3:
                    return math.min(radii.w, radii.z);
                default:
                    return 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RotationSpaceBounds GetEdgeBounds(int edgeIndex, ref RotationSpaceBounds bounds, float4 cornerRadii)
        {
            float radius = ResolvedRadiusAtEdge(edgeIndex, cornerRadii);
            if (Math.ApproximatelyZero(radius))
            {
                // If there is no corner rounding, then we return one "edge" which is really just the entire
                // bounds
                return bounds;
            }

            switch (edgeIndex)
            {
                case 0:
                    // Right
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.TR.x - radius, bounds.BL.y + radius),
                        TR = new float2(bounds.TR.x, bounds.TR.y - radius),
                    };
                case 1:
                    // Top
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x + radius, bounds.TR.y - radius),
                        TR = new float2(bounds.TR.x - radius, bounds.TR.y),
                    };
                case 2:
                    // Left
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x, bounds.BL.y + radius),
                        TR = new float2(bounds.BL.x + radius, bounds.TR.y - radius),
                    };
                case 3:
                    // Bottom
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x, bounds.BL.y),
                        TR = new float2(bounds.TR.x - radius, bounds.BL.y + radius),
                    };
                default:
                    Debug.LogError("Invalid edge index");
                    return default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RotationSpaceBounds GetCornerBounds(int cornerIndex, ref RotationSpaceBounds bounds, float4 cornerRadii)
        {
            float radius = ResolvedRadiusAtCorner(cornerIndex, cornerRadii);
            switch (cornerIndex)
            {
                case 0:
                    // BL
                    return new RotationSpaceBounds()
                    {
                        BL = bounds.BL,
                        TR = bounds.BL + radius
                    };
                case 1:
                    // TL
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.BL.x, bounds.TR.y - radius),
                        TR = new float2(bounds.BL.x + radius, bounds.TR.y),
                    };
                case 2:
                    return new RotationSpaceBounds()
                    {
                        BL = bounds.TR - radius,
                        TR = bounds.TR
                    };
                case 3:
                    return new RotationSpaceBounds()
                    {
                        BL = new float2(bounds.TR.x - radius, bounds.BL.y),
                        TR = new float2(bounds.TR.x, bounds.BL.y + radius),
                    };
                default:
                    Debug.LogError("Invalid corner index");
                    return default;
            }
        }
    }
}
