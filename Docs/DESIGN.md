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

## Day & night

Light shifts across a session: bright day, dark night. **No seasons** — too much work for the
value. Instead, individual places get their own **signature weather** later. To start: sun and dark,
nothing else.

This is not new tech — it's the existing veil finally doing its job. Day = light ambient;
night = the veil closes in and the player carries a **small vision radius**.

- Vision radius is **upgradeable through gear**, and **temporarily boosted** by pickups or by
  buffs off certain monsters.
- **Night serves the pillar for free:** it makes *known ground unfamiliar again*. The ratchet's
  payoff — *"this place used to kill me"* — comes back on the commute, without building a new map.

## Home

Home is a checkpoint, but not a menu. **It's a place, and it's explored too**: go inside, upstairs,
poke around for items, take short walks nearby.

At home the player is safe: **hunger doesn't drain**, and the table always serves a free meal —
an NPC fills you up. That warmth is deliberate: home should be somewhere you *want* to come back to.
The frontier is scarier when there's something to come back for.

### The rule that unifies everything

Every irreversible choice happens at home; the field is where you live with it.

| At home | In the field |
| --- | --- |
| swap **character** | — |
| change **gear** | — |
| stock **supplies** | — |
| **hunger refills free** | hunger drains |
| **saves** | no save (except on death) |

> **Home is where you commit. The field is where you live with it.**

Four separate decisions turned out to be one principle. It's also the cheap one technically:
every complex state change happens **in one place, at one time, while nothing is moving** — no
swapping mid-combat, no migrating mid-run, no saving mid-transition.

## Hunger — the expedition budget

Food drains over time in the field. Starve and you lose health continuously until you die.
Respawning refills hunger.

**Hunger is not a nagging timer — it's the budget for a trip.** The loop it creates:

> stock up at home → head out → probe, get lost, **learn the route** → supplies run low → head
> home → next time go further, with better gear *and* better knowledge.

**Why it earns its place** (it's the second axis of the ratchet): the frontier extends because you
are **stronger** (kill faster = less food per distance), because you can **carry more**, because
your **production** is better — and because **you know the way**. That last one is the gift:
hunger is what makes *route knowledge a resource*. Without a budget, wandering costs nothing, so
knowing the way is worth nothing.

**The tuning rule — it's the number, not the mechanic:**

| Drain fast | interrupts exploring to eat → a nagging bar ❌ |
| --- | --- |
| **Drain slow, generous budget** | barely noticed while out; it only says *"this trip ends here"* ✅ |

The player should **rarely look at the hunger bar** while exploring.

### Supplies vs fullness — keep them separate

Two different things, easy to accidentally merge in code:

| | What it is | Where it comes from |
| --- | --- | --- |
| **Fullness** (the bar) | your current state | **free at home** (the table) |
| **Supply bag** | food you *carry* | **must be earned** — farmed, found, produced |

The table refills the bar; it does **not** fill the bag. That's what keeps food production
meaningful and makes "stock up before a trip" a real act instead of a free top-up.

- The **supply bag is a gear item** with capacity — **upgrade it to extend your range**. One gear
  slot means *reach* rather than *power*.

### Gear costs hunger

**Higher-rung gear drains hunger faster.** It pays for itself later: you kill faster, so you find
more food.

**Why it's good:** it stops "max gear = infinite range". The frontier stays a frontier at every
power level — the ratchet can't trivialise itself.

**Because gear only changes at home, this is a commitment, not an exploit.** You pick a loadout and
live with it, which turns it into a real choice:

- **Scout run** — light gear, slow drain, long reach, avoid fights.
- **Clear run** — heavy gear, hungry, strong, short reach.

The optimum is a mix (enough gear to survive the frontier, light enough to reach it), so neither
extreme dominates. **Watch the net:** if good gear doesn't earn back more food than it costs,
players will strip down — and upgrading will feel like a punishment.

## Building

The player can build: **torch towers** (light — and light is survival at night), **production
units** (fruit trees and the like, feeding the supply economy).

**⚠️ Buildings vs "maps reset":** a map's contents rebuild when you re-enter, so anything built
there would vanish. Solved the same way as the death drop: **buildings are player data, not map
state**.

```
buildings: [(map id, position, type, state)]
drops:     [(map id, position, items)]
```

A map resets *its own* contents; what the player put into the world persists, because it never
belonged to the map. Same pattern, reused — both are "my footprint in the world", saved together.

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

## Parking lot — ideas, not commitments

Things that sound good but aren't decided. Nothing here gets built until the core loop is fun.

- **Home NPC as a relationship, not a vending machine.** She already gives home its warmth (the
  reason you *want* to return, which is what makes the frontier scary). The version worth building
  is *memory*: she asks how the trip went, reacts to a streak of deaths, cooks something new when
  you bring back a strange ingredient, and lets slip **hints about what's out there** — feeding the
  pillar rather than sitting beside it.
- **An heir you can play.** After some time, a new controllable character is born and joins the
  roster. Precedent exists (Rogue Legacy, Massive Chalice, Fire Emblem). **Big scope, and it drags
  in a different pillar** (legacy/generations vs exploration) — park it. If it ever happens, it
  should ride the existing character system (stats + attack + skill), i.e. an heir is just another
  character config, unlocked by a condition instead of a purchase.

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
