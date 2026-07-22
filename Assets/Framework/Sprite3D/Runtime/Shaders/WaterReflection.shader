// Fake water reflection for a billboard sprite: the same sprite, flipped to hang below the caster's
// feet, tinted to the water and rippling. It draws ONLY where the water shader marked the stencil
// (bit 1 / ref 2), so it clips itself to the water and vanishes cleanly at the shoreline instead of
// smearing across the land. ZTest Always + no depth write: it lies on the water like a decal.
//
// WaterReflection.cs positions and flips the quad; this shader only tints, ripples and masks it.
Shader "Sprite/WaterReflection"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Water Tint", Color) = (0.55, 0.8, 0.95, 1)
        _Alpha ("Max Alpha", Range(0,1)) = 0.45
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.05
        _FadeDepth ("Depth Fade", Range(0,1)) = 0.6      // fade toward the deep (far) end
        _WobbleAmount ("Wobble Amount", Float) = 0.03
        _WobbleScale ("Wobble Scale", Float) = 6
        _WobbleSpeed ("Wobble Speed", Float) = 1.5

        _StencilRef ("Stencil Ref", Int) = 2
        _StencilMask ("Stencil Mask", Int) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Stencil { Ref [_StencilRef] ReadMask [_StencilMask] Comp Equal }   // only on marked water

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 wpos : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Tint;
            float _Alpha, _Cutoff, _FadeDepth, _WobbleAmount, _WobbleScale, _WobbleSpeed;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.wpos   = mul(unity_ObjectToWorld, IN.vertex).xyz;
                OUT.uv     = IN.uv;
                OUT.color  = IN.color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                // uv.y ~1 at the waterline (feet), ~0 at the deep end after the vertical flip; the ripple
                // and the fade both grow with that distance so the near edge stays crisp and readable.
                float depth = saturate(1.0 - IN.uv.y);
                float wob = sin(_Time.y * _WobbleSpeed + (IN.wpos.x + IN.wpos.z) * _WobbleScale)
                            * _WobbleAmount * depth;

                fixed4 c = tex2D(_MainTex, float2(IN.uv.x + wob, IN.uv.y));
                clip(c.a - _Cutoff);

                c.rgb *= _Tint.rgb * IN.color.rgb;
                float fade = lerp(1.0, 1.0 - _FadeDepth, depth);
                c.a *= _Alpha * _Tint.a * IN.color.a * fade;
                return c;
            }
            ENDCG
        }
    }
}
