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

// x and y the unit direction into what it clears (the side stairs), z where the fog is gone along it, w how far it thins
float4 _ClearLine;

float ClearLine (float2 at, float4 edge)
{
    return dot(edge.xy, edge.xy) < 1e-6 ? 1 : smoothstep(0, edge.w, edge.z - dot(at, edge.xy));
}

sampler2D _FogMask;
float4 _FogMaskRect;

float Open (float2 at)
{
    return _FogMaskRect.z <= 0 ? 1 : tex2D(_FogMask, (at - _FogMaskRect.xy) * _FogMaskRect.zw).r;
}

float Thick (float2 at)
{
    return _FogMaskRect.z <= 0 ? 0 : tex2D(_FogMask, (at - _FogMaskRect.xy) * _FogMaskRect.zw).g;
}

#define PROP_HOLES 6
float4 _PropHoles[PROP_HOLES];

float PropHoles (float2 at)
{
    float open = 1;
    for (int i = 0; i < PROP_HOLES; i++) open *= Hole(at, _PropHoles[i]);
    return open;
}

float FogHoles (float2 at)
{
    return Hole(at, _Hole0) * Hole(at, _Hole1) * Hole(at, _Hole2) * Hole(at, _Hole3) * ClearLine(at, _ClearLine) * Open(at) * PropHoles(at);
}

#endif
