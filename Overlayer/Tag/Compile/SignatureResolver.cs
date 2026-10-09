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
            if (rawArgs.Length > 0 && (hasFormatFlag || !tag.IsJS)) {
                format = rawArgs[0];
            }
        } else if (hasFormatFlag && rawArgs.Length > 0) {
            if (!TryTakeAllAsValues(rawArgs, parameters, minRequired)) {
                format = rawArgs[^1];
                args = rawArgs[..^1];
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
