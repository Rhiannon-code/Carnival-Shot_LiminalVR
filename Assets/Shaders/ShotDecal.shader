Shader "IntuitiveDesigns/Shot Decal"
{
    Properties
    {
        _MainTex ("Holes (RGBA)", 2D) = "white" {}
        _DarkShare ("Brightness in a blackout, as a share of lit", Range(0, 1)) = 0.12
        _WoodEdge ("Raw wood on the torn edge", Range(0, 1)) = 0.5
    }

    SubShader
    {
        // After everything solid, before the fog and the other see-through effects drawn over it
        Tags { "RenderType" = "Transparent" "Queue" = "AlphaTest+50" "IgnoreProjector" = "True" }
        LOD 100

        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -4

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _DarkShare;
            half _RoomLight;
            half _WoodEdge;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 art = tex2D(_MainTex, i.uv);
                // The wood is unlit, so it would glow in a blackout. _RoomLight is set by CarnivalLights
                half3 wood = art.rgb * i.color.rgb * _WoodEdge * lerp(_DarkShare, 1, _RoomLight);
                return fixed4(wood, art.a) * i.color.a;
            }
            ENDCG
        }
    }
}
