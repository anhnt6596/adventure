// GPU-instanced billboard grass. Each instance is one upright quad that yaws around Y to face the
// camera (cylindrical billboard - full spherical would flatten under the top-down camera). Wind bends
// the quad around its base; colour varies by world position. Unlit and alpha-clipped: the screen-space
// veil darkens it for free, so there is no lighting to wire.
Shader "Grass/Billboard"
{
    Properties
    {
        _MainTex ("Grass", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Tint ("Tint", Color) = (1,1,1,1)

        _ColorNoiseScale ("Colour Patch Scale", Float) = 0.05
        _ColorDark ("Patch Dark", Color) = (0.75,0.8,0.55,1)
        _ColorLight ("Patch Light", Color) = (1,1,0.85,1)
        _BladeVariation ("Per-Blade Variation", Range(0,1)) = 0.2

        [Header(Toon)]
        _ToonBlend ("Toon Blend", Range(0,1)) = 0                 // 0 = texture colour, 1 = flat toon
        _BaseColor ("Toon Base (root)", Color) = (0.35,0.55,0.2,1)
        _TipColor ("Toon Tip", Color) = (0.7,0.9,0.4,1)
        _ToonBands ("Toon Bands (0 = smooth)", Range(0,6)) = 3

        _WindScale ("Wind Scale", Float) = 0.08
        _WindSpeed ("Wind Speed", Float) = 0.4
        _WindBend ("Wind Bend (deg)", Float) = 22
        _BendShade ("Bend Shading", Range(-1,1)) = 0.2

        _PushStrength ("Interactor Push", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;   // quad: x in [-0.5,0.5], y in [0,1] (base at 0)
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 tint        : TEXCOORD1;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            float _Cutoff;
            float4 _Tint, _ColorDark, _ColorLight, _BaseColor, _TipColor;
            float _ColorNoiseScale, _WindScale, _WindSpeed, _WindBend, _PushStrength, _BladeVariation, _BendShade;
            float _ToonBlend, _ToonBands;

            // xyz = world position, w = radius. Set globally by GrassInteractorManager.
            float4 _GrassInteractors[16];
            float _GrassInteractorCount;

            // cheap value noise
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1,0)), u.x),
                            lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), u.x), u.y);
            }

            Varyings vert (Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;

                float3 rootWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
                float2 scale = float2(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21));

                // Wind: bend more toward the top (positionOS.y is the height fraction).
                float w = Noise(rootWS.xz * _WindScale + _Time.y * _WindSpeed) * 2.0 - 1.0;
                float ang = radians(w * _WindBend) * IN.positionOS.y;
                float s = sin(ang), c = cos(ang);
                float2 bent = float2(IN.positionOS.x * c - IN.positionOS.y * s,
                                     IN.positionOS.x * s + IN.positionOS.y * c);

                // Face the camera like the game's other billboards (Transform.forward = camForward):
                // the quad tilts back with the camera's pitch, base anchored, top leaning away.
                float3 camFwd = -UNITY_MATRIX_V._m20_m21_m22;
                float3 right  = normalize(cross(float3(0, 1, 0), camFwd));
                float3 up     = cross(camFwd, right);
                float3 posWS  = rootWS + right * (bent.x * scale.x) + up * (bent.y * scale.y);

                // Interactors (player, enemies) push the grass away, more toward the top.
                float3 push = 0;
                int count = (int)_GrassInteractorCount;
                for (int k = 0; k < count; k++)
                {
                    float2 d = rootWS.xz - _GrassInteractors[k].xz;
                    float r = _GrassInteractors[k].w;
                    float dist = length(d);
                    if (dist < r && dist > 1e-4)
                    {
                        float t = 1.0 - dist / r;
                        push.xz += (d / dist) * (t * t);
                    }
                }
                posWS += push * (_PushStrength * IN.positionOS.y);

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;

                float n = Noise(rootWS.xz * _ColorNoiseScale);              // large soft patches
                float blade = Hash(rootWS.xz * 41.3 + 7.0);                 // one value per tuft
                float3 tint = lerp(_ColorDark.rgb, _ColorLight.rgb, n) * _Tint.rgb;
                tint *= 1.0 + (blade - 0.5) * 2.0 * _BladeVariation;        // brighten/darken each tuft
                tint *= 1.0 + (w * IN.positionOS.y) * _BendShade;          // lean shifts shade -> wind shimmer
                OUT.tint = tint;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _Cutoff);

                // Flat toon: colour from a base->tip gradient (banded for cel steps), shape from the
                // texture alpha. Blended with the texture's own colour so it can be a hybrid.
                float h = IN.uv.y;
                if (_ToonBands >= 2) h = floor(h * _ToonBands) / (_ToonBands - 1);
                float3 toon = lerp(_BaseColor.rgb, _TipColor.rgb, saturate(h));
                float3 base = lerp(tex.rgb, toon, _ToonBlend);

                return half4(base * IN.tint, 1);
            }
            ENDHLSL
        }
    }
}
