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

        int needed = text.Length;
        for (int i = 0; i < reps.Length; i++) {
            if (reps[i] != null) {
                needed += reps[i].Length;
            }
        }

        if (_charBuffer.Length < needed) {
            _charBuffer = new char[Math.Max(_charBuffer.Length * 2, needed + 64)];
        }

        int written = ComposeInternal(text, segs, reps, _charBuffer);

        scratchReps = memoSegs == segs ? memoReps : null;
        memoReps = reps;
        memoSegs = segs;
        memoText = text;
        return memoResult = new string(_charBuffer, 0, written);
    }

    public int RenderTo(Span<char> destination) {
        string text = Text ?? string.Empty;
        var segs = segments;
        if (segs == null || segs.Length == 0) {
            int len = Math.Min(text.Length, destination.Length);
            text.AsSpan(0, len).CopyTo(destination);
            return len;
        }

        var reps = new string[segs.Length];
        for (int i = 0; i < segs.Length; i++) {
            try {
                reps[i] = segs[i].Replacer.Get();
            } catch {
                reps[i] = null;
            }
        }

        return ComposeInternal(text, segs, reps, destination);
    }

    private static int ComposeInternal(string template, CompiledSegment[] segs, string[] reps, Span<char> outBuf) {
        int outIdx = 0;
        int last = 0;
        int tplLen = template.Length;

        for (int i = 0; i < segs.Length; i++) {
            var s = segs[i];
            int from = O5Kit.Core.MathCompat.Clamp(s.Index, 0, tplLen);
            if (from > last) {
                int staticLen = from - last;
                int copyLen = Math.Min(staticLen, outBuf.Length - outIdx);
                template.AsSpan(last, copyLen).CopyTo(outBuf[outIdx..]);
                outIdx += copyLen;
            }
            string replacement = reps[i];
            if (replacement == null) {
                int end = O5Kit.Core.MathCompat.Clamp(s.Index + s.Length, 0, tplLen);
                if (end > from) {
                    int staticLen = end - from;
                    int copyLen = Math.Min(staticLen, outBuf.Length - outIdx);
                    template.AsSpan(from, copyLen).CopyTo(outBuf[outIdx..]);
                    outIdx += copyLen;
                }
                last = Math.Max(last, end);
            } else {
                int copyLen = Math.Min(replacement.Length, outBuf.Length - outIdx);
                replacement.AsSpan(0, copyLen).CopyTo(outBuf[outIdx..]);
                outIdx += copyLen;
                last = Math.Max(last, O5Kit.Core.MathCompat.Clamp(s.Index + s.Length, 0, tplLen));
            }
        }

        if (last < tplLen && outIdx < outBuf.Length) {
            int staticLen = tplLen - last;
            int copyLen = Math.Min(staticLen, outBuf.Length - outIdx);
            template.AsSpan(last, copyLen).CopyTo(outBuf[outIdx..]);
            outIdx += copyLen;
        }

        return outIdx;
    }

    private char[] _charBuffer = new char[512];
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
