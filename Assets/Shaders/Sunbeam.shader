// A shaft of daylight through a shot-open skylight (Skylight.cs). Added on top, brightest face-on and at the
// top, fading out at its edges, so it reads as a beam rather than a tube. No light is involved
Shader "CrystalCatch/Sunbeam"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.92, 0.7, 1)
        _Strength ("Strength (set by script)", Range(0, 2)) = 0
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

            fixed4 _Color;
            float _Strength;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float up : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.up = v.texcoord.y;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                return _Color * (facing * facing * lerp(0.25, 1, i.up) * _Strength);
            }
            ENDCG
        }
    }
}
