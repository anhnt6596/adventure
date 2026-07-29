using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Draws each clip as the film strip it actually is — one editable row of frames per authored direction, in
// order. A sprite animation is judged by looking at it: a missing direction, a frame out of order, or a hit
// landing on the wrong pose are all obvious in a strip and all invisible in a column of object fields.
//
// Every cell is a real property field, so assigning, undo and prefab overrides behave exactly as they would
// in the default inspector. Bulk filling is left to the raw-array toggle, where Unity's own list already
// takes a multi-selection drop — a hand-rolled one here only competed with the cells underneath it.
[CustomEditor(typeof(CharacterAnimSet))]
public class CharacterAnimSetEditor : Editor
{
    const float MaxCell = 64f;
    const float MinCell = 38f;   // below this Unity's object field has no room to draw its picker
    const float Gap = 2f;
    const float RowPad = 2f;    // breathing room under a strip; the frame number lives in the grip, not below

    const float GripH = 13f;    // the drag handle laid over the top edge of each frame
    const float TailBtn = 28f;  // the + / × buttons parked at the end of the run, side by side

    bool _raw;
    readonly Dictionary<string, Vector2> _scroll = new Dictionary<string, Vector2>();

    // Reorder-drag state. Kept here rather than in DragAndDrop because this never leaves the strip: the
    // moment it entered the DragAndDrop system it would look like an asset drag and every ObjectField on
    // screen would offer to accept it.
    int _dragFrom = -1, _dragHover = -1;
    string _dragPath;

