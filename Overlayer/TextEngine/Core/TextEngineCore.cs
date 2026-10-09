using Overlayer.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.Tag.Runtime;
using Overlayer.TextEngine.Parse;
using Overlayer.TextEngine.Runtime;
using System.Text;

namespace Overlayer.TextEngine.Core;

public sealed class TextEngineCore {
    private readonly object _lock = new();
    private long compileGeneration;

    private volatile CompiledSegment[] segments = [];
    private volatile TextEngineState state;
    private CompileDiagnostic? engineDiagnostic;

    public string Text {
        get;
        set {
            if (field == value) {
                return;
            }

            field = value;
            StartCompile();
        }
    } = string.Empty;

    public void ForceRecompile() => StartCompile();

    public CompiledSegment[] Segments => segments;

    public TextEngineState State => state;

    public CompileDiagnostic[] GetDiagnostics() {
        lock (_lock) {
            if (state == TextEngineState.Error) {
                return engineDiagnostic.HasValue ? [engineDiagnostic.Value] : [];
            }

            var segs = segments;
            return segs == null ? [] : [.. segs.SelectMany(s => s.Replacer.Compiled.Diagnostics)];
        }
    }

    private void StartCompile() {
        lock (_lock) {
            state = TextEngineState.Compiling;
            engineDiagnostic = null;

            long generation = ++compileGeneration;
            string snapshot = Text;
            _ = Task.Run(() => CompileInternal(snapshot, generation));
        }
    }

    private void CompileInternal(string snapshot, long generation) {
        try {
            var tags = Parser.Parse(snapshot);
            var newSegments = tags.Count > 0 ? new CompiledSegment[tags.Count] : [];

            for (int i = 0; i < tags.Count; i++) {
                var t = tags[i];
                newSegments[i] = new CompiledSegment(
                    t.Index,
                    t.Length,
                    new Replacer {
                        Parsed = t
                    }
                );
            }

            CompiledSegment[] oldSegments;
            lock (_lock) {
                if (generation != compileGeneration) {
                    foreach (var seg in newSegments) {
                        seg.Replacer.Dispose();
                    }

                    return;
                }

                oldSegments = segments;
                segments = newSegments;
                state = TextEngineState.Ready;
            }

            if (oldSegments != null) {
                foreach (var seg in oldSegments) {
                    seg.Replacer.Dispose();
                }
            }
        } catch (Exception e) {
            lock (_lock) {
                if (generation != compileGeneration) {
                    return;
                }

                state = TextEngineState.Error;

                var context = new DiagnosticContext(null, 0, snapshot.Length);
                engineDiagnostic = new CompileDiagnostic(
                    DiagnosticId.InternalError,
                    CompileSeverity.Error,
                    context,
                    [e]
                );
                segments = [];
            }
        }
    }

    public string Get() {
        string text = Text ?? string.Empty;

        if (state == TextEngineState.Compiling) {
            return $"[ {MainCore.Tr.Get("COMPILING", "Compiling")}{GetLoadingText()} ]";
        }

        var segs = segments;

        if (segs == null || segs.Length == 0) {
            return text;
        }

        var reps = memoSegs == segs && memoReps != null ? scratchReps ??= new string[segs.Length] : new string[segs.Length];
        bool same = memoSegs == segs && ReferenceEquals(memoText, text) && memoReps != null;
        for (int i = 0; i < segs.Length; i++) {
            string r;
            try {
                r = segs[i].Replacer.Get();
            } catch {
                r = null;
            }
            reps[i] = r;
            if (same && !ReferenceEquals(r, memoReps[i])) {
                same = false;
            }
        }
        if (same) {
            return memoResult;
        }

        var sb = new StringBuilder(text.Length);
        int last = 0;

        for (int i = 0; i < segs.Length; i++) {
            var s = segs[i];
            int from = Math.Clamp(s.Index, 0, text.Length);
            if (from > last) {
                sb.Append(text, last, from - last);
            }
            string replacement = reps[i];
            if (replacement == null) {
                int end = Math.Clamp(s.Index + s.Length, 0, text.Length);
                if (end > from) {
                    sb.Append(text, from, end - from);
                }
                last = Math.Max(last, end);
            } else {
                sb.Append(replacement);
                last = Math.Max(last, Math.Clamp(s.Index + s.Length, 0, text.Length));
            }
        }

        if (last < text.Length) {
            sb.Append(text, last, text.Length - last);
        }

        // Swap buffers: reps becomes the memo, old memo becomes next scratch.
        scratchReps = memoSegs == segs ? memoReps : null;
        memoReps = reps;
        memoSegs = segs;
        memoText = text;
        return memoResult = sb.ToString();
    }

    private CompiledSegment[] memoSegs;
    private string[] memoReps;
    private string[] scratchReps;
    private string memoText;
    private string memoResult;

    private static readonly long FrameIntervalTicks = TimeSpan.FromMilliseconds(80).Ticks;
    private static readonly string[] LoadingFrames = [".", "..", "..."];

    private string GetLoadingText() {
        int index = (int)(DateTime.Now.Ticks / FrameIntervalTicks % LoadingFrames.Length);

        return LoadingFrames[index];
    }

    public void Dispose() {
        lock (_lock) {
            if (segments != null) {
                foreach (var seg in segments) {
                    seg.Replacer.Dispose();
                }
                segments = null;
            }
        }
    }
}

public enum TextEngineState {
    Idle,
    Compiling,
    Ready,
    Error
}
