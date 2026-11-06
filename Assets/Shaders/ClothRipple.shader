// Faked cloth for the canvas roof and the banners: a slow ripple in the vertex shader, no physics (ADR 0064, fake-cloth test).
// Vertex colour red is how free a vertex is, 0 where it's pinned. The bake sees the mesh at rest, so the ripple stays small
Shader "CrystalCatch/Cloth Ripple"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.8, 0.7, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Amplitude ("Sway at a free vertex, metres", Float) = 0.02
        _Speed ("Speed", Float) = 1.2
        _Waves ("Waves per metre", Float) = 1.5
        _AirAmplitude ("A passing round's push at a free vertex, metres", Float) = 0.05
        _AirReach ("How far the push carries, metres", Float) = 0.6
        _AirLife ("How long it takes to settle, seconds", Float) = 1.2
        _AirSpeed ("How fast the wave spreads, metres a second", Float) = 2.5
        _AirWaves ("Wave crests per metre", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        // Seen from under it and from the room side, so neither face may be culled
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert noforwardadd nodynlightmap nodirlightmap
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Amplitude;
        float _Speed;
        float _Waves;
        float _AirAmplitude;
        float _AirReach;
        float _AirLife;
        float _AirSpeed;
        float _AirWaves;
        // The last few rounds that passed close: where (xyz) and when (w, in _Time.y), set by ClothAir
        float4 _ClothAir[4];

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert (inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y * _Speed;
            float sway = sin(t + world.x * _Waves) + 0.5 * sin(t * 1.7 + world.z * _Waves * 1.3);
            float push = 0;
            for (int i = 0; i < 4; i++)
            {
                float age = _Time.y - _ClothAir[i].w;
                float d = distance(world, _ClothAir[i].xyz);
                float fade = saturate(1 - age / _AirLife);
                push += (age >= 0) * sin((d - age * _AirSpeed) * _AirWaves * 6.2832) * exp(-d / _AirReach) * fade * fade;
            }
            v.vertex.xyz += v.normal * (sway * _Amplitude + push * _AirAmplitude) * v.color.r;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
