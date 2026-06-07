using System.Collections.Concurrent;
using UnityEngine;
using Overlayer.Core;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.Async;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
internal sealed
#else
public
#endif
class MainThread
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : MonoBehaviour
#endif
{
    private static readonly ConcurrentQueue<Action> queue = new();

    public static void Enqueue(Action action) {
        if(action == null) {
            return;
        }

        queue.Enqueue(action);
    }

    private void Update() {
        while(queue.TryDequeue(out Action action)) {
            try {
                action();
            } catch(Exception e) {
                MainCore.Log.Err(e.Message);
            }
        }
    }
}