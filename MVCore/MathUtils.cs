﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenTK;

namespace MVCore
{
    public static class MathUtils
    {
        public static float FloatParse(string text)
        {
            float res = float.Parse(text, CultureInfo.InvariantCulture);
            return res;
        }
        
        public static float[] mulMatArrays(float[] lmat1, float[] lmat2, int count)
        {
            float[] res = new float[count * 16];
            Array.Clear(res, 0, count * 16);
            for (int i = 0; i < count; i++)
            {
                int off = 16 * i;
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                        for (int m = 0; m < 4; m++)
                            res[off + 4 * j + k] += lmat1[off + 4 * j + m] * lmat2[off + 4 * m + k];
            }

            return res;
        }
        


        public static void mulMatArrays(ref float[] dest, float[] lmat1, float[] lmat2, int count)
        {
            for (int i = 0; i < count; i++)
            {
                /* Iterative version
                int off = 16 * i;
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                    {
                        dest[off + 4 * j + k] = 0.0f;
                        for (int m = 0; m < 4; m++)
                            dest[off + 4 * j + k] += lmat1[off + 4 * j + m] * lmat2[off + 4 * m + k];
                    }
                */

                //Unrolled version
                int off = 16 * i;
                
                for (int j = 0; j < 4; j++)
                {
                    //k = 0
                    dest[off + 4 * j + 0] = lmat1[off + 4 * j + 0] * lmat2[off + 4 * 0 + 0];
                    dest[off + 4 * j + 0] += lmat1[off + 4 * j + 1] * lmat2[off + 4 * 1 + 0];
                    dest[off + 4 * j + 0] += lmat1[off + 4 * j + 2] * lmat2[off + 4 * 2 + 0];
                    dest[off + 4 * j + 0] += lmat1[off + 4 * j + 3] * lmat2[off + 4 * 3 + 0];

                    //k = 1
                    dest[off + 4 * j + 1] =  lmat1[off + 4 * j + 0] * lmat2[off + 4 * 0 + 1];
                    dest[off + 4 * j + 1] += lmat1[off + 4 * j + 1] * lmat2[off + 4 * 1 + 1];
                    dest[off + 4 * j + 1] += lmat1[off + 4 * j + 2] * lmat2[off + 4 * 2 + 1];
                    dest[off + 4 * j + 1] += lmat1[off + 4 * j + 3] * lmat2[off + 4 * 3 + 1];

                    //k = 2
                    dest[off + 4 * j + 2] =  lmat1[off + 4 * j + 0] * lmat2[off + 4 * 0 + 2];
                    dest[off + 4 * j + 2] += lmat1[off + 4 * j + 1] * lmat2[off + 4 * 1 + 2];
                    dest[off + 4 * j + 2] += lmat1[off + 4 * j + 2] * lmat2[off + 4 * 2 + 2];
                    dest[off + 4 * j + 2] += lmat1[off + 4 * j + 3] * lmat2[off + 4 * 3 + 2];

                    //k = 3
                    dest[off + 4 * j + 3] =  lmat1[off + 4 * j + 0] * lmat2[off + 4 * 0 + 3];
                    dest[off + 4 * j + 3] += lmat1[off + 4 * j + 1] * lmat2[off + 4 * 1 + 3];
                    dest[off + 4 * j + 3] += lmat1[off + 4 * j + 2] * lmat2[off + 4 * 2 + 3];
                    dest[off + 4 * j + 3] += lmat1[off + 4 * j + 3] * lmat2[off + 4 * 3 + 3];
                }
            }

        }

        public static void vectofloatArray(float[] flist, List<Vector3> veclist)
        {
            int count = veclist.Count;
            for (int i = 0; i < count; i++)
            {
                flist[3 * i] = veclist[i].X;
                flist[3 * i + 1] = veclist[i].Y;
                flist[3 * i + 2] = veclist[i].Z;
            }
        }

        //Add matrix to JMArray
        public static void insertMatToArray16(float[] array, int offset, Matrix4 mat)
        {
            //mat.Transpose();//Transpose Matrix Testing
            array[offset + 0] = mat.M11;
            array[offset + 1] = mat.M12;
            array[offset + 2] = mat.M13;
            array[offset + 3] = mat.M14;
            array[offset + 4] = mat.M21;
            array[offset + 5] = mat.M22;
            array[offset + 6] = mat.M23;
            array[offset + 7] = mat.M24;
            array[offset + 8] = mat.M31;
            array[offset + 9] = mat.M32;
            array[offset + 10] = mat.M33;
            array[offset + 11] = mat.M34;
            array[offset + 12] = mat.M41;
            array[offset + 13] = mat.M42;
            array[offset + 14] = mat.M43;
            array[offset + 15] = mat.M44;
        }

