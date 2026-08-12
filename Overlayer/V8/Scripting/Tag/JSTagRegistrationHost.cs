using Microsoft.ClearScript;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.Tag.Core;
using Overlayer.V8.Scripting.Diagnostic;
using static Overlayer.Overlay.OvObject;

namespace Overlayer.V8.Scripting.Tag;

public class JSTagRegistrationHost(JSScriptLoader loader, string filePath) {
    public const string HostBindingName = "__OverlayerRegisterTag";
    public const string BindingScript = @"
        Object.defineProperty(globalThis, 'RegisterTag', {
            value: function(name, func, options) {
                __OverlayerRegisterTag(
                    name, 
                    func, 
                    options, 
                    Function.prototype.toString.call(func)
                );
            },
            writable: false,
            configurable: false
        });
    ";

    private readonly JSScriptLoader _loader = loader;
    public string FilePath { get; } = filePath;

    public void RegisterTag(string name, object func, object options, string functionSource) {
        if(string.IsNullOrWhiteSpace(name)) {
            _loader.Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.MissingName, JSSeverity.Error, FilePath, FilePath));
            return;
        }

        if(func is not ScriptObject scriptFunc) {
            _loader.Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.InvalidFormat, JSSeverity.Error, FilePath));
            return;
        }

        string desc = null;
        TagType type = TagType.None;
        string[] paramNames;
        try {
            paramNames = ExtractParameterNames(functionSource);
        } catch(Exception e) {
            _loader.Diagnostics.Add(new JSDiagnostic(
                JSTagDiagnosticId.InvalidFormat,
                JSSeverity.Error,
                FilePath,
                name,
                e.Message
            ));
            return;
        }

        if(options is ScriptObject obj) {
            var typeProp = obj.GetProperty("Type");
            if(typeProp != null && typeProp != Undefined.Value) {
                type = (TagType)Convert.ToInt32(typeProp);
            }

            var descProp = obj.GetProperty("Desc");
            if(descProp != null && descProp != Undefined.Value) {
                desc = descProp.ToString();
            }
        }

        try {
            var tag = new TagCore(name, scriptFunc, paramNames, type, desc);

            JSTagManager.Add(name, scriptFunc, type, desc);
            TagManager.Set(tag);

            _loader.RegisterFileTag(FilePath, name);

            MainCore.V8.GenerateImplJs();
            MainCore.V8.LoadImplJs();

            MainThread.Enqueue(TextEngineUpdater.RecompileAll);
        } catch(Exception) {
            JSTagManager.Remove(name);
            _loader.Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.DuplicateName, JSSeverity.Error, FilePath, name));
        }
    }

    private static string[] ExtractParameterNames(string source) {
        if(string.IsNullOrWhiteSpace(source)) {
            throw new InvalidOperationException("Function source is unavailable");
        }

        int arrow = FindTopLevelArrow(source);
        string parameters;
        if(arrow >= 0) {
            string head = source[..arrow].Trim();
            if(head.StartsWith("async ", StringComparison.Ordinal)) {
                head = head[6..].Trim();
            }

            parameters = head.Length >= 2 && head[0] == '(' && head[^1] == ')'
                ? head[1..^1]
                : head;
        } else {
            int open = source.IndexOf('(');
            int close = open < 0 ? -1 : FindMatchingParenthesis(source, open);
            if(open < 0 || close < 0) {
                throw new InvalidOperationException("Function parameters cannot be parsed");
            }

            parameters = source[(open + 1)..close];
        }

        return SplitParameters(parameters)
            .Select(NormalizeParameter)
            .ToArray();
    }

    private static string[] SplitParameters(string source) {
        if(string.IsNullOrWhiteSpace(source)) {
            return [];
        }

        List<string> parameters = [];
        int start = 0;
        int parentheses = 0;
        int brackets = 0;
        int braces = 0;
        char quote = '\0';
        bool escaped = false;

        for(int i = 0; i < source.Length; i++) {
            char current = source[i];
            if(quote != '\0') {
                if(escaped) {
                    escaped = false;
                } else if(current == '\\') {
                    escaped = true;
                } else if(current == quote) {
                    quote = '\0';
                }

                continue;
            }

            if(current is '\'' or '"' or '`') {
                quote = current;
                continue;
            }

            switch(current) {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '{':
                    braces++;
                    break;
                case '}':
                    braces--;
                    break;
                case ',' when parentheses == 0 && brackets == 0 && braces == 0:
                    parameters.Add(source[start..i]);
                    start = i + 1;
                    break;
            }
        }

        parameters.Add(source[start..]);
        return [.. parameters];
    }

    private static string NormalizeParameter(string source, int index) {
        string parameter = source.Trim();
        int equals = FindTopLevelCharacter(parameter, '=');
        bool optional = equals >= 0;
        string name = (equals >= 0 ? parameter[..equals] : parameter).Trim();

        if(name.StartsWith("...", StringComparison.Ordinal)) {
            name = name[3..].Trim();
            optional = true;
        }

        if(!IsIdentifier(name)) {
            name = $"arg{index + 1}";
        }

        return optional ? $"{name}?" : name;
    }

    private static bool IsIdentifier(string value) {
        if(string.IsNullOrEmpty(value) ||
           (!char.IsLetter(value[0]) && value[0] != '_' && value[0] != '$')) {
            return false;
        }

        return value.Skip(1).All(character =>
            char.IsLetterOrDigit(character) || character == '_' || character == '$'
        );
    }

    private static int FindTopLevelArrow(string source) {
        int parentheses = 0;
        int brackets = 0;
        int braces = 0;
        char quote = '\0';
        bool escaped = false;

        for(int i = 0; i < source.Length; i++) {
            char current = source[i];
            if(quote != '\0') {
                if(escaped) {
                    escaped = false;
                } else if(current == '\\') {
                    escaped = true;
                } else if(current == quote) {
                    quote = '\0';
                }

                continue;
            }

            if(current is '\'' or '"' or '`') {
                quote = current;
                continue;
            }

            switch(current) {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '{':
                    braces++;
                    break;
                case '}':
                    braces--;
                    break;
            }

            if(current == '=' && i + 1 < source.Length && source[i + 1] == '>' &&
               parentheses == 0 && brackets == 0 && braces == 0) {
                return i;
            }
        }

        return -1;
    }

    private static int FindMatchingParenthesis(string source, int start) {
        int depth = 0;
        char quote = '\0';
        bool escaped = false;

        for(int i = start; i < source.Length; i++) {
            char current = source[i];
            if(quote != '\0') {
                if(escaped) {
                    escaped = false;
                } else if(current == '\\') {
                    escaped = true;
                } else if(current == quote) {
                    quote = '\0';
                }

                continue;
            }

            if(current is '\'' or '"' or '`') {
                quote = current;
                continue;
            }

            if(current == '(') {
                depth++;
            } else if(current == ')' && --depth == 0) {
                return i;
            }
        }

        return -1;
    }

    private static int FindTopLevelCharacter(string source, char target) {
        int parentheses = 0;
        int brackets = 0;
        int braces = 0;
        char quote = '\0';
        bool escaped = false;

        for(int i = 0; i < source.Length; i++) {
            char current = source[i];
            if(quote != '\0') {
                if(escaped) {
                    escaped = false;
                } else if(current == '\\') {
                    escaped = true;
                } else if(current == quote) {
                    quote = '\0';
                }

                continue;
            }

            if(current is '\'' or '"' or '`') {
                quote = current;
                continue;
            }

            switch(current) {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '{':
                    braces++;
                    break;
                case '}':
                    braces--;
                    break;
            }

            if(current == target && parentheses == 0 && brackets == 0 && braces == 0) {
                return i;
            }
        }

        return -1;
    }
}
