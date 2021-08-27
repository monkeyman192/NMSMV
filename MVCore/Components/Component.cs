﻿using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{   
    public abstract class Component : IDisposable
    {
        public abstract Component Clone();
        public abstract void CopyFrom(Component c);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    };
}
