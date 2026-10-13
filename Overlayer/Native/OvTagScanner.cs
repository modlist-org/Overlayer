using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Overlayer.Native;

internal enum OvTagType : uint {
    Simple = 0,
    Colon = 1,
    Func = 2,
    JsExpr = 3,
}

[StructLayout(LayoutKind.Sequential)]
internal struct OvTagSpan {
    public uint RawStart;
    public uint RawLen;
    public uint NameStart;
    public uint NameLen;
    public uint ArgsStart;
    public uint ArgsLen;
    public OvTagType Type;
}

internal static unsafe class OvTagScanner {
    private const string LibName = "o5native";

    public static bool IsSupported => O5Native.IsAvailable;

    public static int Scan(string input, Span<OvTagSpan> buffer) {
        if (string.IsNullOrEmpty(input)) {
            return 0;
        }

        if (IsSupported) {
            try {
                fixed (char* p = input)
                fixed (OvTagSpan* outPtr = buffer) {
                    return (int)o5_tag_scan(p, (nuint)input.Length, outPtr, (nuint)buffer.Length);
                }
            } catch {
                // Fallback to managed scanner on any native invocation failure.
            }
        }

        return ScanManaged(input, buffer);
    }

    public static int ScanManaged(string input, Span<OvTagSpan> buffer) {
        if (string.IsNullOrEmpty(input)) {
            return 0;
        }

        int count = 0;
        int i = 0;
        int len = input.Length;

        while (i < len) {
            if (input[i] != '{') {
                i++;
                continue;
            }

            int tagStart = i;
            int nameStart = i + 1;
            int nameEnd = nameStart;
            while (nameEnd < len && (char.IsLetterOrDigit(input[nameEnd]) || input[nameEnd] == '_')) {
                nameEnd++;
            }

            if (nameEnd == nameStart || nameEnd >= len) {
                i++;
                continue;
            }

            int nameLen = nameEnd - nameStart;
            char delimiter = input[nameEnd];
            bool isJs = (delimiter == ':' || delimiter == '(') &&
                        input.AsSpan(nameStart, nameLen).Equals("JSExpr", StringComparison.OrdinalIgnoreCase);

            if (isJs) {
                int close = FindBalancedClose(input, nameEnd + 1);
                if (close >= 0) {
                    if (count < buffer.Length) {
                        buffer[count] = new OvTagSpan {
                            RawStart = (uint)tagStart,
                            RawLen = (uint)(close - tagStart + 1),
                            NameStart = (uint)nameStart,
                            NameLen = (uint)nameLen,
                            ArgsStart = (uint)(nameEnd + 1),
                            ArgsLen = (uint)(close - nameEnd - 1),
                            Type = OvTagType.JsExpr,
                        };
                    }
                    count++;
                    i = close + 1;
                    continue;
                }
            }

            if (delimiter == '}') {
                if (count < buffer.Length) {
                    buffer[count] = new OvTagSpan {
                        RawStart = (uint)tagStart,
                        RawLen = (uint)(nameEnd - tagStart + 1),
                        NameStart = (uint)nameStart,
                        NameLen = (uint)nameLen,
                        ArgsStart = 0,
                        ArgsLen = 0,
                        Type = OvTagType.Simple,
                    };
                }
                count++;
                i = nameEnd + 1;
                continue;
            }

            if (delimiter == ':') {
                int close = nameEnd + 1;
                while (close < len && input[close] != '}') {
                    close++;
                }
                if (close < len && input[close] == '}') {
                    if (count < buffer.Length) {
                        buffer[count] = new OvTagSpan {
                            RawStart = (uint)tagStart,
                            RawLen = (uint)(close - tagStart + 1),
                            NameStart = (uint)nameStart,
                            NameLen = (uint)nameLen,
                            ArgsStart = (uint)(nameEnd + 1),
                            ArgsLen = (uint)(close - nameEnd - 1),
                            Type = OvTagType.Colon,
                        };
                    }
                    count++;
                    i = close + 1;
                    continue;
                }
            }

            if (delimiter == '(') {
                int close = nameEnd + 1;
                while (close < len && input[close] != '}') {
                    close++;
                }
                if (close < len && input[close] == '}' && close > nameEnd + 1 && input[close - 1] == ')') {
                    if (count < buffer.Length) {
                        buffer[count] = new OvTagSpan {
                            RawStart = (uint)tagStart,
                            RawLen = (uint)(close - tagStart + 1),
                            NameStart = (uint)nameStart,
                            NameLen = (uint)nameLen,
                            ArgsStart = (uint)(nameEnd + 1),
                            ArgsLen = (uint)(close - 1 - (nameEnd + 1)),
                            Type = OvTagType.Func,
                        };
                    }
                    count++;
                    i = close + 1;
                    continue;
                }
            }

            i++;
        }

        return count;
    }

    private static int FindBalancedClose(string input, int start) {
        int depth = 1;
        int i = start;
        int len = input.Length;
        while (i < len) {
            char c = input[i];
            if (c == '/' && i + 1 < len && input[i + 1] == '/') {
                while (i < len && input[i] != '\n') {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < len && input[i + 1] == '*') {
                i += 2;
                while (i + 1 < len && !(input[i] == '*' && input[i + 1] == '/')) {
                    i++;
                }
                i = Math.Min(len, i + 2);
                continue;
            }
            if (c == '\'' || c == '"' || c == '`') {
                i = SkipJsString(input, i);
                continue;
            }
            if (c == '{') {
                depth++;
            } else if (c == '}') {
                depth--;
                if (depth == 0) return i;
            }
            i++;
        }
        return -1;
    }

    private static int SkipJsString(string input, int start) {
        char quote = input[start];
        int i = start + 1;
        int len = input.Length;
        while (i < len) {
            if (input[i] == '\\') {
                i += 2;
                continue;
            }
            if (input[i] == quote) {
                return i + 1;
            }
            if (quote != '`' && input[i] == '\n') {
                return i;
            }
            i++;
        }
        return len;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [SuppressGCTransition]
    private static extern nuint o5_tag_scan(char* src, nuint len, OvTagSpan* outTags, nuint maxTags);
}
