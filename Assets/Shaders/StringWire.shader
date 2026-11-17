Shader "CrystalCatch/String Wire"
{
    Properties
    {
        _Color ("Colour", Color) = (0.05, 0.04, 0.04, 1)
        _Sheen ("Metal sheen at grazing angles", Color) = (0.35, 0.3, 0.25, 1)
        _Braid ("Braid twists per metre of wire", Float) = 40
        _BraidDepth ("How dark the braid's grooves are", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "StringSway.cginc"

            fixed4 _Color;
            fixed4 _Sheen;
            float _Braid;
            float _BraidDepth;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float rim : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, fixed4 color : COLOR)
            {
                v2f o;
                float4 swayed = StringSway(vertex, color.r);
                o.pos = UnityObjectToClipPos(swayed);
                o.uv = uv;
                float3 toEye = normalize(WorldSpaceViewDir(swayed));
                o.rim = 1 - saturate(dot(normalize(UnityObjectToWorldNormal(normal)), toEye));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float groove = abs(frac(i.uv.x * _Braid + i.uv.y) - 0.5) * 2;
                fixed4 col = _Color * lerp(1 - _BraidDepth, 1, groove);
                return col + _Sheen * pow(i.rim, 3);
            }
            ENDCG
        }
    }
}
