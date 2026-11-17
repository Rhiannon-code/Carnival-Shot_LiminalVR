#ifndef FOG_HOLES_INCLUDED
#define FOG_HOLES_INCLUDED

float4 _Hole0;
float4 _Hole1;
float4 _Hole2;
float4 _Hole3;

float Hole (float2 at, float4 hole)
{
    return hole.z <= 0 ? 1 : smoothstep(hole.z, hole.z + hole.w, distance(at, hole.xy));
}

float FogHoles (float2 at)
{
    return Hole(at, _Hole0) * Hole(at, _Hole1) * Hole(at, _Hole2) * Hole(at, _Hole3);
}

#endif
