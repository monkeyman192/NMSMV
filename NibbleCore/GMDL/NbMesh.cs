using System.Collections.Generic;

namespace NbCore
{
    
    public class NbMesh : Entity
    {
        public long Hash;
        public MeshMetaData MetaData;
        public MeshData Data;
        public float[] InstanceDataBuffer; //Instance Data
        public int InstanceCount = 0;
        public int RenderedInstanceCount = 0;
        public List<MeshComponent> instanceRefs = new();
        public float[] instanceBoneMatrices;
        public int instanceBoneMatricesTex;
        public int instanceBoneMatricesTexTBO;

        public const int MAX_INSTANCES = 512;

        private bool _disposed = false;
        
        public NbMesh() : base(EntityType.Mesh)
        {
            
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