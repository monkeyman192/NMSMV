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
    //Class Which will store all the texture resources for better memory management
    public class ResourceManagementSystem : EngineSystem
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
        
        //BufferManagers
        public GLLightBufferManager lightBufferMgr = new();

        //public int[] shader_programs;
        //Extra manager
        public TextureManager texMgr = new();
        public FontManager fontMgr = new();
        public TextManager txtMgr = new();

        public bool initialized = false;

        public ResourceManagementSystem() : base(EngineSystemEnum.RESOURCE_MANAGEMENT_SYSTEM)
        {
               
        }

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
            
            initialized = true;
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
            
        }
    }

    


   
}
