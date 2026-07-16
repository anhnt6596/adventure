# Architecture

## Boot flow

```
LoadingScene  ──►  GameScene
  App (prefab, DontDestroyOnLoad)      GameScope (LifetimeScope, Parent = App)
   └─ UISystem                          Main Camera / Player / GameUI
```

1. **LoadingScene** is the only scene that must be in Build Settings first. It holds the
   `App` prefab: the root `LifetimeScope`, marked `DontDestroyOnLoad`.
2. `LoadingFlow` (entry point in App) runs on start and calls `ISceneService.LoadAsync("GameScene")`,
   reporting 0..1 progress. Future boot steps (save load, remote config, warmup) hook in there.
3. **GameScene** has `GameScope`, a child scope. It resolves everything App registers,
   plus its own scene-lifetime objects.

**Editor:** `Tools > Play From Loading` (on by default) makes Play always boot LoadingScene
regardless of the open scene, and returns to the scene you were editing on exit. Without a
live `App`, `GameScope` has no parent and nothing resolves.

## DI scopes

| Scope | Lifetime | Holds |
| --- | --- | --- |
| `App` | whole session | `ConfigRegistry`, `IEventBus`, `IUISystem`, `ISceneService`, `IInputGate`, save |
| `GameScope` | one scene | `MainCharStats` + its config, `GameController`, scene objects |

**The rule that bites:** a child scope sees its parent; a parent **never** sees its child.
So a registration's dependencies must live in the **same scope or an ancestor** — never a
descendant. (We hit this: `MainCharStats` in App couldn't see `MainCharStatsConfig` in GameScope.)

**How to decide where something goes:** ask *"when does this die?"* — dies with the scene →
`GameScope`; lives forever → `App`. And everything it depends on must die at the same time or later.

**Injecting scene objects:** `GameScope`'s `Auto Inject Game Objects` list (explicit, visible in
the Inspector — not a scene-wide scan). Runtime-spawned objects get injected at their spawn
point instead (`container.Instantiate(...)`), so the list stays small.

## Input gating

`IInputGate` blocks player input by **kind** (`Move` / `Attack` / `Camera` / `All`).

```csharp
_block = _gate.Block(InputKind.All, "pause");   // hold
_block.Dispose();                                // release
```

Blocks **stack**: two blockers + one release still blocks. `Describe()` tells you who is
blocking what. Input scripts check `_gate.Allows(kind)` before executing commands.

**Gate at the input layer, not in `Character`.** `Character` is simulation — it shouldn't know
what pause or a cutscene is, and gating it would also block AI/knockback/cutscene-driven movement.

**Two independent axes — don't conflate:**
- `Time.timeScale = 0` → freezes the world. Does **not** stop input (`Update` still runs).
- `IInputGate` → takes away control. Doesn't freeze anything.

Pause uses both. A story beat uses only the gate (the world keeps animating).

## UI

UI Toolkit. Two separate paths, on purpose:

- **`Core.UI` / `UISystem`** (in App): pooled views resolved by type — `Show<PausePopup>()`.
  Layers (HUD/Sheet/Popup/Overlay/…), popup stack, Escape handling, appear/disappear FX.
  **Convention:** the UXML file name must match the view class name (`PausePopup.uxml` ↔
  `class PausePopup`). `UIRegistryGenerator` matches them; `UISystem.Initialize` regenerates
  in the editor automatically. The registry asset must be assigned to `UISystem._registry`.
- **Scene `UIDocument`** (e.g. `GameUI` in GameScene): plain MonoBehaviour + UIDocument, not in
  the registry. For scene-owned UI. Give the UISystem document a **higher sort order** so popups
  render above scene UI.

`BasePopup` expects `#dimmer` and `#popup` elements in its UXML. UI Toolkit animations run on
unscaled time, so popup FX still play at `timeScale = 0`.

## Modules (`Framework/`)

- **Sprite3D** — `CameraViewDir` (single source of camera forward + a dirty flag),
  `Billboard` + `BillboardManager` (one LateUpdate for all billboards; O(1) when the camera is
  still), `ViewDir2` (swap sprite per view angle), `CharacterAnimator`, and the darkness veil
  (`LightManager`, `LightCamera`, `ShadowMaskFollow`, `DarknessMask`/`Fog` shaders).
  **Requires `CameraViewDir` on the camera** and layers `World` (6) / `Light` (7).
- **GridBuild** — `BuildGrid` (occupancy by id, mask + rotation, polygon-baked blocked cells),
  `IGrid`, `GridTypes`, `PolygonZone`. Placement *views* live in `_Project` (they depend on the
  light system, so they're policy, not mechanism).

## Conventions

- **Fail loud at wiring boundaries.** A missing registry/gate/component logs an error naming the
  fix — never a silent no-op. (Most of one day's debugging came from silent nulls.)
- **Explicit over scan.** Drag references; don't auto-discover. The exception is a list that is
  itself visible in the Inspector.
- **Model ⊥ view.** Simulation state is authoritative and lives in plain classes; views only
  present it. Sprites/animation never own gameplay state.
