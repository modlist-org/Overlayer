using System.Runtime.InteropServices;

namespace Overlayer.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct OvTagSpan {
    public uint RawStart;
    public uint RawLen;
    public uint ExprStart;
    public uint ExprLen;
    public uint FmtStart;
    public uint FmtLen;
}

internal static unsafe class OvTagScanner {
    private const string LibName = "overlayernative";
    private static bool scannerAvailable = true;

    public static bool IsSupported => OvNative.IsAvailable && scannerAvailable;

    public static int Scan(string input, Span<OvTagSpan> buffer) {
        if (string.IsNullOrEmpty(input)) {
            return 0;
        }

        if (IsSupported) {
            try {
                fixed (char* p = input)
                fixed (OvTagSpan* outPtr = buffer) {
                    return (int)ov_tag_scan(p, (nuint)input.Length, outPtr, (nuint)buffer.Length);
                }
            } catch (EntryPointNotFoundException) {
                scannerAvailable = false;
            } catch (DllNotFoundException) {
                scannerAvailable = false;
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

            // Escape {{
            if (i + 1 < len && input[i + 1] == '{') {
                i += 2;
                continue;
            }

            int tagStart = i;
            int depth = 1;
            int parenDepth = 0;
            int bracketDepth = 0;
            int colonPos = -1;
            // `?` of an open ternary: its `:` is not the format separator.
            int ternaries = 0;
            int idx = i + 1;
            bool matched = false;

            while (idx < len) {
                char c = input[idx];

                if (c == '/' && idx + 1 < len && input[idx + 1] == '/') {
                    idx += 2;
                    while (idx < len && input[idx] != '\n') {
                        idx++;
                    }
                    continue;
                }

                if (c == '/' && idx + 1 < len && input[idx + 1] == '*') {
                    idx += 2;
                    while (idx + 1 < len && !(input[idx] == '*' && input[idx + 1] == '/')) {
                        idx++;
                    }
                    idx = Math.Min(len, idx + 2);
                    continue;
                }

                if (c == '\'' || c == '"' || c == '`') {
                    idx = SkipString(input, idx);
                    continue;
                }

                if (c == '(') {
                    parenDepth++;
                } else if (c == ')') {
                    if (parenDepth > 0) parenDepth--;
                } else if (c == '[') {
                    bracketDepth++;
                } else if (c == ']') {
                    if (bracketDepth > 0) bracketDepth--;
                } else if (c == '{') {
                    depth++;
                } else if (c == '}') {
                    depth--;
                    if (depth == 0) {
                        matched = true;
                        break;
                    }
                } else if (c == '?') {
                    if (depth == 1 && parenDepth == 0 && bracketDepth == 0 && colonPos < 0) {
                        char next = idx + 1 < len ? input[idx + 1] : '\0';
                        if (next == '?') {
                            idx++; // `??`
                        } else if (next != '.') { // `?.` is optional chaining
                            ternaries++;
                        }
                    }
                } else if (c == ':') {
                    if (depth == 1 && parenDepth == 0 && bracketDepth == 0 && colonPos < 0) {
                        if (ternaries > 0) ternaries--;
                        else colonPos = idx;
                    }
                }

                idx++;
            }

            if (matched) {
                int tagEnd = idx;
                int rawLen = tagEnd - tagStart + 1;

                int exprStart = tagStart + 1;
                int exprLen;
                int fmtStart = 0;
                int fmtLen = 0;

                if (colonPos >= 0) {
                    exprLen = colonPos - exprStart;
                    fmtStart = colonPos + 1;
                    fmtLen = tagEnd - fmtStart;
                } else {
                    exprLen = tagEnd - exprStart;
                }

                if (count < buffer.Length) {
                    buffer[count] = new OvTagSpan {
                        RawStart = (uint)tagStart,
                        RawLen = (uint)rawLen,
                        ExprStart = (uint)exprStart,
                        ExprLen = (uint)exprLen,
                        FmtStart = (uint)fmtStart,
                        FmtLen = (uint)fmtLen,
                    };
                }
                count++;
                i = tagEnd + 1;
            } else {
                i++;
            }
        }

        return count;
    }

    private static int SkipString(string input, int start) {
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
    private static extern nuint ov_tag_scan(char* src, nuint len, OvTagSpan* outTags, nuint maxTags);
}
