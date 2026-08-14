#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill required for C# 9 `init` accessors to compile under netstandard2.0, whose BCL
/// predates this marker type (it ships in net5.0+). Compile-time only; no runtime behavior.
/// </summary>
internal static class IsExternalInit { }
#endif
