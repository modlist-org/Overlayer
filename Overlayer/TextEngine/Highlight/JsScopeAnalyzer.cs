using Esprima;
using Esprima.Ast;

namespace Overlayer.TextEngine.Highlight;

public static class JsScopeAnalyzer {
    private const int MaxLength = 16384;

    public static List<(string Name, string Detail, bool Callable)> GetDeclared(string source) {
        var result = new List<(string, string, bool)>();
        if (string.IsNullOrWhiteSpace(source) || source.Length > MaxLength) {
            return result;
        }

        try {
            var program = new JavaScriptParser().ParseScript(source);
            var declared = new List<(string Name, string Detail, bool Callable)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Walk(program, declared, seen);
            return declared;
        } catch {
            return GetDeclaredFallback(source);
        }
    }

    private static void Walk(Node node, List<(string Name, string Detail, bool Callable)> declared, HashSet<string> seen) {
        if (node == null) {
            return;
        }

        switch (node) {
            case VariableDeclaration declaration: {
                string detail = declaration.Kind.ToString().ToLowerInvariant();
                foreach (var declarator in declaration.Declarations) {
                    AddIdentifier(declarator.Id, detail, false, declared, seen);
                }

                break;
            }
            case FunctionDeclaration function: {
                if (function.Id != null) {
                    AddName(function.Id.Name, "function", true, declared, seen);
                }

                AddParameters(function.Params, declared, seen);
                break;
            }
            case ClassDeclaration cls: {
                if (cls.Id != null) {
                    AddName(cls.Id.Name, "class", false, declared, seen);
                }

                break;
            }
            case CatchClause catcher: {
                AddIdentifier(catcher.Param, "param", false, declared, seen);
                break;
            }
            case ArrowFunctionExpression arrow: {
                AddParameters(arrow.Params, declared, seen);
                break;
            }
            case FunctionExpression expression: {
                AddParameters(expression.Params, declared, seen);
                break;
            }
        }

        foreach (var child in node.ChildNodes) {
            if (child is Node childNode) {
                Walk(childNode, declared, seen);
            }
        }
    }

    private static void AddParameters(
        System.Collections.IEnumerable parameters,
        List<(string Name, string Detail, bool Callable)> declared,
        HashSet<string> seen) {
        if (parameters == null) {
            return;
        }

        foreach (var parameter in parameters) {
            if (parameter is AssignmentPattern assignment) {
                AddIdentifier(assignment.Left, "param", false, declared, seen);
            } else {
                AddIdentifier(parameter as Node, "param", false, declared, seen);
            }
        }
    }

    private static void AddIdentifier(
        Node node, string detail, bool callable,
        List<(string Name, string Detail, bool Callable)> declared,
        HashSet<string> seen) {
        if (node is Identifier identifier) {
            AddName(identifier.Name, detail, callable, declared, seen);
        }
    }

    private static void AddName(
        string name, string detail, bool callable,
        List<(string Name, string Detail, bool Callable)> declared,
        HashSet<string> seen) {
        if (string.IsNullOrEmpty(name) || !seen.Add(name)) {
            return;
        }

        declared.Add((name, detail, callable));
    }

    private static List<(string Name, string Detail, bool Callable)> GetDeclaredFallback(string source) {
        var result = new List<(string, string, bool)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int i = 0;
        while (i < source.Length) {
            if (!MatchKeyword(source, i, out string keyword, out int after)) {
                i++;
                continue;
            }

            int j = after;
            while (j < source.Length && char.IsWhiteSpace(source[j])) {
                j++;
            }

            int start = j;
            while (j < source.Length && (char.IsLetterOrDigit(source[j]) || source[j] == '_' || source[j] == '$')) {
                j++;
            }

            if (j > start) {
                string name = source[start..j];
                if (seen.Add(name)) {
                    result.Add((name, keyword, keyword == "function"));
                }
            }

            i = j > after ? j : after;
        }

        return result;
    }

    private static bool MatchKeyword(string source, int index, out string keyword, out int after) {
        keyword = null;
        after = index;
        foreach (var candidate in new[] { "function", "const", "let", "var", "class" }) {
            if (source.Length - index < candidate.Length) {
                continue;
            }

            bool matches = true;
            for (int k = 0; k < candidate.Length; k++) {
                if (source[index + k] != candidate[k]) {
                    matches = false;
                    break;
                }
            }

            if (!matches) {
                continue;
            }

            int end = index + candidate.Length;
            bool boundaryBefore = index == 0 || (!char.IsLetterOrDigit(source[index - 1]) && source[index - 1] != '_' && source[index - 1] != '$');
            bool boundaryAfter = end >= source.Length || (!char.IsLetterOrDigit(source[end]) && source[end] != '_' && source[end] != '$');
            if (boundaryBefore && boundaryAfter) {
                keyword = candidate;
                after = end;
                return true;
            }
        }

        return false;
    }

}
