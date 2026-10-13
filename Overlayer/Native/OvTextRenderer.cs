using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct OvTextSlot {
    public uint Index;
    public uint Length;
    public uint Offset;
    public uint TextLength;
}

internal static unsafe class OvTextRenderer {
    private static bool available = true;
    internal static bool IsSupported => OvNative.IsAvailable && available;

    internal static bool TryRender(string template, ReadOnlySpan<OvTextSlot> slots,
        ReadOnlySpan<char> replacements, Span<char> destination, out int written) {
        written = 0;
        if (!IsSupported) return false;
        if (destination.IsEmpty) return true;
        try {
            fixed (char* t = template)
            fixed (OvTextSlot* s = slots)
            fixed (char* r = replacements)
            fixed (char* d = destination) {
                bool bulk = template.Length >= 65536 || destination.Length >= 65536 || slots.Length > 128;
                written = (int)(bulk
                    ? ov_render_text_bulk(t, (nuint)template.Length, s, (nuint)slots.Length,
                        r, (nuint)replacements.Length, d, (nuint)destination.Length)
                    : ov_render_text(t, (nuint)template.Length, s, (nuint)slots.Length,
                        r, (nuint)replacements.Length, d, (nuint)destination.Length));
                return true;
            }
        } catch (EntryPointNotFoundException) {
            available = false;
        } catch (DllNotFoundException) {
            available = false;
        }
        return false;
    }

    [DllImport("overlayernative", CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint ov_render_text(char* template, nuint length, OvTextSlot* slots,
        nuint count, char* replacements, nuint replacementLength, char* output, nuint capacity);

    // Large copies must not keep the GC waiting in a suppressed transition.
    [DllImport("overlayernative", EntryPoint = "ov_render_text", CallingConvention = CallingConvention.Cdecl)]
    private static extern nuint ov_render_text_bulk(char* template, nuint length, OvTextSlot* slots,
        nuint count, char* replacements, nuint replacementLength, char* output, nuint capacity);
}
