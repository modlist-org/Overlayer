using System.Buffers;
using Overlayer.Core;
using Overlayer.Tag.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using Overlayer.TextEngine.Runtime;

namespace Overlayer.TextEngine.Core;

public sealed class TextEngineCore : IDisposable {
    private readonly object _lock = new();
    private readonly object renderLock = new();
    private long compileGeneration;
    private bool disposed;
    private volatile string text = string.Empty;
    private volatile TextEngineState state;
    private CompiledTextSnapshot current;
    private CompileDiagnostic? engineDiagnostic;

    public string Text {
        get => text;
        set {
            value ??= string.Empty;
            lock (_lock) {
                if (text == value || disposed) return;
                text = value;
                StartCompileLocked();
            }
        }
    }

    /// <summary>Tags only this text can use (registered from its Fx code). Recompiles on change.</summary>
    public LocalTagSet LocalTags {
        get => localTags;
        set {
            lock (_lock) {
                if (localTags == value || disposed) return;
                localTags = value;
                StartCompileLocked();
            }
        }
    }
    private volatile LocalTagSet localTags;

    public void ForceRecompile() {
        lock (_lock) {
            if (!disposed) StartCompileLocked();
        }
    }

    public CompiledSegment[] Segments {
        get { lock (_lock) return current?.Segments ?? []; }
    }

    public TextEngineState State => state;
    internal bool? NativeRendering { get; set; }

    public CompileDiagnostic[] GetDiagnostics() {
        lock (_lock) {
            if (state == TextEngineState.Error)
                return engineDiagnostic.HasValue ? [engineDiagnostic.Value] : [];
            return current == null ? [] : [.. current.Segments.SelectMany(s => s.Replacer.Compiled.Diagnostics)];
        }
    }

    private void StartCompileLocked() {
        state = TextEngineState.Compiling;
        engineDiagnostic = null;
        long generation = ++compileGeneration;
        string source = text;
        var locals = localTags;
        _ = Task.Run(() => CompileInternal(source, locals, generation));
    }

    private void CompileInternal(string source, LocalTagSet locals, long generation) {
        CompiledSegment[] built = null;
        int initialized = 0;
        bool snapshotOwnsBindings = false;
        CompiledTextSnapshot candidate = null;
        try {
            lock (_lock) {
                if (disposed || generation != compileGeneration) return;
            }
            var tags = Parser.Parse(source);
            var plan = new TextRenderPlan(source, tags);
            long bindingGeneration = TagCache.Instance.Generation;
            locals = RunTemplateRegistrations(tags, locals);
            built = new CompiledSegment[tags.Count];
            TagCache.Locals = locals;
            for (int i = 0; i < tags.Count; i++) {
                var tag = tags[i];
                var replacer = new Replacer();
                try { replacer.Parsed = tag; }
                catch { replacer.Dispose(); throw; }
                built[i] = new CompiledSegment(tag.Index, tag.Length, replacer);
                initialized++;
            }
            candidate = new CompiledTextSnapshot(source, built, plan);
            snapshotOwnsBindings = true;
            CompiledTextSnapshot previous;
            lock (_lock) {
                if (disposed || generation != compileGeneration) return;
                if (bindingGeneration != TagCache.Instance.Generation) {
                    StartCompileLocked();
                    return;
                }
                previous = current;
                current = candidate;
                candidate = null; // Ownership transferred to the published generation.
                state = TextEngineState.Ready;
            }
            previous?.Release();
        } catch (Exception error) {
            CompiledTextSnapshot previous;
            lock (_lock) {
                if (disposed || generation != compileGeneration) return;
                previous = current;
                current = null;
                state = TextEngineState.Error;
                engineDiagnostic = new CompileDiagnostic(DiagnosticId.InternalError, CompileSeverity.Error,
                    new DiagnosticContext(null, 0, source.Length), [error]);
            }
            previous?.Release();
        } finally {
            TagCache.Locals = null;
            if (candidate != null) candidate.Release();
            else if (!snapshotOwnsBindings && built != null) {
                for (int i = 0; i < initialized; i++) built[i].Replacer.Dispose();
            }
        }
    }

    private readonly LocalTagScope templateScope = new();

