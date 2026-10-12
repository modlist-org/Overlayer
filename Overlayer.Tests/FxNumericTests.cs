using Overlayer.IO.Fx;
using Overlayer.Native;
using Xunit;
using Xunit.Abstractions;

public sealed class FxNumericTests {
    private readonly ITestOutputHelper output;

    static FxNumericTests() {
        try {
            string baseDir = AppContext.BaseDirectory;
            string dir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "O5Kit", "native", "zig-out", "lib"));
            if (Directory.Exists(dir)) {
                string so = Directory.GetFiles(dir, "O5KitNative.*").FirstOrDefault();
                if (!string.IsNullOrEmpty(so)) {
                    Environment.SetEnvironmentVariable("O5NATIVE_PATH", so);
                }
            }
        } catch {
        }
    }

    public FxNumericTests(ITestOutputHelper output) {
        this.output = output;
    }

    private static readonly (string expr, double want)[] Corpus = [
        ("2+3*4", 14), ("10-4-3", 3), ("2*(3+4)", 14), ("-5+3", -2), ("2*-3", -6),
        ("---5", -5), ("(2+3)*(4-1)", 15), ("10%3+1", 2), ("2*3%2", 0),
        ("2147483647+1", 2147483648.0), ("1000000*1000000", 1000000000000.0),
        ("9223372036854775807+1", -9223372036854775808.0),
        ("1/2", 0.5), ("7/2", 3.5), ("10/4", 2.5), ("PI/2", 1.5707963267948966),
        ("10%3", 1), ("-5%2", -1), ("7%-2", 1), ("5.5%2", 1.5), ("7.5%-2.0", 1.5),
        ("2^3", 1), ("2*3^1", 7), ("1+2^3", 0), ("5&3", 1), ("5|3", 7),
        ("15^15", 0), ("5|3&1", 5), ("2&3^1", 3), ("5^3|1", 7), ("1<<2&3", 0),
        ("2+3&1", 1), ("3.5^0", 4), ("2^2.0", 0), ("0.5^2", 2),
        ("1<<3", 8), ("8>>2", 2), ("1<<64", 1), ("64>>1", 32), ("3<<2+1", 24),
        ("-1<<1", -2), ("1<<-1", -9223372036854775808.0),
        ("2**3**2", 512), ("-2**2", -4), ("2*3**2", 18), ("2**-1", 0.5),
        ("(-2)**2", 4), ("Pow(2,3)", 8), ("2**-(1)", 0.5), ("2**--1", 2),
        ("1>2", 0), ("1==1", 1), ("1!=1", 0), ("!(1>2)", 1),
        ("1>0 && 2>1", 1), ("1<2==true", 1), ("5%2==1", 1),
        ("1 and 0", 0), ("not true", 0), ("TRUE", 1),
        ("2>1 ? 10 : 20", 10), ("1>2 ? 10 : 20", 20), ("true?1:2", 1),
        ("1?2:3", 2), ("2.5?1:2", 1), ("!5", 0), ("!0", 1), ("1&&2", 1),
        ("2==true", 0), ("0==false", 1), ("true>0", 1), ("1>true", 0),
        (".5+.5", 1), ("5.+1", 6), ("1e3", 1000), ("1E-3", 0.001),
        ("2*0.5", 1), ("0.1+0.2", 0.30000000000000004), ("1/3", 0.3333333333333333),
        ("PI*2", 6.283185307179586), ("E*E", 7.3890560989306495),
        ("Sqrt(4)", 2), ("Sin(0)", 0), ("Cos(0)", 1), ("Tan(0)", 0), ("Exp(0)", 1),
        ("Floor(2.9)", 2), ("Ceiling(2.1)", 3), ("Truncate(2.9)", 2),
        ("Floor(-2.1)", -3), ("Abs(-5)", 5), ("Abs(-5.5)", 5.5), ("Abs(2.5)", 2.5),
        ("Sign(-5)", -1), ("Sign(0)", 0), ("Sign(2.5)", 1),
        ("Max(1,2)", 2), ("Min(1,2)", 1), ("Min(2.5,3)", 2.5), ("Max(1.0,2)", 2),
        ("Round(2.5,0)", 2), ("Round(-2.5,0)", -2), ("Round(3.5,0)", 4),
        ("Round(2.675,2)", 2.68), ("Round(1000,false)", 1000),
        ("PI", 3.141592653589793), ("E", 2.718281828459045),
        ("pi", 3.141592653589793), ("e", 2.718281828459045),
        ("(1^3)+1", 3), ("(1^3)+2.5", 4.5), ("-(1^3)", -2),
        ("(1^3)>1", 1), ("(1^3)>-1", 1), ("(1^3)==-1", 0), ("(1^3)==2", 1),
        ("(1^3)?1:2", 1), ("(1^3)+(-5)", -3), ("(1^3)+true", 3),
        ("true^true", 0), ("false^100", 100), ("true&1", 1), ("1&true", 1),
        ("(1^3)%2", 0), ("10%(1^3)", 0), ("10%2.5", 0),
        ("(1^3)/(0-2)", -1), ("(1^3)/2.5", 0.8), ("1<<3000000000", 1),
        ("(1^3)%(0-2)", 0), ("Pow(2,true)", 2), ("+-5", -5), ("+5", 5),
        ("2*+3", 6), ("+(3)", 3), ("Abs(true)", 1), ("Sign(false)", 0),
        ("Max(1,true)", 1), ("Min(1,false)", 0),
        ("1/0", double.PositiveInfinity), ("(1^3)/0", double.PositiveInfinity),
        ("Max(2.5,(1^3))", 2.5), ("Min(2.5,(1^3))", 2.0),
    ];

    private static readonly string[] Declines = [
        "", "   ", "Tanh(0)", "Cosh(0)", "Sinh(0)", "2 in (1,2,3)", "foo(1)",
        "Sin()", "Sin(1,2)", "2+", "*2", "(2+3", "2+3)", "1,2", "'a'", "0x",
        "1e", "()", "Max(1,2,3)", "Min(1,2,3)", "Round(2.5)", "Pow(2)",
        "2**not true",
    ];

    private static readonly (string expr, double want, double rel)[] Approx = [
        ("Sin(1)", 0.8414709848078965, 1e-15),
        ("Cos(1)", 0.5403023058681398, 1e-15),
        ("Tan(1)", 1.5574077246549023, 1e-15),
        ("Asin(1)", 1.5707963267948966, 1e-15),
        ("Acos(0)", 1.5707963267948966, 1e-15),
        ("Atan(1)", 0.7853981633974483, 1e-15),
        ("Atan2(1,1)", 0.7853981633974483, 1e-15),
        ("Exp(1)", 2.718281828459045, 1e-15),
        ("Ln(10)", 2.302585092994046, 1e-15),
        ("Log10(100)", 2.0, 1e-15),
        ("Log(100,10)", 2.0, 1e-15),
        ("Sqrt(2)*Sqrt(2)", 2.0000000000000004, 1e-15),
        ("IEEERemainder(5,2)", 1.0, 1e-15),
        ("Log(10)", 2.302585092994046, 1e-15),
        ("2**PI", 8.824977827076287, 1e-15),
        ("Cos(Pow(1e3,2.5))", 0.24595818981861853, 1e-12),
    ];

    private static readonly string[] Nans = [
        "0.0/0.0", "Sqrt(-1)", "Log(0)", "Log(-1)", "Log(100,1)", "5.5%0.0", "5%0",
        "Max(PI,Sqrt(-2))", "Min(Sqrt(-2),1)",
    ];

    private static bool Same(double a, double b) {
        return a == b || (double.IsNaN(a) && double.IsNaN(b));
    }

    [Fact]
    public void FixedCorpus() {
        var bad = new List<string>();
        foreach (var (e, want) in Corpus) {
            if (!FxNumeric.TryEvaluate(e, typeof(double), out object got) || !(got is double d) || !Same(d, want)) {
                bad.Add($"corpus: {e} got={got}");
            }
        }
        foreach (var (e, want, rel) in Approx) {
            if (!FxNumeric.TryEvaluate(e, typeof(double), out object got) || !(got is double d)) {
                bad.Add($"approx eval failed: {e}");
            } else if (Math.Abs(d - want) / Math.Max(Math.Abs(want), 1e-300) > rel) {
                bad.Add($"approx: {e} got={d:R} want={want:R}");
            }
        }
        foreach (string e in Nans) {
            if (!FxNumeric.TryEvaluate(e, typeof(double), out object got) || !(got is double d) || !double.IsNaN(d)) {
                bad.Add($"nan: {e} got={got}");
            }
        }
        foreach (string e in Declines) {
            if (FxNumeric.TryEvaluate(e, typeof(double), out _)) {
                bad.Add($"should decline: {e}");
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    private sealed class RefParser {
        private readonly string s;
        private int pos;

        private RefParser(string s) {
            this.s = s;
        }

        public static double Eval(string e) {
            var p = new RefParser(e);
            double v = p.Add();
            p.Skip();
            if (p.pos != e.Length) {
                throw new Exception("trailing");
            }
            return v;
        }

        private void Skip() {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) {
                pos++;
            }
        }

        private double Add() {
            double v = Mul();
            while (true) {
                Skip();
                if (pos < s.Length && (s[pos] == '+' || s[pos] == '-')) {
                    char op = s[pos++];
                    double r = Mul();
                    v = op == '+' ? v + r : v - r;
                } else {
                    return v;
                }
            }
        }

        private double Mul() {
            double v = Unary();
            while (true) {
                Skip();
                if (pos < s.Length && (s[pos] == '*' || s[pos] == '/' || s[pos] == '%')) {
                    char op = s[pos++];
                    double r = Unary();
                    v = op == '*' ? v * r : op == '/' ? v / r : v % r;
                } else {
                    return v;
                }
            }
        }

        private double Unary() {
            Skip();
            if (pos < s.Length && (s[pos] == '-' || s[pos] == '+')) {
                char op = s[pos++];
                double v = Unary();
                return op == '-' ? -v : v;
            }
            return Primary();
        }

        private double Primary() {
            Skip();
            if (pos < s.Length && s[pos] == '(') {
                pos++;
                double v = Add();
                Skip();
                if (pos >= s.Length || s[pos] != ')') {
                    throw new Exception("paren");
                }
                pos++;
                return v;
            }
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.' || s[pos] == 'e' || s[pos] == 'E' ||
                ((s[pos] == '+' || s[pos] == '-') && pos > start && (s[pos - 1] == 'e' || s[pos - 1] == 'E')))) {
                pos++;
            }
            if (pos == start) {
                throw new Exception("number");
            }
            return double.Parse(s.Substring(start, pos - start), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [Fact]
    public void FuzzCore() {
        var rng = new Random(1337);
        string[] nums = ["0", "1", "2", "7", "10", "0.5", "2.5", "100", "1e3", "3.25", ".5", "5."];
        string[] ops = ["+", "-", "*", "/", "%"];
        var bad = new List<string>();
        int n = 0;
        for (int i = 0; i < 20000; i++) {
            string e = GenArith(rng, nums, ops, 0);
            double want;
            try {
                want = RefParser.Eval(e);
            } catch {
                continue;
            }
            n++;
            if (!FxNumeric.TryEvaluate(e, typeof(double), out object got) || !(got is double d) || !Same(d, want)) {
                if (bad.Count < 20) {
                    bad.Add($"core: {e} got={got} want={want:R}");
                }
            }
        }
        output.WriteLine($"fuzz core checked {n}");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    private static string GenArith(Random rng, string[] nums, string[] ops, int depth) {
        if (depth > 3 || rng.NextDouble() < 0.4) {
            string v = nums[rng.Next(nums.Length)];
            return rng.NextDouble() < 0.15 ? "-" + v : v;
        }
        if (rng.NextDouble() < 0.2) {
            return "(" + GenArith(rng, nums, ops, depth + 1) + ")";
        }
        return GenArith(rng, nums, ops, depth + 1) + ops[rng.Next(ops.Length)] + GenArith(rng, nums, ops, depth + 1);
    }

    [Fact]
    public void FuzzContract() {
        var rng = new Random(7331);
        string[] nums = ["0", "1", "2", "3", "7", "10", "0.5", "2.5", "100", "0x10", "PI", "1e3"];
        string[] ops2 = ["+", "-", "*", "/", "%", "^", "&", "|", "**", "<<", ">>", "==", "!=", "<", "<=", ">", ">="];
        string[] funcs = ["Abs", "Sign", "Floor", "Ceiling", "Truncate", "Sqrt", "Sin", "Cos", "Max", "Min", "Pow", "Round"];
        var bad = new List<string>();
        int evals = 0;
        for (int i = 0; i < 20000; i++) {
            string e = GenFull(rng, nums, ops2, funcs, 0);
            bool ok1, ok2;
            double v1 = 0, v2 = 0;
            object o1, o2;
            try {
                ok1 = FxNumeric.TryEvaluate(e, typeof(double), out o1);
                v1 = ok1 && o1 is double d1 ? d1 : 0;
            } catch (Exception ex) {
                bad.Add($"crash1: {e} {ex.GetType().Name}");
                continue;
            }
            try {
                ok2 = FxNumeric.TryEvaluate(e, typeof(double), out o2);
                v2 = ok2 && o2 is double d2 ? d2 : 0;
            } catch (Exception ex) {
                bad.Add($"crash2: {e} {ex.GetType().Name}");
                continue;
            }
            if (ok1 != ok2 || (ok1 && !Same(v1, v2))) {
                bad.Add($"nondeterministic: {e}");
            } else if (ok1) {
                evals++;
            }
            if (bad.Count >= 20) {
                break;
            }
        }
        output.WriteLine($"fuzz contract evals {evals}");
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    private static string GenFull(Random rng, string[] nums, string[] ops2, string[] funcs, int depth) {
        if (depth > 2 || rng.NextDouble() < 0.35) {
            return nums[rng.Next(nums.Length)];
        }
        int kind = rng.Next(6);
        switch (kind) {
            case 0:
                return "(" + GenFull(rng, nums, ops2, funcs, depth + 1) + ops2[rng.Next(ops2.Length)] + GenFull(rng, nums, ops2, funcs, depth + 1) + ")";
            case 1: {
                string f = funcs[rng.Next(funcs.Length)];
                if (f is "Max" or "Min" or "Pow" or "Round") {
                    return f + "(" + GenFull(rng, nums, ops2, funcs, depth + 1) + "," + GenFull(rng, nums, ops2, funcs, depth + 1) + ")";
                }
                return f + "(" + GenFull(rng, nums, ops2, funcs, depth + 1) + ")";
            }
            case 2:
                return "-" + GenFull(rng, nums, ops2, funcs, depth + 1);
            case 3:
                return "(" + GenFull(rng, nums, ops2, funcs, depth + 1) + ">" + GenFull(rng, nums, ops2, funcs, depth + 1) +
                    "?" + GenFull(rng, nums, ops2, funcs, depth + 1) + ":" + GenFull(rng, nums, ops2, funcs, depth + 1) + ")";
            case 4:
                return "!(" + GenFull(rng, nums, ops2, funcs, depth + 1) + ">0)";
            default:
                return nums[rng.Next(nums.Length)];
        }
    }

    [Fact]
    public void Bench() {
        string[] templates = [
            "1920/2+Sin(1.5)*10", "PI*2+Max(3,4)", "100*0.5+Round(2.675,2)", "1+2+3+4+5",
            "Sin(Cos(Tan(0.5)))*Sqrt(Abs(-16))", "Pow(Max(2,3),Min(4,5))/Log(100,10)",
            "(1+2)*(3-4)/(5+6)-7%3+2**10", "Atan2(Sin(1),Cos(1))*180/PI",
        ];
        const int iters = 500000;
        foreach (string t in templates) {
            FxNumeric.TryEvaluate(t, typeof(double), out _);
        }

        double Best(System.Action fn) {
            double best = double.MaxValue;
            for (int r = 0; r < 3; r++) {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                fn();
                sw.Stop();
                best = System.Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            return best;
        }

        int total = iters * templates.Length;
        double cached = Best(() => {
            for (int i = 0; i < iters; i++) {
                foreach (string t in templates) {
                    FxNumeric.TryEvaluate(t, typeof(double), out _);
                }
            }
        });
        output.WriteLine($"fxnumeric cached: {cached:F1}ms for {total} evals");

        if (O5Expr.IsSupported) {
            long[] handles = new long[templates.Length];
            for (int i = 0; i < templates.Length; i++) {
                handles[i] = O5Expr.Compile(templates[i]);
            }
            double raw = Best(() => {
                for (int i = 0; i < iters; i++) {
                    foreach (long h in handles) {
                        O5Expr.TryEval(h, out _);
                    }
                }
            });
            output.WriteLine($"zig raw eval: {raw:F1}ms for {total} evals");
            foreach (long h in handles) {
                O5Expr.Free(h);
            }
        }
    }
}
