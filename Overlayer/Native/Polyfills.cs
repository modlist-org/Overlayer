#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices {
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class SuppressGCTransitionAttribute : Attribute { }
}
#endif
