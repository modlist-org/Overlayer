using UnityEngine.EventSystems;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
#endif

namespace Overlayer.UI.Utility;

public class UnityUtils {
    public static void AddEvent(EventTriggerType type, Action<PointerEventData> cb, EventTrigger trigger) {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>((e) =>
#else            
                (e) =>
#endif
                {
                    if(e is PointerEventData ped) {
                        cb(ped);
                    }
                }
#if ML && IL2CPP
            ))
#endif
    );
        trigger.triggers.Add(entry);
    }
}
