Shader "Unlit/DarknessMask"
{
    Properties {
        _LightTex ("Light Texture", 2D) = "white" {}
        _DarkColor ("Dark Color", Color) = (0.0, 0.0, 0.1, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.95, 0.8, 1)
        _GlowStrength ("Glow Strength", Range(0, 4)) = 1
        _GlowThreshold ("Glow Threshold (normal=1)", Range(0, 1)) = 1.0
    }
    SubShader {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off

        // ---- Pass 1: DARKEN (multiply) — dark tint in shadow, reveal to normal in light ----
        Pass {
            Blend DstColor Zero
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _LightTex;
            fixed4 _DarkColor;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 screenPos : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float b = saturate(tex2D(_LightTex, uv).r);
                return lerp(_DarkColor, fixed4(1,1,1,1), b);
            }
            ENDCG
        }

        // ---- Pass 2: GLOW (additive) — NOTE: URP renders only Pass 1 of an overlay, so this pass is
        //      inactive under URP. Screen-wide glare lives on the Fog overlay (additive) instead. ----
        Pass {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _LightTex;
            fixed4 _GlowColor;
            float _GlowStrength, _GlowThreshold;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 screenPos : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float b = tex2D(_LightTex, uv).r;                                   // raw (HDR-ready)
                // glow = amount ABOVE the "normal" line (_GlowThreshold). LDR caps b at 1,
                // so keep threshold near 1 -> only the brightest cores glow. HDR buffer -> set it to 1.0
                // and let emitter intensity (>1) drive glow cleanly.
                float g = max(0.0, b - _GlowThreshold) * _GlowStrength;
                return fixed4(_GlowColor.rgb * g, 1);                               // adds light on top
            }
            ENDCG
        }
    }
}
