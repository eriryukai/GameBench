#include "BindingHelpers.hlsli"

struct VertexOutput
{
    float4 Position : SV_Position;
    float3 Color : COLOR0;
};

VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    static const float2 positions[3] = {
        float2( 0.0, -0.5),
        float2( 0.5,  0.5),
        float2(-0.5,  0.5)
    };

    static const float3 colors[3] = {
        float3(1.0, 0.0, 0.0),
        float3(0.0, 1.0, 0.0),
        float3(0.0, 0.0, 1.0)
    };

    VertexOutput output;
    output.Position = float4(positions[vertexId], 0.0, 1.0);
    output.Color = colors[vertexId];
    return output;
}

float4 PSMain(VertexOutput input) : SV_Target0
{
    return float4(input.Color, 1.0);
}
