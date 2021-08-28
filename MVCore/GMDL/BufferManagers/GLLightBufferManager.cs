using System;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Systems;
using MVCore.Utils;
using System.Runtime.InteropServices;
using System.Collections.Generic;


namespace MVCore
{
    
    [StructLayout(LayoutKind.Explicit)]
    public struct GLLight
    {
        [FieldOffset(0)]
        public Vector3 position; 
        [FieldOffset(12)]
        public float isRenderable; 
        [FieldOffset(16)]
        public Vector3 color; 
        [FieldOffset(28)]
        public float intensity; 
        [FieldOffset(32)]
        public Vector3 direction; 
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

        public int AddInstance(ref GLInstancedLightMesh mesh, SceneGraphNode l)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }

            if (instance_id < GLInstancedLightMesh.MAX_INSTANCES)
            {

                //Uplod worldMat to the meshVao
                SetInstanceWorldMat(ref mesh, instance_id, TransformationSystem.GetEntityWorldMat(l));
                
                //TODO: Implement Light Component and set all light properties
                //SetInstanceColor(ref mesh, instance_id, l.Color);
                //SetInstanceIntensity(ref mesh, instance_id, l.Intensity);
                //SetInstanceDirection(ref mesh, instance_id, l.Direction);
                //SetInstanceFOV(ref mesh, instance_id, (float)Math.Cos(MathUtils.radians(l.FOV)));
                //SetInstanceFallOff(ref mesh, instance_id, (int) l.Falloff);
                //SetInstanceType(ref mesh, instance_id, (l.LightType == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f);
                
                mesh.instanceRefs.Add(l); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        public int AddInstance(ref GLInstancedLightMesh mesh, SceneGraphNode l, Matrix4 worldMat)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }

            if (instance_id < GLInstancedLightMesh.MAX_INSTANCES)
            {

                //Uplod worldMat to the meshVao
                SetInstanceWorldMat(ref mesh, instance_id, worldMat);
                
                
                //TODO: Implement Light Component and use it to properly pass light properties
                //SetInstanceColor(ref mesh, instance_id, l.Color);
                //SetInstanceIntensity(ref mesh, instance_id, l.Intensity);
                //SetInstanceDirection(ref mesh, instance_id, l.Direction);
                //SetInstanceFOV(ref mesh, instance_id, (float)Math.Cos(MathUtils.radians(l.FOV)));
                //SetInstanceFallOff(ref mesh, instance_id, (int)l.Falloff);
                //SetInstanceType(ref mesh, instance_id, (l.LightType == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f);

                mesh.instanceRefs.Add(l); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        //WorldMat
        private void SetInstanceWorldMat(ref GLInstancedLightMesh mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instance_id * instance_struct_size_floats + instance_worldMat_float_offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public Matrix4 GetInstanceWorldMat(GLInstancedLightMesh mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instance_id * instance_struct_size_floats + instance_worldMat_float_offset;
                    return MathUtils.Matrix4FromArray(ar, offset);
                }
            }

        }


        //Color
        public static void SetInstanceColor(ref GLInstancedLightMesh mesh, int instance_id, Vector3 color)
        {
            SetPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_color_float_Offset,
                           color);
        }

        public static Vector3 GetInstanceColor(ref GLInstancedLightMesh mesh, int instance_id)
        {
            return (Vector3) GetPropertyVal(BufferPropertyType.VEC4,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_color_float_Offset);
        }

        //Direction
        public static void SetInstanceDirection(ref GLInstancedLightMesh mesh, int instance_id, Vector3 dir)
        {

            SetPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_direction_float_Offset,
                           dir);
        }

        public static Vector3 GetInstanceDirection(ref GLInstancedLightMesh mesh, int instance_id)
        {
            return (Vector3) GetPropertyVal(BufferPropertyType.VEC3,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_direction_float_Offset);
        }

        //Falloff
        public static void SetInstanceFallOff(ref GLInstancedLightMesh mesh, int instance_id, int falloff)
        {
            SetPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_falloff_float_Offset,
                           falloff);
        }

        public static int GetInstanceFalloff(ref GLInstancedLightMesh mesh, int instance_id)
        {
            return (int) GetPropertyVal(BufferPropertyType.INT,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_falloff_float_Offset);
        }

        //Type
        public static void SetInstanceType(ref GLInstancedLightMesh mesh, int instance_id, float type)
        {
            SetPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_type_float_Offset,
                           type);
        }

        public static int GetInstanceType(ref GLInstancedLightMesh mesh, int instance_id)
        {
            return (int) GetPropertyVal(BufferPropertyType.INT,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_type_float_Offset);
        }

        //FOV
        public static void SetInstanceFOV(ref GLInstancedLightMesh mesh, int instance_id, float fov)
        {
            SetPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_fov_float_Offset,
                           fov);
        }

        public static float GetInstanceFOV(ref GLInstancedLightMesh mesh, int instance_id)
        {
            return (float) GetPropertyVal(BufferPropertyType.FLOAT,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_fov_float_Offset);
        }

        //Intensity
        public static void SetInstanceIntensity(ref GLInstancedLightMesh mesh, int instance_id, float intensity)
        {
            SetPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_intensity_float_Offset,
                           intensity);
        }

        public static float GetInstanceIntensity(ref GLInstancedLightMesh mesh, int instance_id)
        {
            return (float) GetPropertyVal(BufferPropertyType.FLOAT,
                                            mesh.dataBuffer,
                                          instance_id * instance_struct_size_floats + instance_intensity_float_Offset);
        }


    }
    
}
