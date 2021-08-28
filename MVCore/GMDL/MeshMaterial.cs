using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using GLSLHelper;

namespace MVCore
{
    public class MeshMaterial : TkMaterialData, IDisposable
    {
        private bool disposed = false;
        public bool proc = false;
        public float[] material_flags = new float[64];
        public string name_key = "";
        public TextureManager texMgr;
        public int shaderHash = int.MaxValue;
        public GLSLShaderConfig shader;

        public static List<string> supported_flags = new() {
                "_F01_DIFFUSEMAP",
                "_F02_SKINNED",
                "_F03_NORMALMAP",
                "_F07_UNLIT",
                "_F09_TRANSPARENT",
                "_F11_ALPHACUTOUT",
                "_F14_UVSCROLL",
                "_F16_DIFFUSE2MAP",
                "_F17_MULTIPLYDIFFUSE2MAP",
                "_F21_VERTEXCOLOUR",
                "_F22_TRANSPARENT_SCALAR",
                "_F24_AOMAP",
                "_F34_GLOW",
                "_F35_GLOW_MASK",
                "_F39_METALLIC_MASK",
                "_F43_NORMAL_TILING",
                "_F51_DECAL_DIFFUSE",
                "_F52_DECAL_NORMAL",
                "_F55_MULTITEXTURE"};

        public string PName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }

        public string PClass
        {
            get
            {
                return Class;
            }
        }

        public List<string> MaterialFlags
        {
            get
            {
                List<string> l = new();

                foreach (TkMaterialFlags f in Flags)
                {
                    l.Add(((TkMaterialFlags.UberFlagEnum)f.MaterialFlag).ToString());
                }

                return l;
            }
        }

        public string type;
        //public MatOpts opts;
        public Dictionary<string, Sampler> _PSamplers = new();

        public Dictionary<string, Sampler> PSamplers
        {
            get
            {
                return _PSamplers;
            }
        }

        private readonly Dictionary<string, Uniform> _CustomPerMaterialUniforms = new();
        public Dictionary<string, Uniform> CustomPerMaterialUniforms
        {
            get
            {
                return _CustomPerMaterialUniforms;
            }
        }

        public List<Uniform> activeUniforms = new();

