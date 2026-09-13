using Microsoft.ClearScript.V8;
using Overlayer.Core;
using Overlayer.IO.Fx;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;

namespace Overlayer.TagImpl;

public static class JavaScirpt {
    /// <summary>
    /// Specifies the internal error types for the JavaScript advanced tag execution pipeline.
    /// </summary>
    public enum JSErrorId {
        /// <summary>
        /// Triggered when the JavaScript tag is called without any expression arguments.
        /// </summary>
        MissingExpression,

        /// <summary>
        /// Triggered when the V8 JavaScript engine instance is null or has not been initialized.
        /// </summary>
        EngineNull,

        /// <summary>
        /// Triggered when the V8 engine fails to parse or compile the JavaScript code due to a syntax error.
        /// <para><strong>Data:</strong></para>
        /// <c>[0]</c> (Exception) : The underlying V8 compilation or syntax exception caught.
        /// </summary>
        SyntaxError
    }

    public readonly struct JSDiagnosticData(JSErrorId id, object data) {
        public readonly JSErrorId Id = id;
        public readonly object Data = data;
    }

    [Tag(
        TagType = TagType.Advanced,
        Desc = "Evaluates JavaScript expressions using the V8 engine.\nOptimized with pre-compiled script execution."
    )]
    public static Func<string> JSExpr(ParsedTag parsed, DiagnosticContext ctx, List<CompileDiagnostic> diags) {
        if(parsed.Args == null || parsed.Args.Length == 0) {
            diags.Add(new CompileDiagnostic(
                DiagnosticId.AdvancedTagException,
                CompileSeverity.Error,
                ctx,
                [new JSDiagnosticData(JSErrorId.MissingExpression, null)]
            ));
            return () => parsed.Raw;
        }

        string restoredJsCode = string.Join(", ", parsed.Args);

        var v8Manager = MainCore.V8;
        if(v8Manager?.Engine == null) {
            diags.Add(new CompileDiagnostic(
                DiagnosticId.AdvancedTagException,
                CompileSeverity.Error,
                ctx,
                [new JSDiagnosticData(JSErrorId.EngineNull, null)]
            ));
            return () => parsed.Raw;
        }

        V8Script compiledScript;
        try {
            compiledScript = v8Manager.Engine.Compile(restoredJsCode);
        } catch(Exception ex) {
            diags.Add(new CompileDiagnostic(
                DiagnosticId.AdvancedTagException,
                CompileSeverity.Error,
                ctx,
                [new JSDiagnosticData(JSErrorId.SyntaxError, ex)]
            ));
            return () => parsed.Raw;
        }

        return () => {
            try {
                if (v8Manager.TryEvaluateFx(FxValue.WrapJsBlock(restoredJsCode), out var result)) {
                    return result?.ToString() ?? string.Empty;
                }

                return parsed.Raw;
            } catch {
                return parsed.Raw;
            }
        };
    }
}