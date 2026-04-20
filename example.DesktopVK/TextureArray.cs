using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleGame;

public sealed class TextureArray : Texture2D
{
    public int ArraySize { get; }

    public TextureArray(GraphicsDevice graphicsDevice, int width, int height, int arraySize)
        : base(
            graphicsDevice,
            width,
            height,
            false,
            SurfaceFormat.Color,
            SurfaceType.Texture,
            false,
            arraySize
        )
    {
        ArraySize = arraySize;
    }

    public void Add(int index, Texture2D texture)
    {
        if (index < 0 || index >= ArraySize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                "Texture array index out of range."
            );
        }

        if (texture == null)
        {
            throw new ArgumentNullException(nameof(texture));
        }

        if (texture.Width != Width || texture.Height != Height)
        {
            throw new InvalidOperationException(
                $"Texture size {texture.Width}x{texture.Height} does not match array size {Width}x{Height}."
            );
        }

        if (texture.Format != SurfaceFormat.Color || Format != SurfaceFormat.Color)
        {
            throw new InvalidOperationException(
                $"Texture format mismatch: texture={texture.Format}, array={Format}. Expected SurfaceFormat.Color."
            );
        }

        if (texture.LevelCount != LevelCount)
        {
            Debug.WriteLine(
                $"[TextureArray] LevelCount mismatch: texture={texture.LevelCount}, array={LevelCount}. Using min."
            );
        }

        int levels = Math.Min(texture.LevelCount, LevelCount);
        for (int mipLevel = 0; mipLevel < levels; mipLevel++)
        {
            float divisor = 1.0f / (1 << mipLevel);
            int mipWidth = (int)(texture.Width * divisor);
            int mipHeight = (int)(texture.Height * divisor);
            var pixelData = new Color[mipWidth * mipHeight];

            texture.GetData(
                mipLevel,
                0,
                new Rectangle(0, 0, mipWidth, mipHeight),
                pixelData,
                0,
                pixelData.Length
            );

            SetData(
                mipLevel,
                index,
                new Rectangle(0, 0, mipWidth, mipHeight),
                pixelData,
                0,
                pixelData.Length
            );
        }
    }
}
