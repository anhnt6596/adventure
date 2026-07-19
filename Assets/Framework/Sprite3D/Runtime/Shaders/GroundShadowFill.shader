// The single "fill" pass for merged ground shadows. Draws the shadow colour once, ONLY where the stencil
// was marked by GroundShadowStencil — so any number of overlapping shadows become one flat, correctly-dark
// region (no stacking). Darkness comes from the global _ShadowStrength (set by ShadowSun), so it fades
// with the time of day like the per-object version. Put this on a ground-plane quad via ShadowComposite.
Shader "Sprite/GroundShadowFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _StencilRef ("Stencil Ref", Int) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Stencil { Ref [_StencilRef] Comp Equal }   // draw only where a shadow marked the stencil

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; };
            struct v2f { float4 vertex : SV_POSITION; };

            float _ShadowStrength;   // set globally by ShadowSun

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                return fixed4(0.0, 0.0, 0.0, _ShadowStrength);   // one flat, merged darkening
            }
            ENDCG
        }
    }
}
