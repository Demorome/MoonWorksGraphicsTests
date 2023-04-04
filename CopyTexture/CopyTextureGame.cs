﻿using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Math.Float;

namespace MoonWorks.Test
{
    class CopyTextureGame : Game
    {
        private GraphicsPipeline pipeline;
        private Buffer vertexBuffer;
        private Buffer indexBuffer;
        private Texture originalTexture;
        private Texture textureCopy;
        private Texture textureSmallCopy;
        private Sampler sampler;

        public CopyTextureGame() : base(TestUtils.GetStandardWindowCreateInfo(), TestUtils.GetStandardFrameLimiterSettings(), 60, true)
        {
            // Load the shaders
            ShaderModule vertShaderModule = new ShaderModule(GraphicsDevice, TestUtils.GetShaderPath("TexturedQuadVert"));
            ShaderModule fragShaderModule = new ShaderModule(GraphicsDevice, TestUtils.GetShaderPath("TexturedQuadFrag"));

            // Create the graphics pipeline
            GraphicsPipelineCreateInfo pipelineCreateInfo = TestUtils.GetStandardGraphicsPipelineCreateInfo(
                MainWindow.SwapchainFormat,
                vertShaderModule,
                fragShaderModule
            );
            pipelineCreateInfo.AttachmentInfo.ColorAttachmentDescriptions[0].BlendState = ColorAttachmentBlendState.AlphaBlend;
            pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>();
            pipelineCreateInfo.FragmentShaderInfo.SamplerBindingCount = 1;
            pipeline = new GraphicsPipeline(GraphicsDevice, pipelineCreateInfo);

            // Create sampler
            sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);

            // Create and populate the GPU resources
            vertexBuffer = Buffer.Create<PositionTextureVertex>(GraphicsDevice, BufferUsageFlags.Vertex, 12);
            indexBuffer = Buffer.Create<ushort>(GraphicsDevice, BufferUsageFlags.Index, 12);

            CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();

            cmdbuf.SetBufferData(
                vertexBuffer,
                new PositionTextureVertex[]
                {
                    new PositionTextureVertex(new Vector3(-1f, 0f, 0), new Vector2(0, 0)),
                    new PositionTextureVertex(new Vector3( 0f, 0f, 0), new Vector2(1, 0)),
                    new PositionTextureVertex(new Vector3( 0f, 1f, 0), new Vector2(1, 1)),
                    new PositionTextureVertex(new Vector3(-1f, 1f, 0), new Vector2(0, 1)),

                    new PositionTextureVertex(new Vector3(0f, 0f, 0), new Vector2(0, 0)),
                    new PositionTextureVertex(new Vector3(1f, 0f, 0), new Vector2(1, 0)),
                    new PositionTextureVertex(new Vector3(1f, 1f, 0), new Vector2(1, 1)),
                    new PositionTextureVertex(new Vector3(0f, 1f, 0), new Vector2(0, 1)),

                    new PositionTextureVertex(new Vector3(-0.5f, -1f, 0), new Vector2(0, 0)),
                    new PositionTextureVertex(new Vector3( 0.5f, -1f, 0), new Vector2(1, 0)),
                    new PositionTextureVertex(new Vector3( 0.5f,  0f, 0), new Vector2(1, 1)),
                    new PositionTextureVertex(new Vector3(-0.5f,  0f, 0), new Vector2(0, 1))
                }
            );
            cmdbuf.SetBufferData(
                indexBuffer,
                new ushort[]
                {
                    0, 1, 2,
                    0, 2, 3,
                }
            );

            // Load the texture. Copy-pasted from Texture.LoadPNG,
            // but with the texture bytes stored.
            var pixels = RefreshCS.Refresh.Refresh_Image_LoadPNGFromFile(
                TestUtils.GetTexturePath("ravioli.png"),
                out var width,
                out var height,
                out var channels
            );

            var byteCount = (uint)(width * height * channels);

