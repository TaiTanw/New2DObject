using System;

/// <summary>
/// 特殊类，用于控制反射相关，
/// 若代码不涉及反射相关的操作，请无视此类
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class FieldOrderAttribute : Attribute
{
    public int Order { get; private set; }

    public FieldOrderAttribute(int order)
    {
        Order = order;
    }
}

