using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering;

namespace UnityEditor.CustomTimeline
{
    [TrackColor(0, 1, 0)]
    [TrackClipType(typeof(FrameCaptureAsset))]
    public class FrameCaptureTrack : TrackAsset
    {
        public FrameCaptureRuntimeValue frameCaptureRuntimeValue = new FrameCaptureRuntimeValue();

        protected override Playable CreatePlayable(PlayableGraph graph, GameObject gameObject, TimelineClip clip)
        {
            FrameCaptureAsset clipAsset = (FrameCaptureAsset)clip.asset;
            clipAsset.clip = clip;
            clipAsset.frameCaptureRuntimeValue = frameCaptureRuntimeValue;
            return base.CreatePlayable(graph, gameObject, clip);
        }

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var playable = ScriptPlayable<FrameCaptureMixerBehaviour>.Create(graph, inputCount);
            FrameCaptureMixerBehaviour behaviour = playable.GetBehaviour();
            behaviour.frameCaptureRuntimeValue = frameCaptureRuntimeValue;
            return playable;
        }

        [System.Serializable]
        public class FrameCaptureRuntimeValue
        {
            [SplitVector4("Tiling", "Offset")]
            public Vector4 PreviewFrameTilingOffset = new Vector4(1, 1, 0, 0);
            public Texture PreviewBackground;
            public BlendMode PreviewSrcBlend = BlendMode.SrcAlpha;
            public BlendMode PreviewDstBlend = BlendMode.OneMinusSrcAlpha;
            [Header("Transparent"), Range(1, 5)]
            public float bloomStrength = 1;
            [Header("Outline")]
            public bool outline = true;
            public Color outlineColor = Color.white;
            [Range(0, 15)]
            public float outlineWidth = 5;
        }
    }
}