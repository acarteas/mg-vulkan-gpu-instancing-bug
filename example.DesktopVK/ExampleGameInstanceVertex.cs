using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleGame;

[StructLayout(LayoutKind.Sequential)]
public struct ExampleGameInstanceVertex : IVertexType
{
    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
        new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6),
        new VertexElement(80, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 7),
        new VertexElement(96, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 8),
        new VertexElement(
            112,
            VertexElementFormat.Vector4,
            VertexElementUsage.TextureCoordinate,
            9
        ),
        new VertexElement(
            128,
            VertexElementFormat.Vector4,
            VertexElementUsage.TextureCoordinate,
            10
        ),
        new VertexElement(
            144,
            VertexElementFormat.Vector4,
            VertexElementUsage.TextureCoordinate,
            11
        ),
        new VertexElement(
            160,
            VertexElementFormat.Vector4,
            VertexElementUsage.TextureCoordinate,
            12
        )
    );

    public Vector4 WorldRow0;
    public Vector4 WorldRow1;
    public Vector4 WorldRow2;
    public Vector4 WorldRow3;
    public Vector4 OutlineCenterAndWidths;
    public Vector4 OutlineColorAndStrength;
    public Vector4 BrightnessAndFlags;
    public Vector4 HatchSpacingAndWidth;
    public Vector4 ObjectiveOverlayColorAndStrength;
    public Vector4 ObjectiveOverlayWidthsAndHatch;
    public Vector4 ObjectiveOverlaySpacingAndFlags;

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public ExampleGameInstanceVertex(
        Matrix worldMatrix,
        Vector2 outlineCenter,
        float outlineCoreWidthPx,
        float innerGradientWidthPx,
        Vector3 outlineColor,
        float outlineStrength,
        float brightness,
        float hatchStrength,
        float hatchSpacingWorld,
        float hatchLineWidthWorld,
        Vector3 objectiveOverlayColor,
        float objectiveOverlayCoreWidthPx,
        float objectiveOverlayInnerGradientWidthPx,
        float objectiveOverlayStrength,
        float objectiveOverlayHatchStrength,
        float objectiveOverlayHatchSpacingWorld,
        float objectiveOverlayHatchLineWidthWorld,
        bool objectiveOverlayEnabled,
        bool attackMarkerEnabled
    )
    {
        WorldRow0 = new Vector4(worldMatrix.M11, worldMatrix.M12, worldMatrix.M13, worldMatrix.M14);
        WorldRow1 = new Vector4(worldMatrix.M21, worldMatrix.M22, worldMatrix.M23, worldMatrix.M24);
        WorldRow2 = new Vector4(worldMatrix.M31, worldMatrix.M32, worldMatrix.M33, worldMatrix.M34);
        WorldRow3 = new Vector4(worldMatrix.M41, worldMatrix.M42, worldMatrix.M43, worldMatrix.M44);
        OutlineCenterAndWidths = new Vector4(
            outlineCenter.X,
            outlineCenter.Y,
            outlineCoreWidthPx,
            innerGradientWidthPx
        );
        OutlineColorAndStrength = new Vector4(
            outlineColor.X,
            outlineColor.Y,
            outlineColor.Z,
            outlineStrength
        );
        BrightnessAndFlags = new Vector4(brightness, hatchStrength, 0f, 0f);
        HatchSpacingAndWidth = new Vector4(hatchSpacingWorld, hatchLineWidthWorld, 0f, 0f);
        ObjectiveOverlayColorAndStrength = new Vector4(
            objectiveOverlayColor.X,
            objectiveOverlayColor.Y,
            objectiveOverlayColor.Z,
            objectiveOverlayStrength
        );
        ObjectiveOverlayWidthsAndHatch = new Vector4(
            objectiveOverlayCoreWidthPx,
            objectiveOverlayInnerGradientWidthPx,
            objectiveOverlayHatchStrength,
            objectiveOverlayEnabled ? 1.0f : 0.0f
        );
        ObjectiveOverlaySpacingAndFlags = new Vector4(
            objectiveOverlayHatchSpacingWorld,
            objectiveOverlayHatchLineWidthWorld,
            attackMarkerEnabled ? 1.0f : 0.0f,
            0f
        );
    }
}
