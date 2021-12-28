using System;
using System.Collections.Generic;
using System.Text;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Graphics.OpenGL4;
using NbCore.Utils;


namespace NbCore
{
    public class GLInstancedMesh
    {
        //Class static properties
        public string Name;
        public NbMesh Mesh;
        public GLVao vao;
        public GLVao bHullVao;
        
        //Instance Data
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Offset 

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int instanceBoneMatricesTex;
        public int instanceBoneMatricesTexTBO;

        public static Dictionary<NbPrimitiveDataType, DrawElementsType> IndicesLengthMap = new()
        {
            { NbPrimitiveDataType.UnsignedByte, DrawElementsType.UnsignedByte },
            { NbPrimitiveDataType.UnsignedInt, DrawElementsType.UnsignedInt },
            { NbPrimitiveDataType.UnsignedShort, DrawElementsType.UnsignedShort }
        };

        //GLSpecific Properties
        public DrawElementsType IndicesLength { 
            get
            {
                return IndicesLengthMap[Mesh.MetaData.IndicesLength];
            }
        }

        public GLInstancedMesh()
        {
            vao = new GLVao();
        }

        public GLInstancedMesh(NbMesh mesh)
        {
            vao =  GraphicsAPI.generateVAO(mesh);
            Mesh = mesh;
        }

        public void SetupTBO()
        {
            //Setup the TBO
            instanceBoneMatricesTex = GL.GenTexture();
            instanceBoneMatricesTexTBO = GL.GenBuffer();

            int bufferSize = Mesh.InstanceCount * 128 * 16 * 4;
            
            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            GL.BufferData(BufferTarget.TextureBuffer, bufferSize, Mesh.instanceBoneMatrices, BufferUsageHint.StreamDraw);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }

        

        //TODO: Generate a Data object back to the geometry object with the bound hull vertices
        //and then use the normal generateVao method
        //
        //public GLVao getCollisionMeshVao(MeshMetaData metaData)
        //{
        //    //Collision Mesh isn't used anywhere else.
        //    //No need to check for hashes and shit

        //    float[] vx_buffer_float = new float[(metaData.BoundHullEnd - metaData.BoundHullStart) * 3];

        //    for (int i = 0; i < metaData.BoundHullEnd - metaData.BoundHullStart; i++)
        //    {
        //        NbVector3 v = bhullverts[i + metaData.BoundHullStart];
        //        vx_buffer_float[3 * i + 0] = v.X;
        //        vx_buffer_float[3 * i + 1] = v.Y;
        //        vx_buffer_float[3 * i + 2] = v.Z;
        //    }

        //    //Generate intermediate geom
        //    GeomObject temp_geom = new();

        //    //Set main Geometry Info
        //    temp_geom.vertCount = vx_buffer_float.Length / 3;
        //    temp_geom.indicesCount = metaData.BatchCount;
        //    temp_geom.indicesType = indicesType;

        //    //Set Strides
        //    temp_geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

        //    //Set Buffer Offsets
        //    temp_geom.mesh_descr = "vn";
        //    bufInfo buf = new bufInfo()
        //    {
        //        count = 3,
        //        normalize = false,
        //        offset = 0,
        //        sem_text = "vPosition",
        //        semantic = 0,
        //        stride = 0,
        //        type = NbPrimitiveDataType.Float
        //    };
        //    temp_geom.bufInfo.Add(buf);

        //    buf = new bufInfo()
        //    {
        //        count = 3,
        //        normalize = false,
        //        offset = 0,
        //        sem_text = "nPosition",
        //        semantic = 2,
        //        stride = 0,
        //        type = NbPrimitiveDataType.Float
        //    };
        //    temp_geom.bufInfo.Add(buf);

        //    //Set Buffers
        //    temp_geom.ibuffer = new byte[temp_geom.indicesLength * metaData.BatchCount];
        //    temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

        //    System.Buffer.BlockCopy(ibuffer, metaData.BatchStartPhysics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
        //    System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


        //    return temp_geom.generateVAO();
        //}

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
