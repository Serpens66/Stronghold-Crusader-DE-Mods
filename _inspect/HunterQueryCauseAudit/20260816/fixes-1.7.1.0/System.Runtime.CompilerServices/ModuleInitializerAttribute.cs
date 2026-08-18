using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class ModuleInitializerAttribute : Attribute
{
}
