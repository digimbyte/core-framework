/*! \cond PRIVATE */

using System.Collections.Generic;
using UnityEngine;

public static class AudioClipOptimizer {
    private static readonly Dictionary<EntityId, string> AudioClipNameByInstanceId = new Dictionary<EntityId, string>();

    public static string CachedName(this AudioClip clip)
    {
        var instanceId = clip.GetEntityId();
        if (AudioClipNameByInstanceId.ContainsKey(instanceId))
        {
            return AudioClipNameByInstanceId[instanceId];
        }

        var clipName = clip.name; // allocate
        AudioClipNameByInstanceId.Add(instanceId, clipName);

        return clipName;
    }
}
/*! \endcond */