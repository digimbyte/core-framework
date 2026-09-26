
using System.Collections.Generic;

namespace Aura.Internal.Utilities.Extensions
{
    internal static class IEnumerableExtensions
    {
        public static void CopyTo<U, T>(this IEnumerable<U> source, List<T> dest, bool append = false) where T : U
        {
            if (!append)
            {
                dest.Clear();
            }

            foreach (U obj in source)
            {
                T typed = (T)obj;

                if (typed == null)
                {
                    continue;
                }

                dest.Add(typed);
            }
        }
    }
}
