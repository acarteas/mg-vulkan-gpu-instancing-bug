#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ExampleGame;

internal sealed class TerrainSceneRenderer : IDisposable
{
    private readonly record struct TerrainMeshPartDraw(
        VertexBuffer VertexBuffer,
        IndexBuffer IndexBuffer,
        int VertexOffset,
        int StartIndex,
        int PrimitiveCount,
        Matrix LocalTransform
    );

    private readonly GraphicsDevice _graphicsDevice;
    private readonly TerrainMeshPartDraw[] _meshParts;
    private DynamicVertexBuffer? _instanceBuffer;

    public int MeshPartCount => _meshParts.Length;

    public TerrainSceneRenderer(GraphicsDevice graphicsDevice, Model model)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _meshParts = CreateMeshParts(model ?? throw new ArgumentNullException(nameof(model)));
    }

    public void Dispose()
    {
        _instanceBuffer?.Dispose();
    }

    public void RenderBaseline(
        Effect effect,
        Matrix[] worldMatrices,
        int instanceCount,
        Matrix topDownRotation
    )
    {
        if (instanceCount <= 0)
        {
            return;
        }

        for (int instanceIndex = 0; instanceIndex < instanceCount; instanceIndex++)
        {
            Matrix worldMatrix = worldMatrices[instanceIndex];
            var worldTranslation = worldMatrix.Translation;
            effect.Parameters["HexOutlineCenter"]?.SetValue(
                new Vector2(worldTranslation.X, worldTranslation.Y)
            );

            for (int meshPartIndex = 0; meshPartIndex < _meshParts.Length; meshPartIndex++)
            {
                TerrainMeshPartDraw meshPart = _meshParts[meshPartIndex];
                effect
                    .Parameters["World"]
                    ?.SetValue(meshPart.LocalTransform * topDownRotation * worldMatrix);
                _graphicsDevice.SetVertexBuffer(meshPart.VertexBuffer);
                _graphicsDevice.Indices = meshPart.IndexBuffer;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        meshPart.VertexOffset,
                        meshPart.StartIndex,
                        meshPart.PrimitiveCount
                    );
                }
            }
        }
    }

    public void RenderInstanced(
        Effect effect,
        ExampleGameInstanceVertex[] instances,
        int instanceCount,
        Matrix topDownRotation
    )
    {
        if (instanceCount <= 0)
        {
            return;
        }

        EnsureInstanceBuffer(instanceCount);
        _instanceBuffer!.SetData(instances, 0, instanceCount, SetDataOptions.Discard);

        for (int meshPartIndex = 0; meshPartIndex < _meshParts.Length; meshPartIndex++)
        {
            TerrainMeshPartDraw meshPart = _meshParts[meshPartIndex];
            effect
                .Parameters["MeshLocalTransform"]
                ?.SetValue(meshPart.LocalTransform * topDownRotation);
            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(meshPart.VertexBuffer, 0, 0),
                new VertexBufferBinding(_instanceBuffer, 0, 1)
            );
            _graphicsDevice.Indices = meshPart.IndexBuffer;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphicsDevice.DrawInstancedPrimitives(
                    PrimitiveType.TriangleList,
                    meshPart.VertexOffset,
                    meshPart.StartIndex,
                    meshPart.PrimitiveCount,
                    instanceCount
                );
            }
        }
    }

    private void EnsureInstanceBuffer(int requiredCapacity)
    {
        if (_instanceBuffer != null && _instanceBuffer.VertexCount >= requiredCapacity)
        {
            return;
        }

        _instanceBuffer?.Dispose();
        int capacity = Math.Max(1, requiredCapacity);
        _instanceBuffer = new DynamicVertexBuffer(
            _graphicsDevice,
            ExampleGameInstanceVertex.VertexDeclaration,
            capacity,
            BufferUsage.WriteOnly
        );
    }

    private static TerrainMeshPartDraw[] CreateMeshParts(Model model)
    {
        var meshParts = new List<TerrainMeshPartDraw>();
        foreach (ModelMesh mesh in model.Meshes)
        {
            Matrix localTransform = mesh.ParentBone.Transform;
            foreach (ModelMeshPart part in mesh.MeshParts)
            {
                meshParts.Add(
                    new TerrainMeshPartDraw(
                        part.VertexBuffer,
                        part.IndexBuffer,
                        part.VertexOffset,
                        part.StartIndex,
                        part.PrimitiveCount,
                        localTransform
                    )
                );
            }
        }

        return meshParts.ToArray();
    }
}
