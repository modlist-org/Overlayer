using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;

namespace Overlayer.Tag.Runtime;

// Runs many JS tag calls in one V8 transition (~700 ns each when separate).
// Every queued call still runs exactly once and in queue order; results are
// marshalled to the same .NET values ClearScript would have produced.
internal sealed class JsBatch {
    private enum State : byte { Direct, Value, Number, String, Error }

    private readonly List<JsBinding> bindings = [];
    private object[] values = new object[16];
    private double[] numbers = new double[16];
    private int[] starts = new int[16], lengths = new int[16];
    private State[] states = new State[16];
    private readonly List<int> group = [];
    private readonly List<ScriptEngine> engines = [];
    private ScriptEngine[] owners = new ScriptEngine[16];

    internal int Count => bindings.Count;

    internal int Add(JsBinding binding) {
        bindings.Add(binding);
        return bindings.Count - 1;
    }

    internal void Clear() {
        bindings.Clear();
        Array.Clear(values, 0, values.Length);
        Array.Clear(owners, 0, owners.Length);
    }

    internal void Run() {
        int count = bindings.Count;
        if (values.Length < count) {
            values = new object[Math.Max(count, values.Length * 2)];
            states = new State[values.Length];
            numbers = new double[values.Length];
            starts = new int[values.Length];
            lengths = new int[values.Length];
        }
        Array.Clear(states, 0, count);
        if (owners.Length < count) owners = new ScriptEngine[values.Length];
        // Blocked tags never call JS; they keep their last text via the delegate.
        engines.Clear();
        for (int i = 0; i < count; i++) {
            var owner = owners[i] = Owner(bindings[i]);
            if (owner != null && !engines.Contains(owner)) engines.Add(owner);
        }
        foreach (var engine in engines) {
            group.Clear();
            for (int i = 0; i < count; i++) {
                if (owners[i] == engine) group.Add(i);
            }
            // A lone call gains nothing from batching.
            if (group.Count >= 2) RunnerFor(engine)?.Run(this, group);
        }
    }

    // One runner per batch and engine keeps the JS-side call list stable across
    // frames; a runner shared by several callers would rebuild it every call.
    private readonly List<(ScriptEngine Engine, Runner Runner)> runners = [];

    private Runner RunnerFor(ScriptEngine engine) {
        foreach (var entry in runners) {
            if (entry.Engine == engine) return entry.Runner;
        }
        Runner runner;
        try { runner = new Runner(engine); } catch { return null; }
        // Engines are replaced only on reload; drop runners bound to old ones.
        if (runners.Count >= 4) runners.Clear();
        runners.Add((engine, runner));
        return runner;
    }

    private static ScriptEngine Owner(JsBinding binding) =>
        binding.IsBlocked ? null : binding.Function?.Engine;

    // False: evaluate this slot through its normal delegate.
    internal bool TryTake(int index, out string text, out bool failed) {
        text = null;
        failed = false;
        switch (states[index]) {
            case State.Value:
                states[index] = State.Direct;
                object value = values[index];
                values[index] = null;
                text = bindings[index].Complete(value);
                return true;
            case State.Number:
                states[index] = State.Direct;
                text = bindings[index].CompleteNumber(numbers[index]);
                return true;
            case State.String:
                states[index] = State.Direct;
                text = bindings[index].CompleteString((string)values[index], starts[index], lengths[index]);
                values[index] = null;
                return true;
            case State.Error:
                states[index] = State.Direct;
                failed = true;
                return true;
            default:
                return false;
        }
    }

    private void Set(int index, object value) {
        values[index] = value;
        states[index] = State.Value;
    }

    private void SetNumber(int index, double value) {
        numbers[index] = value;
        states[index] = State.Number;
    }

    private void SetString(int index, string source, int start, int length) {
        values[index] = source;
        starts[index] = start;
        lengths[index] = length;
        states[index] = State.String;
    }

    private void Fail(int index) => states[index] = State.Error;

