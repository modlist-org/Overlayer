using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Overlayer.Native;

internal static unsafe class O5Expr {
    private const string LibName = "o5native";

    public static bool IsSupported => O5Native.IsAvailable;

    public static long Compile(string source) {
        if (string.IsNullOrEmpty(source) || !IsSupported) {
            return 0;
        }
        try {
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            fixed (byte* p = bytes) {
                return o5_expr_compile(p, (nuint)bytes.Length);
            }
        } catch {
            return 0;
        }
    }

    public static bool TryEval(long handle, out double value) => TryEval(handle, 0.0, out value);

    public static bool TryEval(long handle, double t, out double value) {
        value = 0;
        if (handle == 0 || !IsSupported) {
            return false;
        }
        try {
            value = o5_expr_eval_t(handle, t);
            return true;
        } catch {
            value = 0;
            return false;
        }
    }

    public static bool TryEval(long handle, double t, ReadOnlySpan<double> vars, out double value) {
        value = 0;
        if (handle == 0 || !IsSupported) {
            return false;
        }
        try {
            fixed (double* p = vars) {
                value = o5_expr_eval_vars(handle, t, p, (nuint)vars.Length);
            }
            return true;
        } catch {
            value = 0;
            return false;
        }
    }

    public static bool TryEval(long handle, double t, double* vars, nuint varCount, out double value) {
        value = 0;
        if (handle == 0 || !IsSupported) {
            return false;
        }
        try {
            value = o5_expr_eval_vars(handle, t, vars, varCount);
            return true;
        } catch {
            value = 0;
            return false;
        }
    }

    public static void Free(long handle) {
        if (handle == 0 || !IsSupported) {
            return;
        }
        try {
            o5_expr_free(handle);
        } catch {
        }
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern long o5_expr_compile(byte* src, nuint len);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double o5_expr_eval(long handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double o5_expr_eval_t(long handle, double t);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern double o5_expr_eval_vars(long handle, double t, double* vars, nuint varCount);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void o5_expr_free(long handle);
}
