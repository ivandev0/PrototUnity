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

int3 IdToCoord(uint instanceID)
{
    uint idOfCell = voxels[instanceID].id;
    uint width = colorSize.x, height = colorSize.y;
    int x = idOfCell % width;
    int y = (idOfCell / width) % height;
    int z = idOfCell / (width * height);
    return int3(x, y, z); 
}

void GetColor_float(uint instanceID, out float4 color)
{
    int3 coord = IdToCoord(instanceID);
    color = colors.Load(int4(coord, 0));
}

#endif // VOXELS