#ifndef CUT_THROUGH
#define CUT_THROUGH

// Set per material by CutThroughController
float3 _CutThroughTargetPosition;
float3 _CutThroughAxis;      // Normalized direction from the target to the camera
float _CutThroughRadius;     // 0 - no cut at all
float _CutThroughNearOffset; // Distance from the target towards the camera where the cut starts

float GetCutThroughAmount(float3 positionWS)
{
    float3 offset = positionWS - _CutThroughTargetPosition;
    float along = dot(offset, _CutThroughAxis);
    float perpendicular = length(offset - _CutThroughAxis * along);

    float radial = saturate(_CutThroughRadius - perpendicular);
    float depth = saturate(along - _CutThroughNearOffset);
    return radial * depth;
}

void CutThrough_float(float3 positionWS, out float alpha)
{
    alpha = 1;
// `#if` exists because we want to cut part of the mesh but do not allow for light to pass through
#if !defined(SHADERPASS) || !defined(SHADERPASS_SHADOWCASTER) || SHADERPASS != SHADERPASS_SHADOWCASTER
    alpha = 1 - GetCutThroughAmount(positionWS);
#endif
}

#endif // CUT_THROUGH
