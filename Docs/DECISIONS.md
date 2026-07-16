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
