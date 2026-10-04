using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using MelonLoader;
#endif

namespace Overlayer.UI.Factory.Page;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
internal sealed class DiagonalTileGraphic
#if ML && IL2CPP
    (System.IntPtr ptr) : MaskableGraphic(ptr)
#else
    : MaskableGraphic
#endif
{
    public bool RightHalf;

    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if(r.width <= 0f || r.height <= 0f) {
            return;
        }

        const float bottomSplit = 0.28f;
        const float topSplit = 0.72f;
        const float gap = 14f;
        const float radius = 6.25f;
        float bottom = r.xMin + r.width * bottomSplit;
        float top = r.xMin + r.width * topSplit;
        float halfGap = gap * 0.5f;
        var points = new List<Vector2>(20);
        if(RightHalf) {
            points.Add(new Vector2(bottom + halfGap, r.yMin));
            points.Add(new Vector2(r.xMax - radius, r.yMin));
            AddArc(points, new Vector2(r.xMax - radius, r.yMin + radius), radius, -90f, 0f, 5);
            points.Add(new Vector2(r.xMax, r.yMax - radius));
            AddArc(points, new Vector2(r.xMax - radius, r.yMax - radius), radius, 0f, 90f, 5);
            points.Add(new Vector2(top + halfGap, r.yMax));
        } else {
            points.Add(new Vector2(r.xMin + radius, r.yMin));
            points.Add(new Vector2(bottom - halfGap, r.yMin));
            points.Add(new Vector2(top - halfGap, r.yMax));
            points.Add(new Vector2(r.xMin + radius, r.yMax));
            AddArc(points, new Vector2(r.xMin + radius, r.yMax - radius), radius, 90f, 180f, 5);
            points.Add(new Vector2(r.xMin, r.yMin + radius));
            AddArc(points, new Vector2(r.xMin + radius, r.yMin + radius), radius, 180f, 270f, 5);
        }

        Vector2 center = Vector2.zero;
        foreach(var point in points) {
            center += point;
        }
        center /= points.Count;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = center;
        vh.AddVert(v);
        foreach(var point in points) {
            v.position = point;
            vh.AddVert(v);
        }
        for(int i = 0; i < points.Count; i++) {
            int current = i + 1;
            int next = (i + 1) % points.Count + 1;
            vh.AddTriangle(0, current, next);
        }
    }

    private static void AddArc(List<Vector2> points, Vector2 center, float radius, float start, float end, int segments) {
        for(int i = 1; i <= segments; i++) {
            float angle = Mathf.Lerp(start, end, i / (float)segments) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
