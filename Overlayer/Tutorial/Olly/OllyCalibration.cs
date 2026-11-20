using Newtonsoft.Json.Linq;
using Overlayer.Core;
using UnityEngine;

namespace Overlayer.Tutorial.Olly;

// 3x3 gaze calibration for Olly's eyes and head. Sample() bilinearly interpolates
// between the 9 points for any screen target. Row-major order, row 0 is the top row.
internal static class OllyCalibration {
    public const int Count = 9;

    // Grid fractions of the screen (x right, y up).
    public static readonly float[] Fx = [0.12f, 0.5f, 0.88f];
    public static readonly float[] Fy = [0.88f, 0.5f, 0.12f];

    public static readonly Vector2[] EyeL = [
        new(-9f, -3.9f), new(-1.3f, -5.4f), new(2.1f, -3.1f),
        new(-11f, 1.1f), new(0f, 0f), new(4.2f, 0f),
        new(-9.8f, 7f), new(-1.9f, 10f), new(0.2f, 9.1f),
    ];
    public static readonly Vector2[] EyeR = [
        new(-4.4f, -3.9f), new(-0.2f, -5.4f), new(7.9f, -2.5f),
        new(-7.3f, 1.1f), new(0f, 0f), new(10.7f, 0f),
        new(-6.3f, 5f), new(-1.1f, 9f), new(8.3f, 9f),
    ];
    public static readonly Vector2[] Head = [
        new(-3.4f, -1.1f), new(0f, -2f), new(3.7f, -1f),
        new(-3.6f, 0f), new(0f, 0f), new(4.4f, 0f),
        new(-3.5f, 5.4f), new(0f, 6.1f), new(1.8f, 5.7f),
    ];

    static OllyCalibration() {
        // A saved file overrides the baked table.
        Load();
    }

    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Olly", "olly_calib.json");

    public static Vector2 PointScreen(int i) => new(Fx[i % 3] * Screen.width, Fy[i / 3] * Screen.height);

    public static (Vector2 eyeL, Vector2 eyeR, Vector2 head) Sample(Vector2 screen) {
        float nx = Mathf.Clamp01(screen.x / Screen.width);
        float ny = Mathf.Clamp01(screen.y / Screen.height);
        int x0, x1;
        float tx;
        if (nx <= Fx[1]) {
            x0 = 0; x1 = 1; tx = (nx - Fx[0]) / (Fx[1] - Fx[0]);
        } else {
            x0 = 1; x1 = 2; tx = (nx - Fx[1]) / (Fx[2] - Fx[1]);
        }
        int y0, y1;
        float ty;
        if (ny >= Fy[1]) {
            y0 = 0; y1 = 1; ty = (Fy[0] - ny) / (Fy[0] - Fy[1]);
        } else {
            y0 = 1; y1 = 2; ty = (Fy[1] - ny) / (Fy[1] - Fy[2]);
        }
        tx = Mathf.Clamp01(tx);
        ty = Mathf.Clamp01(ty);
        static Vector2 Bilinear(Vector2[] a, int x0, int x1, int y0, int y1, float tx, float ty) {
            Vector2 top = Vector2.Lerp(a[y0 * 3 + x0], a[y0 * 3 + x1], tx);
            Vector2 bot = Vector2.Lerp(a[y1 * 3 + x0], a[y1 * 3 + x1], tx);
            return Vector2.Lerp(top, bot, ty);
        }
        return (Clamp(Down(Bilinear(EyeL, x0, x1, y0, y1, tx, ty)), true),
            Clamp(Down(Bilinear(EyeR, x0, x1, y0, y1, tx, ty)), true),
            Clamp(HeadScale(Bilinear(Head, x0, x1, y0, y1, tx, ty)), false));
    }

    // Never beyond the table range.
    private static Vector2 Clamp(Vector2 v, bool eye) {
        float mx = 0.01f, my = 0.01f;
        if (eye) {
            foreach (var e in EyeL) {
                mx = Mathf.Max(mx, Mathf.Abs(e.x));
                my = Mathf.Max(my, Mathf.Abs(e.y));
            }
            foreach (var e in EyeR) {
                mx = Mathf.Max(mx, Mathf.Abs(e.x));
                my = Mathf.Max(my, Mathf.Abs(e.y));
            }
        } else {
            foreach (var h in Head) {
                mx = Mathf.Max(mx, Mathf.Abs(h.x));
                my = Mathf.Max(my, Mathf.Abs(h.y));
            }
        }
        return new(Mathf.Clamp(v.x, -mx, mx), Mathf.Clamp(v.y, -my, my));
    }

    // Looking down moves the eyes less; the head moves less overall, least looking down.
    private static Vector2 Down(Vector2 v) => v.y > 0f ? new(v.x, v.y * 1.08f) : v;
    private static Vector2 HeadScale(Vector2 v) {
        v *= 0.5f;
        if (v.y > 0f) {
            return new(v.x, v.y * 0.2f);
        }
        return v.y < 0f ? new(v.x, v.y * 0.8f) : v;
    }

    public static void Clear() {
        for (int i = 0; i < Count; i++) {
            EyeL[i] = EyeR[i] = Head[i] = Vector2.zero;
        }
    }

    private static JArray Vec(Vector2 v) => [v.x, v.y];

    public static string Export() {
        var eyeL = new JArray();
        var eyeR = new JArray();
        var head = new JArray();
        for (int i = 0; i < Count; i++) {
            eyeL.Add(Vec(EyeL[i]));
            eyeR.Add(Vec(EyeR[i]));
            head.Add(Vec(Head[i]));
        }
        return new JObject { ["eyeL"] = eyeL, ["eyeR"] = eyeR, ["head"] = head }.ToString();
    }

    public static bool Import(string json) {
        try {
            var obj = JObject.Parse(json);
            if (!ReadInto(obj["eyeL"], EyeL) || !ReadInto(obj["eyeR"], EyeR) || !ReadInto(obj["head"], Head)) {
                return false;
            }
            return true;
        } catch {
            return false;
        }
    }

    private static bool ReadInto(JToken token, Vector2[] into) {
        if (token is not JArray arr || arr.Count != Count) {
            return false;
        }
        for (int i = 0; i < Count; i++) {
            if (arr[i] is not JArray pair || pair.Count != 2) {
                return false;
            }
            into[i] = new Vector2((float)pair[0], (float)pair[1]);
        }
        return true;
    }

    public static void Save() {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, Export());
        } catch (Exception e) {
            MainCore.Log.Err($"[OllyCalib] Save failed: {e.Message}");
        }
    }

    public static bool Load() {
        try {
            if (!File.Exists(FilePath)) {
                return false;
            }
            return Import(File.ReadAllText(FilePath));
        } catch (Exception e) {
            MainCore.Log.Err($"[OllyCalib] Load failed: {e.Message}");
            return false;
        }
    }
}
