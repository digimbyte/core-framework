
using UnityEngine;

namespace Aura.Internal.Rendering
{
    internal interface ITexturePackSubscriber
    {
        void HandleTextureArrayRecreated(Texture2DArray textureArray);
    }
}
