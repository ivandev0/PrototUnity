#include <UnityIndirect.cginc>

struct VoronoiCell
{
    uint vertexCount;
    uint vertexStart;
};
            
StructuredBuffer<uint> idsToRender;
StructuredBuffer<VoronoiCell> cells;
StructuredBuffer<float3> vertices;
Texture3D colors;
float4 colorSize;

float3 GetVoxelPosition(float3 meshPositionOS, uint svInstanceID, uint vertexID)
{
    uint instanceID = GetIndirectInstanceID(svInstanceID);
    uint idOfCell = idsToRender[instanceID];
    VoronoiCell cell = cells[idOfCell];
    if (vertexID >= cell.vertexCount) return meshPositionOS;
    return meshPositionOS + vertices[cell.vertexStart + vertexID];
}

float3 GetVoxelNormal(float3 normal, uint svInstanceID, uint vertexID)
{
    uint instanceID = GetIndirectInstanceID(svInstanceID);
    uint idOfCell = idsToRender[instanceID];
    VoronoiCell cell = cells[idOfCell];
    if (vertexID >= cell.vertexCount) return normal;

    float3 v0, v1, v2;
    if (vertexID % 3 == 0)
    {
        v0 = vertices[cell.vertexStart + vertexID];
        v1 = vertices[cell.vertexStart + vertexID + 1];
        v2 = vertices[cell.vertexStart + vertexID + 2];
    }
    else if (vertexID % 3 == 1)
    {
        v0 = vertices[cell.vertexStart + vertexID - 1];
        v1 = vertices[cell.vertexStart + vertexID];
        v2 = vertices[cell.vertexStart + vertexID + 1];
    }
    else if (vertexID % 3 == 2)
    {
        v0 = vertices[cell.vertexStart + vertexID - 2];
        v1 = vertices[cell.vertexStart + vertexID - 1];
        v2 = vertices[cell.vertexStart + vertexID];
    }

    return normalize(cross(v2 - v0, v1 - v0));
}