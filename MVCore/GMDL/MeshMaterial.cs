﻿using System;
using System.Collections.Generic;
using GLSLHelper;
using MVCore.Common;

namespace MVCore
{
    //Stolen from NMS sorry HG ^.^
    public enum MaterialFlagEnum
    {
        _F01_DIFFUSEMAP,
        _F02_SKINNED,
        _F03_NORMALMAP,
        _F04_,
        _F05_INVERT_ALPHA,
        _F06_BRIGHT_EDGE,
        _F07_UNLIT,
        _F08_REFLECTIVE,
        _F09_TRANSPARENT,
        _F10_NORECEIVESHADOW,
        _F11_ALPHACUTOUT,
        _F12_BATCHED_BILLBOARD,
        _F13_UVANIMATION,
        _F14_UVSCROLL,
        _F15_WIND,
        _F16_DIFFUSE2MAP,
        _F17_MULTIPLYDIFFUSE2MAP,
        _F18_UVTILES,
        _F19_BILLBOARD,
        _F20_PARALLAXMAP,
        _F21_VERTEXCOLOUR,
        _F22_TRANSPARENT_SCALAR,
        _F23_TRANSLUCENT,
        _F24_AOMAP,
        _F25_ROUGHNESS_MASK,
        _F26_STRETCHY_PARTICLE,
        _F27_VBTANGENT,
        _F28_VBSKINNED,
        _F29_VBCOLOUR,
        _F30_REFRACTION,
        _F31_DISPLACEMENT,
        _F32_REFRACTION_MASK,
        _F33_SHELLS,
        _F34_GLOW,
        _F35_GLOW_MASK,
        _F36_DOUBLESIDED,
        _F37_,
        _F38_NO_DEFORM,
        _F39_METALLIC_MASK,
        _F40_SUBSURFACE_MASK,
        _F41_DETAIL_DIFFUSE,
        _F42_DETAIL_NORMAL,
        _F43_NORMAL_TILING,
        _F44_IMPOSTER,
        _F45_VERTEX_BLEND,
        _F46_BILLBOARD_AT,
        _F47_REFLECTION_PROBE,
        _F48_WARPED_DIFFUSE_LIGHTING,
        _F49_DISABLE_AMBIENT,
        _F50_DISABLE_POSTPROCESS,
        _F51_DECAL_DIFFUSE,
        _F52_DECAL_NORMAL,
        _F53_COLOURISABLE,
        _F54_COLOURMASK,
        _F55_MULTITEXTURE,
        _F56_MATCH_GROUND,
        _F57_DETAIL_OVERLAY,
        _F58_USE_CENTRAL_NORMAL,
        _F59_SCREENSPACE_FADE,
        _F60_ACUTE_ANGLE_FADE,
        _F61_CLAMP_AMBIENT,
        _F62_DETAIL_ALPHACUTOUT,
        _F63_DISSOLVE,
        _F64_,
    }
    
    public class MeshMaterial : Entity
    {
        public string Name = "";
        public string Class = "";
        public bool proc = false;
        public string name_key = "";
        public TextureManager texMgr;
        public GLSLShaderConfig Shader;
        public readonly List<Uniform> Uniforms = new();
        public readonly List<Sampler> Samplers = new();
        public readonly Dictionary<string, Sampler> SamplerMap = new();
        public readonly List<MaterialFlagEnum> Flags = new();
        
        public readonly float[] material_flags = new float[64];

        public static List<MaterialFlagEnum> supported_flags = new() {
            MaterialFlagEnum._F01_DIFFUSEMAP,
            MaterialFlagEnum._F03_NORMALMAP,
            MaterialFlagEnum._F07_UNLIT,
            MaterialFlagEnum._F09_TRANSPARENT,
            MaterialFlagEnum._F22_TRANSPARENT_SCALAR,
            MaterialFlagEnum._F11_ALPHACUTOUT,
            MaterialFlagEnum._F14_UVSCROLL,
            MaterialFlagEnum._F16_DIFFUSE2MAP,
            MaterialFlagEnum._F17_MULTIPLYDIFFUSE2MAP,
            MaterialFlagEnum._F21_VERTEXCOLOUR,
            MaterialFlagEnum._F24_AOMAP,
            MaterialFlagEnum._F34_GLOW,
            MaterialFlagEnum._F35_GLOW_MASK,
            MaterialFlagEnum._F39_METALLIC_MASK,
            MaterialFlagEnum._F43_NORMAL_TILING,
            MaterialFlagEnum._F51_DECAL_DIFFUSE,
            MaterialFlagEnum._F52_DECAL_NORMAL,
            MaterialFlagEnum._F55_MULTITEXTURE
        };

