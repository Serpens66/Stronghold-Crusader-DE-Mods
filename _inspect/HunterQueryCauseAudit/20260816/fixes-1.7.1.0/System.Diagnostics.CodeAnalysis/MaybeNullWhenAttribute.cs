namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class MaybeNullWhenAttribute : Attribute
{
	public bool ReturnValue { get; }

	public MaybeNullWhenAttribute(bool returnValue)
	{
		ReturnValue = returnValue;
	}
}
