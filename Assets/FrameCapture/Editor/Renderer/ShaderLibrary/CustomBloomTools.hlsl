#ifndef CUSTOM_BLOOM_TOOLS_INCLUDED
#define CUSTOM_BLOOM_TOOLS_INCLUDED

#include "SamplerTools.hlsl"

// Hardcoded dependencies to reduce the number of variants
#if _BLOOM_LQ || _BLOOM_HQ || _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
#define BLOOM
#if _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
#define BLOOM_DIRT
#endif
#endif

struct BloomParam
{
    half intensity;
    half3 tint;
    float RGBM;
    float2 lensDirtScale;
    float2 lensDirtOffset;
    half lensDirtIntensity;
};

TEXTURE2D(_LensDirt_Texture);
TEXTURE2D_X(_CustomBloomTexture);

half3 SampleCustomBloom(BloomParam param, float2 uv, out half3 dirt)
{
    dirt = 0;
    half4 bloom = 0;
#if defined(BLOOM)
    bloom = SAMPLE_TEXTURE2D_X(_CustomBloomTexture, sampler_LinearClamp, uv);
#if UNITY_COLORSPACE_GAMMA
    bloom.xyz *= bloom.xyz; // ¦Ã to linear
#endif
    UNITY_BRANCH
    if (param.RGBM > 0)
    {
        bloom.xyz = DecodeRGBM(bloom);
    }
    bloom.xyz *= param.intensity;
#if defined(BLOOM_DIRT)
    // UVs for the dirt texture should be DistortUV(uv * DirtScale + DirtOffset) but
    // considering we use a cover-style scale on the dirt texture the difference
    // isn't massive so we chose to save a few ALUs here instead in case lens
    // distortion is active.
    dirt = SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_LinearClamp, uv * param.lensDirtScale + param.lensDirtOffset).xyz;
    dirt *= param.lensDirtIntensity * bloom.xyz;
#endif
    bloom.xyz *= param.tint;
#endif
    return bloom.xyz;
}

#endif