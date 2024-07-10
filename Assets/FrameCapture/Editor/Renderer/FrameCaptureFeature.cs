using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor
{
    public class FrameCaptureFeature : ScriptableRendererFeature
    {
        public static FrameCaptureFeature Self;

        const string _CustomCameraTargetName = "_CustomCameraTarget";
        const string _FrameCaptureTextureName = "_FrameCaptureTexture";
        const string _TRANSPARENT = "_TRANSPARENT";
        const string _GAME_ALBEDO = "_GAME_ALBEDO";

        public RuntimeVal<bool> Preview;
        [SplitVector4("Tiling", "Offset")]
        public RuntimeVal<Vector4> PreviewFrameTilingOffset = new RuntimeVal<Vector4>(new Vector4(1, 1, 0, 0));
        public RuntimeVal<Texture> PreviewBackground = new RuntimeVal<Texture>(null);
        public RuntimeVal<BlendMode> PreviewSrcBlend = new RuntimeVal<BlendMode>(BlendMode.SrcAlpha);
        public RuntimeVal<BlendMode> PreviewDstBlend = new RuntimeVal<BlendMode>(BlendMode.OneMinusSrcAlpha);

        [Range(.1f, 1)]
        public float resolution = 1;
        public bool Transparent = false;

        public AlbedoType albedoType = AlbedoType.GameView;
        [ColorUsage(false)]
        public Color backgroundColor = Color.clear;
        public LayerMask layer = ~0;
        public List<string> shaderTags = new List<string>() { "UniversalForward", "UniversalForwardOnly", "LightweightForward", "SRPDefaultUnlit" };

        [Range(1, 5)]
        public RuntimeVal<float> bloomStrength = new RuntimeVal<float>(1);
        public RuntimeVal<bool> outline = new RuntimeVal<bool>(false);
        [Range(0, 15)]
        public RuntimeVal<float> outlineWidth = new RuntimeVal<float>(5);
        public RuntimeVal<Color> outlineColor = new RuntimeVal<Color>(Color.white);

        public List<ScriptableRendererFeature> customRendererFeature = new List<ScriptableRendererFeature>();

        public bool applyFXAA = true;
        public RuntimeVal<string> savePath = new RuntimeVal<string>("Frame Capture");

        class CustomDrawPass : ScriptableRenderPass
        {
            FrameCaptureFeature feature;
            List<ShaderTagId> shaderTags;
            RenderTargetHandle _CustomCameraTarget;

            public CustomDrawPass(FrameCaptureFeature feature)
            {
                this.feature = feature;
                shaderTags = new List<ShaderTagId>();
                for (int i = 0; i < feature.shaderTags.Count; i++)
                {
                    string tag = feature.shaderTags[i];
                    if (string.IsNullOrWhiteSpace(tag) || shaderTags.Any(x => x.name == tag))
                        continue;
                    shaderTags.Add(new ShaderTagId(tag));
                }
                _CustomCameraTarget.Init(_CustomCameraTargetName);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.width = Mathf.RoundToInt(descriptor.width * feature.resolution);
                descriptor.height = Mathf.RoundToInt(descriptor.height * feature.resolution);
                descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
                cmd.GetTemporaryRT(_CustomCameraTarget.id, descriptor);
                ConfigureTarget(_CustomCameraTarget.Identifier());
                Color backgroundColor = feature.backgroundColor;
                backgroundColor.a = feature.Transparent ? 0 : 1;
                ConfigureClear(ClearFlag.All, backgroundColor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("FrameCapture: Custom draw pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    DrawingSettings drawingSettings = CreateDrawingSettings(shaderTags, ref renderingData, SortingCriteria.CommonOpaque);
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, feature.layer);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

                    drawingSettings = CreateDrawingSettings(shaderTags, ref renderingData, SortingCriteria.CommonTransparent);
                    filteringSettings = new FilteringSettings(RenderQueueRange.transparent, feature.layer);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

                    for (int i = 0; i < feature.customRendererFeature.Count; i++)
                    {
                        IFrameCaptureFeatureHandle handle = feature.customRendererFeature[i] as IFrameCaptureFeatureHandle;
                        if (handle != null)
                            handle.FrameCaptureAfterDrawHandle(cmd, _CustomCameraTarget);
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(_CustomCameraTarget.id);
            }
        }

        class ApplyTexturePass : ScriptableRenderPass
        {
            static readonly int _BloomStrength = Shader.PropertyToID("_BloomStrength");
            static readonly int _OutlineWidth = Shader.PropertyToID("_OutlineWidth");
            static readonly int _OutlineColor = Shader.PropertyToID("_OutlineColor");
            static readonly string _TONEMAP_ACES = "_TONEMAP_ACES";
            static readonly string _TONEMAP_NEUTRAL = "_TONEMAP_NEUTRAL";

            Material mat;
            FrameCaptureFeature feature;
            RenderTargetHandle _TempTarget;
            RenderTargetHandle _FrameCaptureTexture;

            public ApplyTexturePass(Material mat, FrameCaptureFeature feature)
            {
                this.mat = mat;
                this.feature = feature;
                _TempTarget.Init("_CameraTransparentTemp");
                _FrameCaptureTexture.Init(_FrameCaptureTextureName);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.colorFormat = RenderTextureFormat.ARGB32;
                descriptor.width = Mathf.RoundToInt(descriptor.width * feature.resolution);
                descriptor.height = Mathf.RoundToInt(descriptor.height * feature.resolution);
                descriptor.msaaSamples = 1;
                cmd.GetTemporaryRT(_TempTarget.id, descriptor);
                cmd.GetTemporaryRT(_FrameCaptureTexture.id, descriptor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (mat == null)
                    return;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("FrameCapture: Apply texture pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    RefreshMat();
                    RenderTargetIdentifier cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
                    Blit(cmd, cameraColorTarget, _TempTarget.Identifier(), mat, 0);

                    for (int i = 0; i < feature.customRendererFeature.Count; i++)
                    {
                        IFrameCaptureFeatureHandle handle = feature.customRendererFeature[i] as IFrameCaptureFeatureHandle;
                        if (handle != null)
                            handle.FrameCaptureBeforePostHandle(cmd, _TempTarget);
                    }

                    Blit(cmd, _TempTarget.Identifier(), _FrameCaptureTexture.Identifier(), mat, 1);
                    if (feature.outline.Val)
                        Blit(cmd, _FrameCaptureTexture.Identifier(), _TempTarget.Identifier(), mat, 2);
                    else
                        Blit(cmd, _FrameCaptureTexture.Identifier(), _TempTarget.Identifier());

                    for (int i = 0; i < feature.customRendererFeature.Count; i++)
                    {
                        IFrameCaptureFeatureHandle handle = feature.customRendererFeature[i] as IFrameCaptureFeatureHandle;
                        if (handle != null)
                            handle.FrameCaptureAfterPostHandle(cmd, _TempTarget);
                    }

                    if (feature.applyFXAA)
                        Blit(cmd, _TempTarget.Identifier(), _FrameCaptureTexture.Identifier(), mat, 3);
                    else
                        Blit(cmd, _TempTarget.Identifier(), _FrameCaptureTexture.Identifier());
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            void RefreshMat()
            {
                if (feature.Transparent)
                    mat.EnableKeyword(_TRANSPARENT);
                else
                    mat.DisableKeyword(_TRANSPARENT);
                Tonemapping tonemapping = VolumeManager.instance.stack.GetComponent<Tonemapping>();
                if (tonemapping.IsActive())
                {
                    switch (tonemapping.mode.value)
                    {
                        case TonemappingMode.Neutral:
                            mat.DisableKeyword(_TONEMAP_ACES);
                            mat.EnableKeyword(_TONEMAP_NEUTRAL);
                            break;
                        case TonemappingMode.ACES:
                            mat.EnableKeyword(_TONEMAP_ACES);
                            mat.DisableKeyword(_TONEMAP_NEUTRAL);
                            break;
                        default:
                            mat.DisableKeyword(_TONEMAP_ACES);
                            mat.DisableKeyword(_TONEMAP_NEUTRAL);
                            break;
                    }
                }
                else
                {
                    mat.DisableKeyword(_TONEMAP_ACES);
                    mat.DisableKeyword(_TONEMAP_NEUTRAL);
                }
                mat.SetFloat(_BloomStrength, Mathf.GammaToLinearSpace(feature.bloomStrength.Val));
                mat.SetFloat(_OutlineWidth, feature.outlineWidth.Val);
                mat.SetColor(_OutlineColor, feature.outlineColor.Val);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(_TempTarget.id);
                cmd.ReleaseTemporaryRT(_FrameCaptureTexture.id);
            }
        }

        class CutFramePass : ScriptableRenderPass
        {
            FrameCaptureFeature feature;
            RenderTargetHandle _FrameCaptureTexture;

            public CutFramePass(FrameCaptureFeature feature)
            {
                this.feature = feature;
                _FrameCaptureTexture.Init(_FrameCaptureTextureName);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                int width = Mathf.RoundToInt(descriptor.width * feature.resolution);
                int height = Mathf.RoundToInt(descriptor.height * feature.resolution);
                feature.frameTex = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("FrameCapture: Cut frame pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    RenderTargetIdentifier cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
                    Blit(cmd, _FrameCaptureTexture.Identifier(), feature.frameTex);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        class PreviewPass : ScriptableRenderPass
        {
            static readonly int _PreviewFrameTilingOffset = Shader.PropertyToID("_PreviewFrameTilingOffset");
            static readonly int _PreviewEnableBackground = Shader.PropertyToID("_PreviewEnableBackground");
            static readonly int _PreviewBackground = Shader.PropertyToID("_PreviewBackground");
            static readonly int _PreviewSrcBlend = Shader.PropertyToID("_PreviewSrcBlend");
            static readonly int _PreviewDstBlend = Shader.PropertyToID("_PreviewDstBlend");

            Material mat;
            FrameCaptureFeature feature;
            RenderTargetHandle _FrameCaptureTexture;

            public PreviewPass(Material mat, FrameCaptureFeature feature)
            {
                this.mat = mat;
                this.feature = feature;
                _FrameCaptureTexture.Init(_FrameCaptureTextureName);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (mat == null)
                    return;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("FrameCapture: Texture preview pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    RenderTargetIdentifier cameraColorTarget = renderingData.cameraData.renderer.cameraColorTarget;
                    mat.SetVector(_PreviewFrameTilingOffset, feature.PreviewFrameTilingOffset.Val);
                    mat.SetFloat(_PreviewEnableBackground, feature.PreviewBackground.Val == null ? 0 : 1);
                    mat.SetTexture(_PreviewBackground, feature.PreviewBackground.Val);
                    mat.SetInt(_PreviewSrcBlend, (int)feature.PreviewSrcBlend.Val);
                    mat.SetInt(_PreviewDstBlend, (int)feature.PreviewDstBlend.Val);
                    Blit(cmd, _FrameCaptureTexture.Identifier(), cameraColorTarget, mat, 4);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public bool InCutFrame { get; private set; }
        string inCutPath;
        CustomDrawPass customDrawPass;
        CustomBloomPass bloomPass;
        ApplyTexturePass applyTexturePass;
        CutFramePass cutFramePass;
        PreviewPass previewPass;
        RenderTexture frameTex;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (frameTex != null)
            {
                SaveFrameCapture();
                RenderTexture.ReleaseTemporary(frameTex);
                frameTex = null;
            }
            if (cutFramePass == null || renderingData.cameraData.isPreviewCamera || renderingData.cameraData.isSceneViewCamera)
            {
                InCutFrame = false;
                return;
            }
            if (Preview.Val || InCutFrame)
            {
                if (albedoType == AlbedoType.CustomDrawer || Transparent)
                {
                    renderer.EnqueuePass(customDrawPass);
                    renderer.EnqueuePass(bloomPass);
                }
                if (applyTexturePass == null)
                    return;
                for (int i = 0; i < customRendererFeature.Count; i++)
                {
                    IFrameCaptureFeatureHandle handle = customRendererFeature[i] as IFrameCaptureFeatureHandle;
                    if (handle != null)
                        handle.FrameCaptureWillPostHandle();
                }
                renderer.EnqueuePass(applyTexturePass);
            }
            if (InCutFrame)
                renderer.EnqueuePass(cutFramePass);
            if (Preview.Val)
            {
                renderingData.cameraData.antialiasing = AntialiasingMode.None;
                renderer.EnqueuePass(previewPass);
            }
        }

        public override void Create()
        {
            Self = this;
            name = "Frame Capture Feature";
            customDrawPass = null;
            bloomPass = null;
            applyTexturePass = null;
            cutFramePass = null;
            previewPass = null;

            Shader shader = Shader.Find("Hidden/FrameCaptureShader");
            if (shader == null)
            {
                Debug.LogError("Frame Capture: shader not found.");
                return;
            }
            Material mat = CoreUtils.CreateEngineMaterial(shader);
            switch (albedoType)
            {
                case AlbedoType.GameView:
                    mat.EnableKeyword(_GAME_ALBEDO);
                    break;
                default:
                    mat.DisableKeyword(_GAME_ALBEDO);
                    break;
            }

            customDrawPass = new CustomDrawPass(this);
            customDrawPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            bloomPass = new CustomBloomPass(mat, _CustomCameraTargetName, resolution);
            bloomPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            applyTexturePass = new ApplyTexturePass(mat, this);
            applyTexturePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            cutFramePass = new CutFramePass(this);
            cutFramePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            previewPass = new PreviewPass(mat, this);
            previewPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public void CutFrame()
        {
            InCutFrame = true;
            inCutPath = savePath.Val;
        }

        void SaveFrameCapture()
        {
            InCutFrame = false;
            if (frameTex == null)
                return;
            string path = savePath.Val;
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!path.EndsWith(".png"))
                {
                    if (!path.EndsWith("/"))
                        path += "/";
                    System.TimeSpan ts = System.DateTime.Now - new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
                    path += $"{System.Convert.ToInt64(ts.TotalSeconds)}.png";
                }
                if (Transparent)
                    SaveToARGB32(frameTex, path);
                else
                    SaveToRGB24(frameTex, path);
            }
        }

        public static void SaveToARGB32(RenderTexture renderTexture, string path)
        {
            SaveToPNG(renderTexture, path, TextureFormat.ARGB32);
        }

        public static void SaveToRGB24(RenderTexture renderTexture, string path)
        {
            SaveToPNG(renderTexture, path, TextureFormat.RGB24);
        }

        static void SaveToPNG(RenderTexture renderTexture, string path, TextureFormat format)
        {
            if (renderTexture == null || string.IsNullOrWhiteSpace(path))
                return;
            RenderTexture pre = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tex.Apply();
            RenderTexture.active = pre;
            byte[] bytes = ImageConversion.EncodeToPNG(tex);
            Object.DestroyImmediate(tex);
            string _path = $"{Application.dataPath}/../{path}";
            if (!_path.EndsWith(".png"))
                _path += ".png";
            string directoryPath = System.IO.Path.GetDirectoryName(_path);
            System.IO.Directory.CreateDirectory(directoryPath);
            System.IO.File.WriteAllBytes(_path, bytes);
            Debug.Log($"Save png: {_path}");
        }

        public enum AlbedoType { GameView, CustomDrawer }
    }
}