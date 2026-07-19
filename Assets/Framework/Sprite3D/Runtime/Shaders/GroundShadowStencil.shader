// Merge-mode ground shadow: same ground projection as GroundShadow, but writes ONLY the stencil buffer
// (no colour). Every shadow marks the pixels it covers with _StencilRef; overlaps all write the same
// value, so the union is marked exactly once. GroundShadowFill then darkens those pixels a single time —
// so overlapping shadows never stack into a darker blob. Pair with a ShadowComposite in the scene.
Shader "Sprite/GroundShadowStencil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Lift ("Ground Lift", Float) = 0.02
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
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
        ColorMask 0                                       // stencil only, no colour
        Stencil { Ref [_StencilRef] Comp Always Pass Replace }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            float _Lift, _Cutoff, _ShadowGroundY;
            float4 _SunGroundDir;   // set globally by ShadowSun

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                float3 wpos  = mul(unity_ObjectToWorld, IN.vertex).xyz;
                float  baseY = _ShadowGroundY;
                float  h     = max(0.0, wpos.y - baseY);
                wpos.xz += h * _SunGroundDir.xy;
                wpos.y   = baseY + _Lift;
                OUT.vertex   = UnityWorldToClipPos(wpos);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                clip(tex2D(_MainTex, IN.texcoord).a - _Cutoff);   // mark only the solid part of the silhouette
                return 0;                                          // ColorMask 0 → only the stencil is written
            }
            ENDCG
        }
    }
}
