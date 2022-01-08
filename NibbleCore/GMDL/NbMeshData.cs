using System;

namespace NbCore
{
    public struct NbMeshData: IDisposable
    {
        public ulong Hash;
        public uint VertexBufferStride;
        public byte[] VertexBuffer;
        public byte[] IndexBuffer;
        public bufInfo[] buffers;
        public NbPrimitiveDataType IndicesLength;

        public void Dispose()
        {
            VertexBuffer = null;
            IndexBuffer = null;
        }

        public static NbMeshData Create()
        {
            NbMeshData md = new()
            {
                Hash = 0,
                VertexBuffer = null,
                IndexBuffer = null,
                IndicesLength = NbPrimitiveDataType.UnsignedInt
            };
            return md;
        }
        
    }
}