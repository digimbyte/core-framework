
using Aura.Compat;
using Aura.Internal.Collections;
using Aura.Internal.Core;
using Aura.Internal.Utilities.Extensions;
using Unity.Burst;
using Unity.Collections;

namespace Aura.Internal.Rendering
{
    [BurstCompile]
    internal struct SubQuadVertCopyJob : IAuraJobParallelFor
    {
        [ReadOnly]
        public NativeList<DataStoreID> DirtyBatches;
        [ReadOnly]
        public AuraHashMap<DataStoreID, AuraList<VisualElementIndex, VisualElement>> VisualElements;
        [NativeDisableParallelForRestriction]
        public NativeList<RenderIndex, SubQuadData> SubQuadData;

        [NativeDisableParallelForRestriction]
        public AuraHashMap<DataStoreID, DrawCallSummary> DrawCallSummaries;
        [NativeDisableParallelForRestriction]
        public AuraHashMap<DataStoreID, AuraList<SubQuadVert>> SubQuadBuffers;

        private AuraList<SubQuadVert> shaderData;

        public void Execute(int index)
        {
            DataStoreID batchRootID = DirtyBatches[index];
            shaderData = SubQuadBuffers.GetAndClear(batchRootID);
            AuraList<VisualElementIndex, VisualElement> visualElements = VisualElements[batchRootID];

            DrawCallSummary drawCallSummary = DrawCallSummaries[batchRootID];
            for (int i = 0; i < drawCallSummary.DrawCalls.Length; ++i)
            {
                ref DrawCall drawCall = ref drawCallSummary.DrawCalls.ElementAt(i);
                ref DrawCallDescriptor descriptor = ref drawCallSummary.DrawCallDescriptors.ElementAt(drawCall.DescriptorID);

                if (descriptor.DrawCallType != VisualType.UIBlock2D)
                {
                    continue;
                }

                ref AuraList<VisualElementIndex> orderedBlocks = ref drawCallSummary.NonIndexedElements.ElementAt(drawCall.ID);
                ref ShaderIndexBounds indexBounds = ref drawCallSummary.IndexBounds.ElementAt(drawCall.ID);
                indexBounds.InstanceStart = shaderData.Length;

                for (int j = 0; j < orderedBlocks.Length; ++j)
                {
                    ref VisualElement visualElement = ref visualElements.ElementAt(orderedBlocks[j]);
                    shaderData.AddRange(ref SubQuadData.ElementAt(visualElement.RenderIndex).Verts);
                }

                indexBounds.InstanceCount = shaderData.Length - indexBounds.InstanceStart;
            }

            SubQuadBuffers[batchRootID] = shaderData;
            DrawCallSummaries[batchRootID] = drawCallSummary;
        }
    }
}
