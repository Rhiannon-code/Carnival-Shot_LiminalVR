// A sign: lit like Standard, with the same property names, so its materials move over and keep their values. It also clips
// a bullet hole through itself where SignHole says, with a torn pale rim round it. With no hole the mask reads its white
// corner, so nothing is cut: the shatter shards share these materials and stay whole. Lift keeps it readable in the dark
Shader "CrystalCatch/Sign"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _GlossMapScale ("Smoothness Scale", Range(0, 1)) = 1
        [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _BumpScale ("Normal Scale", Float) = 1
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _HoleMask ("Bullet hole mask, set by step 41", 2D) = "white" {}
        _RimColour ("Torn rim", Color) = (0.72, 0.55, 0.34, 1)
        _Lift ("Lift with the room lit: how much of its own colour it shows with no light on it", Range(0, 1)) = 0.3
        _LiftDark ("Lift in a blackout, just enough to find it", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Standard vertex:vert noforwardadd nolightmap nodynlightmap nodirlightmap nofog
        #pragma target 3.0
        #pragma shader_feature _NORMALMAP
        #pragma shader_feature _METALLICGLOSSMAP

        struct Input
        {
            float2 uv_MainTex;
            float2 holeUv;
        };

        fixed4 _Color;
        sampler2D _MainTex;
        half _Glossiness;
        half _GlossMapScale;
        half _Metallic;
        sampler2D _MetallicGlossMap;
        half _BumpScale;
        sampler2D _BumpMap;
        sampler2D _HoleMask;
        fixed4 _RimColour;
        half _Lift;
        half _LiftDark;
        half _RoomLight;
        float4 _HoleU;
        float4 _HoleV;

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.holeUv = float2(dot(v.vertex.xyz, _HoleU.xyz) + _HoleU.w, dot(v.vertex.xyz, _HoleV.xyz) + _HoleV.w);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed mask = tex2D(_HoleMask, IN.holeUv).r;
            clip(mask - 0.1);

            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = mask < 0.75 ? _RimColour.rgb : albedo.rgb;

            // Signs are the targets, so they read in a dim room whatever light the probes have. In a blackout it nearly goes,
            // or they look pasted on over the dark. _RoomLight is set by CarnivalLights, 0 dark to 1 lit
            o.Emission = o.Albedo * lerp(_LiftDark, _Lift, _RoomLight);

        #if _METALLICGLOSSMAP
            fixed4 metal = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Metallic = metal.r;
            o.Smoothness = metal.a * _GlossMapScale;
        #else
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        #endif

        #if _NORMALMAP
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
        #endif
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
