using System;
using NbCore.Common;
using NbCore.Math;
using NbCore.Utils;
using NbCore.Systems;

namespace NbCore
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

        public static int AddMeshInstance(ref NbMesh mesh, MeshComponent mc)
        {
            int instance_id = mesh.InstanceCount;

            if (instance_id < NbMesh.MAX_INSTANCES)
            {
                mesh.instanceRefs.Add(mc); //Keep reference
                mesh.InstanceCount++;
            }
            else return -1;
            
            //Expand mesh data buffer if required
            if ((instance_id+1) * instance_struct_size_floats > mesh.InstanceDataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.InstanceDataBuffer.Length + instance_struct_size_floats * 5]; //Extend by 5 instances
                Array.Copy(mesh.InstanceDataBuffer, newBuffer, mesh.InstanceDataBuffer.Length);
                mesh.InstanceDataBuffer = newBuffer;
            }

            return instance_id;
        }
        
        public static void AddRenderInstance(ref MeshComponent mc, TransformData td)
        {
            NbMesh mesh = mc.Mesh;
            
            if ((mc.RenderInstanceID < mesh.RenderedInstanceCount - 1) && mc.RenderInstanceID >= 0)
            {
                Callbacks.Assert(false, "This should not happen");
            } else if (mc.RenderInstanceID > mesh.RenderedInstanceCount)
            {
                MeshComponent lastmc = mesh.instanceRefs[mesh.RenderedInstanceCount];
                int old_pos = mesh.RenderedInstanceCount;
                int new_pos = mc.RenderInstanceID;
                
                //Move the last data to the position of the requested instance
                mesh.instanceRefs[new_pos] = lastmc;
                mesh.instanceRefs[old_pos] = mc;
                
                //Copy buffer data
                int old_instance_offset = old_pos * instance_struct_size_floats;
                int new_instance_offset = new_pos * instance_struct_size_floats;
                Array.Copy(mesh.InstanceDataBuffer, old_instance_offset, 
                    mesh.InstanceDataBuffer, new_instance_offset,
                    instance_struct_size_floats);
                
                //Set RenderrInstanceIDs
                lastmc.RenderInstanceID = new_pos;

            }
            
            mc.RenderInstanceID = mesh.RenderedInstanceCount;

            //Uplod worldMat to the meshVao
            NbMatrix4 actualWorldMat = td.WorldTransformMat;
            NbMatrix4 actualWorldMatInv = (actualWorldMat).Inverted();
            SetInstanceWorldMat(mesh, mc.RenderInstanceID, actualWorldMat);
            SetInstanceWorldMatInv(mesh, mc.RenderInstanceID, actualWorldMatInv);
            SetInstanceNormalMat(mesh, mc.RenderInstanceID, NbMatrix4.Transpose(actualWorldMatInv));

            mesh.RenderedInstanceCount++;
        }
        
        public static int AddRenderInstance(ref NbMesh mesh, NbMatrix4 worldMat, NbMatrix4 worldMatInv, NbMatrix4 normMat)
        {
        
            int render_instance_id = mesh.RenderedInstanceCount;

            //Expand mesh data buffer if required
            if (render_instance_id * instance_struct_size_bytes > mesh.InstanceDataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.InstanceDataBuffer.Length + instance_struct_size_floats * 5]; //Extend by 5 instances
                Array.Copy(mesh.InstanceDataBuffer, newBuffer, mesh.InstanceDataBuffer.Length);
                mesh.InstanceDataBuffer = newBuffer;
            }
            
            //Uplod worldMat to the meshVao
            SetInstanceWorldMat(mesh, render_instance_id, worldMat);
            SetInstanceWorldMatInv(mesh, render_instance_id, worldMatInv);
            SetInstanceNormalMat(mesh, render_instance_id, normMat);

            mesh.RenderedInstanceCount++;
            
            return render_instance_id;
        }

        public static void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc)
        {
            Common.Callbacks.Assert(mc.RenderInstanceID >= 0, "Negative instance ID. ILLEGAL instance removal");

            if (mc.RenderInstanceID == mesh.RenderedInstanceCount - 1)
            {
                mesh.RenderedInstanceCount--;
                return;
            }
            
            //Find last instance
            MeshComponent lastmc = mesh.instanceRefs[mesh.RenderedInstanceCount - 1];

            //Fetch last instance databuffer
            float[] tempbuffer = new float[instance_struct_size_floats];
            int instance_float_offset = lastmc.RenderInstanceID * instance_struct_size_floats;
            Array.Copy(mesh.InstanceDataBuffer, instance_float_offset, tempbuffer, 0, instance_struct_size_floats);

            //Swap instances in the instanceRefs List
            mesh.instanceRefs.RemoveAt(mc.RenderInstanceID);
            mesh.instanceRefs.Insert(mc.RenderInstanceID, lastmc);
            mesh.instanceRefs.RemoveAt(mesh.instanceRefs.Count - 1);
            mesh.instanceRefs.Add(mc);

            //Replace removed instance data with the data of the last instance
            instance_float_offset = mc.RenderInstanceID * instance_struct_size_floats;
            Array.Copy(tempbuffer, 0, mesh.InstanceDataBuffer, instance_float_offset, instance_struct_size_floats);

            //Swap RenderInstanceIds
            (lastmc.RenderInstanceID, mc.RenderInstanceID) = (mc.RenderInstanceID, lastmc.RenderInstanceID);

            
            mesh.RenderedInstanceCount--;
        }
        
        public static void RemoveMeshInstance(NbMesh mesh, MeshComponent mc)
        {
            Common.Callbacks.Assert(mc.InstanceID >= 0, "Negative instance ID. ILLEGAL instance removal");

            mesh.instanceRefs.RemoveAt(mc.InstanceID);
            
            foreach (MeshComponent mmc in mesh.instanceRefs)
            {
                if (mmc.InstanceID > mc.InstanceID)
                    mmc.InstanceID--;
            }
            
            mesh.InstanceCount--;
        }

        //Overload with transform overrides
        public static void ClearMeshInstances(NbMesh mesh)
        {
            mesh.instanceRefs.Clear();
            mesh.InstanceCount = 0;
        }

        public static void SetInstanceLODLevel(NbMesh mesh, int instance_id, int level)
        {
            unsafe
            {
                mesh.InstanceDataBuffer[instance_id * instance_struct_size_floats + instance_LOD_Float_Offset] = (float)level;
            }
        }

        public static int GetInstanceLODLevel(NbMesh mesh, int instance_id)
        {
            unsafe
            {
                return (int) mesh.InstanceDataBuffer[instance_id * instance_struct_size_floats + instance_LOD_Float_Offset];
            }
        }

        public static void SetInstanceSelectedStatus(NbMesh mesh, int instance_id, bool status)
        {
            unsafe
            {
                mesh.InstanceDataBuffer[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] = status ? 1.0f : 0.0f;
            }
        }

        public static bool GetInstanceSelectedStatus(NbMesh mesh, int instance_id)
        {
            unsafe
            {
                return mesh.InstanceDataBuffer[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] > 0.0f;
            }
        }

        public static NbMatrix4 GetInstanceWorldMat(NbMesh mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset;
                    return MathUtils.Matrix4FromArray(ar, offset);
                }
            }

        }

        public static NbMatrix4 GetInstanceNormalMat(NbMesh mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    return MathUtils.Matrix4FromArray(ar, instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset);
                }
            }
        }

        public static NbVector3 GetInstanceColor(NbMesh mesh, int instance_id)
        {
            float col;
            unsafe
            {
                col = mesh.InstanceDataBuffer[instance_id * instance_struct_size_floats + instance_color_Float_Offset];
            }

            return new NbVector3(col, col, col);
        }

        public static void SetInstanceUniform4(NbMesh mesh, int instance_id, string un_name, NbVector4 un)
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

                mesh.InstanceDataBuffer[offset] = un.X;
                mesh.InstanceDataBuffer[offset + 1] = un.Y;
                mesh.InstanceDataBuffer[offset + 2] = un.Z;
                mesh.InstanceDataBuffer[offset + 3] = un.W;
            }
        }

        public static NbVector4 GetInstanceUniform(NbMesh mesh, int instance_id, string un_name)
        {
            NbVector4 un = new();
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

                un.X = mesh.InstanceDataBuffer[offset];
                un.Y = mesh.InstanceDataBuffer[offset + 1];
                un.Z = mesh.InstanceDataBuffer[offset + 2];
                un.W = mesh.InstanceDataBuffer[offset + 3];
            }

            return un;
        }

        public static void SetInstanceWorldMat(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public static void SetInstanceWorldMatInv(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMatInv_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public static void SetInstanceNormalMat(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }


    }
}
