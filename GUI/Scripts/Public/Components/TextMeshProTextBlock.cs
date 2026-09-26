
using TMPro;
using UnityEngine;

namespace Aura.TMP
{
    [AddComponentMenu("Aura/Text Mesh Pro - TextBlock")]
    public sealed class TextMeshProTextBlock : TextMeshPro
    {
        public override void SetVerticesDirty()
        {
            base.SetVerticesDirty();

            if (m_OnDirtyVertsCallback != null)
            {
                m_OnDirtyVertsCallback();
            }
        }

        internal void DisableSubmeshRenderers() => SetActiveSubTextObjectRenderers(false);
    }
}
