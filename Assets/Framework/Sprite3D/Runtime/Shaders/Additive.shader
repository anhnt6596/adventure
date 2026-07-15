Shader "Healthy/Additive"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        [HDR]_Color ("Tint Color (extra)", Color) = (1,1,1,1)
        _InvFade ("Soft Particles Factor", Range(0.01, 3.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        LOD 100

        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Color (0,0,0,0) }
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Fog & soft particles
            #pragma multi_compile_fog
            #pragma multi_compile __ SOFTPARTICLES_ON

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _InvFade;

            #ifdef SOFTPARTICLES_ON
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 col    : COLOR0;
                UNITY_FOG_COORDS(1)
                #ifdef SOFTPARTICLES_ON
                float4 projPos : TEXCOORD2;
                #endif
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);

                // Vertex color * extra color
                o.col = v.color * _Color;

                // >>> ch?nh ?úng macro fog ? ?ây
                UNITY_TRANSFER_FOG(o, o.pos);

                #ifdef SOFTPARTICLES_ON
                o.projPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                #endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                #ifdef SOFTPARTICLES_ON
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                float partZ  = i.projPos.z;
                float diff   = saturate(_InvFade * (sceneZ - partZ));
                if (diff <= 0) discard;
                #endif

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = tex * i.col;

                #ifdef SOFTPARTICLES_ON
                col *= diff;
                #endif

                // >>> áp fog ?úng macro
                UNITY_APPLY_FOG(i.fogCoord, col);

                return col; // Blend One One ? pass
            }
            ENDCG
        }
    }

    Fallback "Particles/Additive"
}
