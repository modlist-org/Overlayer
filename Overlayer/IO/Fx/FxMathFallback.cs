using System;
using System.Collections.Generic;
using System.Globalization;

namespace Overlayer.IO.Fx;

/// <summary>
/// Minimal math-expression evaluator used when NCalc cannot run (stripped
/// IL2CPP runtimes where its parser compiler has no JIT). Supports numbers,
/// <c>+ - * / % ^</c>, parentheses, unary +/- , <c>PI</c>/<c>E</c> (any case),
/// common functions and comparisons. Throws <see cref="FormatException"/> on
/// invalid input, like a failed NCalc evaluation.
/// </summary>
internal static class FxMathFallback {
    public static bool TryEvaluate(string text, out double value) {
        value = 0d;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        try {
            var parser = new Parser(text);
            value = parser.ParseOr();
            parser.SkipWhite();
            if (!parser.End) {
                return false;
            }
            return !(double.IsNaN(value) || double.IsInfinity(value));
        } catch {
            return false;
        }
    }

    private sealed class Parser {
        private readonly string _text;
        private int _pos;

        public Parser(string text) => _text = text;

        public bool End {
            get {
                SkipWhite();
                return _pos >= _text.Length;
            }
        }

        public void SkipWhite() {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) {
                _pos++;
            }
        }

        private bool Eat(char c) {
            SkipWhite();
            if (_pos < _text.Length && _text[_pos] == c) {
                _pos++;
                return true;
            }
            return false;
        }

        private void Expect(char c) {
            if (!Eat(c)) {
                throw new FormatException($"Expected '{c}'.");
            }
        }

        public double ParseOr() {
            double left = ParseAnd();
            while (true) {
                if (Eat('|')) {
                    Expect('|');
                    double right = ParseAnd();
                    left = (left != 0d || right != 0d) ? 1d : 0d;
                } else {
                    return left;
                }
            }
        }

        private double ParseAnd() {
            double left = ParseEquality();
            while (true) {
                if (Eat('&')) {
                    Expect('&');
                    double right = ParseEquality();
                    left = (left != 0d && right != 0d) ? 1d : 0d;
                } else {
                    return left;
                }
            }
        }

        private double ParseEquality() {
            double left = ParseComparison();
            while (true) {
                SkipWhite();
                if (Match("==")) {
                    left = left == ParseComparison() ? 1d : 0d;
                } else if (Match("!=")) {
                    left = left != ParseComparison() ? 1d : 0d;
                } else {
                    return left;
                }
            }
        }

        private double ParseComparison() {
            double left = ParseAdditive();
            while (true) {
                SkipWhite();
                if (Match(">=")) {
                    left = left >= ParseAdditive() ? 1d : 0d;
                } else if (Match("<=")) {
                    left = left <= ParseAdditive() ? 1d : 0d;
                } else if (Eat('>')) {
                    left = left > ParseAdditive() ? 1d : 0d;
                } else if (Eat('<')) {
                    left = left < ParseAdditive() ? 1d : 0d;
                } else {
                    return left;
                }
            }
        }

        private double ParseAdditive() {
            double left = ParseMultiplicative();
            while (true) {
                if (Eat('+')) {
                    left += ParseMultiplicative();
                } else if (Eat('-')) {
                    left -= ParseMultiplicative();
                } else {
                    return left;
                }
            }
        }

        private double ParseMultiplicative() {
            double left = ParsePower();
            while (true) {
                if (Eat('*')) {
                    left *= ParsePower();
                } else if (Eat('/')) {
                    left /= ParsePower();
                } else if (Eat('%')) {
                    left %= ParsePower();
                } else {
                    return left;
                }
            }
        }

        private double ParsePower() {
            double left = ParseUnary();
            if (Eat('^')) {
                double right = ParsePower();
                return Math.Pow(left, right);
            }
            return left;
        }

        private double ParseUnary() {
            if (Eat('+')) {
                return ParseUnary();
            }
            if (Eat('-')) {
                return -ParseUnary();
            }
            if (Eat('!')) {
                return ParseUnary() == 0d ? 1d : 0d;
            }
            return ParsePrimary();
        }

