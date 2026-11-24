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
        _EdgeMetres ("Edge fade, metres", Float) = 0.5
        _EdgeWobble ("How far the edge wanders in and out, metres", Float) = 0.6
        _CarpetBoost ("Extra density where the mask's green says (the carpet)", Float) = 1.8
        _WispLow ("Wisps: noise below this is clear", Range(0, 1)) = 0.2
        _WispHigh ("Wisps: noise above this is full", Range(0, 2)) = 1
        _Stand ("Where the player stands, x and z: set by step 18", Vector) = (0, 0, 0, 0)
        _Hole0 ("Clear round a prop: x, z, radius, softness (step 18)", Vector) = (0, 0, 0, 0.2)
        _Hole1 ("Clear round a prop: x, z, radius, softness (step 18)", Vector) = (0, 0, 0, 0.2)
        _Hole2 ("Clear round a prop: x, z, radius, softness (step 18)", Vector) = (0, 0, 0, 0.2)
        _Hole3 ("Clear round a prop: x, z, radius, softness (step 18)", Vector) = (0, 0, 0, 0.2)
        _ClearLine ("Clear past a line: direction x, z, distance, softness (step 51)", Vector) = (0, 0, 0, 0.4)
        _FogMask ("Open floor, seen from above (red: white is fog, black is clear)", 2D) = "white" {}
        _FogMaskRect ("Mask covers: x min, z min, 1 / width, 1 / depth", Vector) = (0, 0, 0, 0)
        _NearFade ("Gone by this close, metres", Float) = 0.6
        _NearFadeRange ("Fully there this much further, metres", Float) = 2
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
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "FogHoles.cginc"

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
                float3 world : TEXCOORD3;
                float2 size : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _Noise;
            fixed4 _Color;
            float _Density;
            float _ScaleA;
            float _ScaleB;
            float4 _DriftA;
            float4 _DriftB;
            float _EdgeMetres;
            float _EdgeWobble;
            float _CarpetBoost;
            float _WispLow;
            float _WispHigh;
            float _NearFade;
            float _NearFadeRange;
            float4 _Stand;
            float4 _FogStir0;
            float4 _FogStir1;

            // x and y are where, z how far the ring has spread, w how strong it still is
            float Stir (float2 at, float4 stir)
            {
                return 1 - stir.w * (1 - smoothstep(stir.z * 0.5, stir.z, distance(at, stir.xy)));
            }

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
                o.world = world;
                // The quad's own width and depth, so its edges fade over metres however big step 51 lays it
                o.size = float2(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed fog = smoothstep(_WispLow, _WispHigh, tex2D(_Noise, i.a).r * tex2D(_Noise, i.b).r * 2);
                float edgeFade = max(_EdgeMetres, 0.01);
                // The edge wanders with the fog's own noise, so it never runs in a straight line
                float wander = (tex2D(_Noise, i.a * 0.37).r - 0.5) * _EdgeWobble;
                float2 edge = smoothstep(0, edgeFade, i.uv * i.size + wander) * smoothstep(0, edgeFade, (1 - i.uv) * i.size + wander);
                float near = saturate((distance(i.world.xz, _Stand.xy) - _NearFade) / max(_NearFadeRange, 0.01));
                float holes = FogHoles(i.world.xz);
                float stirred = Stir(i.world.xz, _FogStir0) * Stir(i.world.xz, _FogStir1);
                float thick = 1 + _CarpetBoost * Thick(i.world.xz);
                return fixed4(_Color.rgb, saturate(fog * _Density * thick) * edge.x * edge.y * near * stirred * holes);
            }
            ENDCG
        }
    }
}
