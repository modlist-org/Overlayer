using Microsoft.ClearScript;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.Tag.Core;
using Overlayer.V8.Scripting.Diagnostic;
using static Overlayer.Overlay.OvObject;

namespace Overlayer.V8.Scripting.Tag;

public class JSTagRegistrationHost(JSScriptLoader loader, string filePath) {
    private readonly JSScriptLoader _loader = loader;
    public string FilePath { get; } = filePath;

    public void RegisterTag(string name, object func, object options) {
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
        List<string> paramList = [];
        bool hasParams = false;

        if(options is ScriptObject obj) {
            if(obj.GetProperty("Params") is ScriptObject paramsProp && !Equals(paramsProp, Undefined.Value)) {
                bool isJsArray = false;
                if(obj.Engine is ScriptEngine engine) {
                    if(engine.Evaluate("Array.isArray") is ScriptObject isArrayFunc) {
                        isJsArray = Convert.ToBoolean(isArrayFunc.Invoke(false, paramsProp));
                    }
                }

                if(isJsArray) {
                    hasParams = true;
                    int length = Convert.ToInt32(paramsProp.GetProperty("length"));

                    for(int i = 0; i < length; i++) {
                        string paramName = paramsProp.GetProperty(i)?.ToString() ?? $"arg{i}";
                        paramList.Add(paramName);
                    }
                }
            }

            var typeProp = obj.GetProperty("Type");
            if(typeProp != null && typeProp != Undefined.Value) {
                type = (TagType)Convert.ToInt32(typeProp);
            }

            var descProp = obj.GetProperty("Desc");
            if(descProp != null && descProp != Undefined.Value) {
                desc = descProp.ToString();
            }
        }

        if(!hasParams) {
            _loader.Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.MissingParams, JSSeverity.Error, FilePath, name));
            return;
        }

        JSTagManager.Add(name, scriptFunc, type, desc);

        try {
            var tag = new TagCore(name, scriptFunc, [.. paramList], type, desc);

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
}