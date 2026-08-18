using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
	public string ParameterName { get; }

	public CallerArgumentExpressionAttribute(string parameterName)
	{
		ParameterName = parameterName;
	}
}
