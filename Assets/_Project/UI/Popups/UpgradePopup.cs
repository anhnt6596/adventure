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

    const float RingSpacing = 155f;
    const float NodeSize = 84f;
    const float CanvasMargin = 110f;

    IGetUpgradeTree _trees;
    UpgradeSystem _upgrades;
    CharacterLevels _levels;
    IArtProvider _art;

    readonly VisualElement _avatar, _body, _canvas, _edges, _detail;
    readonly Label _charName, _points, _detailTitle, _detailBlurb, _detailState, _empty;
    readonly Button _buyButton, _resetButton;
    readonly ScrollView _scroll;

    readonly Dictionary<string, VisualElement> _nodeElements = new Dictionary<string, VisualElement>();
    readonly Dictionary<string, Vector2> _nodeCentres = new Dictionary<string, Vector2>();
    VisualElement _centreElement;

    UpgradeTreeConfig _tree;
    UpgradeTreeLayout _layout;
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
        _detail = root.Q<VisualElement>("detail");
        _detailTitle = root.Q<Label>("detail-title");
        _detailBlurb = root.Q<Label>("detail-blurb");
        _detailState = root.Q<Label>("detail-state");
        _empty = root.Q<Label>("empty");
        _buyButton = root.Q<Button>("buy-button");
        _resetButton = root.Q<Button>("reset-button");

        root.Q<Button>("close-button")?.RegisterCallback<ClickEvent>(_ => Close());
        _buyButton?.RegisterCallback<ClickEvent>(_ => Buy());
        _resetButton?.RegisterCallback<ClickEvent>(_ => ResetTree());

        if (_scroll != null)
        {
            _scroll.mode = ScrollViewMode.VerticalAndHorizontal;
            // Drag anywhere in the tree to pan it — the same manipulator the rest of the UI uses, so it
            // behaves the way every other draggable surface in the game does.
            _scroll.contentViewport.AddManipulator(new DragScrollManipulator(_scroll));
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

        _layout = UpgradeTreeLayout.Build(_tree);

        bool hasTree = _tree != null && _tree.nodes != null && _tree.nodes.Length > 0;
        if (_body != null) _body.style.display = hasTree ? DisplayStyle.Flex : DisplayStyle.None;
        if (_empty != null) _empty.style.display = hasTree ? DisplayStyle.None : DisplayStyle.Flex;
        if (_resetButton != null) _resetButton.style.display = hasTree ? DisplayStyle.Flex : DisplayStyle.None;

        ClearNodes();
        if (!hasTree) return;

        float size = 2f * (_layout.Radius * RingSpacing + CanvasMargin);
        _centre = new Vector2(size * 0.5f, size * 0.5f);
        if (_canvas != null)
        {
            _canvas.style.width = size;
            _canvas.style.height = size;
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

        foreach (var node in _tree.nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) continue;
            if (!_layout.TryGet(node.id, out var slot)) continue;

            var position = _centre + slot.Offset * RingSpacing;

            var element = MakeNode(node);
            element.style.left = position.x - NodeSize * 0.5f;
            element.style.top = position.y - NodeSize * 0.5f;

            _canvas?.Add(element);
            _nodeElements[node.id] = element;
            _nodeCentres[node.id] = position;
        }

        Refresh();
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

        return button;
    }

    void ClearNodes()
    {
        foreach (var element in _nodeElements.Values) element.RemoveFromHierarchy();
        _nodeElements.Clear();
        _nodeCentres.Clear();
        _centreElement?.RemoveFromHierarchy();
    }

    void Select(UpgradeNode node)
    {
        _selected = node;
        Refresh();
    }

    // Display is looked up by KEY, never by id: the id says which node this is, the key says what it is,
    // and two nodes that are the same thing twice share the second without sharing the first.
    //
    // TODO: text by (character, key), the way icons already are — a string table keyed exactly like the art
    // folders, so nothing readable lives in a config. Until it exists the key IS the label, which is ugly on
    // purpose: an unlocalised placeholder that looks like one will not quietly ship.
    string Title(UpgradeNode node) => node?.key ?? "";
    string Blurb(UpgradeNode node) => "";

    void Refresh()
    {
        if (_tree == null) return;

        int available = _upgrades?.Available(_characterId, _tree) ?? 0;
        int level = _levels?.Level(_characterId) ?? CharacterLevels.StartLevel;
        if (_points != null) _points.text = $"Level {level}   ·   {available} point{(available == 1 ? "" : "s")} to spend";

        foreach (var node in _tree.nodes)
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
        RefreshDetail(available);
    }

    string StateClass(UpgradeNode node, int available)
    {
        if (_upgrades.IsBought(_characterId, node)) return "node--bought";
        if (!_upgrades.IsUnlocked(_characterId, _tree, node)) return "node--locked";
        return available >= 1 ? "node--ready" : "node--short";
    }

    void RefreshDetail(int available)
    {
        bool has = _selected != null;
        if (_detail != null) _detail.style.visibility = has ? Visibility.Visible : Visibility.Hidden;
        if (!has) return;

        var node = _selected;
        if (_detailTitle != null) _detailTitle.text = Title(node);
        if (_detailBlurb != null) _detailBlurb.text = Blurb(node);

        bool bought = _upgrades.IsBought(_characterId, node);
        bool unlocked = _upgrades.IsUnlocked(_characterId, _tree, node);

        // No price is shown anywhere: every node costs one point, so the only number that ever changes is
        // how many are left, and the header already carries that.
        if (_detailState != null)
        {
            _detailState.text =
                bought ? "Unlocked."
                : !unlocked ? "Locked — unlock a node leading into this one first."
                : available < 1 ? "No points left. Level up to spend again."
                : "";
        }

        if (_buyButton != null)
        {
            _buyButton.style.display = bought ? DisplayStyle.None : DisplayStyle.Flex;
            _buyButton.SetEnabled(_upgrades.CanBuy(_characterId, _tree, node));
        }
    }

    void Buy()
    {
        if (_selected == null) return;
        _upgrades?.Buy(_characterId, _tree, _selected);   // fires Changed -> Refresh
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
        if (_tree?.nodes == null || _upgrades == null || _nodeCentres.Count == 0) return;

        var painter = ctx.painter2D;
        painter.lineWidth = 4f;
        painter.lineCap = LineCap.Round;

        foreach (var node in _tree.nodes)
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
