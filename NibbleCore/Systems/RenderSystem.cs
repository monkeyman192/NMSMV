using System;
using System.Collections.Generic;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using NbCore;
using NbCore.Common;
using NbCore.Platform.Graphics;
using NbCore.Managers;
using NbCore.Math;
using OpenTK.Graphics.OpenGL4;

namespace NbCore.Systems
{
    public class RenderingSystem : EngineSystem, IDisposable
    {
        //Entity Registration Management
        private readonly Dictionary<long, MeshComponent> EntityDataMap;
        
        //Mesh Management
        readonly List<NbMesh> globalMeshList = new();
        readonly List<NbMesh> collisionMeshList = new();
        readonly List<NbMesh> locatorMeshList = new();
        readonly List<NbMesh> jointMeshList = new();
        readonly List<NbMesh> lightMeshList = new();
        
        readonly List<SceneGraphNode> LightList = new();
        readonly List<NbMesh> lightVolumeMeshList = new();

        public IGraphicsApi Renderer;
        
        //Entity Managers used by the rendering system
        public readonly MaterialManager MaterialMgr = new();
        public readonly GeometryManager GeometryMgr = new();
        public readonly TextureManager TextureMgr = new();
        public readonly ShaderManager ShaderMgr = new();
        public readonly FontManager FontMgr = new();

        public ShadowRenderer shdwRenderer; //Shadow Renderer instance
        //Control Font and Text Objects
        public int last_text_height;
        
        private NbVector2i ViewportSize;
        private const int blur_fbo_scale = 2;
        private double gfTime = 0.0f;

        //Render Buffers
        private FBO gBuffer;
        private FBO renderBuffer;
        
        
        private const ulong MAX_OCTREE_WIDTH = 256;

        //Octree Structure
        private Octree octree;

        public RenderingSystem() : base(EngineSystemEnum.RENDERING_SYSTEM)
        {
            EntityDataMap = new();
        }

        public void init(int width, int height)
        {
            
            //Identify System
            Log(string.Format("Renderer {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("Vendor {0}", GL.GetString(StringName.Vendor)), LogVerbosityLevel.INFO);
            Log(string.Format("OpenGL Version {0}", GL.GetString(StringName.Version)), LogVerbosityLevel.INFO);
            Log(string.Format("Shading Language Version {0}", GL.GetString(StringName.ShadingLanguageVersion)), LogVerbosityLevel.INFO);


            //Initialize API
            Renderer = new GraphicsAPI(); //Use OpenGL by default
            Renderer.Init();

            //Setup Shadow Renderer
            shdwRenderer = new ShadowRenderer();


            //Add default rendering resources
            CompileMainShaders();
            AddDefaultPrimitives();
            AddDefaultMaterials();
            AddDefaultLights();


            //Initialize Octree
            octree = new Octree(MAX_OCTREE_WIDTH);

            //Initialize Gbuffer
            setupGBuffer(width, height);

            Log("Resource Manager Initialized", LogVerbosityLevel.INFO);
        }

        

        public void setupGBuffer(int width, int height)
        {
            //Create gbuffer
            gBuffer = Renderer.CreateFrameBuffer(width, height);
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //albedo
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //normals
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //info
            gBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true); //depth
            
            renderBuffer = Renderer.CreateFrameBuffer(width, height);
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //final pass
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //color 0 - blur 0
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //color 1 - blur 1
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false); //composite
            renderBuffer.AddAttachment(TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true); //depth
            
            //Add attachments




            Log("FBOs Initialized", LogVerbosityLevel.INFO);
        }

        public FBO getRenderFBO()
        {
            return renderBuffer;
        }

        public FBO getGeometryFBO()
        {
            return gBuffer;
        }

        public void getMousePosInfo(int x, int y, ref NbVector4[] arr)
        {
            //Fetch Depth
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
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

            GLSLShaderConfig shader_conf;

            //Geometry Shader
            //Compile Object Shaders
            GLSLShaderSource geometry_shader_vs = new("Shaders/Simple_VSEmpty.glsl", true);
            GLSLShaderSource geometry_shader_fs = new("Shaders/Simple_FSEmpty.glsl", true);
            GLSLShaderSource geometry_shader_gs = new("Shaders/Simple_GS.glsl", true);

            shader_conf = GLShaderHelper.compileShader(geometry_shader_vs, geometry_shader_fs, geometry_shader_gs, null, null,
                            new(), SHADER_TYPE.DEBUG_MESH_SHADER, SHADER_MODE.DEFFERED);

            shader_conf.GetComponent<GUIDComponent>().Dispose();
            shader_conf.Dispose();
            
            //Compile Object Shaders
            GLSLShaderSource gizmo_shader_vs = new("Shaders/Gizmo_VS.glsl", true);
            GLSLShaderSource gizmo_shader_fs = new("Shaders/Gizmo_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gizmo_shader_vs, gizmo_shader_fs, null, null, null,
                            new(), SHADER_TYPE.GIZMO_SHADER, SHADER_MODE.DEFFERED);

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
                new(), SHADER_TYPE.BBOX_SHADER, SHADER_MODE.DEFFERED);
            shader_conf.Dispose();

