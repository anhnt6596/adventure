using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The whole brain on one screen: four dropdowns, and under each one the picked behaviour's own fields. Same
// trick as SpawnZoneEditor — drive a [SerializeReference] slot by swapping the concrete instance in place —
// but the list of choices is found by reflection instead of a hand-written enum, which is the point of the
// whole arrangement: a new behaviour is ONE [Serializable] class and it appears here by itself. Nothing to
// register, no factory to extend.
[CustomEditor(typeof(EnemyBrainConfig))]
public class EnemyBrainConfigEditor : Editor
{
    static readonly Dictionary<Type, Type[]> _impls = new Dictionary<Type, Type[]>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Slot("idle",    typeof(IIdleBehavior), "No target: what it does with its time.");
        Slot("aggro",   typeof(IAggro),        "Whether it starts fights on its own. Passive units still retaliate — that path is the FSM's, not this.");
        Slot("pursuit", typeof(IPursuit),      "How it closes the distance once it has a target.");
        Slot("attack",  typeof(IAttackPlan),   "When it pulls the trigger, once in range and facing.");

        // The FSM skeleton's own distances and timers — same asset, below the behaviours, so the whole mind is
        // one screen. Excluded above so the four slots keep their dropdowns instead of being drawn raw.
        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script", "idle", "aggro", "pursuit", "attack");

        serializedObject.ApplyModifiedProperties();

        var brain = (EnemyBrainConfig)target;
        if (!brain.IsComplete)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("An empty slot leaves the monster inert — EnemyAI refuses to run a partial brain.", MessageType.Warning);
        }
    }

    void Slot(string field, Type iface, string help)
    {
        var prop = serializedObject.FindProperty(field);
        if (prop == null) return;

        var options = Implementations(iface);
        var names = new string[options.Length + 1];
        names[0] = "(none)";
        for (int i = 0; i < options.Length; i++) names[i + 1] = ObjectNames.NicifyVariableName(options[i].Name);

        var current = prop.managedReferenceValue?.GetType();
        int index = current == null ? 0 : Array.IndexOf(options, current) + 1;

        EditorGUILayout.Space();
        int picked = EditorGUILayout.Popup(ObjectNames.NicifyVariableName(field), index, names);
        if (picked != index)
        {
            prop.managedReferenceValue = picked == 0 ? null : Activator.CreateInstance(options[picked - 1]);
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.LabelField(help, EditorStyles.miniLabel);

        // The behaviour's own fields, inline — no foldout, so the whole brain reads as one flat screen.
        if (prop.managedReferenceValue == null) return;
        EditorGUI.indentLevel++;
        var end = prop.GetEndProperty();
        var it = prop.Copy();
        bool enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;
            EditorGUILayout.PropertyField(it, true);
        }
        EditorGUI.indentLevel--;
    }

    // Every concrete [Serializable] class implementing the slot's interface. TypeCache is Unity's prebuilt index,
    // so this is cheap; the result is cached per interface for the domain's lifetime anyway.
    static Type[] Implementations(Type iface)
    {
        if (_impls.TryGetValue(iface, out var cached)) return cached;

        var found = TypeCache.GetTypesDerivedFrom(iface)
            .Where(t => t.IsClass && !t.IsAbstract && Attribute.IsDefined(t, typeof(SerializableAttribute))
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name)
            .ToArray();

        _impls[iface] = found;
        return found;
    }
}
