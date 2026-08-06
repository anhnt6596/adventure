using System;
using System.Collections.Generic;
using Core.Save;
using VContainer;

// What has something new in it, and whether the player has looked at it yet. One entry per place a badge can
// appear — today "upgrade", tomorrow a quest log, a crafting bench, a letter from home.
//
// COUNTS, NOT A BOOLEAN, and that is the whole design. A flag would need somebody to remember to turn it off,
// and "off until the next one" is not something a flag can say: turn it off after the player looks, and the
// source that computes it turns it straight back on, because there ARE still unspent points. Two numbers make
// that state expressible without a conversation between them — a source only ever says how many things are
// waiting, and this decides whether that is news.
//
//     waiting     how many are there, pushed by whoever knows (see Report)
//     seen        how many there were when the player last looked (see Acknowledge)
//     badge       waiting > seen
//
// SEEN CLAMPS DOWN when waiting falls, and that is not tidying — it is the one rule that keeps the badge
// working after a spend. Acknowledge three points, spend all three, earn one: without the clamp the badge
// compares 1 against 3 and stays quiet about a point the player has never seen. With it, spending drags seen
// down to 0 and the new point is news again.
//
// SEEN IS SAVED, waiting is not. Waiting is a fact about the running game and whoever owns it re-reports it on
// load; seen is a fact about the player, and a badge you dismissed yesterday coming back tomorrow is exactly
// the nag this exists to prevent.
public class NotificationService : ISavable
{
    readonly Dictionary<string, int> _waiting = new Dictionary<string, int>();
    readonly Dictionary<string, int> _seen = new Dictionary<string, int>();
    readonly SaveService _save;

    public string SaveKey => "notifications";

    // One event for the lot rather than one per key. Every listener is a badge that redraws by asking, so a
    // finer signal would only mean more subscriptions doing the same work.
    public event Action Changed;

    [Inject]
    public NotificationService(SaveService save)
    {
        _save = save;
        _save.Register(this);   // loads _seen
    }

    // How many things are waiting under this key. Called by whoever can work that out — this class knows
    // nothing about points or quests, which is what lets a second source join without touching it.
    public void Report(string key, int waiting)
    {
        if (string.IsNullOrEmpty(key)) return;

        waiting = Math.Max(0, waiting);
        bool moved = false;

        if (Waiting(key) != waiting)
        {
            _waiting[key] = waiting;
            moved = true;
        }

        // Nobody can have seen more than there are. See the note at the top: this is what makes a point earned
        // after a full spend count as news.
        if (Seen(key) > waiting)
        {
            _seen[key] = waiting;
            _save.Save(SaveKey);
            moved = true;
        }

        if (moved) Changed?.Invoke();
    }

    // Waiting > seen, and no separate "is there anything" test: seen is clamped to waiting, so the only way to
    // be above it is to have something the player has not seen.
    public bool Has(string key) => !string.IsNullOrEmpty(key) && Waiting(key) > Seen(key);

    // For a badge that stands for everything at once — the avatar, which opens the popup the tabs live in.
    public bool Any
    {
        get
        {
            foreach (var kv in _waiting)
                if (kv.Value > Seen(kv.Key)) return true;
            return false;
        }
    }

    // The player looked. Saved immediately rather than at the next checkpoint: this is a UI state whose whole
    // job is to not come back, and the run it would come back in is the one that crashed.
    public void Acknowledge(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        int waiting = Waiting(key);
        if (Seen(key) == waiting) return;

        _seen[key] = waiting;
        _save.Save(SaveKey);
        Changed?.Invoke();
    }

    int Waiting(string key) => _waiting.TryGetValue(key, out int n) ? n : 0;
    int Seen(string key) => _seen.TryGetValue(key, out int n) ? n : 0;

    // A flat string->int, the shape CharacterLevels and PayGateSystem already prove round-trips through the
    // serializer.
    public void Save(SaveBag bag) => bag.Set("Seen", new Dictionary<string, int>(_seen));

    public void Load(SaveBag bag)
    {
        _seen.Clear();
        foreach (var kv in bag.Get("Seen", new Dictionary<string, int>()))
            if (!string.IsNullOrEmpty(kv.Key)) _seen[kv.Key] = Math.Max(0, kv.Value);
    }
}
