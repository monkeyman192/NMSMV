using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
   public enum THREAD_REQUEST_TYPE
    {
        GL_RESIZE_REQUEST,
        GL_COMPILE_SHADER_REQUEST,
        GL_COMPILE_ALL_SHADERS_REQUEST,
        GL_MODIFY_SHADER_REQUEST,
        GL_RESUME_RENDER_REQUEST,
        GL_PAUSE_RENDER_REQUEST,
        QUERY_GLCONTROL_STATUS_REQUEST,
        MOUSEPOSITION_INFO_REQUEST,
        NEW_SCENE_REQUEST,
        NEW_TEST_SCENE_REQUEST,
        UPDATE_SCENE_REQUEST,
        CHANGE_MODEL_PARENT_REQUEST,
        TERMINATE_REQUEST,  
        LOAD_NMS_ARCHIVES_REQUEST,
        OPEN_FILE_REQUEST,
        GIZMO_PICKING_REQUEST,
        INIT_RESOURCE_MANAGER,
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
        public List<object> arguments;
        public THREAD_REQUEST_TYPE type;
        public THREAD_REQUEST_STATUS status;
        public THREAD_REQUEST_OWNER owner;
        public int response;
        private bool disposedValue;

        public ThreadRequest()
        {
            type = THREAD_REQUEST_TYPE.NULL;
            owner = THREAD_REQUEST_OWNER.NULL;
            status = THREAD_REQUEST_STATUS.ACTIVE;
            arguments = new List<object>();
            response = 0;
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
