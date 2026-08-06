using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

// The upgrade tree for whichever character is currently being played: a centre node with the rest radiating
// out from it, paid for with points earned by levelling.
//
// UXML/USS live next to this file and MUST be named UpgradePopup.* — the UI registry matches by file name.
//
// THE LAYOUT IS AUTHORED, NOT SOLVED. Each node carries its own ring and angle, and this only turns that into
// pixels. A radial solver would fight the one thing that makes the shape worth having — nodes that two
// different branches lead into — and the trees are small and hand-built, so the shape IS the design.
//
// EDGES ARE PAINTED, NOT BUILT. One element under the nodes draws every link through Painter2D, rather than
// a rotated VisualElement per link: one element repaints in one pass, can colour each link by whether the
// node behind it is owned, and does not put a hundred throwaway elements in the tree.
//
// GAME STATE IS PUSHED IN, ART IS INJECTED, and the split is not arbitrary: UISystem builds its views with
// the App container, so UpgradeSystem, CharacterLevels and who is being played — all GameScope — would
// silently resolve to nothing through [Inject] and have to arrive via Bind. IArtProvider is App-scope, so it
// comes down the normal path. The rule to keep: if it knows about the running game it is pushed, if it only
// knows about the project it is injected.
public class UpgradePopup : BasePopup
{
    // A full-screen panel that fades; zoom is for the small centred popups.
    protected override EffectType AppearFx => EffectType.Fade;

    // Public so the tree editor can draw at the same proportions — a node that fits here and overlaps in
    // game is the whole reason to have a preview, and two copies of these numbers would drift apart.
    public const float RingSpacing = 230f;   // one tree unit, in pixels
    public const float NodeSize = 120f;      // matches .node in the USS

    const float CanvasMargin = 160f;
    const float TipGap = 16f;        // between the top of a node and the tooltip standing over it

    IGetUpgradeTree _trees;
    UpgradeSystem _upgrades;
    CharacterLevels _levels;
    IArtProvider _art;

    readonly VisualElement _avatar, _body, _canvas, _edges, _tip;
    readonly Label _charName, _points, _tipText, _empty;
    readonly Button _buyButton, _resetButton;
    readonly ScrollView _scroll;

    readonly Dictionary<string, VisualElement> _nodeElements = new Dictionary<string, VisualElement>();
    readonly Dictionary<string, Vector2> _nodeCentres = new Dictionary<string, Vector2>();
    VisualElement _centreElement;

    UpgradeTreeConfig _tree;
    string _characterId;
    UpgradeNode _selected;
    Vector2 _centre;

    public UpgradePopup(VisualElement root) : base(root)
    {
        _avatar = root.Q<VisualElement>("avatar");
        _charName = root.Q<Label>("char-name");
        _points = root.Q<Label>("points");
        _body = root.Q<VisualElement>("body");
        _scroll = root.Q<ScrollView>("tree-scroll");
        _canvas = root.Q<VisualElement>("tree-canvas");
        _edges = root.Q<VisualElement>("tree-edges");
        _tip = root.Q<VisualElement>("tip");
        _tipText = root.Q<Label>("tip-text");
        _empty = root.Q<Label>("empty");
        _buyButton = root.Q<Button>("buy-button");
        _resetButton = root.Q<Button>("reset-button");

        root.Q<Button>("close-button")?.RegisterCallback<ClickEvent>(_ => Close());
        _buyButton?.RegisterCallback<ClickEvent>(_ => Buy());
        _resetButton?.RegisterCallback<ClickEvent>(_ => ResetTree());

        // Pressing anywhere that is not the tooltip or a node puts the tooltip away. On the way DOWN the
        // tree, because a button swallows the press it handles and half of what a player can hit in here is
        // a button — waiting for the event to bubble back up would mean it never arrives.
        root.RegisterCallback<PointerDownEvent>(OnPressedAnywhere, TrickleDown.TrickleDown);

        if (_scroll != null)
        {
            _scroll.mode = ScrollViewMode.VerticalAndHorizontal;
            // Drag anywhere in the tree to pan it — the same manipulator the rest of the UI uses, so it
            // behaves the way every other draggable surface in the game does.
            _scroll.contentViewport.AddManipulator(new DragScrollManipulator(_scroll));

            // The tooltip hangs outside the scroll view, so nothing moves it when the tree slides underneath.
            // Dragging to pan closes it anyway (the press lands outside it), but a mouse wheel does not — and
            // a tooltip left floating over the wrong node is worse than one that closed.
            _scroll.horizontalScroller.valueChanged += _ => PlaceTip();
            _scroll.verticalScroller.valueChanged += _ => PlaceTip();
        }

        if (_edges != null) _edges.generateVisualContent += PaintEdges;
    }

