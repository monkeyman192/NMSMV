using System;
using NbCore.Common;
using NbCore.Math;
using NbCore.Utils;
using NbCore.Systems;

namespace NbCore
{

    /**
     * Instance Buffer Documentation
     * Instancing using the GLMeshBufferManager works as follows.
     *      
     *      Every time an instance of a mesh has to be created, the AddMeshInstance method is called.
     *      This method is responsible for mainly incremented the mesh's global instance counter
     *      but also to allocate enough space in the underlying instance buffer to store the instance's
     *      properties.
     *      
     *      Note: I think that this process can be skipped. Managing RenderInstances is more than enough
     *      to differentiate instances, keep track of the active instances and also manage the instance buffer
     * 
     *      Every time the rendering status of an instance is modified, the AddRenderInstance/RemoveRenderInstance
     *      methods are called.
     *      
     *      The AddRenderInstance method, stores the instance data of the requested instance at the end of the instance buffer.
     *      It also sets the new render instance id to the requested meshcomponent 
     *      
     *      Schematic Representation of the instance insertion
     *      | 0 | 1 | 2 | 3 | 4 | * | <----
     *      
     *      
     *      The RemoveRenderInstance method, is responsible for removing the requested instance from the buffer, using its stored
     *      renderInstanceID, which reveals its position in the buffer. In order to prevent the update of all the instance refs
     *      of all intermediate instances, the method swaps the instance data with just the last instance of the buffer and
     *      decreases the renderInstanceCounter.
     *      
     *      Schematic Representation of the instance removal (removing Instance 2)
     
     *      Start:     
     *      | 0 | 1 | 2 | 3 | 4 | x | x |
     *      Swap 2 with 4 that is the last member:     
     *      | 0 | 1 | 4 | 3 | 2 | x | x |
     *      Data for 2 is still in the buffer, but the counter has been decreased so it won't be used.     
     *      | 0 | 1 | 4 | 3 | 2 | x | x |
     *
     * 
     * 
     */


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

        public static int GetNextMeshInstanceID(ref NbMesh mesh)
        {
            int render_instance_id = mesh.InstanceCount;

            //Expand mesh data buffer if required
            if ((render_instance_id + 1) * instance_struct_size_floats > mesh.InstanceDataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.InstanceDataBuffer.Length + instance_struct_size_floats * 5]; //Extend by 5 instances
                Array.Copy(mesh.InstanceDataBuffer, newBuffer, mesh.InstanceDataBuffer.Length);
                mesh.InstanceDataBuffer = newBuffer;
            }

            return render_instance_id;
        }

        public static void AddRenderInstance(ref MeshComponent mc, TransformData td)
        {
            NbMesh mesh = mc.Mesh;

            if (mc.InstanceID >= 0)
            {
                Callbacks.Assert(false, "Non negative renderInstanceID on a non visible mesh. This should not happen");
                return;
            }

            mc.InstanceID = GetNextMeshInstanceID(ref mesh);
            
            //Uplod worldMat to the meshVao
            NbMatrix4 actualWorldMat = td.WorldTransformMat;
            NbMatrix4 actualWorldMatInv = (actualWorldMat).Inverted();
            SetInstanceWorldMat(mesh, mc.InstanceID, actualWorldMat);
            SetInstanceWorldMatInv(mesh, mc.InstanceID, actualWorldMatInv);
            SetInstanceNormalMat(mesh, mc.InstanceID, NbMatrix4.Transpose(actualWorldMatInv));

            mesh.InstanceCount++;
        }
        
        public static int AddRenderInstance(ref NbMesh mesh, NbMatrix4 worldMat, NbMatrix4 worldMatInv, NbMatrix4 normMat)
        {
        
            int render_instance_id = mesh.InstanceCount;

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

            mesh.InstanceCount++;
            
            return render_instance_id;
        }

        public static void RemoveRenderInstance(ref NbMesh mesh, MeshComponent mc)
        {
            Callbacks.Assert(mc.InstanceID >= 0, "Negative instance ID. ILLEGAL instance removal");

            if (mc.InstanceID == mesh.InstanceCount - 1)
            {
                mesh.InstanceCount--;
                return;
            }
            
            //Find last instance
            MeshComponent lastmc = mesh.instanceRefs[mesh.InstanceCount - 1];

            //Fetch last instance databuffer
            float[] tempbuffer = new float[instance_struct_size_floats];
            int instance_float_offset = lastmc.InstanceID * instance_struct_size_floats;
            Array.Copy(mesh.InstanceDataBuffer, instance_float_offset, tempbuffer, 0, instance_struct_size_floats);

            //Swap instances in the instanceRefs List
            mesh.instanceRefs.RemoveAt(mc.InstanceID);
            mesh.instanceRefs.Insert(mc.InstanceID, lastmc);
            mesh.instanceRefs.RemoveAt(mesh.instanceRefs.Count - 1);
            mesh.instanceRefs.Add(mc);

            //Replace removed instance data with the data of the last instance
            instance_float_offset = mc.InstanceID * instance_struct_size_floats;
            Array.Copy(tempbuffer, 0, mesh.InstanceDataBuffer, instance_float_offset, instance_struct_size_floats);

            //Swap RenderInstanceIds
            (lastmc.InstanceID, mc.InstanceID) = (mc.InstanceID, lastmc.InstanceID);

            
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
