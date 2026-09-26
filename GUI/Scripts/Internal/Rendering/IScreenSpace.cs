
using System.Collections.Generic;
using UnityEngine;

namespace Aura.Internal.Rendering
{
    internal interface IScreenSpace
    {
        EntityId CameraID { get; }

        List<Camera> AdditionalCameras { get; }

        void Update();
    }
}

