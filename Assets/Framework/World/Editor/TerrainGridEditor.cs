using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGrid))]
public class TerrainGridEditor : Editor
{
    static bool _painting;
    static int _brush = 1;
    static int _brushSize = 1;

    TerrainGrid _grid;
    TerrainRenderer _renderer;

    void OnEnable()
    {
        _grid = (TerrainGrid)target;
        _renderer = _grid.GetComponent<TerrainRenderer>();
        Undo.undoRedoPerformed += OnUndo;
    }

    void OnDisable() => Undo.undoRedoPerformed -= OnUndo;

    void OnUndo()
    {
        if (_grid == null) return;
        _grid.MarkDirty();
        if (_renderer != null) _renderer.Build();
        SceneView.RepaintAll();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var set = _grid.Set;
        if (set == null || set.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a TerrainSet to paint.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Paint", EditorStyles.boldLabel);

        _painting = GUILayout.Toggle(_painting, _painting ? "Painting (Esc to stop)" : "Start Painting", "Button", GUILayout.Height(28));

        using (new EditorGUI.DisabledScope(!_painting))
        {
            _brushSize = EditorGUILayout.IntSlider("Brush Size", _brushSize, 1, 12);

            EditorGUILayout.LabelField("Terrain");
            for (int i = 0; i < set.Count; i++)
            {
                var layer = set.layers[i];
                var rect = EditorGUILayout.GetControlRect(GUILayout.Height(22));

                var swatch = new Rect(rect.x, rect.y, 22, rect.height);
                EditorGUI.DrawRect(swatch, Opaque(layer.previewColor));

                var button = new Rect(rect.x + 26, rect.y, rect.width - 26, rect.height);
                string label = string.IsNullOrEmpty(layer.name) ? $"Layer {i}" : layer.name;
                if (!layer.walkable) label += "  (blocks)";

                bool selected = _brush == i;
                if (GUI.Toggle(button, selected, label, "Button") && !selected) _brush = i;
            }
        }

        if (_painting)
            EditorGUILayout.HelpBox("Left click / drag to paint. Shift-click paints terrain 0.", MessageType.None);
    }

    void OnSceneGUI()
    {
        if (!_painting) return;

        var e = Event.current;

        // Takes the scene's default click so painting does not fight object selection.
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            _painting = false;
            Repaint();
            e.Use();
            return;
        }

        if (!TryPickCell(e.mousePosition, out int cx, out int cy))
            return;

        DrawBrush(cx, cy);

        bool paint = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt;
        if (!paint) return;

        Paint(cx, cy, e.shift ? (byte)0 : (byte)_brush);
        e.Use();
    }

    bool TryPickCell(Vector2 mouse, out int cx, out int cy)
    {
        cx = cy = 0;
        var tf = _grid.transform;
        var plane = new Plane(tf.up, tf.position);
        var ray = HandleUtility.GUIPointToWorldRay(mouse);
        if (!plane.Raycast(ray, out float dist)) return false;

        _grid.WorldToCell(ray.GetPoint(dist), out cx, out cy);
        return true;
    }

    void Paint(int cx, int cy, byte id)
    {
        Undo.RecordObject(_grid, "Paint Terrain");

        var map = _grid.Map;
        int r = _brushSize - 1;
        bool changed = false;

        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!map.InBounds(x, y) || map.Get(x, y) == id) continue;
                map.Set(x, y, id);
                changed = true;
            }

        if (!changed) return;

        _grid.MarkDirty();
        EditorUtility.SetDirty(_grid);
        if (_renderer != null) _renderer.Build();
        SceneView.RepaintAll();
    }

    // previewColor names a terrain; its alpha is not meaningful and is often 0.
    static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

    void DrawBrush(int cx, int cy)
    {
        var set = _grid.Set;
        var color = _brush < set.Count ? Opaque(set.layers[_brush].previewColor) : Color.magenta;

        float cs = _grid.CellSize;
        int r = _brushSize - 1;
        var tf = _grid.transform;

        Vector3 min = tf.TransformPoint(new Vector3((cx - r) * cs, 0f, (cy - r) * cs));
        Vector3 max = tf.TransformPoint(new Vector3((cx + r + 1) * cs, 0f, (cy + r + 1) * cs));
        Vector3 a = new Vector3(min.x, min.y, max.z);
        Vector3 b = new Vector3(max.x, max.y, min.z);

        Handles.color = color;
        Handles.DrawSolidRectangleWithOutline(new[] { min, a, max, b },
            new Color(color.r, color.g, color.b, 0.25f), color);

        SceneView.RepaintAll();
    }
}
