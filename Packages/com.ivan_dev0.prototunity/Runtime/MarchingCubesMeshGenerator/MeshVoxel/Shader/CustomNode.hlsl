#ifndef VOXELS
#define VOXELS

#include "Packages/com.ivan_dev0.prototunity/Runtime/CutThrough/Shader/CutThrough.hlsl"

struct Voxel
{
    uint id;
    float3 position;
};

StructuredBuffer<Voxel> voxels;
Texture3D<float> points;
Texture3D colors;
float4 colorSize;
Texture2D<float> heights;

int3 IdToCoord(uint instanceID)
{
    uint idOfCell = voxels[instanceID].id;
    uint width = colorSize.x, height = colorSize.y;
    int x = idOfCell % width;
    int y = (idOfCell / width) % height;
    int z = idOfCell / (width * height);
    return int3(x, y, z); 
}

// Same idea as `CutThrough_float`, but instead of discarding pixels it scales down voxels inside the cut through.
// `voxelPosition` is the result of `GetVoxelPosition`, `visiblePosition` is the result of `HideFacesInsideVolume`.
void GetVoxelPositionScaled_float(float3 positionOS, float3 voxelPosition, float3 visiblePosition, out float3 scaledPosition)
{
    scaledPosition = visiblePosition;
// Keep the shadow caster untouched, so light does not pass through the scaled voxels
#if !defined(SHADERPASS) || !defined(SHADERPASS_SHADOWCASTER) || SHADERPASS != SHADERPASS_SHADOWCASTER
    float3 center = voxelPosition - positionOS;
    float scale = 1 - GetCutThroughAmount(center);
    // Show all faces if the voxel is scaled down
    if (scale < 1)
    {
        // Note: if `softness == 0` then we will not see the faces of full neighbouring voxels.
        // To fix it add `|| GetCutThroughAmount(center + normalWS) > 0` condition.
        scaledPosition = center + positionOS * scale;
    }
#endif
}

void GetVoxelPosition_float(uint instanceId, float3 positionOS, out float3 voxelPosition)
{
    int3 coord = IdToCoord(instanceId);
    float height = heights.Load(int3(coord[0], coord[2], 0));
    voxelPosition = positionOS + voxels[instanceId].position + float3(0, height, 0);
}

void HideFacesInsideVolume_float(uint instanceId, float3 positionWS, float3 normalWS, out float3 voxelPosition)
{
    int3 coord = IdToCoord(instanceId);
    int3 neighbor = coord + int3(round(normalWS));
    bool neighborIsInside = all(neighbor >= 0) && all(neighbor < int3(colorSize.xyz));
    bool faceMustBeHidden = neighborIsInside && points.Load(int4(neighbor, 0)) <= 0;
    // Hide face if it is completely inside the mesh, so we don't see them with cut through
    voxelPosition = faceMustBeHidden ? float3(0, 0, 0) : positionWS;
}

void GetColor_float(uint instanceID, out float4 color)
{
    int3 coord = IdToCoord(instanceID);
    color = colors.Load(int4(coord, 0));
}

// --- CAVE logic ---

Texture3D<uint> dugArea; // 1 - the point was dug, 0 - otherwise
float showOnlyCaves;

// The voxel is a wall of a cave if at least one of its neighbours was dug
bool IsCaveVoxel(int3 coord)
{
    for (int i = -1; i <= 1; ++i)
    {
        for (int j = -1; j <= 1; ++j)
        {
            for (int k = -1; k <= 1; ++k)
            {
                int3 neighbor = coord + int3(i, j, k);
                if (any(neighbor < 0) || any(neighbor >= int3(colorSize.xyz))) continue;
                if (dugArea.Load(int4(neighbor, 0)) != 0) return true;
            }
        }
    }
    return false;
}

// When only caves must be shown (e.g. the target is underground) hide all voxels except the walls of caves
bool ShowOnlyCave(int3 coord)
{
    // Keep the shadow caster untouched, so light does not pass through the hidden voxels
    #if !defined(SHADERPASS) || !defined(SHADERPASS_SHADOWCASTER) || SHADERPASS != SHADERPASS_SHADOWCASTER
    return showOnlyCaves > 0.5 && !IsCaveVoxel(coord);
    #else
    return false;
    #endif
}

void ShowOnlyCave_float(uint instanceId, float3 positionWS, out float3 voxelPosition)
{
    int3 coord = IdToCoord(instanceId);
    voxelPosition = ShowOnlyCave(coord) ? float3(0, 0, 0) : positionWS;
}

#endif // VOXELS