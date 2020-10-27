using System;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace GbSharp.Gui
{
    // Based off the basic Veldrid samples
    class Renderer
    {
        private GraphicsDevice GfxDevice;
        private CommandList CommandList;
        private DeviceBuffer VertexBuffer;
        private DeviceBuffer IndexBuffer;
        private Shader[] Shaders;
        private Pipeline Pipeline;

        private Texture RenderTexture;
        private TextureView RenderTextureView;

        private ResourceSet TextureResourceSet;

        private const string VertexCode = @"
#version 450
layout(location = 0) in vec2 Position;
layout(location = 1) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_texCoords;

void main()
{
    gl_Position = vec4(Position, 0, 1);
    fsin_texCoords = TexCoords;
}";

        private const string FragmentCode = @"
#version 450
layout(location = 0) in vec2 fsin_texCoords;
layout(location = 0) out vec4 fsout_color;
layout(set = 0, binding = 0) uniform texture2D SurfaceTexture;
layout(set = 0, binding = 1) uniform sampler SurfaceSampler;

void main()
{
    fsout_color =  texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_texCoords);
}";

        public Renderer(GraphicsDevice graphicsDevice)
        {
            GfxDevice = graphicsDevice;

            CreateResources();
        }

        private void CreateResources()
        {
            ResourceFactory resourceFactory = GfxDevice.ResourceFactory;

            Vertex[] quadVertices =
            {
                new Vertex(new Vector2(-1f, 1f), new Vector2(-1, 0)),
                new Vertex(new Vector2(1f, 1f), new Vector2(0, 0)),
                new Vertex(new Vector2(-1f, -1f), new Vector2(-1, 1)),
                new Vertex(new Vector2(1f, -1f), new Vector2(0, 1))
            };

            BufferDescription vbDescription = new BufferDescription(4 * 16, BufferUsage.VertexBuffer);
            VertexBuffer = resourceFactory.CreateBuffer(vbDescription);
            GfxDevice.UpdateBuffer(VertexBuffer, 0, quadVertices);

            ushort[] quadIndices = { 0, 1, 2, 3 };
            BufferDescription ibDescription = new BufferDescription(4 * sizeof(ushort), BufferUsage.IndexBuffer);
            IndexBuffer = resourceFactory.CreateBuffer(ibDescription);
            GfxDevice.UpdateBuffer(IndexBuffer, 0, quadIndices);

            VertexLayoutDescription vertexLayout = new VertexLayoutDescription(
                new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2));

            ShaderDescription vertexShaderDesc = new ShaderDescription(
                ShaderStages.Vertex,
                Encoding.UTF8.GetBytes(VertexCode),
                "main");
            ShaderDescription fragmentShaderDesc = new ShaderDescription(
                ShaderStages.Fragment,
                Encoding.UTF8.GetBytes(FragmentCode),
                "main");

            Shaders = resourceFactory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);

            RenderTexture = resourceFactory.CreateTexture(new TextureDescription(160, 144, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
            RenderTextureView = resourceFactory.CreateTextureView(RenderTexture);

            ResourceLayout textureLayout = resourceFactory.CreateResourceLayout(
               new ResourceLayoutDescription(
                   new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                   new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            TextureResourceSet = resourceFactory.CreateResourceSet(new ResourceSetDescription(textureLayout, RenderTextureView, GfxDevice.Aniso4xSampler));

            // Create pipeline
            GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription();
            pipelineDescription.BlendState = BlendStateDescription.SingleOverrideBlend;
            pipelineDescription.RasterizerState = new RasterizerStateDescription(
                cullMode: FaceCullMode.Back,
                fillMode: PolygonFillMode.Solid,
                frontFace: FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: false);
            pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            pipelineDescription.ResourceLayouts = new ResourceLayout[] { textureLayout };
            pipelineDescription.ShaderSet = new ShaderSetDescription(
                vertexLayouts: new VertexLayoutDescription[] { vertexLayout },
                shaders: Shaders);
            pipelineDescription.Outputs = GfxDevice.SwapchainFramebuffer.OutputDescription;

            Pipeline = resourceFactory.CreateGraphicsPipeline(pipelineDescription);

            CommandList = resourceFactory.CreateCommandList();
        }

        public void Draw(byte[] framebuffer)
        {
            unsafe
            {
                fixed (byte* texDataPtr = &framebuffer[0])
                {
                    GfxDevice.UpdateTexture(RenderTexture, (IntPtr)texDataPtr, 4 * 160 * 144, 0, 0, 0, 160, 144, 1, 0, 0);
                }
            }

            // Begin() must be called before commands can be issued.
            CommandList.Begin();

            // We want to render directly to the output window.
            CommandList.SetFramebuffer(GfxDevice.SwapchainFramebuffer);
            CommandList.ClearColorTarget(0, RgbaFloat.Black);

            // Set all relevant state to draw our quad.
            CommandList.SetVertexBuffer(0, VertexBuffer);
            CommandList.SetIndexBuffer(IndexBuffer, IndexFormat.UInt16);
            CommandList.SetPipeline(Pipeline);
            CommandList.SetGraphicsResourceSet(0, TextureResourceSet);
            // Issue a Draw command for a single instance with 4 indices.
            CommandList.DrawIndexed(
                indexCount: 4,
                instanceCount: 1,
                indexStart: 0,
                vertexOffset: 0,
                instanceStart: 0);

            // End() must be called before commands can be submitted for execution.
            CommandList.End();
            GfxDevice.SubmitCommands(CommandList);

            // Once commands have been submitted, the rendered image can be presented to the application window.
            GfxDevice.SwapBuffers();
        }

        public void DisposeResources()
        {
            Pipeline.Dispose();

            foreach (Shader shader in Shaders)
            {
                shader.Dispose();
            }

            CommandList.Dispose();
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            GfxDevice.Dispose();
        }

    }
}
