#ifndef NOVA_UIBLOCK_3D
#define NOVA_UIBLOCK_3D

#include "Aura.cginc"
#include "Generated/UIBlock3D.g.cginc"

NOVA_DECLARE_BUFFER(UIBlock3DData, _AuraData);

v2f AuraVert(UIBlock3DVert v, uint instanceID : SV_InstanceID)
{
    AuraVertInit(instanceID, v2f, o);

    uint indexIntoIndexBuffer = InstanceIDToDataIndex(instanceID);
    NOVA_GET_BUFFER_ITEM_uint(index, indexIntoIndexBuffer, _AuraDataIndices);
    NOVA_GET_BUFFER_ITEM_UIBlock3DData(shaderData, index, _AuraData);

    half rCorner = AuraPickCornerRadius(v.Pos.xy, (half4)shaderData.CornerRadii);
    float3 vertNodePos = v.Pos * shaderData.Size + v.CornerOffsetDir * (float)rCorner + v.EdgeOffsetDir * shaderData.EdgeRadius;

    NOVA_GET_BUFFER_ITEM_TransformAndLighting(transformAndLighting, shaderData.TransformIndex, _AuraTransformsAndLighting);
    float3 rootSpace = mul(transformAndLighting.RootFromBlock, float4(vertNodePos, 1)).xyz;
    float3 worldPos = AuraRootToWorldPos(rootSpace);
    o.pos = UnityWorldToClipPos(worldPos);

    #if defined(NOVA_CLIPPING)
        SetRootPos(o, rootSpace);
    #endif

    AuraColorToV2F(Color, o, shaderData.Color);

    #if defined(NOVA_LIT)
        AuraSetLitV2FParams(o, transformAndLighting);
        SetWorldPos(o, worldPos);
        float3 rootNormal = AuraRootFromBlockNormal(transformAndLighting.RootFromBlock, v.Normal);
        float3 worldNormal = UnityObjectToWorldNormal(rootNormal);
        SetWorldNormal(o, worldNormal);

        AuraInitInstance(appdata_full, appdata);
        appdata.vertex = float4(rootSpace, 1);
        appdata.normal = rootNormal;

        AuraDoLitVert(o, worldPos, worldNormal, appdata);
    #endif


    return o;
}

fixed4 AuraFrag(v2f i) : SV_Target
{
    AuraFragInit(i);

    fixed4 color = GetColor(i);

    #if defined(NOVA_CLIPPING)
        half clipWeight = GetTotalVisualModifierClipping(GetRootPos(i));
        // NOTE: We don't soften here, just clip
        clip(clipWeight - 1.0);
    #endif
    
    #if defined(NOVA_CLIP_RECT)
        color = ApplyGlobalColorModification(color);
    #elif defined(NOVA_CLIP_MASK)
        color = ApplyClipMaskAndColorModifiers(color, GetRootPos(i));
    #endif

    #if defined(NOVA_LIT)
        color = AuraDoLightingCalculations(i, color);
    #endif

    return color;
}

#endif