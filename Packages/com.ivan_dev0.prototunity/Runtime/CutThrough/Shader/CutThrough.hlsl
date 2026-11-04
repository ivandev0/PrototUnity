#ifndef CUT_THROUGH
#define CUT_THROUGH

// Set per material by CutThroughController
float3 _CutThroughTargetPosition;
float3 _CutThroughAxis;      // Normalized direction from the target to the camera
float _CutThroughRadius;     // 0 - no cut at all
float _CutThroughNearOffset; // Distance from the target towards the camera where the cut starts
float _CutThroughEdgeSoftness;

float GetCutThroughAmount(float3 positionWS)
{
    float3 offset = positionWS - _CutThroughTargetPosition;
    float along = dot(offset, _CutThroughAxis);
    float perpendicular = length(offset - _CutThroughAxis * along);

    float softness = max(_CutThroughEdgeSoftness, 0.0001);
    float radial = saturate((_CutThroughRadius - perpendicular) / softness);
    float depth = saturate((along - _CutThroughNearOffset) / softness);
    return radial * depth;
}

static const float CutThroughBayer[16] = {
    0, 8, 2, 10,
    12, 4, 14, 6,
    3, 11, 1, 9,
    15, 7, 13, 5
};

float GetBayerThreshold(float2 pixelPosition)
{
    uint2 pixel = uint2(pixelPosition) % 4;
    return (CutThroughBayer[pixel.y * 4 + pixel.x] + 0.5) / 16.0;
}

void CutThrough_float(float3 positionWS, float2 pixelPosition, out float alpha)
{
    alpha = 1;
// `#if` exists because we want to cut part of the mesh but do not allow for light to pass through
#if !defined(SHADERPASS) || !defined(SHADERPASS_SHADOWCASTER) || SHADERPASS != SHADERPASS_SHADOWCASTER
    if (GetCutThroughAmount(positionWS) > GetBayerThreshold(pixelPosition))
    {
        alpha = 0;
    }
#endif
}

#endif // CUT_THROUGH
