// Stylized water, fully procedural (no textures) and mobile-cheap. Depth reads from the per-vertex
// shore distance the mesh builder bakes into UV1.x: shallow and light near land, deep and dark out in
// open water. A cheap ridged-noise "caustic web" sparkles on top, and a wavy foam band plus a crisp
// crest line ring the shore. Unlit; the day/night veil darkens it for free. The noise frame can follow
// the camera so an endless surface never shows a seam.
Shader "World/StylizedWater"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.06, 0.30, 0.55, 1)
        _ShallowColor ("Shallow", Color) = (0.30, 0.72, 0.85, 1)
        _FoamColor ("Foam", Color) = (0.95, 0.99, 1, 1)

        [Header(Depth)]
        _DepthRange ("Shallow to Deep (world units)", Float) = 6

        [Header(Caustics)]
        _CausticScale ("Caustic Scale", Float) = 0.25
        _CausticSpeed ("Caustic Speed", Float) = 0.05
        _CausticSharp ("Caustic Sharpness", Range(1, 16)) = 7
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.35

        [Header(Ripple distortion)]
        _DistortScale ("Distort Scale", Float) = 1.5
        _DistortAmount ("Distort Amount", Float) = 0.15

        [Header(Foam)]
        _FoamWidth ("Foam Band Width", Float) = 1.0
        _FoamWobble ("Foam Wobble", Float) = 0.6
        _FoamNoiseScale ("Foam Noise Scale", Float) = 1.4
        _CrestDist ("Crest Line Distance", Float) = 0.45
        _CrestWidth ("Crest Line Width", Float) = 0.2

        _CameraFollow ("Camera Follow", Range(0, 1)) = 0
    }

    SubShader
    {
        // Water is the lowest layer: render before everything and write no depth, so ground, grass and
        // sprites all draw over it instead of being culled by the flat water plane.
        Tags { "RenderType"="Opaque" "Queue"="Geometry-100" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            ZWrite Off

            // Mark the water surface in stencil bit 1 (ref 2) so the reflection can clip itself to the
            // water, even along the shoreline. WriteMask 2 leaves bit 0 alone — that bit belongs to the
            // ground-shadow merge (GroundShadowStencil), so the two systems never step on each other.
            Stencil { Ref 2 WriteMask 2 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv1 : TEXCOORD1; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 worldPos : TEXCOORD0; float shore : TEXCOORD1; };

            float4 _DeepColor, _ShallowColor, _FoamColor;
            float _DepthRange;
            float _CausticScale, _CausticSpeed, _CausticSharp, _CausticStrength;
            float _DistortScale, _DistortAmount;
            float _FoamWidth, _FoamWobble, _FoamNoiseScale, _CrestDist, _CrestWidth;
            float _CameraFollow;

            float Hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS);
                OUT.worldPos = ws;
                OUT.shore = IN.uv1.x;
                OUT.positionHCS = TransformWorldToHClip(ws);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.worldPos.xz - _WorldSpaceCameraPos.xz * _CameraFollow;
                float t = _Time.y;

                // Depth: shallow and light at the shore (shore ~ 0), deepening out into open water.
                float depth = saturate(IN.shore / max(_DepthRange, 1e-3));
                float3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);

                // Ripple: warp the caustic coords with slow scrolling noise so the web wobbles.
                float2 uv = p * _CausticScale;
                float2 warp = float2(Noise(uv * _DistortScale + t * 0.11),
                                     Noise(uv * _DistortScale + 5.2 - t * 0.13)) - 0.5;
                uv += warp * _DistortAmount;

                // Caustic web: two noise layers drifting apart; their ridge line makes thin bright veins.
                float a = Noise(uv + float2( t, t * 0.6) * _CausticSpeed);
                float b = Noise(uv * 1.7 + float2(-t * 0.7, t) * _CausticSpeed + 3.7);
                float web = pow(saturate(1.0 - abs(a - b) * 2.0), _CausticSharp);
                // Brighter in the shallows where a real bed would catch the light; faint in the deep.
                col += web * _CausticStrength * (1.0 - depth * 0.7);

                // Foam: a crisp wavy line lapping at the waterline plus a broken wash behind it. Two noise
                // octaves — one wobbles the shoreline, a finer one clumps the wash — so it never reads as a
                // flat white fade. The wash is squared to leave gaps of open water between the clumps.
                float ft    = _Time.y * 0.2;
                float wob   = Noise(IN.worldPos.xz * _FoamNoiseScale + ft);
                float clump = Noise(IN.worldPos.xz * _FoamNoiseScale * 2.7 - ft);
                float shore = IN.shore + (wob - 0.5) * _FoamWobble;

                float edge = 1.0 - smoothstep(0.0, _CrestWidth, abs(shore - _CrestDist));  // the lapping line
                float wash = 1.0 - smoothstep(0.0, _FoamWidth, shore);                     // broad wash
                wash *= clump * clump;                                                      // break it into clumps
                float foam = saturate(max(edge, wash));
                col = lerp(col, _FoamColor.rgb, foam * _FoamColor.a);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
