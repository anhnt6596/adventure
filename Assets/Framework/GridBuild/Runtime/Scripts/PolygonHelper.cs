using System.Collections.Generic;
using UnityEngine;

public static class PolygonHelper
{
    public static bool PointInPoly(Vector2 point, List<Vector2> polygon)
    {
        int count = polygon.Count;
        bool inside = false;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];
            bool intersect = ((pi.y > point.y) != (pj.y > point.y)) &&
            (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y + Mathf.Epsilon) + pi.x);
            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    public static List<List<Vector3>> ToPolygonList(List<IPolygon> polygons)
    {
        var result = new List<List<Vector3>>();
        if (polygons == null) return result;

        foreach (var p in polygons)
        {
            if (p == null) continue;
            var poly = p.GetPolygon();
            if (poly == null || poly.Count < 3) continue;
            result.Add(new List<Vector3>(poly));
        }
        return result;
    }

}