            //Texture Mixing Shader
            GLSLShaderSource texture_mixing_shader_vs = new("Shaders/texture_mixer_VS.glsl", true);
            GLSLShaderSource texture_mixing_shader_fs = new("Shaders/texture_mixer_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            new(), SHADER_TYPE.TEXTURE_MIX_SHADER, SHADER_MODE.DEFAULT);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.TEXTURE_MIX_SHADER);


            //GBuffer Shaders

            GLSLShaderSource gbuffer_shader_vs = new("Shaders/Gbuffer_VS.glsl", true);
            GLSLShaderSource gbuffer_shader_fs = new("Shaders/Gbuffer_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), SHADER_TYPE.GBUFFER_SHADER, SHADER_MODE.DEFAULT);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GBUFFER_SHADER);

            //Light Pass Shaders

            //UNLIT
            GLSLShaderSource lpass_shader_vs = new("Shaders/light_pass_VS.glsl", true);
            GLSLShaderSource lpass_shader_fs = new("Shaders/light_pass_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            new(), SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER);


            //LIT
            lpass_shader_vs = new("Shaders/light_pass_VS.glsl", true);
            lpass_shader_fs = new("Shaders/light_pass_FS.glsl", true);
            lpass_shader_fs.AddDirective("_D_LIGHTING");
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            new(), SHADER_TYPE.LIGHT_PASS_LIT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.LIGHT_PASS_LIT_SHADER);

            //GAUSSIAN HORIZONTAL BLUR SHADER
            GLSLShaderSource gaussian_blur_shader_fs = new("Shaders/gaussian_horizontalBlur_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            new(), SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
            

            //GAUSSIAN VERTICAL BLUR SHADER
            gaussian_blur_shader_fs = new("Shaders/gaussian_verticalBlur_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            new(), SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER, SHADER_MODE.DEFAULT);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
            
            //BRIGHTNESS EXTRACTION SHADER
            gbuffer_shader_fs = new("Shaders/brightness_extract_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER);
            
            //ADDITIVE BLEND
            gbuffer_shader_fs = new("Shaders/additive_blend_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), SHADER_TYPE.ADDITIVE_BLEND_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.ADDITIVE_BLEND_SHADER);

            //FXAA
            gbuffer_shader_fs = new("Shaders/fxaa_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                new(), SHADER_TYPE.FXAA_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.FXAA_SHADER);
            
            //TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_fs = new("Shaders/tone_mapping_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                new(), SHADER_TYPE.TONE_MAPPING, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.TONE_MAPPING);

            //INV TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_fs = new("Shaders/inv_tone_mapping_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), SHADER_TYPE.INV_TONE_MAPPING, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.INV_TONE_MAPPING);
            
            //BWOIT SHADER
            gbuffer_shader_fs = new("Shaders/bwoit_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), SHADER_TYPE.BWOIT_COMPOSITE_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.BWOIT_COMPOSITE_SHADER);
            
            //Text Shaders
            GLSLShaderSource text_shader_vs = new("Shaders/Text_VS.glsl", true);
            GLSLShaderSource text_shader_fs = new("Shaders/Text_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(text_shader_vs, text_shader_fs, null, null, null,
                            new(), SHADER_TYPE.TEXT_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.TEXT_SHADER);
            
            //FILTERS - EFFECTS

            //Pass Shader
            GLSLShaderSource passthrough_shader_fs = new("Shaders/PassThrough_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            new(), SHADER_TYPE.PASSTHROUGH_SHADER, SHADER_MODE.FORWARD);
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.PASSTHROUGH_SHADER);

            //Red Shader
            GLSLShaderSource red_shader_fs = new("Shaders/RedFill.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, red_shader_fs, null, null, null,
                            new(), SHADER_TYPE.RED_FILL_SHADER, SHADER_MODE.FORWARD);
            
            //Attach UBO binding Points
            EngineRef.RegisterEntity(shader_conf);
            ShaderMgr.AddGenericShader(shader_conf, SHADER_TYPE.RED_FILL_SHADER);
            
        }

        private void AddDefaultLights()
        {
            SceneGraphNode light = EngineRef.CreateLightNode("Default Light", 200.0f, ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);
            TransformationSystem.SetEntityLocation(light, new NbVector3(100.0f, 100.0f, 100.0f));
            
            EngineRef.RegisterEntity(light);
            LightList.Add(light);
        }

        private void AddDefaultPrimitives()
        {
            //Setup Primitive Vaos

            //Default quad
            Primitives.Quad q = new(1.0f, 1.0f);

            NbMesh mesh = new()
            {
                Hash = (ulong) "default_quad".GetHashCode(),
                Data = q.GetData(),
                MetaData = q.GetMetaData()
            };
            
            Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            GeometryMgr.AddPrimitiveMesh(mesh);
            q.Dispose();

            //Default render quad
            q = new Primitives.Quad();
            
            mesh = new()
            {
                Hash = (ulong)"default_renderquad".GetHashCode(),
                Data = q.GetData(),
                MetaData = q.GetMetaData(),
            };
            Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            GeometryMgr.AddPrimitiveMesh(mesh);
            q.Dispose();
            
            //Default cross
            Primitives.Cross c = new(0.1f, true);
            
            mesh = new()
            {
                Hash = (ulong)"default_cross".GetHashCode(),
                Data = c.GetData(),
                MetaData = c.GetMetaData()
            };
            Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            GeometryMgr.AddPrimitiveMesh(mesh);
            c.Dispose();


            //Default cube
            Primitives.Box bx = new(1.0f, 1.0f, 1.0f, new NbVector3(1.0f), true);

            mesh = new()
            {
                Hash = (ulong)"default_box".GetHashCode(),
                Data = bx.GetData(),
                MetaData = bx.GetMetaData()
            };
            Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            GeometryMgr.AddPrimitiveMesh(mesh);
            bx.Dispose();
            
            //Default sphere
            Primitives.Sphere sph = new(new NbVector3(0.0f, 0.0f, 0.0f), 100.0f);

            mesh = new()
            {
                Hash = (ulong)"default_sphere".GetHashCode(),
                Data = sph.GetData(),
                MetaData = sph.GetMetaData()
            };

            Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            GeometryMgr.AddPrimitiveMesh(mesh);
            sph.Dispose();

            //Light Sphere Mesh
            Primitives.Sphere lsph = new(new NbVector3(0.0f, 0.0f, 0.0f), 1.0f);
            
            mesh = new()
            {
                Hash = (ulong)"default_light_sphere".GetHashCode(),
                Data = lsph.GetData(),
                MetaData = lsph.GetMetaData()
            };

            Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            GeometryMgr.AddPrimitiveMesh(mesh);
            lsph.Dispose();
            
            GenerateGizmoParts();
        }

        private void AddDefaultMaterials()
        {
            //Cross Material
            MeshMaterial mat;
            GLSLShaderConfig shader;

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
            shader = EngineRef.renderSys.Renderer.CompileMaterialShader(mat, SHADER_MODE.DEFFERED);
            EngineRef.renderSys.Renderer.AttachShaderToMaterial(mat, shader);

            EngineRef.RegisterEntity(mat.Shader); //Register Shader
            EngineRef.RegisterEntity(mat); //Register Material
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
            shader = EngineRef.renderSys.Renderer.CompileMaterialShader(mat, SHADER_MODE.DEFFERED);
            EngineRef.renderSys.Renderer.AttachShaderToMaterial(mat, shader);

            EngineRef.RegisterEntity(mat.Shader); //Register Shader
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
            shader = EngineRef.renderSys.Renderer.CompileMaterialShader(mat, SHADER_MODE.DEFFERED);
            EngineRef.renderSys.Renderer.AttachShaderToMaterial(mat, shader);

            EngineRef.RegisterEntity(mat.Shader); //Register Shader
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
            shader = EngineRef.renderSys.Renderer.CompileMaterialShader(mat, SHADER_MODE.DEFFERED);
            EngineRef.renderSys.Renderer.AttachShaderToMaterial(mat, shader);

            EngineRef.RegisterEntity(mat.Shader); //Register Shader
            EngineRef.RegisterEntity(mat);
            MaterialMgr.AddMaterial(mat);

        }

        private void GenerateGizmoParts()
        {
            //Translation Gizmo
            Primitives.Arrow translation_x_axis = new(0.015f, 0.25f, new NbVector3(1.0f, 0.0f, 0.0f), false, 20);
            //Move arrowhead up in place
            NbMatrix4 t = NbMatrix4.CreateRotationZ(MathUtils.radians(90));
            translation_x_axis.applyTransform(t);

            Primitives.Arrow translation_y_axis = new(0.015f, 0.25f, new NbVector3(0.0f, 1.0f, 0.0f), false, 20);
            Primitives.Arrow translation_z_axis = new(0.015f, 0.25f, new NbVector3(0.0f, 0.0f, 1.0f), false, 20);
            t = NbMatrix4.CreateRotationX(MathUtils.radians(90));
            translation_z_axis.applyTransform(t);

            //Generate Geom objects
            translation_x_axis.geom = translation_x_axis.getGeom();
            translation_y_axis.geom = translation_y_axis.getGeom();
            translation_z_axis.geom = translation_z_axis.getGeom();


            //GLVao x_axis_vao = translation_x_axis.getVAO();
            //GLVao y_axis_vao = translation_y_axis.getVAO();
            //GLVao z_axis_vao = translation_z_axis.getVAO();


            //Generate PrimitiveMeshVaos
            for (int i = 0; i < 3; i++)
            {
                string name = "";
                Primitives.Primitive arr = null;
                switch (i)
                {
                    case 0:
                        arr = translation_x_axis;
                        name = "default_translation_gizmo_x_axis";
                        break;
                    case 1:
                        arr = translation_y_axis;
                        name = "default_translation_gizmo_y_axis";
                        break;
                    case 2:
                        arr = translation_z_axis;
                        name = "default_translation_gizmo_z_axis";
                        break;
                }

                NbMesh mesh = new()
                {
                    Hash = (ulong) name.GetHashCode(),
                    Data = arr.GetData(),
                    MetaData = arr.GetMetaData()
                };
                
                
                if (!GeometryMgr.AddPrimitiveMesh(mesh))
                    mesh.Dispose();
                arr.Dispose();
            }

        }

        public GLSLShaderConfig GetMaterialShader(MeshMaterial mat, SHADER_MODE mode)
        {
            //Calculate Shader hash
            long shader_hash = Renderer.CalculateMaterialShaderhash(mat, mode);
            GLSLShaderConfig shader = null;

            if (ShaderMgr.ShaderHashExists(shader_hash))
            {
                shader =  ShaderMgr.GetShaderByHash(shader_hash);
            } else
            {
                //Compile Material Shader
                shader =  Renderer.CompileMaterialShader(mat, mode);
                
            }
            
            Renderer.AttachShaderToMaterial(mat, shader);
            return shader;
        }

        public void RegisterEntity(Entity e)
        {
            if (EntityDataMap.ContainsKey(e.GetID()))
            {
                Log("Entity Already Registered", Common.LogVerbosityLevel.INFO);
                return;
            }

            if (!e.HasComponent<MeshComponent>())
            {
                Log(string.Format("Entity {0} should have a mesh component", e.GetID()), Common.LogVerbosityLevel.INFO);
                return;
            }

            MeshComponent mc = e.GetComponent<MeshComponent>() as MeshComponent;

            Renderer.AddMesh(mc.Mesh);
            MaterialMgr.AddMaterial(mc.Material);
            
            //Insert to Maps
            EntityDataMap[e.GetID()] = mc;
            process_model(mc);

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
        }

        private void process_model(MeshComponent m)
        {
            if (m == null)
                return;

            //Explicitly handle locator, scenes and collision meshes
            switch (m.Mesh.Type)
            {
                case (NbMeshType.Locator):  
                    {
                        if (!locatorMeshList.Contains(m.Mesh))
                            locatorMeshList.Add(m.Mesh);
                        break;
                    }
                case (NbMeshType.Collision):
                    collisionMeshList.Add(m.Mesh);
                    break;
                case (NbMeshType.Joint):
                    jointMeshList.Add(m.Mesh);
                    break;
                case (NbMeshType.Light):
                    lightMeshList.Add(m.Mesh);
                    break;
                case (NbMeshType.LightVolume):
                    {
                        if (!lightVolumeMeshList.Contains(m.Mesh))
                            lightVolumeMeshList.Add(m.Mesh);
                        break;
                    }
            }

            //Check if the shader has been registered to the rendering system
            if (!ShaderMgr.ShaderIDExists(m.Material.Shader.GetID()))
            {
                ShaderMgr.AddShader(m.Material.Shader);
            }

            if (!ShaderMgr.ShaderContainsMaterial(m.Material.Shader, m.Material))
            {
                ShaderMgr.AddMaterialToShader(m.Material);
            }
            
            //Add all meshes to the global meshlist
            if (!globalMeshList.Contains(m.Mesh))
                globalMeshList.Add(m.Mesh);

            //Add meshes to their associated material meshlist
            if (!MaterialMgr.MaterialContainsMesh(m.Material, m.Mesh))
                MaterialMgr.AddMeshToMaterial(m.Material, m.Mesh);
            
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

        //This method updates UBO data for rendering
        private void prepareCommonPerFrameUBO()
        {
            //FrameData
            Renderer.SetCameraData(RenderState.activeCam);
            Renderer.SetCommonDataPerFrame(gBuffer, RenderState.rotMat, gfTime);
            Renderer.SetRenderSettings(RenderState.settings.renderSettings);
            Renderer.SetLightDataPerFrame(EngineRef.GetEntityTypeList(EntityType.SceneNodeLight));
            Renderer.UploadFrameData();
        }

        public void Resize(int x, int y)
        {
            ViewportSize.X = x;
            ViewportSize.Y = y;
            //Renderer.ResizeViewport(x, y);
            gBuffer?.resize(x, y);
            renderBuffer?.resize(x, y);
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

        private void renderDefaultMeshes()
        {
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //Collisions
            if (RenderState.settings.viewSettings.ViewCollisions)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("collisionMat");
                Renderer.EnableMaterialProgram(mat);

                //Render static meshes
                foreach (NbMesh m in collisionMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;
                    
                    Renderer.RenderCollision(m, mat);
                }
            }

            //Lights
            if (RenderState.settings.viewSettings.ViewLights)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
                Renderer.EnableMaterialProgram(mat);
                
                //Render static meshes
                foreach (NbMesh m in lightMeshList)
                {
                    if (m.InstanceCount == 0) continue;
                    Renderer.RenderLight(m, mat);
                }
            }

            //Light Volumes
            if (RenderState.settings.viewSettings.ViewLightVolumes)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
                Renderer.EnableMaterialProgram(mat);

                //Render static meshes
                foreach (NbMesh m in lightVolumeMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;
                    
                    Renderer.RenderMesh(m, mat);
                }
            }

            //Joints
            if (RenderState.settings.viewSettings.ViewJoints)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("jointMat");
                Renderer.EnableMaterialProgram(mat);

                //Render static meshes
                foreach (NbMesh m in jointMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;

                    Renderer.RenderJoint(m, mat);
                }
            }

            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);

            //Locators
            if (RenderState.settings.viewSettings.ViewLocators)
            {
                MeshMaterial mat = EngineRef.GetMaterialByName("crossMat");
                Renderer.EnableMaterialProgram(mat);

                //Render static meshes
                foreach (NbMesh m in locatorMeshList)
                {
                    if (m.InstanceCount == 0)
                        continue;

                    Renderer.RenderLocator(m, mat);
                }
            }
            
            GL.Enable(EnableCap.CullFace);
        }

        private void renderTestQuad()
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            //Set Test Program
            GLSLShaderConfig shader = ShaderMgr.GetGenericShader(SHADER_TYPE.RED_FILL_SHADER);
            Renderer.SetProgram(shader.ProgramID);

            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode()),
                shader, shader.CurrentState);
        }

        private void renderStaticMeshes()
        {
            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);
            
            foreach (GLSLShaderConfig shader in ShaderMgr.GLDeferredShaders)
            {
                foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
                {
                    foreach (NbMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
                    {
                        if (mesh.InstanceCount == 0)
                            continue;

                        Renderer.SetProgram(shader.ProgramID);
                        Renderer.RenderMesh(mesh, mat);

                        //TODO : Store the bhull as a separate mesh and render it just like any other mesh
                        //if (RenderState.settings.viewSettings.ViewBoundHulls)
                        //{
                        //    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        //    //Renderer.RenderMesh(mesh, mat, RENDERPASS.BHULL);
                        //    GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.settings.renderSettings.RENDERMODE);
                        //}
                    }
                }
                
                //GL.BindBuffer(BufferTarget.UniformBuffer, 0); //Unbind UBOs
                
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

            //Set polygon mode
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }

        private void renderGeometry()
        {
            //DEFERRED STAGE - STATIC MESHES

            //At first render the static meshes
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            //DEFERRED STAGE
            Renderer.BindDrawFrameBuffer(gBuffer, new int[] {0, 1, 2});
            Renderer.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);

            //renderTestQuad();
            renderStaticMeshes(); //Deferred Rendered MESHES
            //renderDecalMeshes(); //Render Decals
            //renderDefaultMeshes(); //Collisions, Locators, Joints
            
            //renderDeferredLightPass(); //Deferred Lighting Pass to pbuf

            //FORWARD STAGE - TRANSPARENT MESHES
            //renderTransparent(); //Directly to Pbuf

            Renderer.SyncGPUCommands();
            
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
                GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetChannel(4));

                foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
                {
                    foreach (NbMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
                    {
                        if (mesh.InstanceCount == 0)
                            continue;
                        
                        Renderer.RenderMesh(mesh, mat);
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
            FBO.copyDepthChannel(gBuffer.fbo, renderBuffer.fbo, 
                                 gBuffer.Size.X, gBuffer.Size.Y,
                                 renderBuffer.Size.X, renderBuffer.Size.Y);
            
            //Render the first pass in the first channel of the pbuf
            GL.ClearTexImage(renderBuffer.GetChannel(1), 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.ClearTexImage(renderBuffer.GetChannel(2), 0, PixelFormat.Rgba, PixelType.Float, new float[] { 1.0f, 1.0f ,1.0f, 1.0f});

            //Enable writing to both channels after clearing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
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
                Renderer.EnableShaderProgram(shader); //Set Program
                
                foreach (MeshMaterial mat in ShaderMgr.GetShaderMaterials(shader))
                {
                    foreach (NbMesh mesh in MaterialMgr.GetMaterialMeshes(mat))
                    {
                        if (mesh.InstanceCount == 0)
                            continue;
                    
                        Renderer.RenderMesh(mesh, mat);
                    }
                    
                }
            }

            Renderer.UnbindMeshBuffers();
            GL.DepthMask(true); //Re-enable depth buffer
            
            //Composite Step
            GLSLShaderConfig bwoit_composite_shader = ShaderMgr.GetGenericShader(SHADER_TYPE.BWOIT_COMPOSITE_SHADER); 
            
            //Draw to main color channel
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlendFunc(BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha); //Set compositing blend func
                                                                                    //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); //Set compositing blend func


            //Prepare Shader State
            bwoit_composite_shader.ClearCurrentState();
            bwoit_composite_shader.CurrentState.AddSampler("in1Tex",
                new()
                {
                    Target = NbTextureTarget.Texture2D,
                    TextureID = renderBuffer.GetChannel(1)
                }
            );

            bwoit_composite_shader.CurrentState.AddSampler("in2Tex",
                new()
                {
                    Target = NbTextureTarget.Texture2D,
                    TextureID = renderBuffer.GetChannel(2)
                }
            );

            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode()), 
                bwoit_composite_shader, bwoit_composite_shader.CurrentState);
            
            GL.Disable(EnableCap.Blend);
        }

        
        
        private void renderFinalPass()
        {
            //TESTING: Immediately pass albedo from the gbuffer to the final renderBuffer
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 0, 0, 
                                     renderBuffer.Size.X, renderBuffer.Size.Y, 
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
            GL.ClearColor(new OpenTK.Mathematics.Color4(5, 5, 5, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            //TEST RENDERING

            //GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
            //GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Draw to the light color channel only

            //GL.ClearColor(1.0f, 0.0f, 1.0f, 1.0f);
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            
            //END:TEST
            

            //Prepare UBOs
            prepareCommonPerFrameUBO();

            //Prepare Mesh UBO
            Renderer.PrepareMeshBuffers();
            
            //Render Geometry
            renderGeometry();

            //Pass result to Render FBO
            renderFinalPass();
            
            return;

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

            //Prepare Mesh UBOs
            Renderer.PrepareMeshBuffers();

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

        private void pass_tex(int to_fbo, DrawBufferMode to_channel, int InTex, int[] to_buf_size)
        {
            //passthrough a texture to the specified to_channel of the to_fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel);

            GL.Disable(EnableCap.DepthTest); //Disable Depth test
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GLSLShaderConfig shader = EngineRef.GetShaderByType(SHADER_TYPE.PASSTHROUGH_SHADER);
            //render_quad(new string[] {"sizeX", "sizeY" }, new float[] { to_buf_size[0], to_buf_size[1]}, new string[] { "InTex" }, new int[] { InTex }, shader);
            shader.ClearCurrentState();
            shader.CurrentState.AddSampler("InTex", new GLSLSamplerState()
            {
                Target = NbTextureTarget.Texture2D,
                TextureID = InTex
            });

            Renderer.RenderQuad(GeometryMgr.GetPrimitiveMesh((ulong) "default_renderquad".GetHashCode()),
                        shader, shader.CurrentState);
            GL.Enable(EnableCap.DepthTest); //Re-enable Depth test
        }


        //private void bloom()
        //{
        //    //Load Programs
        //    GLSLShaderConfig gs_horizontal_blur_program = EngineRef.GetShaderByType(SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
        //    GLSLShaderConfig gs_vertical_blur_program = EngineRef.GetShaderByType(SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER);
        //    GLSLShaderConfig br_extract_program = EngineRef.GetShaderByType(SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER) ;
        //    GLSLShaderConfig add_program = EngineRef.GetShaderByType(SHADER_TYPE.ADDITIVE_BLEND_SHADER);
            
        //    GL.Disable(EnableCap.DepthTest);

        //    //Copy Color to blur fbo channel 1
        //    FBO.copyChannel(pbuf.fbo, blur_fbo.fbo, gbuf.size[0], gbuf.size[1], blur_fbo.size_x, blur_fbo.size_y,
        //        ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
        //    //pass_tex(blur_fbo.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, new int[] { blur_fbo.size_x, blur_fbo.size_y });

        //    //Extract Brightness on the blur buffer and write it to channel 0
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //Write to blur1
            
        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[1] }, br_extract_program);



        //    //Copy Color to blur fbo channel 1
        //    //FBO.copyChannel(blur_fbo.fbo, pbuf.fbo, blur_fbo.size_x, blur_fbo.size_y, gbuf.size[0], gbuf.size[1],
        //    //    ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

        //    //return;

        //    //Console.WriteLine(GL.GetError()); 

        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, blur_fbo.fbo);
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
        //    GL.Viewport(0, 0, blur_fbo.size_x, blur_fbo.size_y); //Change the viewport
        //    int blur_amount = 2;
        //    for (int i=0; i < blur_amount; i++)
        //    {
        //        //Step 1- Apply horizontal blur
        //        GL.DrawBuffer(DrawBufferMode.ColorAttachment1); //blur2
        //        GL.Clear(ClearBufferMask.ColorBufferBit);
                
        //        render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[0]}, gs_horizontal_blur_program);

        //        //Step 2- Apply horizontal blur
        //        GL.DrawBuffer(DrawBufferMode.ColorAttachment0); //blur2
        //        GL.Clear(ClearBufferMask.ColorBufferBit);

        //        render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { blur_fbo.channels[1] }, gs_vertical_blur_program);
        //    }

        //    //Blit to screen
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, blur_fbo.fbo);
        //    GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
        //    GL.Clear(ClearBufferMask.ColorBufferBit); //Clear Screen
            
