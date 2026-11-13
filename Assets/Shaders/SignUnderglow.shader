// Laid over a sign as a second material: red at the stand's foot, fading out up the sign. SignUnderglow sets
// where the foot is and how far the fade runs, so it needs no texture and no light
Shader "CrystalCatch/Sign Underglow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.15, 0.1, 1)
        _Strength ("Strength with the room lit", Range(0, 4)) = 0.15
        _StrengthDark ("Strength in a blackout, when it's the sign's main light", Range(0, 4)) = 0.35
        _Falloff ("Falloff, higher hugs the foot", Range(0.5, 8)) = 2
        _HoleMask ("Bullet hole mask, set by step 41", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One
            ZWrite Off
            // Drawn over the sign's own surface: without this the two fight over depth as it turns, and it flickers
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float height : TEXCOORD0;
                float2 holeUv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            half _Strength;
            half _StrengthDark;
            half _RoomLight;
            half _Falloff;
            half _Amount;
            float _FootY;
            float _FadeHeight;
            sampler2D _HoleMask;
            float4 _HoleU;
            float4 _HoleV;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.holeUv = float2(dot(v.vertex.xyz, _HoleU.xyz) + _HoleU.w, dot(v.vertex.xyz, _HoleV.xyz) + _HoleV.w);
                o.height = (mul(unity_ObjectToWorld, v.vertex).y - _FootY) / max(_FadeHeight, 0.001);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Nothing to glow on where the sign has a hole in it
                clip(tex2D(_HoleMask, i.holeUv).r - 0.1);

                half glow = pow(saturate(1 - i.height), _Falloff);
                return fixed4(_Color.rgb * (lerp(_StrengthDark, _Strength, _RoomLight) * glow * _Amount), 1);
            }
            ENDCG
        }
    }
}
