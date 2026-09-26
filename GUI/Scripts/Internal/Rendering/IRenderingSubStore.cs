
using Aura.Compat;
using Aura.Internal.Collections;
using Aura.Internal.Common;
using Aura.Internal.Core;
using Aura.Internal.Hierarchy;
using Unity.Collections;

namespace Aura.Internal.Rendering
{
    internal struct RenderingPreUpdateData
    {
        [ReadOnly]
        public AuraHashMap<DataStoreID, DataStoreIndex> DataStoreIDToDataStoreIndex;
        [ReadOnly]
        public NativeList<BatchGroupElement> AllBatchGroupElements;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;

        public RenderingDirtyState DirtyState;
    }


    internal interface IRenderingSubStore<T,TIndex> : IInitializable where TIndex : IIndex<TIndex>
    {
        bool RemoveAtSwapBack(DataStoreID dataStoreID, TIndex index, out DataStoreIndex swappedForwardIndex);
        void ClearDirtyState();
    }
}
