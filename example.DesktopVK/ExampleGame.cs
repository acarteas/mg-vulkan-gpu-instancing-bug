using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ExampleGame;

public sealed class ExampleGame : Game
{
    private const float HexRadius = 1.0f;
    private const float CircumradiusToApothem = 0.8660254f;
    private const float FaceSdfRangePx = 16.0f;
    private const float FaceOutlineCoreWidthPx = 1.0f;
    private const float FaceInnerGradientWidthPx = 0.0f;
    private const float FaceOutlineStrength = 1.0f;
    private const float FaceBrightness = 1.0f;
    private const float FaceHatchStrength = 0.0f;
    private const float FaceHatchSpacingWorld = 0.3f;
    private const float FaceHatchLineWidthWorld = 0.03f;
    private const float FaceOutlineMinSurfaceUpDot = 0.35f;
    private const float FaceOutlineSurfaceUpDotBlendWidth = 0.2f;
    private const float ObjectiveOverlayCoreWidthPx = 1.1f;
    private const float ObjectiveOverlayInnerGradientWidthPx = 5.0f;
    private const float ObjectiveOverlayStrength = 0.95f;
    private const float ObjectiveOverlayHatchStrength = 0.7f;
    private const float ObjectiveOverlayHatchSpacingWorld = 0.24f;
    private const float ObjectiveOverlayHatchLineWidthWorld = 0.05f;

    private static readonly Matrix TopDownRotation = Matrix.CreateRotationX(MathHelper.PiOver2);
    private static readonly int[] InstanceCountOptions = [1, 7, 49, 196];
    private static readonly Vector3 LightDirection = Vector3.Normalize(
        new Vector3(1.0f, 1.0f, -1.5f)
    );
    private static readonly Vector3 LightColor = Vector3.One;
    private static readonly Vector3 AmbientColor = new Vector3(0.25f, 0.25f, 0.25f);
    private static readonly Vector3 FaceOutlineColor = Vector3.Zero;
    private static readonly Vector3 AttackMarkerColor = new Vector3(1.0f, 0.45f, 0.12f);

    private readonly GraphicsDeviceManager _graphicsDeviceManager;
    private readonly Vector4[] _clipSpheres = new Vector4[16];
    private readonly Vector4[] _clipCapsuleStarts = new Vector4[8];
    private readonly Vector4[] _clipCapsuleEnds = new Vector4[8];
    private readonly Matrix[] _worldMatrices = new Matrix[InstanceCountOptions[^1]];
    private readonly ExampleGameInstanceVertex[] _instanceVertices = new ExampleGameInstanceVertex[
        InstanceCountOptions[^1]
    ];

    private KeyboardState _previousKeyboardState;
    private Model _hexagonModel = null!;
    private Effect _baselineEffect = null!;
    private Effect _instancedEffect = null!;
    private TextureArray _hexTextureArray = null!;
    private Texture2D _shadowFallbackTexture = null!;
    private TerrainSceneRenderer _sceneRenderer = null!;
    private Matrix _view;
    private Matrix _projection;
    private RenderMode _renderMode = RenderMode.Instanced;
    private int _instanceCountIndex = 2;

    private enum RenderMode
    {
        Baseline,
        Instanced,
    }

