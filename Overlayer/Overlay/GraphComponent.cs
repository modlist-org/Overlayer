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
        if(now - lastPushTime < 0.0001f) {
            return;
        }
        lastPushTime = now;
        EnsureFunc();
        if(func == null) {
            return;
        }
        float value;
        try {
            object raw = func.Invoke(false, now);
            if(raw == null || raw == Undefined.Value || !TryToFloat(raw, out value)) {
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
        float cutoff = now - Math.Max(0.01f, Window);
        int trim = 0;
        while(trim < history.Count && history[trim].x < cutoff) {
            trim++;
        }
        int over = history.Count - trim - Math.Max(8, Samples);
        if(over > 0) {
            trim += over;
        }
        if(trim > 0) {
            history.RemoveRange(0, trim);
        }
        SetVerticesDirty();
    }

    private void EnsureFunc() {
        if(func != null && funcCode == JsCode) {
            return;
        }
        DisposeFunc();
        funcCode = JsCode;
        if(string.IsNullOrWhiteSpace(JsCode)) {
            return;
        }
        try {
            var engine = MainCore.V8.Engine;
            if(engine == null) {
                return;
            }
            func = engine.Evaluate($"(function(t){{ return ({JsCode}); }})") as ScriptObject;
        } catch {
            func = null;
        }
    }

    private void DisposeFunc() {
        if(func == null) {
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
        if(rect.width <= 0f || rect.height <= 0f) {
            return;
        }

        float now = Time.realtimeSinceStartup;
        float window = Math.Max(0.01f, Window);
        float start = now - window;
        float lo = Min, hi = Max;
        if(AutoScale && history.Count > 0) {
            lo = hi = history[0].y;
            foreach(var p in history) {
                if(p.y < lo) lo = p.y;
                if(p.y > hi) hi = p.y;
            }
            if(hi - lo < 0.001f) {
                hi = lo + 1f;
            }
        } else if(hi - lo < 0.001f) {
            hi = lo + 1f;
        }

        Vector2 Map(Vector2 p) {
            float x = (p.x - start) / window * rect.width - rect.width * 0.5f;
            float y = (p.y - lo) / (hi - lo) * rect.height - rect.height * 0.5f;
            return new Vector2(x, y);
        }

        UIVertex vert = UIVertex.simpleVert;

        void Quad(Vector2 a, Vector2 b, float width, Color color) {
            Vector2 dir = b - a;
            if(dir.sqrMagnitude < 0.000001f) {
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

        if(ShowGrid) {
            Color grid = new(LineColor.r, LineColor.g, LineColor.b, LineColor.a * 0.25f);
            for(int i = 1; i < 4; i++) {
                float y = -rect.height * 0.5f + rect.height * i / 4f;
                Quad(new Vector2(-rect.width * 0.5f, y), new Vector2(rect.width * 0.5f, y), 1f, grid);
            }
            for(int i = 1; i < 6; i++) {
                float x = -rect.width * 0.5f + rect.width * i / 6f;
                Quad(new Vector2(x, -rect.height * 0.5f), new Vector2(x, rect.height * 0.5f), 1f, grid);
            }
        }

        if(history.Count == 0) {
            return;
        }

        if(ShowFill && history.Count > 1) {
            float baseY = -rect.height * 0.5f;
            for(int i = 1; i < history.Count; i++) {
                Vector2 a = Map(history[i - 1]), b = Map(history[i]);
                int baseIndex = vh.currentVertCount;
                vert.color = FillColor;
                vert.position = new Vector2(a.x, baseY); vh.AddVert(vert);
                vert.position = new Vector2(b.x, baseY); vh.AddVert(vert);
                vert.position = b; vh.AddVert(vert);
                vert.position = a; vh.AddVert(vert);
                vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
            }
        }

        if(ShowAxes) {
            Color axis = new(LineColor.r, LineColor.g, LineColor.b, LineColor.a * 0.6f);
            if(lo <= 0f && 0f <= hi) {
                float y = (0f - lo) / (hi - lo) * rect.height - rect.height * 0.5f;
                Quad(new Vector2(-rect.width * 0.5f, y), new Vector2(rect.width * 0.5f, y), 1f, axis);
            }
        }

        float half = Math.Max(0.5f, Thickness) * 0.5f;
        for(int i = 1; i < history.Count; i++) {
            Vector2 a = Map(history[i - 1]), b = Map(history[i]);
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if(len < 0.001f) {
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
            for(int i = 0; i <= capSegs; i++) {
                float a = i / (float)capSegs * MathF.PI * 2f;
                vert.position = p + new Vector2(MathF.Cos(a) * half, MathF.Sin(a) * half);
                vh.AddVert(vert);
            }
            for(int i = 1; i <= capSegs; i++) {
                vh.AddTriangle(baseIndex, baseIndex + i, baseIndex + i + 1);
            }
        }

        Cap(Map(history[0]));
        Cap(Map(history[^1]));
    }
}