        public List<Uniform> ActiveUniforms = new();

        //Disposable Stuff
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public MeshMaterial() : base(EntityType.Material)
        {
            Name = "NULL";
            Class = "NULL";
            Type = EntityType.Material;
            
            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        
        public void CompileShader(string vspath, string fspath)
        {
            //Calculate material hash
            List<string> includes = new();
            for (int i = 0; i < Flags.Count; i++)
            {
                if (supported_flags.Contains(Flags[i]))
                    includes.Add(Flags[i].ToString().Split(".")[^1]);
            }

            int hash  = GLShaderHelper.calculateShaderHash(includes);
            GLSLShaderConfig shader = RenderState.engineRef.GetShaderByHash(hash);

            if (shader == null)
            {
                try
                {
                    Shader = compileMaterialShader(this);
                    RenderState.engineRef.RegisterEntity(Shader);

                    //Load Active Uniforms to Material

                    foreach (Uniform un in Uniforms)
                    {
                        if (Shader.uniformLocations.ContainsKey(un.Name))
                        {
                            un.ShaderLocation = Shader.uniformLocations[un.Name];
                            ActiveUniforms.Add(un);
                        }
                    }

                    foreach (Sampler s in Samplers)
                    {
                        if (Shader.uniformLocations.ContainsKey(s.Name))
                        {
                            s.ShaderLocation = Shader.uniformLocations[s.Name];
                        }
                    }
                } catch (Exception e)
                {
                    Callbacks.Log("Error during material shader compilation: " + e.Message, Common.LogVerbosityLevel.ERROR);
                }
            }
            else
            {
                Shader = shader;
            }
        }

        //Wrapper to support uberflags
        public bool has_flag(MaterialFlagEnum flag)
        {
            return material_flags[(int) flag] > 0.0f;
        }

        public bool add_flag(MaterialFlagEnum flag)
        {
            if (has_flag((flag)))
                return false;

            material_flags[(int) flag] = 1.0f;
            Flags.Add(flag);
            
            return true;
        }

        public MeshMaterial Clone()
        {
            MeshMaterial newmat = new();
            //Remix textures
            return newmat;
        }


        protected override void Dispose(bool disposing)
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

        public static int calculateShaderHash(List<MaterialFlagEnum> flags)
        {
            string hash = "";

            for (int i = 0; i < flags.Count; i++)
            {
                if (supported_flags.Contains(flags[i]))
                    hash += "_" + flags[i];
            }

            if (hash == "")
                hash = "DEFAULT";

            return hash.GetHashCode();
        }

        public static GLSLShaderConfig compileMaterialShader(MeshMaterial mat)
        {
            SHADER_MODE mode = SHADER_MODE.DEFFERED;
            
            List<string> includes = new();
            
            if (mat.Flags.Contains(MaterialFlagEnum._F51_DECAL_DIFFUSE) ||
                mat.Flags.Contains(MaterialFlagEnum._F52_DECAL_NORMAL))
            {
                mode = SHADER_MODE.DECAL | SHADER_MODE.FORWARD;
            } else if (mat.Flags.Contains(MaterialFlagEnum._F09_TRANSPARENT) ||
                       mat.Flags.Contains(MaterialFlagEnum._F22_TRANSPARENT_SCALAR) ||
                       mat.Flags.Contains(MaterialFlagEnum._F11_ALPHACUTOUT))
            {
                mode = SHADER_MODE.FORWARD;
            }
            
            for (int i = 0; i < mat.Flags.Count; i++)
            {
                if (supported_flags.Contains(mat.Flags[i]))
                    includes.Add(mat.Flags[i].ToString().Split(".")[^1]);
            }
            
            string vs_path = "Shaders/Simple_VS.glsl";
            vs_path = System.IO.Path.GetFullPath(vs_path);
            vs_path = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);

            string fs_path = "Shaders/Simple_FS.glsl";
            fs_path = System.IO.Path.GetFullPath(fs_path);
            fs_path = System.IO.Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);

            GLSLShaderSource vs = RenderState.engineRef.GetShaderSourceByFilePath(vs_path);
            GLSLShaderSource fs = RenderState.engineRef.GetShaderSourceByFilePath(fs_path);
            
            GLSLShaderConfig shader = GLShaderHelper.compileShader(vs, fs, null, null, null,
                new(), includes, SHADER_TYPE.MATERIAL_SHADER, mode);
            
            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);

            //Save shader to the resource Manager
            return shader;
        }

    }

}
