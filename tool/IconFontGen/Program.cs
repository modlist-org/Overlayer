using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace IconFontGen;

/// <summary>
/// Pure-BCL deterministic SVG-to-TrueType icon font compiler.
/// Compiles an icons.json manifest and its referenced SVG files into a
/// single TrueType (.ttf) icon font consumable by Unity TextMeshPro.
/// </summary>
public static class Program {
    public static int Main(string[] args) {
        if (args.Length != 2) {
            Console.Error.WriteLine("Usage: IconFontGen <manifest.json> <output.ttf>");
            return 1;
        }

        string manifestPath = Path.GetFullPath(args[0]);
        string outputPath = Path.GetFullPath(args[1]);
        string baseDir = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Invalid manifest path: {manifestPath}");

        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var manifest = JsonSerializer.Deserialize<IconManifest>(File.ReadAllText(manifestPath, Encoding.UTF8), options)
            ?? throw new InvalidOperationException($"Failed to deserialize manifest: {manifestPath}");

        if (manifest.Glyphs is null || manifest.Glyphs.Count == 0) {
            throw new InvalidOperationException("Manifest contains no glyphs.");
        }

        var compiler = new SvgToTrueTypeCompiler(manifest, baseDir);
        byte[] ttfBytes = compiler.Build();

        string? outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir)) {
            Directory.CreateDirectory(outDir);
        }
        File.WriteAllBytes(outputPath, ttfBytes);
        Console.WriteLine($"IconFontGen: wrote {outputPath} ({manifest.Glyphs.Count} glyphs, {ttfBytes.Length} bytes)");
        return 0;
    }
}

internal sealed class IconManifest {
    public string FontName { get; set; } = "Icons";
    public int UnitsPerEm { get; set; } = 1024;
    public int Ascender { get; set; } = 896;
    public int Descender { get; set; } = -128;
    public double SourceSize { get; set; } = 128.0;
    public List<GlyphEntry> Glyphs { get; set; } = [];
}

internal sealed class GlyphEntry {
    public string Name { get; set; } = string.Empty;
    public string Codepoint { get; set; } = string.Empty;
    public string Svg { get; set; } = string.Empty;

    [JsonIgnore]
    public ushort ParsedCodepoint => ParseCodepoint(Codepoint);

    private static ushort ParseCodepoint(string raw) {
        string s = raw.Trim();
        if (s.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            return ushort.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return ushort.Parse(s, CultureInfo.InvariantCulture);
    }
}

internal readonly record struct Vec2(double X, double Y) {
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

internal readonly record struct AffineMatrix(double A, double B, double C, double D, double E, double F) {
    public static readonly AffineMatrix Identity = new(1, 0, 0, 1, 0, 0);

    public Vec2 Apply(Vec2 p) => new(A * p.X + C * p.Y + E, B * p.X + D * p.Y + F);

    public static AffineMatrix Multiply(AffineMatrix m1, AffineMatrix m2) => new(
        m1.A * m2.A + m1.C * m2.B,
        m1.B * m2.A + m1.D * m2.B,
        m1.A * m2.C + m1.C * m2.D,
        m1.B * m2.C + m1.D * m2.D,
        m1.A * m2.E + m1.C * m2.F + m1.E,
        m1.B * m2.E + m1.D * m2.F + m1.F
    );

    public static AffineMatrix Translate(double tx, double ty) => new(1, 0, 0, 1, tx, ty);
    public static AffineMatrix Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);

    public static AffineMatrix Rotate(double deg, double cx = 0, double cy = 0) {
        double rad = deg * Math.PI / 180.0;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        var r = new AffineMatrix(cos, sin, -sin, cos, 0, 0);
        if (cx == 0 && cy == 0) return r;
        return Multiply(Translate(cx, cy), Multiply(r, Translate(-cx, -cy)));
    }
}

internal readonly record struct TtfPoint(short X, short Y, bool OnCurve);

internal sealed class TtfContour {
    public List<TtfPoint> Points { get; } = [];
    public short MinX { get; private set; }
    public short MaxX { get; private set; }
    public short MinY { get; private set; }
    public short MaxY { get; private set; }

    public void UpdateBounds() {
        short minX = short.MaxValue, maxX = short.MinValue, minY = short.MaxValue, maxY = short.MinValue;
        foreach (var pt in Points) {
            if (pt.X < minX) minX = pt.X;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.Y > maxY) maxY = pt.Y;
        }
        MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY;
    }

    public List<Vec2> FlattenPolygon() {
        var poly = new List<Vec2>(Points.Count * 2);
        int n = Points.Count;
        int i = 0;
        while (i < n) {
            var cur = Points[i];
            if (cur.OnCurve) {
                poly.Add(new Vec2(cur.X, cur.Y));
                var next = Points[(i + 1) % n];
                if (!next.OnCurve) {
                    var end = Points[(i + 2) % n];
                    SampleQuad(
                        new Vec2(cur.X, cur.Y),
                        new Vec2(next.X, next.Y),
                        new Vec2(end.X, end.Y),
                        poly
                    );
                    i += 2;
                    continue;
                }
            } else {
                poly.Add(new Vec2(cur.X, cur.Y));
            }
            i++;
        }
        return poly;
    }

    private static void SampleQuad(Vec2 p0, Vec2 p1, Vec2 p2, List<Vec2> poly) {
        const int steps = 6;
        for (int s = 1; s < steps; s++) {
            double t = (double)s / steps;
            double mt = 1.0 - t;
            poly.Add(new Vec2(
                mt * mt * p0.X + 2.0 * mt * t * p1.X + t * t * p2.X,
                mt * mt * p0.Y + 2.0 * mt * t * p1.Y + t * t * p2.Y
            ));
        }
    }
}

internal sealed class CompiledGlyph {
    public string Name { get; init; } = string.Empty;
    public ushort Codepoint { get; init; }
    public List<TtfContour> Contours { get; init; } = [];
    public ushort AdvanceWidth { get; init; }
    public short LeftSideBearing { get; init; }
    public short XMin { get; init; }
    public short YMin { get; init; }
    public short XMax { get; init; }
    public short YMax { get; init; }
    public int TotalPoints => Contours.Sum(c => c.Points.Count);
}

