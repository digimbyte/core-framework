
using UnityEngine;

namespace Aura.Internal.Rendering
{
    internal static class ShaderPropertyIDs
    {
        // Material properties
        public static int ZWrite;
        public static int ZTest;
        public static int SrcBlend;
        public static int DestBlend;
        public static int AdditiveLightingSrcBlend;
        public static int AdditiveLightingDstBlend;
        public static int CullMode;

        // Shared
        public static int WorldFromLocalTransform;
        public static int LocalFromWorldTransform;
        public static int TransformsAndLighting;
        public static int FirstIndex;
        public static int LastIndex;
        public static int ViewingFromBehind;
        public static int ShaderData;
        public static int DataIndices;

        // Clip Mask
        public static int VisualModifierCount;
        public static int VisualModifersFromRoot;
        public static int ClipRectInfos;
        public static int ClipMaskParams;
        public static int ClipMaskIndex;
        public static int GlobalColorModifiers;
        public static int ClipMaskTexture;

        // UIBlock2D
        public static int SubQuadVerts;
        public static int EdgeSoftenWidth;
        public static int DynamicTexture;
        public static int StaticTexture;

        // Drop shadow
        public static int PerBlockData;

        /// <summary>
        /// We can't init these statically because it complains about calling Shader.PropertyToID
        /// </summary>
        public static void Init()
        {
            ZWrite = Shader.PropertyToID("_ZWrite");
            ZTest = Shader.PropertyToID("_ZTest");
            SrcBlend = Shader.PropertyToID("_SrcBlend");
            DestBlend = Shader.PropertyToID("_DstBlend");
            AdditiveLightingSrcBlend = Shader.PropertyToID("_AdditiveLightingSrcBlend");
            AdditiveLightingDstBlend = Shader.PropertyToID("_AdditiveLightingDstBlend");
            CullMode = Shader.PropertyToID("_CullMode");

            // Shared
            WorldFromLocalTransform = Shader.PropertyToID("_AuraWorldFromLocal");
            LocalFromWorldTransform = Shader.PropertyToID("_AuraLocalFromWorld");
            TransformsAndLighting = Shader.PropertyToID("_AuraTransformsAndLighting");
            FirstIndex = Shader.PropertyToID("_AuraFirstIndex");
            LastIndex = Shader.PropertyToID("_AuraLastIndex");
            ViewingFromBehind = Shader.PropertyToID("_AuraViewingFromBehind");
            ShaderData = Shader.PropertyToID("_AuraData");
            DataIndices = Shader.PropertyToID("_AuraDataIndices");

            // Clip Mask
            VisualModifierCount = Shader.PropertyToID("_AuraVisualModifierCount");
            VisualModifersFromRoot = Shader.PropertyToID("_AuraVisualModifiersFromRoot");
            ClipRectInfos = Shader.PropertyToID("_AuraClipRectInfos");
            ClipMaskParams = Shader.PropertyToID("_AuraClipMaskParams");
            ClipMaskIndex = Shader.PropertyToID("_AuraClipMaskIndex");
            GlobalColorModifiers = Shader.PropertyToID("_AuraGlobalColorModifiers");
            ClipMaskTexture = Shader.PropertyToID("_ClipMaskTex");

            // UIBlock2D
            SubQuadVerts = Shader.PropertyToID("_AuraSubQuadVerts");
            EdgeSoftenWidth = Shader.PropertyToID("_AuraEdgeSoftenWidth");

            // Image/Color
            DynamicTexture = Shader.PropertyToID("_AuraDynamicTexture");
            StaticTexture = Shader.PropertyToID("_AuraTextureArray");

            // Drop Shadow
            PerBlockData = Shader.PropertyToID("_AuraPerBlockData");
        }
    }
}
