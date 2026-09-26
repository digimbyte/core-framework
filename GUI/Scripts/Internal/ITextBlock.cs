
using Aura.Internal.Rendering;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aura
{
    internal interface ITextBlock : IRenderBlock<Internal.TextBlockData>
    {
        void UpdateMeshSize(ref TextMargin newMargin);
    }
}
