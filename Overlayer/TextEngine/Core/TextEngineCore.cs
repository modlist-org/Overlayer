using Overlayer.Core;
using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;
using Overlayer.TextEngine.Runtime;
using System.Text;

namespace Overlayer.TextEngine.Core;

public sealed class TextEngineCore {
    private readonly object _lock = new();
    private Task _compileTask;

    private volatile CompiledSegment[] segments;
    private volatile TextEngineState state;

    public string Text {
        get;
        set {
            if(field == value) {
                return;
            }

            field = value;
            StartCompile();
        }
    }

    public void ForceRecompile() => StartCompile();

    public CompiledSegment[] Segments => segments;

    public TextEngineState State => state;

    public CompileDiagnostic[] GetDiagnostics() {
        var segs = segments;
        if(segs == null) {
            return [];
        }

        return [.. segs.SelectMany(s => s.Replacer.Compiled.Diagnostics)];
    }

    private void StartCompile() {
        lock(_lock) {
            state = TextEngineState.Compiling;

            _compileTask = Task.Run(CompileInternal);
        }
    }

    private async void CompileInternal() {
        try {
            await Task.Delay(500);
            var tags = Parser.Parse(Text);
            var newSegments = tags.Count > 0 ? new CompiledSegment[tags.Count] : [];

            for(int i = 0; i < tags.Count; i++) {
                var t = tags[i];
                newSegments[i] = new CompiledSegment(
                    t.Index,
                    t.Length,
                    new Tag.Runtime.Replacer {
                        Parsed = t
                    }
                );
            }

            CompiledSegment[] oldSegments;
            lock(_lock) {
                oldSegments = segments;
                segments = newSegments;
                state = TextEngineState.Ready;
            }

            if(oldSegments != null) {
                foreach(var seg in oldSegments) {
                    seg.Replacer.Dispose();
                }
            }
        } catch {
            state = TextEngineState.Ready;
            throw;
        }
    }

    public string Get() {
        if(state == TextEngineState.Compiling) {
            return $"[ {MainCore.Tr.Get("COMPILING", "Compiling")}{GetLoadingText()} ]";
        }

        var segs = segments;

        if(segs == null || segs.Length == 0) {
            return Text ?? string.Empty;
        }

        var sb = new StringBuilder(Text.Length);
        int last = 0;

        foreach(var s in segs) {
            sb.Append(Text, last, s.Index - last);
            sb.Append(s.Replacer.Get());

            last = s.Index + s.Length;
        }

        sb.Append(Text, last, Text.Length - last);

        return sb.ToString();
    }

    private static readonly long FrameIntervalTicks = TimeSpan.FromMilliseconds(80).Ticks;
    private static readonly string[] LoadingFrames = [".", "..", "..."];

    private string GetLoadingText() {
        int index = (int)(DateTime.Now.Ticks / FrameIntervalTicks % LoadingFrames.Length);

        return LoadingFrames[index];
    }

    public void Dispose() {
        lock(_lock) {
            if(segments != null) {
                foreach(var seg in segments) {
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
    Ready
}