internal sealed class SvgToTrueTypeCompiler {
    private readonly IconManifest _manifest;
    private readonly string _baseDir;

    public SvgToTrueTypeCompiler(IconManifest manifest, string baseDir) {
        _manifest = manifest;
        _baseDir = baseDir;
    }

    public byte[] Build() {
        var glyphs = new List<CompiledGlyph>(_manifest.Glyphs.Count + 1) {
            // GID 0: empty .notdef with full EM advance width
            new CompiledGlyph {
                Name = ".notdef",
                Codepoint = 0,
                Contours = [],
                AdvanceWidth = (ushort)_manifest.UnitsPerEm,
                LeftSideBearing = 0,
                XMin = 0, YMin = 0, XMax = 0, YMax = 0,
            }
        };

        var orderedEntries = _manifest.Glyphs.OrderBy(g => g.ParsedCodepoint).ToList();
        foreach (var entry in orderedEntries) {
            string svgPath = Path.Combine(_baseDir, entry.Svg);
            var doc = XDocument.Load(svgPath);
            var root = doc.Root ?? throw new InvalidOperationException($"Empty SVG: {svgPath}");
            var contours = ExtractContours(root);
            NormalizeContourWinding(contours);

            short xMin = 0, yMin = 0, xMax = 0, yMax = 0;
            if (contours.Count > 0) {
                xMin = contours.Min(c => c.MinX);
                yMin = contours.Min(c => c.MinY);
                xMax = contours.Max(c => c.MaxX);
                yMax = contours.Max(c => c.MaxY);
            }

            glyphs.Add(new CompiledGlyph {
                Name = entry.Name,
                Codepoint = entry.ParsedCodepoint,
                Contours = contours,
                AdvanceWidth = (ushort)_manifest.UnitsPerEm,
                LeftSideBearing = contours.Count > 0 ? xMin : (short)0,
                XMin = xMin,
                YMin = yMin,
                XMax = xMax,
                YMax = yMax,
            });
        }

        return AssembleTrueType(glyphs);
    }

    private List<TtfContour> ExtractContours(XElement root) {
        double scale = _manifest.UnitsPerEm / _manifest.SourceSize;
        double asc = _manifest.Ascender;

        Vec2 ToTtf(Vec2 svgPt) => new(svgPt.X * scale, asc - svgPt.Y * scale);

        var rawContours = new List<List<(Vec2 Pt, bool On)>>();

        void Walk(XElement el, AffineMatrix ctm) {
            string tag = el.Name.LocalName;
            if (tag is "defs" or "clipPath" or "mask") return;

            var localMat = ParseTransform((string?)el.Attribute("transform"));
            var nextMat = AffineMatrix.Multiply(ctm, localMat);

            if (tag is "svg" or "g") {
                foreach (var child in el.Elements()) {
                    Walk(child, nextMat);
                }
                return;
            }

            string? fill = ((string?)el.Attribute("fill"))?.Trim().ToLowerInvariant();
            if (fill == "none") return;

            switch (tag) {
                case "path": {
                    string? d = (string?)el.Attribute("d");
                    if (!string.IsNullOrEmpty(d)) {
                        rawContours.AddRange(ParseSvgPathData(d, nextMat, ToTtf));
                    }
                    break;
                }
                case "rect": {
                    double x = ParseAttr(el, "x", 0);
                    double y = ParseAttr(el, "y", 0);
                    double w = ParseAttr(el, "width", 0);
                    double h = ParseAttr(el, "height", 0);
                    string d = BuildRectPath(x, y, w, h, ParseOptAttr(el, "rx"), ParseOptAttr(el, "ry"));
                    rawContours.AddRange(ParseSvgPathData(d, nextMat, ToTtf));
                    break;
                }
                case "circle": {
                    double cx = ParseAttr(el, "cx", 0);
                    double cy = ParseAttr(el, "cy", 0);
                    double r = ParseAttr(el, "r", 0);
                    rawContours.AddRange(ParseSvgPathData(BuildEllipsePath(cx, cy, r, r), nextMat, ToTtf));
                    break;
                }
                case "ellipse": {
                    double cx = ParseAttr(el, "cx", 0);
                    double cy = ParseAttr(el, "cy", 0);
                    double rx = ParseAttr(el, "rx", 0);
                    double ry = ParseAttr(el, "ry", 0);
                    rawContours.AddRange(ParseSvgPathData(BuildEllipsePath(cx, cy, rx, ry), nextMat, ToTtf));
                    break;
                }
                case "polygon" or "polyline": {
                    var pts = ExtractNumbers((string?)el.Attribute("points") ?? "");
                    if (pts.Count >= 4) {
                        string d = $"M{pts[0].ToString(CultureInfo.InvariantCulture)} {pts[1].ToString(CultureInfo.InvariantCulture)}";
                        for (int i = 2; i + 1 < pts.Count; i += 2) {
                            d += $" L{pts[i].ToString(CultureInfo.InvariantCulture)} {pts[i + 1].ToString(CultureInfo.InvariantCulture)}";
                        }
                        if (tag == "polygon") d += " Z";
                        rawContours.AddRange(ParseSvgPathData(d, nextMat, ToTtf));
                    }
                    break;
                }
            }
        }

        Walk(root, AffineMatrix.Identity);

        var finalized = new List<TtfContour>(rawContours.Count);
        foreach (var raw in rawContours) {
            var contour = QuantizeAndCleanContour(raw);
            if (contour != null) {
                finalized.Add(contour);
            }
        }
        return finalized;
    }

