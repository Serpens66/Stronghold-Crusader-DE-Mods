using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class RequiredMemberAttribute : Attribute
{
}
