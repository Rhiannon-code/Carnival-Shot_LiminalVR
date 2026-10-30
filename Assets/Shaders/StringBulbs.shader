// A whole string of carnival bulbs in one mesh. _Glow is set by CarnivalLights, in step with the baked lighting it
// swaps, so the bulbs go dark exactly when the light they throw does
Shader "CrystalCatch/String Bulbs"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.8, 0.45, 1)
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

            fixed4 _Color;
            float _Glow;

            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }

            fixed4 frag() : SV_Target { return _Color * _Glow; }
            ENDCG
        }
    }
}
