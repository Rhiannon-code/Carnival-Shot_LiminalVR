
Shader "Hidden/RangeSetup/FitMask"
{
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

            float _FitSign;
            float _FitLow;
            float _FitHigh;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float height : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.height = mul(unity_ObjectToWorld, v.vertex).y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float up = saturate((i.height - _FitLow) / max(_FitHigh - _FitLow, 0.0001));
                return _FitSign > 0.5 ? fixed4(up, 1, 0, 1) : fixed4(0, 0, 0, 1);
            }
            ENDCG
        }
    }
}
