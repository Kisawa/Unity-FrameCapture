using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;

namespace UnityEditor.CustomTimeline
{
    [CustomEditor(typeof(FrameCaptureAsset))]
    public class FrameCaptureAssetEditor : Editor
    {
        FrameCaptureAsset self;
        SerializedProperty captureNameProp;
        SerializedProperty frameRateProp;

        int currentFrame = -1;
        int endFrame => Mathf.CeilToInt((float)(self.clip.duration * frameRateProp.intValue));
        bool pause;
        bool inCapture => currentFrame > -1 && currentFrame <= endFrame;

        private void OnEnable()
        {
            self = serializedObject.targetObject as FrameCaptureAsset;
            captureNameProp = serializedObject.FindProperty("captureName");
            frameRateProp = serializedObject.FindProperty("frameRate");
        }

        private void OnDisable()
        {
            if (FrameCaptureFeature.Self != null)
                FrameCaptureFeature.Self.savePath.SetActive(false);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (self.clip == null)
                return;

            if (currentFrame == endFrame + 1)
                currentFrame = -1;
            if (inCapture && !pause)
            {
                if (FrameCaptureFeature.Self == null)
                {
                    TimelineEditor.inspectedDirector.time = self.clip.start + 1.0 / frameRateProp.intValue * currentFrame;
                    TimelineEditor.inspectedDirector.Evaluate();
                    currentFrame++;
                }
                else if (!FrameCaptureFeature.Self.InCutFrame)
                {
                    TimelineEditor.inspectedDirector.time = self.clip.start + 1.0 / frameRateProp.intValue * currentFrame;
                    FrameCaptureFeature.Self.savePath.SetActive(true);
                    FrameCaptureFeature.Self.savePath.Val = CheckCapturePath();
                    FrameCaptureFeature.Self.CutFrame();
                    TimelineEditor.inspectedDirector.Evaluate();
                    currentFrame++;
                }
                Repaint();
                TimelineEditor.Refresh(RefreshReason.SceneNeedsUpdate);
            }

            EditorGUI.BeginDisabledGroup(inCapture);
            EditorGUILayout.PropertyField(captureNameProp);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(frameRateProp);
            if (EditorGUI.EndChangeCheck())
                frameRateProp.intValue = Mathf.Max(1, frameRateProp.intValue);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(captureNameProp.stringValue));
            if (inCapture)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(pause ? "Goon" : "Pause"))
                    pause = !pause;
                if (GUILayout.Button("Stop", GUILayout.Width(100)))
                {
                    currentFrame = -1;
                    pause = false;
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (GUILayout.Button("Start"))
                currentFrame = 0;
            EditorGUI.EndDisabledGroup();

            serializedObject.ApplyModifiedProperties();
        }

        string CheckCapturePath()
        {
            string folder = $"Frame Capture/{self.owner.name}";
            string path = $"{folder}/{self.captureName}_{currentFrame}.png";
            return path;
        }
    }
}