    public ExampleGame()
    {
        _graphicsDeviceManager = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            SupportedOrientations =
                DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight,
        };
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
        _graphicsDeviceManager.PreferredBackBufferWidth = 1600;
        _graphicsDeviceManager.PreferredBackBufferHeight = 900;
    }

    protected override void Initialize()
    {
        base.Initialize();
        RefreshProjection();
        BuildWorldMatrices();
        BuildInstanceVertices();
        UpdateWindowTitle();
    }

    protected override void LoadContent()
    {
        _hexagonModel = Content.Load<Model>("Models/hexagon");
        _baselineEffect = Content.Load<Effect>("Shaders/Terrain/HexTerrainTextureArrayLighting");
        _instancedEffect = Content.Load<Effect>(
            "Shaders/Terrain/HexTerrainTextureArrayLightingInstanced"
        );
        _hexTextureArray = CreateHexTextureArray();
        _shadowFallbackTexture = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Single);
        _shadowFallbackTexture.SetData([1.0f]);
        _sceneRenderer = new TerrainSceneRenderer(GraphicsDevice, _hexagonModel);

        LogStartupDetails();
    }

    protected override void UnloadContent()
    {
        _sceneRenderer?.Dispose();
        _hexTextureArray?.Dispose();
        _shadowFallbackTexture?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboardState = Keyboard.GetState();

        if (IsNewKeyPress(keyboardState, Keys.Escape))
        {
            Exit();
        }

        if (IsNewKeyPress(keyboardState, Keys.Space))
        {
            _renderMode = _renderMode == RenderMode.Instanced
                ? RenderMode.Baseline
                : RenderMode.Instanced;
            UpdateWindowTitle();
        }

        if (IsNewKeyPress(keyboardState, Keys.Up))
        {
            _instanceCountIndex = Math.Min(_instanceCountIndex + 1, InstanceCountOptions.Length - 1);
            UpdateWindowTitle();
        }

        if (IsNewKeyPress(keyboardState, Keys.Down))
        {
            _instanceCountIndex = Math.Max(_instanceCountIndex - 1, 0);
            UpdateWindowTitle();
        }

        _previousKeyboardState = keyboardState;
        RefreshProjection();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1f, 0);
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
        GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

        int instanceCount = InstanceCountOptions[_instanceCountIndex];
        ConfigureBaselineEffect();
        ConfigureInstancedEffect(gameTime);

        if (_renderMode == RenderMode.Baseline)
        {
            _sceneRenderer.RenderBaseline(
                _baselineEffect,
                _worldMatrices,
                instanceCount,
                TopDownRotation
            );
        }
        else
        {
            _sceneRenderer.RenderInstanced(
                _instancedEffect,
                _instanceVertices,
                instanceCount,
                TopDownRotation
            );
        }

        base.Draw(gameTime);
    }

    private void RefreshProjection()
    {
        int backBufferWidth = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferWidth);
        int backBufferHeight = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferHeight);
        float aspectRatio = backBufferWidth / (float)backBufferHeight;
        _projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.PiOver4,
            aspectRatio,
            0.1f,
            300f
        );
        _view = Matrix.CreateLookAt(
            new Vector3(0f, -18f, 14f),
            new Vector3(0f, 0f, 0f),
            Vector3.UnitZ
        );
    }

    private void ConfigureBaselineEffect()
    {
        ConfigureSharedSceneEffectParameters(_baselineEffect, 0.0f);
        _baselineEffect.Parameters["Textures"]?.SetValue(_hexTextureArray);
        _baselineEffect
            .Parameters["HexOutlineApothem"]
            ?.SetValue(HexRadius * CircumradiusToApothem);
        _baselineEffect.Parameters["FaceSdfRangePx"]?.SetValue(FaceSdfRangePx);
        _baselineEffect.Parameters["FaceOutlineCoreWidthPx"]?.SetValue(FaceOutlineCoreWidthPx);
        _baselineEffect
            .Parameters["FaceInnerGradientWidthPx"]
            ?.SetValue(FaceInnerGradientWidthPx);
        _baselineEffect.Parameters["FaceOutlineStrength"]?.SetValue(FaceOutlineStrength);
        _baselineEffect.Parameters["FaceOutlineColor"]?.SetValue(FaceOutlineColor);
        _baselineEffect.Parameters["FaceBrightness"]?.SetValue(FaceBrightness);
        _baselineEffect.Parameters["FaceHatchStrength"]?.SetValue(FaceHatchStrength);
        _baselineEffect.Parameters["FaceHatchSpacingWorld"]?.SetValue(FaceHatchSpacingWorld);
        _baselineEffect.Parameters["FaceHatchLineWidthWorld"]?.SetValue(FaceHatchLineWidthWorld);
        _baselineEffect
            .Parameters["FaceOutlineMinSurfaceUpDot"]
            ?.SetValue(FaceOutlineMinSurfaceUpDot);
        _baselineEffect
            .Parameters["FaceOutlineSurfaceUpDotBlendWidth"]
            ?.SetValue(FaceOutlineSurfaceUpDotBlendWidth);
        _baselineEffect.Parameters["ObjectiveOverlayEnabled"]?.SetValue(false);
        _baselineEffect.Parameters["ObjectiveOverlayColor"]?.SetValue(Vector3.Zero);
        _baselineEffect
            .Parameters["ObjectiveOverlayCoreWidthPx"]
            ?.SetValue(ObjectiveOverlayCoreWidthPx);
        _baselineEffect
            .Parameters["ObjectiveOverlayInnerGradientWidthPx"]
            ?.SetValue(ObjectiveOverlayInnerGradientWidthPx);
        _baselineEffect
            .Parameters["ObjectiveOverlayStrength"]
            ?.SetValue(ObjectiveOverlayStrength);
        _baselineEffect
            .Parameters["ObjectiveOverlayHatchStrength"]
            ?.SetValue(ObjectiveOverlayHatchStrength);
        _baselineEffect
            .Parameters["ObjectiveOverlayHatchSpacingWorld"]
            ?.SetValue(ObjectiveOverlayHatchSpacingWorld);
        _baselineEffect
            .Parameters["ObjectiveOverlayHatchLineWidthWorld"]
            ?.SetValue(ObjectiveOverlayHatchLineWidthWorld);
        _baselineEffect.Parameters["AttackMarkerEnabled"]?.SetValue(false);
        _baselineEffect.Parameters["AttackMarkerColor"]?.SetValue(AttackMarkerColor);
    }

    private void ConfigureInstancedEffect(GameTime gameTime)
    {
        ConfigureSharedSceneEffectParameters(_instancedEffect, (float)gameTime.TotalGameTime.TotalSeconds);
        _instancedEffect.Parameters["Textures"]?.SetValue(_hexTextureArray);
        _instancedEffect
            .Parameters["HexOutlineApothem"]
            ?.SetValue(HexRadius * CircumradiusToApothem);
        _instancedEffect.Parameters["FaceSdfRangePx"]?.SetValue(FaceSdfRangePx);
        _instancedEffect
            .Parameters["FaceOutlineMinSurfaceUpDot"]
            ?.SetValue(FaceOutlineMinSurfaceUpDot);
        _instancedEffect
            .Parameters["FaceOutlineSurfaceUpDotBlendWidth"]
            ?.SetValue(FaceOutlineSurfaceUpDotBlendWidth);
        _instancedEffect.Parameters["AttackMarkerColor"]?.SetValue(AttackMarkerColor);
    }

    private void ConfigureSharedSceneEffectParameters(Effect effect, float attackMarkerTime)
    {
        effect.Parameters["Projection"]?.SetValue(_projection);
        effect.Parameters["View"]?.SetValue(_view);
        effect.Parameters["LightDirection"]?.SetValue(LightDirection);
        effect.Parameters["LightColor"]?.SetValue(LightColor);
        effect.Parameters["AmbientColor"]?.SetValue(AmbientColor);
        effect.Parameters["SpecularPower"]?.SetValue(16.0f);
        effect.Parameters["SpecularIntensity"]?.SetValue(0.1f);
        effect.Parameters["CameraPosition"]?.SetValue(new Vector3(0f, -18f, 14f));
        effect.Parameters["ShadowsEnabled"]?.SetValue(false);
        effect.Parameters["ShadowMap"]?.SetValue(_shadowFallbackTexture);
        effect.Parameters["LightViewProjection"]?.SetValue(Matrix.Identity);
        effect.Parameters["AttackMarkerTime"]?.SetValue(attackMarkerTime);
        effect.Parameters["ClipSphereCount"]?.SetValue(0);
        effect.Parameters["ClipSpheres"]?.SetValue(_clipSpheres);
        effect.Parameters["ClipCapsuleCount"]?.SetValue(0);
        effect.Parameters["ClipCapsuleStarts"]?.SetValue(_clipCapsuleStarts);
        effect.Parameters["ClipCapsuleEnds"]?.SetValue(_clipCapsuleEnds);
    }

    private TextureArray CreateHexTextureArray()
    {
        Texture2D faceTexture = Content.Load<Texture2D>("Textures/Grass");
        Texture2D sideTopTexture = Content.Load<Texture2D>("Textures/GrassDirtEdge");
        Texture2D sideTexture = Content.Load<Texture2D>("Textures/Dirt");
        var textureArray = new TextureArray(
            GraphicsDevice,
            faceTexture.Width,
            faceTexture.Height,
            3
        );
        textureArray.Add(0, faceTexture);
        textureArray.Add(1, sideTopTexture);
        textureArray.Add(2, sideTexture);
        return textureArray;
    }

    private void BuildWorldMatrices()
    {
        var worldPositions = new Vector3[_worldMatrices.Length];
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int index = 0; index < worldPositions.Length; index++)
        {
            int col = index % 14;
            int row = index / 14;
            int q = col;
            int r = row - ((col - (col & 1)) / 2);
            Vector2 hexPosition = AxialToPixelFlatTop(q, r, HexRadius);
            worldPositions[index] = new Vector3(hexPosition, 0f);

            minX = Math.Min(minX, hexPosition.X);
            minY = Math.Min(minY, hexPosition.Y);
            maxX = Math.Max(maxX, hexPosition.X);
            maxY = Math.Max(maxY, hexPosition.Y);
        }

        var centerOffset = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
        for (int index = 0; index < _worldMatrices.Length; index++)
        {
            _worldMatrices[index] = Matrix.CreateTranslation(worldPositions[index] - centerOffset);
        }
    }

    private void BuildInstanceVertices()
    {
        for (int index = 0; index < _instanceVertices.Length; index++)
        {
            Vector3 translation = _worldMatrices[index].Translation;
            _instanceVertices[index] = new ExampleGameInstanceVertex(
                _worldMatrices[index],
                new Vector2(translation.X, translation.Y),
                FaceOutlineCoreWidthPx,
                FaceInnerGradientWidthPx,
                FaceOutlineColor,
                FaceOutlineStrength,
                FaceBrightness,
                FaceHatchStrength,
                FaceHatchSpacingWorld,
                FaceHatchLineWidthWorld,
                Vector3.Zero,
                ObjectiveOverlayCoreWidthPx,
                ObjectiveOverlayInnerGradientWidthPx,
                ObjectiveOverlayStrength,
                ObjectiveOverlayHatchStrength,
                ObjectiveOverlayHatchSpacingWorld,
                ObjectiveOverlayHatchLineWidthWorld,
                objectiveOverlayEnabled: false,
                attackMarkerEnabled: false
            );
        }
    }

    private void LogStartupDetails()
    {
        Assembly monoGameAssembly = typeof(Game).Assembly;
        string monoGameVersion = monoGameAssembly.GetName().Version?.ToString() ?? "unknown";
        Console.WriteLine($"POC OS: {Environment.OSVersion}");
        Console.WriteLine($"MonoGame assembly: {monoGameAssembly.FullName}");
        Console.WriteLine($"MonoGame version: {monoGameVersion}");
        Console.WriteLine($"Graphics profile: {GraphicsDevice.GraphicsProfile}");
        Console.WriteLine($"Adapter: {GraphicsAdapter.DefaultAdapter.Description}");
        Console.WriteLine($"Mesh parts: {_sceneRenderer.MeshPartCount}");
        Console.WriteLine($"Content root: {Content.RootDirectory}");
        Console.WriteLine($"Working directory: {Environment.CurrentDirectory}");
    }

    private void UpdateWindowTitle()
    {
        int instanceCount = InstanceCountOptions[_instanceCountIndex];
        Window.Title =
            $"Hex Instancing POC | DesktopVK | {_renderMode} | Count {instanceCount} | {GraphicsDevice?.GraphicsProfile}";
    }

    private bool IsNewKeyPress(KeyboardState keyboardState, Keys key)
    {
        return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }

    private static Vector2 AxialToPixelFlatTop(int q, int r, float size)
    {
        float x = 1.5f * size * q;
        float y = size * MathF.Sqrt(3.0f) * (r + (q / 2.0f));
        return new Vector2(x, y);
    }
}
