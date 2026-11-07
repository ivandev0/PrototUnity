#ifndef VOXELS
#define VOXELS

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
    if (faceMustBeHidden)
    {
        // Hide face if it is completely inside the mesh, so we don't see them with cut through
        voxelPosition = float3(0, 0, 0);
    } else
    {
        voxelPosition = positionWS;
    }
}

void GetColor_float(uint instanceID, out float4 color)
{
    int3 coord = IdToCoord(instanceID);
    color = colors.Load(int4(coord, 0));
}

#endif // VOXELS