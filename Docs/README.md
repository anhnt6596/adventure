# Docs

Notes for the Adventure project. Kept short on purpose — a doc is worth writing when it
records something the code can't say by itself: a decision, a trade-off, a rule.

| Doc | What's in it |
| --- | --- |
| [DESIGN.md](DESIGN.md) | The game: world, maps, progression |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Boot flow, DI scopes, modules, conventions |
| [DECISIONS.md](DECISIONS.md) | Dated log of why things are the way they are |

Code-local docs live next to the code they describe:
- `Assets/_Project/Script/Config/CONFIG.md` — config/ScriptableObject system

## Project shape

```
Assets/
  Framework/          reusable, sealed behind asmdefs — no game deps
    Core/             event bus, save, scene service, DI injector
    Core.UI/          UI Toolkit system: registry, popups, sheets, FX
    Sprite3D/         billboards, view dirs, darkness veil + light buffer
    GridBuild/        grid, placement math, polygon zones
  _Project/           the game itself (Assembly-CSharp)
    Script/ UI/ Art/ Prefabs/ Scenes/ Settings/
```

**Rule of thumb:** `Framework/` = mechanism (any game could use it as-is).
`_Project/` = policy (this game's decisions). When unsure, start in `_Project/`
and promote later — demoting out of a framework is harder than promoting into one.
