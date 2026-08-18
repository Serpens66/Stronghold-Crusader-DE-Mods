namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class DoesNotReturnAttribute : Attribute
{
}
