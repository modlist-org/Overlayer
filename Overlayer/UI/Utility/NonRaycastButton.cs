using UnityEngine;
using UnityEngine.EventSystems;
using Overlayer.Core;


#if ML && IL2CPP
using MelonLoader;
using Il2CppInterop.Runtime;
#endif

namespace Overlayer.UI.Utility; 
#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public class NonRaycastButton
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
# else 
    : MonoBehaviour
#endif
{

    public Action onClick;

    private void Start() {
        var trigger = gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };

        entry.callback.AddListener(
#if ML && IL2CPP
            DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<BaseEventData>>(new Action<BaseEventData>((e) =>
#else            
            (e) =>
#endif
            {
                OnClickInternal();
            }
#if ML && IL2CPP
            ))
#endif
        );
        trigger.triggers.Add(entry);
    }

    private void OnClickInternal() {
        onClick?.Invoke();
    }
}