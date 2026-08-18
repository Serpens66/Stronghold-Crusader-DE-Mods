namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class MaybeNullAttribute : Attribute
{
}
