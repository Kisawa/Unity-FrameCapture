using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using UnityEngine.Rendering;

[CustomPropertyDrawer(typeof(RuntimeVal<bool>))]
[CustomPropertyDrawer(typeof(RuntimeVal<int>))]
[CustomPropertyDrawer(typeof(RuntimeVal<float>))]
[CustomPropertyDrawer(typeof(RuntimeVal<Color>))]
[CustomPropertyDrawer(typeof(RuntimeVal<Vector2>))]
[CustomPropertyDrawer(typeof(RuntimeVal<Vector3>))]
[CustomPropertyDrawer(typeof(RuntimeVal<Vector4>))]
[CustomPropertyDrawer(typeof(RuntimeVal<string>))]
[CustomPropertyDrawer(typeof(RuntimeVal<Texture>))]
[CustomPropertyDrawer(typeof(RuntimeVal<BlendMode>))]
public class RuntimeValDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect activeRect = new Rect(position.x, position.y, 30, position.height);
        bool active = RuntimeValUtil.GetRuntimeValActive(property, fieldInfo);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUI.Toggle(activeRect, active);
        EditorGUI.EndDisabledGroup();
        position.x += 30;
        position.width -= 30;
        SerializedProperty prop = property.FindPropertyRelative("OriginVal");
        EditorGUI.PropertyField(position, prop, label);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty prop = property.FindPropertyRelative("OriginVal");
        return base.GetPropertyHeight(prop, label);
    }
}

[CustomPropertyDrawer(typeof(RangeAttribute))]
public class RangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.type == "RuntimeVal`1")
        {
            Rect activeRect = new Rect(position.x, position.y, 30, position.height);
            bool active = RuntimeValUtil.GetRuntimeValActive(property, fieldInfo);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.Toggle(activeRect, active);
            EditorGUI.EndDisabledGroup();
            position.x += 30;
            property = property.FindPropertyRelative("OriginVal");
        }
        RangeAttribute range = attribute as RangeAttribute;
        if (property.propertyType == SerializedPropertyType.Float)
            EditorGUI.Slider(position, property, range.min, range.max, label);
        else if (property.propertyType == SerializedPropertyType.Integer)
            EditorGUI.IntSlider(position, property, Convert.ToInt32(range.min), Convert.ToInt32(range.max), label);
        else
            EditorGUI.LabelField(position, label.text, "Use Range with float or int.");
    }
}

[CustomPropertyDrawer(typeof(SplitVector4Attribute))]
public class SplitVector4Drawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.type == "RuntimeVal`1")
        {
            Rect activeRect = new Rect(position.x, position.y - EditorGUIUtility.singleLineHeight, 30, position.height);
            bool active = RuntimeValUtil.GetRuntimeValActive(property, fieldInfo);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.Toggle(activeRect, active);
            EditorGUI.EndDisabledGroup();
            position.x += 30;
            property = property.FindPropertyRelative("OriginVal");
        }
        SplitVector4Attribute split = attribute as SplitVector4Attribute;
        if (property.propertyType == SerializedPropertyType.Vector4)
        {
            position.y -= EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(position, label);
            EditorGUI.BeginChangeCheck();
            position.x += 13;
            position.y += EditorGUIUtility.singleLineHeight * 2;
            Vector2 val0 = EditorGUI.Vector2Field(position, split.name0, new Vector2(property.vector4Value.x, property.vector4Value.y));
            position.y += EditorGUIUtility.singleLineHeight;
            Vector2 val1 = EditorGUI.Vector2Field(position, split.name1, new Vector2(property.vector4Value.z, property.vector4Value.w));
            if (EditorGUI.EndChangeCheck())
                property.vector4Value = new Vector4(val0.x, val0.y, val1.x, val1.y);
        }
        else
            EditorGUI.LabelField(position, label.text, "Use Range with float or int.");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.type == "RuntimeVal`1")
            property = property.FindPropertyRelative("OriginVal");
        if (property.propertyType == SerializedPropertyType.Vector4)
            return base.GetPropertyHeight(property, label) * 3;
        return base.GetPropertyHeight(property, label);
    }
}

public static class RuntimeValUtil
{
    public static bool GetRuntimeValActive(SerializedProperty property, FieldInfo fieldInfo)
    {
        if (property.type != "RuntimeVal`1")
            return false;
        Type type = fieldInfo.FieldType;
        FieldInfo activeInfo = type.GetField("active", BindingFlags.NonPublic | BindingFlags.Instance);
        object instance = fieldInfo.GetValue(property.serializedObject.targetObject);
        bool active = (bool)activeInfo.GetValue(instance);
        return active;
    }
}