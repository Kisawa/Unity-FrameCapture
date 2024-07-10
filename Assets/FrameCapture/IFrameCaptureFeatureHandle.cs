using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public interface IFrameCaptureFeatureHandle
{
    void FrameCaptureAfterDrawHandle(CommandBuffer cmd, RenderTargetHandle customCameraTarget);

    void FrameCaptureBeforePostHandle(CommandBuffer cmd, RenderTargetHandle frameCaptureTexture);

    void FrameCaptureAfterPostHandle(CommandBuffer cmd, RenderTargetHandle frameCaptureTexture);

    void FrameCaptureWillPostHandle();
}