#include "UnityIndirect.cginc"
            
struct Voxel
{
    uint id;
    float3 position;
};

StructuredBuffer<Voxel> voxels;
Texture3D colors;
float4 colorSize;
uniform float4x4 _ObjectToWorld;

float3 GetVoxelPosition(float3 meshPositionOS, uint svInstanceID)
{
    uint instanceID = GetIndirectInstanceID(svInstanceID);
    return meshPositionOS + voxels[instanceID].position;
}

float4 GetColor(uint instanceID)
{
    uint idOfCell = voxels[instanceID].id;
    uint width = colorSize.x, height = colorSize.y;
    int x = idOfCell % width;
    int y = (idOfCell / width) % height;
    int z = idOfCell / (width * height);
    half4 color = colors.Load(int4(x, y, z, 0));
    return color;  
}
