#include "../Common/LightingShadowCommon.fxh"
#include "../Common/TextureLayerSelect.fxh"

Texture2DArray Textures : register(t0);
SamplerState TextureSampler : register(s0);

float2 HexOutlineCenter = float2(0.0f, 0.0f);
float HexOutlineApothem = 1.0f;
float FaceSdfRangePx = 16.0f;
float FaceOutlineCoreWidthPx = 1.0f;
float FaceInnerGradientWidthPx = 4.0f;
float FaceOutlineStrength = 1.0f;
float3 FaceOutlineColor = float3(0.0f, 0.0f, 0.0f);
float FaceBrightness = 1.0f;
float FaceHatchStrength = 0.0f;
float FaceHatchSpacingWorld = 0.3f;
float FaceHatchLineWidthWorld = 0.03f;
float FaceOutlineMinSurfaceUpDot = 0.35f;
float FaceOutlineSurfaceUpDotBlendWidth = 0.2f;
bool ObjectiveOverlayEnabled = false;
float3 ObjectiveOverlayColor = float3(0.0f, 0.0f, 0.0f);
float ObjectiveOverlayCoreWidthPx = 1.1f;
float ObjectiveOverlayInnerGradientWidthPx = 5.0f;
float ObjectiveOverlayStrength = 0.95f;
float ObjectiveOverlayHatchStrength = 0.7f;
float ObjectiveOverlayHatchSpacingWorld = 0.24f;
float ObjectiveOverlayHatchLineWidthWorld = 0.05f;
bool AttackMarkerEnabled = false;
float3 AttackMarkerColor = float3(1.0f, 0.45f, 0.12f);
float AttackMarkerTime = 0.0f;

struct VSInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float2 TexCoord1 : TEXCOORD1;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float3 WorldPos : TEXCOORD1;
    float TexIndex : TEXCOORD2;
    float4 LightClipPos : TEXCOORD3;
};

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    float4 worldPos = mul(input.Position, World);
    float4 viewPos = mul(worldPos, View);
    float4 clipPos = mul(viewPos, Projection);

    output.Position = clipPos;
    output.WorldPos = worldPos.xyz;
    output.Normal = normalize(mul(float4(input.Normal, 0.0f), World).xyz);
    output.TexCoord = input.TexCoord;
    output.TexIndex = input.TexCoord1.x;
    output.LightClipPos = mul(worldPos, LightViewProjection);

    return output;
}

float SignedDistanceToFlatTopHex(float2 position, float apothem)
{
    const float3 hexConstants = float3(-0.866025404f, 0.5f, 0.577350269f);

    float2 p = abs(position);
    p -= 2.0f * min(dot(hexConstants.xy, p), 0.0f) * hexConstants.xy;
    p -= float2(clamp(p.x, -hexConstants.z * apothem, hexConstants.z * apothem), apothem);

    return length(p) * sign(p.y);
}

float DistanceToSegment(float2 samplePosition, float2 start, float2 end)
{
    float2 segment = end - start;
    float segmentLengthSquared = max(dot(segment, segment), 1e-6f);
    float segmentProgress = saturate(
        dot(samplePosition - start, segment) / segmentLengthSquared
    );
    float2 nearestPoint = start + (segment * segmentProgress);
    return length(samplePosition - nearestPoint);
}

float SegmentMask(
    float2 samplePosition,
    float2 start,
    float2 end,
    float halfWidth,
    float antiAliasWidth
)
{
    float segmentDistance = DistanceToSegment(samplePosition, start, end);
    return 1.0f - smoothstep(
        halfWidth - antiAliasWidth,
        halfWidth + antiAliasWidth,
        segmentDistance
    );
}

