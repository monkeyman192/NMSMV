using System;
using OpenTK.Graphics.OpenGL4;

namespace MVCore
{
    public class GLInstancedLightMesh : GLInstancedMesh
    {
        public const int MAX_INSTANCED = 1024; //Max Instances for lights

        public int instanceLightTex;
        public int instanceLightTexTBO;


        public GLInstancedLightMesh() : base()
        {
            initializeLightTex();
        }

        public GLInstancedLightMesh(MeshMetaData data) : base(data)
        {
            initializeLightTex();
        }

        public void initializeLightTex()
        {
            //Setup the TBO
            instanceLightTex = GL.GenTexture();
            instanceLightTexTBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.TextureBuffer, instanceLightTexTBO);
            GL.BufferData(BufferTarget.TextureBuffer, dataBuffer.Length * sizeof(float), dataBuffer, BufferUsageHint.StreamDraw);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceLightTexTBO);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }

        public void uploadData()
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, instanceLightTexTBO);

            int gpuBuffSize = 0;
            GL.GetBufferParameter(BufferTarget.TextureBuffer, BufferParameterName.BufferSize, out gpuBuffSize);

            int cpuBuffSize = dataBuffer.Length * sizeof(float);

            //Check if the buffer has to be resized
            if (cpuBuffSize > gpuBuffSize)
            {
                GL.BufferData(BufferTarget.TextureBuffer, cpuBuffSize, dataBuffer, BufferUsageHint.StreamDraw);
            }
            else
            {
                int bufferSize = instance_count * GLLightBufferManager.instance_struct_size_bytes;
                GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, dataBuffer);
            }
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }

    }
}
