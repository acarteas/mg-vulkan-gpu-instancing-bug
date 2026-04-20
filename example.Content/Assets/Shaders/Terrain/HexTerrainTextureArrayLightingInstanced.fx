#include "../Common/LightingShadowCommon.fxh"
#include "../Common/TextureLayerSelect.fxh"

Texture2DArray Textures : register(t0);
SamplerState TextureSampler : register(s0);

float4x4 MeshLocalTransform;
float HexOutlineApothem = 1.0f;
float FaceSdfRangePx = 16.0f;
float FaceOutlineMinSurfaceUpDot = 0.35f;
float FaceOutlineSurfaceUpDotBlendWidth = 0.2f;
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
    float4 OutlineCenterAndWidths : TEXCOORD4;
    float4 OutlineColorAndStrength : TEXCOORD5;
    float4 BrightnessAndFlags : TEXCOORD6;
    float2 HatchSpacingAndWidth : TEXCOORD7;
    float4 ObjectiveOverlayColorAndStrength : TEXCOORD8;
    float4 ObjectiveOverlayWidthsAndHatch : TEXCOORD9;
    float4 ObjectiveOverlaySpacingAndFlags : TEXCOORD10;
};

float SignedDistanceToFlatTopHex(float2 position, float apothem)
{
    const float3 hexConstants = float3(-0.866025404f, 0.5f, 0.577350269f);

    float2 p = abs(position);
    p -= 2.0f * min(dot(hexConstants.xy, p), 0.0f) * hexConstants.xy;
    p -= float2(clamp(p.x, -hexConstants.z * apothem, hexConstants.z * apothem), apothem);

    return length(p) * sign(p.y);
}

float DistanceToSegment(float2 samplePosition, float2 startPosition, float2 endPosition)
{
    float2 segmentVector = endPosition - startPosition;
    float segmentLengthSquared = max(dot(segmentVector, segmentVector), 1e-6f);
    float segmentProgress = saturate(
        dot(samplePosition - startPosition, segmentVector) / segmentLengthSquared
    );
    float2 nearestPosition = startPosition + (segmentVector * segmentProgress);
    return length(samplePosition - nearestPosition);
}

float SegmentMask(
    float2 samplePosition,
    float2 startPosition,
    float2 endPosition,
    float halfWidth,
    float antiAliasWidth
)
{
    float segmentDistance = DistanceToSegment(samplePosition, startPosition, endPosition);
    return 1.0f - smoothstep(
        halfWidth - antiAliasWidth,
        halfWidth + antiAliasWidth,
        segmentDistance
    );
}

