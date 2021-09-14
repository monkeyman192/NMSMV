using System;
using System.Collections.Generic;
using System.IO;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore.Common;
using MVCore.Text;
using MVCore.Utils;
using MVCore.Systems;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace MVCore
{
    public interface IBaseResourceManager
    {
        public void Cleanup();
    }


    //Class Which will store all the texture resources for better memory management
    public class ResourceManager : IBaseResourceManager
    {
        public Dictionary<long, MeshMaterial> MaterialIDMap = new();
        public List<MeshMaterial> MaterialList = new();
        public Dictionary<string, GeomObject> GLgeoms = new();
        public Dictionary<string, SceneGraphNode> GLScenes = new();
        public Dictionary<string, Texture> GLTextures = new();
        public Dictionary<string, AnimMetadata> Animations = new();

        public Dictionary<string, GLVao> GLPrimitiveVaos = new();
        public Dictionary<string, GLVao> GLVaos = new();
        public Dictionary<string, GLInstancedMesh> GLPrimitiveMeshes = new();

        public List<SceneGraphNode> LightList = new();
        public Dictionary<string, Font> FontMap = new();
        //public Dictionary<string, int> GLShaders = new Dictionary<string, int>();
        public Dictionary<SHADER_TYPE, GLSLShaderConfig> GenericShaders = new(); //Generic Shader Map
        public readonly Dictionary<int, GLSLShaderConfig> ShaderMap = new();
        public readonly Dictionary<MeshMaterial, List<GLInstancedMesh>> MaterialMeshMap = new();
        public readonly Dictionary<GLSLShaderConfig, List<MeshMaterial>> ShaderMaterialMap = new();
        
        public readonly List<GLSLShaderConfig> GLDeferredShaders = new();
        public readonly List<GLSLShaderConfig> GLForwardTransparentShaders = new();
        public readonly List<GLSLShaderConfig> GLDeferredDecalShaders = new();

        //BufferManagers
        public GLLightBufferManager lightBufferMgr = new();

        //Global NMS File Archive handles
        public Dictionary<string, libPSARC.PSARC.Archive> NMSFileToArchiveMap = new();
        public List<string> NMSSceneFilesList = new();
        public SortedDictionary<string, libPSARC.PSARC.Archive> NMSArchiveMap = new();

        //public int[] shader_programs;
        //Extra manager
        public TextureManager texMgr = new();
        public FontManager fontMgr = new();
        public TextManager txtMgr = new();

        public bool initialized = false;

        //Procedural Generation Options
        //TODO: This is 99% NOT correct
        //public Dictionary<string, int> procTextureLayerSelections = new Dictionary<string, int>();

        //public DebugForm DebugWin;

        public void Init()
        {
            initialized = false;

            //Add defaults
            AddDefaultTextures();
            AddDefaultPrimitives();
            AddDefaultLights();
            AddDefaultFonts();
            AddDefaultTexts();
            CompileMainShaders();
            
            initialized = true;
        }

        public void CompileMainShaders()
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
            
            //Compile Object Shaders
            GLSLShaderSource gizmo_shader_vs = new("Shaders/Gizmo_VS.glsl", true);
            GLSLShaderSource gizmo_shader_fs = new("Shaders/Gizmo_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gizmo_shader_vs, gizmo_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.GIZMO_SHADER, SHADER_MODE.DEFFERED);
            
            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);
            GenericShaders[SHADER_TYPE.GIZMO_SHADER] = shader_conf;


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
            
            
            //Texture Mixing Shader
            GLSLShaderSource texture_mixing_shader_vs = new("Shaders/texture_mixer_VS.glsl", true);
            GLSLShaderSource texture_mixing_shader_fs = new("Shaders/texture_mixer_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            new(), new(), GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER, SHADER_MODE.DEFAULT);
            
            GenericShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER] = shader_conf;
            
            
            
            //GBuffer Shaders

            GLSLShaderSource gbuffer_shader_vs = new("Shaders/Gbuffer_VS.glsl", true);
            GLSLShaderSource gbuffer_shader_fs = new("Shaders/Gbuffer_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.GBUFFER_SHADER, SHADER_MODE.DEFAULT);
            GenericShaders[SHADER_TYPE.GBUFFER_SHADER] = shader_conf;
            
            //Light Pass Shaders

            //UNLIT
            GLSLShaderSource lpass_shader_vs = new("Shaders/light_pass_VS.glsl", true);
            GLSLShaderSource lpass_shader_fs = new("Shaders/light_pass_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            new(), new(),SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER] = shader_conf;
            
            //LIT
            lpass_shader_vs = new("Shaders/light_pass_VS.glsl");
            lpass_shader_fs = new("Shaders/light_pass_FS.glsl");
            lpass_shader_fs.AddDirective("_D_LIGHTING");
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.LIGHT_PASS_LIT_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.LIGHT_PASS_LIT_SHADER] = shader_conf;
            
            
            //GAUSSIAN HORIZONTAL BLUR SHADER
            GLSLShaderSource gaussian_blur_shader_fs = new("Shaders/gaussian_horizontalBlur_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER] = shader_conf;
            
            
            //GAUSSIAN VERTICAL BLUR SHADER
            gaussian_blur_shader_fs = new("Shaders/gaussian_verticalBlur_FS.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            new(), new(), 
                            SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER, SHADER_MODE.DEFAULT);
            GenericShaders[SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER] = shader_conf;
            
            //BRIGHTNESS EXTRACTION SHADER
            gbuffer_shader_fs = new("Shaders/brightness_extract_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(),
                            SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER] = shader_conf;
            

            //ADDITIVE BLEND
            gbuffer_shader_fs = new("Shaders/additive_blend_fs.glsl",  true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), 
                            SHADER_TYPE.ADDITIVE_BLEND_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.ADDITIVE_BLEND_SHADER] = shader_conf;
            
            //FXAA
            gbuffer_shader_fs = new("Shaders/fxaa_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                new(), new(), SHADER_TYPE.FXAA_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.FXAA_SHADER] = shader_conf;
            
            //TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_fs = new("Shaders/tone_mapping_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                new(),new(), SHADER_TYPE.TONE_MAPPING, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.TONE_MAPPING] = shader_conf;
            
            //INV TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_fs = new("Shaders/inv_tone_mapping_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.INV_TONE_MAPPING, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.INV_TONE_MAPPING] = shader_conf;
            

            //BWOIT SHADER
            gbuffer_shader_fs = new("Shaders/bwoit_shader_fs.glsl", true);
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.BWOIT_COMPOSITE_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER] = shader_conf;
            
            //Text Shaders
            GLSLShaderSource text_shader_vs = new("Shaders/Text_VS.glsl");
            GLSLShaderSource text_shader_fs = new("Shaders/Text_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(text_shader_vs, text_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.TEXT_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.TEXT_SHADER] = shader_conf;
            
            //Camera Shaders
            //TODO: Add Camera Shaders if required
            GenericShaders[GLSLHelper.SHADER_TYPE.CAMERA_SHADER] = null;

            //FILTERS - EFFECTS

            //Pass Shader
            GLSLShaderSource passthrough_shader_fs = new("Shaders/PassThrough_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            new(), new(), SHADER_TYPE.PASSTHROUGH_SHADER, SHADER_MODE.FORWARD);
            GenericShaders[SHADER_TYPE.PASSTHROUGH_SHADER] = shader_conf;
            
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

        private void AddDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            //Add Default textures
            //White tex
            string texpath = "default.dds";
            Texture tex = new();
            tex.textureInit((byte[]) Callbacks.getResource("default.dds"), texpath); //Manually load data

            texMgr.AddTexture(tex);

            //Transparent Mask
            texpath = "default_mask.dds";
            tex = new();
            tex.textureInit((byte[]) Callbacks.getResource("default_mask.dds"), texpath);
            texMgr.AddTexture(tex);
        }

        private void AddDefaultLights()
        {
            SceneGraphNode light = SceneGraphNode.CreateLight("Default Light", 200.0f, ATTENUATION_TYPE.QUADRATIC, LIGHT_TYPE.POINT);
            TransformationSystem.SetEntityLocation(light, new Vector3(100.0f, 100.0f, 100.0f));
            
            LightList.Add(light);
        }

        

        private void AddDefaultFonts()
        {
            Font f;
            //Droid Sans
            f = new Font(Callbacks.getResource("droid.fnt"),
                         Callbacks.getBitMapResource("droid.png"), 1);
            fontMgr.addFont(f);
            
            //Segoe
            f = new Font(Callbacks.getResource("segoe.fnt"),
                         Callbacks.getBitMapResource("segoe.png"), 1);
            fontMgr.addFont(f);
        }

        //fontMgr.addFont("droid.fnt");
        
        private void AddDefaultTexts()
        {
            Font f = fontMgr.getFont("Segoe UI");
            //Font f = fontMgr.getFont("Arial");
            //Font f = fontMgr.getFont("Droid Sans Mono");

            Vector3 default_color = new(1.0f, 1.0f, 0.5f);
            float lineHeight = 15.0f;
            int pos_x = 10;
            int pos_y = 90;
            GLText t = new(f, new Vector2(pos_x, pos_y), lineHeight, default_color,
                string.Format("FPS: {0:000.0}", RenderStats.fpsCount));
            txtMgr.addText(t, TextManager.Semantic.FPS);
            
            t = new(f, new Vector2(pos_x, pos_y - lineHeight), lineHeight, default_color,
                string.Format("OccludedNum: {0:0000}", RenderStats.occludedNum));
            txtMgr.addText(t, TextManager.Semantic.OCCLUDED_COUNT);

            t = new(f, new Vector2(pos_x, pos_y - 2 * lineHeight), lineHeight, default_color,
                string.Format("Total Vertices: {0:D1}", RenderStats.vertNum));
            txtMgr.addText(t, TextManager.Semantic.VERT_COUNT);
            
            t = new(f, new Vector2(pos_x, pos_y - 3 * lineHeight), lineHeight, default_color,
                string.Format("Total Triangles: {0:D1}", RenderStats.trisNum));
            txtMgr.addText(t, TextManager.Semantic.TRIS_COUNT);

            t = new(f, new Vector2(pos_x, pos_y - 4 * lineHeight), lineHeight, default_color,
                string.Format("Textures: {0:D1}", RenderStats.texturesNum));
            txtMgr.addText(t, TextManager.Semantic.TEXTURE_COUNT);

            t = new(f, new Vector2(pos_x, pos_y - 5 * lineHeight), lineHeight, default_color,
                string.Format("Controller: {0} ", RenderState.activeGamepad?.getName()));
            txtMgr.addText(t, TextManager.Semantic.CTRL_ID);
        
        }

        private void GenerateGizmoParts()
        {
            //Translation Gizmo
            Primitives.Arrow translation_x_axis = new(0.015f, 0.25f, new Vector3(1.0f, 0.0f, 0.0f), false, 20);
            //Move arrowhead up in place
            Matrix4 t = Matrix4.CreateRotationZ(MathUtils.radians(90));
            translation_x_axis.applyTransform(t);

            Primitives.Arrow translation_y_axis = new(0.015f, 0.25f, new Vector3(0.0f, 1.0f, 0.0f), false, 20);
            Primitives.Arrow translation_z_axis = new(0.015f, 0.25f, new Vector3(0.0f, 0.0f, 1.0f), false, 20);
            t = Matrix4.CreateRotationX(MathUtils.radians(90));
            translation_z_axis.applyTransform(t);

            //Generate Geom objects
            translation_x_axis.geom = translation_x_axis.getGeom();
            translation_y_axis.geom = translation_y_axis.getGeom();
            translation_z_axis.geom = translation_z_axis.getGeom();


            GLPrimitiveVaos["default_translation_gizmo_x_axis"] = translation_x_axis.getVAO();
            GLPrimitiveVaos["default_translation_gizmo_y_axis"] = translation_y_axis.getVAO();
            GLPrimitiveVaos["default_translation_gizmo_z_axis"] = translation_z_axis.getVAO();


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

                GLPrimitiveMeshes[name] = new()
                {
                    type = SceneNodeType.GIZMOPART,
                    vao = GLPrimitiveVaos[name],
                    MetaData = new()
                    {
                        BatchCount = arr.geom.indicesCount,
                        IndicesLength = DrawElementsType.UnsignedInt,
                    }
                };
                
                //TODO: Probably I should generate SceneGraphNodes with Meshcomponents in order to attach materials
                //GLPrimitiveMeshes[name].material = GLmaterials["crossMat"];
            }

        }

        public void AddDefaultPrimitives()
        {
            //Setup Primitive Vaos

            //Default quad
            Primitives.Quad q = new(1.0f, 1.0f);
            GLPrimitiveVaos["default_quad"] = q.getVAO();
            GLPrimitiveMeshes["default_quad"] = new();
            GLPrimitiveMeshes["default_quad"].vao = GLPrimitiveVaos["default_quad"];
            
            //Default render quad
            q = new Primitives.Quad();
            GLPrimitiveVaos["default_renderquad"] = q.getVAO();
            GLPrimitiveMeshes["default_renderquad"] = new();
            GLPrimitiveMeshes["default_renderquad"].vao = GLPrimitiveVaos["default_renderquad"];

            //Default cross
            Primitives.Cross c = new(0.1f, true);
            GLPrimitiveVaos["default_cross"] = c.getVAO();

            GLPrimitiveMeshes["default_cross"] = new()
            {
                type = SceneNodeType.GIZMO,
                vao = GLPrimitiveVaos["default_cross"],
                MetaData = new()
                {
                    BatchCount = c.geom.indicesCount,
                    AABBMIN = new Vector3(-0.1f),
                    AABBMAX = new Vector3(0.1f),
                    IndicesLength = DrawElementsType.UnsignedInt,

                }
            };
            
            //Default cube
            Primitives.Box bx = new(1.0f, 1.0f, 1.0f, new Vector3(1.0f), true);
            GLPrimitiveVaos["default_box"] = bx.getVAO();
            GLPrimitiveMeshes["default_box"] = new();
            GLPrimitiveMeshes["default_box"].vao = GLPrimitiveVaos["default_box"];

            //Default sphere
            Primitives.Sphere sph = new(new Vector3(0.0f, 0.0f, 0.0f), 100.0f);
            GLPrimitiveVaos["default_sphere"] = sph.getVAO();
            GLPrimitiveMeshes["default_sphere"] = new();
            GLPrimitiveMeshes["default_sphere"].vao = GLPrimitiveVaos["default_sphere"];

            //Light Sphere Mesh
            Primitives.Sphere lsph = new(new Vector3(0.0f, 0.0f, 0.0f), 1.0f);
            GLPrimitiveVaos["default_light_sphere"] = lsph.getVAO();
            GLPrimitiveMeshes["default_light_sphere"] = new GLInstancedLightMesh()
            {
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
                vao = GLPrimitiveVaos["default_light_sphere"]
            };
            
            GenerateGizmoParts();
        }

        public bool ShaderExistsForMaterial(int shaderHash)
        {
            return RenderState.activeResMgr.ShaderMap.ContainsKey(shaderHash);
        }

        public void Cleanup()
        {
            //Cleanup global texture manager
            texMgr.Cleanup();
            fontMgr.cleanup();
            txtMgr.cleanup();
            //procTextureLayerSelections.Clear();

            foreach (SceneGraphNode p in GLScenes.Values)
                p.Dispose();
            GLScenes.Clear();

            //Cleanup Geom Objects
            foreach (GeomObject p in GLgeoms.Values)
                p.Dispose();
            GLgeoms.Clear();

            //Cleanup GLVaos
            foreach (GLVao p in GLVaos.Values)
                p.Dispose();
            GLVaos.Clear();
            
            //Cleanup Animations
            Animations.Clear();

            //Cleanup Materials
            
            //Cleanup Material Shaders
            ShaderMaterialMap.Clear();
            MaterialMeshMap.Clear();
            ShaderMap.Clear();
            
            GLDeferredShaders.Clear();
            GLForwardTransparentShaders.Clear();
            GLDeferredDecalShaders.Clear();
            
            //Cleanup archives
            //NMSUtils.unloadNMSArchives(this);
            
        }
    }

    


   
}
