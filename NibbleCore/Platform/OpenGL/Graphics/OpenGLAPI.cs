using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using NbCore;
using NbCore.Systems;
using NbCore.Math;
using NbCore.Common;
using NbCore.Platform.Graphics;
using OpenTK.Graphics.OpenGL4;


namespace NbCore.Platform.Graphics.OpenGL
{
    //Framebuffer Structs
    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerFrameSamplers
    {
        [FieldOffset(0)]
        public int depthMap; //Depth Map Sampler ID
        public static readonly int SizeInBytes = 12;
    };

    [StructLayout(LayoutKind.Explicit)]
    struct CommonPerFrameUniforms
    {
        [FieldOffset(0)]
        public float diffuseFlag; //Enable Textures
        [FieldOffset(4)]
        public float use_lighting; //Enable lighting
        [FieldOffset(8)]
        public float gfTime; //Fractional Time
        [FieldOffset(12)]
        public float MSAA_SAMPLES; //MSAA Samples
        [FieldOffset(16)]
        public OpenTK.Mathematics.Vector2 frameDim; //Frame Dimensions
        [FieldOffset(24)]
        public float cameraNearPlane;
        [FieldOffset(28)]
        public float cameraFarPlane;
        [FieldOffset(32)]
        public OpenTK.Mathematics.Matrix4 rotMat;
        [FieldOffset(96)]
        public OpenTK.Mathematics.Matrix4 rotMatInv;
        [FieldOffset(160)]
        public OpenTK.Mathematics.Matrix4 mvp;
        [FieldOffset(224)]
        public OpenTK.Mathematics.Matrix4 lookMatInv;
        [FieldOffset(288)]
        public OpenTK.Mathematics.Matrix4 projMatInv;
        [FieldOffset(352)]
        public OpenTK.Mathematics.Vector4 cameraPositionExposure; //Exposure is the W component
        [FieldOffset(368)]
        public int light_number;
        [FieldOffset(384)]
        public OpenTK.Mathematics.Vector3 cameraDirection;
        [FieldOffset(400)]
        public unsafe fixed float lights[32 * 64];
        //[FieldOffset(400), MarshalAs(UnmanagedType.LPArray, SizeConst=32*64)]
        //public float[] lights;
        public static readonly int SizeInBytes = 8592;
    };

    public class GraphicsAPI : IGraphicsApi
    {
        private const string RendererName = "OpenGL Renderer";
        private int activeProgramID = -1;
        public Dictionary<ulong, GLInstancedMesh> MeshMap = new();
        private readonly Dictionary<string, int> UBOs = new();
        private readonly Dictionary<string, int> SSBOs = new();

        private int multiBufferActiveId;
        private readonly List<int> multiBufferSSBOs = new(4);
        private readonly List<IntPtr> multiBufferSyncStatuses = new(4);

        private static readonly Dictionary<NbTextureTarget, TextureTarget> TextureTargetMap = new()
        {
            {NbTextureTarget.Texture1D , TextureTarget.Texture1D},
            {NbTextureTarget.Texture2D , TextureTarget.Texture2D},
            {NbTextureTarget.Texture2DArray , TextureTarget.Texture2DArray }
        };

        //UBO structs
        CommonPerFrameUniforms cpfu;
        private byte[] atlas_cpmu;

        private const int MAX_NUMBER_OF_MESHES = 2000;
        private const int MULTI_BUFFER_COUNT = 3;

        private DebugProc GLDebug;

        private void Log(string msg, LogVerbosityLevel lvl)
        {
            NbCore.Common.Callbacks.Log(string.Format("* {0} : {1}", RendererName, msg), lvl);
        }

        private void GLDebugMessage(DebugSource source, DebugType type, int id, DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            bool report = false;
            switch (severity)
            {
                case DebugSeverity.DebugSeverityHigh:
                    report = true;
                    break;
            }

            if (report)
            {
                string msg = source == DebugSource.DebugSourceApplication ?
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}" :
                $"openGL - {Marshal.PtrToStringAnsi(message, length)}\n\tid:{id} severity:{severity} type:{type} source:{source}\n";

                Log(msg, LogVerbosityLevel.DEBUG);
            }
        }

