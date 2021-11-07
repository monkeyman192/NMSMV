﻿using System;
using OpenTK;
using NbCore.Math;
using NbCore.Systems;
using NbCore.Utils;
using System.Runtime.InteropServices;
using System.Collections.Generic;


namespace NbCore
{
    
    [StructLayout(LayoutKind.Explicit)]
    public struct GLLight
    {
        [FieldOffset(0)]
        public OpenTK.Mathematics.Vector3 position; //I don't like this at all. Check the consequences of using my class instead
        [FieldOffset(12)]
        public float isRenderable; 
        [FieldOffset(16)]
        public OpenTK.Mathematics.Vector3 color; 
        [FieldOffset(28)]
        public float intensity; 
        [FieldOffset(32)]
        public OpenTK.Mathematics.Vector3 direction; 
        [FieldOffset(44)]
        public float fov; 
        [FieldOffset(48)]
        public int falloff;
        [FieldOffset(52)]
        public float type;
        [FieldOffset(56)]
        public float radius;

        public static readonly int SizeInBytes = 64;
    }

    public class GLLightBufferManager : GLBufferManager
    {
        //Relative Instance Offsets
        public const int instance_worldMat_float_offset = 0;
        public const int instance_direction_float_Offset = 16;
        public const int instance_color_float_Offset = 20;
        //public const int instance_parameters_float_Offset = 24;
        public const int instance_intensity_float_Offset = 23;
        public const int instance_falloff_float_Offset = 24;
        public const int instance_fov_float_Offset = 25;
        public const int instance_type_float_Offset = 26;
        
        public const int instance_struct_size_bytes = 128;
        public const int instance_struct_size_floats = 32;

        //Instance Data Format:
        //TODO

        public int AddRenderInstance(ref NbMesh mesh, MeshComponent mc, NbMatrix4 worldMat)
        {
            int render_instance_id = mesh.RenderedInstanceCount;
            
            //Expand mesh data buffer if required
            if (render_instance_id * instance_struct_size_bytes > mesh.InstanceDataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.InstanceDataBuffer.Length + 256];
                Array.Copy(mesh.InstanceDataBuffer, newBuffer, mesh.InstanceDataBuffer.Length);
                mesh.InstanceDataBuffer = newBuffer;
            }

            //Uplod worldMat to the meshVao
            SetInstanceWorldMat(mesh, render_instance_id, worldMat);
                
            //TODO: Implement Light Component and use it to properly pass light properties
            //SetInstanceColor(ref mesh, instance_id, l.Color);
            //SetInstanceIntensity(ref mesh, instance_id, l.Intensity);
            //SetInstanceDirection(ref mesh, instance_id, l.Direction);
            //SetInstanceFOV(ref mesh, instance_id, (float)Math.Cos(MathUtils.radians(l.FOV)));
            //SetInstanceFallOff(ref mesh, instance_id, (int)l.Falloff);
            //SetInstanceType(ref mesh, instance_id, (l.LightType == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f);

            mesh.RenderedInstanceCount++;

            return render_instance_id;
        }

        public int AddMeshInstance(NbMesh mesh, MeshComponent mc, NbMatrix4 worldMat)
        {
            int instance_id = mesh.InstanceCount;

            if (instance_id < NbMesh.MAX_INSTANCES)
            {
                mesh.instanceRefs.Add(mc);
                mesh.InstanceCount++;
            }
            
            return instance_id;
        }

        //WorldMat
        private void SetInstanceWorldMat(NbMesh mesh, int instance_id, NbMatrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    int offset = instance_id * instance_struct_size_floats + instance_worldMat_float_offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public NbMatrix4 GetInstanceWorldMat(NbMesh mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.InstanceDataBuffer)
                {
                    int offset = instance_id * instance_struct_size_floats + instance_worldMat_float_offset;
                    return MathUtils.Matrix4FromArray(ar, offset);
                }
            }

        }


        //Color
        public static void SetInstanceColor(NbMesh mesh, int instance_id, NbVector3 color)
        {
            SetPropertyVal(mesh.InstanceDataBuffer,
                           instance_id * instance_struct_size_floats + instance_color_float_Offset,
                           color);
        }

        public static NbVector3 GetInstanceColor(NbMesh mesh, int instance_id)
        {
            return (NbVector3) GetPropertyVal(BufferPropertyType.VEC4,
                                            mesh.InstanceDataBuffer,
                                            instance_id * instance_struct_size_floats + instance_color_float_Offset);
        }

        //Direction
        public static void SetInstanceDirection(NbMesh mesh, int instance_id, NbVector3 dir)
        {

            SetPropertyVal(mesh.InstanceDataBuffer,
                           instance_id * instance_struct_size_floats + instance_direction_float_Offset,
                           dir);
        }

        public static NbVector3 GetInstanceDirection(NbMesh mesh, int instance_id)
        {
            return (NbVector3) GetPropertyVal(BufferPropertyType.VEC3,
                                            mesh.InstanceDataBuffer,
                                            instance_id * instance_struct_size_floats + instance_direction_float_Offset);
        }

        //Falloff
        public static void SetInstanceFallOff(NbMesh mesh, int instance_id, int falloff)
        {
            SetPropertyVal(mesh.InstanceDataBuffer,
                           instance_id * instance_struct_size_floats + instance_falloff_float_Offset,
                           falloff);
        }

        public static int GetInstanceFalloff(ref NbMesh mesh, int instance_id)
        {
            return (int) GetPropertyVal(BufferPropertyType.INT,
                                            mesh.InstanceDataBuffer,
                                            instance_id * instance_struct_size_floats + instance_falloff_float_Offset);
        }

        //Type
        public static void SetInstanceType(ref NbMesh mesh, int instance_id, float type)
        {
            SetPropertyVal(mesh.InstanceDataBuffer,
                           instance_id * instance_struct_size_floats + instance_type_float_Offset,
                           type);
        }

        public static int GetInstanceType(ref NbMesh mesh, int instance_id)
        {
            return (int) GetPropertyVal(BufferPropertyType.INT,
                                            mesh.InstanceDataBuffer,
                                            instance_id * instance_struct_size_floats + instance_type_float_Offset);
        }

        //FOV
        public static void SetInstanceFOV(ref NbMesh mesh, int instance_id, float fov)
        {
            SetPropertyVal(mesh.InstanceDataBuffer,
                           instance_id * instance_struct_size_floats + instance_fov_float_Offset,
                           fov);
        }

        public static float GetInstanceFOV(ref NbMesh mesh, int instance_id)
        {
            return (float) GetPropertyVal(BufferPropertyType.FLOAT,
                                            mesh.InstanceDataBuffer,
                                            instance_id * instance_struct_size_floats + instance_fov_float_Offset);
        }

        //Intensity
        public static void SetInstanceIntensity(ref NbMesh mesh, int instance_id, float intensity)
        {
            SetPropertyVal(mesh.InstanceDataBuffer,
                           instance_id * instance_struct_size_floats + instance_intensity_float_Offset,
                           intensity);
        }

        public static float GetInstanceIntensity(ref NbMesh mesh, int instance_id)
        {
            return (float) GetPropertyVal(BufferPropertyType.FLOAT,
                                            mesh.InstanceDataBuffer,
                                          instance_id * instance_struct_size_floats + instance_intensity_float_Offset);
        }


    }
    
}
