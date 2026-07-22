// Stylized water, mobile-cheap: sample a tileable caustics texture at world UVs, distorted by scrolling
// noise so the surface ripples and the reflection warps. Blue tint + a wavy shore foam band (shore
// distance is baked per-vertex, UV1.x, by the mesh builder). Unlit; noise frame can follow the camera.
Shader "World/StylizedWater"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Caustics (tileable, R)", 2D) = "black" {}

        _DeepColor ("Deep (base)", Color) = (0.10, 0.45, 0.72, 1)
        _ShallowColor ("Shallow (caustic)", Color) = (0.78, 0.93, 1, 1)
        _FoamColor ("Foam", Color) = (1, 1, 1, 1)

        _NoiseScale ("Caustics Tiling", Float) = 0.3
        _ScrollSpeed ("Scroll Speed", Float) = 0.08
        _DistortScale ("Distort Scale", Float) = 2
        _DistortAmount ("Distort Amount", Float) = 0.06

        _FoamWidth ("Foam Width", Float) = 1.5
        _FoamWobble ("Foam Wobble", Float) = 0.5
        _FoamNoiseScale ("Foam Noise Scale", Float) = 0.7

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

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv1 : TEXCOORD1; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 worldPos : TEXCOORD0; float shore : TEXCOORD1; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _DeepColor, _ShallowColor, _FoamColor;
            float _NoiseScale, _ScrollSpeed, _DistortScale, _DistortAmount;
            float _FoamWidth, _FoamWobble, _FoamNoiseScale, _CameraFollow;

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
                float2 uv = (IN.worldPos.xz - _WorldSpaceCameraPos.xz * _CameraFollow) * _NoiseScale;
                float t = _Time.y * _ScrollSpeed;

                // Warp the sample coords with scrolling noise, then scroll the caustics themselves.
                float2 dn = float2(Noise(uv * _DistortScale + t), Noise(uv * _DistortScale + 7.3 - t)) - 0.5;
                uv += dn * _DistortAmount + float2(t, t * 0.6);

                float caustic = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;
                float3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, caustic);

                // Foam: a wavy white band near the shore.
                float fn = Noise(IN.worldPos.xz * _FoamNoiseScale + _Time.y * 0.2);
                float shore = IN.shore + (fn - 0.5) * _FoamWobble;
                float foam = 1.0 - smoothstep(0.0, _FoamWidth, shore);
                col = lerp(col, _FoamColor.rgb, saturate(foam));

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
