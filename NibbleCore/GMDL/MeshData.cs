using System;

namespace NbCore
{
    public class MeshData: IDisposable
    {
        public byte[] VertexBuffer;
        public byte[] IndexBuffer;

        public void Dispose()
        {
            VertexBuffer = null;
            IndexBuffer = null;
        }
        
    }
}