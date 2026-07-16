# Design

## The pillar: exploration

**Exploration is the point.** Combat and gear matter, but discovering the world is where the fun is.

This is the tie-breaker. When a decision is unclear, the question is *"does this make exploring
better?"* — not *"is this more content"*.

**What it implies about the other systems:** gear and combat are **enablers**, not rivals. Better
gear should mean *reach further, survive the next region, open the next gate* — the reward for
exploring, and the means to explore more.

## The core loop

```
go further out  ──►  better drops + a chance at a new checkpoint
      ▲                                  │
      │                                  ▼
 the old frontier                  unlock it: home moves outward
 becomes ordinary  ◄──  gear up ──►  the next frontier is reachable
```

1. Push out from the last home. Further = more dangerous = **better gear, and the chance to find
   and unlock a new checkpoint**.
2. Unlocking a checkpoint **moves home outward**. What was the frontier becomes *ordinary ground*
   you now pass through safely.
3. Stronger gear + a closer home ⇒ a new frontier is now within reach. Repeat.

The frontier is a **ratchet**: it only moves out, and every rung of it eventually becomes a
commute. That feeling — *this place used to kill me* — is the game.

**Why this settles the grind worry:** rewards scale with **how far out you are**, so farming a known,
safe map is *inefficient by construction*. The optimal play is always to push. The merge ladder
(2048 Greys for a Diamond) then measures **how far you've travelled**, not how long you've stood
still — the maths is identical, the experience is the opposite. Keep it that way: **the day a safe
map out-farms the frontier, the drop table is broken.**

**And it settles the death penalty:** losing gear far out is the *cost of the frontier*, and it's
temporary by design — once you gear up, that ground becomes safe and the run back is trivial. The
penalty is steep exactly where the game wants tension, and self-erasing exactly where it doesn't.
If players avoid the frontier instead of respecting it, it's tuned too high.

## Look

2D sprites in a 3D world — Don't Starve style. Billboarded sprites, hand-drawn art,
a top-down-ish orbit camera the player can rotate (Q/E snap).

Lighting is **faked, not real**: a second camera renders only the `Light` layer into a
buffer; a full-screen veil multiplies the scene down to `_DarkColor` and reveals it where
the buffer is bright. No real-time lights, no shadow maps. Cheap and mobile-friendly.

## World

- Character explores maps on foot.
- **Open world, but every map is hand-built.** No procedural generation.
- **A map is a prefab**, not a scene. One GameScene; jumping swaps the map prefab inside it.
- Maps connect via **jump points**: stepping on one swaps to another map, covered by a
  **quick transition effect** — no walk-through, no seamless streaming, no scene load.
- Some jump points are **locked behind an achievement**: reach a level, pay currency, etc.

**Why this is cheap (and deliberate):** exactly one map is live at a time, and the jump is a
cut, not a stream. That drops streaming, cross-region sim LOD and scene-load juggling from the
problem set. Maps are small and the game is 2D — performance is not a design constraint here.
The world *feels* large; the runtime holds one prefab.

Because the map is a prefab and not a scene, **`GameScope` survives a jump** — character,
camera, UI and services all persist. Only the map contents are destroyed and rebuilt.

### Ground & movement — TO RESEARCH

- Ground is drawn as a **grid of 2D tiles**.
- The same grid carries **obstacles**: cells are walkable or not.
- Not designed yet: tile authoring, how the walkable mask is defined/baked, how movement reads
  it (free movement clamped by cells vs cell-to-cell). `Framework/GridBuild` already has grid
  math + polygon-baked blocked cells — likely the starting point.

## Menu & characters

The start screen is a **menu laid over the live game** — the scene runs behind it as the
background. Pressing START just **hides the menu**; nothing loads, nothing spawns.

- The menu holds the other entry buttons too (not just START).
- It has a **change-character** button. Picking a character **updates the scene immediately**
  (live preview); pressing START **applies** that choice.
- The game **unlocks multiple characters**, each serving a different playstyle.
- **Characters can only be changed at home.**

### What makes characters different

Deliberately narrow — playstyle **emerges from numbers**, not from bespoke code per character:

| Axis | Notes |
| --- | --- |
| Stats | move speed, HP, regen, attack power |
| Attack | the kind of hit: melee swing, bow shot, … |
| Skill | one special ability, unique per character |

So a character is mostly **data**: a config of stats + which attack + which skill. That fits the
existing pieces — `Stat`/`StatModifier` already model the numbers, and `Config` + `ConfigRegistry`
already resolve data by id (which is also what unlocking and swapping need).

Only **attack** and **skill** are behaviour rather than numbers, so they're the one part that needs
more than a config field. Keep the count of attack kinds and skills small; if it ever wants a
"new concept per character", that's a signal the differentiation stopped being data.

## Gear: gacha + merge

Gear is pulled from **gacha** and upgraded by **merging two identical items into one a step up**
the ladder.

**The ladder** — colours, most with 2 tiers, ends included with 1:

| # | Rung | | # | Rung |
| --- | --- | --- | --- | --- |
| 0 | Grey | | 6 | Purple II |
| 1 | Green I | | 7 | Orange I |
| 2 | Green II | | 8 | Orange II |
| 3 | Blue I | | 9 | Red I |
| 4 | Blue II | | 10 | Red II |
| 5 | Purple I | | 11 | Diamond |

12 rungs. Merge is `2 × rung(n) → 1 × rung(n+1)`.

