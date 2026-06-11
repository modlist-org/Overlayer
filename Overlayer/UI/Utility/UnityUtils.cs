using UnityEngine.EventSystems;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
#endif

namespace Overlayer.UI.Utility;

public class UnityUtils {
    public static void AddEvent(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> cb) {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>(
#endif
            (e) => cb(e)
#if ML && IL2CPP
            ))
#endif
        );
        trigger.triggers.Add(entry);
    }

    public static void AddEvents(EventTrigger trigger, params (EventTriggerType type, Action<BaseEventData> cb)[] events) {
        foreach(var item in events) {
            AddEvent(trigger, item.type, item.cb);
        }
    }

    public static void AddEvent(EventTrigger trigger, EventTriggerType type, Action cb)
        => AddEvent(trigger, type, (_) => cb());

    public static void AddEvents(EventTrigger trigger, params (EventTriggerType type, Action cb)[] events) {
        foreach(var item in events) {
            AddEvent(trigger, item.type, item.cb);
        }
    }
}
