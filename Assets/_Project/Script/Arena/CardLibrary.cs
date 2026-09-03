using System.Collections.Generic;
using Core.Save;
using VContainer;

// Which upgrade cards this save has. The pool a run draws from, and it belongs to the PLAYER rather than to
// any arena — what the player has learned to find is a fact about them, not about the room they walk into.
// Hanging it off an arena would mean a card was unlocked by going somewhere, which is a different game.
//
// EVERY CARD IN THE PROJECT IS ALREADY KNOWN, because a card is a Config and ConfigRegistry collects those by
// itself. So there is no list of cards to maintain anywhere: dropping a new asset in the project adds it to
// the game, and this only decides which of them are yours yet.
//
// THE SAVE HOLDS THE EARNED ONES ONLY. Cards marked unlockedByDefault are not written down — they are true of
// every save, so storing them would be a copy of the assets that goes stale the day one is reticked. Same
// rule ExperienceSystem keeps for firsts and UpgradeSystem for ranks: store the decision, derive the rest.
public class CardLibrary : ISavable
{
    readonly HashSet<string> _earned = new HashSet<string>();   // unlocked BY PLAY, by card id
    readonly ConfigRegistry _configs;
    readonly SaveService _save;

    public string SaveKey => "cards";

    [Inject]
    public CardLibrary(ConfigRegistry configs, SaveService save)
    {
        _configs = configs;
        _save = save;
        _save.Register(this);   // loads _earned
    }

    public bool IsUnlocked(RunUpgradeCard card)
        => card != null && (card.unlockedByDefault || _earned.Contains(card.Id));

    // Earned by play. Idempotent, and it saves only when something actually changed — this will be called
    // from a reward screen, where the same card being offered twice is not an event.
    public bool Unlock(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || !_earned.Add(cardId)) return false;

        _save.Save(SaveKey);
        return true;
    }

    // Everything the player may be offered right now, filled into the caller's list. Rebuilt on each draw
    // rather than cached: unlocking mid-session is a thing that will happen, and a cached pool is a second
    // answer to "what do I have" that goes stale the moment it does.
    public void Unlocked(List<RunUpgradeCard> into)
    {
        into.Clear();
        if (_configs == null) return;

        foreach (var card in _configs.All<RunUpgradeCard>())
            if (IsUnlocked(card)) into.Add(card);
    }

    public void Save(SaveBag bag) => bag.Set("Earned", new List<string>(_earned));

    public void Load(SaveBag bag)
    {
        _earned.Clear();
        foreach (var id in bag.Get("Earned", new List<string>()))
            if (!string.IsNullOrEmpty(id)) _earned.Add(id);
    }
}