VSOutput VSMain(
    VSInput input,
    float4 instanceWorldRow0 : TEXCOORD2,
    float4 instanceWorldRow1 : TEXCOORD3,
    float4 instanceWorldRow2 : TEXCOORD4,
    float4 instanceWorldRow3 : TEXCOORD5,
    float4 outlineCenterAndWidths : TEXCOORD6,
    float4 outlineColorAndStrength : TEXCOORD7,
    float4 brightnessAndFlags : TEXCOORD8,
    float4 hatchSpacingAndWidth : TEXCOORD9,
    float4 objectiveOverlayColorAndStrength : TEXCOORD10,
    float4 objectiveOverlayWidthsAndHatch : TEXCOORD11,
    float4 objectiveOverlaySpacingAndFlags : TEXCOORD12
)
{
    VSOutput output;
    float4 localPosition = mul(input.Position, MeshLocalTransform);
    float4 worldPosition = float4(
        dot(localPosition, float4(
            instanceWorldRow0.x,
            instanceWorldRow1.x,
            instanceWorldRow2.x,
            instanceWorldRow3.x
        )),
        dot(localPosition, float4(
            instanceWorldRow0.y,
            instanceWorldRow1.y,
            instanceWorldRow2.y,
            instanceWorldRow3.y
        )),
        dot(localPosition, float4(
            instanceWorldRow0.z,
            instanceWorldRow1.z,
            instanceWorldRow2.z,
            instanceWorldRow3.z
        )),
        dot(localPosition, float4(
            instanceWorldRow0.w,
            instanceWorldRow1.w,
            instanceWorldRow2.w,
            instanceWorldRow3.w
        ))
    );
    float4 viewPosition = mul(worldPosition, View);
    float3 localNormal = mul(float4(input.Normal, 0.0f), MeshLocalTransform).xyz;
    float3 worldNormal = float3(
        dot(float4(localNormal, 0.0f), float4(
            instanceWorldRow0.x,
            instanceWorldRow1.x,
            instanceWorldRow2.x,
            instanceWorldRow3.x
        )),
        dot(float4(localNormal, 0.0f), float4(
            instanceWorldRow0.y,
            instanceWorldRow1.y,
            instanceWorldRow2.y,
            instanceWorldRow3.y
        )),
        dot(float4(localNormal, 0.0f), float4(
            instanceWorldRow0.z,
            instanceWorldRow1.z,
            instanceWorldRow2.z,
            instanceWorldRow3.z
        ))
    );

    output.Position = mul(viewPosition, Projection);
    output.WorldPos = worldPosition.xyz;
    output.Normal = normalize(worldNormal);
    output.TexCoord = input.TexCoord;
    output.TexIndex = input.TexCoord1.x;
    output.LightClipPos = mul(worldPosition, LightViewProjection);
    output.OutlineCenterAndWidths = outlineCenterAndWidths;
    output.OutlineColorAndStrength = outlineColorAndStrength;
    output.BrightnessAndFlags = brightnessAndFlags;
    output.HatchSpacingAndWidth = hatchSpacingAndWidth.xy;
    output.ObjectiveOverlayColorAndStrength = objectiveOverlayColorAndStrength;
    output.ObjectiveOverlayWidthsAndHatch = objectiveOverlayWidthsAndHatch;
    output.ObjectiveOverlaySpacingAndFlags = objectiveOverlaySpacingAndFlags;

    return output;
}

