using System.Collections.Generic;
using Core.UI;
using UnityEngine;

// The upgrades a player collects inside one arena, and the draft that hands them out. Born with the run, and
// — this is the part that matters — taken back off the character when the run ends.
//
// EVERYTHING IS TAGGED WITH THIS OBJECT. Every modifier a card adds carries `this` as its source, so closing
// the run is one RemoveBySource per stat rather than a ledger of what was applied. The body outlives the run
// (you walk out of the arena in it), so anything left tagged would follow the player home — the single way
// this system could break the rule that a run's power dies with the run.
//
// CARDS ARE APPLIED, NOT REMEMBERED AS RANKS. The world's tree stores counts and rebuilds the whole set on
// every change, because a save has to survive a node being repriced. A run has no save and lasts twenty
// minutes: a card taken is a modifier added, and the pile is thrown away whole at the end. `_taken` exists
// only so a card with a per-run limit can be kept out of later hands.
//
// ONE DRAFT PER LEVEL, QUEUED. A fat kill can carry the player through two levels at once, and that is two
// choices — shown one after the other rather than merged, because merging would quietly halve what the run
// paid for.
//
// AND ONE ON ARRIVAL. Walking in is level 1, and level 1 gets a card like every level after it: the run opens
// on a decision rather than on a minute of hitting things with the character you brought. It also means every
// run is DIFFERENT from its first second, which is most of what makes starting one again worth doing.
public class RunUpgrades
{
    readonly CardLibrary _library;
    readonly RunLevel _level;
    readonly IPlayer _player;
    readonly IUISystem _ui;
    readonly int _handSize;

    readonly List<RunUpgradeCard> _hand = new List<RunUpgradeCard>();
    readonly List<RunUpgradeCard> _pool = new List<RunUpgradeCard>();   // scratch for a draw
    readonly Dictionary<RunUpgradeCard, int> _taken = new Dictionary<RunUpgradeCard, int>();
    readonly HashSet<string> _unlocked = new HashSet<string>();
    readonly List<SkillModifier> _skillBuffs = new List<SkillModifier>();

    RunStats _runStats;  // the run's own layer over the character's numbers — built on first use
    bool _dealtAny;      // a hand has been offered at least once this run
    bool _complained;    // wiring is reported once, not once per level
    int _pending;        // levels gained but not yet chosen for
    bool _drafting;      // a window is open

    public RunUpgrades(CardLibrary library, RunLevel level, IPlayer player, IUISystem ui, int handSize)
    {
        _library = library;
        _level = level;
        _player = player;
        _ui = ui;
        _handSize = Mathf.Max(1, handSize);

        _level.LeveledUp += OnLeveledUp;

        // The opening card. Queued rather than shown from here so it goes through exactly the path every
        // other draft does — one place decides what a hand looks like, and it is not the constructor.
        _pending = 1;
        Next();
    }

    // Said once per run. A missing deck does not fix itself between levels, and twenty identical red lines
    // buries whatever else the console was trying to say.
    void Complain(string message)
    {
        if (_complained) return;
        _complained = true;
        Debug.LogError(message);
    }

    void OnLeveledUp()
    {
        _pending++;
        Next();
    }

    void Next()
    {
        if (_drafting || _pending <= 0) return;

        if (_library == null)
        {
            Complain($"[{nameof(RunUpgrades)}] no {nameof(CardLibrary)} — levelling up can offer nothing.");
            _pending = 0;
            return;
        }

        if (_ui == null)
        {
            Complain($"[{nameof(RunUpgrades)}] no {nameof(IUISystem)} — the draft cannot be shown.");
            _pending = 0;
            return;
        }

        Draw();
        if (_hand.Count == 0)
        {
            // Empty is a legitimate late-run state (everything limited is spent), but on the FIRST draft it
            // means no usable cards at all — which reads exactly like a broken system.
            if (!_dealtAny)
                Complain($"[{nameof(RunUpgrades)}] nothing to offer on the first draft. Cards are Configs, so " +
                         "each needs to be in the ConfigRegistry (rebuild it), unlocked (unlockedByDefault or " +
                         "earned), have a weight above 0, and do something.");
            _pending = 0;
            return;
        }
        _dealtAny = true;

        _pending--;
        _drafting = true;

        var popup = _ui.Show<RunUpgradePopup>();
        if (popup == null)
        {
            Complain($"[{nameof(RunUpgrades)}] the UI system has no {nameof(RunUpgradePopup)}. Its UXML must " +
                     "sit next to the class and share its name, and the registry has to be rebuilt — it " +
                     "regenerates on entering play, so a fresh popup needs one run through the editor first.");
            _drafting = false;
            _pending = 0;
            return;
        }

        popup.Chosen += OnChosen;
        popup.Bind(_level.Level, _hand);
    }

