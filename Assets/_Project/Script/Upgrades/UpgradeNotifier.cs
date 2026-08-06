using VContainer;
using VContainer.Unity;

// Tells NotificationService how many upgrade points are sitting unspent, so the avatar and the upgrade tab can
// wear a (!) without either of them knowing what a point is.
//
// A CLASS OF ITS OWN rather than a few lines inside UpgradeSystem or GameUI. UpgradeSystem answers questions
// about what has been bought and has no business knowing a badge exists; GameUI is where the popup is opened
// and would only compute this while it happens to be listening. This is the rule "unspent points are news",
// written once, in a class named after it — and it is the shape every notification after it can copy.
//
// THE CURRENT CHARACTER ONLY. Points are per character and so is the tree they are spent on, but one avatar is
// on screen and it belongs to whoever is being played. A character sitting at home with points banked is not
// something to nag about on this trip — the day character select exists, the badge on ITS row is that screen's
// question, asked with the same service.
public class UpgradeNotifier : IStartable
{
    // The key the upgrade tab is registered under. Shared with CharacterPopup, which acknowledges it.
    public const string Key = "upgrade";

    readonly NotificationService _notifications;
    readonly UpgradeSystem _upgrades;
    readonly CharacterLevels _levels;
    readonly IGetUpgradeTree _trees;
    readonly IPlayer _player;

    [Inject]
    public UpgradeNotifier(NotificationService notifications, UpgradeSystem upgrades, CharacterLevels levels,
                           IGetUpgradeTree trees, IPlayer player)
    {
        _notifications = notifications;
        _upgrades = upgrades;
        _levels = levels;
        _trees = trees;
        _player = player;
    }

    // Every one of these can change the answer, and all of them are cheap: Report only raises Changed when a
    // number actually moved, so re-reporting the same count on an unrelated event costs nothing.
    public void Start()
    {
        _levels.Changed += OnLevelChanged;
        _upgrades.Changed += Report;
        _player.Spawned += _ => Report();   // a switch changes whose points these are
        Report();
    }

    void OnLevelChanged(string characterId) => Report();

    void Report()
    {
        var mc = _player?.Current;
        string id = mc != null ? mc.Id : null;

        // No body yet (before the first spawn) is not "nothing waiting" — it is "no question asked yet", and
        // reporting zero here would let the player earn their first level with the badge already acknowledged.
        if (string.IsNullOrEmpty(id)) return;

        var tree = _trees?.Get(id);

        // No tree authored means the points have nowhere to go, so there is nothing to point at. Available
        // would happily report the whole level as spendable and badge a tab with an empty screen behind it.
        if (tree == null) { _notifications.Report(Key, 0); return; }

        _notifications.Report(Key, _upgrades.Available(id, tree));
    }
}
