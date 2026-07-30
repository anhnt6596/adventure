using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Bridge))]
public class BridgeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var shapeProp = serializedObject.FindProperty("shape");
        ManagedRefPicker.DrawTypeDropdown(shapeProp, typeof(BridgeShape), "Shape");

        EditorGUILayout.PropertyField(shapeProp, true);
        DrawPropertiesExcluding(serializedObject, "m_Script", "shape");
        serializedObject.ApplyModifiedProperties();

        var bridge = (Bridge)target;
        if (bridge.ResolveGrid() == null)
        {
            EditorGUILayout.HelpBox("No TerrainGrid found — put the bridge under a map, or set the grid explicitly.",
                                    MessageType.Error);
            return;
        }

        // The only question worth asking about a bridge, and the one the scene view cannot answer: does it join
        // two stretches of ground that were separate without it?
        int joined = bridge.RegionsJoined();
        EditorGUILayout.Space();
        switch (joined)
        {
            case 0:
                EditorGUILayout.HelpBox("The deck touches no walkable ground. You cannot step onto it — it needs "
                                        + "to reach the shore at both ends.", MessageType.Error);
                break;
            case 1:
                EditorGUILayout.HelpBox("A jetty: the deck only reaches ground you could already walk to. Fine if "
                                        + "that is the intent, not a crossing.", MessageType.Warning);
                break;
            default:
                EditorGUILayout.HelpBox($"Joins {joined} stretches of ground that are separate without it.",
                                        MessageType.Info);
                break;
        }
    }
}
