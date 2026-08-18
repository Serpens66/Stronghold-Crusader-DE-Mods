namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class UnscopedRefAttribute : Attribute
{
}
