using System.Collections.Generic;
using System;

using NbCore.Math;

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
    
    public class NbMesh : Entity
    {
        public long Hash;
        public NbMeshType Type;
        public MeshMetaData MetaData;
        public MeshData Data;
        public float[] InstanceDataBuffer; //Instance Data
        public int InstanceCount = 0;
        public int RenderedInstanceCount = 0;
        public List<MeshComponent> instanceRefs = new();
        public float[] instanceBoneMatrices;
        public bool skinned = false;
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        
        
        public const int MAX_INSTANCES = 512;

        private bool _disposed = false;
        
        public NbMesh() : base(EntityType.Mesh)
        {
            
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
            
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Data?.Dispose();
                }
                
                _disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}