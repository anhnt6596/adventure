using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Core.Save;

// The one owner of the player character: spawns it from an id, holds the live reference, and persists WHICH
// character is current. It spawns into a CHILD SCOPE that carries the id's stats + inventory config, so every
// injected component on the prefab (MC, Picker, attacks) receives them through its normal [Inject] —
// no per-component wiring here.
public class PlayerSystem : IPlayer, IStartable, ISavable
{
    const string DefaultId = "MC 1";

    readonly IGetMCConfig _mcConfig;
    readonly PrefabRegistry _prefabs;
    readonly IObjectResolver _container;
    readonly SaveService _save;
    readonly IGetUpgradeTree _trees;
    readonly UpgradeSystem _upgrades;
    readonly CharacterLevels _levels;

    string _currentId = DefaultId;
    IScopedObjectResolver _scope;   // per-spawn scope holding this body's stats/config; disposed on the next spawn
    MainCharStats _stats;           // the live set, kept so a node bought mid-game can land on it now
    MainCharStatsConfig _cfg;       // the current body's config, kept for the same reason: re-basing on a level-up

    public MCController Current { get; private set; }
    public bool Exists => Current != null;
    public Vector3 Position => Current != null ? Current.transform.position : Vector3.zero;
    public event Action<MCController> Spawned;

    public string SaveKey => "player";

    [Inject]
    public PlayerSystem(IGetMCConfig mcConfig, PrefabRegistry prefabs, IObjectResolver container, SaveService save,
                        IGetUpgradeTree trees, UpgradeSystem upgrades, CharacterLevels levels)
    {
        _mcConfig = mcConfig;
        _prefabs = prefabs;
        _container = container;
        _save = save;
        _trees = trees;
        _upgrades = upgrades;
        _levels = levels;
        _save.Register(this);   // runs Load() now → _currentId from save (DefaultId on a fresh game)

        // Buying a node has to show up NOW — the popup is open, nothing respawns, and an upgrade you cannot
        // feel until you next die is a bug nobody reports for a month.
        _upgrades.Changed += ApplyUpgrades;

        // Same argument for the level: hunger drain is priced off it, and a level gained mid-trip has to bite
        // on that trip. Filtered to the body actually out there — every character banks its own level, and the
        // ones sitting at home have no stats to re-base.
        _levels.Changed += OnLevelChanged;
    }

    void OnLevelChanged(string characterId)
    {
        if (characterId == _currentId) ApplyLevelScaling();
    }

    public void Start()
    {
        if (Spawn(_currentId)) return;
        if (_currentId == DefaultId) return;   // the fallback itself failed — nothing left to try

        // A saved id that can no longer spawn (config renamed, prefab removed) would otherwise leave the game
        // with no player at all, permanently. Fall back so the next save heals the choice.
        Debug.LogWarning($"[{nameof(PlayerSystem)}] saved character '{_currentId}' can't spawn — falling back to '{DefaultId}'.");
        _currentId = DefaultId;
        Spawn(_currentId);
    }

    // Swap the player to another character id, and only then record the choice: a failed switch must not
    // leave a dead id behind. SaveService saves everything on quit, so a dirty _currentId would be persisted
    // even without the explicit Save here — and would spawn nothing on the next run.
    public void SwitchTo(string id)
    {
        if (!Spawn(id)) return;
        _currentId = id;
        _save.Save(SaveKey);
    }

    // True only once a live MC is Current. Everything is checked BEFORE the old body comes down, so a
    // bad id leaves the current player standing instead of destroying it and finding out afterwards.
    bool Spawn(string id)
    {
        var ident = _prefabs.Get(id);
        if (ident == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — no prefab with that id in the registry.");
            return false;
        }

        // The registry holds every Identifiable prefab, not just characters — a prop or enemy id must not
        // get this far, or we'd tear down the player for a body that can never be one.
        if (ident.GetComponent<MCController>() == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — its prefab has no {nameof(MCController)}.");
            return false;
        }

        var cfg = _mcConfig.Get(id);
        if (cfg == null)
        {
            Debug.LogError($"[{nameof(PlayerSystem)}] can't spawn '{id}' — no config with that id in the registry.");
            return false;
        }

        // A swap takes over where the old body stood, so switching character doesn't teleport the player.
        // Read the pose before destroying it; with no body yet (first spawn) fall back to the prefab's own
        // transform — identical to a plain Instantiate — and let MapService move it to the map's gate.
        bool hasBody = Current != null;
        var position = hasBody ? Current.transform.position : ident.transform.position;
        var rotation = hasBody ? Current.transform.rotation : ident.transform.rotation;

        // tear down the previous body + its scope (respawn / switch)
        if (hasBody) UnityEngine.Object.Destroy(Current.gameObject);
        _scope?.Dispose();

        _stats = new MainCharStats(cfg);
        _cfg = cfg;
        _scope = _container.CreateScope(b =>
        {
            // Both faces of ONE registration: everything that only READS a stat asks for ICharacterStats and
            // cannot modify one; the few components that genuinely move a stat (Hunger pausing the drain)
            // ask for MainCharStats, and asking for it is the declaration.
            //
            // As().AsSelf() rather than two RegisterInstance calls — RegisterInstance also registers the
            // concrete type, so registering it twice puts MainCharStats in the registry twice and the scope
            // refuses to build. Same shape GameScope already uses for PlayerSystem itself.
            b.RegisterInstance(_stats).As<ICharacterStats>().AsSelf();
            b.RegisterInstance<IHungerConfig>(cfg);
            b.RegisterInstance<IDamageableConfig>(cfg);   // MC's HP — MCController exposes it to its Damageable
        });

        // Both before the body is built, so Damageable's Start reads a maximum that already includes them.
        // Level first: it moves bases, and the upgrades that land on top are shares OF those bases.
        ApplyLevelScaling();
        ApplyUpgrades();

        // NOTE: _scope.Instantiate() would re-route injection through the parent LifetimeScope (which lacks
        // ICharacterStats) — instantiate plainly, then inject with THIS scope so the per-spawn stats bind.
        var go = UnityEngine.Object.Instantiate(ident.gameObject, position, rotation);
        _scope.InjectGameObject(go);
        Current = go.GetComponent<MCController>();   // guaranteed by the prefab check above

        // Worked out above, before this body existed. Now it does, so it can be told.
        ApplySkillUpgrades();

        Spawned?.Invoke(Current);
        return true;
    }

