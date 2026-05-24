using Overlayer.Tag.Diagnostics;
using Overlayer.TextEngine.Parse;
using Overlayer.TextEngine.Runtime;
using System.Text;

namespace Overlayer.TextEngine.Core;

public sealed class TextEngineCore {
    public string Text {
        get;
        set {
            if(field == value) {
                return;
            }

            field = value;
            Compile();
        }
    }

    public CompiledSegment[] Segments;

    public CompileDiagnostic[] GetDiagnostics()
        => [.. Segments.SelectMany(s => s.Replacer.Compiled.Diagnostics)];

    private void Compile() {
        if(string.IsNullOrEmpty(Text)) {
            Segments = [];
            return;
        }

        var tags = Parser.Parse(Text);

        if(tags.Count == 0) {
            Segments = [];
            return;
        }

        Segments = new CompiledSegment[tags.Count];

        for(int i = 0; i < tags.Count; i++) {
            var t = tags[i];

            Segments[i] = new CompiledSegment(
                t.Index,
                t.Length,
                new Tag.Runtime.Replacer {
                    Parsed = t
                }
            );
        }
    }

    public string Get() {
        if(Segments == null || Segments.Length == 0) {
            return Text ?? string.Empty;
        }

        var sb = new StringBuilder(Text.Length);
        int last = 0;

        for(int i = 0; i < Segments.Length; i++) {
            var s = Segments[i];

            sb.Append(Text, last, s.Index - last);
            sb.Append(s.Replacer.Get());

            last = s.Index + s.Length;
        }

        sb.Append(Text, last, Text.Length - last);

        return sb.ToString();
    }
}