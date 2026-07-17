# Decisions

Dated log. Each entry: what was decided, and **why** — including what it costs and when to revisit.
If a decision turns out wrong, add a new entry rather than editing history.

---

## 2026-07-16 — Pillar: exploration wins ties

Exploration is the core fun. Combat and gear are important, but subordinate — they exist to let the
player reach further.

**Why write it down:** every later argument ("add a dungeon that farms merges", "make death harsher",
"gate this behind grind") gets decided by one question instead of taste: *does it make exploring
better?* A pillar is only useful if it can **reject** things.

**What it rejects, concretely:** systems whose efficient path is farming a known map rather than
seeing a new one. If the merge ladder or a gate ever makes standing still the optimal play, the
system is wrong — not the pillar.

**How the loop enforces it structurally** (rather than by willpower): rewards scale with **distance
out**, and checkpoints unlock as you push, moving home outward. Farming a safe map is then
*inefficient by construction* — the optimal play is always to advance. The frontier is a ratchet;
each rung eventually becomes ordinary ground. The 2048-Grey ladder stops being a treadmill and
becomes a **distance-travelled meter** — same maths, opposite experience.

**The invariant to protect:** *a safe, known map must never out-farm the frontier.* That single
property is what keeps the pillar true. Check drop tables against it whenever they change.

---

## 2026-07-16 — Maps are prefabs, jumps are a cut

Open world, hand-built maps. **A map is a prefab inside one GameScene**, not a scene of its own.
Jumping destroys the current map prefab and instantiates the next, covered by a brief transition FX.

**Why:** it removes streaming, cross-map sim LOD, seamless transitions *and* scene-load/scope
juggling from the problem set. Maps are small and the game is 2D — performance isn't a constraint
at this scale. The world reads as large; the runtime holds one prefab.

**Consequence (the good kind):** `GameScope` outlives a jump, so character, camera, UI and services
persist for free. Only map contents are rebuilt. What needs care is the reverse: anything holding a
reference *into* the map (spawn points, jump points, targets) must rebind on swap.

**Cost:** no seamless traversal — jumps are a cut, not a walk. Accepted.

**Seam to keep:** reference maps **by id**, not as direct prefab fields. Direct refs mean every map
loads with whatever holds the list. Fine now; keeping the id indirection means on-demand loading
later is a one-place change.

---

## 2026-07-17 — Least-art-cost wins, all else equal

Solo project: art is the scarce resource, not code. When two approaches reach the same result,
take the one that needs fewer drawings — even if it costs more engineering.

**Why write it down:** the terrain work spent a day rediscovering it. Dual-grid was chosen for its
elegance (16 tiles instead of 47, layers instead of N² pairs) without checking whether the art
existed. It doesn't — there is essentially no ready-made dual-grid art — so the "cheap" option
required drawing everything from scratch.

**What it rejects:** any system whose tile count multiplies with terrain count, and any elegance
argument that doesn't price the art.

---

## 2026-07-17 — Shader-blended terrain, deferred

Each terrain is **one tiling texture**. The renderer produces per-cell weights; the shader blends
the textures by weight, breaking the boundary up with noise. **No transition art at all** — no
corners, no edges, no 9/13/47-tile sets, no pairs.

**Why it's the endgame:** it is the only approach whose art cost doesn't grow with terrain count.
One texture per terrain, forever. Adding stone or sand is one drawing, not nine, and it
automatically meets everything already there. It is also how Don't Starve gets its organic
coastlines — it *has* to, since its world is generated and nobody can hand-author transitions for a
random map.

**Why it was skipped first time round — fear it looks bad.** A fair fear, and precisely locatable:
lerping two textures by weight gives a **soft smear**, which is genuinely ugly and is what most
people picture. The technique that works is the opposite — a **hard threshold with noise pushed into
it**: `smoothstep(t - eps, t + eps, weight + noise(uv))` with a small `eps` gives a *crisp* edge
that noise bends into an organic line. The drawn look survives too: the band near the threshold can
be tinted, which is exactly the pack's brown shore, now a parameter.

