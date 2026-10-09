// A glowing edge around the gun and glove while a power-up runs. Drawn on a copy of each mesh pushed
// a few millimetres out along its normals, so it rims the silhouette without touching the artist's
// textures. Additive, no post-processing: Quest 2 has no budget for bloom.
Shader "CrystalCatch/Power-Up Glow"
{
    Properties
    {
        _Color ("Colour (set per gun by PowerUpTint)", Color) = (1, 0.82, 0.45, 1)
        _Inflate ("Push out (metres)", Float) = 0.004
        _Edge ("Edge start", Range(0, 1)) = 0.45
        _Softness ("Edge softness", Range(0.01, 0.5)) = 0.15
        _Strength ("Strength", Range(0, 4)) = 1.6
        _PulseSpeed ("Pulse speed", Float) = 5
        _PulseDepth ("Pulse depth", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Inflate;
            float _Edge;
            float _Softness;
            float _Strength;
            float _PulseSpeed;
            float _PulseDepth;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Pushed out in world metres, so a scaled-down model gets the same width of glow
                float3 normal = UnityObjectToWorldNormal(v.normal);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz + normal * _Inflate;

                o.vertex = UnityWorldToClipPos(world);
                o.normal = normal;
                o.view = UnityWorldSpaceViewDir(world);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float facing = 1 - saturate(dot(normalize(i.normal), normalize(i.view)));
                float rim = smoothstep(_Edge - _Softness, _Edge + _Softness, facing);
                float pulse = 1 - _PulseDepth * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                return _Color * (rim * _Strength * pulse);
            }
            ENDCG
        }
    }
}
