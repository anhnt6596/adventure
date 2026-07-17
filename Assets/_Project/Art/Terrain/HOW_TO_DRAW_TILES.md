# How to draw a terrain layer

You draw **4 small pieces**. The generator makes the 16 tiles.

`Tools > Terrain > Dual Grid Tileset Generator`

## Why four pieces

Every dual-grid tile is four quadrants, and a quadrant only ever looks four ways. Draw each once in
the **SW orientation** (as it appears in a tile's bottom-left corner); the generator rotates and
mirrors them into all 16 combinations.

Piece size = **half a tile**. For 64px tiles (matching the Kenney pack) draw at **32x32**.

| Piece | When it appears | What to draw |
| --- | --- | --- |
| **Solid** | quadrant is interior | Fill, edge to edge. No rim anywhere — nothing is exposed. |
| **Edge** | one side is open | Fill, with the rim along the **top** edge only. |
| **Outer Corner** | the quadrant is a lone nub | Fill in the **bottom-left** triangle, rim along the diagonal, transparent above it. |
| **Inner Corner** | fill wraps around an empty corner | Mostly fill, with the **bottom-left** corner bitten out; rim along that concave diagonal. |

`Outer Corner` and `Inner Corner` are near-complements: one is the nub, the other is the hole.

## The two rules that make it tile

1. **Fill must reach the piece's edge wherever there is no rim.** A gap at the boundary shows as a
   hard seam where two quadrants meet. The rim sits *on* the edge; it is not inset.
2. **Rim thickness must be identical in all four pieces.** The rim continues across quadrant
   boundaries — different thicknesses make a visible step at the joins.

Transparency is the rest of the rule: **opaque where the terrain is, alpha 0 where it isn't**, so
lower layers show through. Never paint the neighbouring terrain into the piece — that is what makes
a set unstackable (see `TERRAIN_ART.md`).

## Rotation is not free

The generator rotates and mirrors your pieces, so the art must survive it: **no directional
lighting, no baked shadow, no text or motif with an up**. Flat colour with a rim (the Kenney style)
rotates perfectly. A grass tuft lit from the top-left does not — it will appear lit from four
different directions across one map.

If you want directional light, don't use the generator: draw all 16 tiles by hand and fill
`TerrainLayer.tiles` directly.

## Drawing it in Kenney's style

The imported pack (`Art/Kenney/RPGBase`) is flat colour plus a rim and light noise. Measured:

| | Colour |
| --- | --- |
| grass fill | `RGB(141, 196, 53)` |
| dirt rim | `RGB(176, 128, 82)` — also `(197,143,92)`, `(189,137,88)` |

Use flat fill, a ~4px rim, and a sparse scatter of slightly-darker fill for texture. Nothing here
needs a pixel artist — it's shapes and two colours.

Kenney's own terrain tiles are **not** usable as-is: same-grid, both terrains painted opaque, and
pairwise per terrain combination. Their value is the palette and the style.

## Steps

1. Draw the four pieces at 32x32 (Aseprite, Krita, Photoshop — anything with alpha).
2. Import them. Tick **Read/Write** in the importer, or the generator can't read the pixels.
3. `Tools > Terrain > Dual Grid Tileset Generator` — assign the four, set the output path, Generate.
4. The output is sliced automatically, **16 sprites, and slot index == mask** — sprite `_5` goes in
   `tiles[5]`. No lookup table.
5. Assign the 16 into the layer's `tiles` array on the `TerrainSet`.
6. Paint with `TerrainGrid` (`Start Painting` in its Inspector) and look at it.

## Checking it worked

Two tiles prove the mapping: **slot 0 must be fully transparent**, **slot 15 fully opaque**. If
those are right and something still looks wrong, the fault is in the art, not the wiring.

Then paint a blob and look at the joins. Any hard straight seam means rule 1 was broken; a step in
the rim means rule 2 was.

## Status

The generator's tile maths is verified — all 16 masks produce the correct fill, and the rim is
continuous with no gaps. **It has not been run inside Unity yet.**