    [Inject]
    public void Construct(IArtProvider art) => _art = art;

    // Called by whoever opened it, straight after Show — Show runs OnShow first, so nothing here may assume
    // it has been bound yet.
    public void Bind(IGetUpgradeTree trees, UpgradeSystem upgrades, CharacterLevels levels, string characterId)
    {
        _trees = trees;
        _upgrades = upgrades;
        _levels = levels;
        _characterId = characterId;

        Subscribe();
        Build();
    }

    public override void OnShow()
    {
        base.OnShow();
        Subscribe();     // a re-open before Bind lands still tracks whatever it was showing last
    }

    public override void OnHide()
    {
        Unsubscribe();
        base.OnHide();
    }

    void Subscribe()
    {
        if (_upgrades != null) { _upgrades.Changed -= Refresh; _upgrades.Changed += Refresh; }
        if (_levels != null) { _levels.Changed -= OnLevelChanged; _levels.Changed += OnLevelChanged; }
    }

    void Unsubscribe()
    {
        if (_upgrades != null) _upgrades.Changed -= Refresh;
        if (_levels != null) _levels.Changed -= OnLevelChanged;
    }

    void OnLevelChanged(string characterId)
    {
        if (characterId == _characterId) Refresh();
    }

    // Rebuilt on every open rather than kept: the character can have changed since last time, and a tree is
    // a few dozen elements — cheap enough that keeping a cache in step would cost more than it saves.
    void Build()
    {
        _tree = _trees?.Get(_characterId);
        _selected = null;

        if (_charName != null) _charName.text = string.IsNullOrEmpty(_characterId) ? "—" : _characterId;

        var avatar = _art?.Avatar(_characterId);
        if (_avatar != null)
            _avatar.style.backgroundImage = avatar != null
                ? new StyleBackground(avatar)
                : new StyleBackground(StyleKeyword.None);

        // Hoisted: Nodes flattens the inherited chain on every call while authoring, and Build asks twice.
        var nodes = _tree != null ? _tree.Nodes : null;
        bool hasTree = nodes != null && nodes.Count > 0;
        if (_body != null) _body.style.display = hasTree ? DisplayStyle.Flex : DisplayStyle.None;
        if (_empty != null) _empty.style.display = hasTree ? DisplayStyle.None : DisplayStyle.Flex;
        if (_resetButton != null) _resetButton.style.display = hasTree ? DisplayStyle.Flex : DisplayStyle.None;

        ClearNodes();
        if (!hasTree) return;

        // The canvas is SYMMETRIC about the centre, not a tight box around the nodes. A tight box puts the
        // centre wherever the tree happens to be lopsided, so the character you are looking at drifts off to
        // one side — and the centre is the one thing that should always be in the middle. The empty half of
        // a one-sided tree is the price, and it is only scrolling.
        Vector2 extent = Vector2.zero;
        foreach (var node in nodes)
        {
            if (node == null) continue;
            extent = Vector2.Max(extent, new Vector2(Mathf.Abs(node.position.x), Mathf.Abs(node.position.y)));
        }

        var size = extent * (2f * RingSpacing) + new Vector2(CanvasMargin, CanvasMargin) * 2f;
        _centre = size * 0.5f;

        if (_canvas != null)
        {
            _canvas.style.width = size.x;
            _canvas.style.height = size.y;
        }

        // The centre is not a node and is never bought — it is the character, and everything without a
        // requirement grows out of it. Drawn first so the real nodes sit over it.
        if (_centreElement == null)
        {
            _centreElement = new VisualElement();
            _centreElement.AddToClassList("node");
            _centreElement.AddToClassList("node--root");
            _centreElement.pickingMode = PickingMode.Ignore;
        }
        _centreElement.style.left = _centre.x - NodeSize * 0.5f;
        _centreElement.style.top = _centre.y - NodeSize * 0.5f;
        var portrait = _art?.Avatar(_characterId);
        _centreElement.style.backgroundImage = portrait != null
            ? new StyleBackground(portrait)
            : new StyleBackground(StyleKeyword.None);
        _canvas?.Add(_centreElement);

        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) continue;

            var position = _centre + node.position * RingSpacing;

            var element = MakeNode(node);
            element.style.left = position.x - NodeSize * 0.5f;
            element.style.top = position.y - NodeSize * 0.5f;

