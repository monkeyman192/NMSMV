using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore;
using MVCore.Common;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace MVCore.Systems
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
        public Vector2 frameDim; //Frame Dimensions
        [FieldOffset(24)]
        public float cameraNearPlane;
        [FieldOffset(28)]
        public float cameraFarPlane;
        [FieldOffset(32)]
        public Matrix4 rotMat;
        [FieldOffset(96)]
        public Matrix4 rotMatInv;
        [FieldOffset(160)]
        public Matrix4 mvp;
        [FieldOffset(224)]
        public Matrix4 lookMatInv;
        [FieldOffset(288)]
        public Matrix4 projMatInv;
        [FieldOffset(352)]
        public Vector4 cameraPositionExposure; //Exposure is the W component
        [FieldOffset(368)]
        public int light_number;
        [FieldOffset(384)]
        public Vector3 cameraDirection;
        [FieldOffset(400)]
        public unsafe fixed float lights[32 * 64];
        //[FieldOffset(400), MarshalAs(UnmanagedType.LPArray, SizeConst=32*64)]
        //public float[] lights;
        public static readonly int SizeInBytes = 8592;
    };

    public class RenderingSystem : EngineSystem, IDisposable
    {
        readonly List<GLInstancedMesh> staticObjectsQueue = new();
        readonly List<GLInstancedMesh> movingMeshQueue = new();

        readonly List<GLInstancedMesh> globalMeshList = new();
        readonly List<GLInstancedMesh> collisionMeshList = new();
        readonly List<GLInstancedMesh> locatorMeshList = new();
        readonly List<GLInstancedMesh> jointMeshList = new();
        readonly List<GLInstancedMesh> lightMeshList = new();
        readonly List<GLInstancedMesh> lightVolumeMeshList = new();

        public ResourceManager resMgr; //REf to the active resource Manager
        
        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        public int last_text_height;
        
        private GBuffer gbuf;
        private PBuffer pbuf;
        private FBO gizmo_fbo;
        private FBO blur_fbo;
        private FBO render_fbo;
        private Vector2i ViewportSize;
        private const int blur_fbo_scale = 2;
        private double gfTime = 0.0f;
        
        private readonly Dictionary<string, int> UBOs = new();
        private readonly Dictionary<string, int> SSBOs = new();

        private int multiBufferActiveId;
        private readonly List<int> multiBufferSSBOs = new(4);
        private readonly List<IntPtr> multiBufferSyncStatuses = new(4);
        
        //Octree Structure
        private Octree octree;

        //UBO structs
        CommonPerFrameUniforms cpfu;
        private byte[] atlas_cpmu;

        private const int MAX_NUMBER_OF_MESHES = 2000;
        private const ulong MAX_OCTREE_WIDTH = 256;
        private const int MULTI_BUFFER_COUNT = 3;
        private DebugProc GLDebug;

        public RenderingSystem() : base(EngineSystemEnum.RENDERING_SYSTEM)
        {

        }

        public void init(ResourceManager input_resMgr, int width, int height)
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
            //Identify System
            Log(string.Format("Renderer {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("Vendor {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("OpenGL Version {0}", GL.GetString(StringName.Version)), LogVerbosityLevel.INFO);
            Log(string.Format("Shading Language Version {0}", GL.GetString(StringName.ShadingLanguageVersion)), LogVerbosityLevel.INFO);

            //Setup Resource Manager
            resMgr = input_resMgr;

            //Wait for the resource Manager to be initialized
            while (!resMgr.initialized)
                continue;
            
            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Setup per Frame UBOs
            setupFrameUBO();

            //Setup SSBOs
            setupSSBOs(2 * 1024 * 1024); //Init SSBOs to 2MB
            multiBufferActiveId = 0;
            SSBOs["_COMMON_PER_MESH"] = multiBufferSSBOs[0];
            
            //Initialize Octree
            octree = new Octree(MAX_OCTREE_WIDTH);

            //Initialize Gbuffer
            setupGBuffer(width, height);

            Log("Resource Manager Initialized", LogVerbosityLevel.INFO);
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

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gbuf = new GBuffer(width, height);
            pbuf = new PBuffer(width, height);
            blur_fbo = new FBO(TextureTarget.Texture2D, 3, width / blur_fbo_scale, height / blur_fbo_scale, false);
            gizmo_fbo = new FBO(TextureTarget.Texture2D, 2, width, height, false);
            render_fbo = new FBO(TextureTarget.Texture2D, 1, width, height, false);

            Log("FBOs Initialized", LogVerbosityLevel.INFO);
        }

        public FBO getRenderFBO()
        {
            return render_fbo;
        }

        public void getMousePosInfo(int x, int y, ref Vector4[] arr)
        {
            //Fetch Depth
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.ReadPixels(x, y, 1, 1, 
                PixelFormat.DepthComponent, PixelType.Float, arr);
            //Fetch color from UI Fbo
        }

        public void progressTime(double dt)
        {
            gfTime += dt;
        }

        public override void CleanUp()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit

            globalMeshList.Clear();
            collisionMeshList.Clear();
            locatorMeshList.Clear();
            jointMeshList.Clear();
            lightMeshList.Clear();
            lightVolumeMeshList.Clear();
            staticObjectsQueue.Clear();
            movingMeshQueue.Clear();
            octree.clear();
        }

        public static void IdentifyActiveShaders()
        {
            RenderState.activeResMgr.GLDeferredShaders.Clear();
            RenderState.activeResMgr.GLDeferredDecalShaders.Clear();
            RenderState.activeResMgr.GLForwardTransparentShaders.Clear();

            foreach (GLSLShaderConfig conf in RenderState.activeResMgr.ShaderMap.Values)
            {
                if ((conf.ShaderMode & SHADER_MODE.FORWARD) == SHADER_MODE.FORWARD)
                    RenderState.activeResMgr.GLForwardTransparentShaders.Add(conf);
                else if ((conf.ShaderMode & SHADER_MODE.DECAL) == SHADER_MODE.DECAL)
                    RenderState.activeResMgr.GLDeferredDecalShaders.Add(conf);
                else
                    RenderState.activeResMgr.GLDeferredShaders.Add(conf);
            }
        }

        public void populate(Scene s)
        {
            CleanUp();

            //Populate octree
            //octree.insert(root);
            //octree.report();
            MeshComponent mc;
            foreach (SceneGraphNode n in s.GetMeshNodes())
            {
                mc = n.GetComponent<MeshComponent>() as MeshComponent;
                process_model(mc);
            }
            
            //Add default light mesh
            mc = resMgr.GLlights[0].GetComponent<MeshComponent>() as MeshComponent;
            process_model(mc);
            
            IdentifyActiveShaders();

        }

        private void process_model(MeshComponent m)
        {
            if (m == null)
                return;

            //Explicitly handle locator, scenes and collision meshes
            switch (m.MeshVao.type)
            {
                case (TYPES.MODEL):
                case (TYPES.LOCATOR):
                case (TYPES.GIZMO):
                    {
                        if (!locatorMeshList.Contains(m.MeshVao))
                            locatorMeshList.Add(m.MeshVao);
                        break;
                    }
                case (TYPES.COLLISION):
                    collisionMeshList.Add(m.MeshVao);
                    break;
                case (TYPES.JOINT):
                    jointMeshList.Add(m.MeshVao);
                    break;
                case (TYPES.LIGHT):
                    lightMeshList.Add(m.MeshVao);
                    break;
                case (TYPES.LIGHTVOLUME):
                    {
                        if (!lightVolumeMeshList.Contains(m.MeshVao))
                            lightVolumeMeshList.Add(m.MeshVao);
                        break;
                    }
                default:
                    {
                        //Add mesh to the corresponding material meshlist
                        if (!resMgr.MaterialMeshMap[m.Material].Contains(m.MeshVao))
                            resMgr.MaterialMeshMap[m.Material].Add(m.MeshVao);
                        break;
                    }
            }

            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(m.MeshVao))
                globalMeshList.Add(m.MeshVao);
            
            //Add meshes to their associated material meshlist
            if (!resMgr.MaterialMeshMap[m.Material].Contains(m.MeshVao))
                resMgr.MaterialMeshMap[m.Material].Add(m.MeshVao);
            
        }

        private void process_models(SceneGraphNode root)
        {
            MeshComponent mc = root.GetComponent<MeshComponent>() as MeshComponent;
            process_model(mc);
            
            //Repeat process with children
            foreach (SceneGraphNode child in root.Children)
            {
                process_models(child);
            }
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
            bindUBOs();
        
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

        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //Prepare Struct
            cpfu.diffuseFlag = (RenderState.settings.renderSettings.UseTextures) ? 1.0f : 0.0f;
            cpfu.use_lighting = (RenderState.settings.renderSettings.UseLighting) ? 1.0f : 0.0f;
            cpfu.frameDim.X = gbuf.size[0];
            cpfu.frameDim.Y = gbuf.size[1];
            cpfu.mvp = RenderState.activeCam.viewMat;
            cpfu.rotMat = RenderState.rotMat;
            cpfu.rotMatInv = RenderState.rotMat.Inverted();
            cpfu.lookMatInv = RenderState.activeCam.lookMatInv;
            cpfu.projMatInv = RenderState.activeCam.projMatInv;
            cpfu.cameraPositionExposure.Xyz = RenderState.activeCam.Position;
            cpfu.cameraPositionExposure.W = RenderState.settings.renderSettings.HDRExposure;
            cpfu.cameraDirection = RenderState.activeCam.Front;
            cpfu.cameraNearPlane = RenderState.activeCam.zNear;
            cpfu.cameraFarPlane = RenderState.activeCam.zFar;
            cpfu.light_number = Math.Min(32, resMgr.GLlights.Count);
            cpfu.gfTime = (float) gfTime;
            cpfu.MSAA_SAMPLES = gbuf.msaa_samples;


            int size = GLLight.SizeInBytes;
            byte[] light_buffer = new byte[size];
            
            //Upload light information
            for (int i = 0; i < Math.Min(32, resMgr.GLlights.Count); i++)
            {
                int offset = (GLLight.SizeInBytes / 4) * i;

                SceneGraphNode l = resMgr.GLlights[i];

                LightComponent lc = l.GetComponent<LightComponent>() as LightComponent;

                /* NEW WAY TESTING
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(l._strct, ptr, true);
                Marshal.Copy(ptr, cpfu.lights, offset, size);
                Marshal.FreeHGlobal(ptr);
                */
                
                //Position : Offset 0
                unsafe {
                    Vector4 localPosition = TransformationSystem.GetEntityWorldPosition(l);
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
                    cpfu.lights[offset + 8] =  lc.Direction.X;
                    cpfu.lights[offset + 9] =  lc.Direction.Y;
                    cpfu.lights[offset + 10] = lc.Direction.Z;
                    cpfu.lights[offset + 11] = lc.FOV;
                    //Falloff: Offset 48(12)
                    cpfu.lights[offset + 12] = (float) lc.Falloff;
                    //Type: Offset 52(13)
                    cpfu.lights[offset + 13] = (float) lc.LightType;
                }

            }
            
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOs["_COMMON_PER_FRAME"]);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, CommonPerFrameUniforms.SizeInBytes, ref cpfu);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        private bool prepareCommonPermeshSSBO(GLInstancedMesh m, ref int UBO_Offset)
        {

            //if (m.instance_count == 0 || m.visible_instances == 0) //use the visible_instance if we maintain an occluded status
            if (m.RenderedInstanceCount == 0)
                return true;

            m.UBO_aligned_size = 0;

            //Calculate aligned size
            int newsize = 4 * m.dataBuffer.Length;
            newsize = ((newsize >> 8) + 1) * 256;
            
            if (newsize + UBO_Offset > atlas_cpmu.Length)
            {
#if DEBUG
                Console.WriteLine("Mesh overload skipping...");
#endif
                return false;
            }

            m.UBO_aligned_size = newsize; //Save new size

            if (m.skinned)
                m.uploadSkinningData();

            if (m.type == TYPES.LIGHTVOLUME)
            {
                ((GLInstancedLightMesh) m).uploadData();
            }

            unsafe
            {
                fixed(void* p = m.dataBuffer)
                {
                    byte* bptr = (byte*) p;

                    Marshal.Copy((IntPtr) p, atlas_cpmu, UBO_Offset, 
                        m.UBO_aligned_size);
                }
            }

            m.UBO_offset = UBO_Offset; //Save offset
            UBO_Offset += m.UBO_aligned_size; //Increase the offset

            return true;
        }

        //This Method binds UBos to binding points
        private void bindUBOs()
        {
            //Bind Matrices
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, UBOs["_COMMON_PER_FRAME"]);
        }

        public void resize(Vector2i size)
        {
            resize(size.X, size.Y);
        }

        public void resize(int w, int h)
        {
            ViewportSize = new Vector2i(w, h);
            gbuf?.resize(w, h);
            pbuf?.resize(w, h);
            render_fbo?.resize(w, h);
            gizmo_fbo?.resize(w, h);
            blur_fbo?.resize(w / blur_fbo_scale, h / blur_fbo_scale);
        }


