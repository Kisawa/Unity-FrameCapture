using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RuntimeVal<T>
{
    bool active;
    public bool Active => active;

    public T OriginVal;

    T _val;
    public T Val
    {
        get => active ? _val : OriginVal;
        set => _val = value;
    }

    public RuntimeVal(T val)
    {
        active = false;
        OriginVal = val;
        _val = val;
    }

    public void SetActive(bool res)
    {
        active = res;
    }

    public void Reset()
    {
        _val = OriginVal;
    }
}

public class RangeAttribute : PropertyAttribute
{
    public float min;
    public float max;

    public RangeAttribute(float min, float max)
    {
        this.min = min;
        this.max = max;
    }
}

public class SplitVector4Attribute : PropertyAttribute
{
    public string name0;
    public string name1;

    public SplitVector4Attribute(string name0, string name1)
    {
        this.name0 = name0;
        this.name1 = name1;
    }
}