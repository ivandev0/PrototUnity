#include "UnityIndirect.cginc"
            
struct Voxel
{
    uint id;
    float3 position;
};

StructuredBuffer<Voxel> voxels;
Texture3D colors;
float4 colorSize;
Texture2D<float> heights;
uniform float4x4 _ObjectToWorld;

int3 IdToCoord(uint instanceID)
{
    uint idOfCell = voxels[instanceID].id;
    uint width = colorSize.x, height = colorSize.y;
    int x = idOfCell % width;
    int y = (idOfCell / width) % height;
    int z = idOfCell / (width * height);
    return int3(x, y, z); 
}

float3 GetVoxelPosition(float3 meshPositionOS, uint svInstanceID)
{
    uint instanceID = GetIndirectInstanceID(svInstanceID);
    int3 coord = IdToCoord(svInstanceID);
    float height = heights.Load(int3(coord[0], coord[2], 0));
    return meshPositionOS + voxels[instanceID].position + float3(0, height, 0);
}

float4 GetColor(uint instanceID)
{
    int3 coord = IdToCoord(instanceID);
    half4 color = colors.Load(int4(coord, 0));
    return color;  
}
