using Overlayer.Core;
using System.Collections.Concurrent;
using UnityEngine;

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
    private const int MaxActionsPerFrame = 64;

    public static void Enqueue(Action action) {
        if (action == null) {
            return;
        }

        queue.Enqueue(action);
    }

    private void Update() {
        // Only drain work that was queued before this frame started. Actions
        // enqueued by an action (e.g. a Repeat(0) pump) belong to next frame;
        // otherwise self-enqueuing work runs up to the entire frame cap here.
        int frameBatch = Math.Min(queue.Count, MaxActionsPerFrame);
        int processed = 0;
        while (processed < frameBatch && queue.TryDequeue(out Action action)) {
            processed++;
            try {
                action();
            } catch (Exception e) {
                MainCore.Log.Err(e.ToString());
            }
        }
    }
}
