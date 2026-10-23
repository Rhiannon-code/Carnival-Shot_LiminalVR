// The red glow spilling over a cover's lip where the signs rise: brightest at the bottom, fading upward and
// at both ends. Additive and worked out in the shader, so it needs no texture and no light; the baked red
// lights do the actual lighting, this only gives the edge its hot line
Shader "CrystalCatch/Glow Strip"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.15, 0.1, 1)
        _Strength ("Strength", Range(0, 4)) = 1.2
        _Falloff ("Falloff, higher hugs the bottom", Range(0.5, 8)) = 2.5
        _Edge ("End fade, share of the length", Range(0.01, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            half _Strength;
            half _Falloff;
            half _Edge;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half rise = pow(1 - i.uv.y, _Falloff);
                half ends = smoothstep(0, _Edge, i.uv.x) * smoothstep(0, _Edge, 1 - i.uv.x);
                return fixed4(_Color.rgb * (_Strength * rise * ends), 1);
            }
            ENDCG
        }
    }
}
