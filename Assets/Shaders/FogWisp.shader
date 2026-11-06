// A soft puff of ground fog (a particle). Worked out from its uv, so it needs no texture, and it fades out close to the
// camera, because a big see-through quad right against the eye is the costly case and looks like a flat card anyway.
// CarnivalLights sets _Color with the room's lights
Shader "CrystalCatch/Fog Wisp"
{
    Properties
    {
        _Color ("Colour", Color) = (0.45, 0.42, 0.5, 1)
        _NearFade ("Gone by this close, metres", Float) = 1
        _NearFadeRange ("Fully there this much further, metres", Float) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
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
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed alpha : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _NearFade;
            float _NearFadeRange;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                float away = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                o.alpha = v.color.a * saturate((away - _NearFade) / max(_NearFadeRange, 0.01));
                o.uv = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float soft = saturate(1 - dot(i.uv, i.uv));
                return fixed4(_Color.rgb, soft * soft * i.alpha);
            }
            ENDCG
        }
    }
}