**The part of that fear that stays true:** a generated boundary has no intent. A drawn shore has
rhythm and deliberate detail; noise is uniformly busy, and up close it reads as machine-made. Don't
Starve gets away with it because the entire game commits to that look.

**Why not now:** real shader work (Shader Graph, days not hours), and the tile path already renders.
This is an upgrade, not a rescue.

**When to do it:** when a third or fourth terrain is wanted, or when the tile boundaries look too
regular against real art. Both are signals the tile approach has hit its ceiling.

**Watch:** use a `Texture2DArray`, not an atlas. Blending atlas pages bleeds neighbouring pixels at
every mip level, and the seams are miserable to chase.

---

## 2026-07-17 — One rung per colour; sub-tiers dropped (12 rungs → 7)

*Supersedes the ladder shape in "2026-07-16 — Gear is `(definition, rung)`, not an asset per tier".
The model is untouched — `(definition, rung)`, merge as a rule, stats scaled from the rung. Only the
ladder got shorter.*

Grey → Green → Blue → Purple → Orange → Red → Diamond. **7 rungs, one per colour.** No I/II
sub-tiers, so every merge changes colour, and the old open question ("does merging past a colour's
last tier roll into the next colour?") stops existing.

**Cost — the merge ceiling collapsed:** `2¹¹ = 2048` Greys per Diamond → `2⁶ = 64`. 2048 was absurd
*by construction*, and that absurdity is what made "merge is the floor, exploration is the ceiling"
enforce itself. 64 is **farmable**. So the invariant (*a safe, known map must never out-farm the
frontier*) no longer has the ladder length protecting it — it now rests on **low-rung drop
abundance** alone.

**Revisit when** drop tables get written. If a safe early map pours out Greys, it can merge to the
top and the pillar breaks. Levers: tighten low-rung drop rates, or make merge cost more than the
two items.

---

## 2026-07-17 — Death drops nothing; gear is kept

*Supersedes the drop half of "2026-07-16 — Death: gold destroyed, gear dropped, drops stack", and
the drop half of "2026-07-17 — Death wipes all gold and the whole supply bag" below. Gold and
supplies are still wiped. Death still writes a save.*

No corpse, no stash, no run back. Gear stays equipped through death.

**Why:** the drop was the one part of the penalty that created **backtracking** — die, walk back,
pick up, resume. That is the single behaviour the pillar exists to reject, and we had built it into
the death rule. Charging gold and food instead makes death a **cost, paid instantly**, and points
the player outward again rather than back over old ground.

**Why gear specifically:** losing gear far out makes players turtle — it punishes the exact play the
game is built to reward, hardest at the frontier where the game wants them. And gear is the slowest
thing to re-earn, so losing it doesn't sting, it deletes hours. A death now costs **range and gold**,
never progress.

**What this removes for free:**
- The drop list in player data (buildings still need that pattern; they no longer share the reason).
- "Drops stack" — no drops, nothing to stack.
- The whole class of *unreachable drop* problems: gear at the bottom of a lake after a water-walk
  buff expires, gear behind a one-way jump, gear on a map you cannot re-enter yet. None of these
  can exist now.

**Cost, accepted:** death is softer, and the corpse-run tension is gone. If deaths stop mattering,
the lever is the gold and supply loss, not bringing the drop back.

---

## 2026-07-17 — Death wipes all gold and the whole supply bag

*Supersedes the gold amount in "2026-07-16 — Death: gold destroyed, gear dropped, drops stack".
Gear still drops at the death spot, drops still stack, death still writes a save — all unchanged.*

**All** carried gold is destroyed (was: half). **The whole supply bag** is destroyed with it.

**Why the supplies:** supplies buy **distance**. Wiping the bag costs the player *range* — the
penalty lands in the currency the pillar actually runs on, not just the wallet. The next trip is a
short one until food is re-earned, so the frontier pushes back in and you work your way out again.
Death now costs the thing the whole game is about.

**Bound — death must stay a cost, not a wall:** the table at home still refills **fullness** for
free, so a player can always leave; they just can't go **far**. If food can't be reached and
re-earned within one free fullness bar, this decision is broken. That free meal is load-bearing now.

---

## 2026-07-17 — Damage types deferred; one type for now

No physical/magic/true split yet.

**Why:** types only create decisions if enemies resist differently — and then the payoff is real
(the region dictates the loadout you commit to at home). But each type is a permanent tax on every
item, enemy, UI and balance pass, and with one character it decides nothing.

**Add one when** a specific enemy demands a different answer. Then the count is known instead of
guessed. Adding is cheap; removing isn't.

**Red flag noted:** *true damage* usually exists to escape a resistance system that became annoying.
Wanting it up front is a hint the resistance system isn't wanted yet.

---

## 2026-07-17 — Affixes at high rarity, fixed per item

Orange+ gear unlocks secondary effects (cooldown, skill damage, auto-shots, big stats for much
faster hunger, extra stat lines). **The effect belongs to the item** — an Orange sword always has
the Orange sword's effect. Nothing is rolled per drop.

**Why affixes:** they make the top of the ladder *qualitatively* different. A 12-rung ladder is only
worth climbing if late rungs change what the item **does**, not just how big its number is.

**Why fixed:** identical items stay genuinely identical, so `(definition, rung)` still describes an
item completely and merge stays a three-line rule. The surprise lives in *which* item drops and
*how high* it lands — not in a hidden roll stacked on top.

*(I briefly assumed affixes would roll per instance and went off designing around a collision with
"only identical items merge". They don't roll. There was no collision — noted as a reminder to check
the premise before solving.)*

---

## 2026-07-17 — Home is where you commit; the field is where you live with it

Character swap, gear change, stocking supplies, refilling hunger and saving **all happen only at
home**. Nothing irreversible happens in the field.

**Why:** four rules that were decided separately turn out to be one principle. It makes every choice
a **commitment with stakes** instead of an optimisation you can undo mid-run (e.g. swapping gear to
dodge the hunger cost). It also happens to be the cheap option: every complex state change occurs in
one place, at one time, while nothing is moving — no swap mid-combat, no migration mid-run, no save
mid-transition.

---

## 2026-07-17 — Hunger is an expedition budget, not a timer

Food drains in the field; starving kills. Stock at home, go out, come back when supplies run low.

**Why it earns its place** (I argued against it first and was wrong): it isn't a tax on lingering —
it's the **budget for a trip**, like fuel in Subnautica or Dredge. It gives a run an arc, and it adds
a second axis to the ratchet: the frontier extends with **strength**, **carry capacity**,
**production** — and **route knowledge**. That last one is the real prize: without a budget,
wandering is free, so knowing the way is worth nothing. Hunger is what makes knowledge a resource.

**The risk is the number, not the mechanic:** drain fast → a nagging bar that interrupts exploring;
drain slow → an invisible budget that only says *"this trip ends here"*. The player should rarely
look at the bar.

**Keep separate:** *fullness* (free at home) vs *supply bag* (must be earned). Merging them makes
food production pointless.

---

## 2026-07-17 — Gear costs hunger

Higher-rung gear drains hunger faster; it pays back through faster kills and more food.

**Why:** it prevents "max gear = infinite range" — the frontier stays a frontier at every power
level, so the ratchet can't trivialise itself. Combined with *gear only changes at home*, it becomes
a **loadout commitment** (scout run vs clear run) rather than a mid-run exploit.

**Watch:** if the net is negative, players strip gear to travel and upgrading feels like a
punishment. The net must stay positive.

---

## 2026-07-17 — Player-placed things are player data, not map state

Buildings (torch towers, production) and death drops both live as player data keyed by map:
`[(map id, position, …)]`.

**Why:** maps reset their contents on re-entry, so anything stored *in* the map is destroyed. The
death drop already needed this; buildings need exactly the same thing. One pattern covers both —
"my footprint in the world" persists, the map's own contents don't.

*(2026-07-17: death drops are gone, so buildings are the only case left. The decision stands on its
own — maps still reset, buildings still have to survive it — it just lost its second example.)*

---

## 2026-07-17 — Premium on both platforms; mobile is a demo

One game, sold once. Steam: buy up front. Mobile: free demo, buy to continue — reach, not revenue.
Rewarded ads only, sparingly (watch on death to keep your gold). A daily free roll on both.

**Why:** it removes F2P monetization from the design entirely, so nothing pulls on the tuning.
Gacha/merge goes back to being a plain loot system instead of a monetization spine — the pillar
stops having a rival. **The Steam build becomes the conscience of the mobile build:** it must be
good when nobody pays, and reviews enforce that.

**Cost, accepted knowingly:** paid mobile is a brutal market — low conversion, poor discovery.
Fine, because mobile isn't the revenue play.

**Rules that fall out:**
- Ads are **rewarded, never forced** — a choice offered at a bad moment, not a tax. Never tax a
  behaviour the game wants (an ad on *saving* would punish going home).
- Platform differences live in **difficulty**, not in **resource income**. Two economies = two
  balances to maintain, solo.
- Freebies are measured in **units of gameplay** (a daily roll ≈ "ten monsters"). The daily is
  **noise** by design. If it's ever worth *waiting* for, it stopped being noise.

---

## 2026-07-17 — Drops land at rungs; merge is the floor, exploration is the ceiling

Gear drops **directly at various rungs**, and the distribution **shifts up with distance out**.
Merging converts surplus upward.

**Why it matters:** it means `2048 Greys per Diamond` is a **ceiling** (grinding Greys alone), not
the expected path. Merge guarantees junk is never worthless (**floor**); distance buys rungs
directly (**ceiling**). Both systems point outward, so neither competes with the pillar.

**Balance numbers deferred** — the shape is the decision; the tuning comes when there's a game to
play.

---

## 2026-07-16 — Gear is `(definition, rung)`, not an asset per tier

12-rung colour ladder (Grey → Green I/II → … → Red I/II → Diamond); merge two of the same rung to
climb one. An item is modelled as a **definition id + a rung index**; merge is a rule, and stats
scale *from* the rung.

**Why:** the alternative — an asset per colour-tier per item — enumerates a combinatorial space
(defs × rungs). It grows multiplicatively with every new gear type, and turns a three-line merge
rule into a maintained lookup table. Composition covers the same space with one asset per
definition and a formula.

**Watch for:** the first request to "make Blue II of this sword special". That's the pressure to
enumerate. Handle it with an override on that definition, not by authoring the whole ladder.

---

## 2026-07-16 — Save on two triggers: home, and death

Homes (several, unlocked over time) are checkpoints. **Saving happens on arriving home and on
dying** — nothing else writes.

**Why home-only:** it deletes a class of problems rather than solving them — no partial world state,
no save during a jump, no per-map persistence, no reconciling a half-changed map. The save policy
*is* a design rule, not a technical feature.

**Why death also saves:** without it, dying and quitting reloads the pre-run save — gear back, gold
back, no penalty. Death would be optional and the corpse-run content dead on arrival. Saving on
death commits the penalty on the spot.

**Cost:** quitting mid-run loses the run's progress. Intended.

**Follow-on:** maps **reset** on re-entry — nothing outside home is persisted, so there is nothing
to restore. The death drop survives that because it's modelled as player data, not map state.

*(2026-07-17: no death drop any more — nothing outside home survives at all except buildings. Why
death saves is unchanged: it commits the gold and supply loss.)*

**Requirement:** applying the penalty and writing the save must be **atomic** — a crash between
"gear moved to drop" and "save written" is the only path to duplicated or lost items.

---

## 2026-07-16 — Death: gold destroyed, gear dropped, drops stack

Half the carried gold is **destroyed** (not dropped). Equipped gear **drops at the death spot** and
waits. A second death leaves a second drop — **they stack**.

**Why destroy the gold:** a recoverable pile turns death into a delay; destroying it makes death a
cost. It also leaves exactly one thing worth running back for, so the corpse-run reads clearly.

**Why drops stack:** overwriting would punish a second death by deleting the first loss retroactively
— a bigger, less legible penalty than the design asks for. Stacking keeps each death's cost its own.

**Modelling:** drops are a **list** in player data — `[(map id, position, items)]` — not map state.

---

## 2026-07-16 — Menu is an overlay on the live game

The start screen isn't a separate scene or state — the game scene runs behind it. START only
**hides** the menu. Character selection previews **live in the scene** and applies on START.

**Why:** no menu scene, no load between menu and play, no duplicate camera/lighting setup, and the
character preview is the real thing rather than a mock. Input is held by the gate (`"pre-start"`)
until START, so the world is visible but not drivable.

**Constraint it creates:** the scene must be presentable before the player "starts". Anything
gameplay-driven must not run while the menu is up — the gate covers input, but timers/spawners
would need their own hold if added later.

---

## 2026-07-16 — Characters: multiple, unlockable, home-only swap

Several characters, each a playstyle. Swapping is allowed **only at home**.

**Why:** restricting the swap to home means the swap never has to work mid-run — no state migration
between characters, no swap during combat, no interaction with map state. The restriction is what
makes the feature cheap.

---

## 2026-07-16 — Fail loud at wiring boundaries

Missing `UISystem._registry`, missing `IInputGate`, missing `CameraViewDir` all used to fail
**silently** (early `return`, `?.` no-op, stale statics).

**Why:** three separate bugs in one day, each costing far more to diagnose than the fix. A null at
a wiring boundary is always a mistake — never a valid state to tolerate quietly.

**Rule:** if a required reference is null, log an error that names the object and the fix.

---

## 2026-07-16 — Scope split: App vs GameScope

`App` = session lifetime; `GameScope` = scene lifetime, child of App.

**Why:** App is `DontDestroyOnLoad`. Registering scene objects there leaves dead references after a
scene load. Splitting by lifetime makes that structurally impossible.

**Gotcha found:** a parent registration cannot resolve a child-scope dependency. Keep a service and
its dependencies in the same scope, or put the dependency higher.

**Revisit when:** something needs to die at a boundary that isn't "the scene" (a battle, a popup
with its own services). Only then add a scope — a scope is a *lifetime* boundary, not a folder.