    private static TtfContour? QuantizeAndCleanContour(List<(Vec2 Pt, bool On)> raw) {
        if (raw.Count < 3) return null;

        var pts = new List<TtfPoint>(raw.Count);
        foreach (var (pt, on) in raw) {
            var q = new TtfPoint((short)Math.Round(pt.X), (short)Math.Round(pt.Y), on);
            if (pts.Count > 0 && pts[^1].X == q.X && pts[^1].Y == q.Y) {
                if (pts[^1].OnCurve != q.OnCurve) {
                    pts[^1] = q with { OnCurve = true };
                }
                continue;
            }
            pts.Add(q);
        }

        if (pts.Count > 1 && pts[^1].X == pts[0].X && pts[^1].Y == pts[0].Y && pts[^1].OnCurve && pts[0].OnCurve) {
            pts.RemoveAt(pts.Count - 1);
        }

        for (int i = pts.Count - 1; i >= 0 && pts.Count >= 3; i--) {
            if (pts[i].OnCurve) continue;
            var prev = pts[(i - 1 + pts.Count) % pts.Count];
            var next = pts[(i + 1) % pts.Count];
            if ((pts[i].X == prev.X && pts[i].Y == prev.Y) || (pts[i].X == next.X && pts[i].Y == next.Y)) {
                pts.RemoveAt(i);
            }
        }

        if (pts.Count < 3) return null;

        var contour = new TtfContour();
        contour.Points.AddRange(pts);
        contour.UpdateBounds();

        var poly = contour.FlattenPolygon();
        if (Math.Abs(SignedArea(poly)) < 1.0) return null;
        return contour;
    }

    private static void NormalizeContourWinding(List<TtfContour> contours) {
        if (contours.Count == 0) return;

        var polys = new List<List<Vec2>>(contours.Count);
        var areas = new double[contours.Count];
        var testPts = new Vec2[contours.Count];

        for (int i = 0; i < contours.Count; i++) {
            var poly = contours[i].FlattenPolygon();
            polys.Add(poly);
            areas[i] = SignedArea(poly);
            testPts[i] = FindInteriorPoint(poly, contours[i]);
        }

        for (int i = 0; i < contours.Count; i++) {
            int enclosing = 0;
            Vec2 pt = testPts[i];
            for (int j = 0; j < contours.Count; j++) {
                if (i == j || Math.Abs(areas[j]) <= Math.Abs(areas[i])) continue;
                var cj = contours[j];
                if (pt.X < cj.MinX || pt.X > cj.MaxX || pt.Y < cj.MinY || pt.Y > cj.MaxY) continue;
                if (PointInPolygon(pt, polys[j])) enclosing++;
            }

            bool isHole = (enclosing & 1) == 1;
            bool wantNegative = !isHole;
            bool isNegative = areas[i] < 0;
            if (wantNegative != isNegative) {
                contours[i].Points.Reverse();
            }
            EnsureStartsOnCurve(contours[i].Points);
        }
    }

    private static void EnsureStartsOnCurve(List<TtfPoint> pts) {
        int onIdx = pts.FindIndex(p => p.OnCurve);
        if (onIdx <= 0) return;
        var rotated = new List<TtfPoint>(pts.Count);
        rotated.AddRange(pts.GetRange(onIdx, pts.Count - onIdx));
        rotated.AddRange(pts.GetRange(0, onIdx));
        pts.Clear();
        pts.AddRange(rotated);
    }

    private static double SignedArea(IReadOnlyList<Vec2> poly) {
        double sum = 0;
        int n = poly.Count;
        for (int i = 0; i < n; i++) {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum * 0.5;
    }

    private static Vec2 FindInteriorPoint(IReadOnlyList<Vec2> poly, TtfContour bounds) {
        double[] fracs = [0.5, 0.37, 0.63, 0.21, 0.79, 0.13, 0.87];
        var xs = new List<double>();

        foreach (double f in fracs) {
            double y = bounds.MinY + (bounds.MaxY - bounds.MinY) * f;
            xs.Clear();
            int n = poly.Count;
            for (int i = 0; i < n; i++) {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y)) {
                    double t = (y - a.Y) / (b.Y - a.Y);
                    xs.Add(a.X + t * (b.X - a.X));
                }
            }
            if (xs.Count < 2) continue;
            xs.Sort();
            double bestSpan = 0;
            double bestX = xs[0];
            for (int k = 0; k + 1 < xs.Count; k += 2) {
                double span = xs[k + 1] - xs[k];
                if (span > bestSpan) {
                    bestSpan = span;
                    bestX = 0.5 * (xs[k] + xs[k + 1]);
                }
            }
            if (bestSpan > 0.01) {
                return new Vec2(bestX, y);
            }
        }

