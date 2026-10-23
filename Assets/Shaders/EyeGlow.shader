// A soft round glow, brightest in the middle and gone at the edge of the quad. Additive, so it lights up the bone
// round an eye socket without a light; Quest 2 has no budget for bloom
Shader "CrystalCatch/Eye Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.2, 0.1, 1)
        _Strength ("Strength", Range(0, 4)) = 1.5
        _Falloff ("Falloff, higher is a tighter spot", Range(0.5, 8)) = 2
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
                half glow = pow(saturate(1 - 2 * distance(i.uv, half2(0.5, 0.5))), _Falloff);
                return fixed4(_Color.rgb * (_Strength * glow), 1);
            }
            ENDCG
        }
    }
}
