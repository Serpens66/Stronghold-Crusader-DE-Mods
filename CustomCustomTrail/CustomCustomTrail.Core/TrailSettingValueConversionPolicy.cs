using System;

namespace CustomCustomTrail.Core;

public static class TrailSettingValueConversionPolicy
{
    public static bool ShouldUseMessagePackEnumConversion(object value, Type targetType)
    {
        if (value is null || targetType is null)
            return false;

        Type effectiveTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (!effectiveTarget.IsEnum)
            return false;

        Type sourceType = value.GetType();
        return sourceType == typeof(bool) ||
            sourceType == typeof(byte) ||
            sourceType == typeof(sbyte) ||
            sourceType == typeof(short) ||
            sourceType == typeof(ushort) ||
            sourceType == typeof(int) ||
            sourceType == typeof(uint) ||
            sourceType == typeof(long) ||
            sourceType == typeof(ulong);
    }
}
