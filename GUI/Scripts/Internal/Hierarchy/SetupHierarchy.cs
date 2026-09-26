
using Aura.Compat;
using Aura.Internal.Core;
using Unity.Burst;
using Unity.Collections;

namespace Aura.Internal.Hierarchy
{
    internal partial class Hierarchy
    {
        [BurstCompile]
        internal struct SetupHierarchy : IAuraJob
        {
            /// <summary>
            /// The list of roots from the hierachy batch groups
            /// </summary>
            [ReadOnly]
            public NativeList<DataStoreID> BatchRootIDs;

            /// <summary>
            /// The lookup table for all tracked elements
            /// </summary>
            [ReadOnly]
            public AuraHashMap<DataStoreID, DataStoreIndex> Lookup;

            /// <summary>
            /// The combined set of roots
            /// </summary>
            [WriteOnly]
            public AuraHashMap<DataStoreID, int> RootIndexMap;

            // The total number of hierarchy elements
            public int HierarchySize;

            public void Execute()
            {
                RootIndexMap.Clear();

                int length = BatchRootIDs.Length;
                for (int i = 0; i < length; ++i)
                {
                    DataStoreID rootID = BatchRootIDs[i];
                    if (Lookup.ContainsKey(rootID))
                    {
                        RootIndexMap.Add(rootID, i);
                    }
                }
            }
        }
    }
}
