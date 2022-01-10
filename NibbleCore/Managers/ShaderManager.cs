using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Platform.Graphics.OpenGL; //TODO: Abstract
using OpenTK.Graphics.OpenGL;

namespace NbCore.Managers
{
    public class ShaderManager: EntityManager<GLSLShaderConfig>
    {
        public readonly List<GLSLShaderConfig> GLDeferredShaders = new();
        public readonly List<GLSLShaderConfig> GLForwardTransparentShaders = new();
        public readonly List<GLSLShaderConfig> GLDeferredDecalShaders = new();
        public readonly Queue<GLSLShaderConfig> CompilationQueue = new();

        private readonly Dictionary<SHADER_TYPE, GLSLShaderConfig> GenericShaders = new(); //Generic Shader Map
        private readonly Dictionary<long, GLSLShaderConfig> ShaderHashMap = new();
        private readonly Dictionary<long, List<MeshMaterial>> ShaderMaterialMap = new();


        public bool AddShader(GLSLShaderConfig shader)
        {
            if (Add(shader))
            {
                GUIDComponent gc = shader.GetComponent<GUIDComponent>() as GUIDComponent;
                ShaderHashMap[shader.Hash] = shader;
                ShaderMaterialMap[gc.ID] = new();

                //Add Shader to the corresponding list
                if ((shader.ShaderMode & SHADER_MODE.FORWARD) == SHADER_MODE.FORWARD)
                    GLForwardTransparentShaders.Add(shader);
                else if ((shader.ShaderMode & SHADER_MODE.DECAL) == SHADER_MODE.DECAL)
                    GLDeferredDecalShaders.Add(shader);
                else
                    GLDeferredShaders.Add(shader);

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

        public void AddShaderForCompilation(GLSLShaderConfig shader)
        {
            CompilationQueue.Enqueue(shader);
        }

        public GLSLShaderConfig GetShaderByHash(long hash)
        {
            return ShaderHashMap[hash];
        }

        public GLSLShaderConfig GetShaderByID(long id)
        {
            return Get(id) as GLSLShaderConfig;
        }

        public bool ShaderHashExists(long hash)
        {
            return ShaderHashMap.ContainsKey(hash);
        }

        public bool ShaderIDExists(long ID) //GUID
        {
            return EntityMap.ContainsKey(ID);
        }

        public void AddMaterialToShader(MeshMaterial mat)
        {
            ShaderMaterialMap[mat.Shader.GetID()].Add(mat);
        }

        public bool ShaderContainsMaterial(GLSLShaderConfig shader, MeshMaterial mat)
        {
            return ShaderMaterialMap[shader.GetID()].Contains(mat);
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
            GLDeferredShaders.Clear();
            GLForwardTransparentShaders.Clear();
            GLDeferredDecalShaders.Clear();

            base.CleanUp();
        }
        


    }
}
