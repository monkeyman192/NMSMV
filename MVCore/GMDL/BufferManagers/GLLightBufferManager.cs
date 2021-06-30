using System;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Utils;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace MVCore.GMDL
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
        
        public static int instance_struct_size_bytes = 128;
        public const int instance_struct_size_floats = 32;

        //Instance Data Format:
        //TODO

        public int addInstance(ref GLInstancedLightMeshVao mesh, Light l)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }

            if (instance_id < GLInstancedLightMeshVao.MAX_INSTANCES)
            {

                //Uplod worldMat to the meshVao

                setInstanceWorldMat(ref mesh, instance_id, l.worldMat);
                setInstanceColor(ref mesh, instance_id, l.Color.Vec);
                setInstanceIntensity(ref mesh, instance_id, l.Intensity);
                setInstanceDirection(ref mesh, instance_id, l._direction.Vec);
                setInstanceFOV(ref mesh, instance_id, (float)Math.Cos(MathUtils.radians(l._fov)));
                setInstanceFallOff(ref mesh, instance_id, (int) l._falloff);
                setInstanceType(ref mesh, instance_id, (l._lightType == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f);
                
                mesh.instanceRefs.Add(l); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        public int addInstance(ref GLInstancedLightMeshVao mesh, Light l, Matrix4 worldMat)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }

            if (instance_id < GLInstancedLightMeshVao.MAX_INSTANCES)
            {

                //Uplod worldMat to the meshVao

                setInstanceWorldMat(ref mesh, instance_id, worldMat);
                setInstanceColor(ref mesh, instance_id, l.Color.Vec);
                setInstanceIntensity(ref mesh, instance_id, l.Intensity);
                setInstanceDirection(ref mesh, instance_id, l._direction.Vec);
                setInstanceFOV(ref mesh, instance_id, (float)Math.Cos(MathUtils.radians(l._fov)));
                setInstanceFallOff(ref mesh, instance_id, (int)l._falloff);
                setInstanceType(ref mesh, instance_id, (l._lightType == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f);

                mesh.instanceRefs.Add(l); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        //WorldMat
        private void setInstanceWorldMat(ref GLInstancedLightMeshVao mesh, int instance_id, Matrix4 mat)
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

        public Matrix4 getInstanceWorldMat(GLInstancedLightMeshVao mesh, int instance_id)
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
        public void setInstanceColor(ref GLInstancedLightMeshVao mesh, int instance_id, Vector3 color)
        {
            setPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_color_float_Offset,
                           color);
        }

        public Vector3 getInstanceColor(ref GLInstancedLightMeshVao mesh, int instance_id)
        {
            return (Vector3) getPropertyVal(BufferPropertyType.VEC4,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_color_float_Offset);
        }

        //Direction
        public void setInstanceDirection(ref GLInstancedLightMeshVao mesh, int instance_id, Vector3 dir)
        {

            setPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_direction_float_Offset,
                           dir);
        }

        public Vector3 getInstanceDirection(ref GLInstancedLightMeshVao mesh, int instance_id)
        {
            return (Vector3) getPropertyVal(BufferPropertyType.VEC3,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_direction_float_Offset);
        }

        //Falloff
        public void setInstanceFallOff(ref GLInstancedLightMeshVao mesh, int instance_id, int falloff)
        {
            setPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_falloff_float_Offset,
                           falloff);
        }

        public int getInstanceFalloff(ref GLInstancedLightMeshVao mesh, int instance_id)
        {
            return (int) getPropertyVal(BufferPropertyType.INT,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_falloff_float_Offset);
        }

        //Type
        public void setInstanceType(ref GLInstancedLightMeshVao mesh, int instance_id, float type)
        {
            setPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_type_float_Offset,
                           type);
        }

        public int getInstanceType(ref GLInstancedLightMeshVao mesh, int instance_id)
        {
            return (int) getPropertyVal(BufferPropertyType.INT,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_type_float_Offset);
        }

        //FOV
        public void setInstanceFOV(ref GLInstancedLightMeshVao mesh, int instance_id, float fov)
        {
            setPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_fov_float_Offset,
                           fov);
        }

        public float getInstanceFOV(ref GLInstancedLightMeshVao mesh, int instance_id)
        {
            return (float) getPropertyVal(BufferPropertyType.FLOAT,
                                            mesh.dataBuffer,
                                            instance_id * instance_struct_size_floats + instance_fov_float_Offset);
        }

        //Intensity
        public void setInstanceIntensity(ref GLInstancedLightMeshVao mesh, int instance_id, float intensity)
        {
            setPropertyVal(mesh.dataBuffer,
                           instance_id * instance_struct_size_floats + instance_intensity_float_Offset,
                           intensity);
        }

        public float getInstanceIntensity(ref GLInstancedLightMeshVao mesh, int instance_id)
        {
            return (float) getPropertyVal(BufferPropertyType.FLOAT,
                                            mesh.dataBuffer,
                                          instance_id * instance_struct_size_floats + instance_intensity_float_Offset);
        }


    }
    
}
