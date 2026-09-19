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
        private readonly List<Vector2> _localPoints = new List<Vector2>();
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

            BuildLocalPoints(rectTransform.rect);
            if (_localPoints.Count < 2) return;

            float halfWidth = Mathf.Max(1f, Thickness * 0.5f);

            // Build one continuous strip. The old implementation emitted an independent
            // quad for every sample; dense curved routes exposed the open quad corners as
            // a saw-tooth edge (especially once a UI Shadow duplicated the mesh).
            for (int i = 0; i < _localPoints.Count; i++)
            {
                Vector2 offset = JoinOffset(i, halfWidth);
                vh.AddVert(_localPoints[i] - offset, color, Vector2.zero);
                vh.AddVert(_localPoints[i] + offset, color, Vector2.zero);
            }

            for (int i = 0; i < _localPoints.Count - 1; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                vh.AddTriangle(a, b, d);
                vh.AddTriangle(a, d, c);
            }

            AddRoundCap(vh, _localPoints[0], halfWidth);
            AddRoundCap(vh, _localPoints[_localPoints.Count - 1], halfWidth);
        }

        private void BuildLocalPoints(Rect rect)
        {
            _localPoints.Clear();

            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 point = ToLocal(_points[i], rect);
                if (_localPoints.Count > 0 &&
                    (point - _localPoints[_localPoints.Count - 1]).sqrMagnitude < 0.01f)
                    continue;

                _localPoints.Add(point);
            }
        }

        private Vector2 JoinOffset(int index, float halfWidth)
        {
            int last = _localPoints.Count - 1;
            if (index <= 0)
                return Perpendicular(SafeDirection(_localPoints[1] - _localPoints[0])) * halfWidth;
            if (index >= last)
                return Perpendicular(SafeDirection(_localPoints[last] - _localPoints[last - 1])) * halfWidth;

            Vector2 previous = SafeDirection(_localPoints[index] - _localPoints[index - 1]);
            Vector2 next = SafeDirection(_localPoints[index + 1] - _localPoints[index]);
            Vector2 previousNormal = Perpendicular(previous);
            Vector2 nextNormal = Perpendicular(next);
            Vector2 miter = previousNormal + nextNormal;

            // A near-180-degree reversal has no stable miter. Falling back to the next
            // segment normal keeps the mesh finite and visually predictable.
            if (miter.sqrMagnitude < 0.0001f)
                return nextNormal * halfWidth;

            miter.Normalize();
            float denominator = Mathf.Abs(Vector2.Dot(miter, nextNormal));
            float length = halfWidth / Mathf.Max(0.42f, denominator);
            length = Mathf.Min(length, halfWidth * 2.15f);
            return miter * length;
        }

        private void AddRoundCap(VertexHelper vh, Vector2 center, float radius)
        {
            const int segments = 12;
            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);

            int firstRing = vh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(p, color, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int current = firstRing + i;
                int next = firstRing + ((i + 1) % segments);
                vh.AddTriangle(centerIndex, current, next);
            }
        }

        private static Vector2 SafeDirection(Vector2 value)
        {
            if (value.sqrMagnitude < 0.0001f) return Vector2.right;
            return value.normalized;
        }

        private static Vector2 Perpendicular(Vector2 direction) =>
            new Vector2(-direction.y, direction.x);

        private static Vector2 ToLocal(FixedPoint2 point, Rect rect)
        {
            float x = Mathf.Lerp(rect.xMin, rect.xMax, point.X / (float)FixedPoint2.Scale);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, point.Y / (float)FixedPoint2.Scale);
            return new Vector2(x, y);
        }
    }
}