#region Rendering Methods

        private void sortLights()
        {
            SceneGraphNode mainLight = resMgr.GLlights[0];

            resMgr.GLlights.RemoveAt(0);
            
            resMgr.GLlights.Sort(
                delegate (SceneGraphNode l1, SceneGraphNode l2)
                {
                    float d1 = (TransformationSystem.GetEntityWorldPosition(l1).Xyz - RenderState.activeCam.Position).Length;
                    float d2 = (TransformationSystem.GetEntityWorldPosition(l2).Xyz - RenderState.activeCam.Position).Length;

                    return d1.CompareTo(d2);
                }
            );

            resMgr.GLlights.Insert(0, mainLight);
        }


        private void LOD_filtering(List<GLInstancedMesh> model_list)
        {
            /* TODO : REplace this shit with occlusion based on the instance_ids
            foreach (GLMeshVao m in model_list)
            {
                int i = 0;
                int occluded_instances = 0;
                while (i < m.instance_count)
                {
                    //Skip non LODed meshes
                    if (!m.name.Contains("LOD"))
                    {
                        i++;
                        continue;
                    }

                    //Calculate distance from camera
                    Vector3 bsh_center = m.Bbox[0] + 0.5f * (m.Bbox[1] - m.Bbox[0]);

                    //Move sphere to object's root position
                    Matrix4 mat = m.getInstanceWorldMat(i);
                    bsh_center = (new Vector4(bsh_center, 1.0f) * mat).Xyz;

                    double distance = (bsh_center - Common.RenderState.activeCam.Position).Length;

                    //Find active LOD
                    int active_lod = m.parent.LODNum - 1;
                    for (int j = 0; j < m.parentScene.LODNum - 1; j++)
                    {
                        if (distance < m.parentScene.LODDistances[j])
                        {
                            active_lod = j;
                            break;
                        }
                    }

                    //occlude the other LOD levels
                    for (int j = 0; j < m.parentScene.LODNum; j++)
                    {
                        if (j == active_lod)
                            continue;
                        
                        string lod_text = "LOD" + j;
                        if (m.name.Contains(lod_text))
                        {
                            m.setInstanceOccludedStatus(i, true);
                            occluded_instances++;
                        }
                    }
                    
                    i++;
                }

                if (m.instance_count == occluded_instances)
                    m.occluded = true;
            }
            */
        }

        /* NOT USED
        private void frustum_occlusion(List<GLMeshVao> model_list)
        {
            foreach (GLMeshVao m in model_list)
            {
                int occluded_instances = 0;
                for (int i = 0; i < m.instance_count; i++)
                {
                    if (m.getInstanceOccludedStatus(i))
                        continue;
                    
                    if (!RenderState.activeCam.frustum_occlude(m, i))
                    {
                        occludedNum++;
                        occluded_instances++;
                        m.setInstanceOccludedStatus(i, false);
                    }
                }
            }
        }
        */

        private void prepareCommonPerMeshSSBOs()
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
            foreach (GLInstancedMesh m in globalMeshList)
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



        private void renderDefaultMeshes()
        {
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //Collisions
            if (RenderState.settings.viewSettings.ViewCollisions)
            {
                MeshMaterial mat = resMgr.GLmaterials["collisionMat"];
                GLSLShaderConfig shader = mat.Shader;
                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLInstancedMesh m in collisionMeshList)
                {
                    if (m.RenderedInstanceCount == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    MeshRenderer.render(m, mat, RENDERPASS.DEFERRED);
                }
            }

            //Lights
            if (RenderState.settings.viewSettings.ViewLights)
            {
                MeshMaterial mat = resMgr.GLmaterials["lightMat"];
                GLSLShaderConfig shader = mat.Shader;
                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLInstancedMesh m in lightMeshList)
                {
                    if (m.RenderedInstanceCount == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    MeshRenderer.render(m, mat, RENDERPASS.DEFERRED);
                }
            }

            //Light Volumes
            if (RenderState.settings.viewSettings.ViewLightVolumes)
            {
                MeshMaterial mat = resMgr.GLmaterials["lightMat"];
                GLSLShaderConfig shader = mat.Shader;
                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLInstancedMesh m in lightVolumeMeshList)
                {
                    if (m.RenderedInstanceCount == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    MeshRenderer.render(m, mat, RENDERPASS.DEFERRED);
                }
            }

            //Joints
            if (RenderState.settings.viewSettings.ViewJoints)
            {
                MeshMaterial mat = resMgr.GLmaterials["jointMat"];
                GLSLShaderConfig shader = mat.Shader;

                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLInstancedMesh m in jointMeshList)
                {
                    if (m.RenderedInstanceCount == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    MeshRenderer.render(m, mat, RENDERPASS.DEFERRED);
                }
            }

            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);

            //Locators
            if (RenderState.settings.viewSettings.ViewLocators)
            {
                MeshMaterial mat = resMgr.GLmaterials["crossMat"];
                GLSLShaderConfig shader = mat.Shader;
                //GLSLShaderConfig shader = RenderState.activeResMgr.GLDefaultShaderMap[mat.shaderHash];

                GL.UseProgram(shader.program_id); //Set Program

                //Render static meshes
                foreach (GLInstancedMesh m in locatorMeshList)
                {
                    if (m.RenderedInstanceCount == 0 || m.UBO_aligned_size == 0)
                        continue;

                    GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                        (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    MeshRenderer.render(m, mat, RENDERPASS.DEFERRED);
                }
            }
            
            GL.Enable(EnableCap.CullFace);
        }

        private void renderStaticMeshes()
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);
            
            foreach (GLSLShaderConfig shader in resMgr.GLDeferredShaders)
            {
                GL.UseProgram(shader.program_id); //Set Program

                foreach (MeshMaterial mat in resMgr.ShaderMaterialMap[shader])
                {
                    foreach (GLInstancedMesh mesh in resMgr.MaterialMeshMap[mat])
                    {
                        if (mesh.RenderedInstanceCount == 0 || mesh.UBO_aligned_size == 0)
                            continue;

                        GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                            (IntPtr)(mesh.UBO_offset), mesh.UBO_aligned_size);

                        MeshRenderer.render(mesh, mat, RENDERPASS.DEFERRED);
                    
                        if (RenderState.settings.viewSettings.ViewBoundHulls)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            MeshRenderer.render(mesh, mat, RENDERPASS.BHULL);
                            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);
                        }    
                    }
                }
                
                
                GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs
                
                /*
                //TESTING - Render Bound Boxes for the transparent meshes
                shader = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER];
                GL.UseProgram(shader.program_id);
                
                //I don't expect any other object type here
                foreach (GLMeshVao m in transparentMeshQueue)
                {
                    if (m.instance_count == 0)
                        continue;
                    
                    //GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOs["_COMMON_PER_MESH"],
                    //    (IntPtr)(m.UBO_offset), m.UBO_aligned_size);

                    m.render(shader, RENDERPASS.BHULL);
                    //if (RenderOptions.RenderBoundHulls)
                    //    m.render(RENDERPASS.BHULL);
                }
                */
            }
        }

        private void renderGeometry()
        {
            //DEFERRED STAGE - STATIC MESHES

            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            
            //DEFERRED STAGE
            gbuf.bind();
            gbuf.clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            renderStaticMeshes(); //Deferred Rendered MESHES
            renderDecalMeshes(); //Render Decals
            renderDefaultMeshes(); //Collisions, Locators, Joints
            
            
            renderDeferredLightPass(); //Deferred Lighting Pass to pbuf

            //FORWARD STAGE - TRANSPARENT MESHES
            //renderTransparent(); //Directly to Pbuf

            //Setup FENCE AFTER ALL THE MAIN GEOMETRY DRAWCALLS ARE ISSUED
            multiBufferSyncStatuses[multiBufferActiveId] = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);

        }
        
        private void renderDecalMeshes()
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            
            foreach (GLSLShaderConfig shader in RenderState.activeResMgr.GLDeferredDecalShaders)
            {
                GL.UseProgram(shader.program_id);
                //Upload depth texture to the shader

                //Bind Depth Buffer
                GL.Uniform1(shader.uniformLocations["mpCommonPerFrameSamplers.depthMap"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.Texture2D, gbuf.depth);

                foreach (MeshMaterial mat in resMgr.ShaderMaterialMap[shader])
                {
                    foreach (GLInstancedMesh mesh in resMgr.MaterialMeshMap[mat])
                    {
                        if (mesh.RenderedInstanceCount == 0 || mesh.UBO_aligned_size == 0)
                            continue;

                        GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, 
                            SSBOs["_COMMON_PER_MESH"], (IntPtr)(mesh.UBO_offset), mesh.UBO_aligned_size);
                        MeshRenderer.render(mesh, mat, RENDERPASS.DECAL);    
                    }
                }
            }
            
            GL.Disable(EnableCap.Blend);
            GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
        }

        private void renderTransparent()
        {
            //Copy depth channel from gbuf to pbuf
            FBO.copyDepthChannel(gbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], gbuf.size[0], gbuf.size[1]);

            //Render the first pass in the first channel of the pbuf
            GL.ClearTexImage(pbuf.blur1, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.ClearTexImage(pbuf.blur2, 0, PixelFormat.Rgba, PixelType.Float, new float[] { 1.0f, 1.0f ,1.0f, 1.0f});

            //Enable writing to both channels after clearing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffers(2, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment1,
                                          DrawBuffersEnum.ColorAttachment2});
            
            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest); //Enable depth test
            //Set BlendFuncs for the 2 drawbuffers
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);
            GL.BlendFunc(1, BlendingFactorSrc.Zero, BlendingFactorDest.OneMinusSrcAlpha);

            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);

            foreach (GLSLShaderConfig shader in resMgr.GLForwardTransparentShaders)
            {
                GL.UseProgram(shader.program_id); //Set Program

                foreach (MeshMaterial mat in resMgr.ShaderMaterialMap[shader])
                {
                    foreach (GLInstancedMesh mesh in resMgr.MaterialMeshMap[mat])
                    {
                        if (mesh.RenderedInstanceCount == 0 || mesh.UBO_aligned_size == 0)
                            continue;
                    
                        GL.BindBufferRange(BufferRangeTarget.ShaderStorageBuffer, 1, SSBOs["_COMMON_PER_MESH"],
                            (IntPtr)(mesh.UBO_offset), mesh.UBO_aligned_size);

                        MeshRenderer.render(mesh,mat, RENDERPASS.FORWARD);
                        //if (RenderOptions.RenderBoundHulls)
                        //    m.render(shader, RENDERPASS.BHULL);    
                    }
                    GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs
                }
            }
            
            GL.DepthMask(true); //Re-enable depth buffer
            
            //Composite Step
            GLSLShaderConfig bwoit_composite_shader = RenderState.activeResMgr.GenericShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER];

            //Draw to main color channel
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlendFunc(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha); //Set compositing blend func
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); //Set compositing blend func
            render_quad(Array.Empty<string>(), 
                        Array.Empty<float>(), 
                        new string[] { "in1Tex", "in2Tex" }, 
                        new TextureTarget[] {TextureTarget.Texture2D, TextureTarget.Texture2D },
                        new int[] { pbuf.blur1, pbuf.blur2 }, 
                        bwoit_composite_shader);
            GL.Disable(EnableCap.Blend);
        }

        
        
        private void renderFinalPass()
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, render_fbo.fbo);
            GL.BlitFramebuffer(0, 0, pbuf.size[0], pbuf.size[1], 0, 0, render_fbo.size_x, render_fbo.size_y, 
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        }
        
        private void renderShadows()
        {

        }

        //Rendering Mechanism
        public void testrender(double dt)
        {
            gfTime += dt; //Update render time

            //Console.WriteLine("Rendering Frame");
            GL.ClearColor(new Color4(5, 5, 5, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            //Prepare UBOs
            prepareCommonPerFrameUBO();

            //Prepare Mesh UBO
            prepareCommonPerMeshSSBOs();

            //Render Geometry
            renderGeometry();

            //Pass result to Render FBO
            renderFinalPass();
            

            //Pass Result to Render FBO
            //Render to render_fbo
            //GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, render_fbo.fbo);
            //GL.Viewport(0, 0, ViewportSize.X, ViewportSize.Y);
            //render_quad(Array.Empty<string>(), Array.Empty<float>(), Array.Empty<string>(), Array.Empty<TextureTarget>(), Array.Empty<int>(), resMgr.GLShaders[SHADER_TYPE.RED_FILL_SHADER]);

        }

        public void render()
        {
            //Prepare UBOs
            prepareCommonPerFrameUBO();
            
            //Render Shadows
            renderShadows();

            //Sort Lights
            sortLights();
            
            //Sort Transparent Objects
            //sortTransparent(); //NOT NEEDED ANYMORE
            
            //LOD filtering
            if (RenderState.settings.renderSettings.LODFiltering)
            {
                //LOD_filtering(staticMeshQueue); TODO: FIX
                //LOD_filtering(transparentMeshQueue); TODO: FIX
            }

            //Prepare Mesh UBO
            prepareCommonPerMeshSSBOs();
            
            //Render octree
            //octree.render(resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER].program_id);

            //Render Geometry
            renderGeometry();

            //Light Pass


            //POST-PROCESSING
            post_process();

            //Final Pass
            renderFinalPass();

            //Render UI();
            //UI Rendering is handled for now by the Window. We'll see if this has to be brought back
            
        }

        private void render_lights()
        {
            for (int i = 0; i < resMgr.GLlights.Count; i++)
            {
                SceneGraphNode l = resMgr.GLlights[i];

                //Fetch MeshComponent
                MeshComponent mc = l.GetComponent<MeshComponent>() as MeshComponent;
                MeshRenderer.render(mc, RENDERPASS.DEFERRED); //Render Light
            }
        }

        /*
        private void render_cameras()
        {
            int active_program = resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BBOX_SHADER].program_id;

            GL.UseProgram(active_program);
            int loc;
            //Send object world Matrix to all shaders


            foreach (Camera cam in resMgr.GLCameras)
            {
                //Old rendering the inverse clip space
                //Upload uniforms
                //loc = GL.GetUniformLocation(active_program, "self_mvp");
                //Matrix4 self_mvp = cam.viewMat;
                //GL.UniformMatrix4(loc, false, ref self_mvp);

                //New rendering the exact frustum plane
                loc = GL.GetUniformLocation(active_program, "worldMat");
                Matrix4 test = Matrix4.Identity;
                test[0, 0] = -1.0f;
                test[1, 1] = -1.0f;
                test[2, 2] = -1.0f;
                GL.UniformMatrix4(loc, false, ref test);

                //Render all inactive cameras
                if (!cam.isActive) cam.render();
            
            }

        }
        */

        private void render_quad(string[] uniforms, float[] uniform_values, string[] sampler_names, TextureTarget[] sampler_targets, int[] texture_ids, GLSLHelper.GLSLShaderConfig shaderConf)
        {
            int quad_vao = resMgr.GLPrimitiveVaos["default_renderquad"].vao_id;

            GL.UseProgram(shaderConf.program_id);
            GL.BindVertexArray(quad_vao);

            //Upload samplers
            for (int i = 0; i < sampler_names.Length; i++)
            {
                GL.Uniform1(shaderConf.uniformLocations[sampler_names[i]], i);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(sampler_targets[i], texture_ids[i]);
            }

            //Upload uniforms - Assuming single float uniforms for now
            for (int i = 0; i < uniforms.Length; i++)
                GL.Uniform1(shaderConf.uniformLocations[uniforms[i]], uniform_values[i]);

            //Render quad
            GL.Disable(EnableCap.DepthTest);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.DepthTest);

        }

        private void pass_tex(int to_fbo, DrawBufferMode to_channel, int InTex, int[] to_buf_size)
        {
            //passthrough a texture to the specified to_channel of the to_fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GLSLShaderConfig shader = RenderState.activeResMgr.GenericShaders[SHADER_TYPE.PASSTHROUGH_SHADER];
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "InTex" }, new TextureTarget[] { TextureTarget.Texture2D },  new int[] { InTex }, shader);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }

        private void bloom()
        {
            //Load Programs
            GLSLShaderConfig gs_horizontal_blur_program = resMgr.GenericShaders[SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER];
            GLSLShaderConfig gs_vertical_blur_program = resMgr.GenericShaders[SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER];
            GLSLShaderConfig br_extract_program = resMgr.GenericShaders[SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER];
            GLSLShaderConfig add_program = resMgr.GenericShaders[SHADER_TYPE.ADDITIVE_BLEND_SHADER];
            
            GL.Disable(EnableCap.DepthTest);

            //Copy Color to blur fbo channel 1
            FBO.copyChannel(pbuf.fbo, blur_fbo.fbo, gbuf.size[0], gbuf.size[1], blur_fbo.size_x, blur_fbo.size_y,
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
            //pass_tex(blur_fbo.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, new int[] { blur_fbo.size_x, blur_fbo.size_y });

            //Extract Brightness on the blur buffer and write it to channel 0
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Write to blur1
            
            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[1] }, br_extract_program);



            //Copy Color to blur fbo channel 1
            //FBO.copyChannel(blur_fbo.fbo, pbuf.fbo, blur_fbo.size_x, blur_fbo.size_y, gbuf.size[0], gbuf.size[1],
            //    ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

            //return;

            //Console.WriteLine(GL.GetError()); 

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
            GL.Viewport(0, 0, blur_fbo.size_x, blur_fbo.size_y); //Change the viewport
            int blur_amount = 2;
            for (int i=0; i < blur_amount; i++)
            {
                //Step 1- Apply horizontal blur
                GL.DrawBuffer(DrawBufferMode.ColorAttachment1); //blur2
                GL.Clear(ClearBufferMask.ColorBufferBit);
                
                render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[0]}, gs_horizontal_blur_program);

                //Step 2- Apply horizontal blur
                GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //blur2
                GL.Clear(ClearBufferMask.ColorBufferBit);

                render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[1] }, gs_vertical_blur_program);
            }

            //Blit to screen
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
            GL.Clear(ClearBufferMask.ColorBufferBit); //Clear Screen
            
            GL.BlitFramebuffer(0, 0, blur_fbo.size_x, blur_fbo.size_y, 0, 0, pbuf.size[0], pbuf.size[1],
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
            GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]); //Restore viewport

            //Save Color to blur2 so that we can composite on the main channel
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0); //color
            GL.DrawBuffer(DrawBufferMode.ColorAttachment2); //blur2
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "in1Tex", "in2Tex" }, new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D }, new int[] { pbuf.blur2, pbuf.blur1 }, add_program);
            //render_quad(new string[] { }, new float[] { }, new string[] { "blurTex" }, new int[] { pbuf.blur1 }, gs_bloom_program);

        }

        private void fxaa()
        {
            //inv_tone_mapping(); //Apply tone mapping pbuf.color shoud be ready
            
            //Load Programs
            GLSLShaderConfig fxaa_program = resMgr.GenericShaders[SHADER_TYPE.FXAA_SHADER];

            //Copy Color to first channel
            FBO.copyChannel(pbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], pbuf.size[0], pbuf.size[1],
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
            //pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size);

            //Apply FXAA
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, fxaa_program);

            //tone_mapping(); //Invert Tone Mapping

        }

        private void tone_mapping()
        {
            //Load Programs
            GLSLShaderConfig tone_mapping_program = resMgr.GenericShaders[SHADER_TYPE.TONE_MAPPING];

            //Copy Color to first channel
            pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

            //Apply Tone Mapping
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, tone_mapping_program);

        }

        private void inv_tone_mapping()
        {
            //Load Programs
            GLSLShaderConfig inv_tone_mapping_program = resMgr.GenericShaders[SHADER_TYPE.INV_TONE_MAPPING];

            //Copy Color to first channel
            pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

            //Apply Tone Mapping
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, inv_tone_mapping_program);

        }

        private void post_process()
        {
            //Actuall Post Process effects in AA space without tone mapping
            if (RenderState.settings.renderSettings.UseBLOOM)
                bloom(); //BLOOM

            tone_mapping(); //FINAL TONE MAPPING, INCLUDES GAMMA CORRECTION

            if (RenderState.settings.renderSettings.UseFXAA)
                fxaa(); //FXAA (INCLUDING TONE/UNTONE)
        }

        private void backupDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.fbo);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            
        }

        private void restoreDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gbuf.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gbuf.fbo);
            GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        private void renderDeferredLightPass()
        {
            
            /*
            GLSLShaderConfig shader_conf = resMgr.GLShaders[SHADER_TYPE.GBUFFER_SHADER];

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            //TEST DRAW TO SCREEN
            //GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            //GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            render_quad(new string[] { }, new float[] { }, new string[] { "albedoTex", "depthTex", "normalTex", "parameterTex"},
                                                            new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D},
                                                            new int[] { gbuf.albedo, gbuf.depth, gbuf.normals, gbuf.info}, shader_conf);
            */

            //Render Light volume
            GLSLShaderConfig shader_conf = resMgr.GenericShaders[SHADER_TYPE.LIGHT_PASS_LIT_SHADER];


            //At first blit the albedo (gbuf 0) -> channel 0 of the pbuf
            FBO.copyChannel(gbuf.fbo, pbuf.fbo, gbuf.size[0], gbuf.size[1], gbuf.size[0], gbuf.size[1],
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            GL.Clear(ClearBufferMask.DepthBufferBit);
            
            //Enable Blend
            //At first render the static meshes
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            

            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(0, BlendingFactorSrc.One, BlendingFactorDest.One);

            //Disable DepthTest and Depth Write
            GL.DepthMask(false);
            GL.Disable(EnableCap.DepthTest);


            GLInstancedLightMesh mesh = resMgr.GLPrimitiveMeshes["default_light_sphere"] as GLInstancedLightMesh;

            GL.UseProgram(shader_conf.program_id);

            //Upload samplers
            string[] sampler_names = new string[] { "albedoTex", "depthTex", "normalTex", "parameterTex" };
            int[] texture_ids = new int[] { gbuf.albedo, gbuf.depth, gbuf.normals, gbuf.info };
            TextureTarget[] sampler_targets = new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D};
            for (int i = 0; i < sampler_names.Length; i++)
            {
                GL.Uniform1(shader_conf.uniformLocations[sampler_names[i]], i);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(sampler_targets[i], texture_ids[i]);
            }
            
            if (mesh.RenderedInstanceCount > 0) 
                MeshRenderer.renderMesh(mesh);

            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);

        }

#endregion Rendering Methods

#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    CleanUp(); //Clean local resources
                    gbuf.Dispose(); //Dispose gbuffer
                    shdwRenderer.Dispose(); //Dispose shadowRenderer
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
#endregion

    }

}
