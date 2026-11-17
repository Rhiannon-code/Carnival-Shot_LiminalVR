Shader "CrystalCatch/Bulb Halo"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.7, 0.35, 1)
        _Size ("Width, metres", Float) = 0.28
        _Falloff ("How soft the edge is", Range(0.5, 6)) = 2.2
        _Strength ("Brightness", Range(0, 2)) = 0.6
        _Glow ("Glow (set by script)", Range(0, 2)) = 1
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
            #include "UnityCG.cginc"
            #include "StringSway.cginc"

            fixed4 _Color;
            float _Size;
            float _Falloff;
            float _Strength;
            float _Glow;
            static const float Seed = 0.002;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0, fixed4 color : COLOR)
            {
                v2f o;
                float2 corner = uv - 0.5;
                float4 middle = float4(vertex.xyz - float3(corner * Seed, 0), 1);
                float3 view = UnityObjectToViewPos(StringSway(middle, color.r).xyz);
                view.xy += corner * _Size;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1));
                o.uv = uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = saturate(1 - length(i.uv - 0.5) * 2);
                return _Color * pow(d, _Falloff) * _Strength * _Glow;
            }
            ENDCG
        }
    }
}
