using System.Collections.Generic;
using System;
using NbCore.Math;
using System.Runtime.InteropServices;

namespace NbCore
{
    public enum NbMeshType
    {
        Mesh,
        Locator,
        Light,
        LightVolume,
        Joint,
        Collision
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MeshInstance
    {
        //4 x Vec4 Uniforms
        [FieldOffset(0)]
        public NbMatrix4 uniforms;
        //Matrices
        [FieldOffset(64)]
        public NbMatrix4 worldMat;
        [FieldOffset(128)]
        public NbMatrix4 normalMat;
        [FieldOffset(192)]
        public NbMatrix4 worldMatInv;
        [FieldOffset(256)]
        public NbVector3 color;
        [FieldOffset(268)]
        public float isSelected;
    };

    public class NbMesh : Entity
    {
        public ulong Hash;
        public new NbMeshType Type;
        public NbMeshMetaData MetaData; //Each mesh has its own object instance
        public NbMeshData Data; //Reference that might be shared with other NbMeshes
        //public float[] InstanceDataBuffer = new float[256]; //Instance Data
        public MeshInstance[] InstanceDataBuffer = new MeshInstance[2];
        public int InstanceCount = 0;
        
        //This is needed only for removing render instances, so that InstanceIDs for relocated meshes in the buffer are updated
        //I think I should find a way to get rid of this at some point. Till then I'll keep it
        public MeshComponent[] instanceRefs = new MeshComponent[10]; 
        
        public float[] instanceBoneMatrices;
        
        public const int MAX_INSTANCES = 512;

        private bool _disposed = false;
        
        public NbMesh() : base(EntityType.Mesh)
        {
            
        }

        //TODO: Move that function to the meshgroup
        //public void setSkinMatrices(SceneComponent sc, int instance_id)
        //{
        //    int instance_offset = 128 * 16 * instance_id;

        //    for (int i = 0; i < BoneRemapIndicesCount; i++)
        //    {
        //        Array.Copy(sc.skinMats, BoneRemapIndices[i] * 16, instanceBoneMatrices, instance_offset + i * 16, 16);
        //    }
        //}

        //TODO: Move that function to the meshgroup
        //public void setDefaultSkinMatrices(int instance_id)
        //{
        //    int instance_offset = 128 * 16 * instance_id;
        //    for (int i = 0; i < BoneRemapIndicesCount; i++)
        //    {
        //        MathUtils.insertMatToArray16(instanceBoneMatrices, instance_offset + i * 16, NbMatrix4.Identity());
        //    }
        //}

        public void initializeSkinMatrices(SceneComponent sc)
        {
            if (InstanceCount == 0 || sc == null)
                return;

            int jointCount = sc.jointDict.Values.Count;

            //TODO: Use the jointCount to adaptively setup the instanceBoneMatrices
            //Console.WriteLine("MAX : 128  vs Effective : " + jointCount.ToString());

            //Re-initialize the array based on the number of instances
            instanceBoneMatrices = new float[InstanceCount * 128 * 16];
            
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Data.Dispose();
                }
                
                _disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}