        public void Init()
        {
#if (DEBUG)
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);

            GLDebug = new DebugProc(GLDebugMessage);

            GL.DebugMessageCallback(GLDebug, IntPtr.Zero);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare,
                DebugSeverityControl.DontCare, 0, new int[] { 0 }, true);

            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
#endif
            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs
            setupSSBOs(2 * 1024 * 1024); //Init SSBOs to 2MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];
        }

        public void SetProgram(int program_id)
        {
            if (activeProgramID != program_id)
                GL.UseProgram(program_id); //Set Program if not already active
        }

        private void setupSSBOs(int size)
        {
            //Allocate space for lights in the framebuffer. TODO: Remove that shit
            //cpfu.lights = new float[32 * 64];

            //Allocate atlas
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes;
            //int atlas_ssbo_buffer_size = MAX_NUMBER_OF_MESHES * CommonPerMeshUniformsInstanced.SizeInBytes; //256 MB just to play safe
            //OpenGL Spec max size for the SSBO is 128 MB, lets stick to that
            atlas_cpmu = new byte[size];

            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
            {
                int ssbo_id = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_id);
                GL.BufferStorage(BufferTarget.ShaderStorageBuffer, size,
                    IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapWriteBit);
                //GL.BufferData(BufferTarget.UniformBuffer, atlas_ubo_buffer_size, IntPtr.Zero, BufferUsageHint.StreamDraw); //FOR OLD METHOD
                multiBufferSSBOs.Add(ssbo_id);
                multiBufferSyncStatuses.Add(GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0));
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            GL.Flush();
        }

        private void deleteSSBOs()
        {
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            //Generate 3 Buffers for the Triple buffering UBO
            for (int i = 0; i < MULTI_BUFFER_COUNT; i++)
                GL.DeleteBuffer(multiBufferSSBOs[i]);
        }

        private void resizeSSBOs(int size)
        {
            deleteSSBOs();
            atlas_cpmu = new byte[size];
            setupSSBOs(size);
        }

        private void setupFrameUBO()
        {
            int ubo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, ubo_id);
            GL.BufferData(BufferTarget.UniformBuffer, CommonPerFrameUniforms.SizeInBytes, IntPtr.Zero, BufferUsageHint.StreamRead);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);

            //Store buffer to UBO dictionary
            UBOs["_COMMON_PER_FRAME"] = ubo_id;

            //Attach the generated buffers to the binding points
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
        }

        private void setupSceneSSBO(int size)
        {
            int ssbo_id = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_id);
            GL.BufferStorage(BufferTarget.ShaderStorageBuffer, size,
                IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapWriteBit);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            //Store buffer to UBO dictionary
            UBOs["_COMMON_PER_SCENE"] = ssbo_id;
            
            //Attach the generated buffers to the binding points
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_SCENE"]);
        }

        private bool prepareCommonPermeshSSBO(GLInstancedMesh m, ref int UBO_Offset)
        {
            //if (m.instance_count == 0 || m.visible_instances == 0) //use the visible_instance if we maintain an occluded status
            if (m.Mesh.InstanceCount == 0)
                return true;

            m.UBO_aligned_size = 0;

            //Calculate aligned size
            int newsize = 4 * m.Mesh.InstanceDataBuffer.Length;
            newsize = ((newsize >> 8) + 1) * 256;

            if (newsize + UBO_Offset > atlas_cpmu.Length)
            {
#if DEBUG
                Console.WriteLine("Mesh overload skipping...");
#endif
                return false;
            }

            m.UBO_aligned_size = newsize; //Save new size

            if (m.Mesh.Type == NbMeshType.LightVolume)
            {
                ((GLInstancedLightMesh)m).uploadData();
            }

            unsafe
            {
                fixed (void* p = m.Mesh.InstanceDataBuffer)
                {
                    byte* bptr = (byte*)p;

                    Marshal.Copy((IntPtr)p, atlas_cpmu, UBO_Offset,
                        m.UBO_aligned_size);
                }
            }

            m.UBO_offset = UBO_Offset; //Save offset
            UBO_Offset += m.UBO_aligned_size; //Increase the offset

            return true;
        }

        public void PrepareMeshBuffers()
        {
            multiBufferActiveId = (multiBufferActiveId + 1) % MULTI_BUFFER_COUNT;

            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[multiBufferActiveId];

            WaitSyncStatus result = WaitSyncStatus.WaitFailed;
            while (result == WaitSyncStatus.TimeoutExpired || result == WaitSyncStatus.WaitFailed)
            {
                //Callbacks.Log(result.ToString());
                //Console.WriteLine("Gamithike o dias");
                result = GL.ClientWaitSync(multiBufferSyncStatuses[multiBufferActiveId], 0, 10);
            }

            GL.DeleteSync(multiBufferSyncStatuses[multiBufferActiveId]);

            //Upload atlas UBO data
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);

            //Prepare UBO data
            int ubo_offset = 0;
            int max_ubo_offset = atlas_cpmu.Length;
            //int max_ubo_offset = 1024 * 1024 * 32;

            //METHOD 2: Use MAP Buffer
            IntPtr ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                 max_ubo_offset, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            //Upload Meshes
            bool atlas_fine = true;
            foreach (GLInstancedMesh m in MeshMap.Values)
            {
                atlas_fine &= prepareCommonPermeshSSBO(m, ref ubo_offset);
            }

            //Console.WriteLine("ATLAS SIZE ORIGINAL: " +  atlas_cpmu.Length + " vs  OFFSET " + ubo_offset);

            if (ubo_offset > 0.9 * atlas_cpmu.Length)
            {
                int new_size = atlas_cpmu.Length + (int)(0.25 * atlas_cpmu.Length);
                //Unmap and unbind buffer
                GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

                resizeSSBOs(new_size);

                //Remap and rebind buffer at the current index
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOs["_COMMON_PER_MESH"]);
                ptr = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                new_size, BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapWriteBit);

            }

            if (ubo_offset != 0)
            {
#if (DEBUG)
                if (ubo_offset > max_ubo_offset)
                    Console.WriteLine("GAMITHIKE O DIAS");
#endif
                //at this point the ubo_offset is the actual size of the atlas buffer

                unsafe
                {
                    Marshal.Copy(atlas_cpmu, 0, ptr, ubo_offset);
                }
            }

            GL.UnmapBuffer(BufferTarget.ShaderStorageBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        }

        public void UploadFrameData()
        {
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_FRAME"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        public void SetRenderSettings(RenderSettings settings)
        {
            //Prepare Struct
            cpfu.diffuseFlag = (RenderState.settings.renderSettings.UseTextures) ? 1.0f : 0.0f;
            cpfu.use_lighting = (RenderState.settings.renderSettings.UseLighting) ? 1.0f : 0.0f;
            cpfu.cameraPositionExposure.W = RenderState.settings.renderSettings.HDRExposure;
        }

        public void SetCameraData(Camera cam)
        {
            cpfu.mvp = RenderState.activeCam.viewMat._Value;
            cpfu.lookMatInv = RenderState.activeCam.lookMatInv._Value;
            cpfu.projMatInv = RenderState.activeCam.projMatInv._Value;
            cpfu.cameraPositionExposure.Xyz = RenderState.activeCam.Position._Value;
            cpfu.cameraDirection = RenderState.activeCam.Front._Value;
            cpfu.cameraNearPlane = RenderState.activeCam.zNear;
            cpfu.cameraFarPlane = RenderState.activeCam.zFar;
        }

        public void SetCommonDataPerFrame(FBO gBuffer, NbMatrix4 rotMat, double time)
        {
            cpfu.frameDim.X = gBuffer.Size.X;
            cpfu.frameDim.Y = gBuffer.Size.Y;
            cpfu.rotMat = RenderState.rotMat._Value;
            cpfu.rotMatInv = RenderState.rotMat._Value.Inverted();
            cpfu.gfTime = (float)time;
            cpfu.MSAA_SAMPLES = gBuffer.msaa_samples;
        }

        public void SetLightDataPerFrame(List<Entity> lights)
        {
            cpfu.light_number = System.Math.Min(32, lights.Count);

            //Upload light information  
            for (int i = 0; i < System.Math.Min(32, cpfu.light_number); i++)
            {
                SceneGraphNode l = lights[i] as SceneGraphNode;
                Callbacks.Assert(l != null,
                    "A non scenegraphnode object made it to the list. THis should not happen");

                int offset = (GLLight.SizeInBytes / 4) * i;
                LightComponent lc = l.GetComponent<LightComponent>() as LightComponent;

                /* NEW WAY TESTING
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(l._strct, ptr, true);
                Marshal.Copy(ptr, cpfu.lights, offset, size);
                Marshal.FreeHGlobal(ptr);
                */

                //Position : Offset 0
                unsafe
                {
                    NbVector4 localPosition = TransformationSystem.GetEntityWorldPosition(l);
                    cpfu.lights[offset + 0] = localPosition.X;
                    cpfu.lights[offset + 1] = localPosition.Y;
                    cpfu.lights[offset + 2] = localPosition.Z;
                    cpfu.lights[offset + 3] = l.IsRenderable ? 1.0f : 0.0f;
                    //Color : Offset 16(4)
                    cpfu.lights[offset + 4] = lc.Color.X;
                    cpfu.lights[offset + 5] = lc.Color.Y;
                    cpfu.lights[offset + 6] = lc.Color.Z;
                    cpfu.lights[offset + 7] = lc.Intensity;
                    //Direction: Offset 32(8)
                    cpfu.lights[offset + 8] = lc.Direction.X;
                    cpfu.lights[offset + 9] = lc.Direction.Y;
                    cpfu.lights[offset + 10] = lc.Direction.Z;
                    cpfu.lights[offset + 11] = lc.FOV;
                    //Falloff: Offset 48(12)
                    cpfu.lights[offset + 12] = (float)lc.Falloff;
                    //Type: Offset 52(13)
                    cpfu.lights[offset + 13] = (float)lc.LightType;
                }

            }
        }

        public void ResizeViewport(int w, int h)
        {
            
        }

        public void AddMesh(NbMesh mesh)
        {

            if (mesh.Hash == 0x0)
            {
                Callbacks.Log("Default mesh hash. Something went wrong during mesh generation", LogVerbosityLevel.WARNING);
                return;
            }

            if (MeshMap.ContainsKey(mesh.Hash))
            {
                Callbacks.Log("Mesh Hash already exists in map", LogVerbosityLevel.WARNING);
            }

            //Generate instanced mesh
            GLInstancedMesh imesh = GenerateAPIMesh(mesh);
            MeshMap[mesh.Hash] = imesh;
        }

        public void RenderMesh()
        {
            throw new NotImplementedException();
        }

        private GLInstancedMesh GenerateAPIMesh(NbMesh mesh)
        {
            GLInstancedMesh imesh = new(mesh);
            return imesh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableMaterialProgram(MeshMaterial mat)
        {
            GLSLShaderConfig shader = mat.Shader;
            GL.UseProgram(shader.ProgramID); //Set Program
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnableShaderProgram(GLSLShaderConfig shader)
        {
            GL.UseProgram(shader.ProgramID); //Set Program
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnbindMeshBuffers()
        {
            GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs
        }

        public void RenderQuad(NbMesh quadMesh, GLSLShaderConfig shaderConf, GLSLShaderState state)
        {
            GLInstancedMesh glmesh = MeshMap[quadMesh.Hash];
            
            GL.UseProgram(shaderConf.ProgramID);
            GL.BindVertexArray(glmesh.vao.vao_id);

            //Upload samplers
            int i = 0;
            foreach (KeyValuePair<string, GLSLSamplerState> sstate in state.Samplers)
            {
                GL.Uniform1(shaderConf.uniformLocations[sstate.Key], i);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(TextureTargetMap[sstate.Value.Target], sstate.Value.TextureID);
            }

            //Floats
            foreach (KeyValuePair<string, float> pair in state.Floats)
            {
                GL.Uniform1(shaderConf.uniformLocations[pair.Key], pair.Value);
            }

            //Vec3s
            foreach (KeyValuePair<string, NbVector3> pair in state.Vec3s)
            {
                GL.Uniform3(shaderConf.uniformLocations[pair.Key],
                    pair.Value.X, pair.Value.Y, pair.Value.Z);
            }

            //Vec4s
            foreach (KeyValuePair<string, NbVector4> pair in state.Vec4s)
            {
                GL.Uniform4(shaderConf.uniformLocations[pair.Key],
                    pair.Value.X, pair.Value.Y, pair.Value.Z, pair.Value.W);
            }

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.DepthTest);
            
        }

        public void RenderMesh(NbMesh mesh)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash]; //Fetch GL Mesh

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount);
            GL.BindVertexArray(0);
        }

        //Fetch main VAO
        public static GLVao generateVAO(NbMesh mesh)
        {
            //Generate VAO
            GLVao vao = new();
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);

            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            //Bind vertex buffer
            int size;
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            //Upload Vertex Buffer
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)mesh.Data.VertexBuffer.Length,
                mesh.Data.VertexBuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);

            Common.Callbacks.Assert(size == mesh.Data.VertexBufferStride * (mesh.MetaData.VertrEndGraphics + 1),
                "Mesh metadata does not match the vertex buffer size from the geometry file");

            //Assign VertexAttribPointers
            for (int i = 0; i < mesh.Data.buffers.Length; i++)
            {
                bufInfo buf = mesh.Data.buffers[i];
                VertexAttribPointerType buftype = VertexAttribPointerType.Float; //default
                switch (buf.type)
                {
                    case NbPrimitiveDataType.Double:
                        buftype = VertexAttribPointerType.Double;
                        break;
                    case NbPrimitiveDataType.UnsignedByte:
                        buftype = VertexAttribPointerType.UnsignedByte;
                        break;
                    case NbPrimitiveDataType.Float:
                        buftype = VertexAttribPointerType.Float;
                        break;
                    case NbPrimitiveDataType.HalfFloat:
                        buftype = VertexAttribPointerType.HalfFloat;
                        break;
                    case NbPrimitiveDataType.Int2101010Rev:
                        buftype = VertexAttribPointerType.Int2101010Rev;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                GL.VertexAttribPointer(buf.semantic, buf.count, buftype, buf.normalize, buf.stride, buf.offset);
                GL.EnableVertexAttribArray(buf.semantic);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)mesh.Data.IndexBuffer.Length,
                mesh.Data.IndexBuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            Common.Callbacks.Assert(size == mesh.Data.IndexBuffer.Length,
                "Mesh metadata does not match the index buffer size from the geometry file");

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < mesh.Data.buffers.Length; i++)
                GL.DisableVertexAttribArray(mesh.Data.buffers[i].semantic);

            return vao;
        }

        public void RenderMesh(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash]; //Fetch GL Mesh

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr) glmesh.UBO_offset, glmesh.UBO_aligned_size);

            SetShaderAndUniforms(glmesh, mat); //Set Shader and material uniforms
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                glmesh.Mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero, 
                glmesh.Mesh.InstanceCount);
            GL.BindVertexArray(0);
        }

        public void RenderLocator(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            SetShaderAndUniforms(glmesh, mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6,
                glmesh.IndicesLength, IntPtr.Zero,
                mesh.InstanceCount); //Use Instancing
            GL.BindVertexArray(0);
        }

        public void RenderJoint(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            SetShaderAndUniforms(glmesh, mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, mesh.MetaData.BatchCount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public void RenderCollision(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];
            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(glmesh.vao.vao_id);
            
            //TODO: make sure that primitive collisions have the vertrstartphysics set to 0
    
            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, glmesh.Mesh.MetaData.BatchCount,
                glmesh.IndicesLength, IntPtr.Zero, glmesh.Mesh.InstanceCount, -glmesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount,
                glmesh.IndicesLength, IntPtr.Zero, glmesh.Mesh.InstanceCount, -glmesh.Mesh.MetaData.VertrStartPhysics);
            
            GL.BindVertexArray(0);
        }

        public void RenderLight(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh.Hash];

            if (glmesh.UBO_aligned_size == 0) return;

            //Bind Mesh Buffer
            GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                (IntPtr)(glmesh.UBO_offset), glmesh.UBO_aligned_size);

            SetShaderAndUniforms(glmesh, mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, mesh.InstanceCount);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, mesh.InstanceCount); //Draw both points
            GL.BindVertexArray(0);
        }

        public void RenderLightVolume(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedLightMesh glmesh = MeshMap[mesh.Hash] as GLInstancedLightMesh;
            
            //Upload Material Information
            GLSLShaderConfig shader = mat.Shader;

            //LightInstanceTex
            GL.Uniform1(shader.uniformLocations["lightsTex"], 6);
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.TextureBuffer, glmesh.instanceLightTex);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                SizedInternalFormat.Rgba32f, glmesh.instanceLightTexTBO);

            RenderMesh(glmesh.Mesh, mat);
        }


        public void renderBBoxes(GLInstancedMesh mesh, int pass)
        {
            for (int i = 0; i > mesh.Mesh.InstanceCount; i++)
            {
                renderBbox(mesh.Mesh.instanceRefs[i]);
            }
        }

        public void uploadSkinningData(GLInstancedMesh mesh)
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, mesh.instanceBoneMatricesTexTBO);
            int bufferSize = mesh.Mesh.InstanceCount * 128 * 16 * 4;
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, mesh.Mesh.instanceBoneMatrices);
            //Console.WriteLine(GL.GetError());
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }
        
        public void renderBbox(MeshComponent mc)
        {
            if (mc == null)
                return;

            NbVector4[] tr_AABB = new NbVector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new NbVector4(mc.Mesh.MetaData.AABBMIN, 1.0f);
            tr_AABB[1] = new NbVector4(mc.Mesh.MetaData.AABBMAX, 1.0f);

            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 0.0f);
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 0.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Indices
            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts1.Length;
            GL.GenBuffers(1, out int vb_bbox);
            GL.GenBuffers(1, out int eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts1);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }
    
        public static void renderBHull(GLInstancedMesh mesh)
        {
            if (mesh.bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(mesh.bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength, 
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength, 
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.BindVertexArray(0);
        }


        private void SetShaderAndUniforms(GLInstancedMesh Mesh, MeshMaterial Material)
        {
            GLSLShaderConfig shader = Material.Shader;
            
            //Upload Material Information
            
            //Upload Custom Per Material Uniforms
            foreach (Uniform un in Material.ActiveUniforms)
                GL.Uniform4(un.ShaderLocation, un.Values);
            
            //BIND TEXTURES
            //Diffuse Texture
            foreach (Sampler s in Material.Samplers)
            {
                if (shader.uniformLocations.ContainsKey(s.Name) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name], s.SamplerID);
                    GL.ActiveTexture(s.texUnit);
                    GL.BindTexture(s.Tex.target, s.Tex.texID);
                }
            }
            
            //BIND TEXTURE Buffer
            if (Mesh.Mesh.MetaData.skinned)
            {
                GL.Uniform1(shader.uniformLocations["skinMatsTex"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.TextureBuffer, Mesh.instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                    SizedInternalFormat.Rgba32f, Mesh.instanceBoneMatricesTexTBO);
            }
        }
        
        public void renderMain(GLInstancedLightMesh mesh, MeshMaterial material)
        {
            
        }

        public void SyncGPUCommands()
        {
            //Setup FENCE AFTER ALL THE MAIN GEOMETRY DRAWCALLS ARE ISSUED
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        }

        public void ClearDrawBuffer(NbBufferMask mask)
        {
            ClearBufferMask glmask = ClearBufferMask.None;

            if (mask.HasFlag(NbBufferMask.Color))
                glmask |= ClearBufferMask.ColorBufferBit;
            
            if (mask.HasFlag(NbBufferMask.Color))
                glmask |= ClearBufferMask.DepthBufferBit;

            GL.Clear(glmask);
        }

        public void BindDrawFrameBuffer(FBO fbo, int[] drawBuffers)
        {
            //Bind Gbuffer fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo.fbo);
            GL.Viewport(0, 0, fbo.Size.X, fbo.Size.Y);

            DrawBuffersEnum[] bufferEnums = new DrawBuffersEnum[drawBuffers.Length];

            for (int i = 0; i < drawBuffers.Length; i++)
                bufferEnums[i] = DrawBuffersEnum.ColorAttachment0 + drawBuffers[i];

            GL.DrawBuffers(bufferEnums.Length, bufferEnums);
        }

        public FBO CreateFrameBuffer(int w, int h)
        {
            FBO fbo = new(w,h);

            return fbo;
        }

        #region ShaderMethods

        public int CalculateMaterialShaderhash(MeshMaterial mat, SHADER_MODE mode)
        {
            //Step 1: COmbine Material directives
            List<string> includes = GetMaterialShaderDirectives(mat);
            includes = GLShaderHelper.CombineShaderDirectives(includes, mode);
            return GLShaderHelper.calculateShaderHash(includes);
        }

        public void AttachShaderToMaterial(MeshMaterial mat, GLSLShaderConfig shader)
        {
            mat.Shader = shader;

            //Load Active Uniforms to Material
            foreach (Uniform un in mat.Uniforms)
            {
                if (shader.uniformLocations.ContainsKey(un.Name))
                {
                    un.ShaderLocation = shader.uniformLocations[un.Name];
                    mat.ActiveUniforms.Add(un);
                }
            }

            foreach (Sampler s in mat.Samplers)
            {
                if (shader.uniformLocations.ContainsKey(s.Name))
                {
                    s.ShaderLocation = shader.uniformLocations[s.Name];
                }
            }
            
        }

        public List<string> GetMaterialShaderDirectives(MeshMaterial mat)
        {
            List<string> includes = new();

            for (int i = 0; i < mat.Flags.Count; i++)
            {
                if (MeshMaterial.supported_flags.Contains(mat.Flags[i]))
                    includes.Add(mat.Flags[i].ToString().Split(".")[^1]);
            }

            return includes;
        }

        public GLSLShaderConfig CompileMaterialShader(MeshMaterial mat, SHADER_MODE mode)
        {
            List<string> directives = GetMaterialShaderDirectives(mat);
            
            string vs_path = "Shaders/Simple_VS.glsl";
            vs_path = System.IO.Path.GetFullPath(vs_path);
            vs_path = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);

            string fs_path = "Shaders/Simple_FS.glsl";
            fs_path = System.IO.Path.GetFullPath(fs_path);
            fs_path = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);

            GLSLShaderSource vs = RenderState.engineRef.GetShaderSourceByFilePath(vs_path);
            GLSLShaderSource fs = RenderState.engineRef.GetShaderSourceByFilePath(fs_path);

            GLSLShaderConfig shader = GLShaderHelper.compileShader(vs, fs, null, null, null,
                directives, SHADER_TYPE.MATERIAL_SHADER, mode);

            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);

            return shader;
        }

        #endregion  
    }
}