#ifndef VOXELS
#define VOXELS

struct Voxel
{
    uint id;
    float3 position;
};

StructuredBuffer<Voxel> voxels;
Texture3D colors;
float4 colorSize;

void GetVoxelPosition_float(uint instanceId, out float3 voxelPosition)
{
    voxelPosition = voxels[instanceId].position;
}

#endif // VOXELS