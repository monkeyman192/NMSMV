﻿using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using MVCore.Systems;

namespace MVCore
{
    public class Joint : Model
    {
        public int jointIndex;
        public Vector3 color;
        
        //Add a bunch of shit for posing
        //public Vector3 _localPosePosition = new Vector3(0.0f);
        //public Matrix4 _localPoseRotation = Matrix4.Identity;
        //public Vector3 _localPoseScale = new Vector3(1.0f);
        public Matrix4 BindMat = Matrix4.Identity; //This is the local Bind Matrix related to the parent joint
        public Matrix4 invBMat = Matrix4.Identity; //This is the inverse of the local Bind Matrix related to the parent
                                                   //DO NOT MIX WITH THE gobject.invBMat which is reverts the transformation to the global space

        //Blending Queues
        public List<Vector3> PositionQueue = new List<Vector3>();
        public List<Vector3> ScaleQueue = new List<Vector3>();
        public List<Quaternion> RotationQueue = new List<Quaternion>();

        public GLInstancedMeshVao meshVao;

        //Props
        
        public Joint()
        {
            Type = TYPES.JOINT;
        }

        protected Joint(Joint input) : base(input)
        {
            this.jointIndex = input.jointIndex;
            this.BindMat = input.BindMat;
            this.invBMat = input.invBMat;
            this.color = input.color;

            meshVao = new GLInstancedMeshVao();
            instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);
            GLMeshBufferManager.SetInstanceWorldMat(meshVao, instanceId, Matrix4.Identity);
            meshVao.type = TYPES.JOINT;
            meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs
            meshVao.vao = new Primitives.LineSegment(Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];
        }

        public override void updateMeshInfo(bool lod_filter=false)
        {
            //We do not apply frustum occlusion on joint objects
            if (renderable && (Children.Count > 0))
            {
                //Update Vertex Buffer based on the new positions
                float[] verts = new float[2 * Children.Count * 3];
                int arraysize = 2 * Children.Count * 3 * sizeof(float);

                Vector3 worldPosition = TransformationSystem.GetEntityWorldPosition(this).Xyz;

                for (int i = 0; i < Children.Count; i++)
                {
                    Vector3 childWorldPosition = TransformationSystem.GetEntityWorldPosition(Children[i]).Xyz;
                    verts[i * 6 + 0] = worldPosition.X;
                    verts[i * 6 + 1] = worldPosition.Y;
                    verts[i * 6 + 2] = worldPosition.Z;
                    verts[i * 6 + 3] = childWorldPosition.X;
                    verts[i * 6 + 4] = childWorldPosition.Y;
                    verts[i * 6 + 5] = childWorldPosition.Z;
                }

                meshVao.metaData.batchcount = 2 * Children.Count;

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                instanceId = GLMeshBufferManager.AddInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity);
            }

            base.updateMeshInfo();
        }

        
        //Disposal
        protected override void Dispose(bool disposing)
        {
            //Dispose GL Stuff
            meshVao?.Dispose();
            base.Dispose(disposing);
        }
    }


}
