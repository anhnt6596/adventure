using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnZone))]
public class SpawnZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var areaProp = serializedObject.FindProperty("area");
        ManagedRefPicker.DrawTypeDropdown(areaProp, typeof(GridArea), "Shape");

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
