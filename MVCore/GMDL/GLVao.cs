using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class GLVao : IDisposable
    {
        //VAO ID
        public int vao_id;
        //VBO IDs
        public int vertex_buffer_object;
        public int element_buffer_object;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public GLVao()
        {
            vao_id = -1;
            vertex_buffer_object = -1;
            element_buffer_object = -1;
        }

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {


                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
