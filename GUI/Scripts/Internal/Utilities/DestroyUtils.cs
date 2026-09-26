
using Aura.Compat;
using UnityEngine;

namespace Aura.Internal.Utilities
{
    internal static class DestroyUtils
    {
        /// <summary>
        /// Horrible name, I know. It just destroys the component properly depending on the
        /// play/editor state
        /// </summary>
        /// <param name="obj"></param>
        public static void Destroy(Object obj)
        {
            if (AuraApplication.IsEditor)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(obj);
                }
                else
                {
                    Object.DestroyImmediate(obj);
                }
            }
            else
            {
                Object.Destroy(obj);
            }
        }

        public static void SafeDestroy(Object obj)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
}

