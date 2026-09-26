
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Aura
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct ClipMaskInfo
    {
        [SerializeField]
        public Color Color;
        [SerializeField]
        public bool Clip;
        [NonSerialized]
        public bool HasMask;

        internal static readonly ClipMaskInfo Default = new ClipMaskInfo()
        {
            Color = Color.white,
            Clip = true,
        };
    }
}

