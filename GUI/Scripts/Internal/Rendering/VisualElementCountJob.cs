
using Aura.Compat;
using Aura.Internal.Collections;
using Aura.Internal.Core;
using Unity.Burst;
using Unity.Collections;

namespace Aura.Internal.Rendering
{
    [BurstCompile]
    internal struct VisualElementCountJob : IAuraJob
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatchRoots;
        [ReadOnly]
        public AuraHashMap<DataStoreID, AuraList<VisualElementIndex, VisualElement>> VisualElements;
        [ReadOnly]
        public AuraHashMap<DataStoreID, RotationSetSummary> RotationSets;

        [WriteOnly]
        public NativeReference<RenderEngineUpdateCounts> VisualElementCount;

        public void Execute()
        {
            RenderEngineUpdateCounts counts = default;
            for (int i = 0; i < DirtyBatchRoots.Length; ++i)
            {
                DataStoreID batchRootID = DirtyBatchRoots[i];
                counts.VisualElementCount += VisualElements[batchRootID].Length;

                RotationSetSummary rotationSet = RotationSets[batchRootID];
                counts.RotationSetCount += rotationSet.SetCount;
                counts.QuadProviderCount += rotationSet.QuadProviderCount;
            }
            VisualElementCount.Value = counts;
        }
    }
}

