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
    public override bool CloseOnEscape => false;

    IInputGate _gate;

    readonly Label _level;
    readonly VisualElement _cards;

    IDisposable _block;
    float _prevTimeScale = 1f;

    // The card the player took. Fired before the window closes, so whoever queued the draft can decide
    // whether another one follows immediately — two levels at once is two drafts, not one.
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

    void Take(RunUpgradeCard card)
    {
        // Fire first, close second. Closing restores the time scale, and the next draft in a multi-level
        // gain wants to freeze it again from the frozen state rather than race the restore.
        Chosen?.Invoke(card);
        Close();
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
