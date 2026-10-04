using Overlayer.Core;

namespace Overlayer.V8.Scripting;

public sealed class JSLog {
    public void Msg(object message) => MainCore.Log.Msg($"[JS] {message}");
    public void Wrn(object message) => MainCore.Log.Wrn($"[JS] {message}");
    public void Err(object message) => MainCore.Log.Err($"[JS] {message}");
    public void Log(object message) => Msg(message);
    public void Warn(object message) => Wrn(message);
    public void Error(object message) => Err(message);
}
