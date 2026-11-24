Shader "CrystalCatch/Laser"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.15, 0.12, 1)
        _Noise ("Noise (tileable, red channel)", 2D) = "white" {}
        [Toggle] _Dot ("Dot (a sphere) rather than the beam", Float) = 0
        _Patch ("Patches per metre", Float) = 1.6
        _Crawl ("Crawl, metres per second", Float) = 0.6
        _Thin ("How faint the thin patches get", Range(0, 1)) = 0.5
        _Flicker ("Flicker", Range(0, 1)) = 0.25
        _Hot ("Paler middle", Range(0, 1)) = 0.6
        _Pale ("The middle's colour", Color) = (1, 0.6, 0.55, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _Color, _Pale;
            sampler2D _Noise;
            float _Dot, _Patch, _Crawl, _Thin, _Flicker, _Hot;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float facing : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.facing = dot(normalize(UnityObjectToWorldNormal(v.normal)), normalize(_WorldSpaceCameraPos - o.world));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 0 in the middle, 1 at the edge
                float out_ = _Dot > 0.5 ? 1 - saturate(i.facing) : abs(i.uv.y * 2 - 1);

                // The beam is patchy along its length; the dot is blotchy across its face
                float along = dot(i.world, float3(0.577, 0.577, 0.577));
                float2 at = _Dot > 0.5 ? float2(i.world.x + i.world.z, i.world.y) * _Patch + _Time.y * _Crawl
                                       : float2(along * _Patch - _Time.y * _Crawl, 0.37);
                float patch = tex2D(_Noise, at).r;

                // A ragged edge: the patches eat into it
                float body = smoothstep(1, 0.35 + 0.4 * patch, out_ + (1 - patch) * 0.25);
                float steady = 1 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 31)) * (0.5 + 0.5 * sin(_Time.y * 17.3));
                float alpha = saturate(body * lerp(1 - _Thin, 1, patch)) * steady;

                float hot = _Hot * pow(saturate(1 - out_), 3);
                fixed3 rgb = lerp(_Color.rgb * i.color.rgb, _Pale.rgb, saturate(hot));
                return fixed4(rgb, alpha * _Color.a * i.color.a);
            }
            ENDCG
        }
    }
}
