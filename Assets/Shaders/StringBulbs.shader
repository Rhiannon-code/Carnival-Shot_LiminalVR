Shader "CrystalCatch/String Bulbs"
{
    Properties
    {
        _Color ("Colour at the edge of the glass", Color) = (1, 0.62, 0.25, 1)
        _CoreColour ("Colour at the hot middle", Color) = (1, 0.95, 0.8, 1)
        _CoreSharpness ("How tight the hot middle is", Range(0.5, 8)) = 2.5
        _Glow ("Glow (set by script)", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "StringSway.cginc"

            fixed4 _Color;
            fixed4 _CoreColour;
            float _CoreSharpness;
            float _Glow;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float facing : TEXCOORD0;
            };

            // Bright where the glass faces the eye, warmer toward its rim, so it reads as a lit bulb, not a flat blob
            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, fixed4 color : COLOR)
            {
                v2f o;
                float4 swayed = StringSway(vertex, color.r);
                o.pos = UnityObjectToClipPos(swayed);
                float3 toEye = normalize(WorldSpaceViewDir(swayed));
                o.facing = saturate(dot(normalize(UnityObjectToWorldNormal(normal)), toEye));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return lerp(_Color, _CoreColour, pow(i.facing, _CoreSharpness)) * _Glow;
            }
            ENDCG
        }
    }
}
