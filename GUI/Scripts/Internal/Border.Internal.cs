
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nova.Internal
{
    internal enum BorderDirection
    {
        Out = 0,
        Center = 1,
        In = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Border : System.IEquatable<Border>
    {
        public Color Color;
        public Length Width;
        public bool Enabled;
        public BorderDirection Direction;
        public bool disableTopLeft;
        public bool disableTop;
        public bool disableTopRight;
        public bool disableLeft;
        public bool disableRight;
        public bool disableBottomLeft;
        public bool disableBottom;
        public bool disableBottomRight;

        public int DisabledSegments => (disableTopLeft ? 1 : 0) | (disableTop ? 2 : 0) | (disableTopRight ? 4 : 0) | (disableLeft ? 8 : 0) | (disableRight ? 16 : 0) | (disableBottomLeft ? 32 : 0) | (disableBottom ? 64 : 0) | (disableBottomRight ? 128 : 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Border other)
        {
            return
                Color.Equals(other.Color) &&
                Width == other.Width &&
                Enabled == other.Enabled &&
                Direction == other.Direction &&
                DisabledSegments == other.DisabledSegments;
        }
    }
}
