
namespace Aura.Internal
{
    internal interface IHierarchyActivatable
    {
        bool Activated { get; }
        bool ActiveInHierarchy { get; }
        bool ActiveSelf { get; }
        bool Deactivating { get; }
    }
}
