using System;
using System.Collections.Generic;
using System.Text;
using GLSLHelper;
using MVCore.GMDL;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace MVCore
{
    public static class MeshRenderer
    {
        public static void renderBBoxes(GLInstancedMeshVao mesh, int pass)
        {
            for (int i = 0; i > mesh.instance_count; i++)
            {
                if (GLMeshBufferManager.GetInstanceOccludedStatus(mesh, i))
                    continue;
                renderBbox(mesh.instanceRefs[i]);
            }
        }

        public static void renderBbox(Model m)
        {
            Vector4[] tr_AABB = new Vector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new Vector4(m.AABBMIN, 1.0f);
            tr_AABB[1] = new Vector4(m.AABBMAX, 1.0f);

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

        public static void renderBSphere(GLInstancedMeshVao mesh, GLSLShaderConfig shader)
        {
            for (int i = 0; i < mesh.instance_count; i++)
            {
                GLVao bsh_Vao = mesh.setupBSphere(i);

                //Rendering
                GL.UseProgram(shader.program_id);
                
                //Step 2 Bind & Render Vao
                //Render Bounding Sphere
                GL.BindVertexArray(bsh_Vao.vao_id);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(PrimitiveType.Triangles, 600, DrawElementsType.UnsignedInt, (IntPtr)0);

                GL.BindVertexArray(0);
                bsh_Vao.Dispose();
            }
        }

        public static void renderMesh(GLInstancedMeshVao mesh)
        {
            GL.BindVertexArray(mesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.metaData.batchcount, mesh.metaData.indicesLength, IntPtr.Zero, mesh.instance_count);
            GL.BindVertexArray(0);
        }

        public static void renderLight(GLInstancedMeshVao mesh)
        {
            GL.BindVertexArray(mesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, mesh.instance_count);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, mesh.instance_count); //Draw both points
            GL.BindVertexArray(0);
        }

        public static void renderCollision(GLInstancedMeshVao mesh)
        {
            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(mesh.vao.vao_id);

            switch (mesh.collisionType)
            {
                //Rendering based on the original mesh buffers
                case COLLISIONTYPES.MESH:
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, mesh.metaData.batchcount,
                        mesh.metaData.indicesLength, IntPtr.Zero, mesh.instance_count, -mesh.metaData.vertrstart_physics);
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, mesh.metaData.batchcount,
                        mesh.metaData.indicesLength, IntPtr.Zero, mesh.instance_count, -mesh.metaData.vertrstart_physics);
                    break;

                //Rendering custom geometry
                case COLLISIONTYPES.BOX:
                case COLLISIONTYPES.CYLINDER:
                case COLLISIONTYPES.CAPSULE:
                case COLLISIONTYPES.SPHERE:
                    GL.DrawElementsInstanced(PrimitiveType.Points, mesh.metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, mesh.instance_count);
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, mesh.metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, mesh.instance_count);
                    break;
            }

            GL.BindVertexArray(0);
        }

        public static void renderLocator(GLInstancedMeshVao mesh)
        {
            GL.BindVertexArray(mesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6,
                mesh.metaData.indicesLength, IntPtr.Zero, mesh.instance_count); //Use Instancing
            GL.BindVertexArray(0);
        }

        public static void renderJoint(GLInstancedMeshVao mesh)
        {
            GL.BindVertexArray(mesh.vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, mesh.metaData.batchcount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public static void renderMain(GLInstancedMeshVao mesh, GLSLShaderConfig shader)
        {
            //Upload Material Information

            //Upload Custom Per Material Uniforms
            foreach (Uniform un in mesh.material.activeUniforms)
            {
                GL.Uniform4(shader.uniformLocations[un.Name], un.vec.vec4);
            }

            //BIND TEXTURES
            //Diffuse Texture
            foreach (Sampler s in mesh.material.PSamplers.Values)
            {
                if (shader.uniformLocations.ContainsKey(s.Name.Value) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name.Value], MyTextureUnit.MapTexUnitToSampler[s.Name.Value]);
                    GL.ActiveTexture(s.texUnit.texUnit);
                    GL.BindTexture(s.tex.target, s.tex.texID);
                }
            }
            
            //BIND TEXTURE Buffer
            if (mesh.skinned)
            {
                GL.Uniform1(shader.uniformLocations["skinMatsTex"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.TextureBuffer, mesh.instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                    SizedInternalFormat.Rgba32f, mesh.instanceBoneMatricesTexTBO);
            }

            //if (instance_count > 100)
            //    Console.WriteLine("Increase the buffers");

            switch (mesh.type)
            {
                case TYPES.GIZMO:
                case TYPES.GIZMOPART:
                case TYPES.MESH:
                case TYPES.LIGHTVOLUME:
                case TYPES.TEXT:
                    renderMesh(mesh);
                    break;
                case TYPES.LOCATOR:
                case TYPES.MODEL:
                    renderLocator(mesh);
                    break;
                case TYPES.JOINT:
                    renderJoint(mesh);
                    break;
                case TYPES.COLLISION:
                    renderCollision(mesh);
                    break;
                case TYPES.LIGHT:
                    renderLight(mesh);
                    break;
            }
        }

        public static void renderMain(GLInstancedLightMeshVao mesh, GLSLShaderConfig shader)
        {
            //Upload Material Information

            //LightInstanceTex
            GL.Uniform1(shader.uniformLocations["lightsTex"], 6);
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.TextureBuffer, mesh.instanceLightTex);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                SizedInternalFormat.Rgba32f, mesh.instanceLightTexTBO);

            GL.BindVertexArray(mesh.vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles,
                mesh.metaData.batchcount, mesh.metaData.indicesLength, IntPtr.Zero, mesh.instance_count);
            GL.BindVertexArray(0);

        }

        public static void renderBHull(GLInstancedMeshVao mesh, GLSLShaderConfig shader)
        {
            if (mesh.bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(mesh.bHullVao.vao_id);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, mesh.metaData.batchcount,
                        mesh.metaData.indicesLength, IntPtr.Zero, -mesh.metaData.vertrstart_physics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, mesh.metaData.batchcount,
                        mesh.metaData.indicesLength, IntPtr.Zero, -mesh.metaData.vertrstart_physics);
            GL.BindVertexArray(0);
        }

        public static void renderDebug(GLInstancedMeshVao mesh, int pass)
        {
            GL.UseProgram(pass);
            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            for (int i = 0; i < mesh.material.Flags.Count; i++)
                GL.Uniform1(loc + (int) mesh.material.Flags[i].MaterialFlag, 1.0f);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            /*
            Util.mulMatArrays(ref skinMats, gobject.invBMats, scene.JMArray, 256);
            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 256, false, skinMats);
            */

            //Step 2: Render VAO
            GL.BindVertexArray(mesh.vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, mesh.metaData.batchcount, DrawElementsType.UnsignedShort, (IntPtr)0);
            GL.BindVertexArray(0);
        }



        //Default render method
        public static bool render(GLInstancedMeshVao mesh, GLSLShaderConfig shader, RENDERPASS pass)
        {
            //Render Object
            switch (pass)
            {
                //Render Main
                case RENDERPASS.DEFERRED:
                case RENDERPASS.FORWARD:
                case RENDERPASS.DECAL:
                    renderMain(mesh, shader);
                    break;
                case RENDERPASS.BBOX:
                case RENDERPASS.BHULL:
                    //renderBbox(shader.program_id, 0);
                    //renderBSphere(shader);
                    renderBHull(mesh, shader);
                    break;
                //Render Debug
                case RENDERPASS.DEBUG:
                    //renderDebug(shader.program_id);
                    break;
                //Render for Picking
                case RENDERPASS.PICK:
                    //renderDebug(shader.program_id);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }

            return true;
        }



    }
}
