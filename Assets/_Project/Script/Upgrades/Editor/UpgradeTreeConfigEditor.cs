using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The tree drawn as the tree, and the place it is arranged. Three things a flat array cannot do: show the
// shape you just described, let you DRAG a node to where it belongs, and let you point a requirement at a
// node that exists instead of spelling its id from memory.
//
// POSITIONS ARE AUTHORED, and dragging writes them straight onto the node in tree units — the same units the
// popup scales at runtime, so what you arrange here is what the player sees. Auto Arrange fills them all in
// from the requirements for anyone who would rather not place a tree by hand, and is a starting point rather
// than a mode: drag anything afterwards and it stays dragged.
//
// The raw array stays one toggle away. A custom inspector that hides the truth is worse than none, and the
// day this drawer has a bug the array is how you get past it.
[CustomEditor(typeof(UpgradeTreeConfig))]
public class UpgradeTreeConfigEditor : Editor
{
    const float GraphHeight = 420f;

    // How much of a tree unit a node takes up, taken from the popup so the two cannot drift. This is what
    // makes the preview worth trusting: if two nodes touch here they touch in game.
    static float NodeUnits => UpgradePopup.NodeSize / UpgradePopup.RingSpacing;

    bool _raw;
    int _selected = -1;
    int _dragging = -1;

    // Pixels per tree unit. NOT fitted to the panel any more: a view that silently rescales itself cannot be
    // used to judge whether anything is too close to anything else, which is most of what arranging a tree
    // is. Fixed scale, scroll to see the rest.
    float _zoom = 130f;
    Vector2 _scroll;

    // The canvas size a drag began at — see DrawGraph, where it is what stops a drag fighting itself.
    Vector2 _latchedSize;

    static GUIStyle _caption;
    static ArtProvider _art;

    // ArtProvider caches misses so a missing file costs one failed load rather than one per repaint — which
    // is right at runtime and wrong here, where you drop the file in WHILE looking at the panel. Reselecting
    // the asset is the refresh.
    void OnEnable() => _art = null;

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

        // Only for "what can be reached" and for Auto Arrange — the drawing itself reads the authored
        // positions. Off the live object, which is already up to date by the time anything is drawn.
        var layout = UpgradeTreeLayout.Build((UpgradeTreeConfig)target);

        DrawGraph(nodes);
        DrawTools(nodes, layout);
        EditorGUILayout.Space();
        DrawSelected(nodes);
        DrawProblems(nodes, layout);

