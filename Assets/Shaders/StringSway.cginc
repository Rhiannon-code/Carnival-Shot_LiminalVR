#ifndef STRING_SWAY_INCLUDED
#define STRING_SWAY_INCLUDED

static const float SwayAmplitude = 0.03;
static const float SwaySpeed = 0.5;
static const float SwayWaves = 0.6;

float4 StringSway(float4 vertex, float freedom)
{
    float3 world = mul(unity_ObjectToWorld, vertex).xyz;
    float phase = (world.x + world.z) * SwayWaves;
    float t = _Time.y * SwaySpeed;
    float3 push = float3(sin(t + phase), 0.3 * sin(1.7 * t + 1.3 * phase), cos(0.8 * t + phase)) * SwayAmplitude * freedom;
    vertex.xyz += mul((float3x3)unity_WorldToObject, push);
    return vertex;
}

#endif