            TextureCreateInfo textureCreateInfo = new TextureCreateInfo();
            textureCreateInfo.Width = (uint) width;
            textureCreateInfo.Height = (uint) height;
            textureCreateInfo.Depth = 1;
            textureCreateInfo.Format = TextureFormat.R8G8B8A8;
            textureCreateInfo.IsCube = false;
            textureCreateInfo.LevelCount = 1;
            textureCreateInfo.UsageFlags = TextureUsageFlags.Sampler;

            originalTexture = new Texture(GraphicsDevice, textureCreateInfo);
            cmdbuf.SetTextureData(originalTexture, pixels, byteCount);

            byte[] textureBytes = new byte[byteCount];
            System.Runtime.InteropServices.Marshal.Copy(pixels, textureBytes, 0, (int) byteCount);

            RefreshCS.Refresh.Refresh_Image_FreePNG(pixels);

            // Create a 1:1 copy of the texture
            textureCopy = new Texture(GraphicsDevice, textureCreateInfo);
            cmdbuf.CopyTextureToTexture(
                new TextureSlice(originalTexture),
                new TextureSlice(textureCopy),
                Filter.Linear
            );

            // Create a half-sized copy of this texture
            textureCreateInfo.Width /= 2;
            textureCreateInfo.Height /= 2;
            textureSmallCopy = new Texture(GraphicsDevice, textureCreateInfo);
            cmdbuf.CopyTextureToTexture(
                new TextureSlice(originalTexture),
                new TextureSlice(
                    textureSmallCopy,
                    new Rect(
                        (int) textureCreateInfo.Width,
                        (int) textureCreateInfo.Height
                    )
                ),
                Filter.Linear
            );

            // Copy the texture to a buffer
            Buffer compareBuffer = Buffer.Create<byte>(GraphicsDevice, 0, (uint) textureBytes.Length);
            cmdbuf.CopyTextureToBuffer(new TextureSlice(originalTexture), compareBuffer);

            GraphicsDevice.Submit(cmdbuf);
            GraphicsDevice.Wait();

            // Compare the original bytes to the copied bytes.
            byte[] copiedBytes = new byte[textureBytes.Length];
            compareBuffer.GetData<byte>(copiedBytes, (uint) copiedBytes.Length);

			var originalSpan = new System.ReadOnlySpan<byte>(textureBytes);
			var copiedSpan = new System.ReadOnlySpan<byte>(copiedBytes);

			if (System.MemoryExtensions.SequenceEqual(originalSpan, copiedSpan))
			{
				Logger.LogError("SUCCESS! Original texture bytes and the bytes from CopyTextureToBuffer match!");

			}
			else
			{
				Logger.LogError("FAIL! Original texture bytes do not match bytes from CopyTextureToBuffer!");
			}
        }

        protected override void Update(System.TimeSpan delta) { }

        protected override void Draw(double alpha)
        {
            CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
            Texture? backbuffer = cmdbuf.AcquireSwapchainTexture(MainWindow);
            if (backbuffer != null)
            {
                cmdbuf.BeginRenderPass(new ColorAttachmentInfo(backbuffer, Color.Black));
                cmdbuf.BindGraphicsPipeline(pipeline);
                cmdbuf.BindVertexBuffers(vertexBuffer);
                cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
                cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(originalTexture, sampler));
                cmdbuf.DrawIndexedPrimitives(0, 0, 2, 0, 0);
                cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(textureCopy, sampler));
                cmdbuf.DrawIndexedPrimitives(4, 0, 2, 0, 0);
                cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(textureSmallCopy, sampler));
                cmdbuf.DrawIndexedPrimitives(8, 0, 2, 0, 0);
                cmdbuf.EndRenderPass();
            }
            GraphicsDevice.Submit(cmdbuf);
        }

        public static void Main(string[] args)
        {
            CopyTextureGame game = new CopyTextureGame();
            game.Run();
        }
    }
}
