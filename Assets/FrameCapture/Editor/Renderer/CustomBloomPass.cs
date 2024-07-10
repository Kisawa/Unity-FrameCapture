using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomBloomPass : ScriptableRenderPass
{
    const int k_MaxPyramidSize = 16;

    Material mat;
    Material uberMaterial;
    readonly GraphicsFormat m_DefaultHDRFormat;
    bool m_UseRGBM;
    string sourceProperty;
    RenderTargetHandle _SourceTexture;
    float resolution;

    public CustomBloomPass(Material uberMaterial, string sourceProperty = "", float resolution = 1)
    {
        this.uberMaterial = uberMaterial;
        this.sourceProperty = sourceProperty;
        this.resolution = resolution;
        Shader shader = Shader.Find("Hidden/Universal Render Pipeline/Bloom");
        if (shader == null)
        {
            Debug.LogError("CustomBloomPass: shader not found.");
            return;
        }
        mat = CoreUtils.CreateEngineMaterial(shader);

        if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render))
        {
            m_DefaultHDRFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            m_UseRGBM = false;
        }
        else
        {
            m_DefaultHDRFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;
            m_UseRGBM = true;
        }
        if (!string.IsNullOrWhiteSpace(sourceProperty))
            _SourceTexture.Init(sourceProperty);

        ShaderConstants._BloomMipUp = new int[k_MaxPyramidSize];
        ShaderConstants._BloomMipDown = new int[k_MaxPyramidSize];
        for (int i = 0; i < k_MaxPyramidSize; i++)
        {
            ShaderConstants._BloomMipUp[i] = Shader.PropertyToID("_CustomBloomMipUp" + i);
            ShaderConstants._BloomMipDown[i] = Shader.PropertyToID("_CustomBloomMipDown" + i);
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (mat == null)
            return;
        Bloom bloom = VolumeManager.instance.stack.GetComponent<Bloom>();
        if (!bloom.IsActive())
        {
            ClearUberMaterial(uberMaterial);
            return;
        }
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, new ProfilingSampler("Custom Bloom")))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            RenderTargetIdentifier target = string.IsNullOrWhiteSpace(sourceProperty) ? renderingData.cameraData.renderer.cameraDepthTarget : _SourceTexture.Identifier();
            SetupBloom(bloom, cmd, target, descriptor);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    void SetupBloom(Bloom bloom, CommandBuffer cmd, RenderTargetIdentifier source, RenderTextureDescriptor descriptor)
    {
        if (mat == null)
            return;
        // Start at half-res
        int tw = Mathf.RoundToInt(descriptor.width * resolution);
        int th = Mathf.RoundToInt(descriptor.height * resolution);

        // Determine the iteration count
        int maxSize = Mathf.Max(tw, th);
        int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
        iterations -= bloom.skipIterations.value;
        int mipCount = Mathf.Clamp(iterations, 1, k_MaxPyramidSize);

        // Pre-filtering parameters
        float clamp = bloom.clamp.value;
        float threshold = Mathf.GammaToLinearSpace(bloom.threshold.value);
        float thresholdKnee = threshold * 0.5f; // Hardcoded soft knee

        // Material setup
        float scatter = Mathf.Lerp(0.05f, 0.95f, bloom.scatter.value);
        mat.SetVector(ShaderConstants._Params, new Vector4(scatter, clamp, threshold, thresholdKnee));
        CoreUtils.SetKeyword(mat, ShaderKeywordStrings.BloomHQ, bloom.highQualityFiltering.value);
        CoreUtils.SetKeyword(mat, ShaderKeywordStrings.UseRGBM, m_UseRGBM);

        // Prefilter
        var desc = GetCompatibleDescriptor(descriptor, tw, th, m_DefaultHDRFormat);
        cmd.GetTemporaryRT(ShaderConstants._BloomMipDown[0], desc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(ShaderConstants._BloomMipUp[0], desc, FilterMode.Bilinear);
        cmd.SetGlobalTexture(ShaderConstants._SourceTex, source);
        Blit(cmd, source, ShaderConstants._BloomMipDown[0], mat, 0);

        // Downsample - gaussian pyramid
        int lastDown = ShaderConstants._BloomMipDown[0];
        for (int i = 1; i < mipCount; i++)
        {
            tw = Mathf.Max(1, tw >> 1);
            th = Mathf.Max(1, th >> 1);
            int mipDown = ShaderConstants._BloomMipDown[i];
            int mipUp = ShaderConstants._BloomMipUp[i];

            desc.width = tw;
            desc.height = th;

            cmd.GetTemporaryRT(mipDown, desc, FilterMode.Bilinear);
            cmd.GetTemporaryRT(mipUp, desc, FilterMode.Bilinear);

            // Classic two pass gaussian blur - use mipUp as a temporary target
            //   First pass does 2x downsampling + 9-tap gaussian
            //   Second pass does 9-tap gaussian using a 5-tap filter + bilinear filtering
            cmd.SetGlobalTexture(ShaderConstants._SourceTex, lastDown);
            Blit(cmd, lastDown, mipUp, mat, 1);
            cmd.SetGlobalTexture(ShaderConstants._SourceTex, mipUp);
            Blit(cmd, mipUp, mipDown, mat, 2);

            lastDown = mipDown;
        }

        // Upsample (bilinear by default, HQ filtering does bicubic instead
        for (int i = mipCount - 2; i >= 0; i--)
        {
            int lowMip = (i == mipCount - 2) ? ShaderConstants._BloomMipDown[i + 1] : ShaderConstants._BloomMipUp[i + 1];
            int highMip = ShaderConstants._BloomMipDown[i];
            int dst = ShaderConstants._BloomMipUp[i];

            cmd.SetGlobalTexture(ShaderConstants._SourceTex, highMip);
            cmd.SetGlobalTexture(ShaderConstants._SourceTexLowMip, lowMip);
            Blit(cmd, highMip, BlitDstDiscardContent(cmd, dst), mat, 3);
        }

        // Cleanup
        for (int i = 0; i < mipCount; i++)
        {
            cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipDown[i]);
            if (i > 0) cmd.ReleaseTemporaryRT(ShaderConstants._BloomMipUp[i]);
        }
        cmd.SetGlobalTexture(ShaderConstants._Bloom_Texture, ShaderConstants._BloomMipUp[0]);

        if (uberMaterial != null)
        {
            // Setup bloom on uber
            var tint = bloom.tint.value.linear;
            var luma = ColorUtils.Luminance(tint);
            tint = luma > 0f ? tint * (1f / luma) : Color.white;

            var bloomParams = new Vector4(bloom.intensity.value, tint.r, tint.g, tint.b);
            uberMaterial.SetVector(ShaderConstants._Bloom_Params, bloomParams);
            uberMaterial.SetFloat(ShaderConstants._Bloom_RGBM, m_UseRGBM ? 1f : 0f);

            // Setup lens dirtiness on uber
            // Keep the aspect ratio correct & center the dirt texture, we don't want it to be
            // stretched or squashed
            var dirtTexture = bloom.dirtTexture.value == null ? Texture2D.blackTexture : bloom.dirtTexture.value;
            float dirtRatio = dirtTexture.width / (float)dirtTexture.height;
            float screenRatio = descriptor.width / (float)descriptor.height;
            var dirtScaleOffset = new Vector4(1f, 1f, 0f, 0f);
            float dirtIntensity = bloom.dirtIntensity.value;

            if (dirtRatio > screenRatio)
            {
                dirtScaleOffset.x = screenRatio / dirtRatio;
                dirtScaleOffset.z = (1f - dirtScaleOffset.x) * 0.5f;
            }
            else if (screenRatio > dirtRatio)
            {
                dirtScaleOffset.y = dirtRatio / screenRatio;
                dirtScaleOffset.w = (1f - dirtScaleOffset.y) * 0.5f;
            }

            uberMaterial.SetVector(ShaderConstants._LensDirt_Params, dirtScaleOffset);
            uberMaterial.SetFloat(ShaderConstants._LensDirt_Intensity, dirtIntensity);
            uberMaterial.SetTexture(ShaderConstants._LensDirt_Texture, dirtTexture);

            // Keyword setup - a bit convoluted as we're trying to save some variants in Uber...
            if (bloom.highQualityFiltering.value)
                uberMaterial.EnableKeyword(dirtIntensity > 0f ? ShaderKeywordStrings.BloomHQDirt : ShaderKeywordStrings.BloomHQ);
            else
                uberMaterial.EnableKeyword(dirtIntensity > 0f ? ShaderKeywordStrings.BloomLQDirt : ShaderKeywordStrings.BloomLQ);
        }
    }

    RenderTextureDescriptor GetCompatibleDescriptor(RenderTextureDescriptor descriptor, int width, int height, GraphicsFormat format, int depthBufferBits = 0)
    {
        var desc = descriptor;
        desc.depthBufferBits = depthBufferBits;
        desc.msaaSamples = 1;
        desc.width = width;
        desc.height = height;
        desc.graphicsFormat = format;
        return desc;
    }

    private BuiltinRenderTextureType BlitDstDiscardContent(CommandBuffer cmd, RenderTargetIdentifier rt)
    {
        // We set depth to DontCare because rt might be the source of PostProcessing used as a temporary target
        // Source typically comes with a depth buffer and right now we don't have a way to only bind the color attachment of a RenderTargetIdentifier
        cmd.SetRenderTarget(new RenderTargetIdentifier(rt, 0, CubemapFace.Unknown, -1),
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
        return BuiltinRenderTextureType.CurrentActive;
    }

    public static void ClearUberMaterial(Material uberMaterial)
    {
        if (uberMaterial == null)
            return;
        uberMaterial.DisableKeyword(ShaderKeywordStrings.BloomLQ);
        uberMaterial.DisableKeyword(ShaderKeywordStrings.BloomHQ);
        uberMaterial.DisableKeyword(ShaderKeywordStrings.BloomLQDirt);
        uberMaterial.DisableKeyword(ShaderKeywordStrings.BloomHQDirt);
    }

    static class ShaderConstants
    {
        public static readonly int _Bloom_Texture = Shader.PropertyToID("_CustomBloomTexture");

        public static readonly int _Params = Shader.PropertyToID("_Params");
        public static readonly int _SourceTex = Shader.PropertyToID("_SourceTex");
        public static readonly int _SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
        public static readonly int _Bloom_Params = Shader.PropertyToID("_Bloom_Params");
        public static readonly int _Bloom_RGBM = Shader.PropertyToID("_Bloom_RGBM");
        public static readonly int _LensDirt_Texture = Shader.PropertyToID("_LensDirt_Texture");
        public static readonly int _LensDirt_Params = Shader.PropertyToID("_LensDirt_Params");
        public static readonly int _LensDirt_Intensity = Shader.PropertyToID("_LensDirt_Intensity");

        public static int[] _BloomMipUp;
        public static int[] _BloomMipDown;
    }
}