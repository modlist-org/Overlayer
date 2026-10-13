using Microsoft.ClearScript;
using Overlayer.Core;
using Overlayer.Native;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Overlayer.Overlay;

#if ML && IL2CPP
using MelonLoader;
#endif

public sealed class OvGraphComponent : MaskableGraphic {
    static OvGraphComponent() {
        GraphExpressionEngine.DefaultTagLookup ??= Overlayer.Tag.Core.TagManager.TryGet;
        GraphExpressionEngine.DefaultTagTouch ??= Overlayer.Patch.Lazy.LazyPatchController.Touch;
        GraphExpressionEngine.DefaultV8Evaluator ??= code => MainCore.V8?.Engine?.Evaluate(code) as ScriptObject;
    }

#if ML && IL2CPP
    public OvGraphComponent(System.IntPtr ptr) : base(ptr) {
    }
#endif
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

    private sealed class SampleRing {
        private const int Capacity = 65536;
        private const int Mask = Capacity - 1;
        private readonly Vector2[] buf = new Vector2[Capacity];
        private int head;
        private int count;

        public int Count => count;

        public void Clear() {
            head = 0;
            count = 0;
        }

        public Vector2 this[int index] => buf[(head + index) & Mask];

        // x is pushed in increasing time order, so the ring is sorted by x.
        public int LowerBound(float x) {
            int lo = 0, hi = count;
            while (lo < hi) {
                int mid = (lo + hi) >> 1;
                if (buf[(head + mid) & Mask].x < x) {
                    lo = mid + 1;
                } else {
                    hi = mid;
                }
            }
            return lo;
        }

        public void Push(Vector2 pt) {
            if (count < Capacity) {
                buf[(head + count) & Mask] = pt;
                count++;
            } else {
                buf[head] = pt;
                head = (head + 1) & Mask;
            }
        }

        public void TrimBefore(float cutoff) {
            while (count > 0 && buf[head].x < cutoff) {
                head = (head + 1) & Mask;
                count--;
            }
        }
    }

    private readonly SampleRing history = new();
    private readonly GraphExpressionEngine engine = new();
    private string funcCode;
    private float lastPushTime = float.NegativeInfinity;
    private Native.OvPoint2D[] _pointBuffer = new Native.OvPoint2D[256];
    private Native.OvQuad2D[] _quadBuffer = new Native.OvQuad2D[256];

    #if ML && IL2CPP
    public
#else
    protected
#endif
    override void OnEnable() {
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
        if (!engine.Evaluate(now, out float value)) {
            return;
        }
        history.Push(new Vector2(now, value));
        float keep = Math.Max(Math.Max(0.01f, Window), 60f);
        history.TrimBefore(now - keep);
        SetVerticesDirty();
    }

    private void EnsureFunc() {
        if (funcCode == JsCode) {
            return;
        }
        funcCode = JsCode;
        engine.Compile(JsCode);
    }

    private void DisposeFunc() {
        engine.Dispose();
        funcCode = null;
    }

    #if ML && IL2CPP
    public
#else
    protected
#endif
    override void OnDisable() {
        DisposeFunc();
    }

    #if ML && IL2CPP
    public
#else
    protected
#endif
    override void OnDestroy() {
        DisposeFunc();
    }


    #if ML && IL2CPP
    public
#else
    protected
#endif
    override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) {
            return;
        }

        float now = Time.realtimeSinceStartup;
        float window = Math.Max(0.01f, Window);
        float start = now - window;
        int first = history.LowerBound(start);
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

        // Copy history once for both geometry passes, including the preceding
        // sample needed to clip the fill at the left edge of the window.
        int startIdx = Math.Max(0, first - 1);
        int bufferCount = history.Count - startIdx;
        if (_pointBuffer.Length < bufferCount) {
            _pointBuffer = new Native.OvPoint2D[Math.Max(_pointBuffer.Length * 2, bufferCount)];
            _quadBuffer = new Native.OvQuad2D[_pointBuffer.Length];
        }
        for (int i = 0; i < bufferCount; i++) {
            var p = history[startIdx + i];
            _pointBuffer[i] = new Native.OvPoint2D { X = p.x, Y = p.y };
        }
        float scaleX = rect.width / window;
        float scaleY = rect.height / (hi - lo);
        var xform = new Native.OvMapTransform {
            ScaleX = scaleX,
            OffsetX = rect.xMin - start * scaleX,
            ScaleY = scaleY,
            OffsetY = rect.yMin - lo * scaleY,
            XMin = rect.xMin,
        };

        if (ShowFill) {
            int fillCount = Native.OvVertexBuilder.BuildGraphFillQuads(
                _pointBuffer.AsSpan(0, bufferCount), xform, rect.yMin, _quadBuffer);
            for (int i = 0; i < fillCount; i++) {
                ref readonly var quad = ref _quadBuffer[i];
                int baseIndex = vh.currentVertCount;
                vert.color = FillColor;
                vert.position = new Vector3(quad.P0.X, quad.P0.Y); vh.AddVert(vert);
                vert.position = new Vector3(quad.P1.X, quad.P1.Y); vh.AddVert(vert);
                vert.position = new Vector3(quad.P2.X, quad.P2.Y); vh.AddVert(vert);
                vert.position = new Vector3(quad.P3.X, quad.P3.Y); vh.AddVert(vert);
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

        int ptCount = history.Count - first;
        if (ptCount >= 2) {
            int quadCount = Native.OvVertexBuilder.BuildGraphQuads(
                _pointBuffer.AsSpan(first - startIdx, ptCount),
                xform,
                half,
                _quadBuffer
            );

            if (quadCount > 0) {
                drawStart = new Vector2((_quadBuffer[0].P0.X + _quadBuffer[0].P1.X) * 0.5f, (_quadBuffer[0].P0.Y + _quadBuffer[0].P1.Y) * 0.5f);
            }

            for (int q = 0; q < quadCount; q++) {
                ref readonly var quad = ref _quadBuffer[q];
                int baseIndex = vh.currentVertCount;
                vert.color = LineColor;
                vert.position = new Vector3(quad.P0.X, quad.P0.Y); vh.AddVert(vert);
                vert.position = new Vector3(quad.P1.X, quad.P1.Y); vh.AddVert(vert);
                vert.position = new Vector3(quad.P2.X, quad.P2.Y); vh.AddVert(vert);
                vert.position = new Vector3(quad.P3.X, quad.P3.Y); vh.AddVert(vert);
                vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
            }
        }

        void Cap(Vector2 p) {
            int baseIndex = vh.currentVertCount;
            vert.color = LineColor;
            const int capSegs = 6;
            vert.position = p; vh.AddVert(vert);
            for (int i = 0; i <= capSegs; i++) {
                float a = i / (float)capSegs * (float)Math.PI * 2f;
                vert.position = p + new Vector2(Mathf.Cos(a) * half, Mathf.Sin(a) * half);
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
