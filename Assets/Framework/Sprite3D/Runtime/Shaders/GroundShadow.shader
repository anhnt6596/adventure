// Casts a sprite's silhouette onto the ground as a flat, sun-sheared shadow. The shadow renderer's
// pivot sits at the caster's ground base; the vertex stage takes each vertex's height above that base
// and shears it along the global sun direction, then flattens it onto the ground plane. So a standing
// billboard turns into a stretched shadow that hugs the ground and swings with the time of day.
//
// Drive the globals from one place (see ShadowSun):
//   _SunGroundDir   : xy = (worldX, worldZ) ground shift PER UNIT of height (direction × length)
//   _ShadowStrength : overall shadow alpha (0 at night)
//   _ShadowGroundY  : world Y of the flat ground — heights are measured from here, so any sprite pivot
//                     works as long as the caster's base sits on the ground.
Shader "Sprite/GroundShadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Lift ("Ground Lift", Float) = 0.02   // tiny world-Y lift so it doesn't z-fight the ground
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

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Lift;

            // Globals, set once per frame by ShadowSun (not per-renderer, so the material still batches).
            float4 _SunGroundDir;
            float  _ShadowStrength;
            float  _ShadowGroundY;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                float3 wpos  = mul(unity_ObjectToWorld, IN.vertex).xyz;
                float  baseY = _ShadowGroundY;               // the flat ground plane (world Y)
                float  h     = max(0.0, wpos.y - baseY);     // this vertex's height above the ground
                wpos.xz += h * _SunGroundDir.xy;             // shear along the sun's ground direction
                wpos.y   = baseY + _Lift;                    // flatten onto the ground
                OUT.vertex   = UnityWorldToClipPos(wpos);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                // Black, soft-edged by the sprite's own alpha; overall darkness from the global strength.
                fixed a = tex2D(_MainTex, IN.texcoord).a * IN.color.a * _ShadowStrength;
                return fixed4(0.0, 0.0, 0.0, a);
            }
            ENDCG
        }
    }
}
