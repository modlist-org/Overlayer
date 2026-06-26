using Microsoft.ClearScript;
using Overlayer.Async;
using Overlayer.Core;
using Overlayer.Tag.Core;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using Overlayer.V8.Scripting.Diagnostic;
using static Overlayer.Overlay.OvObject;

namespace Overlayer.V8.Scripting.Tag;

public class JSTagRegistrationHost(JSScriptLoader loader, string filePath) {
    private readonly JSScriptLoader _loader = loader;
    public string FilePath { get; } = filePath;

    public void RegisterTag(string name, object func, object options = null) {
        if(string.IsNullOrWhiteSpace(name)) {
            _loader.Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.MissingName, JSSeverity.Error, FilePath, FilePath));
            return;
        }

        if(func is not ScriptObject scriptFunc) {
            _loader.Diagnostics.Add(new JSDiagnostic(JSTagDiagnosticId.InvalidFormat, JSSeverity.Error, FilePath));
            return;
        }

        int argCount = Convert.ToInt32(scriptFunc.GetProperty("length"));
        string desc = null;
        TagType type = TagType.None;

        if(options is ScriptObject obj) {
            var descProp = obj.GetProperty("Desc");
            if(descProp != null && descProp != Undefined.Value) {
                desc = descProp.ToString();
            }

            var typeProp = obj.GetProperty("Type");
            if(typeProp != null && typeProp != Undefined.Value) {
                type = (TagType)Convert.ToInt32(typeProp);
            }
        }

        JSTagManager.Add(name, scriptFunc, type, desc);

        try {
            var tag = new TagCore(name, scriptFunc, argCount, type, desc);

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