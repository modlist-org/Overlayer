using Microsoft.ClearScript;
using Overlayer.V8.Scripting.Diagnostic;
using Overlayer.V8.Scripting.Tag;
using System.Reflection;

namespace Overlayer.V8.Scripting.Patch;

public class JSPatchHost(JSScriptLoader loader, string filePath) {
    public const string HostBindingName = "__OverlayerAddPatch";
    public const string BindingScript = @"
        function __OverlayerFnSource(fn) {
            try {
                return (typeof fn === 'function') ? Function.prototype.toString.call(fn) : null;
            } catch (e) {
                return null;
            }
        }
        Object.defineProperty(globalThis, 'AddPatch', {
            value: function(target, options) {
                return __OverlayerAddPatch(
                    target,
                    options,
                    __OverlayerFnSource(options && options.prefix),
                    __OverlayerFnSource(options && options.postfix)
                );
            },
            writable: true,
            configurable: true
        });
        Object.defineProperty(globalThis, 'RemovePatch', {
            value: function(handle) {
                return __OverlayerRemovePatch(handle);
            },
            writable: true,
            configurable: true
        });
    ";

    private static readonly Dictionary<string, Type> Keywords = new(StringComparer.Ordinal) {
        ["bool"] = typeof(bool),
        ["byte"] = typeof(byte),
        ["sbyte"] = typeof(sbyte),
        ["char"] = typeof(char),
        ["decimal"] = typeof(decimal),
        ["double"] = typeof(double),
        ["float"] = typeof(float),
        ["int"] = typeof(int),
        ["uint"] = typeof(uint),
        ["long"] = typeof(long),
        ["ulong"] = typeof(ulong),
        ["short"] = typeof(short),
        ["ushort"] = typeof(ushort),
        ["object"] = typeof(object),
        ["string"] = typeof(string),
        ["void"] = typeof(void),
    };

    private readonly JSScriptLoader _loader = loader;
    public string FilePath { get; } = filePath;

    public int AddPatch(object target, object options, string prefixSource, string postfixSource) {
        if(target is not string targetText || string.IsNullOrWhiteSpace(targetText)) {
            Diag("target must be a \"Type::Method\" or \"Type::Method(Args)\" string.");
            return -1;
        }
        ScriptObject prefix = null;
        ScriptObject postfix = null;
        if(options is ScriptObject obj) {
            prefix = AsFunc(obj.GetProperty("prefix"));
            postfix = AsFunc(obj.GetProperty("postfix"));
        }
        if(prefix == null && postfix == null) {
            Diag("AddPatch needs at least a prefix or a postfix function in options.");
            return -1;
        }
        int prefixArity = Arity(prefix, prefixSource, out bool prefixRest);
        int postfixArity = Arity(postfix, postfixSource, out bool postfixRest);
        int prefixInstance = InstanceIndex(prefixSource, prefixArity);
        int postfixInstance = InstanceIndex(postfixSource, postfixArity);
        int prefixEff = prefixArity - (prefixInstance >= 0 ? 1 : 0);
        int postfixEff = postfixArity - (postfixInstance >= 0 ? 1 : 0);
        MethodBase resolved;
        try {
            resolved = Resolve(targetText.Trim(), prefixEff, postfixEff);
        } catch(Exception e) {
            Diag(e.Message);
            return -1;
        }
        try {
            return JSPatchManager.Add(FilePath, resolved, prefix, postfix,
                prefixSource, postfixSource, prefixArity, postfixArity, prefixRest, postfixRest,
                prefixInstance, postfixInstance);
        } catch(Exception e) {
            Diag(e.Message);
            return -1;
        }
    }

    public bool RemovePatch(object handle) {
        try {
            return JSPatchManager.Remove(Convert.ToInt32(handle));
        } catch {
            return false;
        }
    }

    private void Diag(string message) {
        _loader.Diagnostics.Add(new JSDiagnostic(
            JSTagDiagnosticId.ScriptError, JSSeverity.Error, FilePath,
            new InvalidOperationException($"AddPatch: {message}")));
    }

    private static ScriptObject AsFunc(object value) =>
        value is ScriptObject obj ? obj : null;

