using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class InterpolatedStringHandlerAttribute : Attribute
{
}
