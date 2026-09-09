// Compile-time shim so C# 10 record types (whose synthesized init accessors reference this
// type) build on netstandard2.0 / net48. Never used at runtime: consumers construct via the
// positional constructor.
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
