using System;
using MoonWorks;
using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;
using System.Runtime.InteropServices;

namespace MoonWorksGraphicsTests;

class HelloTexturedCubeExample : Example
{
	private GraphicsPipeline pipeline;
	private Buffer vertexBuffer;
	private Buffer indexBuffer;
	private Sampler[] samplers = new Sampler[6];
	private string[] samplerNames =
    [
        "PointClamp",
		"PointWrap",
		"LinearClamp",
		"LinearWrap",
		"AnisotropicClamp",
		"AnisotropicWrap"
	];

	private int currentSamplerIndex;

	private Texture[] textures = new Texture[2];

	private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
	
	private float cubeTimer;
	private Quaternion cubeRotation;
	private Quaternion previousCubeRotation;
	private Vector3 camPos;

    public override unsafe void Init()
	{
		Window.SetTitle("TexturedCube");

		cubeTimer = 0;
		cubeRotation = Quaternion.Identity;
		previousCubeRotation = Quaternion.Identity;
		camPos = new Vector3(0, 1.5f, 4);

		Logger.LogInfo("Press Left and Right to cycle between sampler states");
		Logger.LogInfo("Setting sampler state to: " + samplerNames[0]);

		// Load the shaders
		Shader vertShader = ShaderCross.Create(
			GraphicsDevice,
			RootTitleStorage,
			TestUtils.GetHLSLPath("PositionSamplerWithMatrix.vert"),
			"main",
			ShaderCross.ShaderFormat.HLSL,
			ShaderStage.Vertex
		);

		Shader fragShader = ShaderCross.Create(
			GraphicsDevice,
			RootTitleStorage,
			TestUtils.GetHLSLPath("TexturedQuadMultiSamplers.frag"),
			"main",
			ShaderCross.ShaderFormat.HLSL,
			ShaderStage.Fragment
		);

		// Create the graphics pipeline
		GraphicsPipelineCreateInfo pipelineCreateInfo = new GraphicsPipelineCreateInfo
		{
			TargetInfo = new GraphicsPipelineTargetInfo
			{
				ColorTargetDescriptions = [
					new ColorTargetDescription
					{
						Format = Window.SwapchainFormat,
						BlendState = ColorTargetBlendState.Opaque
					}
				],
				HasDepthStencilTarget = true,
				DepthStencilFormat = GraphicsDevice.SupportedDepthFormat
			},
			DepthStencilState = new DepthStencilState
			{
				EnableDepthTest = true,
				EnableDepthWrite = true,
				CompareOp = CompareOp.LessOrEqual
			},
			VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>(),
			PrimitiveType = PrimitiveType.TriangleList,
			RasterizerState = RasterizerState.CW_CullBack,
			MultisampleState = MultisampleState.None,
			VertexShader = vertShader,
			FragmentShader = fragShader
		};

		pipeline = GraphicsPipeline.Create(GraphicsDevice, pipelineCreateInfo);

		// Create samplers
		samplers[0] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.PointClamp);
		samplers[1] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.PointWrap);
		samplers[2] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.LinearClamp);
		samplers[3] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.LinearWrap);
		samplers[4] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.AnisotropicClamp);
		samplers[5] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.AnisotropicWrap);

		const float TextureInvScale = 1f;

		ReadOnlySpan<PositionTextureVertex> vertexData = [

			// 
			new PositionTextureVertex(new Vector3(-1, -1, -1), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3(1, -1, -1), new Vector2(TextureInvScale, 0)),
			new PositionTextureVertex(new Vector3(1, 1, -1), new Vector2(TextureInvScale, TextureInvScale)),
			new PositionTextureVertex(new Vector3(-1, 1, -1), new Vector2(0, TextureInvScale)),

			new PositionTextureVertex(new Vector3(-1, -1, 1), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3(1, -1, 1), new Vector2(TextureInvScale, 0)),
			new PositionTextureVertex(new Vector3(1, 1, 1), new Vector2(TextureInvScale, TextureInvScale)),
			new PositionTextureVertex(new Vector3(-1, 1, 1), new Vector2(0, TextureInvScale)),

			new PositionTextureVertex(new Vector3(-1, -1, -1), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3(-1, 1, -1), new Vector2(TextureInvScale, 0)),
			new PositionTextureVertex(new Vector3(-1, 1, 1), new Vector2(TextureInvScale, TextureInvScale)),
			new PositionTextureVertex(new Vector3(-1, -1, 1), new Vector2(0, TextureInvScale)),

			new PositionTextureVertex(new Vector3(1, -1, -1), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3(1, 1, -1), new Vector2(TextureInvScale, 0)),
			new PositionTextureVertex(new Vector3(1, 1, 1), new Vector2(TextureInvScale, TextureInvScale)),
			new PositionTextureVertex(new Vector3(1, -1, 1), new Vector2(0, TextureInvScale)),

			new PositionTextureVertex(new Vector3(-1, -1, -1), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3(-1, -1, 1), new Vector2(TextureInvScale, 0)),
			new PositionTextureVertex(new Vector3(1, -1, 1), new Vector2(TextureInvScale, TextureInvScale)),
			new PositionTextureVertex(new Vector3(1, -1, -1), new Vector2(0, TextureInvScale)),

			new PositionTextureVertex(new Vector3(-1, 1, -1), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3(-1, 1, 1), new Vector2(TextureInvScale, 0)),
			new PositionTextureVertex(new Vector3(1, 1, 1), new Vector2(TextureInvScale, TextureInvScale)),
			new PositionTextureVertex(new Vector3(1, 1, -1), new Vector2(0, TextureInvScale))
		];

		ReadOnlySpan<ushort> indexData = [
			0,  1,  2,  0,  2,  3,
			6,  5,  4,  7,  6,  4,
			8,  9, 10,  8, 10, 11,
			14, 13, 12, 15, 14, 12,
			16, 17, 18, 16, 18, 19,
			22, 21, 20, 23, 22, 20
		];

		// Create and populate the GPU resources

		var resourceUploader = new ResourceUploader(GraphicsDevice);

		vertexBuffer = resourceUploader.CreateBuffer(vertexData, BufferUsageFlags.Vertex);
		indexBuffer = resourceUploader.CreateBuffer(indexData, BufferUsageFlags.Index);

		textures[0] = resourceUploader.CreateTexture2DFromCompressed(RootTitleStorage, TestUtils.GetTexturePath("jacoggers.png"),
			TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);
		textures[1] = resourceUploader.CreateTexture2DFromCompressed(RootTitleStorage, TestUtils.GetTexturePath("container.png"),
			TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);

		resourceUploader.Upload();
		resourceUploader.Dispose();
	}

	public override void Update(System.TimeSpan delta)
	{
		int prevSamplerIndex = currentSamplerIndex;

		if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Left))
		{
			currentSamplerIndex -= 1;
			if (currentSamplerIndex < 0)
			{
				currentSamplerIndex = samplers.Length - 1;
			}
		}

		if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Right))
		{
			currentSamplerIndex += 1;
			if (currentSamplerIndex >= samplers.Length)
			{
				currentSamplerIndex = 0;
			}
		}

		if (prevSamplerIndex != currentSamplerIndex)
		{
			Logger.LogInfo("Setting sampler state to: " + samplerNames[currentSamplerIndex]);
		}

		// Rotate the cube
		cubeTimer += (float) delta.TotalSeconds;
		previousCubeRotation = cubeRotation;
		cubeRotation = Quaternion.CreateFromYawPitchRoll(cubeTimer * 2f, 0, cubeTimer * 2f);
	}

	public override void Draw(double alpha)
	{
		var cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(Window);
		if (swapchainTexture != null)
		{
			// Set up cube model-view-projection matrix
			Matrix4x4 viewToClipSpace = Matrix4x4.CreatePerspectiveFieldOfView(
				float.DegreesToRadians(75f),
				(float) Window.Width / Window.Height,
				0.01f,
				100f
			);

			Matrix4x4 worldToView = Matrix4x4.CreateLookAt(camPos, Vector3.Zero, Vector3.UnitY);

			Matrix4x4 modelToWorld = Matrix4x4.CreateFromQuaternion(
				Quaternion.Slerp(
					previousCubeRotation,
					cubeRotation,
					(float) alpha
				)
			);
			TransformVertexUniform cubeUniforms = new TransformVertexUniform(modelToWorld * worldToView * viewToClipSpace);

			var renderPass = cmdbuf.BeginRenderPass(
				new ColorTargetInfo(swapchainTexture, Color.Black)
			);
			renderPass.BindGraphicsPipeline(pipeline);

			cmdbuf.PushVertexUniformData(cubeUniforms);

			Span<TextureSamplerBinding> samplerBindings = [
				new TextureSamplerBinding(textures[0], samplers[currentSamplerIndex]),
				new TextureSamplerBinding(textures[1], samplers[currentSamplerIndex])
			];
			renderPass.BindFragmentSamplers(samplerBindings);

			renderPass.BindVertexBuffers(vertexBuffer);
			renderPass.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
			renderPass.DrawIndexedPrimitives(36, 1, 0, 0, 0);

			cmdbuf.EndRenderPass(renderPass);
		}
		GraphicsDevice.Submit(cmdbuf);
	}

    public override void Destroy()
    {
        pipeline.Dispose();
		vertexBuffer.Dispose();
		indexBuffer.Dispose();

		for (var i = 0; i < samplers.Length; i += 1)
		{
			samplers[i].Dispose();
		}

		for (var i = 0; i < textures.Length; i += 1)
		{
			textures[i].Dispose();
		}
    }
}
