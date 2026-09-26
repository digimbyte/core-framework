
using UnityEngine;

namespace Aura.Internal
{
    internal class VirtualBlockSerializer : ScriptableObject
    {
        public VirtualBlockModule Module = null;

        private void Awake()
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.HideInInspector;
        }

        private void OnEnable()
        {
            if (Module == null)
            {
                return;
            }

            Module.EnsureIDs();
        }

        private void OnDestroy()
        {
            if (Module != null)
            {
                Module.Dispose();
            }
        }
    }
}
