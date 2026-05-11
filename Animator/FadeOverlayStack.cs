using System.Collections.Generic;
using UnityEngine;
using Nova;

namespace Core.Animator
{
    /// <summary>
    /// Simple reference-counting manager for shared UIBlock2D fade overlays.
    /// Use Push to claim the overlay (will enable its GameObject), and Pop to release it.
    /// The overlay GameObject is only disabled when the last claimant releases it.
    /// This allows multiple systems to 'stack' requests to show the full-screen fade overlay.
    /// </summary>
    public static class FadeOverlayStack
    {
        private static readonly Dictionary<UIBlock2D, int> counts = new Dictionary<UIBlock2D, int>();

        public static void Push(UIBlock2D block)
        {
            if (block == null) return;

            if (!counts.TryGetValue(block, out int c))
            {
                counts[block] = 1;
                if (!block.gameObject.activeSelf)
                {
                    block.gameObject.SetActive(true);
                }
            }
            else
            {
                counts[block] = c + 1;
            }
        }

        public static void Pop(UIBlock2D block)
        {
            if (block == null) return;

            if (!counts.TryGetValue(block, out int c))
            {
                // nothing to pop; don't change GameObject active state
                return;
            }

            c--;
            if (c <= 0)
            {
                counts.Remove(block);
                if (block.gameObject.activeSelf)
                {
                    block.gameObject.SetActive(false);
                }
            }
            else
            {
                counts[block] = c;
            }
        }

        public static int Count(UIBlock2D block)
        {
            if (block == null) return 0;
            counts.TryGetValue(block, out int c);
            return c;
        }
    }
}
