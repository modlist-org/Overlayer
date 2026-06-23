using Overlayer.Core;
using Overlayer.Tag.Core;
using System.Reflection;

namespace Overlayer.V8;

public class TagAccessHelper {
    public object Get(string tagName, params object[] args) {
        if(TagManager.TryGet(tagName, out var tag)) {
            try {
                var result = tag.Invoke(args);
                return result;
            } catch(TargetInvocationException tie) {
                MainCore.Log.Wrn($"[{nameof(TagAccessHelper)}] Target error in {tagName}: {tie.InnerException?.Message}");
            } catch(Exception ex) {
                MainCore.Log.Wrn($"[{nameof(TagAccessHelper)}] Error invoking {tagName}: {ex.GetType().Name} - {ex.Message}");
            }
        }
        return null;
    }
}