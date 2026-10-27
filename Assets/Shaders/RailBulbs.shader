// A whole rail's bulbs in one mesh, so one draw call a rail. Each bulb's place along the rail is in uv.x
// (0 at the launch end), and the lit spot runs along it as _Chase falls from 1 to 0 (RailBulbs.cs)
Shader "CrystalCatch/Rail Bulbs"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.75, 0.4, 1)
        _Idle ("Idle glow", Range(0, 1)) = 0.15
        _Bright ("Lit glow", Float) = 2
        _Width ("Lit length (share of the rail)", Range(0.01, 1)) = 0.2
        _Chase ("Lit spot (set by script, below 0 = none)", Float) = -1
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

            fixed4 _Color;
            float _Idle;
            float _Bright;
            float _Width;
            float _Chase;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float along : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.along = v.texcoord.x;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float lit = _Chase < 0 ? 0 : saturate(1 - abs(i.along - _Chase) / _Width);
                return _Color * (_Idle + _Bright * lit);
            }
            ENDCG
        }
    }
}
