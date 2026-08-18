namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class NotNullWhenAttribute : Attribute
{
	public bool ReturnValue { get; }

	public NotNullWhenAttribute(bool returnValue)
	{
		ReturnValue = returnValue;
	}
}
