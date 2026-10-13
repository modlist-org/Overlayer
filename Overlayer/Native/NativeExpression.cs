namespace Overlayer.Native;

// Cache eviction drops ownership, not the native program. Live compiled tag
// delegates keep this object alive until their final in-flight evaluation ends.
// A program is either an OvJsExpr (JavaScript semantics) or a legacy O5Expr.
internal sealed class NativeExpression {
    private readonly long handle;
    private readonly bool js;
    internal NativeExpression(long handle, bool js = false) {
        this.handle = handle;
        this.js = js;
    }

    internal bool Evaluate(out double value) => Evaluate([], out value);

    internal bool Evaluate(ReadOnlySpan<double> variables, out double value) {
        try {
            if (js) {
                value = OvJsExpr.Eval((nint)handle, variables);
                return true;
            }
            return variables.IsEmpty ? O5Expr.TryEval(handle, out value) : O5Expr.TryEval(handle, 0, variables, out value);
        }
        finally { GC.KeepAlive(this); }
    }

    ~NativeExpression() {
        if (js) OvJsExpr.Free((nint)handle);
        else O5Expr.Free(handle);
    }
}
