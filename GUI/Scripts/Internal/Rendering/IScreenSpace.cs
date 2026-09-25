
using System.Collections.Generic;
using UnityEngine;

namespace Nova.Internal.Rendering
{
    internal interface IScreenSpace
    {
        EntityId CameraID { get; }

        List<Camera> AdditionalCameras { get; }

        void Update();
    }
}

