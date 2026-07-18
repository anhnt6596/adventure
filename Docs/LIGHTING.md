# Lighting & Colour Pipeline (Sprite3D veil)

How the screen's colour is composited, so day/night and weather can drive it correctly.
Source: `Assets/Framework/Sprite3D/Runtime/Shaders/*` + `LightManager.cs`.

## The pieces

The scene renders normally, then two **full-screen overlays** sit on top (Queue=Overlay):
the **veil** (`DarknessMask`) and the **fog** (`Fog`). Point lights are rendered separately by
`LightManager.lightCam` into a `_LightTex` (HDR RenderTexture) that the veil reads to "punch holes".

```
scene ──► DarknessMask (multiply + glow) ──► Fog (coverage haze) ──► screen
              ▲
          _LightTex  (lights drawn with SoftLight / Additive)
```

Split of duties: **DarknessMask** owns darken + tint (multiply); **Fog** owns added light (glare, haze,
colour). **URP renders only the FIRST pass of an overlay material** — so each quad = exactly one effect,
and screen-wide glare can only live on the single-pass Fog overlay (not DarknessMask's dead Pass 2).

`LightManager` fields → shader inputs each frame:
- `ambientColor` × `lightIntensity` → `DarknessMask._DarkColor`
- `fogColor` → `Fog._Color`

## DarknessMask.shader — the veil (darken + reveal)

**Pass 1 — DARKEN**, `Blend DstColor Zero` (multiply):
```
result = lerp(_DarkColor, white, b)   where b = saturate(_LightTex.r)
screen *= result
```
- In shadow (`b=0`): `screen × _DarkColor` → tinted & darkened.
- In light (`b=1`): `screen × white` → untouched.
- `_DarkColor = ambientColor × lightIntensity`.
  - `ambientColor` = the **tint** of shadow (white = neutral darken, red = warm dusk).
  - `lightIntensity` = **brightness**: `1` → `_DarkColor≈ambient` (bright day), `0` → black (dark night).

**Pass 2 — GLOW**, `Blend One One` (additive) — **inactive under URP:**
```
screen += _GlowColor.rgb × max(0, b − _GlowThreshold) × _GlowStrength
```
> ⚠️ URP renders only the first pass (SRPDefaultUnlit) of an overlay, so this second pass never runs —
> the per-light glow is dead here. Screen-wide glare lives on the **Fog overlay** (additive) instead.

## Fog.shader — full-screen ADDITIVE overlay, `Blend One One`

```
screen += fog.rgb          (alpha ignored)
```
- **`rgb`** = the light added. Black = **off**, brighter = stronger (HDR can push past 1 for hard glare).

This is the only veil pass URP renders besides DarknessMask Pass 1, so **all screen-wide light lives
here** — and "glare" is simply this fog set bright; there is **no separate glare lever**. Bright values
push the scene past ~**0.85** (chói). It can only ADD light — darkening/gloom is the DarknessMask multiply.

## Light shaders (drawn into `_LightTex`)

- **SoftLight.shader** — `tex × _Color`, `Blend SrcAlpha OneMinusSrcAlpha`. A soft radial texture
  tinted by `_Color`; this is a light's shape/reach. Its brightness becomes `b` in the veil.
- **Additive.shader** (`Healthy/Additive`) — `Blend One One` particle additive; glowy FX / emitters.

## The three day/night knobs (see `DayNightConfig`)

| Knob | Type | Drives | Meaning |
|------|------|--------|---------|
| `ambientColor` | Gradient | `LightManager.ambientColor` | shadow **tint** (white day, red dusk) |
| `intensity`    | Curve    | `LightManager.lightIntensity` | **brightness** (1 day, ~0 night) |
| `fogColor`     | Gradient | `LightManager.fogColor` (Fog overlay) | additive light = **glare** (rgb; black = off) |

## Recipes

- **Bright day** — `ambient=white`, `intensity=1`, `fog=off (a=0)`.
- **Dark night** — `intensity≈0` (veil → black), `ambient` irrelevant, `fog off`. Lights punch holes.
- **Red sunset** — `ambient=(1, 0.3, 0.15)` + `intensity≈0.5` (multiply → warm & dim).
- **Sun glare / "chói"** — `fog` warm & bright (e.g. `#9A9D53`) with `alpha` up → adds over the screen,
  pushing pixels past ~0.85. Off at night. Sunny weather raises it further.
- **Bright mist / haze (weather)** — `fog` = pale colour at low `alpha` → a soft additive wash.
- **Storm gloom (weather)** — lower `intensity`, cool `ambient` (multiply darkens; fog can only add).

## Weather layers on top

Day/night produces a base `EnvironmentState { ambientColor, fogColor, intensity }`. Weather transforms
it at the seam in `DayNightLighting.LateUpdate` **before** it reaches `LightManager` — e.g. dim
`intensity`, add glare/haze via `fog`, lerp `ambient` — so weather composes with the time of day
instead of replacing it. Fog can only add light; gloom/darkening is `intensity` + `ambient`.