---

## 2026-07-16 — Input gate by kind, not a bool

`IInputGate.Block(InputKind, reason)` returning `IDisposable`; blocks stack.

**Why:** blockers nest in practice (story + popup + pause). A bool or `enabled = false` gets it
wrong the moment two things block and one releases. Kinds let a safe zone block only `Attack`
while movement and camera stay live.

**Cost:** every input site must ask the gate. Accepted — it's one call.

---

## 2026-07-16 — URP mobile: drop unused lighting features

Mobile RP asset: main-light shadows **off**, additional lights **Disabled**, mixed lighting **off**.
HDR stays **on** (the veil's glow needs > 1).

**Why:** lighting is the fake light-buffer; none of those features are used. This is deleting
unused work, not speculative optimization.

**Revisit when:** a real Light component or shadow map is ever needed.

---

## 2026-07-16 — Darkness veil samples by screen UV

The veil reads `_LightTex` with `ComputeScreenPos`, and `LightCamera` mirrors the main camera exactly.

**Why:** alignment previously depended on the veil quad fitting the frustum precisely (plus an FOV
expansion hack). Screen-space UV makes alignment structural — it can't drift with aspect or resize.

**Known issue:** glow (light above 1) is still view-dependent for flat, ground-lying emitters —
perspective changes how much they concentrate per pixel. Fix is per-emitter intent (HDR core,
billboarded), not a shader tweak.

---

## 2026-07-16 — Rejected: RendererFeature to replace the light camera

Tried a RenderGraph `ScriptableRendererFeature` (DrawRenderers into a buffer + blit) to remove the
second camera. **Reverted.**

**Why:** the second camera at 512px over a sparse layer was never measured as a cost. The feature
was a large lift for an unproven gain, and it broke. Kept the change that mattered (screen-UV) and
dropped the rest.

**Revisit when:** profiling on a real device shows the light camera actually costs frame time.
