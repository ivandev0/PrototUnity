#ifndef VORONOI
#define VORONOI

struct VoronoiCell
{
    uint vertexCount;
    uint vertexStart;
};

struct Voxel
{
    uint id;
    float3 position;
};

StructuredBuffer<Voxel> idsToRender;
StructuredBuffer<VoronoiCell> cells;
StructuredBuffer<float3> vertices;
Texture3D colors;
float4 colorSize;

void GetVoxelPosition_float(float3 meshPositionOS, uint instanceID, uint vertexID, out float3 result)
{
    uint idOfCell = idsToRender[instanceID].id;
    VoronoiCell cell = cells[idOfCell];
    if (vertexID >= cell.vertexCount)
    {
        result = meshPositionOS;
        return;
    }
    result = meshPositionOS + vertices[cell.vertexStart + vertexID];
}

void GetVoxelNormal_float(float3 normal, uint instanceID, uint vertexID, out float3 result)
{
    uint idOfCell = idsToRender[instanceID].id;
    VoronoiCell cell = cells[idOfCell];
    if (vertexID >= cell.vertexCount)
    {
        result = normal;
        return;
    }

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

    result = normalize(cross(v2 - v0, v1 - v0));
}

void GetColor_float(uint instanceID, out float4 color)
{
    uint idOfCell = idsToRender[instanceID].id;
    uint width = colorSize.x, height = colorSize.y;
    int x = idOfCell % width;
    int y = (idOfCell / width) % height;
    int z = idOfCell / (width * height);
    color = colors.Load(int4(x, y, z, 0));
}

#endif
