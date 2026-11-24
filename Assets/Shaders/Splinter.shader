Shader "CrystalCatch/Splinter"
{
    Properties
    {
        _MainTex ("Sheet of shapes (alpha cuts them out)", 2D) = "white" {}
        _Bright ("Brightness, to make up for the sheet's greys", Float) = 1.4
        _BlackoutShare ("Brightness left in a blackout (1 = never dims)", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Bright;
            fixed _BlackoutShare;
            // Set by CarnivalLights: 0 with every string of lights off, 1 with them all on
            half _RoomLight;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = half4(v.color.rgb * _Bright * lerp(_BlackoutShare, 1, saturate(_RoomLight)), v.color.a);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 shape = tex2D(_MainTex, i.uv);
                clip(shape.a - 0.5);
                return fixed4(shape.rgb * i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
