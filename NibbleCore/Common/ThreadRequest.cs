using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NbCore
{
   public enum THREAD_REQUEST_TYPE
    {
        ENGINE_RESIZE_VIEWPORT,
        ENGINE_COMPILE_SHADER,
        ENGINE_COMPILE_ALL_SHADERS,
        ENGINE_MODIFY_SHADER,
        ENGINE_RESUME_RENDER,
        ENGINE_PAUSE_RENDER,
        ENGINE_QUERY_GLCONTROL_STATUS,
        ENGINE_MOUSEPOSITION_INFO,
        ENGINE_OPEN_NEW_SCENE,
        ENGINE_OPEN_TEST_SCENE,
        ENGINE_UPDATE_SCENE,
        ENGINE_CHANGE_MODEL_PARENT,
        ENGINE_TERMINATE_RENDER,
        ENGINE_GIZMO_PICKING,
        ENGINE_INIT_RESOURCE_MANAGER,
        //TODO maybe I should split engine events from window events
        WINDOW_OPEN_FILE,
        WINDOW_LOAD_NMS_ARCHIVES,
        WINDOW_CLOSE,
        NULL
    };

    public enum THREAD_REQUEST_STATUS
    {
        ACTIVE,
        FINISHED,
        NULL
    };

    public enum THREAD_REQUEST_OWNER
    {
        SENDER,
        RECEIVER,
        NULL
    };

    public class ThreadRequest :IDisposable
    {
        public object Data;
        public MethodInfo Method;
        public THREAD_REQUEST_TYPE Type;
        public THREAD_REQUEST_STATUS Status;
        public THREAD_REQUEST_OWNER Owner;
        public int ResponseCode;
        public object ResponseData;
        private bool disposedValue;
        
        public ThreadRequest()
        {
            Type = THREAD_REQUEST_TYPE.NULL;
            Owner = THREAD_REQUEST_OWNER.NULL;
            Status = THREAD_REQUEST_STATUS.NULL;
            Data = null;
            Method = null;
            ResponseCode = -1;
            ResponseData = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ThreadRequest()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
