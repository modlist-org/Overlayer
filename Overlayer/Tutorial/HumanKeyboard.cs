using UnityEngine;

namespace Overlayer.Tutorial;

/// <summary>
/// Generates human-like keystroke timestamps with configurable WPM, token difficulty,
/// word-length slope, word-to-word transitions, QWERTY hand/finger biomechanics,
/// fast paired brackets, AR(1) tempo momentum, and brief hesitations.
/// </summary>
internal sealed class HumanKeyboard {
    private enum TokenClass { Prose, Caps, Code, Sym }
    private enum Biomech { Space, Repeat, AltHand, SameFinger, SameHand }

    private readonly struct KeyInfo(byte hand, byte row, byte finger, bool shift) {
        public readonly byte Hand = hand;
        public readonly byte Row = row;
        public readonly byte Finger = finger;
        public readonly bool Shift = shift;
    }

    private readonly struct Token(TokenClass cls, int len) {
        public readonly TokenClass Cls = cls;
        public readonly int Len = len;
    }

    private readonly struct CharMeta(bool isSpace, int tokenIdx, int posInTok) {
        public readonly bool IsSpace = isSpace;
        public readonly int TokenIdx = tokenIdx;
        public readonly int PosInTok = posInTok;
    }

    private static readonly Dictionary<char, KeyInfo> KeyMap = BuildKeyMap();

    // Trained from 34 sentences / 2,404 keys (62 WPM baseline scaled to configurable target WPM).
    public float Wpm = 290f;
    public float PaceVar = 0.18f;
    public float SpaceMul = 1.40f;
    public float SymPenalty = 1.45f;
    public float LenSlope = -0.03f;
    public float ArPhi = 0.58f;
    public float ArSigma = 0.16f;
    public float PauseProb = 0.07f;

    public float ProseMul = 1.14f;
    public float CapsMul = 1.15f;
    public float CodeMul = 1.67f;
    public float SymMul = 2.44f;
    public float AltHandMul = 0.97f;
    public float SameHandMul = 1.07f;
    public float SameFingerMul = 1.31f;
    public float RepeatMul = 0.62f;
    public float PairedBracketMul = 0.30f;
    public float RowJumpMul = 1.10f;
    public float ShiftMul = 1.28f;
    public float WordStartMul = 1.16f;
    public float TransProseToProse = 0.86f;
    public float TransSameToDiff = 1.24f;
    public float TransCodeToCode = 1.15f;

    private int nextSeed;

    public float[] Schedule(string text, float? wpm = null, int? seed = null) {
        if (string.IsNullOrEmpty(text)) {
            return [];
        }

        AnalyzePrompt(text, out var tokens, out var charInfo);
        float targetWpm = Mathf.Clamp(wpm ?? Wpm, 10f, 500f);
        float avgUnitSec = 60f / (5f * targetWpm);

        int s = seed ?? System.Threading.Interlocked.Increment(ref nextSeed);
        var rng = new System.Random(unchecked(text.GetHashCode() ^ (s * 7919)));
        float phraseMul = Mathf.Exp(Mathf.Clamp(RandN(rng) * PaceVar, -0.4f, 0.4f));

        float spaceSliderScale = SpaceMul / 1.40f;
        float symSliderScale = SymPenalty / 1.45f;

        float momentum = 0f;
        float t = 0f;
        var times = new float[text.Length];

        for (int i = 0; i < text.Length; i++) {
            char ch = text[i];
            char prevCh = i > 0 ? text[i - 1] : '\0';
            var ci = charInfo[i];
            Token? tok = ci.TokenIdx >= 0 ? tokens[ci.TokenIdx] : null;
            Token? prevTok = i > 0 && charInfo[i - 1].TokenIdx >= 0 ? tokens[charInfo[i - 1].TokenIdx] : null;
            Token? nextTok = ci.IsSpace && i + 1 < text.Length && charInfo[i + 1].TokenIdx >= 0 ? tokens[charInfo[i + 1].TokenIdx] : null;

            bool bracketPair = i > 0 && IsBracketPair(prevCh, ch);
            float mul;

            if (ci.IsSpace) {
                float transMul = TransProseToProse;
                if (prevTok.HasValue && nextTok.HasValue) {
                    if (prevTok.Value.Cls != nextTok.Value.Cls) {
                        transMul = TransSameToDiff;
                    } else if (prevTok.Value.Cls is TokenClass.Code or TokenClass.Sym) {
                        transMul = TransCodeToCode;
                    }
                }
                float baseTrans = Mathf.Max(0.1f, TransProseToProse);
                mul = SpaceMul * (transMul / baseTrans) * (0.88f + (float)rng.NextDouble() * 0.28f);
            } else if (bracketPair) {
                mul = PairedBracketMul * (0.86f + (float)rng.NextDouble() * 0.26f);
            } else {
                var tv = tok!.Value;
                float clsMul = tv.Cls switch {
                    TokenClass.Caps => CapsMul,
                    TokenClass.Code => CodeMul,
                    TokenClass.Sym => SymMul,
                    _ => ProseMul,
                };

                float lenFactor = Mathf.Clamp(1f + LenSlope * (Mathf.Min(12, tv.Len) - 4.5f), 0.7f, 1.45f);

                var (bio, rowJump) = ClassifyPair(prevCh, ch);
                float bioMul = bio switch {
                    Biomech.Repeat => RepeatMul / 0.96f,
                    Biomech.AltHand => AltHandMul / 0.96f,
                    Biomech.SameFinger => SameFingerMul / 0.96f,
                    _ => SameHandMul / 0.96f,
                };
                if (rowJump) {
                    bioMul *= RowJumpMul;
                }

                float modMul = 1f;
                if (IsSymbol(ch)) {
                    modMul = (SymMul / 1.15f) * symSliderScale;
                } else if (IsAsciiUpper(ch)) {
                    modMul = (ShiftMul / 1.05f) * (0.85f + 0.15f * symSliderScale);
                }

                float startBoost = ci.PosInTok == 0 && i > 0 ? WordStartMul * spaceSliderScale * 0.88f : 1f;
                mul = clsMul * lenFactor * bioMul * modMul * startBoost * (0.84f + (float)rng.NextDouble() * 0.34f);
            }

            momentum = momentum * ArPhi + RandN(rng) * ArSigma;
            float dt = avgUnitSec * phraseMul * mul * Mathf.Exp(Mathf.Clamp(momentum, -0.42f, 0.42f));

            float pBias = bracketPair ? 0f : (ci.IsSpace || ci.PosInTok == 0 || IsSymbol(ch) ? 1.5f : 0.7f);
            if (i > 0 && rng.NextDouble() < PauseProb * pBias) {
                dt += avgUnitSec * (0.9f + (float)rng.NextDouble() * 1.5f);
            }

            t += Mathf.Max(0.014f, dt);
            times[i] = t;
        }

        return times;
    }

