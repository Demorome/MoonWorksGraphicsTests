using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;

namespace MoonWorksGraphicsTests;

class HelloRectangleExample : Example
{
	private GraphicsPipeline FillPipeline;
	private GraphicsPipeline LinePipeline;

	private Buffer VertexBuffer;
	private Buffer IndicesBuffer;

	private Viewport SmallViewport = new Viewport(160, 120, 320, 240);
	private Rect ScissorRect = new Rect(320, 240, 320, 240);

	PositionColorVertex[] Vertices = {
		new PositionColorVertex(new Vector3(0.5f, 0.5f, 0.0f), Color.Blue),  // top right
		new PositionColorVertex(new Vector3(0.5f, -0.5f, 0.0f), Color.Red),  // bottom right
		new PositionColorVertex(new Vector3(-0.5f, -0.5f, 0.0f), Color.Lime),  // bottom left
		new PositionColorVertex(new Vector3(-0.5f, 0.5f, 0.0f), Color.PeachPuff)   // top left 
	};
	ushort[] Indices = {  // note that we start from 0!
		0, 1, 3,   // first triangle
		1, 2, 3    // second triangle
	};  

	private bool UseWireframeMode;
	private bool UseSmallViewport;
	private bool UseScissorRect;

	public override void Init()
	{
		Window.SetTitle("HelloRectangle");

		Logger.LogInfo("Press Left to toggle wireframe mode\nPress Down to toggle small viewport\nPress Right to toggle scissor rect");

		Shader vertShaderModule = ShaderCross.Create(
			GraphicsDevice,
			RootTitleStorage,
			TestUtils.GetHLSLPath("PositionColor.vert"),
			"main",
			ShaderCross.ShaderFormat.HLSL,
			ShaderStage.Vertex
		);

		Shader fragShaderModule = ShaderCross.Create(
			GraphicsDevice,
			RootTitleStorage,
			TestUtils.GetHLSLPath("SolidColor.frag"),
			"main",
			ShaderCross.ShaderFormat.HLSL,
			ShaderStage.Fragment
		);

		GraphicsPipelineCreateInfo pipelineCreateInfo = TestUtils.GetStandardGraphicsPipelineCreateInfo(
			Window.SwapchainFormat,
			vertShaderModule,
			fragShaderModule
		);
		pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionColorVertex>(0);

		FillPipeline = GraphicsPipeline.Create(GraphicsDevice, pipelineCreateInfo);

		pipelineCreateInfo.RasterizerState.FillMode = FillMode.Line;
		LinePipeline = GraphicsPipeline.Create(GraphicsDevice, pipelineCreateInfo);


		// Upload GPU resources and dispatch compute work
		var resourceUploader = new ResourceUploader(GraphicsDevice);
		VertexBuffer = resourceUploader.CreateBuffer<PositionColorVertex>(
			Vertices,
			BufferUsageFlags.Vertex
		);

		IndicesBuffer = resourceUploader.CreateBuffer<ushort>(
			Indices,
			BufferUsageFlags.Index
		);

		resourceUploader.Upload();
		resourceUploader.Dispose();
	}

	public override void Update(System.TimeSpan delta)
	{
		if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Left))
		{
			UseWireframeMode = !UseWireframeMode;
			Logger.LogInfo("Using wireframe mode: " + UseWireframeMode);
		}

		if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Bottom))
		{
			UseSmallViewport = !UseSmallViewport;
			Logger.LogInfo("Using small viewport: " + UseSmallViewport);
		}

		if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Right))
		{
			UseScissorRect = !UseScissorRect;
			Logger.LogInfo("Using scissor rect: " + UseScissorRect);
		}
	}

	public override void Draw(double alpha)
	{
		CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(Window);
		if (swapchainTexture != null)
		{
			var renderPass = cmdbuf.BeginRenderPass(
				new ColorTargetInfo(swapchainTexture, Color.Black)
			);

			renderPass.BindGraphicsPipeline(UseWireframeMode ? LinePipeline : FillPipeline);

			if (UseSmallViewport)
			{
				renderPass.SetViewport(SmallViewport);
			}
			if (UseScissorRect)
			{
				renderPass.SetScissor(ScissorRect);
			}

			renderPass.BindVertexBuffers(VertexBuffer);
			renderPass.BindIndexBuffer(IndicesBuffer, IndexElementSize.Sixteen);
			renderPass.DrawIndexedPrimitives(6, 1, 0, 0, 0);

			cmdbuf.EndRenderPass(renderPass);
		}

		GraphicsDevice.Submit(cmdbuf);
	}

	public override void Destroy()
	{
		FillPipeline.Dispose();
		LinePipeline.Dispose();
	}
}
