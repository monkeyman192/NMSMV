using System;
using System.Collections.Generic;
using NbCore;
using NbCore.Math;

using OpenTK.Graphics.OpenGL4;

namespace NbOpenGLAPI
{
    public class GLRenderer : IRenderApi
    {
        public Dictionary<NbMesh, GLInstancedMesh> MeshMap = new();
        
        public void AddMesh(NbMesh mesh)
        {
            if (!MeshMap.ContainsKey(mesh))
            { 
                //Generate instanced mesh
                GLInstancedMesh imesh = GenerateAPIMesh(mesh);
                MeshMap[mesh] = imesh;
            }
        }

        public void RenderMesh()
        {
            throw new NotImplementedException();
        }

        private GLInstancedMesh GenerateAPIMesh(NbMesh mesh)
        {
            GLInstancedMesh imesh = new(mesh);
            return imesh;
        }

        public void RenderMesh(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh]; //Fetch GL Mesh
            
            SetShaderAndUniforms(glmesh, mat); //Set Shader and material uniforms
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.MetaData.BatchCount, glmesh.IndicesLength, IntPtr.Zero, 
                mesh.RenderedInstanceCount);
            GL.BindVertexArray(0);
        }

        public void RenderLocator(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh];
            
            SetShaderAndUniforms(glmesh, mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6,
                glmesh.IndicesLength, IntPtr.Zero,
                mesh.RenderedInstanceCount); //Use Instancing
            GL.BindVertexArray(0);
        }

        public void RenderJoint(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh];
            
            SetShaderAndUniforms(glmesh, mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, mesh.MetaData.BatchCount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public void RenderCollision(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh];
            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(glmesh.vao.vao_id);
            
            //TODO: make sure that primitive collisions have the vertrstartphysics set to 0
    
            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, glmesh.Mesh.MetaData.BatchCount,
                glmesh.IndicesLength, IntPtr.Zero, glmesh.Mesh.RenderedInstanceCount, -glmesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, glmesh.Mesh.MetaData.BatchCount,
                glmesh.IndicesLength, IntPtr.Zero, glmesh.Mesh.RenderedInstanceCount, -glmesh.Mesh.MetaData.VertrStartPhysics);
            
            GL.BindVertexArray(0);
        }

        public void RenderLight(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedMesh glmesh = MeshMap[mesh];
            
            SetShaderAndUniforms(glmesh, mat);
            
            GL.BindVertexArray(glmesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, mesh.RenderedInstanceCount);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, mesh.RenderedInstanceCount); //Draw both points
            GL.BindVertexArray(0);
        }

        public void RenderLightVolume(NbMesh mesh, MeshMaterial mat)
        {
            GLInstancedLightMesh glmesh = MeshMap[mesh] as GLInstancedLightMesh;
            
            //Upload Material Information
            GLSLShaderConfig shader = mat.Shader;

            //LightInstanceTex
            GL.Uniform1(shader.uniformLocations["lightsTex"], 6);
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.TextureBuffer, glmesh.instanceLightTex);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                SizedInternalFormat.Rgba32f, glmesh.instanceLightTexTBO);

            RenderMesh(glmesh.Mesh, mat);
        }


        public void renderBBoxes(GLInstancedMesh mesh, int pass)
        {
            for (int i = 0; i > mesh.Mesh.RenderedInstanceCount; i++)
            {
                renderBbox(mesh.Mesh.instanceRefs[i]);
            }
        }

        public void uploadSkinningData(GLInstancedMesh mesh)
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, mesh.instanceBoneMatricesTexTBO);
            int bufferSize = mesh.Mesh.RenderedInstanceCount * 128 * 16 * 4;
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, mesh.Mesh.instanceBoneMatrices);
            //Console.WriteLine(GL.GetError());
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }
        
        public void renderBbox(MeshComponent mc)
        {
            if (mc == null)
                return;

            NbVector4[] tr_AABB = new NbVector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new NbVector4(mc.Mesh.MetaData.AABBMIN, 1.0f);
            tr_AABB[1] = new NbVector4(mc.Mesh.MetaData.AABBMAX, 1.0f);

            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 0.0f);
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 0.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Indices
            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts1.Length;
            GL.GenBuffers(1, out int vb_bbox);
            GL.GenBuffers(1, out int eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts1);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }
    
        public static void renderBHull(GLInstancedMesh mesh)
        {
            if (mesh.bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(mesh.bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength, 
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, mesh.Mesh.MetaData.BatchCount,
                mesh.IndicesLength, 
                IntPtr.Zero, -mesh.Mesh.MetaData.VertrStartPhysics);
            GL.BindVertexArray(0);
        }

        private void SetShaderAndUniforms(GLInstancedMesh Mesh, MeshMaterial Material)
        {
            GLSLShaderConfig shader = Material.Shader;
            
            //Upload Material Information
            
            //Upload Custom Per Material Uniforms
            foreach (Uniform un in Material.ActiveUniforms)
                GL.Uniform4(un.ShaderLocation, un.Values);
            
            //BIND TEXTURES
            //Diffuse Texture
            foreach (Sampler s in Material.Samplers)
            {
                if (shader.uniformLocations.ContainsKey(s.Name) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name], s.SamplerID);
                    GL.ActiveTexture(s.texUnit);
                    GL.BindTexture(s.Tex.target, s.Tex.texID);
                }
            }
            
            //BIND TEXTURE Buffer
            if (Mesh.Mesh.skinned)
            {
                GL.Uniform1(shader.uniformLocations["skinMatsTex"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.TextureBuffer, Mesh.instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                    SizedInternalFormat.Rgba32f, Mesh.instanceBoneMatricesTexTBO);
            }
        }
        
        public void renderMain(GLInstancedLightMesh mesh, MeshMaterial material)
        {
            
        }
    }
}