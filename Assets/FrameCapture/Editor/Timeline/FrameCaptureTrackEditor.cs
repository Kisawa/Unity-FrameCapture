using System.Collections;
using System.Collections.Generic;
using UnityEditor.Timeline;
using UnityEngine;

namespace UnityEditor.CustomTimeline
{
    [CustomEditor(typeof(FrameCaptureTrack))]
    public class FrameCaptureTrackEditor : Editor
    {
        SerializedProperty PreviewFrameTilingOffsetProp;
        SerializedProperty PreviewBackgroundProp;
        SerializedProperty PreviewSrcBlendProp;
        SerializedProperty PreviewDstBlendProp;
        SerializedProperty bloomStrengthProp;
        SerializedProperty outlineProp;
        SerializedProperty outlineColorProp;
        SerializedProperty outlineWidthProp;

        private void OnEnable()
        {
            SerializedProperty frameCaptureRuntimeValueProp = serializedObject.FindProperty("frameCaptureRuntimeValue");
            PreviewFrameTilingOffsetProp = frameCaptureRuntimeValueProp.FindPropertyRelative("PreviewFrameTilingOffset");
            PreviewBackgroundProp = frameCaptureRuntimeValueProp.FindPropertyRelative("PreviewBackground");
            PreviewSrcBlendProp = frameCaptureRuntimeValueProp.FindPropertyRelative("PreviewSrcBlend");
            PreviewDstBlendProp = frameCaptureRuntimeValueProp.FindPropertyRelative("PreviewDstBlend");
            bloomStrengthProp = frameCaptureRuntimeValueProp.FindPropertyRelative("bloomStrength");
            outlineProp = frameCaptureRuntimeValueProp.FindPropertyRelative("outline");
            outlineColorProp = frameCaptureRuntimeValueProp.FindPropertyRelative("outlineColor");
            outlineWidthProp = frameCaptureRuntimeValueProp.FindPropertyRelative("outlineWidth");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(PreviewFrameTilingOffsetProp);
            EditorGUILayout.PropertyField(PreviewBackgroundProp);
            EditorGUILayout.PropertyField(PreviewSrcBlendProp);
            EditorGUILayout.PropertyField(PreviewDstBlendProp);
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(bloomStrengthProp);
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(outlineProp);
            EditorGUILayout.PropertyField(outlineColorProp);
            EditorGUILayout.PropertyField(outlineWidthProp);
            if (EditorGUI.EndChangeCheck())
                TimelineEditor.Refresh(RefreshReason.SceneNeedsUpdate);
            serializedObject.ApplyModifiedProperties();
        }
    }
}