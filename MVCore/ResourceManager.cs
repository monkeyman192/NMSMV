﻿using System;
using System.Collections.Generic;
using System.Text;
using MVCore.GMDL;
using libMBIN.NMS.Toolkit;
using OpenTK;
using System.IO;
using GLSLHelper;
using libPSARC;
using System.Diagnostics.Contracts;
using MathNet.Numerics.Statistics;
using MVCore.Common;

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
        public Dictionary<string, GMDL.Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public Dictionary<string, GMDL.GeomObject> GLgeoms = new Dictionary<string, GMDL.GeomObject>();
        public Dictionary<string, GMDL.scene> GLScenes = new Dictionary<string, GMDL.scene>();
        public Dictionary<string, GMDL.Texture> GLTextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, AnimMetadata> Animations = new Dictionary<string, AnimMetadata>();

        public Dictionary<string, GLVao> GLPrimitiveVaos = new Dictionary<string, GLVao>();
        public Dictionary<string, GLVao> GLVaos = new Dictionary<string, GLVao>();
        public Dictionary<string, GLMeshVao> GLPrimitiveMeshVaos = new Dictionary<string, GLMeshVao>();
        
        public List<GMDL.Light> GLlights = new List<GMDL.Light>();
        public List<Camera> GLCameras = new List<Camera>();
        //public Dictionary<string, int> GLShaders = new Dictionary<string, int>();
        public Dictionary<GLSLHelper.SHADER_TYPE, GLSLHelper.GLSLShaderConfig> GLShaders = new Dictionary<GLSLHelper.SHADER_TYPE, GLSLHelper.GLSLShaderConfig>(); //Generic Shaders
        
        public Dictionary<int, GLSLShaderConfig> GLDeferredLITShaderMap = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLDeferredUNLITShaderMap = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLForwardShaderMapTransparent = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLDeferredShaderMapDecal = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLDefaultShaderMap = new Dictionary<int, GLSLShaderConfig>();

        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredLITShaders = new List<GLSLHelper.GLSLShaderConfig>();
        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredUNLITShaders = new List<GLSLHelper.GLSLShaderConfig>();
        public List<GLSLHelper.GLSLShaderConfig> activeGLForwardTransparentShaders = new List<GLSLHelper.GLSLShaderConfig>();
        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredDecalShaders = new List<GLSLHelper.GLSLShaderConfig>();

        public Dictionary<int, List<GLMeshVao>> opaqueMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();
        public Dictionary<int, List<GLMeshVao>> defaultMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();
        public Dictionary<int, List<GLMeshVao>> transparentMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();
        public Dictionary<int, List<GLMeshVao>> decalMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();

        //Global NMS File Archive handles
        public Dictionary<string, libPSARC.PSARC.Archive> NMSFileToArchiveMap = new Dictionary<string, libPSARC.PSARC.Archive>();
        public SortedDictionary<string, libPSARC.PSARC.Archive> NMSArchiveMap = new SortedDictionary<string, libPSARC.PSARC.Archive>();

        //public int[] shader_programs;
        public textureManager texMgr = new textureManager();

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

            initialized = true;
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            //Add Default textures
            //White tex
            string texpath = "default.dds";
            Texture tex = new Texture();
            tex.textureInit(WPFModelViewer.Properties.Resources._default, texpath); //Manually load data
            texMgr.addTexture(tex);

            //Transparent Mask
            texpath = "default_mask.dds";
            tex = new Texture();
            tex.textureInit(WPFModelViewer.Properties.Resources.default_mask, texpath);
            texMgr.addTexture(tex);
        }

        private void addDefaultLights()
        {
            //Add one and only light for now
            Light light = new Light
            {
                name = "Default Light",
                intensity = 50,
                localPosition = new Vector3(100.0f, 100.0f, 100.0f)
            };
        
            light.meshVao = new GLMeshVao();
            light.meshVao.vao = new MVCore.Primitives.LineSegment(1, new Vector3(1.0f, 1.0f, 1.0f)).getVAO();
            light.meshVao.metaData = new MeshMetaData();
            light.meshVao.metaData.batchcount = 2;
            light.meshVao.material = GLmaterials["lightMat"];
            
            
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
            TkMaterialUniform uf = new TkMaterialUniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 1.0f;
            uf.Values.z = 1.0f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["crossMat"] = mat;
            
            //Joint Material
            mat = new Material();
            mat.Name = "jointMat";
            mat.add_flag(TkMaterialFlags.UberFlagEnum._F07_UNLIT);

            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 0.0f;
            uf.Values.z = 0.0f;
            uf.Values.t = 1.0f;
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

        public void addMaterial(Material mat)
        {
            GLmaterials[mat.name_key] = mat;
            //Assign material to shaders
        }

        public void addDefaultPrimitives()
        {
            //Setup Primitive Vaos
            
            //Default quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad(1.0f, 1.0f);
            GLPrimitiveVaos["default_quad"] = q.getVAO();
            GLPrimitiveMeshVaos["default_quad"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_quad"].vao = GLPrimitiveVaos["default_quad"];
            
            //Default render quad
            q = new MVCore.Primitives.Quad();
            GLPrimitiveVaos["default_renderquad"] = q.getVAO();
            GLPrimitiveMeshVaos["default_renderquad"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_renderquad"].vao = GLPrimitiveVaos["default_renderquad"];

            //Default cross
            MVCore.Primitives.Cross c = new MVCore.Primitives.Cross(0.1f);
            GLPrimitiveVaos["default_cross"] = c.getVAO();
            GLPrimitiveMeshVaos["default_cross"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_cross"].metaData = new MeshMetaData();
            GLPrimitiveMeshVaos["default_cross"].metaData.batchcount = 6;
            GLPrimitiveMeshVaos["default_cross"].indicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
            GLPrimitiveMeshVaos["default_cross"].vao = GLPrimitiveVaos["default_cross"];
            GLPrimitiveMeshVaos["default_cross"].material = GLmaterials["crossMat"];

            //Default cube
            MVCore.Primitives.Box bx = new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f);
            GLPrimitiveVaos["default_box"] = bx.getVAO();
            GLPrimitiveMeshVaos["default_box"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_box"].vao = GLPrimitiveVaos["default_box"];

            //Default sphere
            Primitives.Sphere sph = new Primitives.Sphere(new Vector3(0.0f, 0.0f, 0.0f), 100.0f);
            GLPrimitiveVaos["default_sphere"] = sph.getVAO();
            GLPrimitiveMeshVaos["default_sphere"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_sphere"].vao = GLPrimitiveVaos["default_sphere"];

            //Default arrow
            //Primitives.ArrowHead arr = new Primitives.ArrowHead(new Vector3(1.0f, 0.0f, 0.0f));
            //Primitives.Arrow arr = new Primitives.Arrow(0.1f, 1.0f, new Vector3(1.0f, 0.0f, 0.0f), true);
            Primitives.TranslationGizmo arr = new Primitives.TranslationGizmo(true);
            GLPrimitiveVaos["default_translation_gizmo"] = arr.getVAO();
            GLPrimitiveMeshVaos["default_translation_gizmo"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_translation_gizmo"].type = TYPES.GIZMO;
            GLPrimitiveMeshVaos["default_translation_gizmo"].metaData = new MeshMetaData();
            GLPrimitiveMeshVaos["default_translation_gizmo"].metaData.batchcount = arr.geom.indicesCount;
            GLPrimitiveMeshVaos["default_translation_gizmo"].indicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
            GLPrimitiveMeshVaos["default_translation_gizmo"].vao = GLPrimitiveVaos["default_translation_gizmo"];
            GLPrimitiveMeshVaos["default_translation_gizmo"].material = GLmaterials["crossMat"];
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
            else if (mat.MaterialFlags.Contains("_F07_UNLIT"))
            {
                shaderDict = GLDeferredUNLITShaderMap;
            }
            else
            {
                shaderDict = GLDeferredLITShaderMap;
            }

            return shaderDict.ContainsKey(mat.shaderHash);
        }

        public void Cleanup()
        {
            //Cleanup global texture manager
            texMgr.cleanup();
            //procTextureLayerSelections.Clear();

            foreach (GMDL.scene p in GLScenes.Values)
                p.Dispose();
            GLScenes.Clear();

            //Cleanup Geom Objects
            foreach (GMDL.GeomObject p in GLgeoms.Values)
                p.Dispose();
            GLgeoms.Clear();

            //Cleanup GLVaos
            foreach (GMDL.GLVao p in GLVaos.Values)
                p.Dispose();
            GLVaos.Clear();
            
            //Cleanup Animations
            Animations.Clear();

            //Cleanup Materials
            foreach (GMDL.Material p in GLmaterials.Values)
                p.Dispose();
            GLmaterials.Clear();

            //Cleanup Material Shaders
            opaqueMeshShaderMap.Clear();
            defaultMeshShaderMap.Clear();
            transparentMeshShaderMap.Clear();
            decalMeshShaderMap.Clear();

            activeGLDeferredLITShaders.Clear();
            activeGLDeferredUNLITShaders.Clear();
            activeGLDeferredDecalShaders.Clear();
            activeGLForwardTransparentShaders.Clear();

            GLDeferredLITShaderMap.Clear();
            GLDeferredUNLITShaderMap.Clear();
            GLForwardShaderMapTransparent.Clear();
            GLDeferredShaderMapDecal.Clear();
            GLDefaultShaderMap.Clear();

            //Cleanup Lights
            foreach (GMDL.Light p in GLlights)
                p.Dispose();
            GLlights.Clear();

            //Cleanup Cameras
            //TODO: Make Camera Disposable
            //foreach (GMDL.Camera p in GLCameras)
            //    p.Dispose();
            GLCameras.Clear();
        
        }
    }

    public class textureManager: baseResourceManager
    {
        public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        private textureManager masterTexManager;

        public textureManager()
        {
        
        }

        public void cleanup()
        {
            deleteTextures();
            removeTextures();
        }

        public void deleteTextures()
        {
            foreach (GMDL.Texture p in GLtextures.Values)
                p.Dispose();
        }

        public void removeTextures()
        {
            //Warning does not free the textures. Use wisely
            GLtextures.Clear();
        }

        public bool hasTexture(string name)
        {
            //Search on the masterTextureManager first
            if (masterTexManager != null && masterTexManager.hasTexture(name))
                return true;
            else
                return GLtextures.ContainsKey(name);
        }

        public void addTexture(GMDL.Texture t)
        {
            GLtextures[t.name] = t;
        }

        public GMDL.Texture getTexture(string name)
        {
            //Fetches the textures from the masterTexture Manager if it exists
            if (masterTexManager != null && masterTexManager.hasTexture(name))
                return masterTexManager.getTexture(name);
            else
                return GLtextures[name];
        }

        public void deleteTexture(string name)
        {
            if (GLtextures.ContainsKey(name))
            {
                GMDL.Texture t = GLtextures[name];
                t.Dispose();
                GLtextures.Remove(name);
            }
            else
                CallBacks.Log("Texture not found\n");
        }

        public void setMasterTexManager(textureManager mtMgr)
        {
            masterTexManager = mtMgr;
        }



    }


   
}
