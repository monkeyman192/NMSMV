﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using OpenTK;
using OpenTK.Mathematics;


namespace MVCore.Utils
{
    public class TwosComplement
    {
        static public int toInt(uint val, int bits)
        {
            int mask = 1 << (bits - 1);
            return (int)(-(val & mask) + (val & ~mask));
        }
    }

    public static class Half
    {
        public static float decompress(uint float16)
        {
            UInt32 t1 = float16 & 0x7fff; //Non-sign bits
            UInt32 t2 = float16 & 0x8000; //Sign bit
            UInt32 t3 = float16 & 0x7c00; //Exponent


            t1 <<= 13;                              // Align mantissa on MSB
            t2 <<= 16;                              // Shift sign bit into position

            t1 += 0x38000000;                       // Adjust bias

            t1 = (t3 == 0 ? 0 : t1);                // Denormals-as-zero

            t1 |= t2;                               // Re-insert sign bit

            return BitConverter.ToSingle(BitConverter.GetBytes(t1), 0);
        }
    }

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

        public static Quaternion conjugate(Quaternion q)
        {
            Quaternion t = q;
            t.Conjugate();
            return t;
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
            array[offset + 0] = mat.Row0.X;
            array[offset + 1] = mat.Row0.Y;
            array[offset + 2] = mat.Row0.Z;
            array[offset + 3] = mat.Row0.W;
            array[offset + 4] = mat.Row1.X;
            array[offset + 5] = mat.Row1.Y;
            array[offset + 6] = mat.Row1.Z;
            array[offset + 7] = mat.Row1.W;
            array[offset + 8] = mat.Row2.X;
            array[offset + 9] = mat.Row2.Y;
            array[offset + 10] = mat.Row2.Z;
            array[offset + 11] = mat.Row2.W;
            array[offset + 12] = mat.Row3.X;
            array[offset + 13] = mat.Row3.Y;
            array[offset + 14] = mat.Row3.Z;
            array[offset + 15] = mat.Row3.W;
        
        }

        public unsafe static void insertMatToArray16(float* array, int offset, Matrix4 mat)
        {
            //mat.Transpose();//Transpose Matrix Testing
            array[offset + 0] = mat.Row0.X;
            array[offset + 1] = mat.Row0.Y;
            array[offset + 2] = mat.Row0.Z;
            array[offset + 3] = mat.Row0.W;
            array[offset + 4] = mat.Row1.X;
            array[offset + 5] = mat.Row1.Y;
            array[offset + 6] = mat.Row1.Z;
            array[offset + 7] = mat.Row1.W;
            array[offset + 8] = mat.Row2.X;
            array[offset + 9] = mat.Row2.Y;
            array[offset + 10] = mat.Row2.Z;
            array[offset + 11] = mat.Row2.W;
            array[offset + 12] = mat.Row3.X;
            array[offset + 13] = mat.Row3.Y;
            array[offset + 14] = mat.Row3.Z;
            array[offset + 15] = mat.Row3.W;

        }

        

        public unsafe static Matrix4 Matrix4FromArray(float* array, int offset)
        {
            Matrix4 mat = Matrix4.Identity;

            mat.Row0.X = array[offset + 0];
            mat.Row0.Y = array[offset + 1];
            mat.Row0.Z = array[offset + 2];
            mat.Row0.W = array[offset + 3];
            mat.Row1.X = array[offset + 4];
            mat.Row1.Y = array[offset + 5];
            mat.Row1.Z = array[offset + 6];
            mat.Row1.W = array[offset + 7];
            mat.Row2.X = array[offset + 8];
            mat.Row2.Y = array[offset + 9];
            mat.Row2.Z = array[offset + 10];
            mat.Row2.W = array[offset + 11];
            mat.Row3.X = array[offset + 12];
            mat.Row3.Y = array[offset + 13];
            mat.Row3.Z = array[offset + 14];
            mat.Row3.W = array[offset + 15];

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
            return (float) Math.Min(Math.Max(min, val), (double) max);
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

            //return (float) Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return dx * dx + dy * dy + dz * dz;

        }


        public static float distance_Point_to_AABB_alt(Vector3 aabb_min, Vector3 aabb_max, Vector3 p)
        {
            float dx = Math.Min(Math.Abs(aabb_min.X - p.X), Math.Abs(aabb_max.X - p.X));
            float dy = Math.Min(Math.Abs(aabb_min.Y - p.Y), Math.Abs(aabb_max.Y - p.Y));
            float dz = Math.Min(Math.Abs(aabb_min.Z - p.Z), Math.Abs(aabb_max.Z - p.Z));

            //Triggers
            bool is_in_x = (p.X <= aabb_max.X) && (p.X >= aabb_min.X);
            bool is_in_y = (p.Y <= aabb_max.Y) && (p.Y >= aabb_min.Y);
            bool is_in_z = (p.Z <= aabb_max.Z) && (p.Z >= aabb_min.Z);

            //Cases

            if (is_in_x)
            {
                if (is_in_y)
                {
                    if (is_in_z)
                    {
                        return Math.Min(dx, Math.Min(dy, dz));
                    }
                    else
                    {
                        return dz;
                    }
                } else
                {
                    if (is_in_z)
                    {
                        return dy;
                    }
                    else
                    {
                        return (float) Math.Sqrt(dz * dz + dy * dy);
                    }
                }

            } else
            {
                if (is_in_y)
                {
                    if (is_in_z)
                    {
                        return dx;
                    }
                    else
                    {
                        return (float)Math.Sqrt(dx * dx + dz * dz);
                    }
                } else
                {
                    if (is_in_z)
                    {
                        return (float)Math.Sqrt(dx * dx + dy * dy);
                    }
                    else
                    {
                        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    }
                }
            }









        }

