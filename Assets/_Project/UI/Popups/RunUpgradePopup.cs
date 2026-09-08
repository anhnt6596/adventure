using System;
using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

// The level-up draft: a level was gained inside an arena, here are three cards, take one.
//
// UXML/USS live next to this file and MUST be named RunUpgradePopup.uxml (the registry matches by file name).
//
// IT FREEZES THE WORLD. A choice offered while a horde is closing in is not a choice — it is a penalty for
// reading. Both levers, the way ARCHITECTURE.md says: timeScale for the simulation, the input gate for
// control. UI Toolkit animates on unscaled time, so the window still opens smoothly at zero.
//
// NO ESCAPE, NO DIMMER CLICK, NO SKIP. The only way out is picking one — which is why Show is only ever called
// with cards in hand. A draft the player can dismiss would leave the level silently spent.
//
// IT KNOWS NOTHING ABOUT ARENAS. Cards in, one card out through Chosen. What a card does, who applies it and
// what happens to it when the run ends are all the run's business (RunUpgrades) — this is a window.
public class RunUpgradePopup : BasePopup
{
    // NO ESCAPE, NO CLICKING OUTSIDE. Both doors are shut for the same reason: the level has already been
    // spent, so a window that can be dismissed is a level that vanishes with nothing to show for it. The only
    // way out is taking a card, which is also the only way out that means anything.
    public override bool CloseOnEscape => false;
    protected override bool CloseWhenClickBackground => false;

    IInputGate _gate;

    readonly Label _level;
    readonly VisualElement _cards;

    IDisposable _block;
    float _prevTimeScale = 1f;

    // The card the player took, announced once the window is already down — see Take. Whoever queued the
    // draft opens the next one straight off this, so two levels at once is two windows in a row.
    public event Action<RunUpgradeCard> Chosen;

    public RunUpgradePopup(VisualElement root) : base(root)
    {
        _level = root.Q<Label>("level-label");
        _cards = root.Q<VisualElement>("cards");
    }

    [Inject]
    public void Construct(IInputGate gate) => _gate = gate;

    // Called by the run right after Show, the same shape CharacterPopup uses: OnShow has already run, so
    // nothing here may assume it did anything with the hand.
    public void Bind(int level, IReadOnlyList<RunUpgradeCard> hand)
    {
        if (_level != null) _level.text = $"LEVEL {level}";
        if (_cards == null) return;

        _cards.Clear();
        if (hand == null) return;

        foreach (var card in hand)
        {
            if (card == null) continue;
            _cards.Add(Build(card));
        }
    }

    VisualElement Build(RunUpgradeCard card)
    {
        // A Button, not a VisualElement with a click handler: it is a thing you press, so it should focus,
        // respond to a key, and read as pressable to anything that inspects the tree.
        var element = new Button();
        element.AddToClassList("card");

        var title = new Label(card.title) { pickingMode = PickingMode.Ignore };
        title.AddToClassList("card-title");
        element.Add(title);

        // WRITTEN BY THE EFFECT, from the same numbers it applies — see IUpgradeEffect.Describe. A line the
        // popup assembled would need a branch per kind of card and would be free to be wrong.
        var effect = new Label(card.Describe()) { pickingMode = PickingMode.Ignore };
        effect.AddToClassList("card-effect");
        element.Add(effect);

        if (!string.IsNullOrWhiteSpace(card.flavour))
        {
            var flavour = new Label(card.flavour) { pickingMode = PickingMode.Ignore };
            flavour.AddToClassList("card-flavour");
            element.Add(flavour);
        }

        element.RegisterCallback<ClickEvent>(_ => Take(card));
        return element;
    }

    // CLOSE FIRST, ANNOUNCE SECOND, and the order is not a detail — it is the whole reason a three-level
    // gain shows three windows. Whoever is listening opens the NEXT draft the moment it hears, and it opens
    // it in this same window: announcing first meant the second draft was built into a popup that this
    // method then closed on the very next line, so two of the three levels vanished without a sound.
    //
    // It is also the right order for the time scale. Closing puts it back to what it was, so the next window
    // freezes from a running game — the state its own OnShow expects to find and to restore later.
    void Take(RunUpgradeCard card)
    {
        Close();
        Chosen?.Invoke(card);
    }

    public override void OnShow()
    {
        base.OnShow();
        _block = _gate?.Block(InputKind.All, "run-upgrade");
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    public override void OnHide()
    {
        Time.timeScale = _prevTimeScale;
        _block?.Dispose();
        _block = null;
        base.OnHide();
    }
}
