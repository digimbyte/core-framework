
using Aura.Compat;
using Aura.Internal.Collections;
using Aura.Internal.Common;
using Aura.Internal.Utilities.Extensions;
using Unity.Collections;

namespace Aura.Internal.Rendering
{
    internal struct RenderOrderDependencies : IInitializable
    {
        public NativeList<AuraList<CoplanarSetIdentifier>> lists;
        private int nextList;
        public AuraHashMap<CoplanarSetIdentifier, int> setToDependencies;

        public AuraList<CoplanarSetIdentifier> this[CoplanarSetIdentifier index] => lists[setToDependencies[index]];

        public NativeArray<CoplanarSetIdentifier> GetKeyArray(Allocator allocator)
        {
            return setToDependencies.GetKeyArray(allocator);
        }

        public bool TryGetDependencies(CoplanarSetIdentifier set, out AuraList<CoplanarSetIdentifier> dependencies)
        {
            if (!setToDependencies.TryGetValue(set, out int index))
            {
                dependencies = default;
                return false;
            }

            dependencies = lists[index];
            return true;
        }

        public void AddDependency(CoplanarSetIdentifier val, CoplanarSetIdentifier mustRenderOver)
        {
            if (setToDependencies.TryGetValue(val, out int index))
            {
                lists.ElementAt(index).Add(mustRenderOver);
            }
            else
            {
                setToDependencies.Add(val, nextList);
                if (nextList >= lists.Length)
                {
                    AuraList<CoplanarSetIdentifier> newList = new AuraList<CoplanarSetIdentifier>(0, Allocator.Persistent);
                    newList.Add(mustRenderOver);
                    lists.Add(newList);
                }
                else
                {
                    lists.ElementAt(nextList).Add(mustRenderOver);
                }
                nextList += 1;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < lists.Length; ++i)
            {
                lists.ElementAt(i).Clear();
            }
            nextList = 0;
            setToDependencies.Clear();
        }

        public void Init()
        {
            lists.Init();
            nextList = 0;
            setToDependencies.Init();
        }

        public void Dispose()
        {
            lists.DisposeListAndElements();
            setToDependencies.Dispose();
        }
    }
}