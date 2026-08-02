using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The tree drawn as the tree. Nothing here PLACES anything — position is derived from the requirements
// (UpgradeTreeLayout), so this is a picture of what you authored rather than a second place to author it.
// What it is for is the two things a flat array cannot do: show you the shape you just described, and let
// you point a requirement at a node that exists instead of spelling its id from memory.
//
// The raw array stays one toggle away. A custom inspector that hides the truth is worse than none, and the
// day this drawer has a bug the array is how you get past it.
[CustomEditor(typeof(UpgradeTreeConfig))]
public class UpgradeTreeConfigEditor : Editor
{
    const float GraphHeight = 340f;
    const float NodeRadius = 16f;

    bool _raw;
    int _selected = -1;

    static GUIStyle _caption;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Upgrade Tree", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _raw = GUILayout.Toggle(_raw, "Raw", EditorStyles.miniButton, GUILayout.Width(44));
        }

        if (_raw)
        {
            DrawDefaultInspector();   // applies on its own
            return;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"),
            new GUIContent("Id", "The character this tree belongs to — 'MC 1'. Empty falls back to the " +
                                 "asset's name, which is usually not what you want."));

        var nodes = serializedObject.FindProperty("nodes");

        // Off the live object, not the serialized copy: layout only reads ids and requirements, and the
        // object is already up to date by the time anything is drawn.
        var layout = UpgradeTreeLayout.Build((UpgradeTreeConfig)target);

        DrawGraph(nodes, layout);
        DrawTools(nodes);
        EditorGUILayout.Space();
        DrawSelected(nodes);
        DrawProblems(nodes, layout);

        serializedObject.ApplyModifiedProperties();
    }

    // ---- graph ------------------------------------------------------------------------------------

    void DrawGraph(SerializedProperty nodes, UpgradeTreeLayout layout)
    {
        var rect = GUILayoutUtility.GetRect(0, GraphHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.14f, 0.17f));

        // An empty tree drew a featureless dark box, which reads as "this panel is broken" rather than as
        // "there is nothing in here yet". Say which one it is.
        if (nodes.arraySize == 0)
        {
            var empty = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
            GUI.Label(rect, "No nodes yet.\nPress Add node below — the tree draws itself from what you add.", empty);
            return;
        }

        Vector2 centre = rect.center;
        float spacing = (Mathf.Min(rect.width, rect.height) * 0.5f - NodeRadius - 14f)
                        / Mathf.Max(1f, layout.Radius);

        HandleInput(rect, nodes, layout, centre, spacing);

        if (Event.current.type != EventType.Repaint) return;

        var previous = Handles.color;

        DrawEdges(nodes, layout, centre, spacing);
        DrawCentre(centre);
        DrawNodes(nodes, layout, centre, spacing);

        Handles.color = previous;
        GUI.Label(new Rect(rect.x + 6, rect.yMax - 18, rect.width - 12, 16),
                  "layout is derived from requirements — reorder nodes to change the arrangement",
                  EditorStyles.miniLabel);
    }

    void DrawCentre(Vector2 centre)
    {
        Handles.color = new Color(0.92f, 0.78f, 0.35f);
        Handles.DrawSolidDisc(centre, Vector3.forward, NodeRadius * 0.75f);
        GUI.Label(new Rect(centre.x - 50f, centre.y + NodeRadius, 100f, 16f), "centre", Caption);
    }

    void DrawEdges(SerializedProperty nodes, UpgradeTreeLayout layout, Vector2 centre, float spacing)
    {
        for (int i = 0; i < nodes.arraySize; i++)
        {
            if (!TryPosition(nodes, i, layout, centre, spacing, out var to)) continue;
            var requires = Field(nodes, i, "requires");

            if (requires.arraySize == 0)
            {
                Handles.color = new Color(0.92f, 0.78f, 0.35f, 0.4f);
                Handles.DrawAAPolyLine(2f, centre, to);
                continue;
            }

            for (int r = 0; r < requires.arraySize; r++)
            {
                int from = IndexOfId(nodes, requires.GetArrayElementAtIndex(r).stringValue);

                // A link to a node that is not there gets a red stub rather than nothing: a missing edge
                // looks like a tree you laid out that way, a red mark looks like the mistake it is.
                if (from < 0 || !TryPosition(nodes, from, layout, centre, spacing, out var start))
                {
                    Handles.color = new Color(0.9f, 0.35f, 0.3f, 0.9f);
                    Handles.DrawAAPolyLine(2f, to + Vector2.left * 9f, to + Vector2.right * 9f);
                    continue;
                }

                Handles.color = new Color(1f, 1f, 1f, 0.3f);
                Handles.DrawAAPolyLine(2f, start, to);
            }
        }
    }

    void DrawNodes(SerializedProperty nodes, UpgradeTreeLayout layout, Vector2 centre, float spacing)
    {
        for (int i = 0; i < nodes.arraySize; i++)
        {
            if (!TryPosition(nodes, i, layout, centre, spacing, out var p)) continue;

            Handles.color = new Color(0.28f, 0.31f, 0.38f);
            Handles.DrawSolidDisc(p, Vector3.forward, NodeRadius);

            Handles.color = i == _selected ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawWireDisc(p, Vector3.forward, NodeRadius);

            var id = Field(nodes, i, "id").stringValue;
            GUI.Label(new Rect(p.x - 50f, p.y + NodeRadius + 1f, 100f, 16f),
                      string.IsNullOrEmpty(id) ? "(no id)" : id, Caption);
        }
    }

    void HandleInput(Rect rect, SerializedProperty nodes, UpgradeTreeLayout layout, Vector2 centre, float spacing)
    {
        var e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || !rect.Contains(e.mousePosition)) return;

        _selected = -1;
        for (int i = nodes.arraySize - 1; i >= 0; i--)
            if (TryPosition(nodes, i, layout, centre, spacing, out var p)
                && Vector2.Distance(e.mousePosition, p) <= NodeRadius)
            {
                _selected = i;
                break;
            }

        GUI.FocusControl(null);   // or the last text field keeps the caret and eats the typing
        e.Use();
        Repaint();
    }

    static bool TryPosition(SerializedProperty nodes, int index, UpgradeTreeLayout layout,
                            Vector2 centre, float spacing, out Vector2 position)
    {
        position = centre;
        var id = Field(nodes, index, "id").stringValue;
        if (string.IsNullOrEmpty(id) || !layout.TryGet(id, out var slot)) return false;

        position = centre + slot.Offset * spacing;
        return true;
    }

    // ---- editing ----------------------------------------------------------------------------------

    void DrawTools(SerializedProperty nodes)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add node")) Add(nodes);

            using (new EditorGUI.DisabledScope(_selected < 0 || _selected >= nodes.arraySize))
            {
                // Order decides which sibling gets which slice of the circle, so moving a node in the array
                // is the one handle on the arrangement — hence two buttons rather than a hidden rule.
                if (GUILayout.Button("Move ◀", GUILayout.Width(70)) && _selected > 0)
                {
                    nodes.MoveArrayElement(_selected, _selected - 1);
                    _selected--;
                }
                if (GUILayout.Button("Move ▶", GUILayout.Width(70)) && _selected < nodes.arraySize - 1)
                {
                    nodes.MoveArrayElement(_selected, _selected + 1);
                    _selected++;
                }
                if (GUILayout.Button("Delete"))
                {
                    nodes.DeleteArrayElementAtIndex(_selected);
                    _selected = -1;
                }
            }
        }
    }

    void Add(SerializedProperty nodes)
    {
        int index = nodes.arraySize;
        nodes.InsertArrayElementAtIndex(index);

        // Unity copies the previous element into a new slot, so every field has to be stated — otherwise a
        // new node arrives wearing the last one's id and silently becomes a duplicate.
        Field(nodes, index, "id").stringValue = UniqueId(nodes, index);
        Field(nodes, index, "key").stringValue = "";
        Field(nodes, index, "requires").ClearArray();

        _selected = index;
    }

    static string UniqueId(SerializedProperty nodes, int ignore)
    {
        for (int n = 1; n < 999; n++)
        {
            string candidate = $"node{n}";
            if (IndexOfId(nodes, candidate, ignore) < 0) return candidate;
        }
        return Guid.NewGuid().ToString("N");
    }

    void DrawSelected(SerializedProperty nodes)
    {
        if (_selected < 0 || _selected >= nodes.arraySize)
        {
            EditorGUILayout.LabelField("Click a node to edit it.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField("Selected node", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(Field(nodes, _selected, "id"));
        EditorGUILayout.PropertyField(Field(nodes, _selected, "key"));
        DrawRequires(nodes, _selected);
    }

    // Requirements are picked, never typed. They are ids of other nodes, and a typo used to mean a node that
    // is locked forever with nothing anywhere saying why.
    void DrawRequires(SerializedProperty nodes, int index)
    {
        var requires = Field(nodes, index, "requires");

        var choices = new List<string>();
        for (int i = 0; i < nodes.arraySize; i++)
        {
            if (i == index) continue;   // a node cannot open itself
            var id = Field(nodes, i, "id").stringValue;
            if (!string.IsNullOrEmpty(id)) choices.Add(id);
        }

        EditorGUILayout.LabelField(new GUIContent("Requires",
            "Any ONE of these being bought opens this node. Empty = it grows straight off the centre."));

        for (int r = 0; r < requires.arraySize; r++)
        {
            var element = requires.GetArrayElementAtIndex(r);
            using (new EditorGUILayout.HorizontalScope())
            {
                int current = choices.IndexOf(element.stringValue);
                var labels = new List<string>(choices);

                // A value matching nothing is shown rather than reset — silently repointing somebody's link
                // because a node was renamed is worse than showing them it is broken.
                if (current < 0)
                {
                    labels.Insert(0, $"⚠ {element.stringValue}");
                    int picked = EditorGUILayout.Popup(0, labels.ToArray());
                    if (picked != 0) element.stringValue = choices[picked - 1];
                }
                else
                {
                    int picked = EditorGUILayout.Popup(current, labels.ToArray());
                    if (picked != current) element.stringValue = choices[picked];
                }

                if (GUILayout.Button("−", GUILayout.Width(22)))
                {
                    requires.DeleteArrayElementAtIndex(r);
                    break;
                }
            }
        }

        using (new EditorGUI.DisabledScope(choices.Count == 0))
            if (GUILayout.Button("Add requirement"))
            {
                requires.InsertArrayElementAtIndex(requires.arraySize);
                requires.GetArrayElementAtIndex(requires.arraySize - 1).stringValue = choices[0];
            }

        if (requires.arraySize == 0)
            EditorGUILayout.LabelField("No requirements — this one is on the first ring.", EditorStyles.miniLabel);
    }

    // ---- validation -------------------------------------------------------------------------------

    // Everything here fails silently at runtime: a duplicate id makes two nodes buy each other, a broken
    // requirement locks a branch forever, and a loop of requirements locks all of them. None of it throws.
    void DrawProblems(SerializedProperty nodes, UpgradeTreeLayout layout)
    {
        var problems = new List<string>();
        var seen = new HashSet<string>();

        for (int i = 0; i < nodes.arraySize; i++)
        {
            var id = Field(nodes, i, "id").stringValue;

            if (string.IsNullOrWhiteSpace(id)) problems.Add($"Node {i} has no id.");
            else if (!seen.Add(id)) problems.Add($"Two nodes share the id '{id}'.");

            var requires = Field(nodes, i, "requires");
            for (int r = 0; r < requires.arraySize; r++)
            {
                var target = requires.GetArrayElementAtIndex(r).stringValue;
                if (IndexOfId(nodes, target) < 0)
                    problems.Add($"'{id}' requires '{target}', which is not a node in this tree.");
            }
        }

        foreach (var id in layout.Unreachable)
            problems.Add($"'{id}' cannot be reached from the centre — its requirements loop, or lead to " +
                         "something that does not exist. It can never be bought.");

        if (problems.Count == 0) return;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
    }

    // ---- helpers ----------------------------------------------------------------------------------

    static GUIStyle Caption => _caption ??= new GUIStyle(EditorStyles.miniLabel)
    {
        alignment = TextAnchor.UpperCenter,
        normal = { textColor = new Color(0.8f, 0.82f, 0.88f) }
    };

    static SerializedProperty Field(SerializedProperty nodes, int index, string name)
        => nodes.GetArrayElementAtIndex(index).FindPropertyRelative(name);

    static int IndexOfId(SerializedProperty nodes, string id, int ignore = -1)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < nodes.arraySize; i++)
            if (i != ignore && Field(nodes, i, "id").stringValue == id) return i;
        return -1;
    }
}
