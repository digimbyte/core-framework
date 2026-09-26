
using Aura.Compat;
using Aura.Internal.Collections;
using Aura.Internal.Common;
using Aura.Internal.Core;
using Aura.Internal.Utilities;
using Aura.Internal.Utilities.Extensions;
using Unity.Collections;

namespace Aura.Internal.Rendering
{
    /// <summary>
    /// The data common to all blocks
    /// </summary>
    internal struct CommonData : IInitializable
    {
        public NativeList<DataStoreIndex, RenderBounds> BlockRenderBounds;
        public NativeList<DataStoreIndex, int> OrderInZLayer;
        public NativeList<DataStoreIndex, AuraList<VisualElementIndex>> OverlappingElements;
        public NativeList<DataStoreIndex, VisualModifierID> VisualModifierIDs;
        public NativeList<DataStoreIndex, ComputeBufferIndex> TransformIndices;
        public NativeList<DataStoreIndex, CoplanarSetID> CoplanarSetIDs;
        public NativeList<DataStoreIndex, RotationSetID> RotationSetIDs;
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        public AuraComputeBuffer<TransformAndLightingData, TransformAndLightingData> TransformAndLightingData;
        public AuraHashMap<DataStoreID, byte> HiddenElements;

        public NativeList<AuraList<VisualElementIndex>> OverlappingElementsPool;

        public void Init()
        {
            BaseInfos.Init(Constants.AllElementsInitialCapacity);
            TransformAndLightingData.Init(Constants.AllElementsInitialCapacity);
            BlockRenderBounds.Init(Constants.AllElementsInitialCapacity);
            TransformIndices.Init(Constants.AllElementsInitialCapacity);
            OverlappingElements.Init(Constants.AllElementsInitialCapacity);
            VisualModifierIDs.Init(Constants.AllElementsInitialCapacity);
            OrderInZLayer.Init(Constants.AllElementsInitialCapacity);
            CoplanarSetIDs.Init(Constants.AllElementsInitialCapacity);
            RotationSetIDs.Init(Constants.AllElementsInitialCapacity);
            OverlappingElementsPool.Init(Constants.SomeElementsInitialCapacity);
            HiddenElements.Init();
        }

        public void Dispose()
        {
            BaseInfos.Dispose();
            TransformAndLightingData.Dispose();
            BlockRenderBounds.Dispose();
            TransformIndices.Dispose();
            OrderInZLayer.Dispose();
            CoplanarSetIDs.Dispose();
            RotationSetIDs.Dispose();
            OverlappingElements.DisposeListAndElements();
            VisualModifierIDs.Dispose();
            OverlappingElementsPool.DisposeListAndElements();
            HiddenElements.Dispose();
        }
    }
}