    private static void AnalyzePrompt(string text, out List<Token> tokens, out CharMeta[] charInfo) {
        tokens = [];
        charInfo = new CharMeta[text.Length];
        int i = 0;
        while (i < text.Length) {
            if (text[i] == ' ') {
                charInfo[i] = new CharMeta(true, -1, 0);
                i++;
                continue;
            }
            int start = i;
            while (i < text.Length && text[i] != ' ') {
                i++;
            }
            int len = i - start;
            int letters = 0, uppers = 0, digits = 0, syms = 0;
            for (int k = start; k < i; k++) {
                char c = text[k];
                if (IsAsciiAlpha(c)) {
                    letters++;
                    if (IsAsciiUpper(c)) {
                        uppers++;
                    }
                } else if (c >= '0' && c <= '9') {
                    digits++;
                } else {
                    syms++;
                }
            }

            var cls = TokenClass.Prose;
            bool trailingPunctOnly = syms == 1 && letters > 0 && digits == 0 && ",.!?;:'\"".IndexOf(text[i - 1]) >= 0;
            if (letters == 0 && digits == 0) {
                cls = TokenClass.Sym;
            } else if (digits > 0 || (syms > 0 && !trailingPunctOnly)) {
                cls = TokenClass.Code;
            } else if (uppers >= 2 || (uppers == 1 && letters > 1 && !IsAsciiUpper(text[start]))) {
                cls = TokenClass.Caps;
            }

            int tIdx = tokens.Count;
            tokens.Add(new Token(cls, len));
            for (int k = start; k < i; k++) {
                charInfo[k] = new CharMeta(false, tIdx, k - start);
            }
        }
    }

    private static (Biomech bio, bool rowJump) ClassifyPair(char prev, char cur) {
        if (prev == '\0' || prev == ' ' || cur == ' ') {
            return (Biomech.Space, false);
        }
        if (prev == cur) {
            return (Biomech.Repeat, false);
        }
        if (!KeyMap.TryGetValue(prev, out var k0) || !KeyMap.TryGetValue(cur, out var k1)) {
            return (Biomech.SameHand, false);
        }
        bool rowJump = Mathf.Abs(k0.Row - k1.Row) >= 2;
        if (k0.Hand != k1.Hand) {
            return (Biomech.AltHand, rowJump);
        }
        return (k0.Finger == k1.Finger ? Biomech.SameFinger : Biomech.SameHand, rowJump);
    }

    private static Dictionary<char, KeyInfo> BuildKeyMap() {
        var map = new Dictionary<char, KeyInfo>(96);
        string[] rows = [
            "`~1!2@3#4$5%6^7&8*9(0)-_=+",
            "qQwWeErRtTyYuUiIoOpP[{]}\\|",
            "aAsSdDfFgGhHjJkKlL;:'\"",
            "zZxXcCvVbBnNmM,<.>/?"
        ];
        byte[][] fingers = [
            [0, 0, 1, 2, 3, 3, 6, 6, 7, 8, 9, 9, 9],
            [0, 1, 2, 3, 3, 6, 6, 7, 8, 9, 9, 9, 9],
            [0, 1, 2, 3, 3, 6, 6, 7, 8, 9, 9],
            [0, 1, 2, 3, 3, 6, 6, 7, 8, 9]
        ];
        for (byte r = 0; r < rows.Length; r++) {
            string row = rows[r];
            for (int c = 0; c < row.Length / 2; c++) {
                byte finger = fingers[r][c];
                byte hand = (byte)(finger <= 3 ? 0 : 1);
                map[row[c * 2]] = new KeyInfo(hand, r, finger, false);
                map[row[c * 2 + 1]] = new KeyInfo(hand, r, finger, true);
            }
        }
        map[' '] = new KeyInfo(2, 4, 4, false);
        return map;
    }

    private static bool IsAsciiAlpha(char ch) => (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
    private static bool IsAsciiUpper(char ch) => ch >= 'A' && ch <= 'Z';
    private static bool IsSymbol(char ch) => ch != ' ' && !IsAsciiAlpha(ch) && !(ch >= '0' && ch <= '9');

    private static bool IsBracketPair(char prev, char cur) =>
        (prev == '(' && cur == ')') ||
        (prev == '[' && cur == ']') ||
        (prev == '{' && cur == '}') ||
        (prev == '<' && cur == '>');

    private static float RandN(System.Random rng) {
        double u1 = Math.Max(1e-6, rng.NextDouble());
        double u2 = rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }
}
