using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{   
    public abstract class Component : IDisposable
    {
        public abstract Component Clone();
        public abstract void CopyFrom(Component c);

        #region IDisposable Support

        //Disposable Stuff
        public bool disposed = false;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new(IntPtr.Zero, true);

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
            }

            //Free unmanaged resources
            disposed = true;
        }

#if DEBUG
        ~Component()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + GetType().ToString());
        }
#endif
        #endregion
    };
}
