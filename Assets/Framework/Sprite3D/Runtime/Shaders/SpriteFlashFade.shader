// Sprite/Flash plus a HEIGHT-RAMPED fade, for things that have to get out of the player's way — see
// FadeWhenBlocking. Everything Sprite/Flash does, it does identically; leave _FadeAlpha at 1 and the two are
// pixel-for-pixel the same shader, so a sprite can be moved onto this one with no visual change and gain the
// fade only when something asks for it.
//
// The fade is not flat. Below _FadeY0 the sprite is untouched, between _FadeY0 and _FadeY1 it ramps to
// _FadeAlpha, and above that it holds. A tree therefore keeps a solid trunk and loses only its crown: the world
// still reads as having a tree standing there, instead of a ghost you look straight through.
//
// The ramp runs on OBJECT-SPACE Y, not on texcoord.y, and that is not a style choice: a sprite sliced out of a
// sheet carries a sub-rect of the atlas as its UVs, so texcoord.y is nowhere near 0..1 across the sprite and a
// gradient built on it would sit at the wrong height, differently for every sprite. Object space is the mesh
// Unity built from the sprite's own bounds, so the thresholds are stated in the sprite's own units, and the CPU
// side turns the authored percentages into them (FadeWhenBlocking.PushFadeRange). Billboarding rotates the
// TRANSFORM, never the mesh, so this axis stays the sprite's own "up" whatever the camera is doing.
//
// _FadeAlpha/_FadeY0/_FadeY1 ride the same MaterialPropertyBlock as _FlashAmount — which is why HitFlash zeroes
// its flash instead of dropping the block. Both effects apply at once; a tree can be mid-flash and mid-fade.
Shader "Sprite/Flash Fade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        [PerRendererData] _FadeAlpha ("Fade Alpha", Range(0,1)) = 1
        [PerRendererData] _FadeY0 ("Fade Start (object Y)", Float) = 0
        [PerRendererData] _FadeY1 ("Fade End (object Y)", Float) = 0
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
                float  objY     : TEXCOORD1;   // height up the sprite, in the mesh's own units
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _FlashColor;
            float _FlashAmount;
            float _FadeAlpha;
            float _FadeY0;
            float _FadeY1;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;   // SpriteRenderer.color rides the vertex colour
                OUT.objY = IN.vertex.y;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 0 at the base, 1 past the top of the ramp. A zero-width band becomes a hard cut at _FadeY0
                // rather than a divide by zero — which is also the sane reading of "start and end at the same
                // height", so an author who wants a clean line gets one instead of a NaN.
                float span = _FadeY1 - _FadeY0;
                float h = span > 1e-5 ? saturate((IN.objY - _FadeY0) / span) : step(_FadeY0, IN.objY);
                c.a *= lerp(1.0, _FadeAlpha, h);

                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);   // push toward the flash colour
                return c;
            }
            ENDCG
        }
    }
}