            _canvas?.Add(element);
            _nodeElements[node.id] = element;
            _nodeCentres[node.id] = position;
        }

        Refresh();
        ScrollToCentre();
    }

    // Opening on the centre rather than on the top-left corner of a canvas that may be several screens
    // wide. Deferred by a frame because the viewport has no size until layout has run, and scrolling to the
    // middle of a rectangle nobody has measured yet lands at zero.
    void ScrollToCentre()
    {
        if (_scroll == null) return;

        _scroll.schedule.Execute(() =>
        {
            var viewport = _scroll.contentViewport.layout.size;
            _scroll.scrollOffset = _centre - viewport * 0.5f;
        }).ExecuteLater(0);
    }

    VisualElement MakeNode(UpgradeNode node)
    {
        var button = new Button();
        button.AddToClassList("node");
        button.tooltip = Title(node);
        button.RegisterCallback<ClickEvent>(_ => Select(node));

        var sprite = _art?.UpgradeIcon(_characterId, node.key);
        if (sprite != null)
        {
            var icon = new Image { sprite = sprite };
            icon.AddToClassList("node-icon");
            icon.pickingMode = PickingMode.Ignore;
            button.Add(icon);
        }

        // On every node, not only the dear ones: a price that appears just above one point makes the player
        // work out what the ordinary price is, and this is the one screen where that should never take
        // working out. Same reason the tooltip no longer spells the cost in a sentence.
        var cost = new Label(Mathf.Max(1, node.cost).ToString());
        cost.AddToClassList("node-cost");
        cost.pickingMode = PickingMode.Ignore;
        button.Add(cost);

        return button;
    }

    void ClearNodes()
    {
        foreach (var element in _nodeElements.Values) element.RemoveFromHierarchy();
        _nodeElements.Clear();
        _nodeCentres.Clear();
        _centreElement?.RemoveFromHierarchy();
    }

    void OnPressedAnywhere(PointerDownEvent evt)
    {
        if (_selected == null) return;

        // A node is left alone here: its own click decides whether that means "show me this one" or "close
        // the one I already had open", and answering twice would make a second press do both.
        var target = evt.target as VisualElement;
        if (Within(target, _tip) || WithinNode(target)) return;

        _selected = null;
        Refresh();
    }

    static bool Within(VisualElement element, VisualElement container)
    {
        if (container == null) return false;
        for (var e = element; e != null; e = e.parent)
            if (e == container) return true;
        return false;
    }

    // By class rather than by looking the element up in _nodeElements: a press lands on whatever is topmost,
    // which for a node is usually its icon or its cost badge rather than the node itself.
    static bool WithinNode(VisualElement element)
    {
        for (var e = element; e != null; e = e.parent)
            if (e.ClassListContains("node")) return true;
        return false;
    }

    // Clicking the open node again closes it, so the tooltip can be dismissed without having to pick some
    // other node just to get rid of it.
    void Select(UpgradeNode node)
    {
        _selected = _selected != null && node != null && _selected.id == node.id ? null : node;
        Refresh();
    }

    // Display is looked up by KEY, never by id: the id says which node this is, the key says what it is,
    // and two nodes that are the same thing twice share the second without sharing the first.
    //
    // TODO: text by (character, key), the way icons already are — a string table keyed exactly like the art
    // folders, so nothing readable lives in a config. Until it exists the key IS the label, which is ugly on
    // purpose: an unlocalised placeholder that looks like one will not quietly ship. It only reaches the
    // hover tooltip now; what the player reads on click is the effect describing itself.
    string Title(UpgradeNode node) => node?.key ?? "";

    void Refresh()
    {
        if (_tree == null) return;

        int available = _upgrades?.Available(_characterId, _tree) ?? 0;
        int level = _levels?.Level(_characterId) ?? CharacterLevels.StartLevel;
        if (_points != null) _points.text = $"Level {level}   ·   {available} point{(available == 1 ? "" : "s")} to spend";

        foreach (var node in _tree.Nodes)
        {
            if (node == null || !_nodeElements.TryGetValue(node.id, out var element)) continue;

            element.RemoveFromClassList("node--root");
            element.RemoveFromClassList("node--bought");
            element.RemoveFromClassList("node--ready");
            element.RemoveFromClassList("node--short");
            element.RemoveFromClassList("node--locked");
            element.RemoveFromClassList("node--selected");

            element.AddToClassList(StateClass(node, available));
            if (_selected != null && _selected.id == node.id) element.AddToClassList("node--selected");
        }

        _edges?.MarkDirtyRepaint();
        RefreshTip();
    }

    string StateClass(UpgradeNode node, int available)
    {
        if (_upgrades.IsBought(_characterId, node)) return "node--bought";
        if (!_upgrades.IsUnlocked(_characterId, _tree, node)) return "node--locked";
        return available >= Mathf.Max(1, node.cost) ? "node--ready" : "node--short";
    }

    // Over the selected node, in the coordinates of whatever the tooltip hangs off — the popup, not the tree.
    // Taken from the node's LAID-OUT rectangle rather than from the position it was authored at, so the scroll
    // offset, the viewport's own place on screen and the header's height are all already in the answer and
    // none of them has to be reproduced here. The USS translate does the rest: back half a width, up a whole
    // height, so it stands centred above the node.
    void PlaceTip()
    {
        if (_tip == null || _selected == null) return;
        if (!_nodeElements.TryGetValue(_selected.id, out var element)) return;

        var box = element.worldBound;
        var anchor = _tip.parent.WorldToLocal(new Vector2(box.center.x, box.yMin));
        _tip.style.left = anchor.x;
        _tip.style.top = anchor.y - TipGap;
    }

    void RefreshTip()
    {
        if (_tip == null) return;

        // display, not visibility: a hidden-but-laid-out tooltip still takes clicks off the nodes underneath
        // it, which on a tight tree means a node you cannot press and nothing on screen explaining why.
        bool has = _selected != null && _nodeElements.ContainsKey(_selected.id);
        _tip.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        if (!has) return;

        var node = _selected;
        PlaceTip();

        // What it DOES, asked of the effect. A node with none is not a mistake — it is the branch-opener the
        // tree needs to reach the ones past it, and saying so is better than an empty box.
        if (_tipText != null)
        {
            var described = node.effect?.Describe();
            _tipText.text = string.IsNullOrEmpty(described) ? "Opens the nodes after it." : described;
        }

        // No sentence explaining the price or why it is locked. The button carries the price, and a button
        // you can see but cannot press already says "not yet" — a paragraph saying it again is something the
        // player reads once and skips forever after.
        if (_buyButton != null)
        {
            bool bought = _upgrades.IsBought(_characterId, node);
            _buyButton.style.display = bought ? DisplayStyle.None : DisplayStyle.Flex;
            _buyButton.text = $"Unlock ({Mathf.Max(1, node.cost)})";
            _buyButton.SetEnabled(_upgrades.CanBuy(_characterId, _tree, node));
        }
    }

    void Buy()
    {
        if (_selected == null) return;

        // Only dismiss on a buy that actually happened: a refused one leaves the tooltip standing, because the
        // reason it was refused is on the node the player is still looking at.
        if (_upgrades?.Buy(_characterId, _tree, _selected) != true) return;   // fires Changed -> Refresh

        // Bought, so the tooltip has nothing left to offer — the Unlock button hides itself once a node is
        // owned (see RefreshTip), and a box with no action in it is just something covering the tree. Closing
        // it also puts the player back where the next click wants them: on the nodes this one just opened.
        _selected = null;
        Refresh();
    }

    void ResetTree()
    {
        _upgrades?.Reset(_characterId);   // fires Changed -> Refresh
    }

    // Links are drawn from the node that leads IN to the node it opens, and coloured by whether that entry
    // has actually been taken — so a lit line means "this way is open", which is the only thing an edge in
    // an OR graph can usefully say.
    void PaintEdges(MeshGenerationContext ctx)
    {
        if (_tree == null || _upgrades == null || _nodeCentres.Count == 0) return;

        var painter = ctx.painter2D;
        painter.lineWidth = 4f;
        painter.lineCap = LineCap.Round;

        foreach (var node in _tree.Nodes)
        {
            if (node == null) continue;
            if (!_nodeCentres.TryGetValue(node.id, out var to)) continue;

            // No requirements: it grows straight out of the centre, and the line has to say so or a whole
            // first ring appears to float unattached.
            if (node.requires == null || node.requires.Length == 0)
            {
                painter.strokeColor = _upgrades.IsBought(_characterId, node)
                    ? new Color(0.47f, 0.78f, 0.51f, 0.95f)
                    : new Color(0.92f, 0.78f, 0.35f, 0.55f);
                painter.BeginPath();
                painter.MoveTo(_centre);
                painter.LineTo(to);
                painter.Stroke();
                continue;
            }

            foreach (var requiredId in node.requires)
            {
                if (string.IsNullOrEmpty(requiredId)) continue;
                if (!_nodeCentres.TryGetValue(requiredId, out var from)) continue;

                var required = _tree.Find(requiredId);
                bool open = required != null && _upgrades.IsBought(_characterId, required);
                bool taken = open && _upgrades.IsBought(_characterId, node);

                painter.strokeColor = taken ? new Color(0.47f, 0.78f, 0.51f, 0.95f)
                                    : open ? new Color(0.92f, 0.78f, 0.35f, 0.55f)
                                           : new Color(1f, 1f, 1f, 0.12f);

                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(to);
                painter.Stroke();
            }
        }
    }
}
