﻿using System;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Math.Float;

namespace MoonWorksGraphicsTests;

class TexturedQuadExample : Example
{
	private GraphicsPipeline pipeline;
	private GpuBuffer vertexBuffer;
	private GpuBuffer indexBuffer;
	private Sampler[] samplers = new Sampler[6];
	private string[] samplerNames = new string[]
	{
		"PointClamp",
		"PointWrap",
		"LinearClamp",
		"LinearWrap",
		"AnisotropicClamp",
		"AnisotropicWrap"
	};

	private int currentSamplerIndex;

	private Texture[] textures = new Texture[4];
	private string[] imageLoadFormatNames = new string[]
	{
		"PNG from file",
		"PNG from memory",
		"QOI from file",
		"QOI from memory"
	};

	private int currentTextureIndex;

	private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

    public override void Init(Window window, GraphicsDevice graphicsDevice, Inputs inputs)
    {
		Window = window;
		GraphicsDevice = graphicsDevice;
		Inputs = inputs;

		Window.SetTitle("TexturedQuad");

		Logger.LogInfo("Press Left and Right to cycle between sampler states");
		Logger.LogInfo("Setting sampler state to: " + samplerNames[0]);

		Logger.LogInfo("Press Down to cycle between image load formats");
		Logger.LogInfo("Setting image format to: " + imageLoadFormatNames[0]);

		var pngBytes = System.IO.File.ReadAllBytes(TestUtils.GetTexturePath("ravioli.png"));
		var qoiBytes = System.IO.File.ReadAllBytes(TestUtils.GetTexturePath("ravioli.qoi"));

		Logger.LogInfo(pngBytes.Length.ToString());
		Logger.LogInfo(qoiBytes.Length.ToString());

		// Load the shaders
		Shader vertShader = new Shader(
			GraphicsDevice,
			TestUtils.GetShaderPath("TexturedQuad.vert"),
			"main",
			ShaderStage.Vertex,
			ShaderFormat.SPIRV
		);

		Shader fragShader = new Shader(
			GraphicsDevice,
			TestUtils.GetShaderPath("TexturedQuad.frag"),
			"main",
			ShaderStage.Fragment,
			ShaderFormat.SPIRV
		);

		// Create the graphics pipeline
		GraphicsPipelineCreateInfo pipelineCreateInfo = TestUtils.GetStandardGraphicsPipelineCreateInfo(
			Window.SwapchainFormat,
			vertShader,
			fragShader
		);
		pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>();
		pipelineCreateInfo.FragmentShaderResourceInfo = new GraphicsPipelineResourceInfo
		{
			SamplerCount = 1
		};
		pipeline = new GraphicsPipeline(GraphicsDevice, pipelineCreateInfo);

		// Create samplers
		samplers[0] = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);
		samplers[1] = new Sampler(GraphicsDevice, SamplerCreateInfo.PointWrap);
		samplers[2] = new Sampler(GraphicsDevice, SamplerCreateInfo.LinearClamp);
		samplers[3] = new Sampler(GraphicsDevice, SamplerCreateInfo.LinearWrap);
		samplers[4] = new Sampler(GraphicsDevice, SamplerCreateInfo.AnisotropicClamp);
		samplers[5] = new Sampler(GraphicsDevice, SamplerCreateInfo.AnisotropicWrap);

		var vertexData = new Span<PositionTextureVertex>([
			new PositionTextureVertex(new Vector3(-1,  1, 0), new Vector2(0, 0)),
			new PositionTextureVertex(new Vector3( 1,  1, 0), new Vector2(4, 0)),
			new PositionTextureVertex(new Vector3( 1, -1, 0), new Vector2(4, 4)),
			new PositionTextureVertex(new Vector3(-1, -1, 0), new Vector2(0, 4)),
		]);

		var indexData = new Span<ushort>([
			0, 1, 2,
			0, 2, 3,
		]);

		// Create and populate the GPU resources

		var resourceUploader = new ResourceUploader(GraphicsDevice);

		vertexBuffer = resourceUploader.CreateBuffer(vertexData, BufferUsageFlags.Vertex);
		indexBuffer = resourceUploader.CreateBuffer(indexData, BufferUsageFlags.Index);

		textures[0] = resourceUploader.CreateTexture2DFromCompressed(TestUtils.GetTexturePath("ravioli.png"));
		textures[1] = resourceUploader.CreateTexture2DFromCompressed(pngBytes);
		textures[2] = resourceUploader.CreateTexture2DFromCompressed(TestUtils.GetTexturePath("ravioli.qoi"));
		textures[3] = resourceUploader.CreateTexture2DFromCompressed(qoiBytes);

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

		int prevTextureIndex = currentTextureIndex;

		if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Bottom))
		{
			currentTextureIndex = (currentTextureIndex + 1) % imageLoadFormatNames.Length;
		}

		if (prevTextureIndex != currentTextureIndex)
		{
			Logger.LogInfo("Setting texture format to: " + imageLoadFormatNames[currentTextureIndex]);
		}
	}

	public override void Draw(double alpha)
	{
		CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(Window);
		if (swapchainTexture != null)
		{
			var renderPass = cmdbuf.BeginRenderPass(
				new ColorAttachmentInfo(
					swapchainTexture,
					false,
					Color.Black
				)
			);
			renderPass.BindGraphicsPipeline(pipeline);
			renderPass.BindVertexBuffer(vertexBuffer);
			renderPass.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
			renderPass.BindFragmentSampler(new TextureSamplerBinding(textures[currentTextureIndex], samplers[currentSamplerIndex]));
			renderPass.DrawIndexedPrimitives(0, 0, 2);
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
