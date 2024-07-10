using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Playables;
using static UnityEditor.CustomTimeline.FrameCaptureTrack;

namespace UnityEditor.CustomTimeline
{
    public class FrameCaptureMixerBehaviour : PlayableBehaviour
    {
        static FrameCaptureFeature Feature => FrameCaptureFeature.Self;

        public FrameCaptureRuntimeValue frameCaptureRuntimeValue { get; set; }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            SetFrameCaptureRuntimeVal(true);
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            SetFrameCaptureRuntimeVal(true);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            SetFrameCaptureRuntimeVal(false);
        }

        void SetFrameCaptureRuntimeVal(bool active)
        {
            if (FrameCaptureFeature.Self == null)
                return;
            if (active)
            {
                if (frameCaptureRuntimeValue != null)
                {
                    Feature.PreviewFrameTilingOffset.SetActive(true);
                    Feature.PreviewFrameTilingOffset.Val = frameCaptureRuntimeValue.PreviewFrameTilingOffset;
                    Feature.PreviewBackground.SetActive(true);
                    Feature.PreviewBackground.Val = frameCaptureRuntimeValue.PreviewBackground;
                    Feature.PreviewSrcBlend.SetActive(true);
                    Feature.PreviewSrcBlend.Val = frameCaptureRuntimeValue.PreviewSrcBlend;
                    Feature.PreviewDstBlend.SetActive(true);
                    Feature.PreviewDstBlend.Val = frameCaptureRuntimeValue.PreviewDstBlend;
                    Feature.bloomStrength.SetActive(true);
                    Feature.bloomStrength.Val = frameCaptureRuntimeValue.bloomStrength;
                    Feature.outline.SetActive(true);
                    Feature.outline.Val = frameCaptureRuntimeValue.outline;
                    Feature.outlineColor.SetActive(true);
                    Feature.outlineColor.Val = frameCaptureRuntimeValue.outlineColor;
                    Feature.outlineWidth.SetActive(true);
                    Feature.outlineWidth.Val = frameCaptureRuntimeValue.outlineWidth;
                }
            }
            else
            {
                Feature.PreviewFrameTilingOffset.SetActive(false);
                Feature.PreviewBackground.SetActive(false);
                Feature.PreviewSrcBlend.SetActive(false);
                Feature.PreviewDstBlend.SetActive(false);
                Feature.bloomStrength.SetActive(false);
                Feature.outline.SetActive(false);
                Feature.outlineColor.SetActive(false);
                Feature.outlineWidth.SetActive(false);
            }
        }
    }
}