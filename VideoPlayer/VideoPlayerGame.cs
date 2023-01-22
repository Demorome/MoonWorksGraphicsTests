﻿using MoonWorks.Math.Float;
using MoonWorks.Graphics;
using MoonWorks.Video;

namespace MoonWorks.Test
{
    class VideoPlayerGame : Game
    {
        private GraphicsPipeline pipeline;
        private Sampler sampler;
        private Buffer vertexBuffer;
        private Buffer indexBuffer;

        private Video.Video video;
        private VideoPlayer videoPlayer;

        public VideoPlayerGame() : base(TestUtils.GetStandardWindowCreateInfo(), TestUtils.GetStandardFrameLimiterSettings(), 60, true)
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
            pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionTextureVertex>();
            pipelineCreateInfo.FragmentShaderInfo.SamplerBindingCount = 1;
            pipeline = new GraphicsPipeline(GraphicsDevice, pipelineCreateInfo);

            // Create the sampler
            sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.LinearClamp);

            // Create and populate the GPU resources
            vertexBuffer = Buffer.Create<PositionTextureVertex>(GraphicsDevice, BufferUsageFlags.Vertex, 4);
            indexBuffer = Buffer.Create<ushort>(GraphicsDevice, BufferUsageFlags.Index, 6);

            CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
            cmdbuf.SetBufferData(
                vertexBuffer,
                new PositionTextureVertex[]
                {
                    new PositionTextureVertex(new Vector3(-1, -1, 0), new Vector2(0, 0)),
                    new PositionTextureVertex(new Vector3(1, -1, 0), new Vector2(1, 0)),
                    new PositionTextureVertex(new Vector3(1, 1, 0), new Vector2(1, 1)),
                    new PositionTextureVertex(new Vector3(-1, 1, 0), new Vector2(0, 1)),
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
            GraphicsDevice.Submit(cmdbuf);
            GraphicsDevice.Wait();

            // Load the video
            video = new Video.Video(TestUtils.GetVideoPath("hello.ogv"));

            // Play the video
            videoPlayer = new VideoPlayer(GraphicsDevice, AudioDevice);
            videoPlayer.Load(video);
            videoPlayer.Play();
        }

        protected override void Update(System.TimeSpan delta)
        {
            videoPlayer.Render();
        }

        protected override void Draw(double alpha)
        {
            CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
            Texture? backbuffer = cmdbuf.AcquireSwapchainTexture(MainWindow);
            if (backbuffer != null)
            {
                cmdbuf.BeginRenderPass(new ColorAttachmentInfo(backbuffer, Color.CornflowerBlue));
                cmdbuf.BindGraphicsPipeline(pipeline);
                cmdbuf.BindVertexBuffers(vertexBuffer);
                cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
                cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(videoPlayer.RenderTexture, sampler));
                cmdbuf.DrawIndexedPrimitives(0, 0, 2, 0, 0);
                cmdbuf.EndRenderPass();
            }
            GraphicsDevice.Submit(cmdbuf);
        }

        public static void Main(string[] args)
        {
            VideoPlayerGame game = new VideoPlayerGame();
            game.Run();
        }
    }
}