float4 PSMain(VSOutput input) : SV_Target0
{
    // layer 0 = hex face
    // layer 1 = hex side
    // layer 2 = hex face/side transition
    float layer = SelectTextureLayer(input.TexIndex);

    float3 baseColor = Textures.Sample(TextureSampler, float3(input.TexCoord, layer)).rgb;
    float objectiveOverlayOutlineMask = 0.0f;
    float objectiveOverlayHatchMask = 0.0f;
    float attackMarkerMask = 0.0f;
    if (layer < 1.0)
    {
        float2 localHexPosition = input.WorldPos.xy - HexOutlineCenter;
        float sdfOutlineFudgeFactor = 0.02f;
        float signedDistanceWorld = SignedDistanceToFlatTopHex(
            localHexPosition,
            HexOutlineApothem + sdfOutlineFudgeFactor
        );
        float outlineScale = FaceSdfRangePx / max(HexOutlineApothem, 1e-4f);
        float distPx = -signedDistanceWorld * outlineScale;
        float aaPx = max(fwidth(distPx), 0.5f);
        float surfaceUpDot = dot(normalize(input.Normal), float3(0.0f, 0.0f, 1.0f));
        float surfaceBlendWidth = max(FaceOutlineSurfaceUpDotBlendWidth, 1e-4f);
        float surfaceMask = smoothstep(
            FaceOutlineMinSurfaceUpDot,
            FaceOutlineMinSurfaceUpDot + surfaceBlendWidth,
            surfaceUpDot
        );

        float insideMask = smoothstep(-aaPx, aaPx, distPx);
        float outlineCoreEndPx = max(FaceOutlineCoreWidthPx, 0.0f);
        float outlineFadeEndPx = outlineCoreEndPx + max(FaceInnerGradientWidthPx, 0.0f);

        float outline = 1.0f
            - smoothstep(outlineCoreEndPx - aaPx, outlineFadeEndPx + aaPx, distPx);
        outline *= insideMask;
        outline *= surfaceMask;
        outline *= saturate(FaceOutlineStrength);

        baseColor = lerp(baseColor, FaceOutlineColor, saturate(outline));

        float hatchSpacingWorld = max(FaceHatchSpacingWorld, 1e-4f);
        float hatchStrokeWidthWorld = max(FaceHatchLineWidthWorld, 0.0f);
        float hatchHalfStrokeWidthWorld = hatchStrokeWidthWorld * 0.5f;
        float hatchCoordinate = dot(localHexPosition, normalize(float2(1.0f, 1.0f)));
        float hatchWrappedDistance = abs(
            frac((hatchCoordinate / hatchSpacingWorld) + 0.5f) - 0.5f
        ) * hatchSpacingWorld;
        float hatchAntiAliasWorld = max(fwidth(hatchCoordinate), 0.0035f);
        float hatchInteriorMask = smoothstep(
            outlineFadeEndPx + aaPx,
            outlineFadeEndPx + (aaPx * 3.0f),
            distPx
        );
        float hatchMask = 1.0f - smoothstep(
            hatchHalfStrokeWidthWorld - hatchAntiAliasWorld,
            hatchHalfStrokeWidthWorld + hatchAntiAliasWorld,
            hatchWrappedDistance
        );
        hatchMask *= insideMask;
        hatchMask *= surfaceMask;
        hatchMask *= hatchInteriorMask;
        hatchMask *= saturate(FaceHatchStrength);
        baseColor = lerp(baseColor, FaceOutlineColor, saturate(hatchMask));

        if (ObjectiveOverlayEnabled)
        {
            float objectiveOutlineCoreEndPx = max(ObjectiveOverlayCoreWidthPx, 0.0f);
            float objectiveOutlineFadeEndPx = objectiveOutlineCoreEndPx
                + max(ObjectiveOverlayInnerGradientWidthPx, 0.0f);
            objectiveOverlayOutlineMask = 1.0f - smoothstep(
                objectiveOutlineCoreEndPx - aaPx,
                objectiveOutlineFadeEndPx + aaPx,
                distPx
            );
            objectiveOverlayOutlineMask *= insideMask;
            objectiveOverlayOutlineMask *= surfaceMask;
            objectiveOverlayOutlineMask *= saturate(ObjectiveOverlayStrength);

            float objectiveHatchSpacingWorld = max(ObjectiveOverlayHatchSpacingWorld, 1e-4f);
            float objectiveHatchStrokeWidthWorld = max(
                ObjectiveOverlayHatchLineWidthWorld,
                0.0f
            );
            float objectiveHatchHalfStrokeWidthWorld = objectiveHatchStrokeWidthWorld * 0.5f;
            float objectiveHatchInteriorMask = smoothstep(
                objectiveOutlineFadeEndPx + aaPx,
                objectiveOutlineFadeEndPx + (aaPx * 3.0f),
                distPx
            );
            float objectiveHatchWrappedDistance = abs(
                frac((hatchCoordinate / objectiveHatchSpacingWorld) + 0.5f) - 0.5f
            ) * objectiveHatchSpacingWorld;
            objectiveOverlayHatchMask = 1.0f - smoothstep(
                objectiveHatchHalfStrokeWidthWorld - hatchAntiAliasWorld,
                objectiveHatchHalfStrokeWidthWorld + hatchAntiAliasWorld,
                objectiveHatchWrappedDistance
            );
            objectiveOverlayHatchMask *= insideMask;
            objectiveOverlayHatchMask *= surfaceMask;
            objectiveOverlayHatchMask *= objectiveHatchInteriorMask;
            objectiveOverlayHatchMask *= saturate(ObjectiveOverlayHatchStrength);
        }

        if (AttackMarkerEnabled)
        {
            float pulse = 0.5f + (0.5f * sin(AttackMarkerTime * 4.5f));
            float antiAliasWorld = max(fwidth(signedDistanceWorld), 0.0035f);

            float ringInset = HexOutlineApothem * lerp(0.15f, 0.2f, pulse);
            float ringHalfWidth = HexOutlineApothem * 0.0325f;
            float ringDistance = abs(signedDistanceWorld + ringInset);
            float ringMask = 1.0f - smoothstep(
                ringHalfWidth - antiAliasWorld,
                ringHalfWidth + antiAliasWorld,
                ringDistance
            );
            ringMask *= insideMask;
            ringMask *= surfaceMask;

            float crossHalfWidth = HexOutlineApothem * 0.027f;
            float crossHalfLength = HexOutlineApothem * 0.655f;

            float horizontalCrossMask = SegmentMask(
                localHexPosition,
                float2(-crossHalfLength, 0.0f),
                float2(crossHalfLength, 0.0f),
                crossHalfWidth,
                antiAliasWorld
            );
            float verticalCrossMask = SegmentMask(
                localHexPosition,
                float2(0.0f, -crossHalfLength),
                float2(0.0f, crossHalfLength),
                crossHalfWidth,
                antiAliasWorld
            );

            float reticleMask = max(horizontalCrossMask, verticalCrossMask);
            reticleMask *= insideMask;
            reticleMask *= surfaceMask;

            float markerMask = max(ringMask * lerp(0.7f, 1.0f, pulse), reticleMask);
            attackMarkerMask = saturate(markerMask);
        }
    }

    float shadow = ComputeShadowPCF(
        input.LightClipPos,
        0.0005,
        float2(1.0 / 4096.0, 1.0 / 4096.0),
        0.25
    );

    float3 litColor = ComputeLitColor(baseColor, input.Normal, input.WorldPos, shadow);
    litColor *= saturate(FaceBrightness);
    if (layer < 1.0f)
    {
        litColor = lerp(
            litColor,
            ObjectiveOverlayColor,
            saturate(objectiveOverlayOutlineMask)
        );
        litColor = lerp(
            litColor,
            ObjectiveOverlayColor,
            saturate(objectiveOverlayHatchMask)
        );
        litColor = lerp(litColor, AttackMarkerColor, saturate(attackMarkerMask));
    }

    return float4(litColor, 1.0f);
}

technique Basic
{
    pass P0
    {
        VertexShader = compile vs_6_0 VSMain();
        PixelShader = compile ps_6_0 PSMain();
    }
}
