using System;
using MoonWorks;
using MoonWorks.Graphics;
using System.Numerics;
using Buffer = MoonWorks.Graphics.Buffer;
using System.Runtime.InteropServices;
using System.Collections.Generic;

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

	private Texture[] textures = new Texture[3];
	private Texture DepthRT;

	private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
	
	private float cubeTimer;
	private Quaternion[] cubeRotations = new Quaternion[2];
	private Quaternion[] previousCubeRotations = new Quaternion[2];
	private Vector3 camPos;
	private List<Vector3> cubePositions;

    public override unsafe void Init()
	{
		Window.SetTitle("TexturedCube");

		cubeTimer = 0;
		cubeRotations.AsSpan().Fill(Quaternion.Identity);
		previousCubeRotations.AsSpan().Fill(Quaternion.Identity);
		camPos = new Vector3(0f, 0f, 6f);

		cubePositions = new List<Vector3>() {
			new Vector3(0, 0, 0),
			new Vector3(0, 0, 3) // currently unused
		};

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
		textures[2] = resourceUploader.CreateTexture2DFromCompressed(RootTitleStorage, TestUtils.GetTexturePath("hein.png"),
			TextureFormat.R8G8B8A8Unorm, TextureUsageFlags.Sampler);

		DepthRT = Texture.Create2D(
			GraphicsDevice,
			Window.Width,
			Window.Height,
			GraphicsDevice.SupportedDepthFormat,
			TextureUsageFlags.DepthStencilTarget
		);

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

		cubeTimer += (float)delta.TotalSeconds;

		// Rotate the first cube
		previousCubeRotations[0] = cubeRotations[0];
		cubeRotations[0] = Quaternion.CreateFromYawPitchRoll(cubeTimer * 2f, 0, cubeTimer * 2f);
		
		// Rotate the 2nd cube
		previousCubeRotations[1] = cubeRotations[1];
		cubeRotations[1] = Quaternion.CreateFromYawPitchRoll(cubeTimer, 0, 0);
	}

	public override void Draw(double alpha)
	{
		var cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(Window);
		if (swapchainTexture != null)
		{
			// Set up cube model-view-projection matrix
			
			// NOTE: The perspective matrix will flip the handedness (Z-axis) when it converts to NDC (clip space), 
			// ..so worldToView etc. being right-handed is correct.
			Matrix4x4 viewToClipSpace = Matrix4x4.CreatePerspectiveFieldOfView(
				float.DegreesToRadians(75f),
				(float) Window.Width / Window.Height,
				0.01f,
				100f
			);

			Matrix4x4 worldToView = Matrix4x4.CreateLookAt(camPos, cubePositions[0], Vector3.UnitY);

			/*
			Span<Matrix4x4> models = new Matrix4x4[cubePositions.Count];
			models.Fill(Matrix4x4.Identity);
			*/

			Matrix4x4 worldToClipSpace = worldToView * viewToClipSpace;

			var renderPass = cmdbuf.BeginRenderPass(
				new DepthStencilTargetInfo(DepthRT, 1, true),
				new ColorTargetInfo(swapchainTexture, Color.Black)
			);

			/*
			var renderPass = cmdbuf.BeginRenderPass(
				new ColorTargetInfo(swapchainTexture, Color.Black)
			);*/
			renderPass.BindGraphicsPipeline(pipeline);

			Span<TextureSamplerBinding> samplerBindings = [
				new TextureSamplerBinding(textures[0], samplers[currentSamplerIndex]),
				new TextureSamplerBinding(textures[1], samplers[currentSamplerIndex])
			];
			renderPass.BindFragmentSamplers(samplerBindings);

			renderPass.BindVertexBuffers(vertexBuffer);
			renderPass.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);

			{
				for (int i = 0; i < cubePositions.Count; ++i)
				{
					var cubePos = cubePositions[i];

					Matrix4x4 modelToWorld = Matrix4x4.CreateTranslation(cubePos);

					if (i == 0)
					{
						// Do "Scale * Rotation * Translation" (backwards order compared to OpenGL)
						modelToWorld *= Matrix4x4.CreateFromQuaternion(
							Quaternion.Slerp(
								previousCubeRotations[i],
								cubeRotations[i],
								(float)alpha
							)
						);
					}
					else if (i == 1)
					{
						modelToWorld = Matrix4x4.CreateTranslation(cubePositions[0]); // our pivot
						var scale = Matrix4x4.CreateScale(0.5f);
						var rotate = Matrix4x4.CreateFromQuaternion(
							Quaternion.Slerp(
								previousCubeRotations[i],
								cubeRotations[i],
								(float)alpha
							)
						);

						// Orbit the 2nd cube around the first!
						var distance = 3f;
						var angle = cubeTimer % (MathF.PI * 2);
						var orbitMovement = Matrix4x4.CreateTranslation(
							new Vector3(MathF.Sin(angle) * distance,
								0,
								MathF.Cos(angle) * distance
							)
						);

						modelToWorld = modelToWorld * scale * rotate * orbitMovement;

						samplerBindings = [
							new TextureSamplerBinding(textures[2], samplers[currentSamplerIndex]),
							new TextureSamplerBinding(textures[1], samplers[currentSamplerIndex])
						];
						renderPass.BindFragmentSamplers(samplerBindings);
					}

					TransformVertexUniform cubeUniforms = new TransformVertexUniform(modelToWorld * worldToClipSpace);
					cmdbuf.PushVertexUniformData(cubeUniforms);

					renderPass.DrawIndexedPrimitives(36, 1, 0, 0, 0);
				}
			}


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