        public unsafe static void insertMatToArray16(float* array, int offset, Matrix4 mat)
        {
            //mat.Transpose();//Transpose Matrix Testing
            array[offset + 0] = mat.M11;
            array[offset + 1] = mat.M12;
            array[offset + 2] = mat.M13;
            array[offset + 3] = mat.M14;
            array[offset + 4] = mat.M21;
            array[offset + 5] = mat.M22;
            array[offset + 6] = mat.M23;
            array[offset + 7] = mat.M24;
            array[offset + 8] = mat.M31;
            array[offset + 9] = mat.M32;
            array[offset + 10] = mat.M33;
            array[offset + 11] = mat.M34;
            array[offset + 12] = mat.M41;
            array[offset + 13] = mat.M42;
            array[offset + 14] = mat.M43;
            array[offset + 15] = mat.M44;
        }

        public unsafe static Matrix4 Matrix4FromArray(float* array, int offset)
        {
            Matrix4 mat = Matrix4.Identity;

            mat.M11 = array[offset + 0];
            mat.M12 = array[offset + 1];
            mat.M13 = array[offset + 2];
            mat.M14 = array[offset + 3];
            mat.M21 = array[offset + 4];
            mat.M22 = array[offset + 5];
            mat.M23 = array[offset + 6];
            mat.M24 = array[offset + 7];
            mat.M31 = array[offset + 8];
            mat.M32 = array[offset + 9];
            mat.M33 = array[offset + 10];
            mat.M34 = array[offset + 11];
            mat.M41 = array[offset + 12];
            mat.M42 = array[offset + 13];
            mat.M43 = array[offset + 14];
            mat.M44 = array[offset + 15];

            return mat;
        }

        public unsafe static Matrix4 Matrix4FromArray(float[] array, int offset)
        {
            Matrix4 mat = Matrix4.Identity;

            mat.M11 = array[offset + 0];
            mat.M12 = array[offset + 1];
            mat.M13 = array[offset + 2];
            mat.M14 = array[offset + 3];
            mat.M21 = array[offset + 4];
            mat.M22 = array[offset + 5];
            mat.M23 = array[offset + 6];
            mat.M24 = array[offset + 7];
            mat.M31 = array[offset + 8];
            mat.M32 = array[offset + 9];
            mat.M33 = array[offset + 10];
            mat.M34 = array[offset + 11];
            mat.M41 = array[offset + 12];
            mat.M42 = array[offset + 13];
            mat.M43 = array[offset + 14];
            mat.M44 = array[offset + 15];
            
            return mat;
        }

        public static void insertMatToArray12Trans(float[] array, int offset, Matrix4 mat)
        {
            //mat.Transpose();//Transpose Matrix Testing
            array[offset + 0] = mat.M11;
            array[offset + 1] = mat.M21;
            array[offset + 2] = mat.M31;
            array[offset + 3] = mat.M41;
            array[offset + 4] = mat.M12;
            array[offset + 5] = mat.M22;
            array[offset + 6] = mat.M32;
            array[offset + 7] = mat.M42;
            array[offset + 8] = mat.M13;
            array[offset + 9] = mat.M23;
            array[offset + 10] = mat.M33;
            array[offset + 11] = mat.M43;
        }

        public static float Matrix4Norm(Matrix4 a, Matrix4 b)
        {
            float n = 0.0f;
            Matrix4 temp = a - b;
            for (int i=0; i<4; i++)
                for (int j = 0; j < 4; j++)
                {
                    n += temp[i, j] * temp[i, j];
                }

            return (float) Math.Sqrt(n);
        }

        public static bool isIdentity(Matrix4 mat)
        {
            //Hacks, i have no idea yet if mathematically this is valid
            if (mat.M11 != 1.0f)
                return false;
            if (mat.M22 != 1.0f)
                return false;
            if (mat.M33 != 1.0f)
                return false;
            if (mat.M44 != 1.0f)
                return false;

            return true;
        }


        public static float radians(float angle)
        {
            return ((float) Math.PI / 180) * angle;
        }

        public static float degrees(float radians)
        {
            return (float) (radians * 180.0 / (float) Math.PI);
        }

        public static float clamp(float val, float min, float max)
        {
            return (float)Math.Min(Math.Max((double) min, (double) val), (double)max);
        }

        public static double clamp(double val, double min, double max)
        {
            return Math.Min(Math.Max(min, val), max);
        }

        public static float distance_Point_to_AABB(Vector3 aabb_min, Vector3 aabb_max, Vector3 p)
        {
            float dx = Math.Max(Math.Max(aabb_min.X - p.X, 0), aabb_max.X);
            float dy = Math.Max(Math.Max(aabb_min.Y - p.Y, 0), aabb_max.Y);
            float dz = Math.Max(Math.Max(aabb_min.Z - p.Z, 0), aabb_max.Z);

            return (float) Math.Sqrt(dx * dx + dy * dy + dz * dz);
        
        }


    }
}
