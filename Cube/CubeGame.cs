﻿using MoonWorks.Graphics;
using MoonWorks.Math;
using MoonWorks.Math.Float;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MoonWorks.Test
{
	class CubeGame : Game
	{
		private GraphicsPipeline cubePipeline;
		private GraphicsPipeline cubePipelineDepthOnly;
		private GraphicsPipeline skyboxPipeline;
		private GraphicsPipeline skyboxPipelineDepthOnly;
		private GraphicsPipeline blitPipeline;

		private Texture depthTexture;
		private Sampler depthSampler;
		private DepthUniforms depthUniforms;

		private GpuBuffer cubeVertexBuffer;
		private GpuBuffer skyboxVertexBuffer;
		private GpuBuffer blitVertexBuffer;
		private GpuBuffer indexBuffer;

		private TransferBuffer screenshotTransferBuffer;
		private Texture screenshotTexture;
		private Fence? screenshotFence;

		private Texture skyboxTexture;
		private Sampler skyboxSampler;

		private bool finishedLoading = false;
		private float cubeTimer = 0f;
		private Quaternion cubeRotation = Quaternion.Identity;
		private Quaternion previousCubeRotation = Quaternion.Identity;
		private bool depthOnlyEnabled = false;
		private Vector3 camPos = new Vector3(0, 1.5f, 4f);

		private bool takeScreenshot;
		private bool screenshotInProgress;
		private bool swapchainCopied; // don't want to take screenshot if the swapchain was invalid

		struct DepthUniforms
		{
			public float ZNear;
			public float ZFar;

			public DepthUniforms(float zNear, float zFar)
			{
				ZNear = zNear;
				ZFar = zFar;
			}
		}

		// Upload cubemap layers one at a time to minimize transfer size
		unsafe void LoadCubemap(string[] imagePaths)
		{
			var cubemapUploader = new ResourceUploader(GraphicsDevice);

			for (uint i = 0; i < imagePaths.Length; i++)
			{
				var textureRegion = new TextureRegion
				{
					TextureSlice = new TextureSlice
					{
						Texture = skyboxTexture,
						MipLevel = 0,
						Layer = i,
					},
					X = 0,
					Y = 0,
					Z = 0,
					Width = skyboxTexture.Width,
					Height = skyboxTexture.Height,
					Depth = 1
				};

				cubemapUploader.SetTextureDataFromCompressed(
					textureRegion,
					imagePaths[i]
				);

				cubemapUploader.UploadAndWait();
			}

			cubemapUploader.Dispose();
		}

		public CubeGame() : base(TestUtils.GetStandardWindowCreateInfo(), TestUtils.GetStandardFrameLimiterSettings(), TestUtils.PreferredBackends, 60, true)
		{
			ShaderModule cubeVertShaderModule = new ShaderModule(
				GraphicsDevice,
				TestUtils.GetShaderPath("PositionColorWithMatrix.vert")
			);
			ShaderModule cubeFragShaderModule = new ShaderModule(
				GraphicsDevice,
				TestUtils.GetShaderPath("SolidColor.frag")
			);

			ShaderModule skyboxVertShaderModule = new ShaderModule(
				GraphicsDevice,
				TestUtils.GetShaderPath("Skybox.vert")
			);
			ShaderModule skyboxFragShaderModule = new ShaderModule(
				GraphicsDevice,
				TestUtils.GetShaderPath("Skybox.frag")
			);

			ShaderModule blitVertShaderModule = new ShaderModule(
				GraphicsDevice,
				TestUtils.GetShaderPath("TexturedQuad.vert")
			);
			ShaderModule blitFragShaderModule = new ShaderModule(
				GraphicsDevice,
				TestUtils.GetShaderPath("TexturedDepthQuad.frag")
			);

			depthTexture = Texture.CreateTexture2D(
				GraphicsDevice,
				MainWindow.Width,
				MainWindow.Height,
				TextureFormat.D16,
				TextureUsageFlags.DepthStencilTarget | TextureUsageFlags.Sampler
			);
			depthTexture.Name = "Depth Texture";

			depthSampler = new Sampler(GraphicsDevice, new SamplerCreateInfo());
			depthUniforms = new DepthUniforms(0.01f, 100f);

			skyboxTexture = Texture.CreateTextureCube(
				GraphicsDevice,
				2048,
				TextureFormat.R8G8B8A8,
				TextureUsageFlags.Sampler
			);
			skyboxTexture.Name = "Skybox";

			skyboxSampler = new Sampler(GraphicsDevice, new SamplerCreateInfo());

			screenshotTransferBuffer = new TransferBuffer(GraphicsDevice, TransferUsage.Texture, MainWindow.Width * MainWindow.Height * 4);
			screenshotTexture = Texture.CreateTexture2D(GraphicsDevice, MainWindow.Width, MainWindow.Height, MainWindow.SwapchainFormat, TextureUsageFlags.Sampler);
			screenshotTexture.Name = "Screenshot";

			Task loadingTask = Task.Run(() => UploadGPUAssets());

			// Create the cube pipelines

			GraphicsPipelineCreateInfo cubePipelineCreateInfo = new GraphicsPipelineCreateInfo
			{
				AttachmentInfo = new GraphicsPipelineAttachmentInfo(
					TextureFormat.D16,
					new ColorAttachmentDescription(
						MainWindow.SwapchainFormat,
						ColorAttachmentBlendState.Opaque
					)
				),
				DepthStencilState = DepthStencilState.DepthReadWrite,
				VertexShaderInfo = GraphicsShaderInfo.Create<TransformVertexUniform>(cubeVertShaderModule, "main", 0),
				VertexInputState = VertexInputState.CreateSingleBinding<PositionColorVertex>(),
				PrimitiveType = PrimitiveType.TriangleList,
				FragmentShaderInfo = GraphicsShaderInfo.Create(cubeFragShaderModule, "main", 0),
				RasterizerState = RasterizerState.CW_CullBack,
				MultisampleState = MultisampleState.None
			};
			cubePipeline = new GraphicsPipeline(GraphicsDevice, cubePipelineCreateInfo);

			cubePipelineCreateInfo.AttachmentInfo = new GraphicsPipelineAttachmentInfo(TextureFormat.D16);
			cubePipelineDepthOnly = new GraphicsPipeline(GraphicsDevice, cubePipelineCreateInfo);

			// Create the skybox pipelines

			GraphicsPipelineCreateInfo skyboxPipelineCreateInfo = new GraphicsPipelineCreateInfo
			{
				AttachmentInfo = new GraphicsPipelineAttachmentInfo(
						TextureFormat.D16,
						new ColorAttachmentDescription(
							MainWindow.SwapchainFormat,
							ColorAttachmentBlendState.Opaque
						)
					),
				DepthStencilState = DepthStencilState.DepthReadWrite,
				VertexShaderInfo = GraphicsShaderInfo.Create<TransformVertexUniform>(skyboxVertShaderModule, "main", 0),
				VertexInputState = VertexInputState.CreateSingleBinding<PositionVertex>(),
				PrimitiveType = PrimitiveType.TriangleList,
				FragmentShaderInfo = GraphicsShaderInfo.Create(skyboxFragShaderModule, "main", 1),
				RasterizerState = RasterizerState.CW_CullNone,
				MultisampleState = MultisampleState.None,
			};
			skyboxPipeline = new GraphicsPipeline(GraphicsDevice, skyboxPipelineCreateInfo);

			skyboxPipelineCreateInfo.AttachmentInfo = new GraphicsPipelineAttachmentInfo(TextureFormat.D16);
			skyboxPipelineDepthOnly = new GraphicsPipeline(GraphicsDevice, skyboxPipelineCreateInfo);

			// Create the blit pipeline

			GraphicsPipelineCreateInfo blitPipelineCreateInfo = TestUtils.GetStandardGraphicsPipelineCreateInfo(
				MainWindow.SwapchainFormat,
				blitVertShaderModule,
				blitFragShaderModule
			);
			blitPipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>();
			blitPipelineCreateInfo.FragmentShaderInfo = GraphicsShaderInfo.Create<DepthUniforms>(blitFragShaderModule, "main", 1);
			blitPipeline = new GraphicsPipeline(GraphicsDevice, blitPipelineCreateInfo);
		}

		private void UploadGPUAssets()
		{
			Logger.LogInfo("Loading...");

			var cubeVertexData = new Span<PositionColorVertex>([
				new PositionColorVertex(new Vector3(-1, -1, -1), new Color(1f, 0f, 0f)),
				new PositionColorVertex(new Vector3(1, -1, -1), new Color(1f, 0f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(1f, 0f, 0f)),
				new PositionColorVertex(new Vector3(-1, 1, -1), new Color(1f, 0f, 0f)),

				new PositionColorVertex(new Vector3(-1, -1, 1), new Color(0f, 1f, 0f)),
				new PositionColorVertex(new Vector3(1, -1, 1), new Color(0f, 1f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, 1), new Color(0f, 1f, 0f)),
				new PositionColorVertex(new Vector3(-1, 1, 1), new Color(0f, 1f, 0f)),

				new PositionColorVertex(new Vector3(-1, -1, -1), new Color(0f, 0f, 1f)),
				new PositionColorVertex(new Vector3(-1, 1, -1), new Color(0f, 0f, 1f)),
				new PositionColorVertex(new Vector3(-1, 1, 1), new Color(0f, 0f, 1f)),
				new PositionColorVertex(new Vector3(-1, -1, 1), new Color(0f, 0f, 1f)),

				new PositionColorVertex(new Vector3(1, -1, -1), new Color(1f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(1f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, 1), new Color(1f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, -1, 1), new Color(1f, 0.5f, 0f)),

				new PositionColorVertex(new Vector3(-1, -1, -1), new Color(1f, 0f, 0.5f)),
				new PositionColorVertex(new Vector3(-1, -1, 1), new Color(1f, 0f, 0.5f)),
				new PositionColorVertex(new Vector3(1, -1, 1), new Color(1f, 0f, 0.5f)),
				new PositionColorVertex(new Vector3(1, -1, -1), new Color(1f, 0f, 0.5f)),

				new PositionColorVertex(new Vector3(-1, 1, -1), new Color(0f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(-1, 1, 1), new Color(0f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, 1), new Color(0f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(0f, 0.5f, 0f))
			]);

			var skyboxVertexData = new Span<PositionVertex>([
				new PositionVertex(new Vector3(-10, -10, -10)),
				new PositionVertex(new Vector3(10, -10, -10)),
				new PositionVertex(new Vector3(10, 10, -10)),
				new PositionVertex(new Vector3(-10, 10, -10)),

				new PositionVertex(new Vector3(-10, -10, 10)),
				new PositionVertex(new Vector3(10, -10, 10)),
				new PositionVertex(new Vector3(10, 10, 10)),
				new PositionVertex(new Vector3(-10, 10, 10)),

				new PositionVertex(new Vector3(-10, -10, -10)),
				new PositionVertex(new Vector3(-10, 10, -10)),
				new PositionVertex(new Vector3(-10, 10, 10)),
				new PositionVertex(new Vector3(-10, -10, 10)),

				new PositionVertex(new Vector3(10, -10, -10)),
				new PositionVertex(new Vector3(10, 10, -10)),
				new PositionVertex(new Vector3(10, 10, 10)),
				new PositionVertex(new Vector3(10, -10, 10)),

				new PositionVertex(new Vector3(-10, -10, -10)),
				new PositionVertex(new Vector3(-10, -10, 10)),
				new PositionVertex(new Vector3(10, -10, 10)),
				new PositionVertex(new Vector3(10, -10, -10)),

				new PositionVertex(new Vector3(-10, 10, -10)),
				new PositionVertex(new Vector3(-10, 10, 10)),
				new PositionVertex(new Vector3(10, 10, 10)),
				new PositionVertex(new Vector3(10, 10, -10))
			]);

			var indexData = new Span<uint>([
				0, 1, 2,    0, 2, 3,
				6, 5, 4,    7, 6, 4,
				8, 9, 10,   8, 10, 11,
				14, 13, 12, 15, 14, 12,
				16, 17, 18, 16, 18, 19,
				22, 21, 20, 23, 22, 20
			]);

			var blitVertexData = new Span<PositionTextureVertex>([
				new PositionTextureVertex(new Vector3(-1, -1, 0), new Vector2(0, 0)),
				new PositionTextureVertex(new Vector3(1, -1, 0), new Vector2(1, 0)),
				new PositionTextureVertex(new Vector3(1, 1, 0), new Vector2(1, 1)),
				new PositionTextureVertex(new Vector3(-1, -1, 0), new Vector2(0, 0)),
				new PositionTextureVertex(new Vector3(1, 1, 0), new Vector2(1, 1)),
				new PositionTextureVertex(new Vector3(-1, 1, 0), new Vector2(0, 1)),
			]);

			var resourceUploader = new ResourceUploader(GraphicsDevice);

			cubeVertexBuffer = resourceUploader.CreateBuffer(cubeVertexData, BufferUsageFlags.Vertex);
			skyboxVertexBuffer = resourceUploader.CreateBuffer(skyboxVertexData, BufferUsageFlags.Vertex);
			indexBuffer = resourceUploader.CreateBuffer(indexData, BufferUsageFlags.Index);
			blitVertexBuffer = resourceUploader.CreateBuffer(blitVertexData, BufferUsageFlags.Vertex);

			cubeVertexBuffer.Name = "Cube Vertices";
			skyboxVertexBuffer.Name = "Skybox Vertices";
			indexBuffer.Name = "Cube Indices";
			blitVertexBuffer.Name = "Blit Vertices";

			resourceUploader.Upload();
			resourceUploader.Dispose();

			LoadCubemap(new string[]
			{
				TestUtils.GetTexturePath("right.png"),
				TestUtils.GetTexturePath("left.png"),
				TestUtils.GetTexturePath("top.png"),
				TestUtils.GetTexturePath("bottom.png"),
				TestUtils.GetTexturePath("front.png"),
				TestUtils.GetTexturePath("back.png")
			});

			finishedLoading = true;
			Logger.LogInfo("Finished loading!");
			Logger.LogInfo("Press Left to toggle Depth-Only Mode");
			Logger.LogInfo("Press Down to move the camera upwards");
			Logger.LogInfo("Press Right to save a screenshot");
		}

		protected override void Update(System.TimeSpan delta)
		{
			cubeTimer += (float) delta.TotalSeconds;

			previousCubeRotation = cubeRotation;

			cubeRotation = Quaternion.CreateFromYawPitchRoll(
				cubeTimer * 2f,
				0,
				cubeTimer * 2f
			);

			if (TestUtils.CheckButtonDown(Inputs, TestUtils.ButtonType.Bottom))
			{
				camPos.Y = System.MathF.Min(camPos.Y + 0.2f, 15f);
			}
			else
			{
				camPos.Y = System.MathF.Max(camPos.Y - 0.4f, 1.5f);
			}

			if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Left))
			{
				depthOnlyEnabled = !depthOnlyEnabled;
				Logger.LogInfo("Depth-Only Mode enabled: " + depthOnlyEnabled);
			}

			if (!screenshotInProgress && TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Right))
			{
				takeScreenshot = true;
			}
		}

		protected override void Draw(double alpha)
		{
			Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
				MathHelper.ToRadians(75f),
				(float) MainWindow.Width / MainWindow.Height,
				depthUniforms.ZNear,
				depthUniforms.ZFar
			);
			Matrix4x4 view = Matrix4x4.CreateLookAt(
				camPos,
				Vector3.Zero,
				Vector3.Up
			);
			TransformVertexUniform skyboxUniforms = new TransformVertexUniform(view * proj);

			Matrix4x4 model = Matrix4x4.CreateFromQuaternion(
				Quaternion.Slerp(
					previousCubeRotation,
					cubeRotation,
					(float) alpha
				)
			);
			TransformVertexUniform cubeUniforms = new TransformVertexUniform(model * view * proj);

			CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
			Texture swapchainTexture = cmdbuf.AcquireSwapchainTexture(MainWindow);
			if (swapchainTexture != null)
			{
				if (!finishedLoading)
				{
					float sine = System.MathF.Abs(System.MathF.Sin(cubeTimer));
					Color clearColor = new Color(sine, sine, sine);

					// Just show a clear screen.
					cmdbuf.BeginRenderPass(new ColorAttachmentInfo(swapchainTexture, WriteOptions.Cycle, clearColor));
					cmdbuf.EndRenderPass();
				}
				else
				{
					if (!depthOnlyEnabled)
					{
						cmdbuf.BeginRenderPass(
							new DepthStencilAttachmentInfo(depthTexture, WriteOptions.Cycle, new DepthStencilValue(1f, 0)),
							new ColorAttachmentInfo(swapchainTexture, WriteOptions.Cycle, LoadOp.DontCare)
						);
					}
					else
					{
						cmdbuf.BeginRenderPass(
							new DepthStencilAttachmentInfo(depthTexture, WriteOptions.Cycle, new DepthStencilValue(1f, 0), StoreOp.Store)
						);
					}

					// Draw cube
					cmdbuf.BindGraphicsPipeline(depthOnlyEnabled ? cubePipelineDepthOnly : cubePipeline);
					cmdbuf.BindVertexBuffers(cubeVertexBuffer);
					cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.ThirtyTwo);
					cmdbuf.PushVertexShaderUniforms(cubeUniforms);
					cmdbuf.DrawIndexedPrimitives(0, 0, 12);

					// Draw skybox
					cmdbuf.BindGraphicsPipeline(depthOnlyEnabled ? skyboxPipelineDepthOnly : skyboxPipeline);
					cmdbuf.BindVertexBuffers(skyboxVertexBuffer);
					cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.ThirtyTwo);
					cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(skyboxTexture, skyboxSampler));
					cmdbuf.PushVertexShaderUniforms(skyboxUniforms);
					cmdbuf.DrawIndexedPrimitives(0, 0, 12);

					cmdbuf.EndRenderPass();

					if (depthOnlyEnabled)
					{
						// Draw the depth buffer as a grayscale image
						cmdbuf.BeginRenderPass(new ColorAttachmentInfo(swapchainTexture, WriteOptions.Safe, LoadOp.Load));

						cmdbuf.BindGraphicsPipeline(blitPipeline);
						cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(depthTexture, depthSampler));
						cmdbuf.BindVertexBuffers(blitVertexBuffer);
						cmdbuf.PushFragmentShaderUniforms(depthUniforms);
						cmdbuf.DrawPrimitives(0, 2);

						cmdbuf.EndRenderPass();
					}

					if (takeScreenshot)
					{
						cmdbuf.BeginCopyPass();
						cmdbuf.CopyTextureToTexture(swapchainTexture, screenshotTexture, WriteOptions.Unsafe);
						cmdbuf.EndCopyPass();

						swapchainCopied = true;
					}
				}
			}

			if (takeScreenshot && swapchainCopied)
			{
				screenshotFence = GraphicsDevice.SubmitAndAcquireFence(cmdbuf);
				Task.Run(TakeScreenshot);

				takeScreenshot = false;
				swapchainCopied = false;
			}
			else
			{
				GraphicsDevice.Submit(cmdbuf);
			}
		}

		private unsafe void TakeScreenshot()
		{
			screenshotInProgress = true;

			GraphicsDevice.WaitForFences(screenshotFence);

			GraphicsDevice.DownloadFromTexture(
				screenshotTexture,
				screenshotTransferBuffer,
				TransferOptions.Unsafe
			);

			ImageUtils.SavePNG(
				Path.Combine(System.AppContext.BaseDirectory, "screenshot.png"),
				screenshotTransferBuffer,
				0,
				(int) screenshotTexture.Width,
				(int) screenshotTexture.Height,
				screenshotTexture.Format == TextureFormat.B8G8R8A8
			);

			GraphicsDevice.ReleaseFence(screenshotFence);
			screenshotFence = null;

			screenshotInProgress = false;
		}

		public static void Main(string[] args)
		{
			CubeGame game = new CubeGame();
			game.Run();
		}
	}
}
