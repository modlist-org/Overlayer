using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;

namespace Overlayer.Tag.Compile;

public static class SignatureResolver {
    public static ResolvedSignature Resolve(TagCore tag, Placeholder placeholder, List<CompileDiagnostic> diag, DiagnosticContext context) {
        if (tag.MemberType == TagMemberType.Unknown) {
            diag.Add(new CompileDiagnostic(
                DiagnosticId.InternalError,
                CompileSeverity.Error,
                context,
                []
            ));
            return ResolvedSignature.Invalid;
        }

        if ((tag.TagType & TagType.JsOnly) != 0) {
            diag.Add(new CompileDiagnostic(
                DiagnosticId.JsOnlyBlocked,
                CompileSeverity.Error,
                context,
                [tag.Name]
            ));
            return ResolvedSignature.Invalid;
        }

        string[] rawArgs = placeholder.Args ?? [];
        var parameters = tag.Parameters;

        bool hasFormatFlag = (tag.TagType & TagType.ProcessFormat) != 0;

        string[] args = rawArgs;
        string format = null;
        int minRequired = tag.RequiredParameterCount;

        if (parameters.Length == 0) {
            if (rawArgs.Length > 0) {
                format = rawArgs[0];
            }
        } else if (hasFormatFlag && rawArgs.Length > 0) {
            if (!TryTakeAllAsValues(rawArgs, parameters, minRequired)) {
                format = rawArgs[rawArgs.Length - 1];
                string[] trimmedArgs = new string[rawArgs.Length - 1];
                Array.Copy(rawArgs, trimmedArgs, trimmedArgs.Length);
                args = trimmedArgs;
            }
        }

        int valueParamCount = parameters.Length;
        if (format != null) {
            valueParamCount++;
        }

        if (args.Length < minRequired) {
            diag.Add(new CompileDiagnostic(
                DiagnosticId.ArgTooFew,
                CompileSeverity.Error,
                context,
                [minRequired, args.Length]
            ));

            return ResolvedSignature.Invalid;
        }

        if (args.Length > valueParamCount) {
            diag.Add(new CompileDiagnostic(
                DiagnosticId.ArgTooMany,
                CompileSeverity.Warning,
                context,
                [valueParamCount, args.Length]
            ));
        }

        if (format != null) {
            bool valid;
            Exception e;
            if (tag.IsJS) {
                valid = FormatValidator.TryValidateJs(format, out e);
            } else {
                valid = FormatValidator.TryValidate(tag.ReturnType, format, out e);
            }

            if (!valid) {
                diag.Add(new(
                    DiagnosticId.FormatFail,
                    CompileSeverity.Error,
                    context,
                    [format, e]
                ));

                return ResolvedSignature.Invalid;
            }
        }

        for (int i = 0; i < args.Length && i < parameters.Length; i++) {
            try {
                ArgConverter.Convert(args[i], parameters[i].ParameterType);
            } catch (Exception e) {
                diag.Add(new CompileDiagnostic(
                DiagnosticId.ArgConvertFail,
                    CompileSeverity.Error,
                    context,
                    [i, args[i], parameters[i].ParameterType.Name, e]
                ));

                return ResolvedSignature.Invalid;
            }
        }

        var state =
            args.Length < minRequired
                ? CompileState.Error
                : args.Length > valueParamCount
                    ? CompileState.Warning
                    : CompileState.Valid;

        return new ResolvedSignature {
            Args = args,
            Format = format,
            HasFormat = format != null,
            State = state
        };
    }

    /// <summary>
    /// {Tag:a,b,c} arrives as one argument (the text after the colon). For a C# tag it's read
    /// as separate arguments when that makes a valid call; otherwise it stays one piece, so a
    /// format with commas like {Accuracy:#,##0.00} keeps working.
    /// </summary>
    public static string[] ColonArgs(TagCore tag, string[] args) {
        if (tag == null || tag.IsJS || args is not [string single]) {
            return args;
        }
        var parts = SplitTopLevel(single);
        if (parts.Length < 2) {
            return args;
        }
        var trial = new List<CompileDiagnostic>();
        var sig = Resolve(tag, new Placeholder(tag.Name, parts), trial, new DiagnosticContext(tag.Name, 0, 0));
        // Every piece has to be used: as an argument, or the last one as the format.
        bool usesAll = sig.IsExecutable && sig.Args.Length <= tag.Parameters.Length
            && sig.Args.Length + (sig.HasFormat ? 1 : 0) == parts.Length;
        // Not even a warning (extra arguments only warn).
        return usesAll && trial.Count == 0 ? parts : args;
    }

    // Commas outside parentheses and quotes; each piece trimmed.
    private static string[] SplitTopLevel(string text) {
        var parts = new List<string>();
        int depth = 0, start = 0;
        char quote = '\0';
        for (int i = 0; i < text.Length; i++) {
            char ch = text[i];
            if (quote != '\0') {
                if (ch == quote) {
                    quote = '\0';
                }
            } else if (ch is '"' or '\'') {
                quote = ch;
            } else if (ch == '(') {
                depth++;
            } else if (ch == ')') {
                depth = Math.Max(0, depth - 1);
            } else if (ch == ',' && depth == 0) {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(text[start..].Trim());
        return [.. parts];
    }
    private static bool TryTakeAllAsValues(string[] rawArgs, System.Reflection.ParameterInfo[] parameters, int minRequired) {
        if (rawArgs.Length > parameters.Length || rawArgs.Length < minRequired) {
            return false;
        }
        if (rawArgs.Length <= 1 && minRequired == 0) {
            return false;
        }
        for (int i = 0; i < rawArgs.Length; i++) {
            try {
                ArgConverter.Convert(rawArgs[i], parameters[i].ParameterType);
            } catch {
                return false;
            }
        }
        return true;
    }
}
