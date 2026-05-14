using System.Collections.Concurrent;
using UnityEngine;

namespace Overlayer;

internal sealed class MainThread : MonoBehaviour {
    private static readonly ConcurrentQueue<Action> queue = new();

    private static MainThread instance;

    public static void Initialize() {
        if(instance != null) {
            return;
        }

        GameObject obj = new("OverlayerMainThread");
        DontDestroyOnLoad(obj);

        instance = obj.AddComponent<MainThread>();
    }

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
            } catch(Exception ex) {
                Debug.LogException(ex);
            }
        }
    }
}