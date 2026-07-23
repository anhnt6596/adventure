using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGrid))]
public class TerrainGridEditor : Editor
{
    static bool _painting;
    static int _brush = 1;
    static int _brushSize = 1;

    static bool _showReplace;
    static int _replaceFrom;
    static int _replaceTo = 1;

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

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_renderer == null))
                if (GUILayout.Button("Rebuild Mesh (bake)", GUILayout.Height(24)))
                {
                    _grid.MarkDirty();
                    _renderer.Bake();
                    SceneView.RepaintAll();
                }

            if (GUILayout.Button("Bake Walkable", GUILayout.Height(24)))
                BakeWalkable();
        }
        if (_renderer == null)
            EditorGUILayout.HelpBox("No TerrainRenderer on this object — nothing to rebuild.", MessageType.None);

        var set = _grid.Set;
        if (set == null || set.Count == 0)
        {
            EditorGUILayout.HelpBox("Assign a TerrainSet to paint.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Paint", EditorStyles.boldLabel);

        bool wasPainting = _painting;
        _painting = GUILayout.Toggle(_painting, _painting ? "Painting (Esc to stop)" : "Start Painting", "Button", GUILayout.Height(28));
        if (wasPainting && !_painting) FinishPaint();   // rebuild water + bake walkable when painting stops

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
                if (layer.kind != TerrainKind.Land) label += $"  ({layer.kind})";

                bool selected = _brush == i;
                if (GUI.Toggle(button, selected, label, "Button") && !selected) _brush = i;
            }
        }

        if (_painting)
            EditorGUILayout.HelpBox("Left click / drag to paint. Shift-click paints terrain 0.", MessageType.None);

        EditorGUILayout.Space();
        _showReplace = EditorGUILayout.Foldout(_showReplace, "Replace Type (whole map)", true);
        if (_showReplace)
        {
            var names = new string[set.Count];
            for (int i = 0; i < set.Count; i++)
                names[i] = $"{i}: {(string.IsNullOrEmpty(set.layers[i].name) ? "Layer" : set.layers[i].name)}";

            _replaceFrom = EditorGUILayout.Popup("From (A)", Mathf.Clamp(_replaceFrom, 0, set.Count - 1), names);
            _replaceTo   = EditorGUILayout.Popup("To (B)",   Mathf.Clamp(_replaceTo, 0, set.Count - 1), names);

            using (new EditorGUI.DisabledScope(_replaceFrom == _replaceTo))
                if (GUILayout.Button($"Replace all  {_replaceFrom} → {_replaceTo}", GUILayout.Height(24)))
                    ReplaceAll((byte)_replaceFrom, (byte)_replaceTo);

            EditorGUILayout.HelpBox("Swaps every cell of type A to B across the whole map — for when the " +
                                    "TerrainSet order changes and ids shift.", MessageType.None);
        }
    }

    // Bulk remap: every cell painted A becomes B. One undo step; rebuilds mesh and re-bakes walkable since
    // the swap can cross the land/water boundary.
    void ReplaceAll(byte from, byte to)
    {
        if (from == to) return;

        Undo.RecordObject(_grid, "Replace Terrain Type");
        var map = _grid.Map;
        bool changed = false;

        for (int y = 0; y < _grid.Height; y++)
            for (int x = 0; x < _grid.Width; x++)
                if (map.Get(x, y) == from) { map.Set(x, y, to); changed = true; }

        if (!changed) return;

        _grid.MarkDirty();
        EditorUtility.SetDirty(_grid);
        if (_renderer != null) _renderer.Build();
        BakeWalkable();
        SceneView.RepaintAll();
    }

    void BakeWalkable()
    {
        Undo.RecordObject(_grid, "Bake Walkable");
        _grid.BakeWalkable();
        EditorUtility.SetDirty(_grid);
        SceneView.RepaintAll();
    }

    // Painting stopped: now do the deferred heavy work - rebuild the water mesh and bake the walkable
    // boundary, both skipped during the drag to keep it smooth.
    void FinishPaint()
    {
        if (_renderer != null) _renderer.Build(true);
        BakeWalkable();
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
            FinishPaint();
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
        int n = Mathf.Max(1, _brushSize);
        int lo = (n - 1) / 2;          // brush size N paints exactly N x N cells, centred on the cursor
        bool changed = false;

        for (int y = cy - lo; y < cy - lo + n; y++)
            for (int x = cx - lo; x < cx - lo + n; x++)
            {
                if (!map.InBounds(x, y) || map.Get(x, y) == id) continue;
                map.Set(x, y, id);
                changed = true;
            }

        if (!changed) return;

        _grid.MarkDirty();
        EditorUtility.SetDirty(_grid);
        if (_renderer != null) _renderer.Build(false);   // tiles only while dragging; water is the slow part
        SceneView.RepaintAll();
    }

    // previewColor names a terrain; its alpha is not meaningful and is often 0.
    static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

    void DrawBrush(int cx, int cy)
    {
        var set = _grid.Set;
        var color = _brush < set.Count ? Opaque(set.layers[_brush].previewColor) : Color.magenta;

        float cs = _grid.CellSize;
        int n = Mathf.Max(1, _brushSize);
        int lo = (n - 1) / 2;
        var tf = _grid.transform;

        Vector3 min = tf.TransformPoint(new Vector3((cx - lo) * cs, 0f, (cy - lo) * cs));
        Vector3 max = tf.TransformPoint(new Vector3((cx - lo + n) * cs, 0f, (cy - lo + n) * cs));
        Vector3 a = new Vector3(min.x, min.y, max.z);
        Vector3 b = new Vector3(max.x, max.y, min.z);

        Handles.color = color;
        Handles.DrawSolidRectangleWithOutline(new[] { min, a, max, b },
            new Color(color.r, color.g, color.b, 0.25f), color);

        SceneView.RepaintAll();
    }
}
