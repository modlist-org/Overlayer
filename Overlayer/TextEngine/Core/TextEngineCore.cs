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

    private int spinner;

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
            spinner = 0;

            _compileTask = Task.Run(CompileInternal);
        }
    }

    private void CompileInternal() {
        try {
            if(segments != null) {
                foreach(var seg in segments) {
                    seg.Replacer.Dispose();
                }
            }

            if(string.IsNullOrEmpty(Text)) {
                segments = [];
                state = TextEngineState.Ready;
                return;
            }

            var tags = Parser.Parse(Text);

            if(tags.Count == 0) {
                segments = [];
                state = TextEngineState.Ready;
                return;
            }

            var result = new CompiledSegment[tags.Count];

            for(int i = 0; i < tags.Count; i++) {
                var t = tags[i];

                result[i] = new CompiledSegment(
                    t.Index,
                    t.Length,
                    new Tag.Runtime.Replacer {
                        Parsed = t
                    }
                );
            }

            segments = result;
            state = TextEngineState.Ready;
        } catch {
            state = TextEngineState.Ready;
            throw;
        }
    }

    public string Get() {
        if(state == TextEngineState.Compiling) {
            return $"{MainCore.Tr.Get("COMPILING", "Compiling")} {GetSpinner()}";
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

    private char GetSpinner() {
        char[] frames = ['|', '/', '-', '\\'];
        return frames[spinner++ % frames.Length];
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