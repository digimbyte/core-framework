
using Aura.Compat;
using Aura.Internal.Common;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Aura.Internal.Utilities.Extensions
{
    internal static class AuraHashMapExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static V GetAndClear<K, V>(this ref AuraHashMap<K, V> AuraHashMap, K key)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged, IClearable
        {
            V list = AuraHashMap[key];
            list.Clear();
            return list;
        }

        public static V GetAndResize<K, V>(this ref AuraHashMap<K, V> AuraHashMap, K key, int newSize)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged, IResizable
        {
            V list = AuraHashMap[key];
            list.Length = newSize;
            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddEmpty<K, V>(this ref AuraHashMap<K, V> map, K key)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged, IInitializable
        {
            V newVal = new V();
            newVal.Init();
            map.Add(key, newVal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Init<K, V>(this ref AuraHashMap<K, V> map, int capacity = 4)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged
        {
            map = new AuraHashMap<K, V>(capacity, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetAndRemove<K, V>(this ref AuraHashMap<K, V> map, K key, out V val)
            where K : unmanaged, IEquatable<K>
            where V : unmanaged
        {
            if (map.TryGetValue(key, out val))
            {
                map.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

