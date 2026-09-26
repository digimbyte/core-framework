Shader "Hidden/Aura/AuraTextBlockUnlit"
{
    Properties
    {
        [HDR]_FaceColor ("Face Color", Color) = (1, 1, 1, 1)
        _FaceDilate ("Face Weight", Range(-0.25, 0.25)) = 0

        _OutlineColor ("Border Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outward Border Thickness", Range(0, 1)) = 0
        [HideInInspector] _OutlineSoftness ("TMP Padding", Float) = 0

        [HDR]_UnderlayColor ("Shadow Color", Color) = (0, 0, 0, .5)
        _UnderlayOffsetX ("Shadow Offset X", Range(-0.2, 0.2)) = 0
        _UnderlayOffsetY ("Shadow Offset Y", Range(-0.2, 0.2)) = 0
        [HideInInspector] _UnderlayDilate ("TMP Shadow Padding", Float) = 0
        _UnderlaySoftness ("Shadow Softness", Range(0, 0.2)) = 0

        _WeightNormal ("Weight Normal", float) = 0
        _WeightBold ("Weight Bold", float) = .5

        _ShaderFlags ("Flags", float) = 0
        _ScaleRatioA ("Scale RatioA", float) = 1
        _ScaleRatioB ("Scale RatioB", float) = 1
        _ScaleRatioC ("Scale RatioC", float) = 1

        _MainTex ("Font Atlas", 2D) = "white" { }
        _TextureWidth ("Texture Width", float) = 512
        _TextureHeight ("Texture Height", float) = 512
        _GradientScale ("Gradient Scale", float) = 5
        _ScaleX ("Scale X", float) = 1
        _ScaleY ("Scale Y", float) = 1
        _PerspectiveFilter ("Perspective Correction", Range(0, 1)) = 0.875
        _Sharpness ("Sharpness", Range(-1, 1)) = 0

        _VertexOffsetX ("Vertex OffsetX", float) = 0
        _VertexOffsetY ("Vertex OffsetY", float) = 0

        _MaskSoftnessX ("Mask SoftnessX", float) = 0
        _MaskSoftnessY ("Mask SoftnessY", float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _CullMode ("Cull Mode", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
        [HideInInspector]
        _ClipMaskTex ("ClipMaskTex", 2D) = "white" { }
        [HideInInspector]
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "DisableBatching" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
        ZWrite Off
        Lighting Off
        Fog
        {
            Mode Off
        }
        
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM

            #pragma vertex AuraVert
            #pragma fragment AuraFrag
            #pragma target 3.5
            // 
            
            #define PROCEDURAL_INSTANCING_ON
            #pragma instancing_options procedural:setup
            #pragma instancing_options assumeuniformscaling
            #pragma instancing_options nolightmap
            #pragma instancing_options nolodfade
            #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2

            #pragma multi_compile_local __ OUTLINE_ON
            #pragma multi_compile_local __ UNDERLAY_ON UNDERLAY_INNER

            #pragma multi_compile_local __ NOVA_CLIP_RECT NOVA_CLIP_MASK
            #pragma multi_compile_local __ NOVA_SUPER_SAMPLE
            #pragma multi_compile_local __ NOVA_FALLBACK_RENDERING
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #define NOVA_PREMUL_COLORS
            #include "../Aura.cginc"
            #include "../AuraTMPProperties.cginc"

            NOVA_DECLARE_BUFFER(PerVertTextData, _AuraData);

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 atlas : TEXCOORD0;
                float weight : TEXCOORD1;
                float4 color : COLOR;
                #if defined(NOVA_CLIPPING)
                    float3 rootPos : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f AuraVert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                AuraVertInit(instanceID, v2f, o);
                uint index = InstanceIDToDataIndex(instanceID);
                NOVA_GET_BUFFER_ITEM_uint(offsetInstanceID, index, _AuraDataIndices);
                uint vertIndex = 4u * offsetInstanceID + vertexID;
                NOVA_GET_BUFFER_ITEM_PerVertTextData(textData, vertIndex, _AuraData);
                NOVA_GET_BUFFER_ITEM_TransformAndLighting(transformAndLighting, textData.TransformIndex, _AuraTransformsAndLighting);
                float3 blockPos = textData.Position;
                blockPos.xy += float2(_VertexOffsetX, _VertexOffsetY);
                float3 rootSpace = mul(transformAndLighting.RootFromBlock, float4(blockPos, 1)).xyz;
                o.pos = UnityWorldToClipPos(AuraRootToWorldPos(rootSpace));
                o.atlas = textData.Texcoord0.xy;
                float bold = step(textData.Texcoord1.y * textData.ScaleMultiplier, 0);
                // Face weight is independent of outline-dependent TMP ratios.
                o.weight = (lerp(_WeightNormal, _WeightBold, bold) * 0.25 + clamp(_FaceDilate, -0.25, 0.25)) * 0.5;
                o.color = UnpackColor(textData.Color);
                #if defined(NOVA_CLIPPING)
                    o.rootPos = rootSpace;
                #endif
                return o;
            }

            float4 CrispLayers(float2 atlas, float weight, float4 vertexColor, float aa)
            {
                float distance = tex2D(_MainTex, atlas).a - 0.5 + weight;
                float face = saturate(distance / aa + 0.5);
                // Reserve one atlas texel for filtering; 1 uses the full safe
                // outward range without changing the letter face.
                float available = max(0, 0.5 - rcp(max(_GradientScale, 1)) - max(weight, 0));
                float width = saturate(_OutlineWidth) * available;
                float expanded = saturate((distance + width) / aa + 0.5);
                float border = max(0, expanded - face);
                float4 c = float4(vertexColor.rgb * _FaceColor.rgb * _FaceColor.a, _FaceColor.a) * face;
                c += float4(_OutlineColor.rgb * _OutlineColor.a, _OutlineColor.a) * border;

                #if defined(UNDERLAY_ON) || defined(UNDERLAY_INNER)
                    float2 offset = clamp(float2(_UnderlayOffsetX, _UnderlayOffsetY), -0.2, 0.2);
                    float blur = clamp(_UnderlaySoftness, 0, 0.2) * 0.5;
                    float extent = max(abs(offset.x), abs(offset.y)) + blur;
                    float fit = min(1, available / max(extent, 0.0001));
                    offset *= fit;
                    blur *= fit;
                    float2 shadowAtlas = atlas - offset * _GradientScale / float2(_TextureWidth, _TextureHeight);
                    float shadowDistance = tex2D(_MainTex, shadowAtlas).a - 0.5 + weight;
                    float shadow = smoothstep(-aa * 0.5 - blur, aa * 0.5 + blur, shadowDistance);
                    c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * shadow * (1 - expanded);
                #endif
                return c * vertexColor.a;
            }

            float4 AuraFrag(v2f i) : SV_Target
            {
                AuraFragInit(i);
                float sampleValue = tex2D(_MainTex, i.atlas).a;
                float aa = max(length(float2(ddx(sampleValue), ddy(sampleValue))), 0.0001);
                #if defined(NOVA_SUPER_SAMPLE)
                    float2 dx = ddx(i.atlas) * 0.25;
                    float2 dy = ddy(i.atlas) * 0.25;
                    // Average coverage, not distance, for fine contours.
                    float4 c = (CrispLayers(i.atlas + dx + dy, i.weight, i.color, aa)
                              + CrispLayers(i.atlas - dx + dy, i.weight, i.color, aa)
                              + CrispLayers(i.atlas + dx - dy, i.weight, i.color, aa)
                              + CrispLayers(i.atlas - dx - dy, i.weight, i.color, aa)) * 0.25;
                #else
                    float4 c = CrispLayers(i.atlas, i.weight, i.color, aa);
                #endif
                #if defined(NOVA_CLIP_RECT)
                    c = ApplyGlobalColorModification(c);
                #elif defined(NOVA_CLIP_MASK)
                    c = ApplyClipMaskAndColorModifiers(c, i.rootPos);
                #endif
                #if defined(NOVA_CLIPPING)
                    c = ApplyVisualModiferClipping(c, i.rootPos);
                #endif
                #if UNITY_UI_ALPHACLIP
                    clip(c.a - 0.001);
                #endif
                return c;
            }

            NOVA_DUMMY_INSTANCE_SETUP
            ENDCG
        }
    }
}
