using System;
using UnityEditor;
using UnityEngine;

// One row: which stat, which kind, how much — on a single line, because a buff is one sentence and reading
// it should not need three.
//
// THE STAT IS A DROPDOWN, and that is the reason this drawer exists at all. Stats are named by string so a
// buff can be authored as data, but a misspelt name resolves to nothing and quietly buffs the air — the
// failure is invisible in the editor, invisible at runtime, and only shows up as "this upgrade feels like it
// does nothing". Offering the list is what makes the string safe.
//
// A drawer rather than a special case inside the tree editor: buffs live inside a list, and a list is drawn
// by Unity's own drawer, so anything painted by hand up there never reaches the rows.
[CustomPropertyDrawer(typeof(StatBuff))]
public class StatBuffDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var stat = property.FindPropertyRelative("stat");
        var kind = property.FindPropertyRelative("kind");
        var amount = property.FindPropertyRelative("amount");

        float gap = 4f;
        float w = (position.width - gap * 2f) / 3f;
        var r = new Rect(position.x, position.y, w, EditorGUIUtility.singleLineHeight);

        int current = Array.IndexOf(StatId.All, stat.stringValue);
        int picked = EditorGUI.Popup(r, Mathf.Max(0, current), StatId.All);
        if (picked != current) stat.stringValue = StatId.All[picked];

        r.x += w + gap;
        EditorGUI.PropertyField(r, kind, GUIContent.none);

        r.x += w + gap;
        EditorGUI.PropertyField(r, amount, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;
}
