using System;
using System.Reflection;

namespace StartupPerformanceDiagnostic
{
    internal sealed class StaticFieldReader<TValue>
    {
        private readonly FieldInfo field;

        private StaticFieldReader(FieldInfo field)
        {
            this.field = field;
        }

        internal static bool TryCreate(
            Type declaringType,
            string fieldName,
            out StaticFieldReader<TValue> reader,
            out string error)
        {
            reader = null;
            error = string.Empty;
            if (declaringType == null)
            {
                error = "Declaring type is null.";
                return false;
            }
            if (string.IsNullOrEmpty(fieldName))
            {
                error = "Field name is empty.";
                return false;
            }

            FieldInfo resolved = declaringType.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (resolved == null)
            {
                error = "Static field '" + declaringType.FullName + "." + fieldName + "' was not found.";
                return false;
            }
            if (!resolved.IsStatic)
            {
                error = "Field '" + declaringType.FullName + "." + fieldName + "' is not static.";
                return false;
            }
            if (resolved.FieldType != typeof(TValue))
            {
                error = "Field '" + declaringType.FullName + "." + fieldName + "' has type '" +
                        resolved.FieldType.FullName + "' instead of '" + typeof(TValue).FullName + "'.";
                return false;
            }

            reader = new StaticFieldReader<TValue>(resolved);
            return true;
        }

        internal TValue Read()
        {
            return (TValue)field.GetValue(null);
        }
    }
}