**Where gear actually comes from:** drops land **directly at various rungs**, and the rung
distribution **shifts up the further out you are** — the frontier drops better gear, rarely a high
rung outright. Merging is how you **convert surplus low rungs upward**, not the main path.

So `2¹¹ = 2048 Greys per Diamond` is the **ceiling** — the worst case of grinding Greys alone — not
the expected route. That's the point: **merge is the floor** (junk drops are never worthless),
**exploration is the ceiling** (distance buys rungs directly). The two systems stack instead of
competing, and both point outward.

Balance numbers: **later.** The shape is what matters now.

### Modelling it — the trap to avoid

An item is **`(definition id, rung)`**. The ladder is a **rule**, not authored content:

```
Merge(a, b) : a.def == b.def && a.rung == b.rung  ->  new Item(a.def, a.rung + 1)
```

Do **not** create a config/asset per colour-tier of every item. That's enumeration of a
combinatorial space: 20 gear definitions × 12 rungs = 240 assets to author and maintain, and every
new item type multiplies it again. One definition + a rung index composes the same space for free,
and merge stays a three-line rule instead of a lookup table.

Same for stats: scale them **from the rung** (a curve/formula per definition), rather than
authoring 12 stat blocks per item.

**Only identical items merge** — same definition, same rung. Nothing cross-definition.

**Open:** does merging past a colour's last tier roll into the next colour automatically? The
table reads as one flat 12-rung ladder, so: yes.

## Homes & checkpoints

There are **several homes**, unlocked over time. They are the game's checkpoints: the only place
you can save, swap character, and the place you respawn to.

## Business model

**One premium game, two platforms.** Not a F2P game and a paid game — the same game.

- **Steam:** buy up front.
- **Mobile:** free **demo**; buy once to continue. Mobile isn't the revenue play — it's reach:
  let people try it and get hooked first.
- **Ads (mobile, sparingly):** rewarded, never forced. The one that fits: **watch an ad on death to
  keep your gold**. It's a choice offered at a bad moment, not a tax — and it doesn't distort any
  behaviour the game wants (unlike an ad on saving, which would tax going home).
- **Daily roll:** one free gacha roll on a new day, both platforms. Mobile may offer extra rolls for
  watching an ad. Small enough to be **noise** — a "welcome back" moment, not an income source.
- Mobile may be **tuned easier** than Steam (touch is worse, sessions are shorter). Keep that in
  **difficulty** (enemy HP/damage), *not* in resource income — two economies means two balances to
  maintain, for one solo dev.

**Why premium-both matters to the design:** there is no monetization pulling on the tuning, so the
grind can't be inflated to sell relief. **The Steam build is the conscience of the mobile build** —
it has to be good when nobody pays anything, and Steam reviews won't let that slide.

### Tuning rule

Express any freebie in **units of gameplay**. A daily roll ≈ *"ten monsters"*; a session is
hundreds. That keeps it noise. If a daily ever becomes worth *waiting* for, it stopped being noise.

## Death

Dying respawns the player at the **nearest home they have saved at**. The penalty:

- **Half the gold carried is destroyed** — not dropped. There is nothing to run back for; it's gone.
- **Equipped gear drops where you fell** — go back and retrieve it.
- **Death writes a save.** The penalty is committed on the spot, so it can't be undone by quitting.
- **Drops stack.** Dying again before recovering the first drop leaves a second one; both wait.

The dropped stash is **player data**, not map state: *"my drops: [map X, position P, items…]"* — a
list, since drops stack. That matters because maps reset on re-entry — the corpse must survive the
rebuild, and modelling it as player data means it does, for free.

**Gold destroyed vs dropped, deliberately:** a recoverable pile makes death a delay; destroying the
gold makes it a cost. It also keeps the drop unambiguous — one thing to run back for, the gear.

## Saving

**Two triggers, both structural: arriving home, and dying.** Nothing else writes.

Consequences (intended):
- No mid-run autosave, no per-map state to persist, no save during a jump.
- Quitting mid-run loses the run's progress — leaving home is a commitment.
- Death can't be dodged by quitting; the penalty is already written.
- It removes a whole class of problems: partial world state, save-during-transition, and
  reconciling a map mid-change.

**Must be atomic:** applying the penalty (gold halved, gear moved to a drop) and writing the save
are one operation. A crash between them is the one case that could duplicate or delete items.

## Progression

Gates on jump points are the progression spine: level, currency, (later) items/quests.
Unlock state (maps, characters) is player data and must survive across maps and sessions —
written at home, with the rest of the save.

## Open questions (not decided yet)

- Does a visited map remember its state, or reset on re-entry? (Save-at-home points hard at
  **reset** — nothing outside home is persisted anyway.)
- Which save tier holds what: unlocks and character choice change rarely (cold), currency and
  level change per run (warm). See the tiered-save idea before committing.
- How does the swapper reference maps — direct prefab refs, or by id/`AssetReference`?
  (Direct refs in one list mean every map loads with whatever holds that list. Fine at this
  scale; the seam to keep is *referencing maps by id*, so swapping in on-demand loading later
  is a change in one place.)
- What does "at home" mean structurally — a flag on the map prefab, a map id, or a home map type?
- "Nearest" home — nearest by what? Map graph distance, a fixed order, or the last one saved at?
- Is a drop permanent until collected, or can it expire / be capped in count?
- How are **attack** and **skill** referenced from a character config? They're behaviour, not
  numbers — a prefab, a component on the character, or a small strategy object. Decide when the
  second character exists, not before; one character can't show the shape.
