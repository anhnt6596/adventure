using System;
using System.Collections.Generic;
using Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

// The character window: who you are playing across the top, a strip of tabs under it, and one page on at a
// time. Opened by clicking the portrait on the HUD.
//
// UXML/USS live next to this file and MUST be named CharacterPopup.* — the UI registry matches by file name.
//
// ONE POPUP, MANY PAGES, and that is the change this class exists to make. Upgrades used to be a popup of its
// own, so the second screen a character needs — gear, traits, a bestiary — would have been a second popup with
// its own head, its own close button and its own way in. A tab strip says "these are all the same question
// asked about the same character".
//
// ADDING A TAB is five small things and no changes to any of them: a uxml + uss pair beside this file, a
// <ui:Template> and a <ui:Instance class="tab-page"> in CharacterPopup.uxml, a Button in the strip, and one
// entry in the _tabs list below. Nothing here switches on which tab it is.
//
// A TAB IS NOT A VIEW. Pages are plain classes driving a subtree of this file's markup (see UpgradeTab), not
// UIViews the UISystem could be asked to open by itself. That is deliberate: a page opened outside this window
// would have no head, no way out, and no idea which character it was about.
//
// IT PAUSES, exactly the way PausePopup does and for the same reason: everything in here is a decision, the
// numbers it is weighed against move on their own, and a stomach emptying while the player reads is a reason to
// stop reading. Time.timeScale is what stops the world; the input gate is what stops the body being steered
// through UI clicks. TimeService runs on scaled time, so hunger and its drain stop with it.
public class CharacterPopup : BasePopup
{
    // A full-screen panel that fades; zoom is for the small centred popups.
    protected override EffectType AppearFx => EffectType.Fade;

    // One page in the strip. A record of what to press, what to badge and what to turn on — so switching tabs
    // is one loop rather than a switch that has to be edited in three places.
    class Tab
    {
        public string Key;                 // NotificationService key; null for a page nothing ever badges
        public Button Button;
        public VisualElement Badge;
        public Action Show;
        public Action Hide;
    }

    readonly List<Tab> _tabs = new List<Tab>();
    readonly UpgradeTab _upgrade;
    readonly VisualElement _avatar;
    readonly Label _charName;

    IArtProvider _art;
    IInputGate _gate;

    // GameScope, so pushed in rather than injected — see UpgradeTab's note on the same split. Only the service
    // this window itself reads is kept; everything else Bind receives belongs to a page and goes straight
    // through to it, because a copy held here would be a second answer to "which game are we showing".
    NotificationService _notifications;

    IDisposable _block;
    float _prevTimeScale = 1f;
    string _current;

    public CharacterPopup(VisualElement root) : base(root)
    {
        _avatar = root.Q<VisualElement>("avatar");
        _charName = root.Q<Label>("char-name");
        root.Q<Button>("close-button")?.RegisterCallback<ClickEvent>(_ => Close());

        _upgrade = new UpgradeTab(root.Q<VisualElement>("tab-upgrade"));

        _tabs.Add(new Tab
        {
            Key = UpgradeNotifier.Key,
            Button = root.Q<Button>("tab-upgrade-button"),
            Badge = root.Q<VisualElement>("tab-upgrade-badge"),
            Show = _upgrade.OnShow,
            Hide = _upgrade.OnHide,
        });

        foreach (var tab in _tabs)
        {
            var key = tab.Key;
            tab.Button?.RegisterCallback<ClickEvent>(_ => Select(key));
        }
    }

    [Inject]
    public void Construct(IArtProvider art, IInputGate gate)
    {
        _art = art;
        _gate = gate;
    }

    // Called by whoever opened it, straight after Show — so OnShow has already run and must not assume any of
    // this has arrived yet.
    public void Bind(IGetUpgradeTree trees, UpgradeSystem upgrades, CharacterLevels levels,
                     NotificationService notifications, string characterId)
    {
        if (_notifications != null) _notifications.Changed -= RefreshBadges;
        _notifications = notifications;
        if (_notifications != null) _notifications.Changed += RefreshBadges;

        if (_charName != null) _charName.text = string.IsNullOrEmpty(characterId) ? "—" : characterId;

        var portrait = _art?.Avatar(characterId);
        if (_avatar != null)
            _avatar.style.backgroundImage = portrait != null
                ? new StyleBackground(portrait)
                : new StyleBackground(StyleKeyword.None);

        _upgrade.Bind(trees, upgrades, levels, _art, characterId);

        // The tab that is already on screen counts as looked at. OnShow chose it before this method had a
        // service to tell, so the acknowledgement lands here — which is also what makes "open the window" clear
        // the badge, since the window opens onto this tab.
        Acknowledge(_current);
        RefreshBadges();
    }

    public override void OnShow()
    {
        base.OnShow();

        _block = _gate?.Block(InputKind.All, "character");
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;   // freeze the world (UI Toolkit FX runs unscaled)

        // Always back to the first tab. A window that reopens where it was left is only kind if the player
        // chose to leave it there; with one tab it is invisible either way, and remembering is a preference
        // worth adding the day the strip is long enough for it to be one.
        Select(_tabs.Count > 0 ? _tabs[0].Key : null);
        RefreshBadges();
    }

    public override void OnHide()
    {
        Time.timeScale = _prevTimeScale;
        _block?.Dispose();
        _block = null;

        if (_notifications != null) _notifications.Changed -= RefreshBadges;

        // Every page off, not just the current one: the next open picks its own, and a page left subscribed
        // would keep reacting to a game it is no longer showing.
        foreach (var tab in _tabs) tab.Hide?.Invoke();
        _current = null;

        base.OnHide();
    }

    void Select(string key)
    {
        _current = key;

        foreach (var tab in _tabs)
        {
            bool on = tab.Key == key;
            if (on) tab.Show?.Invoke(); else tab.Hide?.Invoke();

            if (tab.Button == null) continue;
            if (on) tab.Button.AddToClassList("tab--current");
            else tab.Button.RemoveFromClassList("tab--current");
        }

        // Looking at a tab is what dismisses its badge — not closing it, and not buying anything on it. See
        // NotificationService for why that does not simply re-badge on the next refresh.
        Acknowledge(key);
        RefreshBadges();
    }

    void Acknowledge(string key)
    {
        if (!string.IsNullOrEmpty(key)) _notifications?.Acknowledge(key);
    }

    void RefreshBadges()
    {
        foreach (var tab in _tabs)
        {
            if (tab.Badge == null) continue;
            bool show = tab.Key != null && _notifications != null && _notifications.Has(tab.Key);
            tab.Badge.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
