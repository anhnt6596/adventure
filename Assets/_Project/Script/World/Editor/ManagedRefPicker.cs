using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Unity has no built-in picker for a [SerializeReference] slot, so this is it: a dropdown of every concrete
// [Serializable] type that fits, which swaps the instance in place.
//
// The list comes from reflection rather than a hand-written enum, and that is the whole point of the
// arrangement: a new shape or behaviour is ONE [Serializable] class and it appears here by itself. Nothing to
// register, no factory to extend, no enum to keep in step - the hand-written enum this replaced in
// SpawnZoneEditor was already one shape behind.
public static class ManagedRefPicker
{
    static readonly Dictionary<Type, Type[]> _impls = new Dictionary<Type, Type[]>();

    // Every concrete [Serializable] class deriving from baseType. TypeCache is Unity's prebuilt index, so this
    // is cheap; the result is cached per base type for the domain's lifetime anyway.
    public static Type[] Implementations(Type baseType)
    {
        if (_impls.TryGetValue(baseType, out var cached)) return cached;

        var found = TypeCache.GetTypesDerivedFrom(baseType)
            .Where(t => t.IsClass && !t.IsAbstract && Attribute.IsDefined(t, typeof(SerializableAttribute))
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name)
            .ToArray();

        _impls[baseType] = found;
        return found;
    }

    // Draws the type dropdown. Returns true if the type changed, in which case the caller should apply before
    // drawing the new instance's fields — the old ones no longer exist.
    public static bool DrawTypeDropdown(SerializedProperty prop, Type baseType, string label, bool allowNone = false)
    {
        var options = Implementations(baseType);
        int offset = allowNone ? 1 : 0;
        var names = new string[options.Length + offset];
        if (allowNone) names[0] = "(none)";
        for (int i = 0; i < options.Length; i++)
            names[i + offset] = ObjectNames.NicifyVariableName(options[i].Name);

        var current = prop.managedReferenceValue?.GetType();
        int index = current == null ? 0 : Array.IndexOf(options, current) + offset;

        int picked = EditorGUILayout.Popup(label, index, names);
        if (picked == index) return false;

        prop.managedReferenceValue = allowNone && picked == 0
            ? null
            : Activator.CreateInstance(options[picked - offset]);
        prop.serializedObject.ApplyModifiedProperties();
        return true;
    }
}
