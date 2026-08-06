using System;
using System.Collections.Generic;
using UnityEngine;

// One character's upgrade tree, as data: nodes radiating from a centre, and which nodes lead into which.
//
// THE ID IS THE CHARACTER'S. ConfigRegistry keys by exact type first and id second (see ConfigRegistry.Build),
// so a tree carrying id "MC 1" sits beside the MainCharStatsConfig carrying id "MC 1" without colliding.
// Name the asset "MC 1 - Tree" for the project window; the id is what anything looks it up by, exactly the
// way MainCharStatsConfig already works.
//
// PAID IN POINTS, NOT IN RESOURCES. A character earns one upgrade point per level of its own, so the tree
// draws on time spent with THAT character rather than on the shared bag — which is what stops investing in
// one character from being taken out of another's pocket. It also leaves the bag with exactly one job
// (pay gates and what comes after), instead of two systems bidding for the same pile.
//
// NO DISPLAY IN HERE. A node has a key and a shape, and that is all: no title, no description, no icon.
// Everything a player reads or looks at is derived from (this tree's id, the node's key) by whatever owns
// presentation — the same rule that keeps portraits off MainCharStatsConfig. A config that carries its own
// text is a config that has to be re-opened to fix a typo and re-authored to translate.
//
// WHAT A NODE DOES lives in its effect, behind [SerializeReference] — see IUpgradeEffect. A node with no
// effect is a junction: it costs a point and opens what comes after it, which is a real thing to want.
//
// TREES ARE DRAWN ON TOP OF ONE ANOTHER. `inherits` names a tree whose nodes come first, so the trunk every
// character shares is authored ONCE and a character's own asset carries only the leaves that are its. Change
// the trunk and every character built on it changes with it — which is the entire point, and the reason this
// is a reference rather than a copy anybody could forget to re-copy.
//
// NOTHING ELSE IN THE GAME KNOWS THERE ARE TWO TIERS. Ask for `Nodes` and the answer is the whole tree,
// trunk first. Points are points, a bought id is a bought id, and the save does not change — a character's
// bought set already only ever held ids, and where an id was authored is not something it needs to know.
[CreateAssetMenu(menuName = "Upgrades/Upgrade Tree")]
public class UpgradeTreeConfig : Config
{
    [Tooltip("Optional. The tree this one grows out of — the trunk every character shares. Its nodes appear " +
             "here but cannot be edited from here: change them in that asset and everyone follows.\n\n" +
             "Ids must stay unique across the whole chain; the tree editor reports it when they are not.")]
    public UpgradeTreeConfig inherits;

    [Tooltip("The nodes THIS asset owns. Inherited ones are not in here — see Nodes for the whole tree.")]
    public UpgradeNode[] nodes = Array.Empty<UpgradeNode>();

    // The whole tree, trunk first. Order is not decoration: the layout and the editor walk it, so a node
    // reading as "after the thing it grew out of" falls out of the order rather than out of a sort.
    //
    // NOT CACHED WHILE AUTHORING. The trunk lives in a different asset, so nothing here is told when it is
    // edited — and an upgrade tree is edited far more often than it is read. A build caches it once, where
    // the assets can no longer change.
    public IReadOnlyList<UpgradeNode> Nodes
    {
#if UNITY_EDITOR
        get => Flatten();
#else
        get => _flat ??= Flatten();
#endif
    }

#if !UNITY_EDITOR
    UpgradeNode[] _flat;
#endif

    UpgradeNode[] Flatten()
    {
        var list = new List<UpgradeNode>();
        Collect(this, list, new HashSet<UpgradeTreeConfig>());
        return list.ToArray();
    }

    static void Collect(UpgradeTreeConfig tree, List<UpgradeNode> into, HashSet<UpgradeTreeConfig> seen)
    {
        if (tree == null) return;

        if (!seen.Add(tree))
        {
            Debug.LogError($"[{nameof(UpgradeTreeConfig)}] '{tree.name}' inherits itself, directly or through " +
                           "a chain. Everything past the loop is dropped.", tree);
            return;
        }

        Collect(tree.inherits, into, seen);

        if (tree.nodes == null) return;
        foreach (var node in tree.nodes)
            if (node != null) into.Add(node);
    }

    // Walks the chain rather than the flattened list, because this is asked once per node per repaint and a
    // lookup that allocates would turn drawing a tree into garbage. Own nodes first; ids are meant to be
    // unique across the chain and the editor says so when they are not.
    //
    // The depth bound is the loop guard: no real tree is sixteen deep, so anything that reaches it is a cycle,
    // and Nodes reports it properly rather than this method hanging.
    public UpgradeNode Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        var tree = this;
        for (int depth = 0; tree != null && depth < 16; depth++, tree = tree.inherits)
        {
            if (tree.nodes == null) continue;
            foreach (var n in tree.nodes)
                if (n != null && n.id == id) return n;
        }
        return null;
    }
}

[Serializable]
public class UpgradeNode
{
    [Tooltip("WHICH node this is. The save key, and what other nodes' `requires` point at. Type it once and " +
             "never change it, or a player loses what they bought.\n\n" +
             "Unique within THIS tree only — nothing ever compares ids across two trees.")]
    public string id;

    [Tooltip("WHAT this node is, for display: the name, the description and the icon are all looked up by " +
             "(this tree's id, this key). Nothing readable is stored on the node itself.\n\n" +
             "Deliberately NOT the id, because two nodes can be the same thing twice — a second +HP further " +
             "out wants the same name and picture as the first, while still being its own node to buy.")]
    public string key;

    [Tooltip("Ids of the nodes leading into this one. ANY ONE of them being bought opens it — routes, not a " +
             "checklist.\n\nEMPTY means it hangs straight off the centre: that is how a first-ring node is " +
             "declared, and why there is no root to configure.")]
    public string[] requires = Array.Empty<string>();

    [Tooltip("What owning this node does. Empty is allowed and means the node only opens the ones after it — " +
             "a junction, which is a real thing to want.")]
    [SerializeReference] public IUpgradeEffect effect;

    [Tooltip("Upgrade points. One is the usual answer and the one the UI stays quiet about; raise it for a " +
             "node that is worth several levels on its own.")]
    [Min(1)] public int cost = 1;

    [Tooltip("Where the node sits, in TREE UNITS: 1 is one step out from the centre, y grows downward. Not " +
             "pixels — both the editor and the popup scale it, so the same tree reads the same at any size.\n\n" +
             "Dragged in the tree editor rather than typed. Auto Arrange fills them all in from the " +
             "requirements when you would rather not place anything by hand.")]
    public Vector2 position;
}
