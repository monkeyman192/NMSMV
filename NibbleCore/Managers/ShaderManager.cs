using System;
using System.Collections.Generic;
using System.Text;
using NbOpenGLAPI;
using OpenTK.Graphics.OpenGL;

namespace NbCore.Managers
{
    public class ShaderManager: EntityManager<GLSLShaderConfig>
    {
        public readonly List<GLSLShaderConfig> GLDeferredShaders = new();
        public readonly List<GLSLShaderConfig> GLForwardTransparentShaders = new();
        public readonly List<GLSLShaderConfig> GLDeferredDecalShaders = new();

        private readonly Dictionary<SHADER_TYPE, GLSLShaderConfig> GenericShaders = new(); //Generic Shader Map
        private readonly Dictionary<long, GLSLShaderConfig> ShaderMap = new();
        private readonly Dictionary<int, GLSLShaderConfig> ShaderHashMap = new();
        private readonly Dictionary<long, List<MeshMaterial>> ShaderMaterialMap = new();


        public void IdentifyActiveShaders()
        {
            GLDeferredShaders.Clear();
            GLDeferredDecalShaders.Clear();
            GLForwardTransparentShaders.Clear();

            foreach (GLSLShaderConfig conf in ShaderMap.Values)
            {
                if ((conf.ShaderMode & SHADER_MODE.FORWARD) == SHADER_MODE.FORWARD)
                    GLForwardTransparentShaders.Add(conf);
                else if ((conf.ShaderMode & SHADER_MODE.DECAL) == SHADER_MODE.DECAL)
                    GLDeferredDecalShaders.Add(conf);
                else
                    GLDeferredShaders.Add(conf);
            }
        }

        public bool AddShader(GLSLShaderConfig shader)
        {
            if (Add(shader))
            {
                GUIDComponent gc = shader.GetComponent<GUIDComponent>() as GUIDComponent;
                ShaderMap[gc.ID] = shader;
                ShaderHashMap[shader.Hash] = shader;
                ShaderMaterialMap[gc.ID] = new();
                return true;
            }
            return false;
        }

        public bool AddGenericShader(GLSLShaderConfig shader, SHADER_TYPE stype)
        {
            if (AddShader(shader))
            {
                GenericShaders[stype] = shader;
                return true;
            }
            return false;
        }

        public GLSLShaderConfig GetGenericShader(SHADER_TYPE stype)
        {
            return GenericShaders[stype];
        }

        public bool ShaderExists(long ID)
        {
            return ShaderMap.ContainsKey(ID);
        }

        public void AddMaterialToShader(MeshMaterial mat)
        {
            ShaderMaterialMap[mat.Shader.GetID()].Add(mat);
        }

        public List<MeshMaterial> GetShaderMaterials(GLSLShaderConfig shader)
        {
            return ShaderMaterialMap[shader.GetID()];
        }

        public new void CleanUp()
        {
            //Shader Cleanup
            ShaderMaterialMap.Clear();
            ShaderHashMap.Clear();
            ShaderMap.Clear();
            GLDeferredShaders.Clear();
            GLForwardTransparentShaders.Clear();
            GLDeferredDecalShaders.Clear();

            base.CleanUp();
        }
        


    }
}
