using Microsoft.ClearScript;
using Overlayer.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Overlay;

public sealed class OvGraphComponent : MaskableGraphic {
    public string JsCode = "50 + 40 * Math.sin(t * 2)";
    public float Window = 10f;
    public int Samples = 120;
    public float Min;
    public float Max = 100f;
    public bool AutoScale;
    public Color LineColor = Color.white;
    public float Thickness = 2f;
    public bool ShowAxes = true;
    public bool ShowGrid;
    public bool ShowFill;
    public Color FillColor = new(1f, 1f, 1f, 0.25f);

    private readonly List<Vector2> history = new();
    private ScriptObject func;
    private string funcCode;
    private float lastPushTime = float.NegativeInfinity;

    protected override void OnEnable() {
        base.OnEnable();
        history.Clear();
        lastPushTime = float.NegativeInfinity;
    }

    private void Update() {
        float now = Time.realtimeSinceStartup;
        float interval = Math.Max(0.01f, Window) / Math.Max(8, Samples);
        if (now - lastPushTime < interval) {
            return;
        }
        lastPushTime = now;
        EnsureFunc();
        if (func == null) {
            return;
        }
        float value;
        try {
            object raw = func.Invoke(false, now);
            if (raw == null || raw == Undefined.Value || !TryToFloat(raw, out value)) {
                return;
            }
        } catch {
            try {
                (func as IDisposable)?.Dispose();
            } catch { }
            func = null;
            return;
        }
        history.Add(new Vector2(now, value));
        float keep = Math.Max(Math.Max(0.01f, Window), 60f);
        float cutoff = now - keep;
        int trim = 0;
        while (trim < history.Count && history[trim].x < cutoff) {
            trim++;
        }
        int over = history.Count - trim - 65536;
        if (over > 0) {
            trim += over;
        }
        if (trim > 0) {
            history.RemoveRange(0, trim);
        }
        SetVerticesDirty();
    }

    private void EnsureFunc() {
        if (func != null && funcCode == JsCode) {
            return;
        }
        DisposeFunc();
        funcCode = JsCode;
        if (string.IsNullOrWhiteSpace(JsCode)) {
            return;
        }
        try {
            var engine = MainCore.V8.Engine;
            if (engine == null) {
                return;
            }
            func = engine.Evaluate($"(function(t){{ return ({JsCode}); }})") as ScriptObject;
        } catch {
            func = null;
        }
    }

    private void DisposeFunc() {
        if (func == null) {
            return;
        }
        try {
            (func as IDisposable)?.Dispose();
        } catch { }
        func = null;
    }

    protected override void OnDisable() {
        DisposeFunc();
    }

    protected override void OnDestroy() {
        DisposeFunc();
    }

    private static bool TryToFloat(object raw, out float value) {
        try {
            value = Convert.ToSingle(raw);
            return !float.IsNaN(value) && !float.IsInfinity(value);
        } catch {
            value = 0f;
            return false;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) {
            return;
        }

        float now = Time.realtimeSinceStartup;
        float window = Math.Max(0.01f, Window);
        float start = now - window;
        int first = 0;
        while (first < history.Count && history[first].x < start) {
            first++;
        }
        float lo = Min, hi = Max;
        if (AutoScale && first < history.Count) {
            float dlo = history[first].y, dhi = dlo;
            for (int i = first + 1; i < history.Count; i++) {
                float y = history[i].y;
                if (y < dlo) dlo = y;
                if (y > dhi) dhi = y;
            }
            if (dhi - dlo < 0.001f) {
                dhi = dlo + 1f;
            }
            lo = Math.Min(Min, dlo);
            hi = Math.Max(Max, dhi);
        }
        if (hi - lo < 0.001f) {
            hi = lo + 1f;
        }

        Vector2 Map(Vector2 p) {
            float x = rect.xMin + (p.x - start) / window * rect.width;
            float y = rect.yMin + (p.y - lo) / (hi - lo) * rect.height;
            return new Vector2(x, y);
        }

        Vector2 ClipLeft(Vector2 a, Vector2 b, float x0) {
            if (a.x >= x0) {
                return a;
            }
            float d = b.x - a.x;
            if (d <= 0.000001f) {
                return b;
            }
            float t = (x0 - a.x) / d;
            return a + (b - a) * t;
        }

        UIVertex vert = UIVertex.simpleVert;

        void Quad(Vector2 a, Vector2 b, float width, Color color) {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 0.000001f) {
                return;
            }
            Vector2 n = new(-dir.y, dir.x);
            n *= width * 0.5f / n.magnitude;
            int baseIndex = vh.currentVertCount;
            vert.color = color;
            vert.position = a - n; vh.AddVert(vert);
            vert.position = a + n; vh.AddVert(vert);
            vert.position = b + n; vh.AddVert(vert);
            vert.position = b - n; vh.AddVert(vert);
            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
        }

