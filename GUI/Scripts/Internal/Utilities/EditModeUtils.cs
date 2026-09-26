
using Aura.Compat;
using System;

namespace Aura.Internal.Utilities
{
    internal static class EditModeUtils
    {
        /// <summary>
        /// Can this get stuck as true...?
        /// </summary>
        [NonSerialized]
        private static bool queued;

        /// <summary>
        /// Will queue a player loop update for the *following* edit mode frame
        /// </summary>
        public static void QueueEditorUpdateNextFrame()
        {
            if (!AuraApplication.IsEditor)
            {
                return;
            }

            if (queued || AuraApplication.IsPlaying)
            {
                return;
            }

            queued = true;
            AuraApplication.EditorDelayCall += () =>
            {
                AuraApplication.QueueEditorPlayerLoop();
                queued = false;
            };

        }
    }
}
