using Overlayer.Core;
using Overlayer.Tag.Core;
using System.Reflection;

namespace Overlayer.V8;

public class TagAccessHelper {
    public object Get(string tagName, params object[] args) {
        if (TagManager.TryGet(tagName, out var tag)) {
            Patch.Lazy.LazyPatchController.Touch(tagName);
            // The generated JS wrappers pass every parameter on (`Fps(windowMs)`), so `Fps()`
            // arrives as an undefined argument; left off, it takes the parameter's default.
            int count = args?.Length ?? 0;
            while (count > 0 && args[count - 1] is Microsoft.ClearScript.Undefined) {
                count--;
            }
            if (args != null && count < args.Length) {
                args = args.Take(count).ToArray();
            }
            try {
                var result = tag.Invoke(args);
                return result;
            } catch (ObjectDisposedException) {
                // Engine was disposed by a concurrent script reload; quiet fallback.
                return null;
            } catch (TargetInvocationException tie) {
                if (tie.InnerException is ObjectDisposedException) {
                    return null;
                }
                MainCore.Log.Wrn($"[{nameof(TagAccessHelper)}] Target error in {tagName}: {tie.InnerException?.Message}");
            } catch (Exception ex) {
                MainCore.Log.Wrn($"[{nameof(TagAccessHelper)}] Error invoking {tagName}: {ex.GetType().Name} - {ex.Message}");
            }
        }
        return null;
    }
}