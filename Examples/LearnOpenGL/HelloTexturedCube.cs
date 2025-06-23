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

    public override unsafe void Init()
    {
		Window.SetTitle("TexturedQuad");

		Logger.LogInfo("Press Left and Right to cycle between sampler states");
		Logger.LogInfo("Setting sampler state to: " + samplerNames[0]);

		// Load the shaders
		Shader vertShader = ShaderCross.Create(
			GraphicsDevice,
			RootTitleStorage,
			TestUtils.GetHLSLPath("TexturedQuad.vert"),
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
		GraphicsPipelineCreateInfo pipelineCreateInfo = TestUtils.GetStandardGraphicsPipelineCreateInfo(
			Window.SwapchainFormat,
			vertShader,
			fragShader
		);
		pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>();

		pipeline = GraphicsPipeline.Create(GraphicsDevice, pipelineCreateInfo);

		// Create samplers
		samplers[0] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.PointClamp);
		samplers[1] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.PointWrap);
		samplers[2] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.LinearClamp);
		samplers[3] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.LinearWrap);
		samplers[4] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.AnisotropicClamp);
		samplers[5] = Sampler.Create(GraphicsDevice, SamplerCreateInfo.AnisotropicWrap);

        ReadOnlySpan<PositionTextureVertex> vertexData = [
			new PositionTextureVertex(new Vector3(-1,  1, 0), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3( 1,  1, 0), new Vector2(2, 0)),
			new PositionTextureVertex(new Vector3( 1, -1, 0), new Vector2(2, 2)),
			new PositionTextureVertex(new Vector3(-1, -1, 0), new Vector2(0, 2)),
		];

        ReadOnlySpan<ushort> indexData = [
			0, 1, 2,
			0, 2, 3,
		];

		// Create and populate the GPU resources

		var resourceUploader = new ResourceUploader(GraphicsDevice);

		vertexBuffer = resourceUploader.CreateBuffer(vertexData, BufferUsageFlags.Vertex);
		indexBuffer = resourceUploader.CreateBuffer(indexData, BufferUsageFlags.Index);

		textures[0] = resourceUploader.CreateTexture2DFromCompressed(RootTitleStorage, TestUtils.GetTexturePath("ravioli.png"),
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
	}

	public override void Draw(double alpha)
	{
		var cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(Window);
		if (swapchainTexture != null)
		{
			var renderPass = cmdbuf.BeginRenderPass(
				new ColorTargetInfo(swapchainTexture, Color.Black)
			);
			renderPass.BindGraphicsPipeline(pipeline);
			renderPass.BindVertexBuffers(vertexBuffer);
			renderPass.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);

			Span<TextureSamplerBinding> samplerBindings = [
				new TextureSamplerBinding(textures[0], samplers[currentSamplerIndex]),
				new TextureSamplerBinding(textures[1], samplers[currentSamplerIndex])
			];
			renderPass.BindFragmentSamplers(samplerBindings);

			renderPass.DrawIndexedPrimitives(6, 1, 0, 0, 0);
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