    // A hand of DIFFERENT cards, weighted, skipping anything this run has had its fill of. Without
    // replacement, so a level-up never offers the same card twice side by side — a choice between a thing and
    // itself is not a choice.
    //
    // Fewer than the hand size is a real state rather than an error: late in a long run everything limited may
    // be spent, and a hand of two beats a hand of two and a blank.
    //
    // THE POOL IS REBUILT EVERY DRAW, out of what the player has unlocked. Cheap, and it is the only way a
    // card unlocked mid-session can turn up in the next draft.
    void Draw()
    {
        _hand.Clear();
        _library.Unlocked(_pool);

        for (int i = _pool.Count - 1; i >= 0; i--)
        {
            var card = _pool[i];
            bool usable = card != null && card.weight > 0f
                          && (card.buffs is { Length: > 0 } || card.effect != null)
                          && !(card.maxPerRun > 0 && _taken.TryGetValue(card, out int n) && n >= card.maxPerRun);
            if (!usable) _pool.RemoveAt(i);
        }

        for (int i = 0; i < _handSize && _pool.Count > 0; i++)
        {
            float total = 0f;
            foreach (var card in _pool) total += card.weight;
            if (total <= 0f) break;

            float roll = Random.value * total;
            int picked = _pool.Count - 1;
            for (int j = 0; j < _pool.Count; j++)
            {
                roll -= _pool[j].weight;
                if (roll <= 0f) { picked = j; break; }
            }

            _hand.Add(_pool[picked]);
            _pool.RemoveAt(picked);   // without replacement — see above
        }
    }

    void OnChosen(RunUpgradeCard card)
    {
        var popup = _ui.Get<RunUpgradePopup>();
        if (popup != null) popup.Chosen -= OnChosen;

        _drafting = false;
        Take(card);

        Next();   // a second level gained in the same instant gets its own window
    }

    void Take(RunUpgradeCard card)
    {
        if (card == null) return;

        _taken[card] = _taken.TryGetValue(card, out int n) ? n + 1 : 1;

        var mc = _player?.Current;
        var stats = mc != null ? mc.Stats as MainCharStats : null;
        if (stats == null)
        {
            Debug.LogError($"[{nameof(RunUpgrades)}] no live character to put '{card.title}' on — the card is " +
                           "lost. A draft can only happen while the run has a body.");
            return;
        }

        // Built here rather than at the start of the run: the layer caches what each stat was worth on first
        // touch, and the body it should be reading is the one standing now.
        _runStats ??= new RunStats(stats, this);

        if (card.buffs != null)
            foreach (var buff in card.buffs)
                _runStats.Apply(buff);

        if (card.effect == null) return;

        // Skill buffs accumulate for the WHOLE run and are re-pushed each time, because a skill's tunables are
        // cleared per source and re-added: pushing only the newest would drop everything taken before it.
        _skillBuffs.Clear();
        card.effect.Apply(new UpgradeContext(stats, this, _unlocked, _skillBuffs));
        PushSkillBuffs(mc);
    }

    // Cards that name a skill's own number (a dash's distance, a cooldown) land here rather than on a stat.
    // Added under this run's source, so the character tree's own skill buffs — tagged with PlayerSystem's
    // source — are untouched by anything the run does.
    void PushSkillBuffs(MCController mc)
    {
        if (_skillBuffs.Count == 0 || mc == null) return;

        foreach (var buff in _skillBuffs)
        {
            foreach (var skill in mc.GetComponentsInChildren<CharacterSkill>(true))
            {
                if (skill.Key != buff.Skill) continue;

                var tunable = skill.Modifiable(buff.Stat);
                if (tunable == null)
                {
                    Debug.LogWarning($"[{nameof(RunUpgrades)}] a card names '{buff.Stat}' on skill " +
                                     $"'{buff.Skill}', which has no tunable by that name — it does nothing.");
                    continue;
                }

                skill.BeginBatch();
                try { tunable.Add(new StatModifier(buff.Amount, buff.Kind, this)); }
                finally { skill.EndBatch(); }
            }
        }
        _skillBuffs.Clear();
    }

    // Everything this run put on the character, taken back off. Called when the run closes — and it has to
    // work on a body that may be about to be replaced anyway (a death respawn), which is why it is safe to
    // call with no body at all.
    public void Dispose()
    {
        _level.LeveledUp -= OnLeveledUp;

        var popup = _ui?.Get<RunUpgradePopup>();
        if (popup != null)
        {
            popup.Chosen -= OnChosen;
            popup.Close();   // a run cannot end with its draft still on screen holding time at zero
        }

        _runStats?.Dispose();
        _runStats = null;

        var mc = _player?.Current;
        if (mc == null) return;

        (mc.Stats as MainCharStats)?.RemoveBySource(this);

        foreach (var skill in mc.GetComponentsInChildren<CharacterSkill>(true))
            skill.RemoveBySource(this);
    }
}
