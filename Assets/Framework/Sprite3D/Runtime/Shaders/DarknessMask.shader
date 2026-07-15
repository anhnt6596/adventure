Shader "Unlit/DarknessMask"
{
    Properties {
        _LightTex ("Light Texture", 2D) = "white" {}
        _DarkColor ("Dark Color", Color) = (0.0, 0.0, 0.1, 1)
    }
    SubShader {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend DstColor Zero
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _LightTex;
            fixed4 _DarkColor;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 screenPos : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);   // sample by SCREEN position
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // screen-UV: alignment does not depend on the quad's mesh UVs / exact fit
                float2 uv = i.screenPos.xy / i.screenPos.w;
                // brightness from the light RT (saturate guards HDR RTs from over-brightening)
                float brightness = saturate(tex2D(_LightTex, uv).r);
                // multiply-veil: bright -> reveal scene, dark -> tint by _DarkColor
                return lerp(_DarkColor, fixed4(1,1,1,1), brightness);
            }
            ENDCG
        }
    }
}