        //    GL.BlitFramebuffer(0, 0, blur_fbo.size_x, blur_fbo.size_y, 0, 0, pbuf.size[0], pbuf.size[1],
        //    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
        //    GL.Viewport(0, 0, gbuf.size[0], gbuf.size[1]); //Restore viewport

        //    //Save Color to blur2 so that we can composite on the main channel
        //    GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, pbuf.fbo);
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.ReadBuffer(ReadBufferMode.ColorAttachment0); //color
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment2); //blur2
        //    GL.BlitFramebuffer(0, 0, gbuf.size[0], gbuf.size[1], 0, 0, gbuf.size[0], gbuf.size[1],
        //    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);

        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "in1Tex", "in2Tex" }, new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D }, new int[] { pbuf.blur2, pbuf.blur1 }, add_program);
        //    //render_quad(new string[] { }, new float[] { }, new string[] { "blurTex" }, new int[] { pbuf.blur1 }, gs_bloom_program);

        //}

        //private void fxaa()
        //{
        //    //inv_tone_mapping(); //Apply tone mapping pbuf.color shoud be ready
            
        //    //Load Programs
        //    GLSLShaderConfig fxaa_program = ShaderMgr.GetGenericShader(SHADER_TYPE.FXAA_SHADER);

        //    //Copy Color to first channel
        //    FBO.copyChannel(pbuf.fbo, pbuf.fbo, pbuf.size[0], pbuf.size[1], pbuf.size[0], pbuf.size[1],
        //        ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1);
        //    //pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size);

        //    //Apply FXAA
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "diffuseTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, fxaa_program);

        //    //tone_mapping(); //Invert Tone Mapping

        //}

        //private void tone_mapping()
        //{
        //    //Load Programs
        //    GLSLShaderConfig tone_mapping_program = ShaderMgr.GetGenericShader(SHADER_TYPE.TONE_MAPPING);

        //    //Copy Color to first channel
        //    pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

        //    //Apply Tone Mapping
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, tone_mapping_program);

        //}

        //private void inv_tone_mapping()
        //{
        //    //Load Programs
        //    GLSLShaderConfig inv_tone_mapping_program = ShaderMgr.GetGenericShader(SHADER_TYPE.INV_TONE_MAPPING);

        //    //Copy Color to first channel
        //    pass_tex(pbuf.fbo, DrawBufferMode.ColorAttachment1, pbuf.color, pbuf.size); //LOOKS OK!

        //    //Apply Tone Mapping
        //    GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, pbuf.fbo);
        //    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

        //    render_quad(Array.Empty<string>(), Array.Empty<float>(), new string[] { "inTex" }, new TextureTarget[] { TextureTarget.Texture2D }, new int[] { pbuf.blur1 }, inv_tone_mapping_program);

        //}

        private void post_process()
        {
            //Actuall Post Process effects in AA space without tone mapping
            //TODO: Bring that back

        //    if (RenderState.settings.renderSettings.UseBLOOM)
        //        bloom(); //BLOOM

        //    tone_mapping(); //FINAL TONE MAPPING, INCLUDES GAMMA CORRECTION

        //    if (RenderState.settings.renderSettings.UseFXAA)
        //        fxaa(); //FXAA (INCLUDING TONE/UNTONE)
        }

        private void backupDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 0, 0, gBuffer.Size.X, gBuffer.Size.Y,
                ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
            
        }

        private void restoreDepth()
        {
            //NOT WORKING
            //Backup the depth buffer to the secondary fbo
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, gBuffer.fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, gBuffer.fbo);
            GL.BlitFramebuffer(0, 0, gBuffer.Size.X, gBuffer.Size.Y, 0, 0, gBuffer.Size.X, gBuffer.Size.Y,
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
            
            MeshMaterial mat = EngineRef.GetMaterialByName("lightMat");
            Renderer.EnableMaterialProgram(mat);


            //At first blit the albedo (gbuf 0) -> channel 0 of the pbuf
            FBO.copyChannel(gBuffer.fbo, renderBuffer.fbo, 
                            gBuffer.Size.X, gBuffer.Size.Y,
                            renderBuffer.Size.X, renderBuffer.Size.Y,
                ReadBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment0);

            //Bind the color channel of the pbuf for drawing
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, renderBuffer.fbo);
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

            
            NbMesh mesh = GeometryMgr.GetPrimitiveMesh((ulong) "default_light_sphere".GetHashCode());
            
            GL.UseProgram(shader_conf.ProgramID);

            //Upload samplers
            string[] sampler_names = new string[] { "albedoTex", "depthTex", "normalTex", "parameterTex" };
            int[] texture_ids = new int[] { gBuffer.GetChannel(0),
                                            gBuffer.GetChannel(3),
                                            gBuffer.GetChannel(1),
                                            gBuffer.GetChannel(2) };
            TextureTarget[] sampler_targets = new TextureTarget[] { TextureTarget.Texture2D, TextureTarget.Texture2D,
                                                            TextureTarget.Texture2D, TextureTarget.Texture2D };
            for (int i = 0; i < sampler_names.Length; i++)
            {
                GL.Uniform1(shader_conf.uniformLocations[sampler_names[i]], i);
                GL.ActiveTexture(TextureUnit.Texture0 + i);
                GL.BindTexture(sampler_targets[i], texture_ids[i]);
            }
            
            //Note: we do not draw the light volumes for preview.
            //Thus we directly render the mesh using the simple RenderMesh method
            if (mesh.InstanceCount > 0) 
                Renderer.RenderMesh(mesh);

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
                    gBuffer.Dispose(); //Dispose gbuffer
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
