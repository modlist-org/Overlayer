using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

// Numeric JavaScript subset compiled to a native stack program. Compile returns
// 0 for anything outside the subset so callers fall back to V8.
internal static unsafe class OvJsExpr {
    private static bool available = true;
    internal static bool IsSupported => OvNative.IsAvailable && available;

    // allowT binds `t` as a parameter, matching graph code wrapped as `function(t)`.
    internal static nint Compile(string source, bool allowT = false) {
        if (string.IsNullOrEmpty(source) || !IsSupported) return 0;
        try {
            fixed (char* p = source) return ov_js_compile(p, (nuint)source.Length, allowT);
        } catch (EntryPointNotFoundException) {
            available = false;
        } catch (DllNotFoundException) {
            available = false;
        }
        return 0;
    }

    internal static int VarCount(nint program) => (int)ov_js_var_count(program);

    internal static double Eval(nint program, ReadOnlySpan<double> vars, double t = 0) {
        fixed (double* p = vars) return ov_js_eval(program, t, p, (nuint)vars.Length);
    }

    internal static void Free(nint program) {
        if (program != 0) ov_js_free(program);
    }

    [DllImport("overlayernative", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint ov_js_compile(char* source, nuint length, [MarshalAs(UnmanagedType.U1)] bool allowT);

    [DllImport("overlayernative", CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern uint ov_js_var_count(nint program);

    [DllImport("overlayernative", CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double ov_js_eval(nint program, double t, double* vars, nuint count);

    [DllImport("overlayernative", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ov_js_free(nint program);
}
