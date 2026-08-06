using System.Collections.Generic;
using UnityEngine;

// A first guess at where every node should sit, worked out from the graph.
//
// NOT WHAT THE GAME READS ANY MORE. Positions are authored — dragged in the tree editor and saved on the
// node — because a tree is a picture and a picture wants a hand on it. This runs behind the Auto Arrange
// button and when a new node needs somewhere sensible to land, so nobody has to place a whole tree from
// scratch to see its shape.
//
// It also still answers "which nodes can be reached at all", which is a property of the graph rather than
// of the drawing, and the tree editor reports it as a problem.
//
// THE CENTRE IS IMPLICIT. It is not a node and cannot be authored; a node with no requirements simply hangs
// off it. So "the tree has a root" stops being something anyone can forget to set up.
//
// RING IS THE SHORTEST WAY IN. Requirements are OR — any one of them opens a node — so the depth that means
// something is the fewest steps from the centre, not the most. A node you can reach in two draws two rings
// out, whatever else also happens to point at it.
//
// ANGLE IS SHARE OF THE CIRCLE. Each node gets a wedge sized by how many leaves hang under it, and its
// children divide that wedge between them. Splitting evenly instead would give a branch of one the same room
// as a branch of nine, and the big branch would collide with itself.
public class UpgradeTreeLayout
{
    // WHAT COMES OUT IS A POSITION, not a ring and an angle. Callers draw a picture; they have no business
    // knowing this one happens to be radial. The day it is replaced by a real graph-drawing algorithm — or by
    // positions somebody placed by hand — this file changes and nothing else does.
    //
    // Offset is in units of one step out from the centre, in screen convention: y grows downward, and a
    // node straight above the centre is (0, -1). Both readers are screens, so doing the trigonometry twice
    // more out there would only be two more chances to get it wrong.
    public readonly struct Slot
    {
        public readonly Vector2 Offset;
        public Slot(Vector2 offset) { Offset = offset; }
    }

    readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();

    // How far the furthest node sits, in the same units. Whoever draws it scales by this rather than by a
    // constant, so a tree that grows does not need somebody to remember to widen the view.
    public float Radius { get; private set; }

    // Nodes the walk never arrived at: something they require does not exist, or a ring of them require each
    // other and nothing outside. They are placed on their own ring past everything else so they are visible
    // rather than stacked on the centre — at runtime they are unreachable too, which is the honest picture.
    public IReadOnlyList<string> Unreachable => _unreachable;
    readonly List<string> _unreachable = new List<string>();

    public bool TryGet(string id, out Slot slot) => _slots.TryGetValue(id, out slot);

    public static UpgradeTreeLayout Build(UpgradeTreeConfig tree)
    {
        var layout = new UpgradeTreeLayout();
        if (tree == null) return layout;

        // Array order is the only handle anyone has on the arrangement, so every step below walks the nodes
        // in that order and the result is stable: the same tree always lays out the same way.
        var order = new List<UpgradeNode>();
        var byId = new Dictionary<string, UpgradeNode>();
        foreach (var node in tree.Nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.id) || byId.ContainsKey(node.id)) continue;
            byId[node.id] = node;
            order.Add(node);
        }
        if (order.Count == 0) return layout;

        var depth = new Dictionary<string, int>();
        var layoutChildren = new Dictionary<string, List<UpgradeNode>>();
        var roots = new List<UpgradeNode>();

        // Breadth first from the implicit centre, so the first time a node is reached is by its shortest way.
        var queue = new Queue<UpgradeNode>();
        foreach (var node in order)
        {
            if (node.requires != null && node.requires.Length > 0) continue;
            depth[node.id] = 1;
            roots.Add(node);
            queue.Enqueue(node);
        }

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();

            foreach (var node in order)
            {
                if (depth.ContainsKey(node.id) || node.requires == null) continue;

                bool follows = false;
                foreach (var requiredId in node.requires)
                    if (requiredId == parent.id) { follows = true; break; }
                if (!follows) continue;

                depth[node.id] = depth[parent.id] + 1;

                // Only the link that got here first counts for layout. The others are real requirements and
                // still get drawn, they just do not decide where the node lives — a node cannot sit in two
                // branches at once.
                if (!layoutChildren.TryGetValue(parent.id, out var list))
                    layoutChildren[parent.id] = list = new List<UpgradeNode>();
                list.Add(node);

                queue.Enqueue(node);
            }
        }

        foreach (var node in order)
            if (!depth.ContainsKey(node.id)) layout._unreachable.Add(node.id);

        var leaves = new Dictionary<string, int>();
        foreach (var root in roots) CountLeaves(root, layoutChildren, leaves);

        int totalLeaves = 0;
        foreach (var root in roots) totalLeaves += leaves[root.id];

        if (totalLeaves > 0)
        {
            // Start half of the first wedge back, so the first root child points straight up instead of
            // landing wherever the arithmetic happened to leave it.
            float cursor = -(360f * leaves[roots[0].id] / totalLeaves) * 0.5f;
            layout.Place(roots, cursor, 360f, layoutChildren, leaves, depth);
        }

        int maxDepth = 0;
        foreach (var kv in depth) maxDepth = Mathf.Max(maxDepth, kv.Value);

        // Whatever could not be reached goes one step further out than everything else, spread evenly, so it
        // is visible and obviously apart rather than stacked on the centre.
        if (layout._unreachable.Count > 0)
        {
            int ring = maxDepth + 1;
            for (int i = 0; i < layout._unreachable.Count; i++)
                layout._slots[layout._unreachable[i]] =
                    new Slot(Direction(360f * i / layout._unreachable.Count) * ring);
            maxDepth = ring;
        }

        layout.Radius = maxDepth;
        return layout;
    }

    void Place(List<UpgradeNode> siblings, float start, float sweep,
               Dictionary<string, List<UpgradeNode>> children, Dictionary<string, int> leaves,
               Dictionary<string, int> depth)
    {
        int total = 0;
        foreach (var node in siblings) total += leaves[node.id];
        if (total <= 0) return;

        float cursor = start;
        foreach (var node in siblings)
        {
            float share = sweep * leaves[node.id] / total;
            _slots[node.id] = new Slot(Direction(cursor + share * 0.5f) * depth[node.id]);

            if (children.TryGetValue(node.id, out var next))
                Place(next, cursor, share, children, leaves, depth);

            cursor += share;
        }
    }

    // 0 points up and grows clockwise, in screen convention — the way anyone reading a dial expects it.
    static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
    }

    static int CountLeaves(UpgradeNode node, Dictionary<string, List<UpgradeNode>> children,
                           Dictionary<string, int> leaves)
    {
        if (leaves.TryGetValue(node.id, out int cached)) return cached;

        if (!children.TryGetValue(node.id, out var list) || list.Count == 0)
            return leaves[node.id] = 1;

        int sum = 0;
        foreach (var child in list) sum += CountLeaves(child, children, leaves);
        return leaves[node.id] = sum;
    }
}
