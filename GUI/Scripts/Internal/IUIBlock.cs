
using Aura.Events;
using Aura.Internal;
using Aura.Internal.Rendering;

namespace Aura
{
    internal interface IUIBlock : IEventTarget, IRenderBlock
    {
        IInputTarget InputTarget { get; }
    }
}
