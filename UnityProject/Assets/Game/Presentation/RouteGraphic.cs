using System.Collections.Generic;
using DontGetSidetracked.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DontGetSidetracked.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RouteGraphic : MaskableGraphic
    {
        private readonly List<FixedPoint2> _points = new List<FixedPoint2>();
        public float Thickness { get; set; } = 12f;

        public void SetPoints(IEnumerable<FixedPoint2> points)
        {
            _points.Clear();
            if (points != null) _points.AddRange(points);
            SetVerticesDirty();
        }

        public void AppendPoint(FixedPoint2 point)
        {
            _points.Add(point);
            SetVerticesDirty();
        }

        public void Clear()
        {
            _points.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2) return;

            Rect rect = rectTransform.rect;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 a = ToLocal(_points[i], rect);
                Vector2 b = ToLocal(_points[i + 1], rect);
                Vector2 direction = (b - a).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x) * (Thickness * 0.5f);
                int start = vh.currentVertCount;
                vh.AddVert(a - normal, color, Vector2.zero);
                vh.AddVert(a + normal, color, Vector2.zero);
                vh.AddVert(b + normal, color, Vector2.zero);
                vh.AddVert(b - normal, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static Vector2 ToLocal(FixedPoint2 point, Rect rect)
        {
            float x = Mathf.Lerp(rect.xMin, rect.xMax, point.X / (float)FixedPoint2.Scale);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, point.Y / (float)FixedPoint2.Scale);
            return new Vector2(x, y);
        }
    }
}