        private double ParsePrimary() {
            SkipWhite();
            if (Eat('(')) {
                double value = ParseOr();
                Expect(')');
                return value;
            }

            if (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.')) {
                return ParseNumber();
            }

            if (_pos < _text.Length && (char.IsLetter(_text[_pos]) || _text[_pos] == '_')) {
                return ParseNamed();
            }

            throw new FormatException($"Unexpected character '{(_pos < _text.Length ? _text[_pos] : '?')}'.");
        }

        private double ParseNumber() {
            int start = _pos;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.')) {
                _pos++;
            }
            if (_pos < _text.Length && (_text[_pos] == 'e' || _text[_pos] == 'E')) {
                int mark = _pos;
                _pos++;
                if (_pos < _text.Length && (_text[_pos] == '+' || _text[_pos] == '-')) {
                    _pos++;
                }
                if (_pos >= _text.Length || !char.IsDigit(_text[_pos])) {
                    _pos = mark;
                } else {
                    while (_pos < _text.Length && char.IsDigit(_text[_pos])) {
                        _pos++;
                    }
                }
            }
            string token = _text.Substring(start, _pos - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) {
                throw new FormatException($"Invalid number '{token}'.");
            }
            return value;
        }

        private double ParseNamed() {
            int start = _pos;
            while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_')) {
                _pos++;
            }
            string name = _text.Substring(start, _pos - start).ToLowerInvariant();
            if (name == "pi") {
                return Math.PI;
            }
            if (name == "e") {
                return Math.E;
            }

            Expect('(');
            var args = new List<double>();
            SkipWhite();
            if (!Eat(')')) {
                while (true) {
                    args.Add(ParseOr());
                    SkipWhite();
                    if (Eat(')')) {
                        break;
                    }
                    Expect(',');
                }
            }
            return Call(name, args);
        }

        private bool Match(string token) {
            SkipWhite();
            if (_pos + token.Length <= _text.Length
                && string.CompareOrdinal(_text, _pos, token, 0, token.Length) == 0) {
                _pos += token.Length;
                return true;
            }
            return false;
        }

        private static double Call(string name, List<double> args) {
            double a0 = args.Count > 0 ? args[0] : 0d;
            double a1 = args.Count > 1 ? args[1] : 0d;
            switch (name) {
                case "sin": return Math.Sin(a0);
                case "cos": return Math.Cos(a0);
                case "tan": return Math.Tan(a0);
                case "asin": return Math.Asin(a0);
                case "acos": return Math.Acos(a0);
                case "atan": return Math.Atan(a0);
                case "atan2": return Math.Atan2(a0, a1);
                case "sqrt": return Math.Sqrt(a0);
                case "abs": return Math.Abs(a0);
                case "floor": return Math.Floor(a0);
                case "ceil": return Math.Ceiling(a0);
                case "ceiling": return Math.Ceiling(a0);
                case "round": return args.Count > 1 ? Math.Round(a0, (int)a1) : Math.Round(a0);
                case "truncate": return Math.Truncate(a0);
                case "sign": return Math.Sign(a0);
                case "exp": return Math.Exp(a0);
                case "log": return args.Count > 1 ? Math.Log(a0, a1) : Math.Log(a0);
                case "log10": return Math.Log10(a0);
                case "pow": return Math.Pow(a0, a1);
                case "min": return args.Count == 0 ? 0d : Min(args);
                case "max": return args.Count == 0 ? 0d : Max(args);
                case "clamp": return args.Count < 3 ? a0 : Math.Min(Math.Max(a0, args[1]), args[2]);
                default: throw new FormatException($"Unknown function '{name}'.");
            }
        }

        private static double Min(List<double> args) {
            double m = args[0];
            for (int i = 1; i < args.Count; i++) {
                if (args[i] < m) {
                    m = args[i];
                }
            }
            return m;
        }

        private static double Max(List<double> args) {
            double m = args[0];
            for (int i = 1; i < args.Count; i++) {
                if (args[i] > m) {
                    m = args[i];
                }
            }
            return m;
        }
    }
}
