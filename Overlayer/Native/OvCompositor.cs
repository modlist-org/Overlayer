using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

internal enum OvSlotType : uint {
    Text = 0,
    Number = 1,
    Integer = 2,
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct OvComposeSlot {
    public uint Index;
    public uint Length;
    public OvSlotType SlotType;
    public char* TextPtr;
    public uint TextLen;
    public double NumberVal;
    public int Decimals;
}

internal static unsafe class OvCompositor {
    private const string LibName = "overlayernative";

    public static bool IsSupported => OvNative.IsAvailable;

    public static int Compose(
        string template,
        ReadOnlySpan<OvComposeSlot> slots,
        Span<char> outBuffer
    ) {
        if (string.IsNullOrEmpty(template)) {
            return 0;
        }

        if (IsSupported) {
            try {
                fixed (char* tplPtr = template)
                fixed (OvComposeSlot* slotsPtr = slots)
                fixed (char* outPtr = outBuffer) {
                    return (int)ov_compose_text(
                        tplPtr,
                        (nuint)template.Length,
                        slotsPtr,
                        (nuint)slots.Length,
                        outPtr,
                        (nuint)outBuffer.Length
                    );
                }
            } catch {
                // Fallback to managed compose on failure
            }
        }

        return ComposeManaged(template, slots, outBuffer);
    }

    public static int FormatDouble(double val, int decimals, Span<char> outBuffer) {
        if (IsSupported) {
            try {
                fixed (char* outPtr = outBuffer) {
                    return (int)ov_format_double(val, decimals, outPtr, (nuint)outBuffer.Length);
                }
            } catch {
            }
        }

        string s = decimals >= 0
            ? val.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture)
            : val.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int len = Math.Min(s.Length, outBuffer.Length);
        s.AsSpan(0, len).CopyTo(outBuffer);
        return len;
    }

    public static int ComposeManaged(
        string template,
        ReadOnlySpan<OvComposeSlot> slots,
        Span<char> outBuffer
    ) {
        int outIdx = 0;
        int lastTpl = 0;
        int tplLen = template.Length;

        for (int i = 0; i < slots.Length; i++) {
            ref readonly var slot = ref slots[i];
            int from = Math.Min((int)slot.Index, tplLen);
            if (from > lastTpl) {
                int staticLen = from - lastTpl;
                int copyLen = Math.Min(staticLen, outBuffer.Length - outIdx);
                template.AsSpan(lastTpl, copyLen).CopyTo(outBuffer[outIdx..]);
                outIdx += copyLen;
            }

            switch (slot.SlotType) {
                case OvSlotType.Text:
                    if (slot.TextPtr != null && slot.TextLen > 0) {
                        int copyLen = Math.Min((int)slot.TextLen, outBuffer.Length - outIdx);
                        new ReadOnlySpan<char>(slot.TextPtr, copyLen).CopyTo(outBuffer[outIdx..]);
                        outIdx += copyLen;
                    }
                    break;
                case OvSlotType.Number:
                    if (outIdx < outBuffer.Length) {
                        int written = FormatDouble(slot.NumberVal, slot.Decimals, outBuffer[outIdx..]);
                        outIdx += written;
                    }
                    break;
                case OvSlotType.Integer:
                    if (outIdx < outBuffer.Length) {
                        string s = ((long)slot.NumberVal).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        int copyLen = Math.Min(s.Length, outBuffer.Length - outIdx);
                        s.AsSpan(0, copyLen).CopyTo(outBuffer[outIdx..]);
                        outIdx += copyLen;
                    }
                    break;
            }

            lastTpl = Math.Min((int)(slot.Index + slot.Length), tplLen);
        }

        if (lastTpl < tplLen && outIdx < outBuffer.Length) {
            int staticLen = tplLen - lastTpl;
            int copyLen = Math.Min(staticLen, outBuffer.Length - outIdx);
            template.AsSpan(lastTpl, copyLen).CopyTo(outBuffer[outIdx..]);
            outIdx += copyLen;
        }

        return outIdx;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint ov_compose_text(
        char* templatePtr,
        nuint templateLen,
        OvComposeSlot* slotsPtr,
        nuint slotsCount,
        char* outBuf,
        nuint outMax
    );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint ov_format_double(double val, int decimals, char* outBuf, nuint outMax);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint ov_format_int(long val, char* outBuf, nuint outMax);
}
