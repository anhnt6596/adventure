Shader "Unlit/Fog"
{
    Properties {
        _Color ("Add Color (glare)", Color) = (0, 0, 0, 1)
    }
    SubShader {
        Tags { "Queue"="Overlay" }
        Blend One One
        ZWrite Off Cull Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Additive (Blend One One): screen += rgb. Alpha is ignored — the COLOUR is the light
                // added: black = off, brighter = stronger (HDR can push way past 1 for hard glare).
                // This is the only veil pass URP renders besides DarknessMask Pass 1, so all screen-wide
                // glare/haze lives here.
                return fixed4(_Color.rgb, 1);
            }
            ENDCG
        }
    }
}