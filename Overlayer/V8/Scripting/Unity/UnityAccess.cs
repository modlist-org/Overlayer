using Microsoft.ClearScript;
using Overlayer.Async;
using Overlayer.Core;
using UnityEngine;

namespace Overlayer.V8.Scripting.Unity;

// Minimal JS-side glue, exposed as the global `Unity` object.
// Everything else is the real UnityEngine API, exposed as host types:
// GameObject, Transform, RectTransform, Component, Behaviour,
// Vector2/3/4, Quaternion, Color, Color32, Mathf, Time, Random,
// UnityObject (= UnityEngine.Object; renamed only because JS already
// has a global `Object`).
//
//   var go = GameObject.Find("Player");
//   go.transform.position = new Vector3(0, 5, 0);
//   go.AddComponent(typeof(Rigidbody)); // needs the type exposed
//   UnityObject.Destroy(go, 1.0);
//
// Two gaps the raw API can't cover from JS, filled here:
//   1. Un-exposed types (game classes, Rigidbody, ...) by string name.
//   2. Destroyed-object checks (a dead host proxy is not JS null).
//   3. NextTick (script files load off the main thread).
// Failures return null / false instead of throwing.
public sealed class UnityAccess {
    private readonly Dictionary<string, Type> typeCache = [];
    // Misses remember the assembly count they scanned; a full scan (GetTypes on
    // every assembly) only reruns once a new assembly has loaded.
    private readonly Dictionary<string, int> missCache = [];
    private readonly object gate = new();

    private static object Norm(object value)
        => value == null || ReferenceEquals(value, Undefined.Value) ? null : value;

    private static bool ToFloat(object value, out float result) {
        result = 0f;
        value = Norm(value);
        if(value == null) {
            return false;
        }
        try {
            result = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        } catch {
            return false;
        }
    }

    private static GameObject AsGameObject(object obj) {
        obj = Norm(obj);
        return obj switch {
            GameObject go => go,
            Component c => c.gameObject,
            _ => null,
        };
    }

    private Type ResolveType(string name) {
        if(string.IsNullOrWhiteSpace(name)) {
            return null;
        }
        name = name.Trim();
        lock(gate) {
            if(typeCache.TryGetValue(name, out var cached)) {
                return cached;
            }
        }
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        lock(gate) {
            if(missCache.TryGetValue(name, out int scanned) && scanned == assemblies.Length) {
                return null;
            }
        }
        Type direct = Type.GetType(name, false);
        if(direct == null) {
            foreach(var asm in assemblies) {
                Type[] types;
                try {
                    types = asm.GetTypes();
                } catch {
                    continue;
                }
                foreach(var t in types) {
                    if(t.Name == name || t.FullName == name) {
                        direct = t;
                        break;
                    }
                }
                if(direct != null) {
                    break;
                }
            }
        }
        lock(gate) {
            if(direct != null) {
                typeCache[name] = direct;
                missCache.Remove(name);
            } else {
                missCache[name] = assemblies.Length;
            }
        }
        return direct;
    }

    private Type ResolveComponent(string name) {
        var type = ResolveType(name);
        if(type == null || !typeof(Component).IsAssignableFrom(type)) {
            return null;
        }
        return type;
    }

    // Same names as UnityEngine.Object, string-typed overloads.
    public object[] FindObjectsOfType(string typeName) {
        var type = ResolveType(typeName);
        if(type == null) {
            return [];
        }
        try {
#pragma warning disable CS0618
            return UnityEngine.Object.FindObjectsOfType(type);
#pragma warning restore CS0618
        } catch {
            return [];
        }
    }

    public object FindObjectOfType(string typeName) {
        var type = ResolveType(typeName);
        if(type == null) {
            return null;
        }
        try {
#pragma warning disable CS0618
            return UnityEngine.Object.FindObjectOfType(type);
#pragma warning restore CS0618
        } catch {
            return null;
        }
    }

    // Same names as GameObject, for types JS can't name directly.
    public object AddComponent(object obj, string typeName) {
        var go = AsGameObject(obj);
        var type = ResolveComponent(typeName);
        if(go == null || type == null) {
            return null;
        }
        try {
            return go.AddComponent(type);
        } catch {
            return null;
        }
    }

