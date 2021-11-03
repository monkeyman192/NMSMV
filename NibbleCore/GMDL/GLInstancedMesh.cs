using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Math;
using OpenTK.Graphics.OpenGL4;
using NbCore.Utils;


namespace NbCore
{
    public class GLInstancedMesh: Entity
    {
        //Class static properties
        public string Name;
        public GLVao vao;
        public GLVao bHullVao;
        public MeshMetaData MetaData;
        public GeomObject GObject;
        public float[] dataBuffer = new float[256];
        
        //Mesh type
        public COLLISIONTYPES collisionType;
        public SceneNodeType type;

        //Instance Data
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Offset 

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        public bool skinned = false;

        //Material Properties
        public NbVector3 color; //Keep a default color for the mesh

        public int InstanceCount = 0;
        public int RenderedInstanceCount = 0;
        public List<MeshComponent> instanceRefs = new();
        public float[] instanceBoneMatrices;
        public int instanceBoneMatricesTex;
        public int instanceBoneMatricesTexTBO;

        public const int MAX_INSTANCES = 512;

        public GLInstancedMesh() : base(EntityType.Mesh)
        {
            vao = new GLVao();
            MetaData = new MeshMetaData();
        }

        public GLInstancedMesh(MeshMetaData MD) : base(EntityType.Mesh)
        {
            vao = new GLVao();
            MetaData = new MeshMetaData(MD); //Copy MetaData
        }

        public void setSkinMatrices(SceneComponent sc, int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;
            
            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                Array.Copy(sc.skinMats, BoneRemapIndices[i] * 16, instanceBoneMatrices, instance_offset + i * 16, 16);
            }
        }

        public void setDefaultSkinMatrices(int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;
            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                MathUtils.insertMatToArray16(instanceBoneMatrices, instance_offset + i * 16, NbMatrix4.Identity());
            }

        }

        public void initializeSkinMatrices(SceneComponent sc)
        {
            if (RenderedInstanceCount == 0 || sc == null)
                return;

            int jointCount = sc.jointDict.Values.Count;

            //TODO: Use the jointCount to adaptively setup the instanceBoneMatrices
            //Console.WriteLine("MAX : 128  vs Effective : " + jointCount.ToString());

            //Re-initialize the array based on the number of instances
            instanceBoneMatrices = new float[RenderedInstanceCount * 128 * 16];
            int bufferSize = RenderedInstanceCount * 128 * 16 * 4;

            //Setup the TBO
            instanceBoneMatricesTex = GL.GenTexture();
            instanceBoneMatricesTexTBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            GL.BufferData(BufferTarget.TextureBuffer, bufferSize, instanceBoneMatrices, BufferUsageHint.StreamDraw);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);

        }

        public void uploadSkinningData()
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            int bufferSize = RenderedInstanceCount * 128 * 16 * 4;
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, instanceBoneMatrices);
            //Console.WriteLine(GL.GetError());
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }


        #region IDisposable Support
        private bool disposed = false;
        private Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    BoneRemapIndices = null;
                    instanceBoneMatrices = null;

                    
                    handle?.Dispose();
                }

                vao?.Dispose();

                if (instanceBoneMatricesTex > 0)
                {
                    GL.DeleteTexture(instanceBoneMatricesTex);
                    GL.DeleteBuffer(instanceBoneMatricesTexTBO);
                }
                
                disposed = true;
                base.Dispose(disposing);
            }
        }

        #endregion



    }



//    public class Mesh : Model
//    {
//        public GLInstancedMesh meshVao;

//        public int LodLevel
//        {
//            get
//            {
//                return metaData.LODLevel;
//            }

//        }

//        public ulong Hash
//        {
//            get
//            {
//                return metaData.Hash;
//            }
//        }

//        public MeshMetaData metaData = new MeshMetaData();
//        public Vector3 color = new Vector3(); //Per instance
//        public bool hasLOD = false;
        
//        public GLVao bHull_Vao;
//        public GeomObject gobject; //Ref to the geometry shit

//        private static List<string> supportedCommonPerMeshUniforms = new List<string>() { "gUserDataVec4" };