        public static Vector3 quaternionToEuler(Quaternion q)
        {
            Matrix4 rotMat = Matrix4.CreateFromQuaternion(q);
            rotMat.Transpose();
            Vector3 test;
            test = matrixToEuler(rotMat, "YZX");
            return test;
        }

        public static Vector3 matrixToEuler(Matrix4 rotMat, string order)
        {
            Vector3 euler = new();

            //rotMat.Transpose();
            double m11 = rotMat[0, 0];
            double m12 = rotMat[0, 1];
            double m13 = rotMat[0, 2];
            double m21 = rotMat[1, 0];
            double m22 = rotMat[1, 1];
            double m23 = rotMat[1, 2];
            double m31 = rotMat[2, 0];
            double m32 = rotMat[2, 1];
            double m33 = rotMat[2, 2];

            if (order == "XYZ")
            {

                euler.Y = (float)Math.Asin(MathUtils.clamp(m13, -1, 1));
                if (Math.Abs(m13) < 0.99999)
                {

                    euler.X = (float)Math.Atan2(-m23, m33);
                    euler.Z = (float)Math.Atan2(-m12, m11);
                }
                else
                {

                    euler.X = (float)Math.Atan2(m32, m22);
                    euler.Z = 0;
                }

            }
            else if (order == "YXZ")
            {

                euler.X = (float)Math.Asin(-MathUtils.clamp(m23, -1, 1));

                if (Math.Abs(m23) < 0.99999)
                {

                    euler.Y = (float)Math.Atan2(m13, m33);
                    euler.Z = (float)Math.Atan2(m21, m22);
                }
                else
                {

                    euler.Y = (float)Math.Atan2(-m31, m11);
                    euler.Z = 0;
                }

            }
            else if (order == "ZXY")
            {

                euler.X = (float)Math.Asin(MathUtils.clamp(m32, -1, 1));

                if (Math.Abs(m32) < 0.99999)
                {

                    euler.Y = (float)Math.Atan2(-m31, m33);
                    euler.Z = (float)Math.Atan2(-m12, m22);

                }
                else
                {

                    euler.Y = 0;
                    euler.Z = (float)Math.Atan2(m21, m11);

                }

            }
            else if (order == "ZYX")
            {

                euler.Y = (float)Math.Asin(-MathUtils.clamp(m31, -1, 1));

                if (Math.Abs(m31) < 0.99999)
                {

                    euler.X = (float)Math.Atan2(m32, m33);
                    euler.Z = (float)Math.Atan2(m21, m11);

                }
                else
                {

                    euler.X = 0;
                    euler.Z = (float)Math.Atan2(-m12, m22);

                }

            }
            else if (order == "YZX")
            {

                euler.Z = (float)Math.Asin(MathUtils.clamp(m21, -1, 1));

                if (Math.Abs(m21) < 0.99999)
                {

                    euler.X = (float)Math.Atan2(-m23, m22);
                    euler.Y = (float)Math.Atan2(-m31, m11);

                }
                else
                {

                    euler.X = 0;
                    euler.Y = (float)Math.Atan2(m13, m33);

                }

            }
            else if (order == "XZY")
            {

                euler.Z = (float)Math.Asin(-MathUtils.clamp(m12, -1, 1));

                if (Math.Abs(m12) < 0.99999)
                {

                    euler.X = (float)Math.Atan2(m32, m22);
                    euler.Y = (float)Math.Atan2(m13, m11);

                }
                else
                {

                    euler.X = (float)Math.Atan2(-m23, m33);
                    euler.Y = 0;

                }

            }
            else
            {
                Console.WriteLine("Unsupported Order");
            }


            //Convert to degrees
            euler.X = MathUtils.degrees(euler.X);
            euler.Y = MathUtils.degrees(euler.Y);
            euler.Z = MathUtils.degrees(euler.Z);

            //Console.WriteLine("Converted Angles {0} {1} {2}", euler.X, euler.Y, euler.Z);

            return euler;
        }


        public static float[] convertVec(Vector3 vec)
        {
            float[] fmat = new float[3];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;

            return fmat;
        }

        public static float[] convertVec(Vector4 vec)
        {
            float[] fmat = new float[4];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            fmat[3] = vec.W;

            return fmat;
        }

        public static float[] convertMat(Matrix4 mat)
        {
            float[] fmat = new float[16];
            fmat[0] = mat.M11;
            fmat[1] = mat.M12;
            fmat[2] = mat.M13;
            fmat[3] = mat.M14;
            fmat[4] = mat.M21;
            fmat[5] = mat.M22;
            fmat[6] = mat.M23;
            fmat[7] = mat.M24;
            fmat[8] = mat.M31;
            fmat[9] = mat.M32;
            fmat[10] = mat.M33;
            fmat[11] = mat.M34;
            fmat[12] = mat.M41;
            fmat[13] = mat.M42;
            fmat[14] = mat.M43;
            fmat[15] = mat.M44;

            return fmat;
        }

    }
}
