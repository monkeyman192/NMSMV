using System;

namespace NbCore
{
    public struct NbMeshData: IDisposable
    {
        public ulong Hash;
        public int VertexBufferStride;
        public byte[] VertexBuffer;
        public byte[] IndexBuffer;
        public bufInfo[] buffers;
        
        public void Dispose()
        {
            VertexBuffer = null;
            IndexBuffer = null;
        }

        public static NbMeshData GetEmpty()
        {
            NbMeshData md = new()
            {
                Hash = 0,
                VertexBuffer = null,
                IndexBuffer = null
            };
            return md;
        }
        
    }
}