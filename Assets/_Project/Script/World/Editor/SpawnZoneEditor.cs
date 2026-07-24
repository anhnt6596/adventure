using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnZone))]
public class SpawnZoneEditor : Editor
{
    enum Shape { Circle, Box }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Shape switcher for the [SerializeReference] area: swap the concrete type in place. Unity has no
        // built-in picker for a managed reference, so this drives it.
        var areaProp = serializedObject.FindProperty("area");
        bool isBox = areaProp.managedReferenceValue is BoxArea;
        var picked = (Shape)EditorGUILayout.EnumPopup("Shape", isBox ? Shape.Box : Shape.Circle);
        if ((picked == Shape.Box) != isBox)
            areaProp.managedReferenceValue = picked == Shape.Box ? new BoxArea() : (SpawnArea)new CircleArea();

        EditorGUILayout.PropertyField(areaProp, true);                       // the shape's own fields
        DrawPropertiesExcluding(serializedObject, "m_Script", "area");       // grid, clearance, spawning config
        serializedObject.ApplyModifiedProperties();

        var zone = (SpawnZone)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Bake Spawn Cells", GUILayout.Height(26)))
        {
            zone.Bake();
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox(
            zone.CellCount > 0
                ? $"{zone.CellCount} spawnable cells baked. Select the zone to see them."
                : "No cells baked — place the zone over walkable land, then press Bake.",
            zone.CellCount > 0 ? MessageType.Info : MessageType.Warning);
    }
}
