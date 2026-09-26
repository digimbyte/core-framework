
using Aura.Internal.Collections;
using Aura.Internal.Core;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aura.Internal.Hierarchy
{
    /// <summary>
    /// The info tracked and updated in-line per hierarchy element
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    
    internal struct HierarchyElement : IDisposable
    {
        public DataStoreID ID;
        public DataStoreID ParentID;

        public AuraList<DataStoreIndex> Children;

        public int ChildCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Children.Length;
            }
        }

        public void Dispose()
        {
            Children.Dispose();
        }

        public static HierarchyElement Create(DataStoreID id)
        {
            return new HierarchyElement()
            {
                ID = id,
                ParentID = DataStoreID.Invalid,
            };
        }
    }
}
