using System;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Utils;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace MVCore
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
        public static void ClearInstances(GLInstancedMeshVao mesh)
        {
            mesh.instanceRefs.Clear();
            mesh.instance_count = 0;
        }

        public static void RemoveInstance(GLInstancedMeshVao mesh, Model m)
        {
            int id = mesh.instanceRefs.IndexOf(m);
            //TODO: Make all the memory shit to push the instances backwards
        }

        //Setters

        public static void SetPropertyVal(float[] buffer, int offset, bool val)
        {
            unsafe
            {
                buffer[offset] = val ? 1.0f : 0.0f;
            }
        }

        public static void SetPropertyVal(float[] buffer, int offset, int val)
        {
            unsafe
            {
                buffer[offset] = val;
            }
        }

        public static void SetPropertyVal(float[] buffer, int offset, float val)
        {
            unsafe
            {
                buffer[offset] = val;
            }
        }

        public static void SetPropertyVal(float[] buffer, int offset, Vector3 val)
        {
            unsafe
            {
                buffer[offset + 0] = val.X;
                buffer[offset + 1] = val.Y;
                buffer[offset + 2] = val.Z;
            }
        }

        public static void SetPropertyVal(float[] buffer, int offset, Vector4 val)
        {
            unsafe
            {
                buffer[offset + 0] = val.X;
                buffer[offset + 1] = val.Y;
                buffer[offset + 2] = val.Z;
                buffer[offset + 3] = val.W;
            }
        }

        public static void SetPropertyVal(float[] buffer, int offset, Matrix4 val)
        {
            SetPropertyVal(buffer, offset, val.Row0);
            SetPropertyVal(buffer, offset + 4, val.Row1);
            SetPropertyVal(buffer, offset + 8, val.Row2);
            SetPropertyVal(buffer, offset + 12, val.Row3);
        }

        //Getters

        public static object GetPropertyVal(BufferPropertyType prop, float[] buffer, int offset)
        {
            return prop switch
            {
                BufferPropertyType.BOOL => GetPropertyBool(buffer, offset),
                BufferPropertyType.FLOAT => GetPropertyFloat(buffer, offset),
                BufferPropertyType.INT => GetPropertyInt(buffer, offset),
                BufferPropertyType.VEC3 => GetPropertyVec3(buffer, offset),
                BufferPropertyType.VEC4 => GetPropertyVec4(buffer, offset),
                BufferPropertyType.MAT4 => GetPropertyMat4(buffer, offset),
                BufferPropertyType.VEC2 => throw new Exception("Not Implemented"),
                BufferPropertyType.MAT3 => throw new Exception("Not Implemented"),
                _ => throw new Exception("Unimplemented property type"),
            };
        }

        private static bool GetPropertyBool(float[] buffer, int offset)
        {
            bool v;
            unsafe
            {
                v = buffer[offset] > 0.0f;
            }
            return v;
        }


        private static float GetPropertyFloat(float[] buffer, int offset)
        {
            float v;
            unsafe
            {
                v = buffer[offset];
            }
            return v;
        }

        private static int GetPropertyInt(float[] buffer, int offset)
        {
            int v;
            unsafe
            {
                v = (int) buffer[offset];
            }
            return v;
        }

        
        private static Vector4 GetPropertyVec4(float[] buffer, int offset)
        {
            float x, y, z, w;
            unsafe
            {
                x = buffer[offset + 0];
                y = buffer[offset + 1];
                z = buffer[offset + 2];
                w = buffer[offset + 3];
            }
            return new(x, y, z, w);
        }


        private static Vector3 GetPropertyVec3(float[] buffer, int offset)
        {
            float x, y, z;
            unsafe
            {
                x = buffer[offset + 0];
                y = buffer[offset + 1];
                z = buffer[offset + 2];
            }
            return new(x, y, z);
        }

        private static Matrix4 GetPropertyMat4(float[] buffer, int offset)
        {
            Vector4 r1 = GetPropertyVec4(buffer, offset + 0);
            Vector4 r2 = GetPropertyVec4(buffer, offset + 4);
            Vector4 r3 = GetPropertyVec4(buffer, offset + 8);
            Vector4 r4 = GetPropertyVec4(buffer, offset + 12);

            return new(r1, r2, r3, r4);
        }

    }
}
