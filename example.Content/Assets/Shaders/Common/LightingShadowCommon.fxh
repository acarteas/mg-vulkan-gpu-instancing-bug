float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 LightViewProjection;

Texture2D ShadowMap : register(t1);
SamplerState ShadowSampler : register(s1);

float3 LightDirection = float3(0.3, -1.0, -0.2);
float3 LightColor = float3(1.0, 1.0, 1.0);
float3 AmbientColor = float3(0.12, 0.12, 0.12);
float SpecularPower = 32.0;
float SpecularIntensity = 0.5;
float3 CameraPosition;
bool ShadowsEnabled;

float ComputeShadowPCF(
    float4 lightClipPos,
    float depthBias,
    float2 shadowMapTexelSize,
    float occludedWeight
)
{
    if (!ShadowsEnabled)
    {
        return 1.0;
    }

    float2 shadowUV = 0.5 * lightClipPos.xy / lightClipPos.w + float2(0.5, 0.5);
    shadowUV.y = 1.0 - shadowUV.y;

    if (shadowUV.x < 0.0 || shadowUV.x > 1.0 || shadowUV.y < 0.0 || shadowUV.y > 1.0)
    {
        return 1.0;
    }

    float currentDepth = lightClipPos.z / lightClipPos.w;
    float shadow = 0.0;

    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            float2 offset = float2(x, y) * shadowMapTexelSize;
            float lightDepth = ShadowMap.Sample(ShadowSampler, shadowUV + offset).r;
            shadow += (currentDepth - depthBias > lightDepth) ? occludedWeight : 1.0;
        }
    }

    return shadow / 9.0;
}

float3 ComputeLitColor(float3 baseColor, float3 normal, float3 worldPos, float shadow)
{
    float3 n = normalize(normal);
    float3 l = normalize(-LightDirection);
    float3 v = normalize(CameraPosition - worldPos);
    float3 h = normalize(l + v);

    float nDotL = saturate(dot(n, l));
    float nDotH = saturate(dot(n, h));

    float3 diffuse = baseColor * LightColor * nDotL * shadow;
    float3 specular = LightColor * SpecularIntensity * pow(nDotH, SpecularPower) * shadow;
    float3 ambient = baseColor * AmbientColor;

    return ambient + diffuse + specular;
}
