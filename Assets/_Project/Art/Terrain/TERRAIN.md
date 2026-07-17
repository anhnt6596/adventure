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

## Nine tiles are enough — Quadrant mode

A tileset like the Kenney pack draws **9** tiles per terrain, the classic 3x3:

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

Quadrant draws each cell as **four tiles instead of one**: the map is split 2x2 (every sub-cell
inheriting its cell's terrain) and the same rule runs on the finer grid at half the tile size.
Example — `[[1 2] [1 3]]` becomes `[[1 1 2 2] [1 1 2 2] [1 1 3 3] [1 1 3 3]]`.

A sub-cell's two *inner* sides always match its own cell, so only the outer two can transition. Its
slot is therefore always one of four:

| Sub-cell | Slots it can need |
| --- | --- |
| top-left | 0, 1 (N), 8 (W), 9 (N+W) |
| top-right | 0, 1 (N), 2 (E), 3 (N+E) |
| bottom-right | 0, 4 (S), 2 (E), 6 (S+E) |
| bottom-left | 0, 4 (S), 8 (W), 12 (S+W) |

Together: **0, 1, 2, 3, 4, 6, 8, 9, 12** — exactly the nine a 3x3 tileset draws. Slots 5, 7, 10, 11,
13, 14, 15 are unreachable, so a one-cell-wide river renders from the west-edge and east-edge tiles
and nothing about the map needs constraining.

The tile-picking algorithm is identical to SameGrid. Only the input differs.

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
| 5, 7, 10, 11, 13, 14, 15 | *leave empty — Quadrant never reads them* | | |

The pack ships several interiors per terrain (`004`, `019`, `021`, … are all plain grass). Any of
them works in slot 0.

## Steps

1. **TerrainSet** (`Create > World > Terrain Set`) — layers in the order above: Dirt, Grass, Water.
   Untick `walkable` on Water. Give each a `previewColor`.
2. **TerrainGrid** on an empty GameObject, TerrainSet assigned, 64 x 64, cell size 1.
3. **Paint now** — `Start Painting`, `Gizmos: Terrain`. The whole map can be drawn before any art
   exists; the colours are enough. `Shift`-click erases to terrain 0, `Esc` stops.
4. **TerrainRenderer** — `Mode: Quadrant`, assign `Terrain.mat`, fill each layer's `tiles`.

The material is a template: each layer copies it and takes the texture from its own sprites, so
nothing needs a texture assigned by hand.
