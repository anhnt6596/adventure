# Terrain

## What the map actually stores

One number per cell. Nothing else. `0` = water, `1` = grass, `2` = dirt — whatever the TerrainSet's
layer order says. No pictures live in the map.

## Why a cell needs more than one tile

A water cell in the middle of a lake and a water cell at the shore are not the same picture. The
shore one needs grass along its edge.

So before drawing a cell, the renderer asks: **which of my four neighbours are a different terrain?**
Each of those sides needs a transition drawn.

## The rule

Each direction carries a number:

```
          North = 1

  West = 8    [cell]    East = 2

          South = 4
```

**Add up the sides that transition.** That total is the slot in `tiles[]`.

| Situation | Sum | Slot |
| --- | --- | --- |
| all four neighbours the same — deep inside | nothing | **0** |
| north differs | 1 | **1** |
| east differs | 2 | **2** |
| north + east differ — top-right corner | 1+2 | **3** |
| south differs | 4 | **4** |
| east + south — bottom-right corner | 2+4 | **6** |
| west differs | 8 | **8** |
| north + west — top-left corner | 1+8 | **9** |
| south + west — bottom-left corner | 4+8 | **12** |

1/2/4/8 are used because every combination then sums to a different total — 0 to 15, no collisions.
That is the only reason there are 16 slots.

## Only 9 of the 16 exist

Measured from the pack's own pixels: it fills **9 slots** — the classic 3x3 arrangement.

```
┌─────────┬─────────┬─────────┐
│ 9       │ 1       │ 3       │
│ top-lft │  top    │ top-rgt │
├─────────┼─────────┼─────────┤
│ 8       │ 0       │ 2       │
│  left   │ INTERIOR│  right  │
├─────────┼─────────┼─────────┤
│ 12      │ 4       │ 6       │
│ bot-lft │ bottom  │ bot-rgt │
└─────────┴─────────┴─────────┘
```

The **7 missing** slots are 5, 7, 10, 11, 13, 14, 15:

| Slot | What it would be |
| --- | --- |
| 5 | north **and** south transition — a strip one cell tall |
| 10 | east **and** west — a strip one cell wide |
| 7, 11, 13, 14 | three sides open — a one-cell spur |
| 15 | all four — a lone cell |

Every one of them is *"this terrain is one cell thin here"*. A 3x3 tileset has no art for that.

## The painting rule this forces

> **Every terrain region must be at least two cells thick.**

Formally: a cell's mask must never have two *opposite* sides open, and never more than two open. The
nine legal masks are exactly the nine the tileset draws.

Water squeezed between two strips of land is slot 10 — no tile. Widen it to two cells and it becomes
two corner cells, both of which exist.

## Layer order follows the art, not preference

The pack draws **grass with dirt around it** and **water with grass around it**. Those painted
surroundings must *be* the layer underneath, or the fringe shows the wrong terrain:

```
Dirt   (0)  base, fills the map
Grass  (1)  drawn over dirt   — its tiles paint dirt at the edges  ✓
Water  (2)  drawn over grass  — its tiles paint grass at the edges ✓
```

This is also why dirt never touches water: grass is always drawn underneath water, so a lake gets a
grass shore even in the middle of a dirt field. The chain removes the pair the pack never drew.

**These tiles are opaque and that is fine** — what they paint around themselves is exactly the layer
below them.

## Which pack tile goes in which slot

Measured, not guessed. Layer 0 only ever needs slot 0 (it fills everything).

| Slot | Grass | Dirt | Water |
| --- | --- | --- | --- |
| 0 — interior | `rpgTile019` | `rpgTile008` | `rpgTile029` |
| 1 — top | `rpgTile001` | `rpgTile006` | `rpgTile011` |
| 2 — right | `rpgTile020` | `rpgTile025` | `rpgTile030` |
| 3 — top-right | `rpgTile002` | `rpgTile007` | `rpgTile012` |
| 4 — bottom | `rpgTile037` | `rpgTile042` | `rpgTile045` |
| 6 — bottom-right | `rpgTile038` | `rpgTile043` | `rpgTile046` |
| 8 — left | `rpgTile018` | `rpgTile023` | `rpgTile028` |
| 9 — top-left | `rpgTile000` | `rpgTile005` | `rpgTile010` |
| 12 — bottom-left | `rpgTile036` | `rpgTile041` | `rpgTile044` |
| 5, 7, 10, 11, 13, 14, 15 | *no art — leave empty or repeat slot 0* | | |

The pack ships several interiors per terrain (`004`, `019`, `021`, … are all plain grass). Any of
them works in slot 0.

`Tools > Terrain > Tile Mapper` does this by measuring the sprites, if you would rather not drag 27
of them.

## Steps

1. **TerrainSet** (`Create > World > Terrain Set`) — layers in the order above: Dirt, Grass, Water.
   Untick `walkable` on Water. Give each a `previewColor`.
2. **TerrainGrid** on an empty GameObject, TerrainSet assigned, 64 x 64, cell size 1.
3. **Paint now** — `Start Painting`, `Gizmos: Terrain`. The whole map can be drawn before any art
   exists; the colours are enough. `Shift`-click erases to terrain 0, `Esc` stops.
4. **TerrainRenderer** — `Mode: SameGrid`, assign `Terrain.mat`, fill each layer's `tiles`.

The material is a template: each layer copies it and takes the texture from its own sprites, so
nothing needs a texture assigned by hand.
