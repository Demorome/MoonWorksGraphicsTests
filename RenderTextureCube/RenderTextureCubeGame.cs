﻿using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Math.Float;
using MoonWorks.Math;
using System.Runtime.InteropServices;

namespace MoonWorks.Test
{
    class RenderTextureCubeGame : Game
    {
        private GraphicsPipeline pipeline;
        private Buffer vertexBuffer;
        private Buffer indexBuffer;
        private Texture cubemap;
        private Sampler sampler;

        private Vector3 camPos = new Vector3(0, 0, 4f);

        private Color[] colors = new Color[]
        {
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.Orange,
            Color.Yellow,
            Color.Purple,
        };

        public RenderTextureCubeGame() : base(TestUtils.GetStandardWindowCreateInfo(), TestUtils.GetStandardFrameLimiterSettings(), 60, true)
        {
            Logger.LogInfo("Press Down to view the other side of the cubemap");

            // Load the shaders
            ShaderModule vertShaderModule = new ShaderModule(GraphicsDevice, TestUtils.GetShaderPath("SkyboxVert"));
            ShaderModule fragShaderModule = new ShaderModule(GraphicsDevice, TestUtils.GetShaderPath("SkyboxFrag"));

            // Create the graphics pipeline
            GraphicsPipelineCreateInfo pipelineCreateInfo = TestUtils.GetStandardGraphicsPipelineCreateInfo(
                MainWindow.SwapchainFormat,
                vertShaderModule,
                fragShaderModule
            );
            pipelineCreateInfo.VertexInputState = VertexInputState.CreateSingleBinding<PositionVertex>();
            pipelineCreateInfo.VertexShaderInfo.UniformBufferSize = (uint) Marshal.SizeOf<TransformVertexUniform>();
            pipelineCreateInfo.FragmentShaderInfo.SamplerBindingCount = 1;
            pipeline = new GraphicsPipeline(GraphicsDevice, pipelineCreateInfo);

            // Create samplers
            sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);

            // Create and populate the GPU resources
            vertexBuffer = Buffer.Create<PositionVertex>(GraphicsDevice, BufferUsageFlags.Vertex, 24);
            indexBuffer = Buffer.Create<ushort>(GraphicsDevice, BufferUsageFlags.Index, 36);
            cubemap = Texture.CreateTextureCube(
                GraphicsDevice,
                16,
                TextureFormat.R8G8B8A8,
                TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler
            );

            CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
            cmdbuf.SetBufferData(
                vertexBuffer,
                new PositionVertex[]
                {
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
                }
            );

            cmdbuf.SetBufferData(
                indexBuffer,
                new ushort[]
                {
                    0, 1, 2,    0, 2, 3,
                    6, 5, 4,    7, 6, 4,
                    8, 9, 10,   8, 10, 11,
                    14, 13, 12, 15, 14, 12,
                    16, 17, 18, 16, 18, 19,
                    22, 21, 20, 23, 22, 20
                }
            );

            // Clear each slice of the cubemap to a different color
            for (uint i = 0; i < 6; i += 1)
            {
                ColorAttachmentInfo attachmentInfo = new ColorAttachmentInfo
                {
                    Texture = cubemap,
                    ClearColor = colors[i],
                    Depth = 0,
                    Layer = i,
                    Level = 0,
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.Store
                };
                cmdbuf.BeginRenderPass(attachmentInfo);
                cmdbuf.EndRenderPass();
            }

            GraphicsDevice.Submit(cmdbuf);
        }

        protected override void Update(System.TimeSpan delta)
        {
            if (TestUtils.CheckButtonPressed(Inputs, TestUtils.ButtonType.Bottom))
            {
                camPos.Z *= -1;
            }
        }

        protected override void Draw(double alpha)
        {
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(75f),
                (float) MainWindow.Width / MainWindow.Height,
                0.01f,
                100f
            );
            Matrix4x4 view = Matrix4x4.CreateLookAt(
                camPos,
                Vector3.Zero,
                Vector3.Up
            );
            TransformVertexUniform vertUniforms = new TransformVertexUniform(view * proj);

            CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
            Texture? backbuffer = cmdbuf.AcquireSwapchainTexture(MainWindow);
            if (backbuffer != null)
            {
                cmdbuf.BeginRenderPass(new ColorAttachmentInfo(backbuffer, Color.Black));
                cmdbuf.BindGraphicsPipeline(pipeline);
                cmdbuf.BindVertexBuffers(vertexBuffer);
                cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
                cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(cubemap, sampler));
                uint vertexUniformOffset = cmdbuf.PushVertexShaderUniforms(vertUniforms);
                cmdbuf.DrawIndexedPrimitives(0, 0, 12, vertexUniformOffset, 0);
                cmdbuf.EndRenderPass();
            }
            GraphicsDevice.Submit(cmdbuf);
        }

        public static void Main(string[] args)
        {
            RenderTextureCubeGame game = new RenderTextureCubeGame();
            game.Run();
        }
    }
}
