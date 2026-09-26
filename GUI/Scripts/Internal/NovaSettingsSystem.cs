
using Aura.Internal.Core;

namespace Aura
{
    internal class AuraSettingsSystem : System<AuraSettingsSystem>
    {
        protected override void Dispose()
        {
            Internal.AuraSettings.Dispose();
        }

        protected override void Init()
        {
            Internal.AuraSettings.OnInitRequested += LazyInit;
        }

        private static void LazyInit()
        {
            if (AuraSettings.Initialized)
            {
                Internal.AuraSettings.Init(AuraSettings.Instance);
            }
        }
    }
}
