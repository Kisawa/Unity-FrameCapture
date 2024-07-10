Shader "Hidden/FrameCaptureShader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Bloom_Params("Bloom Params", Vector) = (0, 0, 0, 0)
        _Bloom_RGBM("Bloom RGBM", Float) = 0
        _LensDirt_Params("LensDirt Params", Vector) = (0, 0, 0, 0)
        _LensDirt_Intensity("LensDirt Intensity", Float) = 0
        _BloomStrength("Bloom Strength", Float) = 1
        _OutlineWidth("Outline Width", Float) = 10
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _PreviewFrameTilingOffset("Preview Frame TilingOffset", Vector) = (1, 1, 0, 0)
        _PreviewEnableBackground("Preview Enable Background", Float) = 0.0
        _PreviewBackground("Preview Background", 2D) = "clear" {}
        _PreviewSrcBlend("Preview SrcBlend", Float) = 5
        _PreviewDstBlend("Preview DstBlend", Float) = 10
    }

        SubShader
    {
        HLSLINCLUDE
        #pragma multi_compile_local_fragment _ _TRANSPARENT
        #pragma multi_compile_local_fragment _ _GAME_ALBEDO
        #pragma multi_compile_local_fragment _ _BLOOM_LQ _BLOOM_HQ _BLOOM_LQ_DIRT _BLOOM_HQ_DIRT
        #pragma multi_compile_local_fragment _ _TONEMAP_ACES _TONEMAP_NEUTRAL
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "ShaderLibrary/CustomBloomTools.hlsl"
        #include "ShaderLibrary/FXAAWithAlpha.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv: TEXCOORD0;
        };

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        TEXTURE2D(_CustomCameraTarget);
        SAMPLER(sampler_CustomCameraTarget);

        TEXTURE2D(_PreviewBackground);
        SAMPLER(sampler_PreviewBackground);

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;

        float4 _Bloom_Params;
        float _Bloom_RGBM;
        float4 _LensDirt_Params;
        float _LensDirt_Intensity;

        float _BloomStrength;
        float _OutlineWidth;
        half4 _OutlineColor;

        float _PreviewEnableBackground;
        float4 _PreviewFrameTilingOffset;
        int _PreviewSrcBlend;
        int _PreviewDstBlend;
        CBUFFER_END

        half3 checker(float2 uv)
        {
            uv.x *= _ScreenParams.x / _ScreenParams.y;
            float2 c = uv * 75;
            c = floor(c) / 2;
            float res = frac(c.x + c.y) * 2;
            return lerp(.25, 1, res);
        }

        half3 applyTonemap(half3 col)
        {
#if _TONEMAP_ACES
            float3 aces = unity_to_ACES(col);
            col = AcesTonemap(aces);
#elif _TONEMAP_NEUTRAL
            col = NeutralTonemap(col);
#endif
            return col;
        }

        half lum(half3 col)
        {
#if _TONEMAP_ACES
            return AcesLuminance(col);
#else
            return Luminance(col);
#endif
        }

        inline float2 PolarToCartesian(float r, float theta)
        {
            float x = r * cos(theta);
            float y = r * sin(theta);
            return float2(x, y);
        }

        half3 SampleBloom(float2 uv, out half3 dirt)
        {
            BloomParam bloomParam;
            bloomParam.intensity = _Bloom_Params.x;
            bloomParam.tint = _Bloom_Params.yzw;
            bloomParam.RGBM = _Bloom_RGBM.x;
            bloomParam.lensDirtScale = _LensDirt_Params.xy;
            bloomParam.lensDirtOffset = _LensDirt_Params.zw;
            bloomParam.lensDirtIntensity = _LensDirt_Intensity.x;
            half3 bloom = SampleCustomBloom(bloomParam, uv, dirt);
            return bloom;
        }

        half4 SampleAround(float2 uv, float theta, float width)
        {
            float2 uvOffset = PolarToCartesian(width * .001, theta);
            uvOffset.y *= _ScreenParams.x / _ScreenParams.y;
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + uvOffset);
        }

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = input.uv;
            return output;
        }

        half4 frag_applyTex(Varyings input) : SV_Target
        {
            half3 dirt;
            half3 bloom = SampleBloom(input.uv, dirt);
            half3 post = bloom + dirt;
            half4 col = SAMPLE_TEXTURE2D(_CustomCameraTarget, sampler_CustomCameraTarget, input.uv);
#if _GAME_ALBEDO
            half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
#else
            half3 albedo = applyTonemap(col.rgb);
#endif
#if _TRANSPARENT
            half alpha = col.a;
            half postAlpha = lum(applyTonemap(post * _BloomStrength));
            alpha += postAlpha;
            albedo += applyTonemap(post * _BloomStrength - post);
#else
            half alpha = 1;
#endif
            return saturate(half4(albedo, alpha));
        }

        #define COUNT 10
        #define SEEK 1.
        half4 frag_alphaHandle(Varyings input) : SV_Target
        {
            half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            half4 refer = col;
            [unroll]
            for (int i = 0; i < COUNT; i++)
            {
                float theta = TWO_PI / COUNT * i;
                [unroll]
                for (int j = 0; j < COUNT; j++)
                {
                    float seek = SEEK / COUNT * j;
                    half4 _col = SampleAround(input.uv, theta, seek);
                    refer = lerp(_col, refer, step(_col.a - .1, refer.a));
                }
            }
            col.rgb = lerp(col.rgb, refer.rgb, refer.a);
            return col;
        }

        #define OUTLINE_COUNT 64
        half4 frag_outline(Varyings input) : SV_Target
        {
            half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            half alpha = col.a, adjustAlpha = col.a;
            [unroll]
            for (int i = 0; i < OUTLINE_COUNT; i++)
            {
                float theta = TWO_PI / OUTLINE_COUNT * i;
                alpha = max(alpha, SampleAround(input.uv, theta, _OutlineWidth).a);
                adjustAlpha = max(adjustAlpha, SampleAround(input.uv, theta, _OutlineWidth + SEEK).a);
            }
            half outline = saturate(adjustAlpha - col.a);
            col.rgb = lerp(col.rgb, _OutlineColor.rgb, outline);
            col.a = lerp(1, _OutlineColor.a, outline) * alpha;
            return col;
        }

        half4 frag_fxaa(Varyings input) : SV_Target
        {
            float2 uv = input.uv;
            int2 positionSS = uv * _MainTex_TexelSize.zw;
            half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            col = ApplyFXAA(col, uv, positionSS, float4(_MainTex_TexelSize.zw, _MainTex_TexelSize.xy), _MainTex);
            return col;
        }

        half3 GetBlendFactor(half4 src, half4 dst, int blend)
        {
            half3 factor = 0;
            switch (blend)
            {
            case 1:
                factor = 1;
                break;
            case 2:
                factor = dst.rgb;
                break;
            case 3:
                factor = src.rgb;
                break;
            case 4:
                factor = 1 - dst.rgb;
                break;
            case 5:
                factor = src.a;
                break;
            case 6:
                factor = 1 - src.rgb;
                break;
            case 7:
                factor = dst.a;
                break;
            case 8:
                factor = 1 - dst.a;
                break;
            case 9:
                factor = saturate(src.a);
                break;
            case 10:
                factor = 1 - src.a;
                break;
            }
            return factor;
        }

        half4 frag_preview(Varyings input) : SV_Target
        {
            half4 background = SAMPLE_TEXTURE2D(_PreviewBackground, sampler_PreviewBackground, input.uv);
            half4 checkerCol = half4(checker(input.uv), 1);
            background = lerp(checkerCol, background, background.a * _PreviewEnableBackground);
            float2 mainUV = input.uv * _PreviewFrameTilingOffset.xy + _PreviewFrameTilingOffset.zw;
            half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
            half3 src = GetBlendFactor(col, background, _PreviewSrcBlend);
            half3 dst = GetBlendFactor(col, background, _PreviewDstBlend);
            col.rgb = background.rgb * dst + col.rgb * src;
            return col;
        }
        ENDHLSL

        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_applyTex
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_alphaHandle
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_outline
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_fxaa
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag_preview
            ENDHLSL
        }
    }
}
