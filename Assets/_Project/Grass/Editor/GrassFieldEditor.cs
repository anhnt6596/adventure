using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassField))]
public class GrassFieldEditor : Editor
{
    static bool _painting;
    static bool _erase;
    static int _brush = 3;

    GrassField _field;

    void OnEnable() => _field = (GrassField)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        _painting = GUILayout.Toggle(_painting, _painting ? "Painting (Esc to stop)" : "Paint Grass Mask", "Button", GUILayout.Height(26));

        using (new EditorGUI.DisabledScope(!_painting))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(!_erase, "Grow", "Button")) _erase = false;
                if (GUILayout.Toggle(_erase, "Erase", "Button")) _erase = true;
            }
            _brush = EditorGUILayout.IntSlider("Brush (cells)", _brush, 1, 20);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fill All"))  { Undo.RecordObject(_field, "Fill Grass"); _field.SetAllMask(255); EditorUtility.SetDirty(_field); }
            if (GUILayout.Button("Clear All")) { Undo.RecordObject(_field, "Clear Grass"); _field.SetAllMask(0); EditorUtility.SetDirty(_field); }
        }

        if (_painting)
            EditorGUILayout.HelpBox("Left drag applies the mode; Shift inverts it.", MessageType.None);
    }

    void OnSceneGUI()
    {
        if (!_painting) return;
        var e = Event.current;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { _painting = false; Repaint(); e.Use(); return; }

        var plane = new Plane(_field.transform.up, _field.transform.position);
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!plane.Raycast(ray, out float dist)) return;
        Vector3 hit = ray.GetPoint(dist);

        float radius = _brush * _field.MaskCellSize;
        bool erasing = _erase ^ e.shift;
        Handles.color = erasing ? new Color(1f, 0.4f, 0.3f, 1f) : new Color(0.4f, 1f, 0.4f, 1f);
        Handles.DrawWireDisc(hit, _field.transform.up, radius);
        SceneView.RepaintAll();

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && !e.alt)
        {
            Undo.RecordObject(_field, "Paint Grass");
            if (_field.Paint(hit, radius, erasing ? (byte)0 : (byte)255))
            {
                _field.RebuildFromEditor();
                EditorUtility.SetDirty(_field);
            }
            e.Use();
        }
    }
}
