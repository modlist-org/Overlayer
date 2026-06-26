using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;

namespace Overlayer.Tag.Compile;

public static class SignatureResolver {
    public static ResolvedSignature Resolve(TagCore tag, Placeholder placeholder, List<CompileDiagnostic> diag, DiagnosticContext context) {
        string[] rawArgs = placeholder.Args ?? [];
        var parameters = tag.Parameters;

        bool hasFormatFlag = (tag.TagType & TagType.ProcessFormat) != 0;

        string[] args = rawArgs;
        string format = null;

        if(parameters.Length == 0) {
            if(rawArgs.Length > 0) {
                format = rawArgs[0];
            }
        } else if(hasFormatFlag && rawArgs.Length > 0) {
            format = rawArgs[^1];
            args = rawArgs[..^1];
        }

        int valueParamCount = parameters.Length;
        int minRequired = tag.RequiredParameterCount;
        if(format != null) {
            valueParamCount++;
        }

        if(args.Length < minRequired) {
            diag.Add(new CompileDiagnostic(
                DiagnosticId.ArgTooFew,
                CompileSeverity.Error,
                context,
                [minRequired, args.Length]
            ));

            return ResolvedSignature.Invalid;
        }

        if(args.Length > valueParamCount) {
            diag.Add(new CompileDiagnostic(
                DiagnosticId.ArgTooMany,
                CompileSeverity.Warning,
                context,
                [valueParamCount, args.Length]
            ));
        }

        if(format != null) {
            Type checkType = tag.IsJS ? typeof(string) : tag.ReturnType;

            if(!FormatValidator.TryValidate(checkType, format, out var e)) {
                diag.Add(new(
                    DiagnosticId.FormatFail,
                    CompileSeverity.Error,
                    context,
                    [format, e]
                ));

                return ResolvedSignature.Invalid;
            }
        }

        for(int i = 0; i < args.Length && i < parameters.Length; i++) {
            try {
                ArgConverter.Convert(args[i], parameters[i].ParameterType);
            } catch(Exception e) {
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
}