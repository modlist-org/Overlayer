namespace Overlayer.TagImpl;

public static class JavaScirpt {
    public enum JSErrorId {
        MissingExpression,
        EngineNull,
        SyntaxError
    }

    public readonly struct JSDiagnosticData(JSErrorId id, object data) {
        public readonly JSErrorId Id = id;
        public readonly object Data = data;

        public override string ToString() => Id switch {
            JSErrorId.MissingExpression => "Expr: missing expression",
            JSErrorId.EngineNull => "Expr: JavaScript engine not initialized",
            JSErrorId.SyntaxError => (Data as Exception)?.Message ?? "Expr: syntax error",
            _ => Id.ToString()
        };
    }
}
