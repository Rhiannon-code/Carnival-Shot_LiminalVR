Shader "CrystalCatch/Fog Wisp"
{
    Properties
    {
        _Color ("Colour", Color) = (0.45, 0.42, 0.5, 1)
        _NearFade ("Gone by this close, metres", Float) = 1
        _NearFadeRange ("Fully there this much further, metres", Float) = 1.5
        _EndZ ("Gone by this z, the fog's far end (step 51)", Float) = 100
        _FloorY ("Gone at this height, the floor (step 51)", Float) = -100
        _ClearSoftness ("Fade into the floor and far end over, metres", Float) = 0.2
        _Hole0 ("Clear round a prop: x, z, radius, softness (step 51)", Vector) = (0, 0, 0, 0.2)
        _Hole1 ("Clear round a prop: x, z, radius, softness (step 51)", Vector) = (0, 0, 0, 0.2)
        _Hole2 ("Clear round a prop: x, z, radius, softness (step 51)", Vector) = (0, 0, 0, 0.2)
        _Hole3 ("Clear round a prop: x, z, radius, softness (step 51)", Vector) = (0, 0, 0, 0.2)
        _ClearLine ("Clear past a line: direction x, z, distance, softness (step 51)", Vector) = (0, 0, 0, 0.4)
        _FogMask ("Open floor, seen from above (red: white is fog, black is clear)", 2D) = "white" {}
        _FogMaskRect ("Mask covers: x min, z min, 1 / width, 1 / depth", Vector) = (0, 0, 0, 0)
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
            #include "FogHoles.cginc"

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
                float3 world : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _NearFade;
            float _NearFadeRange;
            float _EndZ;
            float _FloorY;
            float _ClearSoftness;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float away = distance(o.world, _WorldSpaceCameraPos);
                o.alpha = v.color.a * saturate((away - _NearFade) / max(_NearFadeRange, 0.01));
                o.uv = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float soft = saturate(1 - dot(i.uv, i.uv));
                float softness = max(_ClearSoftness, 0.01);
                float inside = saturate((_EndZ - i.world.z) / softness) * saturate((i.world.y - _FloorY) / softness);
                return fixed4(_Color.rgb, soft * soft * i.alpha * inside * FogHoles(i.world.xz));
            }
            ENDCG
        }
    }
}