    // What the character's level does to its own numbers, written into the BASES rather than added as
    // modifiers — see MainCharStatsConfig.HungerDrainAt for why that is the shape. Idempotent, because it
    // ASSIGNS rather than accumulates: calling it twice at the same level lands on the same number, so spawn,
    // level-up and any later caller can all use the one path without keeping track of each other.
    //
    // Only hunger drain today. Anything else that should grow with level joins this method, and gets its curve
    // next to the number it scales in the config.
    void ApplyLevelScaling()
    {
        if (_stats == null || _cfg == null) return;

        var drain = _stats.Modifiable(StatId.HungerDrain);
        if (drain != null) drain.BaseValue = _cfg.HungerDrainAt(_levels.Level(_currentId));
    }

    // Everything the tree adds is tagged with this one object, so re-applying is "take all of mine off, put
    // all of mine back" — one code path that serves buying, resetting and respawning alike. Three separate
    // paths would be three chances for them to disagree about what is currently on the character.
    static readonly object UpgradeSource = new object();

    // Which skills the tree has opened, rebuilt from nothing on every pass. Held on the system rather than on
    // the body because the body is thrown away and remade on every spawn, and this outlives that: the answer
    // belongs to the CHARACTER, the components are just where it lands.
    readonly HashSet<string> _unlockedSkills = new HashSet<string>();
    readonly List<SkillModifier> _skillBuffs = new List<SkillModifier>();

    void ApplyUpgrades()
    {
        if (_stats == null) return;

        // Batched, because taking everything off and putting it back is ONE change to the character and has to
        // reach the rest of the game as one. Unbatched, the ceiling of every stat falls to its base and climbs
        // again inside this method, and whoever reacts to the fall has already acted by the time the climb
        // arrives — see Stat.BeginBatch.
        _stats.BeginBatch();
        try
        {
            _stats.RemoveBySource(UpgradeSource);

            // Emptied first, exactly like the modifiers above: what is open is derived from what is bought,
            // so a respec has to be able to shut a skill again without anybody tracking that it was opened.
            _unlockedSkills.Clear();
            _skillBuffs.Clear();

            var tree = _trees?.Get(_currentId);
            if (tree == null) return;

            // APPLIED ONCE PER RANK. A rank is the same upgrade again, so three ranks of "+10% attack" is
            // three modifiers of ten percent — which is exactly what the rebuild-from-scratch model makes
            // free: nothing has to know how much a node was worth last time, because everything came off
            // first. It is also why an effect has to be safe to apply repeatedly (see IUpgradeEffect).
            var context = new UpgradeContext(_stats, UpgradeSource, _unlockedSkills, _skillBuffs);
            foreach (var node in tree.Nodes)
            {
                if (node?.effect == null) continue;

                int rank = _upgrades.RankOf(_currentId, node);
                for (int i = 0; i < rank; i++) node.effect.Apply(context);
            }
        }
        finally
        {
            _stats.EndBatch();
            ApplySkillUpgrades();
        }
    }

    // Onto whatever body is standing, and it may be none: at spawn this pass runs BEFORE the body exists, so
    // Spawn calls it again once it does. Two calls rather than one because the set has to be finished before
    // Damageable's Start reads a maximum, and the components only exist after that.
    //
    // The same take-it-all-off-and-put-it-back the stats get, for the same reason: what a skill is worth is a
    // function of what has been bought, so a respec has to be able to undo it without anybody remembering.
    void ApplySkillUpgrades()
    {
        if (Current == null) return;

        foreach (var skill in Current.GetComponentsInChildren<CharacterSkill>(true))
        {
            skill.SetUnlocked(_unlockedSkills.Contains(skill.Key));

            skill.BeginBatch();
            try
            {
                skill.RemoveBySource(UpgradeSource);

                foreach (var buff in _skillBuffs)
                {
                    if (buff.Skill != skill.Key) continue;

                    var stat = skill.Modifiable(buff.Stat);
                    if (stat == null)
                    {
                        // Said out loud rather than dropped: a mistyped tunable is a node the player paid for
                        // that does nothing, and nothing else in the game would ever mention it.
                        Warn($"[{nameof(PlayerSystem)}] upgrade names '{buff.Stat}' on skill '{buff.Skill}', " +
                             "which has no tunable by that name — that node does nothing.");
                        continue;
                    }

                    stat.Add(new StatModifier(buff.Amount, buff.Kind, UpgradeSource));
                }
            }
            finally
            {
                skill.EndBatch();
            }
        }
    }

    // Once per message per session. This runs on every purchase and every spawn, so an unguarded log would
    // fill the console with the same line for one typo.
    readonly HashSet<string> _warned = new HashSet<string>();

    void Warn(string message)
    {
        if (_warned.Add(message)) Debug.LogWarning(message);
    }

    public void Save(SaveBag bag) => bag.Set("current", _currentId);
    public void Load(SaveBag bag) => _currentId = bag.Get("current", DefaultId);
}
