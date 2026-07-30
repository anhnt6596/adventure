// The dark fog bank that rings a map's edge: it hides the emptiness past the border and, in the same stroke,
// says "the way out is through here". The camera keeps the player dead centre, so the border is never something
// they see from a distance — they walk into it, and without this the void outside the map just slides into frame.
//
// GEOMETRY IS A SCREEN QUAD; THE MASK IS WORLD-SPACE. The quad rides the camera (MaskFollowCamera, exactly like
// the day/night veils), but nothing about the darkness is screen-space: every pixel projects its own view ray
// onto the ground plane and the fog is evaluated at that WORLD position. A screen-space vignette is not a
// cheaper version of this, it is a different and wrong effect, twice over:
//   * the landmark is the map's border, which stands still in the world — and so does the gate the player has to
//     walk into. A band pinned to the edge of the screen slides with the camera and can never line up with either.
//   * the camera orbits (Q/E). A screen-edge band is right at one yaw and wrong at every other one.
// No depth buffer is read, and none is needed: the sprites in this project are all ZWrite Off so there IS no
// depth to read, and the ray/plane intersection is analytic. A pixel whose ray never descends to the ground is
// aimed out of the world by definition, and gets the fog at full strength.
//
// FAKE VOLUME, HONESTLY EARNED. The bank is three horizontal slabs stacked from the ground up, each sampled
// where THAT pixel's ray crosses THAT slab's height: p_h = p_0 + rd.xz * (h / rd.y) — exact, not an
// approximation, and about two instructions. So the slabs really are at different heights: they slide against
// each other as the camera moves or orbits, which is the parallax that sells depth, and each drifts on its own
// noise offset so the bank churns instead of sitting there. Compositing runs low slab first: for any pixel the
// higher slab is sampled CLOSER to the camera, so it is the one in front, and low->high is back->front.
//
// The upper slabs are wispier (_TopWisp) and tinted toward _FogTopColor, and that is what makes the bank read as
// a WALL standing in the black outside the map instead of as a hole cut out of the world. It also means the
// outside needs no blackout pass: the camera clears to black and nothing is drawn past a map's border, so the
// void is already there — out there the fog's job is to be a silhouette against it, not to cover it.
//
// KNOWN CONSEQUENCE of a depth-less veil: a tall sprite's upper pixels sample the ground BEHIND the sprite, so a
// character walking into the band is swallowed head-first rather than feet-first. There is no fix without a depth
// buffer to say where the surface actually is; _WallHeight decides how far the bank reaches up the screen and
// therefore how much this shows.
//
// Drawn UNDER the day/night veils (Queue Overlay-10) — after every sprite, before DarknessMask and Fog. The bank
// is part of the WORLD, not part of the image, so the ambient tint has to land on it exactly the way it lands on
// everything else. Over the veils it would be the one thing on screen that does not move with the time of day:
// at night the whole scene shifts toward ambient and the border alone would not, which reads as a hole punched in
// the picture rather than as fog standing in the world.
// The price, accepted: the glare on the Fog overlay (additive, screen-wide, currently black and unused — see the
// SunnyWeather item) will wash the band out somewhat when it comes back. That is a tint on an already dark colour,
// not a change of shape, so the border still reads as a border.
Shader "Unlit/BorderFog"
{
    Properties
    {
        _FogColor ("Fog (at ground)", Color) = (0.04, 0.05, 0.08, 1)
        _FogTopColor ("Fog Top (haze)", Color) = (0.16, 0.17, 0.22, 1)
        _Opacity ("Slab Opacity", Range(0, 1)) = 0.8

        [Header(Shape)]
        _BandCells ("Band Width (cells)", Float) = 2                 // inward from the border; cells, not units
        _WallHeight ("Bank Height (world units)", Float) = 1.5
        _TopWisp ("Top Slab Wispiness", Range(0, 1)) = 0.7

        [Header(Churn)]
        _NoiseScale ("Noise Scale", Float) = 0.35
        _WobbleCells ("Boundary Wobble (cells)", Float) = 0.6
        _ScrollSpeed ("Drift Speed (units per sec)", Float) = 0.25
        _ScrollDir ("Drift Direction (world XZ)", Vector) = (1, 0.35, 0, 0)
    }

    SubShader
    {
        // Overlay-10, not Transparent+n: the number that matters is its position relative to the two day/night
        // veils, so state it against Overlay and it stays right if the transparent range ever gets crowded.
        //
        // Sitting IN the sprite queue was tried and dropped — not because it looked wrong, because it changes
        // nothing: one sorting layer, every sprite at order 0, default transparency sort, and a quad riding the
        // camera at 4.9 units against a world ~50 away wins the distance sort every time. Same picture, with the
        // ordering earned by geometry instead of stated by a number. And it does not buy the thing that would
        // have justified it: a sprite standing in FRONT of the bank still cannot draw over it, because a quad on
        // the camera has no position in the world to be ranked by.
        Tags { "Queue"="Overlay-10" "RenderType"="Transparent" "IgnoreProjector"="True" }

        // A veil: never occludes anything by depth, and is never occluded. It sits closest to the camera of the
        // three overlay quads, so ZTest would pass anyway — stated so it keeps passing if the rig changes.
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One OneMinusSrcAlpha        // premultiplied: the slabs are composited before the blend sees them

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define SLABS 3

            fixed4 _FogColor, _FogTopColor;
            float _Opacity, _BandCells, _WallHeight, _TopWisp;
            float _NoiseScale, _WobbleCells, _ScrollSpeed;
            float4 _ScrollDir;

            // Pushed by MapBorderFog, which reads them off the loaded map's TerrainGrid.
            // _MapRect = (origin world X, origin world Z, size along the grid's X, size along its Z)
            // _MapAxis = (gridX.x, gridX.z, gridZ.x, gridZ.z) — unit world directions, so a rotated map works
            float4 _MapRect;
            float4 _MapAxis;
            float _GroundY;
            float _CellSize;

            float Hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm (float2 p) { return Noise(p) * 0.65 + Noise(p * 2.17 + 11.3) * 0.35; }

            // World distance to the nearest border: positive inside the map, negative outside. Chebyshev-style
            // (the min of the two axes) so corners darken from both sides at once instead of needing a case.
            float EdgeDist (float2 p)
            {
                float2 d = p - _MapRect.xy;
                float2 local = float2(dot(d, _MapAxis.xy), dot(d, _MapAxis.zw));
                float2 toEdge = min(local, _MapRect.zw - local);
                return min(toEdge.x, toEdge.y);
            }

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 ws : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.ws = mul(unity_ObjectToWorld, v.vertex).xyz;   // the quad is a plane, so this interpolates exactly
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 camPos = _WorldSpaceCameraPos;
                float3 rd = normalize(i.ws - camPos);

                // No ground point at all at or above the horizon: that pixel is looking out of the world.
                if (rd.y > -1e-3) return fixed4(_FogTopColor.rgb, 1);

                float cell = max(_CellSize, 1e-3);
                float band = max(_BandCells * cell, 1e-3);
                float wobble = _WobbleCells * cell;

                float2 dir = _ScrollDir.xy;
                dir = dot(dir, dir) > 1e-6 ? normalize(dir) : float2(1, 0);
                float2 drift = dir * (_ScrollSpeed * _Time.y);

                float3 col = 0;      // premultiplied by alpha as it accumulates
                float alpha = 0;

                for (int k = 0; k < SLABS; k++)
                {
                    float f = k / (float)(SLABS - 1);              // 0 at the ground, 1 at the top of the bank

                    // Where this pixel's ray crosses this slab. rd.y is negative, so a slab above the ground is
                    // sampled back along the ray — nearer the camera. That offset IS the parallax.
                    float h = _WallHeight * f;
                    float2 p = camPos.xz + rd.xz * ((_GroundY + h - camPos.y) / rd.y);

                    // Noise shoves the boundary instead of modulating opacity: a boundary that billows, rather
                    // than a ruled line with holes punched through it.
                    float n = Fbm((p + drift * (1.0 + f * 0.6)) * _NoiseScale + float2(k * 7.3, k * 3.1)) - 0.5;
                    float d = EdgeDist(p) + n * wobble * (0.4 + f);

                    float t = saturate(1.0 - d / band);
                    t *= t;                                        // faint where it starts, thick at the border
                    float a = _Opacity * t * lerp(1.0, _TopWisp, f);

                    float3 c = lerp(_FogColor.rgb, _FogTopColor.rgb, f);
                    col = c * a + col * (1.0 - a);
                    alpha = a + alpha * (1.0 - a);
                }

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