        return new Vec2(0.5 * (bounds.MinX + bounds.MaxX), 0.5 * (bounds.MinY + bounds.MaxY));
    }

    private static bool PointInPolygon(Vec2 pt, IReadOnlyList<Vec2> poly) {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++) {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > pt.Y) != (b.Y > pt.Y) &&
                pt.X < (b.X - a.X) * (pt.Y - a.Y) / (b.Y - a.Y) + a.X) {
                inside = !inside;
            }
        }
        return inside;
    }

    // --- SVG parsing & curve conversion ---

    private static readonly Regex PathTokenRegex = new(
        @"[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?",
        RegexOptions.Compiled
    );

    private static readonly Regex NumberRegex = new(
        @"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?",
        RegexOptions.Compiled
    );

    private static readonly Regex TransformFuncRegex = new(
        @"([a-zA-Z]+)\s*\(([^)]*)\)",
        RegexOptions.Compiled
    );

    private static AffineMatrix ParseTransform(string? spec) {
        var mat = AffineMatrix.Identity;
        if (string.IsNullOrWhiteSpace(spec)) return mat;
        foreach (Match m in TransformFuncRegex.Matches(spec)) {
            string fn = m.Groups[1].Value;
            var nums = ExtractNumbers(m.Groups[2].Value);
            AffineMatrix step = fn switch {
                "translate" => AffineMatrix.Translate(nums.Count > 0 ? nums[0] : 0, nums.Count > 1 ? nums[1] : 0),
                "scale" => AffineMatrix.Scale(nums.Count > 0 ? nums[0] : 1, nums.Count > 1 ? nums[1] : (nums.Count > 0 ? nums[0] : 1)),
                "rotate" => nums.Count >= 3 ? AffineMatrix.Rotate(nums[0], nums[1], nums[2]) : AffineMatrix.Rotate(nums[0]),
                "matrix" when nums.Count == 6 => new AffineMatrix(nums[0], nums[1], nums[2], nums[3], nums[4], nums[5]),
                "skewX" => new AffineMatrix(1, 0, Math.Tan(nums[0] * Math.PI / 180.0), 1, 0, 0),
                "skewY" => new AffineMatrix(1, Math.Tan(nums[0] * Math.PI / 180.0), 0, 1, 0, 0),
                _ => throw new NotSupportedException($"Unsupported SVG transform: {fn}")
            };
            mat = AffineMatrix.Multiply(mat, step);
        }
        return mat;
    }

    private static List<double> ExtractNumbers(string raw) {
        var matches = NumberRegex.Matches(raw);
        var list = new List<double>(matches.Count);
        foreach (Match m in matches) {
            list.Add(double.Parse(m.Value, CultureInfo.InvariantCulture));
        }
        return list;
    }

    private static double ParseAttr(XElement el, string attr, double defVal) {
        string? s = (string?)el.Attribute(attr);
        return s != null ? double.Parse(s, CultureInfo.InvariantCulture) : defVal;
    }

    private static double? ParseOptAttr(XElement el, string attr) {
        string? s = (string?)el.Attribute(attr);
        return s != null ? double.Parse(s, CultureInfo.InvariantCulture) : null;
    }

    private static string BuildRectPath(double x, double y, double w, double h, double? rxOpt, double? ryOpt) {
        double rx = rxOpt ?? ryOpt ?? 0;
        double ry = ryOpt ?? rxOpt ?? 0;
        rx = Math.Min(Math.Max(rx, 0), w * 0.5);
        ry = Math.Min(Math.Max(ry, 0), h * 0.5);
        if (rx <= 0 && ry <= 0) {
            return string.Create(CultureInfo.InvariantCulture, $"M{x} {y} H{x + w} V{y + h} H{x} Z");
        }
        const double k = 0.5522847498307936;
        double kx = rx * k, ky = ry * k;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M{x + rx} {y} H{x + w - rx} " +
            $"C{x + w - rx + kx} {y} {x + w} {y + ry - ky} {x + w} {y + ry} " +
            $"V{y + h - ry} " +
            $"C{x + w} {y + h - ry + ky} {x + w - rx + kx} {y + h} {x + w - rx} {y + h} " +
            $"H{x + rx} " +
            $"C{x + rx - kx} {y + h} {x} {y + h - ry + ky} {x} {y + h - ry} " +
            $"V{y + ry} " +
            $"C{x} {y + ry - ky} {x + rx - kx} {y} {x + rx} {y} Z"
        );
    }

    private static string BuildEllipsePath(double cx, double cy, double rx, double ry) {
        const double k = 0.5522847498307936;
        double kx = rx * k, ky = ry * k;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"M{cx + rx} {cy} " +
            $"C{cx + rx} {cy + ky} {cx + kx} {cy + ry} {cx} {cy + ry} " +
            $"C{cx - kx} {cy + ry} {cx - rx} {cy + ky} {cx - rx} {cy} " +
            $"C{cx - rx} {cy - ky} {cx - kx} {cy - ry} {cx} {cy - ry} " +
            $"C{cx + kx} {cy - ry} {cx + rx} {cy - ky} {cx + rx} {cy} Z"
        );
    }

    private static List<List<(Vec2 Pt, bool On)>> ParseSvgPathData(
        string d,
        AffineMatrix ctm,
        Func<Vec2, Vec2> toTtf
    ) {
        var matches = PathTokenRegex.Matches(d);
        var tokens = new string[matches.Count];
        for (int k = 0; k < matches.Count; k++) tokens[k] = matches[k].Value;

        var contours = new List<List<(Vec2 Pt, bool On)>>();
        List<(Vec2 Pt, bool On)>? cur = null;
        int i = 0;
        char cmd = '\0', prevCmd = '\0';
        double cx = 0, cy = 0, sx = 0, sy = 0, pc2x = 0, pc2y = 0, pqx = 0, pqy = 0;

        Vec2 MapPt(double x, double y) => toTtf(ctm.Apply(new Vec2(x, y)));
        double NextF() => double.Parse(tokens[i++], CultureInfo.InvariantCulture);
        bool IsNum() => i < tokens.Length && !char.IsLetter(tokens[i][0]);

        void EnsureContour() {
            if (cur == null) {
                cur = [];
                contours.Add(cur);
            }
        }

        void EmitCubic(double x1, double y1, double x2, double y2, double x3, double y3) {
            EnsureContour();
            Vec2 p0 = MapPt(cx, cy);
            Vec2 p1 = MapPt(x1, y1);
            Vec2 p2 = MapPt(x2, y2);
            Vec2 p3 = MapPt(x3, y3);
            SubdivideCubicToQuads(p0, p1, p2, p3, cur!, 0);
        }

        while (i < tokens.Length) {
            if (char.IsLetter(tokens[i][0])) {
                cmd = tokens[i++][0];
                if (cmd is 'Z' or 'z') {
                    cx = sx; cy = sy;
                    cur = null;
                    prevCmd = cmd;
                    continue;
                }
            }

            switch (cmd) {
                case 'M' or 'm': {
                    double x = NextF(), y = NextF();
                    if (cmd == 'm') { x += cx; y += cy; }
                    cur = [new(MapPt(x, y), true)];
                    contours.Add(cur);
                    cx = sx = x; cy = sy = y;
                    prevCmd = cmd;
                    cmd = cmd == 'M' ? 'L' : 'l';
                    while (IsNum()) {
                        double lx = NextF(), ly = NextF();
                        if (cmd == 'l') { lx += cx; ly += cy; }
                        cur.Add((MapPt(lx, ly), true));
                        cx = lx; cy = ly;
                        prevCmd = cmd;
                    }
                    break;
                }
                case 'L' or 'l': {
                    while (IsNum()) {
                        double x = NextF(), y = NextF();
                        if (cmd == 'l') { x += cx; y += cy; }
                        EnsureContour();
                        cur!.Add((MapPt(x, y), true));
                        cx = x; cy = y; prevCmd = cmd;
                    }
                    break;
                }
                case 'H' or 'h': {
                    while (IsNum()) {
                        double x = NextF() + (cmd == 'h' ? cx : 0);
                        EnsureContour();
                        cur!.Add((MapPt(x, cy), true));
                        cx = x; prevCmd = cmd;
                    }
                    break;
                }
                case 'V' or 'v': {
                    while (IsNum()) {
                        double y = NextF() + (cmd == 'v' ? cy : 0);
                        EnsureContour();
                        cur!.Add((MapPt(cx, y), true));
                        cy = y; prevCmd = cmd;
                    }
                    break;
                }
                case 'C' or 'c': {
                    while (IsNum()) {
                        double x1 = NextF(), y1 = NextF(), x2 = NextF(), y2 = NextF(), x = NextF(), y = NextF();
                        if (cmd == 'c') { x1 += cx; y1 += cy; x2 += cx; y2 += cy; x += cx; y += cy; }
                        EmitCubic(x1, y1, x2, y2, x, y);
                        pc2x = x2; pc2y = y2; cx = x; cy = y; prevCmd = cmd;
                    }
                    break;
                }
                case 'S' or 's': {
                    while (IsNum()) {
                        double x1 = prevCmd is 'C' or 'c' or 'S' or 's' ? 2 * cx - pc2x : cx;
                        double y1 = prevCmd is 'C' or 'c' or 'S' or 's' ? 2 * cy - pc2y : cy;
                        double x2 = NextF(), y2 = NextF(), x = NextF(), y = NextF();
                        if (cmd == 's') { x2 += cx; y2 += cy; x += cx; y += cy; }
                        EmitCubic(x1, y1, x2, y2, x, y);
                        pc2x = x2; pc2y = y2; cx = x; cy = y; prevCmd = cmd;
                    }
                    break;
                }
                case 'Q' or 'q': {
                    while (IsNum()) {
                        double qx = NextF(), qy = NextF(), x = NextF(), y = NextF();
                        if (cmd == 'q') { qx += cx; qy += cy; x += cx; y += cy; }
                        EnsureContour();
                        cur!.Add((MapPt(qx, qy), false));
                        cur.Add((MapPt(x, y), true));
                        pqx = qx; pqy = qy; cx = x; cy = y; prevCmd = cmd;
                    }
                    break;
                }
                case 'T' or 't': {
                    while (IsNum()) {
                        double qx = prevCmd is 'Q' or 'q' or 'T' or 't' ? 2 * cx - pqx : cx;
                        double qy = prevCmd is 'Q' or 'q' or 'T' or 't' ? 2 * cy - pqy : cy;
                        double x = NextF(), y = NextF();
                        if (cmd == 't') { x += cx; y += cy; }
                        EnsureContour();
                        cur!.Add((MapPt(qx, qy), false));
                        cur.Add((MapPt(x, y), true));
                        pqx = qx; pqy = qy; cx = x; cy = y; prevCmd = cmd;
                    }
                    break;
                }
                case 'A' or 'a': {
                    while (IsNum()) {
                        double rx = NextF(), ry = NextF(), rot = NextF();
                        bool largeArc = NextF() != 0;
                        bool sweep = NextF() != 0;
                        double x = NextF(), y = NextF();
                        if (cmd == 'a') { x += cx; y += cy; }
                        foreach (var seg in ArcToCubics(cx, cy, rx, ry, rot, largeArc, sweep, x, y)) {
                            EmitCubic(seg.X1, seg.Y1, seg.X2, seg.Y2, seg.X3, seg.Y3);
                            cx = seg.X3; cy = seg.Y3;
                        }
                        cx = x; cy = y; prevCmd = cmd;
                    }
                    break;
                }
                default:
                    throw new NotSupportedException($"Unsupported SVG path command: {cmd}");
            }
        }

        return contours;
    }

    private static void SubdivideCubicToQuads(
        Vec2 p0, Vec2 p1, Vec2 p2, Vec2 p3,
        List<(Vec2 Pt, bool On)> outPts,
        int depth
    ) {
        double errX = p3.X - 3 * p2.X + 3 * p1.X - p0.X;
        double errY = p3.Y - 3 * p2.Y + 3 * p1.Y - p0.Y;
        double err = (Math.Sqrt(3.0) / 36.0) * Math.Sqrt(errX * errX + errY * errY);
        if (err <= 0.25 || depth >= 8) {
            var q = (p1 * 3.0 + p2 * 3.0 - p0 - p3) * 0.25;
            outPts.Add((q, false));
            outPts.Add((p3, true));
            return;
        }

        Vec2 p01 = (p0 + p1) * 0.5;
        Vec2 p12 = (p1 + p2) * 0.5;
        Vec2 p23 = (p2 + p3) * 0.5;
        Vec2 p012 = (p01 + p12) * 0.5;
        Vec2 p123 = (p12 + p23) * 0.5;
        Vec2 mid = (p012 + p123) * 0.5;

        SubdivideCubicToQuads(p0, p01, p012, mid, outPts, depth + 1);
        SubdivideCubicToQuads(mid, p123, p23, p3, outPts, depth + 1);
    }

    private readonly record struct CubicSeg(double X1, double Y1, double X2, double Y2, double X3, double Y3);

    private static List<CubicSeg> ArcToCubics(
        double x0, double y0, double rx, double ry,
        double angleDeg, bool largeArc, bool sweep, double x, double y
    ) {
        var result = new List<CubicSeg>();
        if (x0 == x && y0 == y) return result;
        rx = Math.Abs(rx); ry = Math.Abs(ry);
        if (rx == 0 || ry == 0) {
            result.Add(new CubicSeg(x0, y0, x, y, x, y));
            return result;
        }

        double phi = angleDeg * Math.PI / 180.0;
        double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);
        double dx2 = (x0 - x) * 0.5, dy2 = (y0 - y) * 0.5;
        double x1p = cosPhi * dx2 + sinPhi * dy2;
        double y1p = -sinPhi * dx2 + cosPhi * dy2;

        double lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
        if (lambda > 1.0) {
            double s = Math.Sqrt(lambda);
            rx *= s; ry *= s;
        }

        double rx2 = rx * rx, ry2 = ry * ry, x1p2 = x1p * x1p, y1p2 = y1p * y1p;
        double num = Math.Max(0.0, rx2 * ry2 - rx2 * y1p2 - ry2 * x1p2);
        double den = rx2 * y1p2 + ry2 * x1p2;
        double coef = den == 0 ? 0 : Math.Sqrt(num / den) * (largeArc == sweep ? -1 : 1);
        double cxp = coef * (rx * y1p / ry);
        double cyp = coef * -(ry * x1p / rx);

        double cx = cosPhi * cxp - sinPhi * cyp + (x0 + x) * 0.5;
        double cy = sinPhi * cxp + cosPhi * cyp + (y0 + y) * 0.5;

        static double VecAngle(double ux, double uy, double vx, double vy) {
            double sign = (ux * vy - uy * vx) < 0 ? -1.0 : 1.0;
            double dot = (ux * vx + uy * vy) / (Math.Sqrt(ux * ux + uy * uy) * Math.Sqrt(vx * vx + vy * vy));
            return sign * Math.Acos(Math.Clamp(dot, -1.0, 1.0));
        }

        double theta1 = VecAngle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        double dtheta = VecAngle((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!sweep && dtheta > 0) dtheta -= 2 * Math.PI;
        if (sweep && dtheta < 0) dtheta += 2 * Math.PI;

        int segs = Math.Max(1, (int)Math.Ceiling(Math.Abs(dtheta) / (Math.PI * 0.5)));
        double delta = dtheta / segs;
        double alpha = (4.0 / 3.0) * Math.Tan(delta * 0.25);

        for (int s = 0; s < segs; s++) {
            double t1 = theta1 + s * delta;
            double t2 = theta1 + (s + 1) * delta;
            double cos1 = Math.Cos(t1), sin1 = Math.Sin(t1);
            double cos2 = Math.Cos(t2), sin2 = Math.Sin(t2);

            Vec2 MapUnit(double ux, double uy) => new(
                cosPhi * (rx * ux) - sinPhi * (ry * uy) + cx,
                sinPhi * (rx * ux) + cosPhi * (ry * uy) + cy
            );

            var c1 = MapUnit(cos1 - alpha * sin1, sin1 + alpha * cos1);
            var c2 = MapUnit(cos2 + alpha * sin2, sin2 - alpha * cos2);
            var end = s == segs - 1 ? new Vec2(x, y) : MapUnit(cos2, sin2);
            result.Add(new CubicSeg(c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y));
        }
        return result;
    }

    // --- TrueType binary assembly ---

    private byte[] AssembleTrueType(List<CompiledGlyph> glyphs) {
        short globalXMin = 0, globalYMin = 0, globalXMax = 0, globalYMax = 0;
        bool hasAny = false;
        short minLsb = short.MaxValue, minRsb = short.MaxValue;
        int maxExtent = 0, maxPoints = 0, maxContours = 0;

        foreach (var g in glyphs) {
            if (g.Contours.Count > 0) {
                if (!hasAny) {
                    globalXMin = g.XMin; globalYMin = g.YMin; globalXMax = g.XMax; globalYMax = g.YMax;
                    hasAny = true;
                } else {
                    globalXMin = Math.Min(globalXMin, g.XMin);
                    globalYMin = Math.Min(globalYMin, g.YMin);
                    globalXMax = Math.Max(globalXMax, g.XMax);
                    globalYMax = Math.Max(globalYMax, g.YMax);
                }
                short rsb = (short)(g.AdvanceWidth - g.XMax);
                minLsb = Math.Min(minLsb, g.LeftSideBearing);
                minRsb = Math.Min(minRsb, rsb);
                maxExtent = Math.Max(maxExtent, g.XMax);
                maxPoints = Math.Max(maxPoints, g.TotalPoints);
                maxContours = Math.Max(maxContours, g.Contours.Count);
            } else {
                minLsb = Math.Min(minLsb, (short)0);
                minRsb = Math.Min(minRsb, (short)g.AdvanceWidth);
            }
        }
        if (minLsb == short.MaxValue) minLsb = 0;
        if (minRsb == short.MaxValue) minRsb = (short)_manifest.UnitsPerEm;

        var (glyfBytes, locaBytes) = BuildGlyfAndLoca(glyphs);
        byte[] headBytes = BuildHead(globalXMin, globalYMin, globalXMax, globalYMax);
        byte[] hheaBytes = BuildHhea((ushort)glyphs.Count, minLsb, minRsb, (short)maxExtent);
        byte[] maxpBytes = BuildMaxp((ushort)glyphs.Count, (ushort)maxPoints, (ushort)maxContours);
        byte[] os2Bytes = BuildOs2(glyphs, globalYMin, globalYMax);
        byte[] hmtxBytes = BuildHmtx(glyphs);
        byte[] cmapBytes = BuildCmap(glyphs);
        byte[] nameBytes = BuildName();
        byte[] postBytes = BuildPost(glyphs);

        var tables = new (string Tag, byte[] Data)[] {
            ("OS/2", os2Bytes),
            ("cmap", cmapBytes),
            ("glyf", glyfBytes),
            ("head", headBytes),
            ("hhea", hheaBytes),
            ("hmtx", hmtxBytes),
            ("loca", locaBytes),
            ("maxp", maxpBytes),
            ("name", nameBytes),
            ("post", postBytes),
        };

        int numTables = tables.Length;
        int entrySelector = (int)Math.Floor(Math.Log2(numTables));
        int searchRange = (1 << entrySelector) * 16;
        int rangeShift = numTables * 16 - searchRange;

        int headerSize = 12 + numTables * 16;
        int totalSize = headerSize;
        var offsets = new int[numTables];
        for (int t = 0; t < numTables; t++) {
            offsets[t] = totalSize;
            totalSize += PaddedLength(tables[t].Data.Length);
        }

        byte[] sfnt = new byte[totalSize];
        BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(0, 4), 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(4, 2), (ushort)numTables);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(6, 2), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(8, 2), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(10, 2), (ushort)rangeShift);

        int headOffset = 0;
        for (int t = 0; t < numTables; t++) {
            var (tag, data) = tables[t];
            if (tag == "head") headOffset = offsets[t];

            int recOff = 12 + t * 16;
            Encoding.ASCII.GetBytes(tag, sfnt.AsSpan(recOff, 4));
            uint checksum = ComputeChecksum(data);
            BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(recOff + 4, 4), checksum);
            BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(recOff + 8, 4), (uint)offsets[t]);
            BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(recOff + 12, 4), (uint)data.Length);
            data.CopyTo(sfnt, offsets[t]);
        }

        uint wholeChecksum = ComputeChecksum(sfnt);
        uint checkSumAdjustment = unchecked(0xB1B0AFBAu - wholeChecksum);
        BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(headOffset + 8, 4), checkSumAdjustment);
        return sfnt;
    }

    private static (byte[] Glyf, byte[] Loca) BuildGlyfAndLoca(List<CompiledGlyph> glyphs) {
        using var glyfMs = new MemoryStream();
        byte[] locaBytes = new byte[(glyphs.Count + 1) * 4];

        for (int gi = 0; gi < glyphs.Count; gi++) {
            BinaryPrimitives.WriteUInt32BigEndian(locaBytes.AsSpan(gi * 4, 4), (uint)glyfMs.Length);
            var g = glyphs[gi];
            if (g.Contours.Count == 0) continue;

            using var gMs = new MemoryStream();
            var w = new BeWriter(gMs);
            w.I16((short)g.Contours.Count);
            w.I16(g.XMin);
            w.I16(g.YMin);
            w.I16(g.XMax);
            w.I16(g.YMax);

            int endPt = -1;
            foreach (var c in g.Contours) {
                endPt += c.Points.Count;
                w.U16((ushort)endPt);
            }
            w.U16(0);

            int totalPts = g.TotalPoints;
            var flags = new byte[totalPts];
            using var xMs = new MemoryStream();
            using var yMs = new MemoryStream();
            var xw = new BeWriter(xMs);
            var yw = new BeWriter(yMs);

            short prevX = 0, prevY = 0;
            int idx = 0;
            foreach (var c in g.Contours) {
                foreach (var pt in c.Points) {
                    byte f = pt.OnCurve ? (byte)0x01 : (byte)0x00;
                    int dx = pt.X - prevX;
                    int dy = pt.Y - prevY;
                    prevX = pt.X;
                    prevY = pt.Y;

                    if (dx == 0) {
                        f |= 0x10;
                    } else if (dx is >= -255 and <= 255) {
                        f |= 0x02;
                        if (dx > 0) f |= 0x10;
                        xw.U8((byte)Math.Abs(dx));
                    } else {
                        xw.I16((short)dx);
                    }

                    if (dy == 0) {
                        f |= 0x20;
                    } else if (dy is >= -255 and <= 255) {
                        f |= 0x04;
                        if (dy > 0) f |= 0x20;
                        yw.U8((byte)Math.Abs(dy));
                    } else {
                        yw.I16((short)dy);
                    }

                    flags[idx++] = f;
                }
            }

            gMs.Write(flags);
            xMs.WriteTo(gMs);
            yMs.WriteTo(gMs);
            while ((gMs.Length & 3) != 0) gMs.WriteByte(0);
            gMs.WriteTo(glyfMs);
        }

        BinaryPrimitives.WriteUInt32BigEndian(locaBytes.AsSpan(glyphs.Count * 4, 4), (uint)glyfMs.Length);
        return (glyfMs.ToArray(), locaBytes);
    }

    private byte[] BuildHead(short xMin, short yMin, short xMax, short yMax) {
        using var ms = new MemoryStream(54);
        var w = new BeWriter(ms);
        const long deterministicTimestamp = 3850000000L;
        w.U32(0x00010000);
        w.U32(0x00010000);
        w.U32(0);
        w.U32(0x5F0F3CF5);
        w.U16(0x0003);
        w.U16((ushort)_manifest.UnitsPerEm);
        w.I64(deterministicTimestamp);
        w.I64(deterministicTimestamp);
        w.I16(xMin); w.I16(yMin); w.I16(xMax); w.I16(yMax);
        w.U16(0);
        w.U16(8);
        w.I16(2);
        w.I16(1);
        w.I16(0);
        return ms.ToArray();
    }

    private byte[] BuildHhea(ushort numGlyphs, short minLsb, short minRsb, short xMaxExtent) {
        using var ms = new MemoryStream(36);
        var w = new BeWriter(ms);
        w.U32(0x00010000);
        w.I16((short)_manifest.Ascender);
        w.I16((short)_manifest.Descender);
        w.I16(0);
        w.U16((ushort)_manifest.UnitsPerEm);
        w.I16(minLsb);
        w.I16(minRsb);
        w.I16(xMaxExtent);
        w.I16(1); w.I16(0); w.I16(0);
        w.I16(0); w.I16(0); w.I16(0); w.I16(0);
        w.I16(0);
        w.U16(numGlyphs);
        return ms.ToArray();
    }

    private static byte[] BuildMaxp(ushort numGlyphs, ushort maxPoints, ushort maxContours) {
        using var ms = new MemoryStream(32);
        var w = new BeWriter(ms);
        w.U32(0x00010000);
        w.U16(numGlyphs);
        w.U16(maxPoints);
        w.U16(maxContours);
        w.U16(0);
        w.U16(0);
        w.U16(2);
        w.U16(0);
        w.U16(0);
        w.U16(0);
        w.U16(0);
        w.U16(0);
        w.U16(0);
        w.U16(0);
        w.U16(0);
        return ms.ToArray();
    }

    private byte[] BuildOs2(List<CompiledGlyph> glyphs, short globalYMin, short globalYMax) {
        ushort firstChar = glyphs.Count > 1 ? glyphs[1].Codepoint : (ushort)0;
        ushort lastChar = glyphs.Count > 1 ? glyphs[^1].Codepoint : (ushort)0;
        ushort winAscent = (ushort)Math.Max(_manifest.Ascender, (int)globalYMax);
        ushort winDescent = (ushort)Math.Max(-_manifest.Descender, -(int)globalYMin);

        using var ms = new MemoryStream(96);
        var w = new BeWriter(ms);
        w.U16(4);
        w.I16((short)_manifest.UnitsPerEm);
        w.U16(400);
        w.U16(5);
        w.U16(0);
        w.I16(650); w.I16(700); w.I16(0); w.I16(140);
        w.I16(650); w.I16(700); w.I16(0); w.I16(480);
        w.I16(50); w.I16(260);
        w.I16(0);
        ms.Write(new byte[] { 2, 0, 5, 9, 0, 0, 0, 0, 0, 0 });
        w.U32(0);
        w.U32(1u << 28);
        w.U32(0);
        w.U32(0);
        ms.Write("OVRL"u8);
        w.U16(0x00C0);
        w.U16(firstChar);
        w.U16(lastChar);
        w.I16((short)_manifest.Ascender);
        w.I16((short)_manifest.Descender);
        w.I16(0);
        w.U16(winAscent);
        w.U16(winDescent);
        w.U32(1u);
        w.U32(0u);
        w.I16((short)(_manifest.Ascender / 2));
        w.I16((short)(_manifest.Ascender * 7 / 10));
        w.U16(0);
        w.U16(0x0020);
        w.U16(0);
        return ms.ToArray();
    }

    private static byte[] BuildHmtx(List<CompiledGlyph> glyphs) {
        using var ms = new MemoryStream(glyphs.Count * 4);
        var w = new BeWriter(ms);
        foreach (var g in glyphs) {
            w.U16(g.AdvanceWidth);
            w.I16(g.LeftSideBearing);
        }
        return ms.ToArray();
    }

    private static byte[] BuildCmap(List<CompiledGlyph> glyphs) {
        var mapped = glyphs
            .Select((g, gid) => (g.Codepoint, Gid: (ushort)gid))
            .Skip(1)
            .OrderBy(x => x.Codepoint)
            .ToList();

        var segments = new List<(ushort Start, ushort End, short Delta)>();
        int idx = 0;
        while (idx < mapped.Count) {
            ushort startCp = mapped[idx].Codepoint;
            ushort startGid = mapped[idx].Gid;
            ushort endCp = startCp;
            ushort curGid = startGid;
            idx++;
            while (idx < mapped.Count &&
                   mapped[idx].Codepoint == endCp + 1 &&
                   mapped[idx].Gid == curGid + 1) {
                endCp++;
                curGid++;
                idx++;
            }
            segments.Add((startCp, endCp, unchecked((short)(startGid - startCp))));
        }
        segments.Add((0xFFFF, 0xFFFF, 1));

        ushort segCount = (ushort)segments.Count;
        ushort segCountX2 = (ushort)(segCount * 2);
        ushort searchRange = (ushort)(2 * (1 << (int)Math.Floor(Math.Log2(segCount))));
        ushort entrySelector = (ushort)Math.Floor(Math.Log2(segCount));
        ushort rangeShift = (ushort)(segCountX2 - searchRange);

        using var subMs = new MemoryStream();
        var sw = new BeWriter(subMs);
        sw.U16(4);
        sw.U16((ushort)(16 + segCount * 8));
        sw.U16(0);
        sw.U16(segCountX2);
        sw.U16(searchRange);
        sw.U16(entrySelector);
        sw.U16(rangeShift);
        foreach (var s in segments) sw.U16(s.End);
        sw.U16(0);
        foreach (var s in segments) sw.U16(s.Start);
        foreach (var s in segments) sw.I16(s.Delta);
        foreach (var _ in segments) sw.U16(0);

        byte[] fmt4 = subMs.ToArray();
        using var cmapMs = new MemoryStream(20 + fmt4.Length);
        var cw = new BeWriter(cmapMs);
        cw.U16(0);
        cw.U16(2);
        cw.U16(0); cw.U16(3); cw.U32(20);
        cw.U16(3); cw.U16(1); cw.U32(20);
        cmapMs.Write(fmt4);
        return cmapMs.ToArray();
    }

    private byte[] BuildName() {
        var entries = new (ushort NameId, string Value)[] {
            (0, "Overlayer UI Assets - free to use by anyone"),
            (1, _manifest.FontName),
            (2, "Regular"),
            (3, $"{_manifest.FontName}-1.0"),
            (4, _manifest.FontName),
            (5, "Version 1.0"),
            (6, _manifest.FontName),
        };

        using var strMs = new MemoryStream();
        var records = new List<(ushort NameId, ushort Length, ushort Offset)>(entries.Length);
        foreach (var (id, val) in entries) {
            byte[] encoded = Encoding.BigEndianUnicode.GetBytes(val);
            records.Add((id, (ushort)encoded.Length, (ushort)strMs.Length));
            strMs.Write(encoded);
        }

        ushort stringOffset = (ushort)(6 + records.Count * 12);
        using var ms = new MemoryStream(stringOffset + (int)strMs.Length);
        var w = new BeWriter(ms);
        w.U16(0);
        w.U16((ushort)records.Count);
        w.U16(stringOffset);
        foreach (var (id, len, off) in records) {
            w.U16(3);
            w.U16(1);
            w.U16(0x0409);
            w.U16(id);
            w.U16(len);
            w.U16(off);
        }
        strMs.WriteTo(ms);
        return ms.ToArray();
    }

    private static byte[] BuildPost(List<CompiledGlyph> glyphs) {
        using var ms = new MemoryStream();
        var w = new BeWriter(ms);
        w.U32(0x00020000);
        w.I32(0);
        w.I16(-100);
        w.I16(50);
        w.U32(1);
        w.U32(0); w.U32(0); w.U32(0); w.U32(0);
        w.U16((ushort)glyphs.Count);
        w.U16(0);
        for (int gi = 1; gi < glyphs.Count; gi++) {
            w.U16((ushort)(258 + (gi - 1)));
        }
        for (int gi = 1; gi < glyphs.Count; gi++) {
            byte[] ascii = Encoding.ASCII.GetBytes(glyphs[gi].Name);
            if (ascii.Length > 255) throw new InvalidOperationException($"Glyph name too long: {glyphs[gi].Name}");
            ms.WriteByte((byte)ascii.Length);
            ms.Write(ascii);
        }
        return ms.ToArray();
    }

    private static int PaddedLength(int len) => (len + 3) & ~3;

    private static uint ComputeChecksum(ReadOnlySpan<byte> data) {
        uint sum = 0;
        int fullWords = data.Length & ~3;
        for (int i = 0; i < fullWords; i += 4) {
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i, 4));
        }
        int rem = data.Length - fullWords;
        if (rem > 0) {
            Span<byte> tail = stackalloc byte[4];
            data.Slice(fullWords).CopyTo(tail);
            sum += BinaryPrimitives.ReadUInt32BigEndian(tail);
        }
        return sum;
    }

    private sealed class BeWriter {
        private readonly Stream _s;
        public BeWriter(Stream s) => _s = s;
        public void U8(byte v) => _s.WriteByte(v);
        public void U16(ushort v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, v); _s.Write(b); }
        public void I16(short v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, v); _s.Write(b); }
        public void U32(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); _s.Write(b); }
        public void I32(int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); _s.Write(b); }
        public void I64(long v) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(b, v); _s.Write(b); }
    }
}
