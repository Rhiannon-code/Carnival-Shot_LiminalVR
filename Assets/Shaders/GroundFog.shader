// Low fog rolling along the range floor. Two lookups of one tileable noise, at different sizes and
// drifting different ways, multiplied together, so patches form and break up instead of sliding past
// as one sheet. Patterned in world space so any size of plane tiles the same. Alpha blended, unlit, no
// depth texture: Quest 2 renders none, which is why there is no soft fade where it meets walls
Shader "CrystalCatch/Ground Fog"
{
    Properties
    {
        _Color ("Colour", Color) = (0.8, 0.74, 0.9, 1)
        _Noise ("Noise (tileable, red channel)", 2D) = "white" {}
        _Density ("Density", Range(0, 1)) = 0.45
        _ScaleA ("Pattern A, tiles per metre", Float) = 0.3
        _ScaleB ("Pattern B, tiles per metre", Float) = 0.55
        _DriftA ("Pattern A drift, metres per second (x, z)", Vector) = (0.06, 0.025, 0, 0)
        _DriftB ("Pattern B drift, metres per second (x, z)", Vector) = (-0.035, 0.045, 0, 0)
        _Edge ("Edge fade, share of the plane", Range(0.01, 0.5)) = 0.22
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 a : TEXCOORD0;
                float2 b : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _Noise;
            fixed4 _Color;
            float _Density;
            float _ScaleA;
            float _ScaleB;
            float4 _DriftA;
            float4 _DriftB;
            float _Edge;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.a = (world.xz - _DriftA.xy * _Time.y) * _ScaleA;
                o.b = (world.xz - _DriftB.xy * _Time.y) * _ScaleB;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed fog = tex2D(_Noise, i.a).r * tex2D(_Noise, i.b).r * 2;
                float2 edge = smoothstep(0, _Edge, i.uv) * smoothstep(0, _Edge, 1 - i.uv);
                return fixed4(_Color.rgb, saturate(fog) * _Density * edge.x * edge.y);
            }
            ENDCG
        }
    }
}