    public object GetComponent(object obj, string typeName) {
        var go = AsGameObject(obj);
        var type = ResolveComponent(typeName);
        if(go == null || type == null) {
            return null;
        }
        try {
            return go.GetComponent(type);
        } catch {
            return null;
        }
    }

    public object[] GetComponents(object obj, string typeName) {
        var go = AsGameObject(obj);
        var type = ResolveComponent(typeName);
        if(go == null || type == null) {
            return [];
        }
        try {
            return go.GetComponents(type);
        } catch {
            return [];
        }
    }

    public bool HasComponent(object obj, string typeName) {
        return GetComponent(obj, typeName) != null;
    }

    // Dead Unity objects still look non-null from JS. Use this.
    public bool IsValid(object obj) {
        obj = Norm(obj);
        if(obj is UnityEngine.Object u) {
            return u != null;
        }
        return obj != null;
    }

    // Run fn on the Unity main thread next frame. Needed when touching
    // Unity API from script load time (loads run off the main thread).
    public bool NextTick(object fn) {
        if(Norm(fn) is not ScriptObject callback) {
            return false;
        }
        try {
            MainThread.Enqueue(() => {
                try {
                    MainCore.V8.InvokeCallback(callback, []);
                } catch {
                }
            });
            return true;
        } catch {
            return false;
        }
    }

    private sealed class RepeatEntry {
        public float Interval;
        public double Next;
        public ScriptObject Fn;
        public string LastError;
    }

    private readonly object repeatGate = new();
    private readonly Dictionary<int, RepeatEntry> repeats = new();
    private int nextRepeatHandle = 1;
    private bool pumpQueued;

    // Run fn every `seconds` on the main thread. seconds <= 0 runs it
    // every frame. Returns a handle for CancelTick (0 = bad args).
    // Same cost class as a per-frame patch callback: keep fn light.
    public int Repeat(object seconds, object fn) {
        if(Norm(fn) is not ScriptObject callback) {
            return 0;
        }
        float interval = 0f;
        if(Norm(seconds) != null && !ToFloat(seconds, out interval)) {
            return 0;
        }
        if(interval < 0f) {
            interval = 0f;
        }
        lock(repeatGate) {
            int handle = nextRepeatHandle++;
            repeats[handle] = new RepeatEntry {
                Interval = interval,
                Next = UnityEngine.Time.realtimeSinceStartup,
                Fn = callback,
            };
            if(!pumpQueued) {
                pumpQueued = true;
                MainThread.Enqueue(Pump);
            }
            return handle;
        }
    }

    public bool CancelTick(object handle) {
        if(!ToInt(handle, out int id)) {
            return false;
        }
        lock(repeatGate) {
            return repeats.Remove(id);
        }
    }

    private void Pump() {
        List<ScriptObject> due = null;
        lock(repeatGate) {
            pumpQueued = false;
            if(repeats.Count == 0) {
                return;
            }
            double now = UnityEngine.Time.realtimeSinceStartup;
            foreach(var entry in repeats.Values) {
                if(now >= entry.Next) {
                    entry.Next = now + entry.Interval;
                    (due ??= new List<ScriptObject>()).Add(entry.Fn);
                }
            }
            if(repeats.Count > 0) {
                pumpQueued = true;
                MainThread.Enqueue(Pump);
            }
        }
        if(due == null) {
            return;
        }
        foreach(var fn in due) {
            try {
                MainCore.V8.InvokeCallback(fn, []);
            } catch(ObjectDisposedException) {
                RemoveFn(fn);
            } catch(Exception e) {
                string message = e.Message ?? "error";
                lock(repeatGate) {
                    foreach(var entry in repeats.Values) {
                        if(entry.Fn == fn && entry.LastError != message) {
                            entry.LastError = message;
                            try {
                                MainCore.Log.Wrn($"[Unity] Repeat callback threw, keeping it: {message}");
                            } catch {
                            }
                            break;
                        }
                    }
                }
            }
        }
    }

    private void RemoveFn(ScriptObject fn) {
        lock(repeatGate) {
            int found = 0;
            foreach(var pair in repeats) {
                if(pair.Value.Fn == fn) {
                    found = pair.Key;
                    break;
                }
            }
            if(found != 0) {
                repeats.Remove(found);
            }
        }
    }

    private static bool ToInt(object value, out int result) {
        result = 0;
        value = Norm(value);
        if(value == null) {
            return false;
        }
        try {
            result = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        } catch {
            return false;
        }
    }
}
