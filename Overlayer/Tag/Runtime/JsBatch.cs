using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;

namespace Overlayer.Tag.Runtime;

// Runs many JS tag calls in one V8 transition (~700 ns each when separate).
// Every queued call still runs exactly once and in queue order; results are
// marshalled to the same .NET values ClearScript would have produced.
internal sealed class JsBatch {
    private enum State : byte { Direct, Value, Error }

    private readonly List<JsBinding> bindings = [];
    private object[] values = new object[16];
    private State[] states = new State[16];
    private readonly List<int> group = [];
    private readonly List<ScriptEngine> engines = [];

    internal int Count => bindings.Count;

    internal int Add(JsBinding binding) {
        bindings.Add(binding);
        return bindings.Count - 1;
    }

    internal void Clear() {
        bindings.Clear();
        Array.Clear(values, 0, values.Length);
    }

    internal void Run() {
        int count = bindings.Count;
        if (values.Length < count) {
            values = new object[Math.Max(count, values.Length * 2)];
            states = new State[values.Length];
        }
        Array.Clear(states, 0, count);
        // Blocked tags never call JS; they keep their last text via the delegate.
        engines.Clear();
        for (int i = 0; i < count; i++) {
            var owner = Owner(bindings[i]);
            if (owner != null && !engines.Contains(owner)) engines.Add(owner);
        }
        foreach (var engine in engines) {
            group.Clear();
            for (int i = 0; i < count; i++) {
                if (Owner(bindings[i]) == engine) group.Add(i);
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

    private sealed class Runner {
        private static readonly object True = true, False = false;

        // Int32 results travel in the string; other numbers go through a shared
        // Float64Array read at most once per run (bit exact, no parsing).
        private const string Source = """
            (() => {
                const calls = [], stash = [];
                let nums = new Float64Array(64);
                const api = {
                    reset: () => { calls.length = 0; stash.length = 0; },
                    add: (fn, ...args) => { calls.push(args.length ? () => fn(...args) : () => fn()); },
                    nums: () => nums,
                    run: () => {
                        let out = '';
                        if (nums.length < calls.length) { nums = new Float64Array(calls.length * 2); out = '!'; }
                        for (let i = 0; i < calls.length; i++) {
                            let v;
                            try { v = calls[i](); } catch { out += 'e'; continue; }
                            switch (typeof v) {
                                case 'number':
                                    if ((v | 0) === v) out += 'i' + v + ';';
                                    else { nums[i] = v; out += 'd'; }
                                    break;
                                case 'string': out += 's' + v.length + ':' + v; break;
                                case 'boolean': out += v ? 't' : 'f'; break;
                                case 'undefined': case 'symbol': out += 'u'; break;
                                default: if (v === null) out += 'z'; else { stash[i] = v; out += 'o'; }
                            }
                        }
                        return out;
                    },
                    take: (i) => { const v = stash[i]; stash[i] = undefined; return v; }
                };
                return (name) => api[name];
            })()
            """;

        private readonly ScriptObject reset, add, run, take, getNums;
        private ITypedArray<double> nums;
        private double[] numbers = [];
        private JsBinding[] layout = [];
        private int layoutCount = -1;

        // Functions fetched with ScriptObject.GetProperty cost ~50 us per call in
        // ClearScript 7.5; the same function returned from Invoke costs ~1 us.
        internal Runner(ScriptEngine engine) {
            var api = (ScriptObject)engine.Evaluate(Source);
            reset = (ScriptObject)api.Invoke(false, "reset");
            add = (ScriptObject)api.Invoke(false, "add");
            run = (ScriptObject)api.Invoke(false, "run");
            take = (ScriptObject)api.Invoke(false, "take");
            getNums = (ScriptObject)api.Invoke(false, "nums");
            nums = (ITypedArray<double>)getNums.Invoke(false);
        }

        internal void Run(JsBatch batch, List<int> group) {
            string encoded;
            try {
                EnsureLayout(batch, group);
                encoded = (string)run.Invoke(false);
            } catch {
                // Calls may have partly run; never repeat them.
                layoutCount = -1;
                foreach (int i in group) batch.Fail(i);
                return;
            }

            int pos = 0;
            if (encoded.Length > 0 && encoded[0] == '!') {
                pos = 1;
                nums = (ITypedArray<double>)getNums.Invoke(false);
            }
            bool numbersRead = false;
            for (int k = 0; k < group.Count; k++) {
                int slot = group[k];
                switch (encoded[pos++]) {
                    case 'i': {
                        bool negative = encoded[pos] == '-';
                        if (negative) pos++;
                        long n = 0;
                        while (encoded[pos] != ';') n = n * 10 + (encoded[pos++] - '0');
                        pos++;
                        batch.Set(slot, (int)(negative ? -n : n));
                        break;
                    }
                    case 'd':
                        if (!numbersRead) {
                            if (numbers.Length < group.Count) numbers = new double[(int)nums.Length];
                            nums.Read(0, (ulong)group.Count, numbers, 0);
                            numbersRead = true;
                        }
                        batch.Set(slot, Narrow(numbers[k]));
                        break;
                    case 's': {
                        int colon = encoded.IndexOf(':', pos);
                        int length = 0;
                        for (int i = pos; i < colon; i++) length = length * 10 + (encoded[i] - '0');
                        batch.Set(slot, encoded.Substring(colon + 1, length));
                        pos = colon + 1 + length;
                        break;
                    }
                    case 't': batch.Set(slot, True); break;
                    case 'f': batch.Set(slot, False); break;
                    case 'u': batch.Set(slot, Undefined.Value); break;
                    case 'z': batch.Set(slot, null); break;
                    case 'o':
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
            if (layout.Length < count) layout = new JsBinding[Math.Max(count, layout.Length * 2)];
            for (int k = 0; k < count; k++) {
                var binding = batch.bindings[group[k]];
                var args = new object[binding.Args.Length + 1];
                args[0] = binding.Function;
                Array.Copy(binding.Args, 0, args, 1, binding.Args.Length);
                add.Invoke(false, args);
                layout[k] = binding;
            }
            layoutCount = count;
        }
    }
}
