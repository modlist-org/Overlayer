using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

public static unsafe class O5Fmt {
    private const string LibName = "o5native";

    public static bool IsSupported => O5Native.IsAvailable;

    public static int Format(double val, ReadOnlySpan<char> format, Span<char> destination) {
        if (destination.IsEmpty) {
            return 0;
        }

        if (IsSupported) {
            try {
                fixed (char* fPtr = format)
                fixed (char* dPtr = destination) {
                    return (int)o5_fmt_f64(val, fPtr, (nuint)format.Length, dPtr, (nuint)destination.Length);
                }
            } catch {
                // Fallback
            }
        }

        return FallbackFormat(val, format, destination);
    }

    public static string FormatString(double val, string format) {
        Span<char> buf = stackalloc char[1024];
        int written = Format(val, format.AsSpan(), buf);
        return new string(buf[..written]);
    }

    private static int FallbackFormat(double val, ReadOnlySpan<char> format, Span<char> destination) {
        string s = string.IsNullOrEmpty(format.ToString()) ? val.ToString() : val.ToString(format.ToString());
        int len = Math.Min(destination.Length, s.Length);
        s.AsSpan(0, len).CopyTo(destination);
        return len;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint o5_fmt_f64(double val, char* fmt, nuint fmtLen, char* dest, nuint maxDest);
}
