using System;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Utils;
using MVCore.Systems;

namespace MVCore
{
    public static class GLMeshBufferManager
    {
        public const int color_Float_Offset = 0;
        public const int color_Byte_Offset = 0;

        public const int skinned_Float_Offset = 3;
        public const int skinned_Byte_Offset = 12;

        public const int instanceData_Float_Offset = 4;
        public const int instanceData_Byte_Offset = 16;

        //Relative Instance Offsets

        //public static int instance_Uniforms_Offset = 0;
        public const int instance_Uniforms_Float_Offset = 0;
        //public static int instance_worldMat_Offset = 64;
        public const int instance_worldMat_Float_Offset = 16;
        //public static int instance_normalMat_Offset = 128;
        public const int instance_normalMat_Float_Offset = 32;
        //public static int instance_worldMatInv_Offset = 192;
        public const int instance_worldMatInv_Float_Offset = 48;
        //public static int instance_isOccluded_Offset = 256;
        public const int instance_isOccluded_Float_Offset = 64;
        //public static int instance_isSelected_Offset = 260;
        public const int instance_isSelected_Float_Offset = 65;
        //public static int instance_color_Offset = 264; //TODO make that a vec4
        public const int instance_color_Float_Offset = 66;
        //public static int instance_LOD_Offset = 268; //TODO make that a vec4
        public const int instance_LOD_Float_Offset = 67;

        public const int instance_struct_size_bytes = 272;
        public const int instance_struct_size_floats = 68;

        //Instance Data Format:
        //0-16 : instance WorldMatrix
        //16-17: isOccluded
        //17-18: isSelected
        //18-20: padding


        public static int AddInstance(ref GLInstancedMesh mesh, SceneGraphNode m)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }

            if (instance_id < GLInstancedMesh.MAX_INSTANCES)
            {
                //Uplod worldMat to the meshVao

                Matrix4 actualWorldMat = TransformationSystem.GetEntityWorldMat(m);
                Matrix4 actualWorldMatInv = (actualWorldMat).Inverted();
                SetInstanceWorldMat(mesh, instance_id, actualWorldMat);
                SetInstanceWorldMatInv(mesh, instance_id, actualWorldMatInv);
                SetInstanceNormalMat(mesh, instance_id, Matrix4.Transpose(actualWorldMatInv));

                mesh.instanceRefs.Add(m); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        //Overload with transform overrides
        public static int AddInstance(GLInstancedMesh mesh, SceneGraphNode m, Matrix4 worldMat, Matrix4 worldMatInv, Matrix4 normMat)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }

            if (instance_id < GLInstancedMesh.MAX_INSTANCES)
            {
                SetInstanceWorldMat(mesh, instance_id, worldMat);
                SetInstanceWorldMatInv(mesh, instance_id, worldMatInv);
                SetInstanceNormalMat(mesh, instance_id, normMat);

                mesh.instanceRefs.Add(m); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        public static void ClearInstances(GLInstancedMesh mesh)
        {
            mesh.instanceRefs.Clear();
            mesh.instance_count = 0;
        }

        public static void RemoveInstance(GLInstancedMesh mesh, SceneGraphNode m)
        {
            int id = mesh.instanceRefs.IndexOf(m);
            //TODO: Make all the memory shit to push the instances backwards
        }


        public static void SetInstanceOccludedStatus(GLInstancedMesh mesh, int instance_id, bool status)
        {
            mesh.visible_instances += (status ? -1 : 1);
            unsafe
            {
                mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isOccluded_Float_Offset] = status ? 1.0f : 0.0f;
            }
        }

        public static bool GetInstanceOccludedStatus(GLInstancedMesh mesh, int instance_id)
        {
            unsafe
            {
                return mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isOccluded_Float_Offset] > 0.0f;
            }
        }

        public static void SetInstanceLODLevel(GLInstancedMesh mesh, int instance_id, int level)
        {
            unsafe
            {
                mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_LOD_Float_Offset] = (float)level;
            }
        }

        public static int GetInstanceLODLevel(GLInstancedMesh mesh, int instance_id)
        {
            unsafe
            {
                return (int) mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_LOD_Float_Offset];
            }
        }

        public static void SetInstanceSelectedStatus(GLInstancedMesh mesh, int instance_id, bool status)
        {
            unsafe
            {
                mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] = status ? 1.0f : 0.0f;
            }
        }

        public static bool GetInstanceSelectedStatus(GLInstancedMesh mesh, int instance_id)
        {
            unsafe
            {
                return mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] > 0.0f;
            }
        }

        public static Matrix4 GetInstanceWorldMat(GLInstancedMesh mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset;
                    return MathUtils.Matrix4FromArray(ar, offset);
                }
            }

        }

        public static Matrix4 GetInstanceNormalMat(GLInstancedMesh mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    return MathUtils.Matrix4FromArray(ar, instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset);
                }
            }
        }

        public static Vector3 GetInstanceColor(GLInstancedMesh mesh, int instance_id)
        {
            float col;
            unsafe
            {
                col = mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_color_Float_Offset];
            }

            return new Vector3(col, col, col);
        }

        public static void SetInstanceUniform4(GLInstancedMesh mesh, int instance_id, string un_name, Vector4 un)
        {
            unsafe
            {
                int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_Uniforms_Float_Offset;
                int uniform_id = 0;
                switch (un_name)
                {
                    case "gUserDataVec4":
                        uniform_id = 0;
                        break;
                }

                offset += uniform_id * 4;

                mesh.dataBuffer[offset] = un.X;
                mesh.dataBuffer[offset + 1] = un.Y;
                mesh.dataBuffer[offset + 2] = un.Z;
                mesh.dataBuffer[offset + 3] = un.W;
            }
        }

        public static Vector4 GetInstanceUniform(GLInstancedMesh mesh, int instance_id, string un_name)
        {
            Vector4 un;
            unsafe
            {
                int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_Uniforms_Float_Offset;
                int uniform_id = 0;
                switch (un_name)
                {
                    case "gUserDataVec4":
                        uniform_id = 0;
                        break;
                }

                offset += uniform_id * 4;

                un.X = mesh.dataBuffer[offset];
                un.Y = mesh.dataBuffer[offset + 1];
                un.Z = mesh.dataBuffer[offset + 2];
                un.W = mesh.dataBuffer[offset + 3];
            }

            return un;
        }

        public static void SetInstanceWorldMat(GLInstancedMesh mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public static void SetInstanceWorldMatInv(GLInstancedMesh mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMatInv_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public static void SetInstanceNormalMat(GLInstancedMesh mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }


    }
}