//        private Dictionary<string, Uniform> _CommonPerMeshUniforms = new Dictionary<string, Uniform>();

//        public Dictionary<string, Uniform> CommonPerMeshUniforms
//        {
//            get
//            {
//                return _CommonPerMeshUniforms;
//            }
//        }

//        //Constructor
//        public Mesh() : base()
//        {
//            Type = TYPES.MESH;
//            metaData = new MeshMetaData();



//            //Init MeshModel Uniforms
//            foreach (string un in supportedCommonPerMeshUniforms)
//            {
//                Uniform my_un = new Uniform(un);
//                _CommonPerMeshUniforms[my_un.Name] = my_un;
//            }
//        }

//        public Mesh(Mesh input) : base(input)
//        {
//            //Copy attributes
//            this.metaData = new MeshMetaData(input.metaData);

//            //Copy Vao Refs
//            this.meshVao = input.meshVao;

//            //Material Stuff
//            this.color = input.color;

//            this.palette = input.palette;
//            this.gobject = input.gobject; //Leave geometry file intact, no need to copy anything here
//        }

//        public void copyFrom(Mesh input)
//        {
//            //Copy attributes
//            metaData = new MeshMetaData(input.metaData);
//            hasLOD = input.hasLOD;

//            //Copy Vao Refs
//            meshVao = input.meshVao;

//            //Material Stuff
//            color = input.color;

//            palette = input.palette;
//            gobject = input.gobject;

//            base.copyFrom(input);
//        }


//        public override void update()
//        {
//            base.update();
//            recalculateAABB(); //Update AABB
//        }

//        // TODO MOVE THAT TO THE CORRESPONDING SYSTEM
////        public override void updateMeshInfo(bool lod_filter = false)
////        {

////#if(DEBUG)
////            if (instanceId < 0)
////                Console.WriteLine("test");
////            if (meshVao.BoneRemapIndicesCount > 128)
////                Console.WriteLine("test");
////#endif

////            if (!active || !renderable || (parentScene.activeLOD != LodLevel) && RenderState.settings.renderSettings.LODFiltering)
////            {
////                base.updateMeshInfo(true);
////                RenderStats.occludedNum += 1;
////                return;
////            }

////            Matrix4 worldMat = TransformationSystem.GetEntityWorldMat(this);
////            bool fr_status = Common.RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
////            bool occluded_status = !fr_status && Common.RenderState.settings.renderSettings.UseFrustumCulling;

////            //Recalculations && Data uploads
////            if (!occluded_status)
////            {
                
////                ////Apply LOD filtering
////                //if (hasLOD && Common.RenderOptions.LODFiltering)
////                ////if (false)
////                //{
////                //    //Console.WriteLine("Active LoD {0}", parentScene.activeLOD);
////                //    if (parentScene.activeLOD != LodLevel)
////                //    {
////                //        meshVao.setInstanceOccludedStatus(instanceId, true);
////                //        base.updateMeshInfo();
////                //        return;
////                //    }
////                //}
                

////                instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);

////                //Upload commonperMeshUniforms
////                GLMeshBufferManager.SetInstanceUniform4(meshVao, instanceId,
////                    "gUserDataVec4", CommonPerMeshUniforms["gUserDataVec4"].Vec.Vec);

////                if (Skinned)
////                {
////                    //Update the mesh remap matrices and continue with the transform updates
////                    meshVao.setSkinMatrices(parentScene, instanceId);
////                    //Fallback
////                    //main_Vao.setDefaultSkinMatrices();
////                }
////            }
////            else
////            {
////                Common.RenderStats.occludedNum += 1;
////            }

////            //meshVao.setInstanceOccludedStatus(instanceId, occluded_status);
////            base.updateMeshInfo();
////        }
        

        
        


//        #region IDisposable Support

//        protected override void Dispose(bool disposing)
//        {
//            if (!disposed)
//            {
//                if (disposing)
//                {

//                    // TODO: dispose managed state (managed objects).
//                    //if (material != null) material.Dispose();
//                    //NOTE: No need to dispose material, because the materials reside in the resource manager
//                    base.Dispose(disposing);
//                }
//            }
//        }

//        #endregion

//    }

}
