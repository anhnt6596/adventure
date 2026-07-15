using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PolygonZone : MonoBehaviour, IPolygon
{
    [Header("Nodes (auto-created as child GameObjects)")]
    public List<Transform> nodes = new List<Transform>();

    [Header("Drawing")]
    public bool loop = false;                        // ?�ng v�ng k�n hay kh�ng
    public Color lineColor = Color.green;
    public float nodeGizmoSize = 0.12f;

    // t?o 1 node m?i (th�m l�m child)
    public Transform AddNode(Vector3 worldPos)
    {
        GameObject go = new GameObject("Node " + nodes.Count);
        go.transform.SetParent(transform, false);
        go.transform.position = worldPos;
#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Add Polygon Node");
#endif
        nodes.Add(go.transform);
        return go.transform;
    }

    public void RemoveNodeAt(int index)
    {
        if (index < 0 || index >= nodes.Count) return;
#if UNITY_EDITOR
        UnityEditor.Undo.DestroyObjectImmediate(nodes[index].gameObject);
#else
        Destroy(nodes[index].gameObject);
#endif
        nodes.RemoveAt(index);
    }

    public void ClearNodes()
    {
#if UNITY_EDITOR
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            UnityEditor.Undo.DestroyObjectImmediate(nodes[i].gameObject);
            nodes.RemoveAt(i);
        }
#else
        foreach (var t in nodes) if (t) Destroy(t.gameObject);
        nodes.Clear();
#endif
    }

    // tr? v? copy v? tr� XY c?a polygon (d�nh cho ki?m tra point-in-polygon)
    public List<Vector2> GetPolygon2D()
    {
        var list = new List<Vector2>(nodes.Count);
        foreach (var t in nodes)
        {
            if (t == null) continue;
            list.Add(new Vector2(t.position.x, t.position.z));
        }
        return list;
    }

    public List<Vector3> GetPolygon()
    {
        var list = new List<Vector3>(nodes.Count);
        foreach (var t in nodes)
        {
            if (t == null) continue;
            list.Add(new Vector3(t.position.x, t.position.y, t.position.z));
        }
        return list;
    }

    private void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        Gizmos.color = lineColor;

        // draw nodes
        for (int i = 0; i < nodes.Count; i++)
        {
            var t = nodes[i];
            if (t == null) continue;
            Gizmos.DrawSphere(t.position, nodeGizmoSize);
        }

        // draw lines
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            if (nodes[i] == null || nodes[i + 1] == null) continue;
            Gizmos.DrawLine(nodes[i].position, nodes[i + 1].position);
        }

        if (loop && nodes.Count > 1)
        {
            var a = nodes[nodes.Count - 1];
            var b = nodes[0];
            if (a != null && b != null) Gizmos.DrawLine(a.position, b.position);
        }
    }
}