    // {RegisterTag(...)} placeholders in the template run once per compile (not
    // per frame) and add tags only this text can use; they render as nothing.
    // Template tags win over Fx-registered ones on a name clash.
    private LocalTagSet RunTemplateRegistrations(List<ParsedTag> tags, LocalTagSet fxLocals) {
        LocalTagSet mine;
        lock (templateScope) {
            bool ok = true;
            templateScope.Begin();
            try {
                foreach (var tag in tags) {
                    if (LocalTagScope.IsRegistration(tag.Expression)) {
                        object result = null;
                        ok &= MainCore.V8?.TryEvaluateFx(tag.Expression, out result) == true;
                    }
                }
            } finally {
                templateScope.End(ok);
            }
            mine = templateScope.Tags;
        }
        if (mine == null) return fxLocals;
        if (fxLocals == null) return mine;
        var merged = new Dictionary<string, TagCore>(fxLocals.Tags, StringComparer.Ordinal);
        foreach (var pair in mine.Tags) merged[pair.Key] = pair.Value;
        return new LocalTagSet(merged);
    }

    private CompiledTextSnapshot Acquire(out TextEngineState status, out string source) {
        lock (_lock) {
            status = state;
            source = text;
            if (status != TextEngineState.Ready || current == null) return null;
            current.Retain();
            return current;
        }
    }

    private static void Evaluate(CompiledTextSnapshot snapshot, Span<string> replacements, JsBatch batch, int jsBase) {
        // Never deduplicate calls: JS tags may be stateful, including two uses
        // of the same tag in one template. JS calls keep their left-to-right
        // order; a batch runs them in one V8 transition before C# tags.
        int js = jsBase;
        for (int i = 0; i < snapshot.Segments.Length; i++) {
            var replacer = snapshot.Segments[i].Replacer;
            try {
                if (replacer.Compiled.Js != null && batch.TryTake(js++, out string text, out bool failed)) {
                    replacements[i] = failed ? null : text;
                    continue;
                }
                replacements[i] = replacer.Get();
            }
            catch { replacements[i] = null; }
        }
    }

    // Leases the snapshot and queues its JS calls; End must follow.
    private CompiledTextSnapshot Begin(JsBatch batch, out int jsBase, out TextEngineState status, out string source) {
        var snapshot = Acquire(out status, out source);
        jsBase = batch.Count;
        if (snapshot != null) foreach (var call in snapshot.JsCalls) batch.Add(call);
        return snapshot;
    }

    private string End(CompiledTextSnapshot snapshot, JsBatch batch, int jsBase, TextEngineState status, string source) {
        lock (renderLock) {
            if (snapshot == null) return status == TextEngineState.Compiling ? LoadingText() : source;
            try {
                if (snapshot.Segments.Length == 0) return snapshot.Text;
                if (memoIdentity != snapshot.Identity) {
                    memoIdentity = snapshot.Identity;
                    memoReplacements = new string[snapshot.Segments.Length];
                    scratchReplacements = new string[snapshot.Segments.Length];
                    memoResult = null;
                }
                Evaluate(snapshot, scratchReplacements, batch, jsBase);
                bool same = memoResult != null;
                for (int i = 0; same && i < scratchReplacements.Length; i++)
                    same = string.Equals(scratchReplacements[i], memoReplacements[i], StringComparison.Ordinal);
                if (same) return memoResult;

                int needed = snapshot.Plan.OutputLength(scratchReplacements);
                if (charBuffer.Length < needed) charBuffer = new char[Math.Max(needed, charBuffer.Length * 2)];
                int written = snapshot.Plan.Compose(scratchReplacements, charBuffer, NativeRendering);
                (memoReplacements, scratchReplacements) = (scratchReplacements, memoReplacements);
                return memoResult = new string(charBuffer, 0, written);
            } finally { snapshot.Release(); }
        }
    }

    public string Get() {
        lock (renderLock) {
            var batch = localBatch ??= new JsBatch();
            batch.Clear();
            var snapshot = Begin(batch, out int jsBase, out var status, out string source);
            batch.Run();
            return End(snapshot, batch, jsBase, status, source);
        }
    }

