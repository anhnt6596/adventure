# Terrain art (test assets)

Placeholder art for verifying the dual-grid system. **Not final** — both are here to prove the tech,
not to ship.

## Files

| File | Source | License | Tiles | Size | Stackable |
| --- | --- | --- | --- | --- | --- |
| `dual-grid-grass.png` | derived from `dual-grid-demo.png` | MIT | 16 (4x4) | 16px | **yes** |
| `dual-grid-demo.png` | [jess-hammer/dual-grid-tilemap-system-unity](https://github.com/jess-hammer/dual-grid-tilemap-system-unity) | MIT | 16 (4x4) | 16px | no (opaque) |
| `dual-grid-shapes.png` | [Clean 2-Corner Wang Tileset](https://opengameart.org/content/clean-2-corner-wang-tileset) by Joe Strout | CC-BY 3.0 | 16 x 3 variants | 64px | yes |

Use **`dual-grid-grass.png`** as the grass layer and the full-dirt tile of `dual-grid-demo.png`
(atlas 12) as the base layer.

**All are placeholders.** CC-BY needs attribution, MIT needs the licence text — irrelevant while
prototyping, not at release.

## The art spec that matters

A layer's tiles must be **opaque where the terrain is and alpha 0 where it isn't**, so lower layers
show through. `dual-grid-demo.png` fails this — its dirt is painted in, so it works for two terrains
by luck and would smear dirt over water the moment a third layer exists. `dual-grid-grass.png` is
the same art with the two dirt colours knocked to alpha 0.

This is why ready-made dual-grid art is scarce: most tilesets paint *both* terrains, for a
single-layer two-terrain system. When commissioning art, the spec is:

> 16 tiles per terrain, corner mask (bit0 SW, bit1 SE, bit2 NW, bit3 NE). Opaque where the terrain
> is, transparent where it is not. Ragged, organic edges — the silhouette is what sells it; no
> shader blending involved.

Drawing to that spec is *easier* than the alternative (the artist never thinks about neighbours),
which is the payoff of the layered approach: a new terrain is one more set of 16, never N^2 pairs.

## Import settings

Slice as **Grid By Cell Size**, cell 16x16 (`dual-grid-demo`) or 64x64 (`dual-grid-shapes`).
Set **Filter Mode: Point**, **Compression: None** — pixel art blurs and smears otherwise.

## Slot mapping — `dual-grid-demo.png`

Our mask (see `DualGrid.cs`) is `bit0 SW, bit1 SE, bit2 NW, bit3 NE`. The source tileset uses a
different bit order, so the atlas index is **not** the mask. Fill `TerrainLayer.tiles[mask]` with
the sprite at the listed atlas index (Unity slices left-to-right, top-to-bottom, so index 0 is the
top-left tile):

| mask | atlas | mask | atlas |
| --- | --- | --- | --- |
| 0 | 12 | 8 | 8 |
| 1 | 0 | 9 | 14 |
| 2 | 13 | 10 | 1 |
| 3 | 3 | 11 | 5 |
| 4 | 15 | 12 | 9 |
| 5 | 11 | 13 | 7 |
| 6 | 4 | 14 | 10 |
| 7 | 2 | 15 | 6 |

Sanity checks: mask 0 (nothing in layer) is fully dirt; mask 15 (all four corners in layer) is fully
grass. If those two are right and the diagonals look wrong, only mask 6 and 9 are left to swap.

Derived from the rule table in the source repo's `DualGridTilemap.cs`; **not verified in-engine yet.**