    // Integral values in the safe range become Int32/Int64, other float-exact
    // values Single, the rest Double: ClearScript's own marshalling.
    internal static object Narrow(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value)) return value;
        if (Math.Floor(value) == value) {
            if (Math.Abs(value) > 9007199254740991d) return value;
            if (value >= int.MinValue && value <= int.MaxValue) return (int)value;
            return (long)value;
        }
        float single = (float)value;
        if (single == value) return single;
        return value;
    }

    private sealed unsafe class Runner {
        private static readonly object True = true, False = false;

        private const int KindNumber = 1, KindString = 2, KindTrue = 3, KindFalse = 4,
            KindUndefined = 5, KindNull = 6, KindObject = 7, KindError = 8, KindSameString = 9;

        // Each call writes [kind, value] into a shared Float64Array that C# reads
        // in place; the returned string carries only string results. No number
        // is ever formatted or parsed, and there is no second V8 transition.
        private const string Source = """
            (() => {
                const calls = [], stash = [], prev = [];
                let slots = new Float64Array(0);
                const api = {
                    reset: () => { calls.length = 0; stash.length = 0; prev.length = 0; },
                    add: (fn, ...args) => { calls.push(args.length ? () => fn(...args) : () => fn()); },
                    // Allocated only on layout changes, never during run.
                    slots: (n) => { if (slots.length < n) slots = new Float64Array(n); return slots; },
                    run: () => {
                        let out = '';
                        for (let i = 0, k = 0; i < calls.length; i++, k += 2) {
                            let v;
                            try { v = calls[i](); } catch { slots[k] = 8; continue; }
                            switch (typeof v) {
                                case 'number': slots[k] = 1; slots[k + 1] = v; break;
                                case 'string':
                                    // Unchanged strings are not marshalled again.
                                    if (v === prev[i]) { slots[k] = 9; break; }
                                    prev[i] = v; slots[k] = 2; slots[k + 1] = v.length; out += v; break;
                                case 'boolean': slots[k] = v ? 3 : 4; break;
                                case 'undefined': case 'symbol': slots[k] = 5; break;
                                default: if (v === null) slots[k] = 6; else { stash[i] = v; slots[k] = 7; }
                            }
                        }
                        return out;
                    },
                    take: (i) => { const v = stash[i]; stash[i] = undefined; return v; }
                };
                return (name) => api[name];
            })()
            """;

        private readonly ScriptObject reset, add, run, take, getSlots;
        // Keeps the Float64Array (and so its backing store) alive. V8 never moves
        // an ArrayBuffer's backing store and this one is closure-private (never
        // detached or resized), so the address stays valid after the
        // InvokeWithDirectAccess callback; it is re-read whenever the array is
        // replaced. Only this runner's run() writes it, under the batch's lock.
        private ITypedArray<double> slotArray;
        private double* slots;
        private JsBinding[] layout = [];
        // Last string each slot returned (or its range in that frame's result
        // string, copied out only once it is repeated); JS sends KindSameString
        // while a slot's string is unchanged.
        private string[] lastStrings = [];
        private string[] lastSources = [];
        private int[] lastStarts = [];
        private int[] lastLengths = [];
        private int layoutCount = -1;

        // Functions fetched with ScriptObject.GetProperty cost ~50 us per call in
        // ClearScript 7.5; the same function returned from Invoke costs ~1 us.
        internal Runner(ScriptEngine engine) {
            var api = (ScriptObject)engine.Evaluate(Source);
            reset = (ScriptObject)api.Invoke(false, "reset");
            add = (ScriptObject)api.Invoke(false, "add");
            run = (ScriptObject)api.Invoke(false, "run");
            take = (ScriptObject)api.Invoke(false, "take");
            getSlots = (ScriptObject)api.Invoke(false, "slots");
        }

        internal void Run(JsBatch batch, List<int> group) {
            string strings;
            try {
                EnsureLayout(batch, group);
                strings = (string)run.Invoke(false);
            } catch {
                // Calls may have partly run; never repeat them.
                layoutCount = -1;
                foreach (int i in group) batch.Fail(i);
                return;
            }

            int pos = 0;
            for (int k = 0; k < group.Count; k++) {
                int slot = group[k];
                double value = slots[2 * k + 1];
                switch ((int)slots[2 * k]) {
                    case KindNumber: batch.SetNumber(slot, value); break;
                    case KindString: {
                        int length = (int)value;
                        batch.SetString(slot, strings, pos, length);
                        lastStrings[k] = null;
                        lastSources[k] = strings;
                        lastStarts[k] = pos;
                        lastLengths[k] = length;
                        pos += length;
                        break;
                    }
                    case KindSameString: {
                        string same = lastStrings[k];
                        if (same == null) {
                            same = lastStrings[k] = lastSources[k].Substring(lastStarts[k], lastLengths[k]);
                            lastSources[k] = null;
                        }
                        batch.SetString(slot, same, 0, same.Length);
                        break;
                    }
                    case KindTrue: batch.Set(slot, True); break;
                    case KindFalse: batch.Set(slot, False); break;
                    case KindUndefined: batch.Set(slot, Undefined.Value); break;
                    case KindNull: batch.Set(slot, null); break;
                    case KindObject:
                        try { batch.Set(slot, take.Invoke(false, k)); } catch { batch.Fail(slot); }
                        break;
                    default: batch.Fail(slot); break;
                }
            }
        }

        // The JS-side call list is rebuilt only when the set of tags changes.
        private void EnsureLayout(JsBatch batch, List<int> group) {
            int count = group.Count;
            bool same = layoutCount == count;
            for (int k = 0; same && k < count; k++) same = ReferenceEquals(layout[k], batch.bindings[group[k]]);
            if (same) return;
            layoutCount = -1;
            reset.Invoke(false);
            if (layout.Length < count) {
                int size = Math.Max(count, layout.Length * 2);
                layout = new JsBinding[size];
                lastStrings = new string[size];
                lastSources = new string[size];
                lastStarts = new int[size];
                lastLengths = new int[size];
            }
            Array.Clear(lastStrings, 0, lastStrings.Length);
            Array.Clear(lastSources, 0, lastSources.Length);
            for (int k = 0; k < count; k++) {
                var binding = batch.bindings[group[k]];
                var args = new object[binding.Args.Length + 1];
                args[0] = binding.Function;
                Array.Copy(binding.Args, 0, args, 1, binding.Args.Length);
                add.Invoke(false, args);
                layout[k] = binding;
            }
            var array = (ITypedArray<double>)getSlots.Invoke(false, Math.Max(2 * count, 64));
            if (!ReferenceEquals(array, slotArray) || slots == null) {
                slotArray = array;
                slots = (double*)array.ArrayBuffer.InvokeWithDirectAccess(pointer => pointer) + (long)array.Offset / sizeof(double);
            }
            layoutCount = count;
        }
    }
}