        serializedObject.ApplyModifiedProperties();
    }

    // ---- graph ------------------------------------------------------------------------------------

    void DrawGraph(SerializedProperty nodes)
    {
        if (nodes.arraySize == 0)
        {
            var box = GUILayoutUtility.GetRect(0, 120f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(box, new Color(0.13f, 0.14f, 0.17f));
            var empty = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
            GUI.Label(box, "No nodes yet.\nPress Add node below.", empty);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(38));
            _zoom = GUILayout.HorizontalSlider(_zoom, 50f, 260f);
            if (GUILayout.Button("1:1", EditorStyles.miniButton, GUILayout.Width(34)))
                _zoom = UpgradePopup.RingSpacing;   // exactly what the popup draws at
        }

        float radius = _zoom * NodeUnits * 0.5f;
        float margin = radius + 26f;

        // Symmetric about the centre, like the popup — the character is the thing that should always be in
        // the middle, whichever way the tree happens to lean.
        Vector2 extent = Vector2.zero;
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var p = Field(nodes, i, "position").vector2Value;
            extent = Vector2.Max(extent, new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)));
        }
        Vector2 size = extent * (2f * _zoom) + Vector2.one * (margin * 2f);

        // A canvas measured from the nodes CANNOT be resized under a drag. It is symmetric about the centre
        // and its top-left is pinned by the layout, so growing it moves the centre — and the centre is what a
        // dragged position is measured from, so the node moves, so the canvas resizes again. Dragging a node
        // past the edge and back sat in exactly that loop and flickered.
        //
        // So a drag latches the canvas: it may only GROW, and the centre stays the point it was when the drag
        // started, which makes the node follow the cursor one to one. Growth therefore all lands on the right
        // and below; a node dragged up or left simply leaves the box for a moment. Releasing drops the latch
        // and the next repaint claims the tight, re-centred size.
        if (_dragging >= 0) size = Vector2.Max(size, _latchedSize);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(GraphHeight));

        var rect = GUILayoutUtility.GetRect(size.x, size.y,
                                            GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.14f, 0.17f));

        Vector2 origin = _dragging >= 0 ? rect.position + _latchedSize * 0.5f : rect.center;
        HandleInput(rect, nodes, origin, _zoom, radius);

        if (Event.current.type == EventType.Repaint)
        {
            var previous = Handles.color;
            DrawEdges(nodes, origin, _zoom);
            DrawCentre(origin, radius);
            DrawNodes(nodes, origin, _zoom, radius);
            Handles.color = previous;
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Label("drag a node to place it — scale is the game's, so what touches here touches there",
                        EditorStyles.miniLabel);
    }

    void DrawCentre(Vector2 centre, float radius)
    {
        Handles.color = new Color(0.92f, 0.78f, 0.35f);
        Handles.DrawSolidDisc(centre, Vector3.forward, radius);

        // The same portrait the popup puts there, found the same way, so a missing one is missing in both.
        var portrait = Art.Avatar(((UpgradeTreeConfig)target).Id);
        if (portrait != null) DrawSprite(portrait, centre, radius * 1.2f);

        GUI.Label(new Rect(centre.x - 60f, centre.y + radius + 2f, 120f, 16f), "centre", Caption);
    }

    void DrawEdges(SerializedProperty nodes, Vector2 origin, float spacing)
    {
        for (int i = 0; i < nodes.arraySize; i++)
        {
            Vector2 to = Position(nodes, i, origin, spacing);
            var requires = Field(nodes, i, "requires");

            if (requires.arraySize == 0)
            {
                Handles.color = new Color(0.92f, 0.78f, 0.35f, 0.4f);
                Handles.DrawAAPolyLine(2f, origin, to);
                continue;
            }

            for (int r = 0; r < requires.arraySize; r++)
            {
                int from = IndexOfId(nodes, requires.GetArrayElementAtIndex(r).stringValue);

                // A link to a node that is not there gets a red stub rather than nothing: a missing edge
                // looks like a tree you laid out that way, a red mark looks like the mistake it is.
                if (from < 0)
                {
                    Handles.color = new Color(0.9f, 0.35f, 0.3f, 0.9f);
                    Handles.DrawAAPolyLine(2f, to + Vector2.left * 9f, to + Vector2.right * 9f);
                    continue;
                }

                Handles.color = new Color(1f, 1f, 1f, 0.3f);
                Handles.DrawAAPolyLine(2f, Position(nodes, from, origin, spacing), to);
            }
        }
    }

    void DrawNodes(SerializedProperty nodes, Vector2 origin, float spacing, float radius)
    {
        var treeId = ((UpgradeTreeConfig)target).Id;

        for (int i = 0; i < nodes.arraySize; i++)
        {
            Vector2 p = Position(nodes, i, origin, spacing);

            Handles.color = new Color(0.28f, 0.31f, 0.38f);
            Handles.DrawSolidDisc(p, Vector3.forward, radius);

            // The node's own icon, resolved through ArtProvider — the SAME lookup the game makes, including
            // the per-character override and the shared fallback. Anything the editor can find here, the
            // game can load; anything missing here is missing there too, which is the point of not going
            // through AssetDatabase for this.
            var key = Field(nodes, i, "key").stringValue;
            var icon = Art.UpgradeIcon(treeId, key);
            if (icon != null) DrawSprite(icon, p, radius * 1.15f);

            Handles.color = i == _selected ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawWireDisc(p, Vector3.forward, radius);

            // Same rule as the popup: the number only appears when it is not the usual one.
            int cost = Field(nodes, i, "cost").intValue;
            if (cost > 1)
                GUI.Label(new Rect(p.x - radius, p.y + radius - 16f, radius * 2f, 16f), cost.ToString(), Caption);

            var id = Field(nodes, i, "id").stringValue;
            GUI.Label(new Rect(p.x - 60f, p.y + radius + 2f, 120f, 16f),
                      string.IsNullOrEmpty(id) ? "(no id)" : id, Caption);
        }
    }

    // A sprite may be a slice of a bigger texture, so it cannot just be blitted whole — the tex coords come
    // off the sprite's own rect. No readable texture required, unlike AssetPreview.
    static void DrawSprite(Sprite sprite, Vector2 centre, float size)
    {
        var tex = sprite.texture;
        if (tex == null) return;

        var tr = sprite.textureRect;
        var coords = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

        // Fit the longer side, so a tall or wide icon keeps its shape instead of being squashed to a square.
        float aspect = tr.width / Mathf.Max(1f, tr.height);
        float w = aspect >= 1f ? size : size * aspect;
        float h = aspect >= 1f ? size / aspect : size;

        GUI.DrawTextureWithTexCoords(new Rect(centre.x - w * 0.5f, centre.y - h * 0.5f, w, h), tex, coords);
    }

    // One instance, reused: it caches what it finds (and what it does not), so scrubbing a tree does not hit
    // Resources once per node per repaint.
    static ArtProvider Art => _art ??= new ArtProvider();

    void HandleInput(Rect rect, SerializedProperty nodes, Vector2 origin, float spacing, float radius)
    {
        var e = Event.current;

        // Ending the drag comes first, and off rawType: a button released outside the window never arrives as
        // a MouseUp, and a drag nobody ended would hold the canvas latched at whatever size it had.
        if (_dragging >= 0 && e.rawType == EventType.MouseUp)
        {
            _dragging = -1;
            if (e.type == EventType.MouseUp) e.Use();
            Repaint();   // the latch is off — claim the tight size now, not on whatever repaint comes next
            return;
        }

        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                _selected = HitTest(nodes, e.mousePosition, origin, spacing, radius);
                _dragging = _selected;
                _latchedSize = rect.size;   // what the canvas may not shrink below until the drag ends
                GUI.FocusControl(null);   // or the last text field keeps the caret and eats the typing
                e.Use();
                Repaint();
                break;

            case EventType.MouseDrag when _dragging >= 0 && _dragging < nodes.arraySize:
                // Back out of pixels into tree units, rounded so a placement is a number somebody could
                // also have typed. Shift for finer work.
                Vector2 units = (e.mousePosition - origin) / spacing;
                float step = e.shift ? 0.05f : 0.25f;
                Field(nodes, _dragging, "position").vector2Value =
                    new Vector2(Mathf.Round(units.x / step) * step, Mathf.Round(units.y / step) * step);
                e.Use();
                Repaint();
                break;
        }
    }

    static int HitTest(SerializedProperty nodes, Vector2 mouse, Vector2 origin, float spacing, float radius)
    {
        for (int i = nodes.arraySize - 1; i >= 0; i--)   // topmost first, matching the draw order
            if (Vector2.Distance(mouse, Position(nodes, i, origin, spacing)) <= radius)
                return i;
        return -1;
    }

    static Vector2 Position(SerializedProperty nodes, int index, Vector2 origin, float spacing)
        => origin + Field(nodes, index, "position").vector2Value * spacing;

    // ---- editing ----------------------------------------------------------------------------------

    void DrawTools(SerializedProperty nodes, UpgradeTreeLayout layout)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add node")) Add(nodes);

            // A starting point, not a mode. It overwrites every position, so it is the thing you press on a
            // fresh tree or when an arrangement has got away from you — and anything dragged afterwards
            // stays where it was put.
            using (new EditorGUI.DisabledScope(nodes.arraySize == 0))
                if (GUILayout.Button("Auto arrange") && Confirm(nodes.arraySize))
                    AutoArrange(nodes, layout);

            using (new EditorGUI.DisabledScope(_selected < 0 || _selected >= nodes.arraySize))
            {
                if (GUILayout.Button(new GUIContent("Clone",
                        "A copy beside the original, keeping its requirements — a sibling on the same branch."),
                        GUILayout.Width(58)))
                    Clone(nodes, _selected, requireSource: false);

                if (GUILayout.Button(new GUIContent("Clone next",
                        "A copy one ring further out that REQUIRES the original, replacing the requirements " +
                        "it copied — the next step along this branch."),
                        GUILayout.Width(74)))
                    Clone(nodes, _selected, requireSource: true);

                if (GUILayout.Button("Delete", GUILayout.Width(58)))
                {
                    nodes.DeleteArrayElementAtIndex(_selected);
                    _selected = -1;
                }
            }
        }
    }

    static bool Confirm(int count)
        => EditorUtility.DisplayDialog("Auto arrange",
            $"Reposition all {count} nodes from their requirements? Anything placed by hand is lost.",
            "Arrange", "Cancel");

    static void AutoArrange(SerializedProperty nodes, UpgradeTreeLayout layout)
    {
        for (int i = 0; i < nodes.arraySize; i++)
        {
            var id = Field(nodes, i, "id").stringValue;
            if (!string.IsNullOrEmpty(id) && layout.TryGet(id, out var slot))
                Field(nodes, i, "position").vector2Value = slot.Offset;
        }
    }

    void Add(SerializedProperty nodes)
    {
        int index = nodes.arraySize;
        nodes.InsertArrayElementAtIndex(index);

        // Unity copies the previous element into a new slot, so every field has to be stated — otherwise a
        // new node arrives wearing the last one's id and silently becomes a duplicate.
        var id = UniqueId(nodes, index);
        Field(nodes, index, "id").stringValue = id;
        Field(nodes, index, "key").stringValue = "";
        Field(nodes, index, "cost").intValue = 1;
        Field(nodes, index, "requires").ClearArray();
        Field(nodes, index, "effect").managedReferenceValue = null;

        // Pushed through before asking the layout anything: it reads the live object, and until this lands
        // the new node does not exist out there to be given a place.
        nodes.serializedObject.ApplyModifiedProperties();

        // Somewhere sensible rather than on top of the centre, where it would be invisible under whatever is
        // already there. With no requirements yet the layout treats it as another first-ring node and spaces
        // it out with them.
        var guess = UpgradeTreeLayout.Build((UpgradeTreeConfig)nodes.serializedObject.targetObject);
        Field(nodes, index, "position").vector2Value =
            guess.TryGet(id, out var slot) ? slot.Offset : new Vector2(0f, -1f);

        _selected = index;
    }

    // The rest of a tree is mostly the last node again: the same buff one ring further out, the same shape on
    // another branch. So everything comes across, THE ID INCLUDED — a clone is renamed on the spot, and
    // editing 'attack-2' into 'attack-3' is less work than typing a name from nothing. Until it is renamed the
    // problems box says two nodes share an id, which is the reminder rather than a fault.
    //
    // requireSource is the difference between the two buttons: off, the copy is a sibling and keeps the
    // requirements it was cloned with; on, it hangs off the original instead and becomes the next step along
    // that branch.
    void Clone(SerializedProperty nodes, int index, bool requireSource)
    {
        // Read before inserting: afterwards there are two elements holding this and no reason to trust which.
        var sourceId = Field(nodes, index, "id").stringValue;
        var sourcePosition = Field(nodes, index, "position").vector2Value;

        nodes.InsertArrayElementAtIndex(index);   // Unity's insert leaves a copy of that element behind it
        int clone = index + 1;

        if (requireSource)
        {
            var requires = Field(nodes, clone, "requires");
            requires.ClearArray();   // replaced, not added to: following one node is one link, not two
            requires.InsertArrayElementAtIndex(0);
            requires.GetArrayElementAtIndex(0).stringValue = sourceId;

            // Sharing the id makes this read as a node requiring itself for as long as the rename is
            // outstanding. It points at the original the moment the rename lands, which is the whole trick —
            // the link is stored as the name the original keeps.
            Field(nodes, clone, "position").vector2Value = StepOut(sourcePosition);
        }
        else
        {
            // A node's width away, rather than exactly on top where it would be invisible under the original.
            Field(nodes, clone, "position").vector2Value = sourcePosition + Vector2.one * NodeUnits;
        }

        // The insert copies the REFERENCE to the effect, so both nodes would drive one object and editing
        // either would edit both. A round trip through JSON is what makes the clone's a separate instance.
        var effect = Field(nodes, index, "effect").managedReferenceValue;
        Field(nodes, clone, "effect").managedReferenceValue =
            effect == null ? null : JsonUtility.FromJson(JsonUtility.ToJson(effect), effect.GetType());

        _selected = clone;
    }

    // One ring further from the centre, along the line the original already sits on — where the node that
    // follows it belongs. A node sitting ON the centre gives no such line, so it starts the way a new one does.
    static Vector2 StepOut(Vector2 from)
        => from.sqrMagnitude > 1e-6f ? from + from.normalized : new Vector2(0f, -1f);

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

        // The key and what it currently resolves to, side by side. Naming art by convention only works if
        // you can see whether the convention was met, and the alternative is playing the game to find out.
        var key = Field(nodes, _selected, "key");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(key);

            var found = Art.UpgradeIcon(((UpgradeTreeConfig)target).Id, key.stringValue);
            var slot = GUILayoutUtility.GetRect(34f, 34f, GUILayout.Width(34f), GUILayout.Height(34f));
            if (found != null) DrawSprite(found, slot.center, 32f);
            else EditorGUI.DrawRect(slot, new Color(1f, 1f, 1f, 0.05f));
        }

        if (!string.IsNullOrEmpty(key.stringValue)
            && Art.UpgradeIcon(((UpgradeTreeConfig)target).Id, key.stringValue) == null)
            EditorGUILayout.LabelField($"no icon at Resources/{ArtProvider.UpgradeFolder}/{key.stringValue}",
                                       EditorStyles.miniLabel);

        EditorGUILayout.PropertyField(Field(nodes, _selected, "cost"));
        DrawRequires(nodes, _selected);
        DrawEffect(nodes, _selected);
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

    // What the node does. The type dropdown comes from ManagedRefPicker, the same reflection the enemy
    // brains use, so a new IUpgradeEffect class shows up here without this file being touched.
    void DrawEffect(SerializedProperty nodes, int index)
    {
        var effect = Field(nodes, index, "effect");
        if (effect == null) return;

        EditorGUILayout.Space();

        if (ManagedRefPicker.DrawTypeDropdown(effect, typeof(IUpgradeEffect), "Effect", allowNone: true))
            return;   // the type just changed; its fields do not exist until the next repaint

        if (effect.managedReferenceValue == null)
        {
            EditorGUILayout.LabelField("No effect — this node only opens the ones after it.", EditorStyles.miniLabel);
            return;
        }

        // The effect's own fields, inline — no foldout, so the node reads as one flat screen. A buff row
        // draws itself through StatBuffDrawer, which is where the stat dropdown lives: rows sit inside a
        // list, and a list is painted by Unity's drawer, so nothing done by hand here would reach them.
        EditorGUI.indentLevel++;
        var end = effect.GetEndProperty();
        var it = effect.Copy();
        bool enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;
            EditorGUILayout.PropertyField(it, true);
        }
        EditorGUI.indentLevel--;
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