    // Harmony-style: a parameter literally named `__instance` is bound to
    // the target instance (null for static methods) and excluded from
    // overload-arity matching. Any position works:
    //   prefix: (args, __instance) => { ... }
    //   postfix: (args, result, __instance) => { ... }
    private static int InstanceIndex(string source, int arity) {
        if(arity <= 0 || string.IsNullOrWhiteSpace(source)) {
            return -1;
        }
        var names = ParamNames(source);
        for(int i = 0; i < names.Count; i++) {
            if(names[i] == "__instance") {
                return i;
            }
        }
        return -1;
    }

    private static List<string> ParamNames(string source) {
        string inner = ParamsOf(source).Trim();
        if(inner.Length >= 2 && inner[0] == '(' && inner[^1] == ')') {
            inner = inner[1..^1];
        }
        if(string.IsNullOrWhiteSpace(inner)) {
            return [];
        }
        var names = new List<string>();
        foreach(string part in SplitTopLevel(inner)) {
            names.Add(SimpleName(part));
        }
        return names;
    }

    private static string SimpleName(string param) {
        string t = param.Trim();
        if(t.StartsWith("...", StringComparison.Ordinal)) {
            return null;
        }
        t = t.TrimStart(new char[]{});
        if(t.Length == 0 || t[0] is '{' or '[' or '(') {
            return null;
        }
        int eq = t.IndexOf('=');
        if(eq >= 0) {
            t = t.Substring(0, eq).TrimEnd(new char[]{});
        }
        int i = 0;
        while(i < t.Length && (char.IsLetterOrDigit(t[i]) || t[i] == '_' || t[i] == '$')) {
            i++;
        }
        string ident = t[..i];
        if(ident.Length == 0 || t[i..].Trim().Length != 0) {
            return null;
        }
        return ident;
    }

    private static int Arity(ScriptObject fn, string source, out bool rest) {
        rest = false;
        if(fn == null) {
            return 0;
        }
        try {
            object len = fn.GetProperty("length");
            if(len is int i) {
                rest = HasRest(source);
                return i;
            }
            if(len is double d) {
                rest = HasRest(source);
                return (int)d;
            }
        } catch { }
        rest = HasRest(source);
        return CountParams(source);
    }