        if (ShowGrid) {
            Color grid = new(LineColor.r, LineColor.g, LineColor.b, LineColor.a * 0.25f);
            for (int i = 1; i < 4; i++) {
                float y = rect.yMin + rect.height * i / 4f;
                Quad(new Vector2(rect.xMin, y), new Vector2(rect.xMin + rect.width, y), 1f, grid);
            }
            for (int i = 1; i < 6; i++) {
                float x = rect.xMin + rect.width * i / 6f;
                Quad(new Vector2(x, rect.yMin), new Vector2(x, rect.yMin + rect.height), 1f, grid);
            }
        }

        if (history.Count == 0 || first >= history.Count) {
            return;
        }

        if (ShowFill && history.Count > first) {
            float baseY = rect.yMin;
            for (int i = Math.Max(1, first); i < history.Count; i++) {
                Vector2 a = Map(history[i - 1]), b = Map(history[i]);
                if (b.x < rect.xMin) {
                    continue;
                }
                Vector2 sa = ClipLeft(a, b, rect.xMin);
                int baseIndex = vh.currentVertCount;
                vert.color = FillColor;
                vert.position = new Vector2(sa.x, baseY); vh.AddVert(vert);
                vert.position = new Vector2(b.x, baseY); vh.AddVert(vert);
                vert.position = b; vh.AddVert(vert);
                vert.position = sa; vh.AddVert(vert);
                vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
            }
        }

        if (ShowAxes) {
            Color axis = new(LineColor.r, LineColor.g, LineColor.b, LineColor.a * 0.6f);
            if (lo <= 0f && 0f <= hi) {
                float y = rect.yMin + (0f - lo) / (hi - lo) * rect.height;
                Quad(new Vector2(rect.xMin, y), new Vector2(rect.xMin + rect.width, y), 1f, axis);
            }
        }

        float half = Math.Max(0.5f, Thickness) * 0.5f;
        Vector2 drawStart = Map(history[first]);
        for (int i = Math.Max(1, first); i < history.Count; i++) {
            Vector2 a = Map(history[i - 1]), b = Map(history[i]);
            if (b.x < rect.xMin) {
                continue;
            }
            a = ClipLeft(a, b, rect.xMin);
            if (i == Math.Max(1, first)) {
                drawStart = a;
            }
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 0.001f) {
                continue;
            }
            Vector2 n = new(-dir.y / len * half, dir.x / len * half);
            int baseIndex = vh.currentVertCount;
            vert.color = LineColor;
            vert.position = a - n; vh.AddVert(vert);
            vert.position = a + n; vh.AddVert(vert);
            vert.position = b + n; vh.AddVert(vert);
            vert.position = b - n; vh.AddVert(vert);
            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
        }

        void Cap(Vector2 p) {
            int baseIndex = vh.currentVertCount;
            vert.color = LineColor;
            const int capSegs = 6;
            vert.position = p; vh.AddVert(vert);
            for (int i = 0; i <= capSegs; i++) {
                float a = i / (float)capSegs * MathF.PI * 2f;
                vert.position = p + new Vector2(MathF.Cos(a) * half, MathF.Sin(a) * half);
                vh.AddVert(vert);
            }
            for (int i = 1; i <= capSegs; i++) {
                vh.AddTriangle(baseIndex, baseIndex + i, baseIndex + i + 1);
            }
        }

        Cap(drawStart);
        Cap(Map(history[^1]));
    }
}
