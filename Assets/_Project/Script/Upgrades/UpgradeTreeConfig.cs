using System;
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
[CreateAssetMenu(menuName = "Upgrades/Upgrade Tree")]
public class UpgradeTreeConfig : Config
{
    public UpgradeNode[] nodes = Array.Empty<UpgradeNode>();

    public UpgradeNode Find(string id)
    {
        if (nodes == null || string.IsNullOrEmpty(id)) return null;
        foreach (var n in nodes)
            if (n != null && n.id == id) return n;
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