    // The frame the grip was last clicked on — what the delete button acts on. One at a time across the whole
    // asset, so the button can only ever mean one thing.
    int _target = -1;
    string _targetPath;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("dirs"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mirror"));

        var set = (CharacterAnimSet)target;
        EditorGUILayout.HelpBox($"{set.AuthoredDirs} direction(s) to author. Mirroring always sources the RIGHT side.\n" +
                                "Drag a frame by its numbered grip to reorder; click it to target, then × to delete.\n" +
                                "To fill a run from a selection of sprites, switch to raw arrays and drop them on the list.",
            MessageType.None);

        EditorGUILayout.Space();
        _raw = EditorGUILayout.ToggleLeft("Edit as raw arrays", _raw);
        EditorGUILayout.Space();

        var clips = serializedObject.FindProperty("clips");
        if (_raw)
        {
            EditorGUILayout.PropertyField(clips, true);
        }
        else
        {
            for (int i = 0; i < clips.arraySize; i++) DrawClip(set, clips.GetArrayElementAtIndex(i));

            EditorGUILayout.Space();
            if (GUILayout.Button("Add action")) clips.arraySize++;
        }

        serializedObject.ApplyModifiedProperties();

        // Releasing outside any strip has to end the drag too, or the next click anywhere would land as a
        // drop. The strips get first look at MouseUp above; this is only the fallback.
        if (Event.current.type == EventType.MouseUp && _dragFrom >= 0)
        {
            _dragFrom = _dragHover = -1;
            Repaint();
        }

        // Sprite previews are generated on a worker; without this the strip paints blank until something
        // else happens to repaint the inspector.
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    void DrawClip(CharacterAnimSet set, SerializedProperty clip)
    {
        var action = clip.FindPropertyRelative("action");
        var fps = clip.FindPropertyRelative("fps");
        var loop = clip.FindPropertyRelative("loop");
        var hitFrame = clip.FindPropertyRelative("hitFrame");
        var dirs = clip.FindPropertyRelative("dirs");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(action, GUIContent.none, GUILayout.Width(70f));
        EditorGUILayout.LabelField("fps", GUILayout.Width(24f));
        EditorGUILayout.PropertyField(fps, GUIContent.none, GUILayout.Width(40f));
        EditorGUILayout.PropertyField(loop, GUIContent.none, GUILayout.Width(16f));
        EditorGUILayout.LabelField("loop", GUILayout.Width(32f));

        // The hit index is meaningless on a loop — the playhead would sweep past it every pass.
        using (new EditorGUI.DisabledScope(loop.boolValue))
        {
            EditorGUILayout.LabelField("hit", GUILayout.Width(22f));
            EditorGUILayout.PropertyField(hitFrame, GUIContent.none, GUILayout.Width(34f));
        }

        GUILayout.FlexibleSpace();

        // The number the combat lock should match, computed rather than remembered.
        int frames = FrameCount(dirs);
        if (frames > 0 && fps.floatValue > 0f)
            EditorGUILayout.LabelField($"{frames} f · {frames / fps.floatValue:0.00}s", EditorStyles.miniLabel, GUILayout.Width(90f));
        EditorGUILayout.EndHorizontal();

        // Keep the direction rows matching what this set says it authors, so a slot can never be orphaned.
        if (dirs.arraySize != set.AuthoredDirs) dirs.arraySize = set.AuthoredDirs;

        for (int d = 0; d < dirs.arraySize; d++)
        {
            var strip = dirs.GetArrayElementAtIndex(d).FindPropertyRelative("frames");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(set.SlotLabel(d), EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("frames", EditorStyles.miniLabel, GUILayout.Width(42f));
            strip.arraySize = Mathf.Max(0, EditorGUILayout.IntField(strip.arraySize, GUILayout.Width(34f)));
            EditorGUILayout.EndHorizontal();

            DrawStrip(strip, loop.boolValue ? -1 : hitFrame.intValue);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    static int FrameCount(SerializedProperty dirs)
    {
        for (int d = 0; d < dirs.arraySize; d++)
        {
            var frames = dirs.GetArrayElementAtIndex(d).FindPropertyRelative("frames");
            if (frames.arraySize > 0) return frames.arraySize;
        }
        return 0;
    }

    void DrawStrip(SerializedProperty frames, int hitFrame)
    {
        int n = frames.arraySize;
        float width = EditorGUIUtility.currentViewWidth - 46f;
        // Both buttons are always reserved, even when × is hidden: letting the row reflow would slide + out
        // from under the cursor the moment a click selects a frame.
        float shelf = Gap + TailBtn * 2f + Gap;

        if (n == 0)
        {
            var empty = GUILayoutUtility.GetRect(width, MinCell);
            var drop = new Rect(empty.x, empty.y, empty.width - shelf, empty.height);
            EditorGUI.DrawRect(drop, new Color(0f, 0f, 0f, 0.12f));
            GUI.Label(drop, "  no frames", EditorStyles.miniLabel);

            var add = new Rect(drop.xMax + Gap, empty.y + (empty.height - TailBtn) * 0.5f, TailBtn, TailBtn);
            if (GUI.Button(add, "+", TailStyle)) frames.arraySize++;
            return;
        }

        float cell = Mathf.Clamp((width - shelf - Gap * (n - 1)) / n, MinCell, MaxCell);
        float total = n * cell + Gap * (n - 1) + shelf;
        float rowH = cell + RowPad;

        // Fit the whole run on screen when it can; scroll only when the frames genuinely do not fit, since
        // seeing the run at once is the entire point of a strip.
        bool scrolls = total > width + 1f;
        Rect row;
        if (scrolls)
        {
            var key = frames.propertyPath;
            _scroll.TryGetValue(key, out var pos);
            pos = EditorGUILayout.BeginScrollView(pos, GUILayout.Height(rowH + 16f));
            row = GUILayoutUtility.GetRect(total, rowH, GUILayout.Width(total));
            EditorGUILayout.EndScrollView();
            _scroll[key] = pos;
        }
        else
        {
            row = GUILayoutUtility.GetRect(width, rowH);
        }

        // A reorder is recorded here and applied AFTER the loop — mutating the array mid-draw invalidates the
        // element properties the remaining cells are about to use.
        int moveFrom = -1, moveTo = -1;

        var e = Event.current;
        bool dragging = _dragFrom >= 0 && _dragPath == frames.propertyPath;

        if (dragging && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
        {
            _dragHover = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.x - row.x) / (cell + Gap)), 0, n - 1);
            if (e.type == EventType.MouseUp)
            {
                if (_dragHover != _dragFrom) { moveFrom = _dragFrom; moveTo = _dragHover; }
                _dragFrom = _dragHover = -1;
            }
            e.Use();
            Repaint();
        }

        for (int i = 0; i < n; i++)
        {
            var box = new Rect(row.x + i * (cell + Gap), row.y, cell, cell);
            bool isHit = i == hitFrame;
            var grip = new Rect(box.x, box.y, cell, GripH);

            // Claim the grip's mouse-down BEFORE the field is drawn. Dragging an ObjectField means "pull this
            // asset out of the field" — its own gesture, and it would win otherwise.
            EditorGUIUtility.AddCursorRect(grip, MouseCursor.Pan);
            if (e.type == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                _dragFrom = _dragHover = i;
                _dragPath = frames.propertyPath;
                _target = i;                        // a click that never becomes a drag is just a selection
                _targetPath = frames.propertyPath;
                e.Use();
                Repaint();
            }

            if (isHit) EditorGUI.DrawRect(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f),
                new Color(1f, 0.5f, 0.2f, 0.8f));

            // ObjectField, NOT PropertyField: PropertyField forces the field to one line, which drops the
            // thumbnail variant entirely. This overload keeps the SerializedProperty (so undo still works)
            // while honouring the tall rect, which is what makes Unity draw the sprite preview.
            EditorGUI.ObjectField(box, frames.GetArrayElementAtIndex(i), typeof(Sprite), GUIContent.none);

            // The grip doubles as the frame number's backing plate, so it costs no extra height. Target wins
            // over the hit tint: it is the thing the delete button is aimed at, and that must never be
            // ambiguous.
            bool isTarget = _targetPath == frames.propertyPath && _target == i;
            EditorGUI.DrawRect(grip, isTarget ? new Color(0.3f, 0.6f, 1f, 0.95f)
                                     : isHit  ? new Color(1f, 0.5f, 0.2f, 0.9f)
                                              : new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(grip, isHit ? $" ≡ {i} hit" : $" ≡ {i}", WhiteMini);

            if (dragging && _dragHover == i && _dragHover != _dragFrom)
                EditorGUI.DrawRect(new Rect(box.x - 1f, box.y, 2f, box.height), new Color(0.3f, 0.6f, 1f, 1f));
        }

        // The shelf sits immediately after the last frame, where the run is being built — square buttons in a
        // row, centred against the frames rather than filling their height. Explicit rects, so drawing ×
        // conditionally costs no GUILayout entry and cannot desync the layout and repaint passes.
        float shelfX = row.x + n * (cell + Gap) + Gap;
        float shelfY = row.y + (cell - TailBtn) * 0.5f;
        int deleteAt = -1;

        if (GUI.Button(new Rect(shelfX, shelfY, TailBtn, TailBtn), "+", TailStyle))
            frames.arraySize++;

        if (_targetPath == frames.propertyPath && _target >= 0 && _target < n
            && GUI.Button(new Rect(shelfX + TailBtn + Gap, shelfY, TailBtn, TailBtn), "×", TailStyle))
            deleteAt = _target;

        if (deleteAt >= 0)
        {
            // On an array of object references the first delete only nulls the entry; clearing it ourselves
            // first makes the single delete actually shorten the array.
            frames.GetArrayElementAtIndex(deleteAt).objectReferenceValue = null;
            frames.DeleteArrayElementAtIndex(deleteAt);
            _target = Mathf.Min(deleteAt, frames.arraySize - 1);
            if (_target < 0) _targetPath = null;
        }
        else if (moveFrom >= 0)
        {
            frames.MoveArrayElement(moveFrom, moveTo);
            _target = moveTo;   // the target is the frame, not the slot — it travels with what was dragged
        }
    }

    // Built off the plain button so the glyph can be scaled up — miniButton hard-codes a small font.
    static GUIStyle _tail;
    static GUIStyle TailStyle => _tail ??= new GUIStyle(GUI.skin.button)
    {
        fontSize = 15,
        alignment = TextAnchor.MiddleCenter,
        padding = new RectOffset(0, 0, 0, 0),
    };

    static GUIStyle _whiteMini;
    static GUIStyle WhiteMini => _whiteMini ??= new GUIStyle(EditorStyles.miniLabel)
    {
        normal = { textColor = Color.white },
        padding = new RectOffset(0, 0, 0, 0),
    };
}
