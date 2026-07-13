using UnityEngine;
using UnityEngine.UIElements;

namespace Core.UI
{
    public class DimmerCutout : VisualElement
    {
        public float MaxAlpha { get; set; } = 0.5f;
        private Color _dimColor = new(0f, 0f, 0f, 0.5f);
        private float _cornerRadius;

        private Rect _cutout;
        private bool _hasCutout;

        public DimmerCutout()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetAlpha(float alpha)
        {
            _dimColor.a = alpha;
            MarkDirtyRepaint();
        }

        public void SetCutout(Rect rect, float cornerRadius = -1f)
        {
            _cutout = rect;
            if (cornerRadius >= 0f) _cornerRadius = cornerRadius;
            _hasCutout = true;
            MarkDirtyRepaint();
        }

        public void ClearCutout()
        {
            _hasCutout = false;
            MarkDirtyRepaint();
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            if (!_hasCutout) return base.ContainsPoint(localPoint);
            if (IsInsideRoundedRect(localPoint, _cutout, _cornerRadius)) return false;
            return base.ContainsPoint(localPoint);
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var painter = mgc.painter2D;
            var size = contentRect;

            painter.fillColor = _dimColor;
            painter.BeginPath();

            painter.MoveTo(new Vector2(0, 0));
            painter.LineTo(new Vector2(size.width, 0));
            painter.LineTo(new Vector2(size.width, size.height));
            painter.LineTo(new Vector2(0, size.height));
            painter.ClosePath();

            if (_hasCutout)
            {
                AddRoundedRectReverse(painter, _cutout, _cornerRadius);
                painter.ClosePath();
            }

            painter.Fill(FillRule.OddEven);
        }

        private static void AddRoundedRectReverse(Painter2D painter, Rect rect, float radius)
        {
            var r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            var x = rect.x;
            var y = rect.y;
            var w = rect.width;
            var h = rect.height;

            painter.MoveTo(new Vector2(x + r, y));
            painter.ArcTo(new Vector2(x, y), new Vector2(x, y + r), r);
            painter.LineTo(new Vector2(x, y + h - r));
            painter.ArcTo(new Vector2(x, y + h), new Vector2(x + r, y + h), r);
            painter.LineTo(new Vector2(x + w - r, y + h));
            painter.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w, y + h - r), r);
            painter.LineTo(new Vector2(x + w, y + r));
            painter.ArcTo(new Vector2(x + w, y), new Vector2(x + w - r, y), r);
        }

        private static bool IsInsideRoundedRect(Vector2 point, Rect rect, float radius)
        {
            if (!rect.Contains(point)) return false;

            var r = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);

            var corners = new[]
            {
                new Vector2(rect.x + r, rect.y + r),
                new Vector2(rect.xMax - r, rect.y + r),
                new Vector2(rect.x + r, rect.yMax - r),
                new Vector2(rect.xMax - r, rect.yMax - r),
            };

            for (var i = 0; i < corners.Length; i++)
            {
                var cx = corners[i].x;
                var cy = corners[i].y;

                var inCornerX = (i % 2 == 0) ? point.x < cx : point.x > cx;
                var inCornerY = (i < 2) ? point.y < cy : point.y > cy;

                if (inCornerX && inCornerY)
                {
                    var dx = point.x - cx;
                    var dy = point.y - cy;
                    return dx * dx + dy * dy <= r * r;
                }
            }

            return true;
        }
    }
}
