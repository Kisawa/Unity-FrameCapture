using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace UnityEditor.CustomTimeline
{
    public class FrameCaptureBehaviour : PlayableBehaviour
    {
        static FrameCaptureFeature Feature => FrameCaptureFeature.Self;

        public FrameCaptureAsset asset { get; set; }

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
            if (Feature == null)
                return;
            if (active)
            {
                Feature.Preview.SetActive(true);
                Feature.Preview.Val = true;
            }
            else
                FrameCaptureFeature.Self.Preview.SetActive(false);
        }
    }
}