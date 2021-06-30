using System;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Utils;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace MVCore.GMDL
{
    
    public enum BufferPropertyType
    {
        BOOL,
        INT,
        FLOAT,
        VEC2,
        VEC3,
        VEC4,
        MAT3,
        MAT4,
    }


    public class GLBufferManager
    {
        
        //Overload with transform overrides
        public void clearInstances(GLInstancedMeshVao mesh)
        {
            mesh.instanceRefs.Clear();
            mesh.instance_count = 0;
        }

        public void removeInstance(GLInstancedMeshVao mesh, Model m)
        {
            int id = mesh.instanceRefs.IndexOf(m);
            //TODO: Make all the memory shit to push the instances backwards
        }

        //Setters

        public void setPropertyVal(float[] buffer, int offset, bool val)
        {
            unsafe
            {
                buffer[offset] = val ? 1.0f : 0.0f;
            }
        }

        public void setPropertyVal(float[] buffer, int offset, int val)
        {
            unsafe
            {
                buffer[offset] = val;
            }
        }

        public void setPropertyVal(float[] buffer, int offset, float val)
        {
            unsafe
            {
                buffer[offset] = val;
            }
        }

        public void setPropertyVal(float[] buffer, int offset, Vector3 val)
        {
            unsafe
            {
                buffer[offset + 0] = val.X;
                buffer[offset + 1] = val.Y;
                buffer[offset + 2] = val.Z;
            }
        }

        public void setPropertyVal(float[] buffer, int offset, Vector4 val)
        {
            unsafe
            {
                buffer[offset + 0] = val.X;
                buffer[offset + 1] = val.Y;
                buffer[offset + 2] = val.Z;
                buffer[offset + 3] = val.W;
            }
        }

        public void setPropertyVal(float[] buffer, int offset, Matrix4 val)
        {
            setPropertyVal(buffer, offset, val.Row0);
            setPropertyVal(buffer, offset + 4, val.Row1);
            setPropertyVal(buffer, offset + 8, val.Row2);
            setPropertyVal(buffer, offset + 12, val.Row3);
        }

        //Getters

        public object getPropertyVal(BufferPropertyType prop, float[] buffer, int offset)
        {
            switch (prop)
            {
                case BufferPropertyType.BOOL:
                    return getPropertyBool(buffer, offset);
                case BufferPropertyType.FLOAT:
                    return getPropertyFloat(buffer, offset);
                case BufferPropertyType.INT:
                    return getPropertyInt(buffer, offset);
                case BufferPropertyType.VEC3:
                    return getPropertyVec3(buffer, offset);
                case BufferPropertyType.VEC4:
                    return getPropertyVec4(buffer, offset);
                case BufferPropertyType.MAT4:
                    return getPropertyMat4(buffer, offset);
                default:
                    throw new Exception("Unimplemented property type");
            }
        }

        private bool getPropertyBool(float[] buffer, int offset)
        {
            bool v;
            unsafe
            {
                v = buffer[offset] > 0.0f ? true : false;
            }
            return v;
        }


        private float getPropertyFloat(float[] buffer, int offset)
        {
            float v;
            unsafe
            {
                v = buffer[offset];
            }
            return v;
        }

        private int getPropertyInt(float[] buffer, int offset)
        {
            int v;
            unsafe
            {
                v = (int) buffer[offset];
            }
            return v;
        }

        
        private Vector4 getPropertyVec4(float[] buffer, int offset)
        {
            float x, y, z, w;
            unsafe
            {
                x = buffer[offset + 0];
                y = buffer[offset + 1];
                z = buffer[offset + 2];
                w = buffer[offset + 3];
            }
            return new Vector4(x, y, z, w);
        }


        private Vector3 getPropertyVec3(float[] buffer, int offset)
        {
            float x, y, z;
            unsafe
            {
                x = buffer[offset + 0];
                y = buffer[offset + 1];
                z = buffer[offset + 2];
            }
            return new Vector3(x, y, z);
        }

        private Matrix4 getPropertyMat4(float[] buffer, int offset)
        {
            Vector4 r1 = getPropertyVec4(buffer, offset + 0);
            Vector4 r2 = getPropertyVec4(buffer, offset + 4);
            Vector4 r3 = getPropertyVec4(buffer, offset + 8);
            Vector4 r4 = getPropertyVec4(buffer, offset + 12);

            return new Matrix4(r1, r2, r3, r4);
        }

    }
}