        public MeshMaterial()
        {
            Name = "NULL";
            Shader = "NULL";
            Link = "NULL";
            Class = "NULL";
            TransparencyLayerID = -1;
            CastShadow = false;
            DisableZTest = false;
            Flags = new();
            Samplers = new();
            Uniforms = new();

            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public MeshMaterial(TkMaterialData md)
        {
            Name = md.Name;
            Shader = md.Shader;
            Link = md.Link;
            Class = md.Class;
            TransparencyLayerID = md.TransparencyLayerID;
            CastShadow = md.CastShadow;
            DisableZTest = md.DisableZTest;
            Flags = new();
            Samplers = new();
            Uniforms = new();

            for (int i = 0; i < md.Flags.Count; i++)
                Flags.Add(md.Flags[i]);
            for (int i = 0; i < md.Samplers.Count; i++)
                Samplers.Add(md.Samplers[i]);
            for (int i = 0; i < md.Uniforms.Count; i++)
                Uniforms.Add(md.Uniforms[i]);

            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public static MeshMaterial Parse(string path, TextureManager input_texMgr)
        {
            //Load template
            //Try to use libMBIN to load the Material files
            TkMaterialData template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkMaterialData;
#if DEBUG
            //Save NMSTemplate to exml
            template.WriteToExml("Temp\\" + template.Name + ".exml");
#endif

            //Make new material based on the template
            MeshMaterial mat = new(template);

            mat.texMgr = input_texMgr;
            mat.init();
            return mat;
        }

        public void init()
        {
            //Get MaterialFlags
            foreach (TkMaterialFlags f in Flags)
                material_flags[(int)f.MaterialFlag] = 1.0f;

            //Get Uniforms
            foreach (TkMaterialUniform un in Uniforms)
            {
                Uniform my_un = new("mpCustomPerMaterial.", un);
                CustomPerMaterialUniforms[my_un.Name] = my_un;
            }

            //Get Samplers
            foreach (TkMaterialSampler sm in Samplers)
            {
                Sampler s = new(sm);
                s.init(texMgr);
                PSamplers[s.PName] = s;
            }


            //Workaround for Procedurally Generated Samplers
            //I need to check if the diffuse sampler is procgen and then force the maps
            //on the other samplers with the appropriate names

            foreach (Sampler s in PSamplers.Values)
            {
                //Check if the first sampler is procgen
                if (s.isProcGen)
                {
                    string name = s.Map;

                    //Properly assemble the mask and the normal map names

                    string[] split = name.Split('.');
                    string pre_ext_name = "";
                    for (int i = 0; i < split.Length - 1; i++)
                        pre_ext_name += split[i] + '.';

                    if (PSamplers.ContainsKey("mpCustomPerMaterial.gMasksMap"))
                    {
                        string new_name = pre_ext_name + "MASKS.DDS";
                        PSamplers["mpCustomPerMaterial.gMasksMap"].PMap = new_name;
                        PSamplers["mpCustomPerMaterial.gMasksMap"].tex = PSamplers["mpCustomPerMaterial.gMasksMap"].texMgr.GetTexture(new_name);
                    }

                    if (PSamplers.ContainsKey("mpCustomPerMaterial.gNormalMap"))
                    {
                        string new_name = pre_ext_name + "NORMAL.DDS";
                        PSamplers["mpCustomPerMaterial.gNormalMap"].PMap = new_name;
                        PSamplers["mpCustomPerMaterial.gNormalMap"].tex = PSamplers["mpCustomPerMaterial.gNormalMap"].texMgr.GetTexture(new_name);
                    }
                    break;
                }
            }

            //Calculate material hash
            List<string> includes = new();
            for (int i = 0; i < MaterialFlags.Count; i++)
            {
                if (supported_flags.Contains(MaterialFlags[i]))
                    includes.Add(MaterialFlags[i]);
            }

            shaderHash = GLShaderHelper.calculateShaderHash(includes);

            if (!Common.RenderState.activeResMgr.ShaderExistsForMaterial(this))
            {
                try
                {
                    compileMaterialShader();

                    //Get Active Uniforms
                    foreach (Uniform un in CustomPerMaterialUniforms.Values)
                    {
                        if (shader.uniformLocations.ContainsKey(un.Name))
                            activeUniforms.Add(un);
                    }

                } catch (Exception e)
                {
                    Common.Callbacks.Log("Error during material shader compilation: " + e.Message, Common.LogVerbosityLevel.ERROR);
                }
            }
                

        }

        //Wrapper to support uberflags
        public bool has_flag(TkMaterialFlags.UberFlagEnum flag)
        {
            return has_flag((TkMaterialFlags.MaterialFlagEnum)flag);
        }

        public bool has_flag(TkMaterialFlags.MaterialFlagEnum flag)
        {
            for (int i = 0; i < Flags.Count; i++)
            {
                if (Flags[i].MaterialFlag == flag)
                    return true;
            }
            return false;
        }

        public bool add_flag(TkMaterialFlags.UberFlagEnum flag)
        {
            //Check if material has flag
            foreach (TkMaterialFlags f in Flags)
            {
                if (f.MaterialFlag == (TkMaterialFlags.MaterialFlagEnum)flag)
                    return false;
            }

            TkMaterialFlags ff = new();
            ff.MaterialFlag = (TkMaterialFlags.MaterialFlagEnum)flag;
            Flags.Add(ff);

            return true;
        }

        public MeshMaterial Clone()
        {
            MeshMaterial newmat = new();
            //Remix textures
            return newmat;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //DISPOSE SAMPLERS HERE
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~MeshMaterial()
        {
            Dispose(false);
        }

        public static int calculateShaderHash(List<TkMaterialFlags> flags)
        {
            string hash = "";

            for (int i = 0; i < flags.Count; i++)
            {
                string s_flag = ((TkMaterialFlags.UberFlagEnum)flags[i].MaterialFlag).ToString();
                if (supported_flags.Contains(s_flag))
                    hash += "_" + s_flag;
            }

            if (hash == "")
                hash = "DEFAULT";

            return hash.GetHashCode();
        }

        private void compileMaterialShader()
        {
            Dictionary<int, GLSLShaderConfig> shaderDict;
            Dictionary<int, List<MeshComponent>> meshList;

            List<string> includes = new();
            List<string> defines = new();

            //Save shader to resource Manager
            //Check for explicit materials
            if (Name == "collisionMat" || Name == "jointMat" || Name == "crossMat")
            {
                shaderDict = Common.RenderState.activeResMgr.GLDefaultShaderMap;
                meshList = Common.RenderState.activeResMgr.defaultMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }
            else if (MaterialFlags.Contains("_F51_DECAL_DIFFUSE") ||
                MaterialFlags.Contains("_F52_DECAL_NORMAL"))
            {
                shaderDict = Common.RenderState.activeResMgr.GLDeferredShaderMapDecal;
                meshList = Common.RenderState.activeResMgr.decalMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }
            else if (MaterialFlags.Contains("_F09_TRANSPARENT") ||
                     MaterialFlags.Contains("_F22_TRANSPARENT_SCALAR") ||
                     MaterialFlags.Contains("_F11_ALPHACUTOUT"))
            {
                shaderDict = Common.RenderState.activeResMgr.GLForwardShaderMapTransparent;
                meshList = Common.RenderState.activeResMgr.transparentMeshShaderMap;
            }
            else
            {
                shaderDict = Common.RenderState.activeResMgr.GLDeferredShaderMap;
                meshList = Common.RenderState.activeResMgr.opaqueMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }

            for (int i = 0; i < MaterialFlags.Count; i++)
            {
                if (supported_flags.Contains(MaterialFlags[i]))
                    includes.Add(MaterialFlags[i]);
            }

            shader = GLShaderHelper.compileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl", null, null, null,
                defines, includes, SHADER_TYPE.MATERIAL_SHADER);
            
            //TODO: Add shader compilation log of material shaders

            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);


            //Save shader to the resource Manager
            shaderDict[shader.shaderHash] = shader;
            meshList[shader.shaderHash] = new(); //Init list
        }

    }

}
