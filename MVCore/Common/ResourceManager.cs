using System;
using System.Collections.Generic;
using System.IO;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Text;
using MVCore.Utils;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace MVCore
{
    public interface baseResourceManager
    {
        void cleanup();
    }


    //Class Which will store all the texture resources for better memory management
    public class ResourceManager
    {
        //public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, Material> GLmaterials = new();
        public Dictionary<string, GeomObject> GLgeoms = new();
        public Dictionary<string, Scene> GLScenes = new();
        public Dictionary<string, Texture> GLTextures = new();
        public Dictionary<string, AnimMetadata> Animations = new();

        public Dictionary<string, GLVao> GLPrimitiveVaos = new();
        public Dictionary<string, GLVao> GLVaos = new();
        public Dictionary<string, GLInstancedMeshVao> GLPrimitiveMeshVaos = new();

        public List<GMDL.Light> GLlights = new();
        public List<Camera> GLCameras = new();
        public Dictionary<string, Font> FontMap = new();
        //public Dictionary<string, int> GLShaders = new Dictionary<string, int>();
        public Dictionary<GLSLHelper.SHADER_TYPE, GLSLHelper.GLSLShaderConfig> GLShaders = new(); //Generic Shaders

        public Dictionary<int, GLSLShaderConfig> GLDeferredShaderMap = new();
        public Dictionary<int, GLSLShaderConfig> GLForwardShaderMapTransparent = new();
        public Dictionary<int, GLSLShaderConfig> GLDeferredShaderMapDecal = new();
        public Dictionary<int, GLSLShaderConfig> GLDefaultShaderMap = new();

        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredShaders = new();
        public List<GLSLHelper.GLSLShaderConfig> activeGLForwardTransparentShaders = new();
        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredDecalShaders = new();

        public Dictionary<int, List<GLInstancedMeshVao>> opaqueMeshShaderMap = new();
        public Dictionary<int, List<GLInstancedMeshVao>> defaultMeshShaderMap = new();
        public Dictionary<int, List<GLInstancedMeshVao>> transparentMeshShaderMap = new();
        public Dictionary<int, List<GLInstancedMeshVao>> decalMeshShaderMap = new();

        //BufferManagers
        public GLLightBufferManager lightBufferMgr = new();

        //Global NMS File Archive handles
        public Dictionary<string, libPSARC.PSARC.Archive> NMSFileToArchiveMap = new();
        public List<string> NMSSceneFilesList = new();
        public SortedDictionary<string, libPSARC.PSARC.Archive> NMSArchiveMap = new();

        //public int[] shader_programs;
        //Extra manager
        public textureManager texMgr = new();
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
            addDefaultTextures();
            addDefaultMaterials();
            addDefaultPrimitives();
            addDefaultLights();
            addDefaultFonts();
            addDefaultTexts();
            addDefaultCameras();
            compileMainShaders();

            initialized = true;
        }

        public void compileMainShaders()
        {

            //Populate shader list
            string log_file = "shader_compilation_log.out";
            
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

            //TODO BRING CHECK BACK
            //while (!FileUtils.IsFileReady(log_file)) { };
            StreamWriter sr = new StreamWriter(log_file, false);

            GLSLHelper.GLSLShaderConfig shader_conf;

            //Geometry Shader
            //Compile Object Shaders
            GLSLShaderText geometry_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText geometry_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            GLSLShaderText geometry_shader_gs = new GLSLShaderText(ShaderType.GeometryShader);
            geometry_shader_vs.addStringFromFile("Shaders/Simple_VSEmpty.glsl");
            geometry_shader_fs.addStringFromFile("Shaders/Simple_FSEmpty.glsl");
            geometry_shader_gs.addStringFromFile("Shaders/Simple_GS.glsl");

            shader_conf = GLShaderHelper.compileShader(geometry_shader_vs, geometry_shader_fs, geometry_shader_gs, null, null,
                            SHADER_TYPE.DEBUG_MESH_SHADER);
            
            sr.WriteLine("###COMPILING MAIN SHADER ###");
            sr.WriteLine(shader_conf.log);
            
            //Compile Object Shaders
            GLSLShaderText gizmo_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gizmo_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gizmo_shader_vs.addStringFromFile("Shaders/Gizmo_VS.glsl");
            gizmo_shader_fs.addStringFromFile("Shaders/Gizmo_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gizmo_shader_vs, gizmo_shader_fs, null, null, null,
                            SHADER_TYPE.GIZMO_SHADER);
            sr.WriteLine("###COMPILING GIZMO SHADER ###");
            sr.WriteLine(shader_conf.log);

            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);
            GLShaders[SHADER_TYPE.GIZMO_SHADER] = shader_conf;


#if DEBUG
            //Report UBOs
            GLShaderHelper.reportUBOs(shader_conf);
#endif

            //Picking Shader

            //Compile Default Shaders

            //BoundBox Shader
            GLSLShaderText bbox_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText bbox_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            bbox_shader_vs.addStringFromFile("Shaders/Bound_VS.glsl");
            bbox_shader_fs.addStringFromFile("Shaders/Bound_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(bbox_shader_vs, bbox_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.BBOX_SHADER);

            sr.WriteLine("###COMPILING BBOX SHADER ###");
            sr.WriteLine(shader_conf.log);

            //Texture Mixing Shader
            GLSLShaderText texture_mixing_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText texture_mixing_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            texture_mixing_shader_vs.addStringFromFile("Shaders/texture_mixer_VS.glsl");
            texture_mixing_shader_fs.addStringFromFile("Shaders/texture_mixer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER);
            
            GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING TEXTURE MIXER SHADER ###");
            sr.WriteLine(shader_conf.log);

            //GBuffer Shaders
            
            var gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            var gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/Gbuffer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.GBUFFER_SHADER);
            GLShaders[SHADER_TYPE.GBUFFER_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING GBUFFER SHADER ###");
            sr.WriteLine(shader_conf.log);

            //Light Pass Shaders

            //UNLIT
            GLSLShaderText lpass_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText lpass_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            lpass_shader_vs.addStringFromFile("Shaders/light_pass_VS.glsl");
            lpass_shader_fs.addStringFromFile("Shaders/light_pass_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER);
            GLShaders[SHADER_TYPE.LIGHT_PASS_UNLIT_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING LIGHT PASS UNLIT SHADER ###");
            sr.WriteLine(shader_conf.log);

            //LIT
            lpass_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            lpass_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            lpass_shader_vs.addStringFromFile("Shaders/light_pass_VS.glsl");
            lpass_shader_fs.addString("#define _D_LIGHTING");
            lpass_shader_fs.addStringFromFile("Shaders/light_pass_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(lpass_shader_vs, lpass_shader_fs, null, null, null,
                            SHADER_TYPE.LIGHT_PASS_LIT_SHADER);
            GLShaders[SHADER_TYPE.LIGHT_PASS_LIT_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING LIGHT PASS LIT SHADER ###");
            sr.WriteLine(shader_conf.log);


            //GAUSSIAN HORIZONTAL BLUR SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gaussian_blur_shader_fs.addStringFromFile("Shaders/gaussian_horizontalBlur_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER);
            GLShaders[SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING HBLUR SHADER ###");
            sr.WriteLine(shader_conf.log);

            //GAUSSIAN VERTICAL BLUR SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gaussian_blur_shader_fs.addStringFromFile("Shaders/gaussian_verticalBlur_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER);
            GLShaders[GLSLHelper.SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING VBLUR SHADER ###");
            sr.WriteLine(shader_conf.log);


            //BRIGHTNESS EXTRACTION SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/brightness_extract_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER);
            GLShaders[GLSLHelper.SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING BRIGHTNESS EXTRACTION SHADER ###");
            sr.WriteLine(shader_conf.log);


            //ADDITIVE BLEND
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/additive_blend_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.ADDITIVE_BLEND_SHADER);
            GLShaders[GLSLHelper.SHADER_TYPE.ADDITIVE_BLEND_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING ADDITIVE BLEND SHADER ###");
            sr.WriteLine(shader_conf.log);

            //FXAA
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/fxaa_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.FXAA_SHADER);
            GLShaders[GLSLHelper.SHADER_TYPE.FXAA_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING FXAA SHADER ###");
            sr.WriteLine(shader_conf.log);

            //TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/tone_mapping_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TONE_MAPPING);
            GLShaders[GLSLHelper.SHADER_TYPE.TONE_MAPPING] = shader_conf;
            sr.WriteLine("###COMPILING TONE MAPPING SHADER ###");
            sr.WriteLine(shader_conf.log);

            //INV TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/inv_tone_mapping_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.INV_TONE_MAPPING);
            GLShaders[SHADER_TYPE.INV_TONE_MAPPING] = shader_conf;
            sr.WriteLine("###COMPILING INV TONE MAPPING SHADER ###");
            sr.WriteLine(shader_conf.log);


            //BWOIT SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/bwoit_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.BWOIT_COMPOSITE_SHADER);
            GLShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING BWOIT SHADER ###");
            sr.WriteLine(shader_conf.log);


            //Text Shaders
            GLSLShaderText text_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText text_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            text_shader_vs.addStringFromFile("Shaders/Text_VS.glsl");
            text_shader_fs.addStringFromFile("Shaders/Text_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(text_shader_vs, text_shader_fs, null, null, null,
                            SHADER_TYPE.TEXT_SHADER);
            GLShaders[SHADER_TYPE.TEXT_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING TEXT SHADER ###");
            sr.WriteLine(shader_conf.log);

            //Camera Shaders
            //TODO: Add Camera Shaders if required
            GLShaders[GLSLHelper.SHADER_TYPE.CAMERA_SHADER] = null;

            //FILTERS - EFFECTS

            //Pass Shader
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText passthrough_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            passthrough_shader_fs.addStringFromFile("Shaders/PassThrough_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            SHADER_TYPE.PASSTHROUGH_SHADER);
            GLShaders[SHADER_TYPE.PASSTHROUGH_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING PASSTHROUGH SHADER ###");
            sr.WriteLine(shader_conf.log);

            //Red Shader
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText red_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            red_shader_fs.addStringFromFile("Shaders/RedFill.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, red_shader_fs, null, null, null,
                            SHADER_TYPE.RED_FILL_SHADER);
            
            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);

            GLShaders[SHADER_TYPE.RED_FILL_SHADER] = shader_conf;
            sr.WriteLine("###COMPILING RED FILL SHADER ###");
            sr.WriteLine(shader_conf.log);


            sr.WriteLine("###FINISHED SHADER COMPILATION###");

            sr.Close();


        }

        private void addDefaultCameras()
        {
            Camera cam = new Camera(90, -1, 0, true)
            {
                isActive = false
            };
            GLCameras.Add(cam);

            cam = new Camera(90, -1, 0, false)
            {
                isActive = false
            };
            GLCameras.Add(cam);

            //Set as active camera the first one by default
            RenderState.activeCam = GLCameras[0];
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            //Add Default textures
            //White tex
            string texpath = "default.dds";
            Texture tex = new Texture();
            tex.textureInit((byte[]) CallBacks.getResource("default.dds"), texpath); //Manually load data

            texMgr.addTexture(tex);

            //Transparent Mask
            texpath = "default_mask.dds";
            tex = new Texture();
            tex.textureInit((byte[]) CallBacks.getResource("default_mask.dds"), texpath);
            texMgr.addTexture(tex);
        }

        private void addDefaultLights()
        {
            //Add one and only light for now
            Light light = new Light
            {
                name = "Default Light",
                _intensity = 200,
                _falloff = ATTENUATION_TYPE.QUADRATIC
            };
            
            light._localPosition.vec = new Vector3(100.0f, 100.0f, 100.0f);
            GLlights.Add(light);
        }

        private void addDefaultMaterials()
        {
            //Cross Material
            Material mat;

            mat = new Material();
            mat.Name = "crossMat";
            mat.add_flag(TkMaterialFlags.UberFlagEnum._F07_UNLIT);
            mat.add_flag(TkMaterialFlags.UberFlagEnum._F21_VERTEXCOLOUR);
            TkMaterialUniform uf = new TkMaterialUniform
            {
                Name = "gMaterialColourVec4",
                Values = new libMBIN.NMS.Vector4f(1.0f, 1.0f,1.0f,1.0f)
            };
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["crossMat"] = mat;

            //Joint Material
            mat = new Material
            {
                Name = "jointMat"
            };
            mat.add_flag(TkMaterialFlags.UberFlagEnum._F07_UNLIT);

            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f(1.0f,0.0f,0.0f,1.0f);
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["jointMat"] = mat;

            //Light Material
            mat = new Material();
            mat.Name = "lightMat";
            mat.add_flag(TkMaterialFlags.UberFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 1.0f;
            uf.Values.z = 0.0f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["lightMat"] = mat;

            //Collision Material
            mat = new Material();
            mat.Name = "collisionMat";
            mat.add_flag(TkMaterialFlags.UberFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 0.8f;
            uf.Values.y = 0.8f;
            uf.Values.z = 0.2f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["collisionMat"] = mat;

        }

        private void addDefaultFonts()
        {
            Font f;
            //Droid Sans
            f = new Font(CallBacks.getResource("droid.fnt"),
                         CallBacks.getBitMapResource("droid.png"), 1);
            fontMgr.addFont(f);
            
            //Segoe
            f = new Font(CallBacks.getResource("segoe.fnt"),
                         CallBacks.getBitMapResource("segoe.png"), 1);
            fontMgr.addFont(f);
        }

        //fontMgr.addFont("droid.fnt");
        
        private void addDefaultTexts()
        {
            Font f = fontMgr.getFont("Segoe UI");
            //Font f = fontMgr.getFont("Arial");
            //Font f = fontMgr.getFont("Droid Sans Mono");

            Vector3 default_color = new Vector3(1.0f, 1.0f, 0.5f);
            float lineHeight = 15.0f;
            int pos_x = 10;
            int pos_y = 90;
            GLText t = new GLText(f, new Vector2(pos_x, pos_y), lineHeight, default_color,
                string.Format("FPS: {0:000.0}", RenderStats.fpsCount));
            txtMgr.addText(t, TextManager.Semantic.FPS);
            
            t = new GLText(f, new Vector2(pos_x, pos_y - lineHeight), lineHeight, default_color,
                string.Format("OccludedNum: {0:0000}", RenderStats.occludedNum));
            txtMgr.addText(t, TextManager.Semantic.OCCLUDED_COUNT);

            t = new GLText(f, new Vector2(pos_x, pos_y - 2 * lineHeight), lineHeight, default_color,
                string.Format("Total Vertices: {0:D1}", RenderStats.vertNum));
            txtMgr.addText(t, TextManager.Semantic.VERT_COUNT);
            
            t = new GLText(f, new Vector2(pos_x, pos_y - 3 * lineHeight), lineHeight, default_color,
                string.Format("Total Triangles: {0:D1}", RenderStats.trisNum));
            txtMgr.addText(t, TextManager.Semantic.TRIS_COUNT);

            t = new GLText(f, new Vector2(pos_x, pos_y - 4 * lineHeight), lineHeight, default_color,
                string.Format("Textures: {0:D1}", RenderStats.texturesNum));
            txtMgr.addText(t, TextManager.Semantic.TEXTURE_COUNT);

            t = new GLText(f, new Vector2(pos_x, pos_y - 5 * lineHeight), lineHeight, default_color,
                string.Format("Controller: {0} ", RenderState.activeGamepad?.getName()));
            txtMgr.addText(t, TextManager.Semantic.CTRL_ID);
        
        }

        public void addMaterial(Material mat)
        {
            GLmaterials[mat.name_key] = mat;
            //Assign material to shaders
        }

        private void generateGizmoParts()
        {
            //Translation Gizmo
            GMDL.Primitives.Arrow translation_x_axis = new GMDL.Primitives.Arrow(0.015f, 0.25f, new Vector3(1.0f, 0.0f, 0.0f), false, 20);
            //Move arrowhead up in place
            Matrix4 t = Matrix4.CreateRotationZ(MathUtils.radians(90));
            translation_x_axis.applyTransform(t);

            GMDL.Primitives.Arrow translation_y_axis = new GMDL.Primitives.Arrow(0.015f, 0.25f, new Vector3(0.0f, 1.0f, 0.0f), false, 20);
            GMDL.Primitives.Arrow translation_z_axis = new GMDL.Primitives.Arrow(0.015f, 0.25f, new Vector3(0.0f, 0.0f, 1.0f), false, 20);
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
                GMDL.Primitives.Primitive arr = null;
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

                GLPrimitiveMeshVaos[name] = new GLInstancedMeshVao();
                GLPrimitiveMeshVaos[name].type = TYPES.GIZMOPART;
                GLPrimitiveMeshVaos[name].metaData = new MeshMetaData();
                GLPrimitiveMeshVaos[name].metaData.batchcount = arr.geom.indicesCount;
                GLPrimitiveMeshVaos[name].metaData.indicesLength = DrawElementsType.UnsignedInt;
                GLPrimitiveMeshVaos[name].vao = GLPrimitiveVaos[name];
                GLPrimitiveMeshVaos[name].material = GLmaterials["crossMat"];

            }

        }

        public void addDefaultPrimitives()
        {
            //Setup Primitive Vaos

            //Default quad
            GMDL.Primitives.Quad q = new GMDL.Primitives.Quad(1.0f, 1.0f);
            GLPrimitiveVaos["default_quad"] = q.getVAO();
            GLPrimitiveMeshVaos["default_quad"] = new GLInstancedMeshVao();
            GLPrimitiveMeshVaos["default_quad"].vao = GLPrimitiveVaos["default_quad"];
            
            //Default render quad
            q = new GMDL.Primitives.Quad();
            GLPrimitiveVaos["default_renderquad"] = q.getVAO();
            GLPrimitiveMeshVaos["default_renderquad"] = new GLInstancedMeshVao();
            GLPrimitiveMeshVaos["default_renderquad"].vao = GLPrimitiveVaos["default_renderquad"];

            //Default cross
            GMDL.Primitives.Cross c = new GMDL.Primitives.Cross(0.1f, true);
            GLPrimitiveMeshVaos["default_cross"] = new GLInstancedMeshVao();
            GLPrimitiveVaos["default_cross"] = c.getVAO();
            GLPrimitiveMeshVaos["default_cross"].type = TYPES.GIZMO;
            GLPrimitiveMeshVaos["default_cross"].metaData = new MeshMetaData();
            GLPrimitiveMeshVaos["default_cross"].metaData.batchcount = c.geom.indicesCount;
            GLPrimitiveMeshVaos["default_cross"].metaData.AABBMIN = new Vector3(-0.1f);
            GLPrimitiveMeshVaos["default_cross"].metaData.AABBMAX = new Vector3(0.1f);
            GLPrimitiveMeshVaos["default_cross"].metaData.indicesLength = DrawElementsType.UnsignedInt;
            GLPrimitiveMeshVaos["default_cross"].vao = GLPrimitiveVaos["default_cross"];
            GLPrimitiveMeshVaos["default_cross"].material = GLmaterials["crossMat"];

            //Default cube
            GMDL.Primitives.Box bx = new GMDL.Primitives.Box(1.0f, 1.0f, 1.0f, new Vector3(1.0f), true);
            GLPrimitiveVaos["default_box"] = bx.getVAO();
            GLPrimitiveMeshVaos["default_box"] = new GLInstancedMeshVao();
            GLPrimitiveMeshVaos["default_box"].vao = GLPrimitiveVaos["default_box"];

            //Default sphere
            GMDL.Primitives.Sphere sph = new GMDL.Primitives.Sphere(new Vector3(0.0f, 0.0f, 0.0f), 100.0f);
            GLPrimitiveVaos["default_sphere"] = sph.getVAO();
            GLPrimitiveMeshVaos["default_sphere"] = new GLInstancedMeshVao();
            GLPrimitiveMeshVaos["default_sphere"].vao = GLPrimitiveVaos["default_sphere"];

            //Light Sphere Mesh
            GMDL.Primitives.Sphere lsph = new GMDL.Primitives.Sphere(new Vector3(0.0f, 0.0f, 0.0f), 1.0f);
            GLPrimitiveVaos["default_light_sphere"] = lsph.getVAO();
            GLPrimitiveMeshVaos["default_light_sphere"] = new GLInstancedLightMeshVao();
            GLPrimitiveMeshVaos["default_light_sphere"].metaData = new MeshMetaData();
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.batchstart_graphics = 0;
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.vertrstart_graphics = 0;
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.vertrend_graphics = 11 * 11 - 1;
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.batchcount = 10 * 10 * 6;
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.AABBMIN = new Vector3(-0.1f);
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.AABBMAX = new Vector3(0.1f);
            GLPrimitiveMeshVaos["default_light_sphere"].metaData.indicesLength = DrawElementsType.UnsignedInt;
            GLPrimitiveMeshVaos["default_light_sphere"].type = TYPES.LIGHTVOLUME;
            GLPrimitiveMeshVaos["default_light_sphere"].vao = GLPrimitiveVaos["default_light_sphere"];
            GLPrimitiveMeshVaos["default_light_sphere"].material = GLmaterials["lightMat"];
            
            generateGizmoParts();
        }

        public bool shaderExistsForMaterial(Material mat)
        {
            Dictionary<int, GLSLShaderConfig> shaderDict;
            
            //Save shader to resource Manager
            if (mat.Name == "collisionMat" || mat.Name == "crossMat" || mat.Name == "jointMat")
            {
                shaderDict = GLDefaultShaderMap;
            }
            else if (mat.MaterialFlags.Contains("_F51_DECAL_DIFFUSE") ||
                mat.MaterialFlags.Contains("_F52_DECAL_NORMAL"))
            {
                shaderDict = GLDeferredShaderMapDecal;
            }
            else if (mat.MaterialFlags.Contains("_F09_TRANSPARENT") ||
                     mat.MaterialFlags.Contains("_F22_TRANSPARENT_SCALAR") ||
                     mat.MaterialFlags.Contains("_F11_ALPHACUTOUT"))
            {
                shaderDict = GLForwardShaderMapTransparent;
            }
            else
            {
                shaderDict = GLDeferredShaderMap;
            }

            return shaderDict.ContainsKey(mat.shaderHash);
        }

        public void Cleanup()
        {
            //Cleanup global texture manager
            texMgr.cleanup();
            fontMgr.cleanup();
            txtMgr.cleanup();
            //procTextureLayerSelections.Clear();

            foreach (Scene p in GLScenes.Values)
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
            foreach (Material p in GLmaterials.Values)
                p.Dispose();
            GLmaterials.Clear();

            //Cleanup Material Shaders
            opaqueMeshShaderMap.Clear();
            defaultMeshShaderMap.Clear();
            transparentMeshShaderMap.Clear();
            decalMeshShaderMap.Clear();

            activeGLDeferredShaders.Clear();
            activeGLDeferredDecalShaders.Clear();
            activeGLForwardTransparentShaders.Clear();

            GLDeferredShaderMap.Clear();
            GLForwardShaderMapTransparent.Clear();
            GLDeferredShaderMapDecal.Clear();
            GLDefaultShaderMap.Clear();

            //Cleanup archives
            NMSUtils.unloadNMSArchives(this);

            //Cleanup Lights
            foreach (Light p in GLlights)
                p.Dispose();
            GLlights.Clear();

            //Cleanup Cameras
            //TODO: Make Camera Disposable
            //foreach (GMDL.Camera p in GLCameras)
            //    p.Dispose();
            GLCameras.Clear();
        
        }
    }

    


   
}
