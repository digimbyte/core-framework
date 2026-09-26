
namespace Aura.Internal.Utilities
{
    internal static class IndexVerifier
    {
        public static bool ValidIndex(int index, int length)
        {
            return index >= 0 && index < length;
        }
    }

}
