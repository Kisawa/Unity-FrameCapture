using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace UnityEditor
{
    [CustomEditor(typeof(FrameCaptureFeature))]
    public class FrameCaptureFeatureEditor : Editor
    {
        FrameCaptureFeature self;
        SerializedProperty resolutionProp;
        SerializedProperty TransparentProp;
        SerializedProperty PreviewProp;
        SerializedProperty PreviewFrameTilingOffsetProp;
        SerializedProperty PreviewBackgroundProp;
        SerializedProperty PreviewSrcBlendProp;
        SerializedProperty PreviewDstBlendProp;

        SerializedProperty albedoTypeProp;
        SerializedProperty backgroundColorProp;
        SerializedProperty layerProp;
        SerializedProperty shaderTagsProp;

        SerializedProperty bloomStrengthProp;
        SerializedProperty outlineProp;
        SerializedProperty outlineWidthProp;
        SerializedProperty outlineColorProp;

        SerializedProperty customRendererFeatureProp;

        SerializedProperty applyFxaaProp;
        SerializedProperty savePathProp;

        private void OnEnable()
        {
            self = (FrameCaptureFeature)serializedObject.targetObject;
            PreviewProp = serializedObject.FindProperty("Preview");
            PreviewFrameTilingOffsetProp = serializedObject.FindProperty("PreviewFrameTilingOffset");
            PreviewBackgroundProp = serializedObject.FindProperty("PreviewBackground");
            PreviewSrcBlendProp = serializedObject.FindProperty("PreviewSrcBlend");
            PreviewDstBlendProp = serializedObject.FindProperty("PreviewDstBlend");

            resolutionProp = serializedObject.FindProperty("resolution");
            TransparentProp = serializedObject.FindProperty("Transparent");

            albedoTypeProp = serializedObject.FindProperty("albedoType");
            backgroundColorProp = serializedObject.FindProperty("backgroundColor");
            layerProp = serializedObject.FindProperty("layer");
            shaderTagsProp = serializedObject.FindProperty("shaderTags");

            bloomStrengthProp = serializedObject.FindProperty("bloomStrength");
            outlineProp = serializedObject.FindProperty("outline");
            outlineWidthProp = serializedObject.FindProperty("outlineWidth");
            outlineColorProp = serializedObject.FindProperty("outlineColor");

            customRendererFeatureProp = serializedObject.FindProperty("customRendererFeature");

            applyFxaaProp = serializedObject.FindProperty("applyFXAA");
            savePathProp = serializedObject.FindProperty("savePath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.BeginVertical("ObjectField");
            EditorGUILayout.PropertyField(PreviewProp);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(PreviewFrameTilingOffsetProp);
            EditorGUILayout.PropertyField(PreviewBackgroundProp);
            EditorGUILayout.PropertyField(PreviewSrcBlendProp);
            EditorGUILayout.PropertyField(PreviewDstBlendProp);
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);
            EditorGUILayout.PropertyField(resolutionProp);
            EditorGUILayout.PropertyField(albedoTypeProp);
            EditorGUILayout.PropertyField(TransparentProp);
            if (TransparentProp.boolValue)
            {
                EditorGUILayout.BeginVertical("Badge");
                EditorGUILayout.PropertyField(bloomStrengthProp);
                EditorGUILayout.PropertyField(outlineProp);
                EditorGUI.BeginDisabledGroup(!self.outline.Val);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(outlineWidthProp);
                EditorGUILayout.PropertyField(outlineColorProp);
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
            }

            if (TransparentProp.boolValue || albedoTypeProp.enumValueIndex == 1)
            {
                GUILayout.Space(10);
                EditorGUILayout.BeginVertical("dragtabdropwindow");
                GUILayout.Space(-3);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(-3);
                EditorGUILayout.LabelField("Drawer Setting", GUI.skin.GetStyle("AM HeaderStyle"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(backgroundColorProp);
                GUILayout.Space(2);
                EditorGUILayout.BeginVertical("EyeDropperHorizontalLine");
                GUILayout.Space(5);
                EditorGUILayout.PropertyField(layerProp);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.PropertyField(shaderTagsProp);
                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(10);
            EditorGUILayout.PropertyField(customRendererFeatureProp);

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical("EyeDropperHorizontalLine");
            EditorGUILayout.PropertyField(applyFxaaProp);
            EditorGUILayout.PropertyField(savePathProp);
            EditorGUI.BeginDisabledGroup(self.InCutFrame);
            if (GUILayout.Button("Cut"))
                self.CutFrame();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}