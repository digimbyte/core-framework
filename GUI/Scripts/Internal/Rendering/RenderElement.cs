
namespace Aura.Internal.Rendering
{
    internal struct RenderElement<T> where T : struct
    {
        public T Val;
        public RenderIndex RenderIndex;

        public RenderElement(ref T val)
        {
            Val = val;
            RenderIndex = -1;
        }
    }
}