    private static readonly object SharedBatchLock = new();
    private static readonly JsBatch SharedBatch = new();
    private static CompiledTextSnapshot[] sharedSnapshots = new CompiledTextSnapshot[16];
    private static int[] sharedBases = new int[16];
    private static TextEngineState[] sharedStates = new TextEngineState[16];
    private static string[] sharedSources = new string[16];

    /// <summary>
    /// Renders several engines with all of their JS tag calls in one V8 transition.
    /// Equivalent to calling <see cref="Get"/> on each engine in list order.
    /// </summary>
    public static void GetAll(IReadOnlyList<TextEngineCore> engines, string[] results) {
        int count = engines.Count;
        lock (SharedBatchLock) {
            if (sharedSnapshots.Length < count) {
                int size = Math.Max(count, sharedSnapshots.Length * 2);
                sharedSnapshots = new CompiledTextSnapshot[size];
                sharedBases = new int[size];
                sharedStates = new TextEngineState[size];
                sharedSources = new string[size];
            }
            var batch = SharedBatch;
            batch.Clear();
            int begun = 0;
            try {
                for (; begun < count; begun++) {
                    sharedSnapshots[begun] = engines[begun].Begin(batch, out sharedBases[begun], out sharedStates[begun], out sharedSources[begun]);
                }
                batch.Run();
                for (int i = 0; i < count; i++) {
                    var snapshot = sharedSnapshots[i];
                    sharedSnapshots[i] = null;
                    results[i] = engines[i].End(snapshot, batch, sharedBases[i], sharedStates[i], sharedSources[i]);
                    sharedSources[i] = null;
                }
            } finally {
                for (int i = 0; i < begun; i++) {
                    sharedSnapshots[i]?.Release();
                    sharedSnapshots[i] = null;
                    sharedSources[i] = null;
                }
                batch.Clear();
            }
        }
    }

    public int RenderTo(Span<char> destination) {
        lock (renderLock) {
            var snapshot = Acquire(out var status, out string source);
            if (snapshot == null) {
                string fallback = status == TextEngineState.Compiling ? LoadingText() : source;
                int length = Math.Min(fallback.Length, destination.Length);
                fallback.AsSpan(0, length).CopyTo(destination);
                return length;
            }
            try {
                if (snapshot.Segments.Length == 0) {
                    int length = Math.Min(snapshot.Text.Length, destination.Length);
                    snapshot.Text.AsSpan(0, length).CopyTo(destination);
                    return length;
                }
                var batch = localBatch ??= new JsBatch();
                batch.Clear();
                foreach (var call in snapshot.JsCalls) batch.Add(call);
                batch.Run();
                var replacements = ArrayPool<string>.Shared.Rent(snapshot.Segments.Length);
                try {
                    var values = replacements.AsSpan(0, snapshot.Segments.Length);
                    Evaluate(snapshot, values, batch, 0);
                    return snapshot.Plan.Compose(values, destination, NativeRendering);
                } finally { ArrayPool<string>.Shared.Return(replacements, clearArray: true); }
            } finally { snapshot.Release(); }
        }
    }

    private JsBatch localBatch;
    private char[] charBuffer = new char[512];
    private object memoIdentity;
    private string[] memoReplacements;
    private string[] scratchReplacements;
    private string memoResult;
    private static readonly long FrameIntervalTicks = TimeSpan.FromMilliseconds(80).Ticks;
    private static readonly string[] LoadingFrames = [".", "..", "..."];

    private static string LoadingText() => $"[ {MainCore.Tr.Get("COMPILING", "Compiling")}{LoadingFrames[(int)(DateTime.Now.Ticks / FrameIntervalTicks % LoadingFrames.Length)]} ]";

    public void Dispose() {
        CompiledTextSnapshot previous;
        lock (_lock) {
            if (disposed) return;
            disposed = true;
            compileGeneration++;
            previous = current;
            current = null;
            state = TextEngineState.Idle;
        }
        previous?.Release();
        // Don't take renderLock here: Dispose may be called from JS or while a
        // render is blocked inside a host callback. Leases own outstanding work.
    }
}

public enum TextEngineState {
    Idle,
    Compiling,
    Ready,
    Error
}
