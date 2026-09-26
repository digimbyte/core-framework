#ifndef NOVA_DROPSHADOW_INCLUDED
#define NOVA_DROPSHADOW_INCLUDED

#include "Aura.cginc"
#include "Generated/DropShadow.g.cginc"

NOVA_DECLARE_BUFFER(PerQuadDropShadowShaderData, _AuraData);
NOVA_DECLARE_BUFFER(PerInstanceDropShadowShaderData, _AuraPerBlockData);

v2f AuraVert(AuraQuadVert v, uint instanceID : SV_InstanceID)
{
    AuraVertInit(instanceID, v2f, o);

    uint indexIntoIndexBuffer = InstanceIDToDataIndex(instanceID);
    NOVA_GET_BUFFER_ITEM_uint(perQuadDataIndex, indexIntoIndexBuffer, _AuraDataIndices);
    NOVA_GET_BUFFER_ITEM_PerQuadDropShadowShaderData(perQuadData, perQuadDataIndex, _AuraData);
    
    uint perBlockDataIndex = perQuadDataIndex / 8;
    NOVA_GET_BUFFER_ITEM_PerInstanceDropShadowShaderData(perBlockData, perBlockDataIndex, _AuraPerBlockData);
    NOVA_GET_BUFFER_ITEM_TransformAndLighting(transformAndLighting, perBlockData.TransformIndex, _AuraTransformsAndLighting);

    float2 blockPos = perQuadData.PositionInNode + perQuadData.QuadSize * v.Pos;
    float3 rootSpace = mul(transformAndLighting.RootFromBlock, float4(blockPos, 0, 1)).xyz;
    float3 worldPos = AuraRootToWorldPos(rootSpace);
    o.pos = UnityWorldToClipPos(worldPos);

    #if defined(NOVA_CLIPPING)
        SetRootPos(o, rootSpace);
    #endif
    
    AuraColorToV2F(Color, o, perBlockData.Color);

    float2 bodyCircleCenter = perBlockData.HalfBlockQuadSize - perBlockData.BlockClipRadius;
    half clipRadiusSign = sign(perBlockData.BlockClipRadius);
    float2 shadowCircleCenter = perBlockData.HalfBlockQuadSize + (1 - clipRadiusSign) * perBlockData.Width - perBlockData.BlockClipRadius;
    float shadowRadius = clipRadiusSign * (perBlockData.BlockClipRadius + perBlockData.Width);
    float blurToAdd = smoothstep(perBlockData.Blur, 0, shadowRadius) * perBlockData.Blur;
    shadowRadius += blurToAdd;
    shadowCircleCenter -= blurToAdd;

    float nFactor = GetNFactor(perBlockData.HalfBlockQuadSize);

    // Positions
    float2 nBlockPos = blockPos * nFactor;
    SetNBlockPos(o, nBlockPos);
    float2 shadowPos = blockPos - perBlockData.Offset;
    float2 nShadowPos = shadowPos * nFactor;
    SetNShadowPos(o, nShadowPos);

    // Radii
    float nBlockRadius = perBlockData.BlockClipRadius * nFactor;
    SetNBlockRadius(o, nBlockRadius);
    float nShadowRadius = shadowRadius * nFactor;
    SetNShadowRadius(o, nShadowRadius);

    // Origins
    float2 nCornerOrigin = bodyCircleCenter * nFactor;
    SetNBlockOrigin(o, nCornerOrigin);
    float2 nShadowOrigin = shadowCircleCenter * nFactor;
    SetNShadowOrigin(o, nShadowOrigin);

    float nShadowBlur = perBlockData.Blur * nFactor;
    SetNShadowBlur(o, nShadowBlur);

    SetEdgeSoftenDisabled(o, 1.0 - perBlockData.EdgeSoftenMask);

    #if defined(NOVA_RADIAL_FILL)
        float2 sinCos;
        float2 radialFillSpacePos = RadialFillVert(nBlockPos, perBlockData.RadialFillCenter, perBlockData.RadialFillRotation, perBlockData.RadialFillAngle, nFactor, sinCos);
        SetRadialFillSpacePos(o, radialFillSpacePos);
        SetRadialFillSinCos(o, sinCos);
    #endif

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

    half4 positions = half4(GetNBlockPos(i), GetNShadowPos(i));
    half4 origins = half4(GetNBlockOrigin(i), GetNShadowOrigin(i));
    half2 radii = half2(GetNBlockRadius(i), GetNShadowRadius(i));
    half2 distancesOutside = DistanceFromCircleEdge2(positions, origins, radii);

    half softenWidth = GetSoftenWidth(positions.xy);
    half softenInverse = 1.0 / softenWidth;
    half clipWeight = GetClipWeight10(distancesOutside.x, softenInverse);

    fixed4 color = GetColor(i);

    half gradientLerpVal = smoothstep(-GetNShadowBlur(i), GetNShadowBlur(i), distancesOutside.y);
    color = lerp(color, fixed4(color.xyz, 0), gradientLerpVal);

    #if defined(NOVA_CLIP_RECT)
        color = ApplyGlobalColorModification(color);
    #elif defined(NOVA_CLIP_MASK)
        color = ApplyClipMaskAndColorModifiers(color, GetRootPos(i));
    #endif

    #if defined(NOVA_LIT)
        #if defined(NOVA_SHADOW_CAST_PASS)
            // Don't assign back to color because the shadow caster pass just returns 0,
            // but we want to clip
            AuraDoLightingCalculations(i, color);
        #else
            color = AuraDoLightingCalculations(i, color);
        #endif
    #endif

    clipWeight *= step(GetEdgeSoftenDisabled(i), clipWeight);
    
    // Flip clip weight
    color = ApplyClipWeight(color, -clipWeight + 1.0);

    #if defined(NOVA_RADIAL_FILL)
        float radialFillClipWeight = GetRadialFillClipWeight(GetRadialFillSpacePos(i), GetRadialFillSinCos(i), softenWidth.x, softenInverse.x);
        color = ApplyClipWeight(color, radialFillClipWeight);
    #endif

    #if defined(NOVA_CLIPPING)
        color = ApplyVisualModiferClipping(color, GetRootPos(i));
    #endif

    #if defined(NOVA_LIT)
        clip(color.a - NOVA_EPSILON);
    #endif

    return color;
}

#endif