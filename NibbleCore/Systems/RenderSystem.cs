using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using GLSLHelper;
using NbCore;
using NbCore.Common;
using NbCore.Managers;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using NbCore.Text;

namespace NbCore.Systems
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
        readonly List<GLInstancedMesh> globalMeshList = new();
        readonly List<GLInstancedMesh> collisionMeshList = new();
        readonly List<GLInstancedMesh> locatorMeshList = new();
        readonly List<GLInstancedMesh> jointMeshList = new();
        readonly List<GLInstancedMesh> lightMeshList = new();
        readonly List<SceneGraphNode> LightList = new();
        readonly List<GLInstancedMesh> lightVolumeMeshList = new();

        //Entity Managers used by the rendering system
        public readonly MaterialManager MaterialMgr = new();
        public readonly GeometryManager GeometryMgr = new();
        public readonly TextureManager TextureMgr = new();
        public readonly ShaderManager ShaderMgr = new();
        public readonly FontManager FontMgr = new();

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

        public void init(int width, int height)
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

            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();

            //Add default rendering resources
            CompileMainShaders();
            AddDefaultPrimitives();
            AddDefaultMaterials();
            AddDefaultLights();
            
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

        private void CleanUpGeometry()
        {
            globalMeshList.Clear();
            collisionMeshList.Clear();
            locatorMeshList.Clear();
            jointMeshList.Clear();
            lightMeshList.Clear();
            lightVolumeMeshList.Clear();
            octree.clear();
        }

        public override void CleanUp()
        {
            //Just cleanup the queues
            //The resource manager will handle the cleanup of the buffers and shit
            CleanUpGeometry();
            //Manager Cleanups
            TextureMgr.CleanUp();
            MaterialMgr.CleanUp();
            ShaderMgr.CleanUp();
            
        }
        
        
    
        private void CompileMainShaders()
        {

            //Populate shader list

#if (DEBUG)
            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringNameIndexed.Extensions, 0).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
                if (s.Contains("texture"))
                    Console.WriteLine(s);
                if (s.Contains("16"))
                    Console.WriteLine(s);
            }

            //Query maximum buffer sizes
            Console.WriteLine("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));
#endif

            GLSLHelper.GLSLShaderConfig shader_conf;

            //Geometry Shader
            //Compile Object Shaders
            GLSLShaderSource geometry_shader_vs = new("Shaders/Simple_VSEmpty.glsl", true);
            GLSLShaderSource geometry_shader_fs = new("Shaders/Simple_FSEmpty.glsl", true);
            GLSLShaderSource geometry_shader_gs = new("Shaders/Simple_GS.glsl", true);

            shader_conf = GLShaderHelper.compileShader(geometry_shader_vs, geometry_shader_fs, geometry_shader_gs, null, null,
                            new(), new(), SHADER_TYPE.DEBUG_MESH_SHADER, SHADER_MODE.DEFFERED);

            shader_conf.GetComponent<GUIDComponent>().Dispose();
            shader_conf.Dispose();
            
            //Compile Object Shaders
            GLSLShaderSource gizmo_shader_vs = new("Shaders/Gizmo_VS.glsl", true);
            GLSLShaderSource gizmo_shader_fs = new("Shaders/Gizmo_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gizmo_shader_vs, gizmo_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.GIZMO_SHADER, SHADER_MODE.DEFFERED);

            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GIZMO_SHADER);


#if DEBUG
            //Report UBOs
            GLShaderHelper.reportUBOs(shader_conf);
#endif

            //Picking Shader

            //Compile Default Shaders

            //BoundBox Shader
            GLSLShaderSource bbox_shader_vs = new("Shaders/Bound_VS.glsl", true);
            GLSLShaderSource bbox_shader_fs = new("Shaders/Bound_FS.glsl", true);

            shader_conf = GLShaderHelper.compileShader(bbox_shader_vs, bbox_shader_fs, null, null, null,
                new(), new(), GLSLHelper.SHADER_TYPE.BBOX_SHADER, SHADER_MODE.DEFFERED);
            shader_conf.Dispose();

            //Texture Mixing Shader
            GLSLShaderSource texture_mixing_shader_vs = new("Shaders/texture_mixer_VS.glsl", true);
            GLSLShaderSource texture_mixing_shader_fs = new("Shaders/texture_mixer_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            new(), new(), GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER, SHADER_MODE.DEFAULT);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.TEXTURE_MIX_SHADER);


            //GBuffer Shaders

            GLSLShaderSource gbuffer_shader_vs = new("Shaders/Gbuffer_VS.glsl", true);
            GLSLShaderSource gbuffer_shader_fs = new("Shaders/Gbuffer_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.GBUFFER_SHADER, SHADER_MODE.DEFAULT);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GBUFFER_SHADER);

            //Light Pass Shaders

            //UNLIT
            GLSLShaderSource lpass_shader_vs = new("Shaders/light_pass_VS.glsl", true);
            GLSLShaderSource lpass_shader_fs = new("Shaders/light_pass_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER);


            //LIT
            lpass_shader_vs = new("Shaders/light_pass_VS.glsl", true);
            lpass_shader_fs = new("Shaders/light_pass_FS.glsl", true);
            lpass_shader_fs.AddDirective("_D_LIGHTING");
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.LIGHT_PASS_LIT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.LIGHT_PASS_LIT_SHADER);

            //GAUSSIAN HORIZONTAL BLUR SHADER
            GLSLShaderSource gaussian_blur_shader_fs = new("Shaders/gaussian_horizontalBlur_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
            

            //GAUSSIAN VERTICAL BLUR SHADER
            gaussian_blur_shader_fs = new("Shaders/gaussian_verticalBlur_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            new(), new(),
                            SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER, SHADER_MODE.DEFAULT);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
            
            //BRIGHTNESS EXTRACTION SHADER
            gbuffer_shader_fs = new("Shaders/brightness_extract_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(),
                            SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER);
            
            //ADDITIVE BLEND
            gbuffer_shader_fs = new("Shaders/additive_blend_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(),
                            SHADER_TYPE.ADDITIVE_BLEND_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.ADDITIVE_BLEND_SHADER);

            //FXAA
            gbuffer_shader_fs = new("Shaders/fxaa_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                new(), new(), SHADER_TYPE.FXAA_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.FXAA_SHADER);
            
            //TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_fs = new("Shaders/tone_mapping_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                new(), new(), SHADER_TYPE.TONE_MAPPING, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.TONE_MAPPING);

            //INV TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_fs = new("Shaders/inv_tone_mapping_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.INV_TONE_MAPPING, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.INV_TONE_MAPPING);
            
            //BWOIT SHADER
            gbuffer_shader_fs = new("Shaders/bwoit_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.BWOIT_COMPOSITE_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.BWOIT_COMPOSITE_SHADER);
            
            //Text Shaders
            GLSLShaderSource text_shader_vs = new("Shaders/Text_VS.glsl", true);
            GLSLShaderSource text_shader_fs = new("Shaders/Text_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(text_shader_vs, text_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.TEXT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.TEXT_SHADER);
            
            //FILTERS - EFFECTS

            //Pass Shader
            GLSLShaderSource passthrough_shader_fs = new("Shaders/PassThrough_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.PASSTHROUGH_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.PASSTHROUGH_SHADER);
            
            /*
             * TESTING
             * 
            //Red Shader
            gbuffer_shader_vs = new(ShaderType.VertexShader);
            GLSLShaderText red_shader_fs = new(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            red_shader_fs.addStringFromFile("Shaders/RedFill.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, red_shader_fs, null, null, null,
                            SHADER_TYPE.RED_FILL_SHADER);
            
            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);
            GLShaders[SHADER_TYPE.RED_FILL_SHADER] = shader_conf; 
            */






        }

        private void AddDefaultLights()
        {
            SceneGraphNode light = EngineRef.CreateLightNode("Default Light", 200.0f, ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);
            TransformationSystem.SetEntityLocation(light, new Vector3(100.0f, 100.0f, 100.0f));
            
            EngineRef.RegisterEntity(light);
            LightList.Add(light);
        }

        private void AddDefaultPrimitives()
        {
            //Setup Primitive Vaos

            //Default quad
            Primitives.Quad q = new(1.0f, 1.0f);

            GLVao def_quad = q.getVAO();
            GLInstancedMesh def_quad_mesh = new();
            def_quad_mesh.Name = "default_quad";
            def_quad_mesh.vao = def_quad;

            EngineRef.RegisterEntity(def_quad_mesh);
            GeometryMgr.AddPrimitiveMesh(def_quad_mesh);
            q.Dispose();

            //Default render quad
            q = new Primitives.Quad();
            GLInstancedMesh def_render_quad = new();
            def_render_quad.Name = "default_renderquad";
            def_render_quad.vao = q.getVAO();
            EngineRef.RegisterEntity(def_render_quad);
            GeometryMgr.AddPrimitiveMesh(def_render_quad);
            q.Dispose();
            
            //Default cross
            Primitives.Cross c = new(0.1f, true);
            
            GLInstancedMesh def_cross = new()
            {
                Name = "default_cross",
                type = SceneNodeType.GIZMO,
                vao = c.getVAO(),
                MetaData = new()
                {
                    BatchCount = c.geom.indicesCount,
                    AABBMIN = new Vector3(-0.1f),
                    AABBMAX = new Vector3(0.1f),
                    IndicesLength = DrawElementsType.UnsignedInt,
                }
            };

            EngineRef.RegisterEntity(def_cross);
            GeometryMgr.AddPrimitiveMesh(def_cross);
            c.Dispose();


            //Default cube
            Primitives.Box bx = new(1.0f, 1.0f, 1.0f, new Vector3(1.0f), true);

            GLInstancedMesh def_box = new()
            {
                Name = "default_box",
                vao = bx.getVAO()
            };
            
            EngineRef.RegisterEntity(def_box);
            GeometryMgr.AddPrimitiveMesh(def_box);
            bx.Dispose();
            
            //Default sphere
            Primitives.Sphere sph = new(new Vector3(0.0f, 0.0f, 0.0f), 100.0f);

            GLInstancedMesh def_sph = new()
            {
                Name = "default_sphere",
                vao = sph.getVAO()
            };
            
            EngineRef.RegisterEntity(def_sph);
            GeometryMgr.AddPrimitiveMesh(def_sph);
            sph.Dispose();

            //Light Sphere Mesh
            Primitives.Sphere lsph = new(new Vector3(0.0f, 0.0f, 0.0f), 1.0f);
            GLInstancedLightMesh def_lsph = new()
            {
                Name = "default_light_sphere",
                MetaData = new()
                {
                    BatchStartGraphics = 0,
                    VertrStartGraphics = 0,
                    VertrEndGraphics = 11 * 11 - 1,
                    BatchCount = 10 * 10 * 6,
                    AABBMIN = new(-0.1f),
                    AABBMAX = new(0.1f),
                    IndicesLength = DrawElementsType.UnsignedInt
                },
                type = SceneNodeType.LIGHTVOLUME,
                vao = lsph.getVAO()
            };

            EngineRef.RegisterEntity(def_lsph);
            GeometryMgr.AddPrimitiveMesh(def_lsph);
            lsph.Dispose();
            
            GenerateGizmoParts();
        }

        private void AddDefaultMaterials()
        {
            //Cross Material
            MeshMaterial mat;

            mat = new();
            mat.Name = "crossMat";
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);
            mat.add_flag(MaterialFlagEnum._F21_VERTEXCOLOUR);
            Uniform uf = new()
            {
                Name = "mpCustomPerMaterial.gMaterialColourVec4",
                Values = new(1.0f, 1.0f, 1.0f, 1.0f)
            };
            mat.Uniforms.Add(uf);
            mat.CompileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl");

            EngineRef.RegisterEntity(mat);
            MaterialMgr.AddMaterial(mat);
            
            //Joint Material
            mat = new MeshMaterial
            {
                Name = "jointMat"
            };
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);

            uf = new Uniform();
            uf.Name = "mpCustomPerMaterial.gMaterialColourVec4";
            uf.Values = new(1.0f, 0.0f, 0.0f, 1.0f);
            mat.Uniforms.Add(uf);
            mat.CompileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl");

            EngineRef.RegisterEntity(mat);
            MaterialMgr.AddMaterial(mat);

            //Light Material
            mat = new()
            {
                Name = "lightMat"
            };
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);

            uf = new();
            uf.Name = "mpCustomPerMaterial.gMaterialColourVec4";
            uf.Values = new(1.0f, 1.0f, 0.0f, 1.0f);
            mat.Uniforms.Add(uf);
            mat.CompileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl");

            EngineRef.RegisterEntity(mat);
            MaterialMgr.AddMaterial(mat);

            //Collision Material
            mat = new();
            mat.Name = "collisionMat";
            mat.add_flag(MaterialFlagEnum._F07_UNLIT);

            uf = new();
            uf.Name = "mpCustomPerMaterial.gMaterialColourVec4";
            uf.Values = new(0.8f, 0.8f, 0.2f, 1.0f);
            mat.Uniforms.Add(uf);
            mat.CompileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl");

            EngineRef.RegisterEntity(mat);
            MaterialMgr.AddMaterial(mat);

        }

        private void GenerateGizmoParts()
        {
            //Translation Gizmo
            Primitives.Arrow translation_x_axis = new(0.015f, 0.25f, new Vector3(1.0f, 0.0f, 0.0f), false, 20);
            //Move arrowhead up in place
            Matrix4 t = Matrix4.CreateRotationZ(Utils.MathUtils.radians(90));
            translation_x_axis.applyTransform(t);

            Primitives.Arrow translation_y_axis = new(0.015f, 0.25f, new Vector3(0.0f, 1.0f, 0.0f), false, 20);
            Primitives.Arrow translation_z_axis = new(0.015f, 0.25f, new Vector3(0.0f, 0.0f, 1.0f), false, 20);
            t = Matrix4.CreateRotationX(Utils.MathUtils.radians(90));
            translation_z_axis.applyTransform(t);

            //Generate Geom objects
            translation_x_axis.geom = translation_x_axis.getGeom();
            translation_y_axis.geom = translation_y_axis.getGeom();
            translation_z_axis.geom = translation_z_axis.getGeom();


            GLVao x_axis_vao = translation_x_axis.getVAO();
            GLVao y_axis_vao = translation_y_axis.getVAO();
            GLVao z_axis_vao = translation_z_axis.getVAO();


            //Generate PrimitiveMeshVaos
            for (int i = 0; i < 3; i++)
            {
                string name = "";
                Primitives.Primitive arr = null;
                GLVao vao = null;
                switch (i)
                {
                    case 0:
                        arr = translation_x_axis;
                        name = "default_translation_gizmo_x_axis";
                        vao = x_axis_vao;
                        break;
                    case 1:
                        arr = translation_y_axis;
                        name = "default_translation_gizmo_y_axis";
                        vao = y_axis_vao;
                        break;
                    case 2:
                        arr = translation_z_axis;
                        name = "default_translation_gizmo_z_axis";
                        vao = z_axis_vao;
                        break;
                }

                GLInstancedMesh temp = new()
                {
                    Name = name,
                    type = SceneNodeType.GIZMOPART,
                    vao = vao,
                    MetaData = new()
                    {
                        BatchCount = arr.geom.indicesCount,
                        IndicesLength = DrawElementsType.UnsignedInt,
                    }
                };

                
                if (!GeometryMgr.AddPrimitiveMesh(temp))
                    temp.Dispose();
                arr.Dispose();

            }

        }


        public void populate(Scene s)
        {
            CleanUpGeometry();

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
            var light = EngineRef.GetSceneNodeByNameType(SceneNodeType.LIGHT, "Default Light");
            mc = light.GetComponent<MeshComponent>() as MeshComponent;
            process_model(mc);
            
            ShaderMgr.IdentifyActiveShaders();

        }

        private void process_model(MeshComponent m)
        {
            if (m == null)
                return;

            //Explicitly handle locator, scenes and collision meshes
            switch (m.MeshVao.type)
            {
                case (SceneNodeType.MODEL):
                case (SceneNodeType.LOCATOR):
                case (SceneNodeType.GIZMO):
                    {
                        if (!locatorMeshList.Contains(m.MeshVao))
                            locatorMeshList.Add(m.MeshVao);
                        break;
                    }
                case (SceneNodeType.COLLISION):
                    collisionMeshList.Add(m.MeshVao);
                    break;
                case (SceneNodeType.JOINT):
                    jointMeshList.Add(m.MeshVao);
                    break;
                case (SceneNodeType.LIGHT):
                    lightMeshList.Add(m.MeshVao);
                    break;
                case (SceneNodeType.LIGHTVOLUME):
                    {
                        if (!lightVolumeMeshList.Contains(m.MeshVao))
                            lightVolumeMeshList.Add(m.MeshVao);
                        break;
                    }
                default:
                    {
                        //Add mesh to the corresponding material meshlist
                        if (!MaterialMgr.MaterialContainsMesh(m.Material, m.MeshVao))
                            MaterialMgr.AddMeshToMaterial(m.Material, m.MeshVao);
                        break;
                    }
            }

            //Check if the shader has been registered to the rendering system
            if (!ShaderMgr.ShaderExists(m.Material.Shader.GetID()))
            {
                ShaderMgr.AddShader(m.Material.Shader);
                ShaderMgr.AddMaterialToShader(m.Material);
            }
                
            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(m.MeshVao))
                globalMeshList.Add(m.MeshVao);

            //Add meshes to their associated material meshlist
            if (!MaterialMgr.MaterialContainsMesh(m.Material, m.MeshVao))
                MaterialMgr.AddMeshToMaterial(m.Material, m.MeshVao);
            
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
            cpfu.light_number = Math.Min(32, EngineRef.GetLightCount());
            cpfu.gfTime = (float) gfTime;
            cpfu.MSAA_SAMPLES = gbuf.msaa_samples;


            int size = GLLight.SizeInBytes;
            byte[] light_buffer = new byte[size];
            
            //Upload light information
            List<Entity> lights = EngineRef.GetEntityTypeList(EntityType.SceneNodeLight);
            for (int i = 0; i < Math.Min(32, cpfu.light_number); i++)
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

            if (m.type == SceneNodeType.LIGHTVOLUME)
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
            List<Entity> lights = EngineRef.GetEntityTypeList(EntityType.SceneNodeLight);
            SceneGraphNode mainLight = (SceneGraphNode) lights[0];

            lights.RemoveAt(0);
            
            lights.Sort(
                delegate (Entity e1, Entity e2)
                {
                    SceneGraphNode l1 = (SceneGraphNode) e1;
                    SceneGraphNode l2 = (SceneGraphNode) e2;
                    
                    float d1 = (TransformationSystem.GetEntityWorldPosition(l1).Xyz - RenderState.activeCam.Position).Length;
                    float d2 = (TransformationSystem.GetEntityWorldPosition(l2).Xyz - RenderState.activeCam.Position).Length;

                    return d1.CompareTo(d2);
                }
            );

            lights.Insert(0, mainLight);
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
                MeshMaterial mat = EngineRef.GetMaterialByName("collisionMat");
                GLSLShaderConfig shader = mat.Shader;
                GL.UseProgram(shader.ProgramID); //Set Program

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
                MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
                GLSLShaderConfig shader = mat.Shader;
                GL.UseProgram(shader.ProgramID); //Set Program

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
                MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
                GLSLShaderConfig shader = mat.Shader;
                GL.UseProgram(shader.ProgramID); //Set Program

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
                MeshMaterial mat = EngineRef.GetMaterialByName("jointMat");
                GLSLShaderConfig shader = mat.Shader;

                GL.UseProgram(shader.ProgramID); //Set Program

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
                MeshMaterial mat = EngineRef.GetMaterialByName("crossMat");
                GLSLShaderConfig shader = mat.Shader;
                //GLSLShaderConfig shader = RenderState.activeResMgr.GLDefaultShaderMap[mat.shaderHash];

                GL.UseProgram(shader.ProgramID); //Set Program

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
            
            foreach (GLSLShaderConfig shader in ShaderMgr.GLDeferredShaders)
            {
                GL.UseProgram(shader.ProgramID); //Set Program

                foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
                {
                    foreach (GLInstancedMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
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
            
            foreach (GLSLShaderConfig shader in ShaderMgr.GLDeferredDecalShaders)
            {
                GL.UseProgram(shader.ProgramID);
                //Upload depth texture to the shader

                //Bind Depth Buffer
                GL.Uniform1(shader.uniformLocations["mpCommonPerFrameSamplers.depthMap"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.Texture2D, gbuf.depth);

                foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
                {
                    foreach (GLInstancedMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
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

            foreach (GLSLShaderConfig shader in ShaderMgr.GLForwardTransparentShaders)
            {
                GL.UseProgram(shader.ProgramID); //Set Program

                foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
                {
                    foreach (GLInstancedMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
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
            GLSLShaderConfig bwoit_composite_shader = ShaderMgr.GetGenericShader(SHADER_TYPE.BWOIT_COMPOSITE_SHADER); 
            
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
            List<Entity> lights = EngineRef.GetEntityTypeList(EntityType.SceneNodeLight);
            for (int i = 0; i < lights.Count; i++)
            {
                SceneGraphNode l = (SceneGraphNode) lights[i];

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
            int quad_vao = GeometryMgr.GetPrimitiveMesh("default_renderquad").vao.vao_id;

            GL.UseProgram(shaderConf.ProgramID);
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
            GLSLShaderConfig shader = EngineRef.GetShaderByType(SHADER_TYPE.PASSTHROUGH_SHADER);
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "InTex" }, new TextureTarget[] { TextureTarget.Texture2D },  new int[] { InTex }, shader);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }

        private void bloom()
        {
            //Load Programs
            GLSLShaderConfig gs_horizontal_blur_program = EngineRef.GetShaderByType(SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
            GLSLShaderConfig gs_vertical_blur_program = EngineRef.GetShaderByType(SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER);
            GLSLShaderConfig br_extract_program = EngineRef.GetShaderByType(SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER) ;
            GLSLShaderConfig add_program = EngineRef.GetShaderByType(SHADER_TYPE.ADDITIVE_BLEND_SHADER);
            
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
            GLSLShaderConfig fxaa_program = ShaderMgr.GetGenericShader(SHADER_TYPE.FXAA_SHADER);

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
            GLSLShaderConfig tone_mapping_program = ShaderMgr.GetGenericShader(SHADER_TYPE.TONE_MAPPING);

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
            GLSLShaderConfig inv_tone_mapping_program = ShaderMgr.GetGenericShader(SHADER_TYPE.INV_TONE_MAPPING);

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
            GLSLShaderConfig shader_conf = ShaderMgr.GetGenericShader(SHADER_TYPE.LIGHT_PASS_LIT_SHADER);


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

            
            GLInstancedLightMesh mesh = GeometryMgr.GetPrimitiveMesh("default_light_sphere") as GLInstancedLightMesh;

            GL.UseProgram(shader_conf.ProgramID);

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

        public override void OnRenderUpdate(double dt)
        {
            throw new NotImplementedException();
        }

        public override void OnFrameUpdate(double dt)
        {
            throw new NotImplementedException();
        }
        #endregion

    }

}
