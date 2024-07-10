using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static UnityEditor.CustomTimeline.FrameCaptureTrack;

namespace UnityEditor.CustomTimeline
{
    public class FrameCaptureAsset : PlayableAsset
    {
        public string captureName;
        public int frameRate = 30;

        public TimelineClip clip { get; set; }
        public FrameCaptureRuntimeValue frameCaptureRuntimeValue { get; set; }
        public GameObject owner { get; private set; }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            this.owner = owner;
            ScriptPlayable<FrameCaptureBehaviour> playable = ScriptPlayable<FrameCaptureBehaviour>.Create(graph);
            playable.GetBehaviour().asset = this;
            return playable;
        }
    }
}