    private static bool HasRest(string source) {
        foreach(string p in SplitTopLevel(ParamsOf(source))) {
            if(p.TrimStart(new char[]{}).StartsWith("...", StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    private static int CountParams(string source) {
        string inner = ParamsOf(source).Trim();
        if(inner.Length >= 2 && inner[0] == '(' && inner[^1] == ')') {
            inner = inner[1..^1];
        }
        if(string.IsNullOrWhiteSpace(inner)) {
            return 0;
        }
        return SplitTopLevel(inner).Count;
    }

    private static string ParamsOf(string source) {
        if(string.IsNullOrWhiteSpace(source)) {
            return string.Empty;
        }
        int open = source.IndexOf('(');
        if(open < 0) {
            int arrow = source.IndexOf("=>", StringComparison.Ordinal);
            if(arrow < 0) {
                return string.Empty;
            }
            string head = source.Substring(0, arrow).Trim();
            return head.StartsWith("...", StringComparison.Ordinal) ? "...x" : head.IndexOf(',') >= 0 ? head : head;
        }
        int depth = 0;
        for(int i = open; i < source.Length; i++) {
            if(source[i] == '(') depth++;
            else if(source[i] == ')' && --depth == 0) {
                return source[(open + 1)..i];
            }
        }
        return string.Empty;
    }

    private static List<string> SplitTopLevel(string source) {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for(int i = 0; i < source.Length; i++) {
            char c = source[i];
            if(c is '(' or '[' or '{' or '<') depth++;
            else if(c is ')' or ']' or '}' or '>') depth--;
            else if(c == ',' && depth == 0) {
                parts.Add(source[start..i]);
                start = i + 1;
            }
        }
        parts.Add(source[start..]);
        return parts;
    }

    private static MethodBase Resolve(string target, int prefixArity, int postfixArity) {
        int sep = target.IndexOf("::", StringComparison.Ordinal);
        if(sep < 0) {
            throw new InvalidOperationException($"Bad target \"{target}\". Use \"Type::Method\" or \"Type::Method(Arg, ...)\"");
        }
        string typeName = target[..sep].Trim();
        string rest = target[(sep + 2)..].Trim();
        string methodName = rest;
        string[] explicitSig = null;
        int paren = rest.IndexOf('(');
        if(paren >= 0 && rest.EndsWith(")", StringComparison.Ordinal)) {
            methodName = rest[..paren].Trim();
            explicitSig = SplitTopLevel(rest[(paren + 1)..^1])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();
        }
        if(string.IsNullOrEmpty(methodName)) {
            throw new InvalidOperationException($"Bad target \"{target}\". Missing method name.");
        }
        Type type = FindType(typeName)
            ?? throw new InvalidOperationException($"Type not found: {typeName}");
        var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == methodName && !m.IsGenericMethodDefinition)
            .ToArray();
        if(candidates.Length == 0) {
            throw new InvalidOperationException($"Method not found: {typeName}::{methodName}");
        }
        if(explicitSig != null) {
            var hit = candidates.FirstOrDefault(m =>
                ParamsMatch(m.GetParameters(), explicitSig));
            if(hit == null) {
                throw new InvalidOperationException(
                    $"No overload matches {target}. Candidates: {string.Join("; ", candidates.Select(Sig))}");
            }
            return hit;
        }
        if(candidates.Length == 1) {
            return candidates[0];
        }
        int? wanted = null;
        if(prefixArity > 1) {
            wanted = prefixArity;
        } else if(postfixArity > 2) {
            wanted = postfixArity - 1;
        }
        if(wanted != null) {
            var hits = candidates.Where(m => m.GetParameters().Length == wanted.Value).ToArray();
            if(hits.Length == 1) {
                return hits[0];
            }
            if(hits.Length == 0) {
                throw new InvalidOperationException(
                    $"No overload of {typeName}::{methodName} takes {wanted} args (__instance does not count toward callback arity). Candidates: {string.Join("; ", candidates.Select(Sig))}");
            }
        }
        throw new InvalidOperationException(
            $"Ambiguous: {typeName}::{methodName} has {candidates.Length} overloads. Candidates: {string.Join("; ", candidates.Select(Sig))}. " +
            "Use a callback with matching expanded arity or specify \"Type::Method(Args)\". A parameter named __instance is excluded from callback arity.");
    }

    private static bool ParamsMatch(ParameterInfo[] parameters, string[] tokens) {
        if(parameters.Length != tokens.Length) {
            return false;
        }
        for(int i = 0; i < parameters.Length; i++) {
            if(!TokenMatches(parameters[i].ParameterType, tokens[i])) {
                return false;
            }
        }
        return true;
    }

    private static bool TokenMatches(Type type, string token) {
        if(Keywords.TryGetValue(token, out var kw)) {
            return kw == type;
        }
        if(token == type.Name || token == type.FullName) {
            return true;
        }
        if(token.EndsWith("[]", StringComparison.Ordinal) && type.IsArray) {
            return TokenMatches(type.GetElementType(), token[..^2]);
        }
        if(token.EndsWith("?", StringComparison.Ordinal)
            && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
            return TokenMatches(type.GetGenericArguments()[0], token[..^1]);
        }
        return false;
    }

    private static string Sig(MethodInfo m) =>
        $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})";

    private static Type FindType(string name) {
        if(Keywords.TryGetValue(name, out var kw)) {
            return kw;
        }
        if(name.EndsWith("[]", StringComparison.Ordinal)) {
            Type elem = FindType(name[..^2]);
            return elem?.MakeArrayType();
        }
        if(name.EndsWith("?", StringComparison.Ordinal)) {
            Type elem = FindType(name[..^1]);
            return elem != null && elem.IsValueType ? typeof(Nullable<>).MakeGenericType(elem) : elem;
        }
        Type direct = Type.GetType(name, false);
        if(direct != null) {
            return direct;
        }
        foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            Type[] types;
            try {
                types = asm.GetTypes();
            } catch {
                continue;
            }
            foreach(var t in types) {
                if(t.Name == name || t.FullName == name) {
                    return t;
                }
            }
        }
        return null;
    }
}
