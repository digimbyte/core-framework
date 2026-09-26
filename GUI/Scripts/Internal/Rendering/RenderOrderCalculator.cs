
using Aura.Compat;
using Aura.Internal.Collections;
using Aura.Internal.Core;
using Aura.Internal.Hierarchy;
using Unity.Collections;

namespace Aura.Internal.Rendering
{
    internal struct RenderOrder : System.IComparable<RenderOrder>
    {
        public SortGroupInfo SortGroupInfo;
        public int OrderInSortGroup;

        public int RenderQueue => SortGroupInfo.RenderQueue;

        public int CompareTo(RenderOrder other)
        {
            if (RenderQueue != other.RenderQueue)
            {
                return RenderQueue.CompareTo(other.RenderQueue);
            }

            if (SortGroupInfo.SortingOrder != other.SortGroupInfo.SortingOrder)
            {
                return SortGroupInfo.SortingOrder.CompareTo(other.SortGroupInfo.SortingOrder);
            }

            return OrderInSortGroup.CompareTo(other.OrderInSortGroup);
        }

        public static readonly RenderOrder RenderUnderEverything = new RenderOrder()
        {
            SortGroupInfo = new SortGroupInfo()
            {
                SortingOrder = int.MinValue,
                RenderQueue = int.MinValue,
                RenderOverOpaqueGeometry = false,
            },
            OrderInSortGroup = int.MinValue,
        };
    }

    internal struct RenderOrderCalculator
    {
        [ReadOnly]
        public NativeList<BatchGroupElement> BatchGroupElements;
        [ReadOnly]
        public AuraHashMap<DataStoreID, ZLayerCounts> ZLayerCounts;
        [ReadOnly]
        public NativeList<RenderElement<BaseRenderInfo>> BaseInfos;
        [ReadOnly]
        public NativeList<DataStoreIndex, int> OrderInZLayer;
        [ReadOnly]
        public AuraHashMap<DataStoreID, SortGroupInfo> SortGroupInfos;

        public RenderOrder GetRenderOrder(DataStoreIndex dataStoreIndex)
        {
            BatchGroupElement batchGroupElement = BatchGroupElements[dataStoreIndex];
            if (!ZLayerCounts.TryGetValue(batchGroupElement.BatchRootID, out ZLayerCounts zlayerCounts))
            {
                return RenderOrder.RenderUnderEverything;
            }

            RenderOrder toRet = new RenderOrder();
            if (!SortGroupInfos.TryGetValue(batchGroupElement.BatchRootID, out toRet.SortGroupInfo))
            {
                toRet.SortGroupInfo = SortGroupInfo.Default;
            }

            short zlayer = BaseInfos[dataStoreIndex].Val.ZIndex;
            int offset = zlayerCounts.GetRenderOrderOffset(zlayer);
            toRet.OrderInSortGroup = OrderInZLayer[dataStoreIndex] + offset;
            return toRet;
        }
    }
}

