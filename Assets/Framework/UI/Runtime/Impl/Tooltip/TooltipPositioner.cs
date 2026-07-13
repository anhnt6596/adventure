using UnityEngine;

namespace Core.UI
{
    public static class TooltipPositioner
    {
        private const float Gap = 8f;

        public static (Vector2 position, TooltipAnchor resolvedPosition) Calculate(
            Rect anchorBound,
            Vector2 tooltipSize,
            Rect containerBound,
            TooltipAnchor preferred)
        {
            if (preferred == TooltipAnchor.Auto)
            {
                return TryAll(anchorBound, tooltipSize, containerBound);
            }

            var pos = ComputePosition(anchorBound, tooltipSize, preferred);
            pos = Clamp(pos, tooltipSize, containerBound);
            return (pos, preferred);
        }

        private static (Vector2, TooltipAnchor) TryAll(
            Rect anchorBound, Vector2 tooltipSize, Rect containerBound)
        {
            var order = new[]
            {
                TooltipAnchor.Above,
                TooltipAnchor.Below,
                TooltipAnchor.Right,
                TooltipAnchor.Left
            };

            foreach (var dir in order)
            {
                var pos = ComputePosition(anchorBound, tooltipSize, dir);
                if (Fits(pos, tooltipSize, containerBound))
                    return (pos, dir);
            }

            // Fallback: above, clamped
            var fallback = ComputePosition(anchorBound, tooltipSize, TooltipAnchor.Above);
            fallback = Clamp(fallback, tooltipSize, containerBound);
            return (fallback, TooltipAnchor.Above);
        }

        private static Vector2 ComputePosition(Rect anchor, Vector2 size, TooltipAnchor dir)
        {
            float x, y;

            switch (dir)
            {
                case TooltipAnchor.Above:
                    x = anchor.center.x - size.x / 2f;
                    y = anchor.yMin - size.y - Gap;
                    break;
                case TooltipAnchor.Below:
                    x = anchor.center.x - size.x / 2f;
                    y = anchor.yMax + Gap;
                    break;
                case TooltipAnchor.Left:
                    x = anchor.xMin - size.x - Gap;
                    y = anchor.center.y - size.y / 2f;
                    break;
                case TooltipAnchor.Right:
                    x = anchor.xMax + Gap;
                    y = anchor.center.y - size.y / 2f;
                    break;
                default:
                    x = anchor.center.x - size.x / 2f;
                    y = anchor.yMin - size.y - Gap;
                    break;
            }

            return new Vector2(x, y);
        }

        private static bool Fits(Vector2 pos, Vector2 size, Rect container)
        {
            return pos.x >= container.xMin
                && pos.y >= container.yMin
                && pos.x + size.x <= container.xMax
                && pos.y + size.y <= container.yMax;
        }

        private static Vector2 Clamp(Vector2 pos, Vector2 size, Rect container)
        {
            pos.x = Mathf.Clamp(pos.x, container.xMin, container.xMax - size.x);
            pos.y = Mathf.Clamp(pos.y, container.yMin, container.yMax - size.y);
            return pos;
        }
    }
}