float4 PSMain(VSOutput input) : SV_Target0
{
    float layer = SelectTextureLayer(input.TexIndex);
    float3 baseColor = Textures.Sample(TextureSampler, float3(input.TexCoord, layer)).rgb;
    float objectiveOverlayOutlineMask = 0.0f;
    float objectiveOverlayHatchMask = 0.0f;
    float attackMarkerMask = 0.0f;
    if (layer < 1.0f)
    {
        float2 localHexPosition = input.WorldPos.xy - input.OutlineCenterAndWidths.xy;
        float signedDistanceWorld = SignedDistanceToFlatTopHex(
            localHexPosition,
            HexOutlineApothem + 0.02f
        );
        float outlineScale = FaceSdfRangePx / max(HexOutlineApothem, 1e-4f);
        float distancePixels = -signedDistanceWorld * outlineScale;
        float antiAliasPixels = max(fwidth(distancePixels), 0.5f);
        float surfaceUpDot = dot(normalize(input.Normal), float3(0.0f, 0.0f, 1.0f));
        float surfaceBlendWidth = max(FaceOutlineSurfaceUpDotBlendWidth, 1e-4f);
        float surfaceMask = smoothstep(
            FaceOutlineMinSurfaceUpDot,
            FaceOutlineMinSurfaceUpDot + surfaceBlendWidth,
            surfaceUpDot
        );
        float insideMask = smoothstep(-antiAliasPixels, antiAliasPixels, distancePixels);
        float outlineCoreEndPixels = max(input.OutlineCenterAndWidths.z, 0.0f);
        float outlineFadeEndPixels = outlineCoreEndPixels + max(
            input.OutlineCenterAndWidths.w,
            0.0f
        );
        float outlineMask = 1.0f
            - smoothstep(
                outlineCoreEndPixels - antiAliasPixels,
                outlineFadeEndPixels + antiAliasPixels,
                distancePixels
            );
        outlineMask *= insideMask;
        outlineMask *= surfaceMask;
        outlineMask *= saturate(input.OutlineColorAndStrength.w);
        baseColor = lerp(
            baseColor,
            input.OutlineColorAndStrength.xyz,
            saturate(outlineMask)
        );

        float hatchSpacingWorld = max(input.HatchSpacingAndWidth.x, 1e-4f);
        float hatchStrokeWidthWorld = max(input.HatchSpacingAndWidth.y, 0.0f);
        float hatchHalfStrokeWidthWorld = hatchStrokeWidthWorld * 0.5f;
        float hatchCoordinate = dot(localHexPosition, normalize(float2(1.0f, 1.0f)));
        float hatchWrappedDistance = abs(
            frac((hatchCoordinate / hatchSpacingWorld) + 0.5f) - 0.5f
        ) * hatchSpacingWorld;
        float hatchAntiAliasWorld = max(fwidth(hatchCoordinate), 0.0035f);
        float hatchInteriorMask = smoothstep(
            outlineFadeEndPixels + antiAliasPixels,
            outlineFadeEndPixels + (antiAliasPixels * 3.0f),
            distancePixels
        );
        float hatchMask = 1.0f - smoothstep(
            hatchHalfStrokeWidthWorld - hatchAntiAliasWorld,
            hatchHalfStrokeWidthWorld + hatchAntiAliasWorld,
            hatchWrappedDistance
        );
        hatchMask *= insideMask;
        hatchMask *= surfaceMask;
        hatchMask *= hatchInteriorMask;
        hatchMask *= saturate(input.BrightnessAndFlags.y);
        baseColor = lerp(
            baseColor,
            input.OutlineColorAndStrength.xyz,
            saturate(hatchMask)
        );

        if (input.ObjectiveOverlayWidthsAndHatch.w > 0.5f)
        {
            float objectiveOutlineCoreEndPixels = max(input.ObjectiveOverlayWidthsAndHatch.x, 0.0f);
            float objectiveOutlineFadeEndPixels = objectiveOutlineCoreEndPixels
                + max(input.ObjectiveOverlayWidthsAndHatch.y, 0.0f);
            objectiveOverlayOutlineMask = 1.0f - smoothstep(
                objectiveOutlineCoreEndPixels - antiAliasPixels,
                objectiveOutlineFadeEndPixels + antiAliasPixels,
                distancePixels
            );
            objectiveOverlayOutlineMask *= insideMask;
            objectiveOverlayOutlineMask *= surfaceMask;
            objectiveOverlayOutlineMask *= saturate(input.ObjectiveOverlayColorAndStrength.w);

            float objectiveHatchSpacingWorld = max(input.ObjectiveOverlaySpacingAndFlags.x, 1e-4f);
            float objectiveHatchStrokeWidthWorld = max(
                input.ObjectiveOverlaySpacingAndFlags.y,
                0.0f
            );
            float objectiveHatchHalfStrokeWidthWorld = objectiveHatchStrokeWidthWorld * 0.5f;
            float objectiveHatchInteriorMask = smoothstep(
                objectiveOutlineFadeEndPixels + antiAliasPixels,
                objectiveOutlineFadeEndPixels + (antiAliasPixels * 3.0f),
                distancePixels
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
            objectiveOverlayHatchMask *= saturate(input.ObjectiveOverlayWidthsAndHatch.z);
        }

        if (input.ObjectiveOverlaySpacingAndFlags.z > 0.5f)
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
            float horizontalMask = SegmentMask(
                localHexPosition,
                float2(-crossHalfLength, 0.0f),
                float2(crossHalfLength, 0.0f),
                crossHalfWidth,
                antiAliasWorld
            );
            float verticalMask = SegmentMask(
                localHexPosition,
                float2(0.0f, -crossHalfLength),
                float2(0.0f, crossHalfLength),
                crossHalfWidth,
                antiAliasWorld
            );
            float reticleMask = max(horizontalMask, verticalMask);
            reticleMask *= insideMask;
            reticleMask *= surfaceMask;

            float markerMask = max(ringMask * lerp(0.7f, 1.0f, pulse), reticleMask);
            attackMarkerMask = saturate(markerMask);
        }
    }

    float shadow = ComputeShadowPCF(
        input.LightClipPos,
        0.0005f,
        float2(1.0f / 4096.0f, 1.0f / 4096.0f),
        0.25f
    );
    float3 litColor = ComputeLitColor(baseColor, input.Normal, input.WorldPos, shadow);
    litColor *= saturate(input.BrightnessAndFlags.x);
    if (layer < 1.0f)
    {
        litColor = lerp(
            litColor,
            input.ObjectiveOverlayColorAndStrength.xyz,
            saturate(objectiveOverlayOutlineMask)
        );
        litColor = lerp(
            litColor,
            input.ObjectiveOverlayColorAndStrength.xyz,
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
