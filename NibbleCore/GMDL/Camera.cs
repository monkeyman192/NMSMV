﻿using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using NbCore;
using NbCore.Math;
using NbCore.Utils;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra.Complex;
using System.ComponentModel;
using System.Drawing.Design;
using System.Security.Permissions;

namespace NbCore
{
    public struct CameraPos
    {
        public NbVector3 PosImpulse;
        public NbVector2 Rotation;

        public void Reset()
        {
            PosImpulse = new NbVector3(0.0f);
            Rotation = new NbVector2(0.0f);
        }
    }

    public class Camera : Entity
    {
        //Base Coordinate System
        public static NbVector3 BaseRight = new(1.0f, 0.0f, 0.0f);
        public static NbVector3 BaseFront = new(0.0f, 0.0f, -1.0f);
        public static NbVector3 BaseUp = new(0.0f, 1.0f, 0.0f);

        //Current Vectors
        public NbVector3 Right;
        public NbVector3 Front;
        public NbVector3 Up;
        public float yaw = MathUtils.radians(-90.0f);
        public float pitch = 0.0f;
        public NbVector3 Position = new(0.0f, 0.0f, 0.0f);
        //Movement Time
        
        public float Speed = 1.0f; //Speed in Units/Sec
        public static float SpeedScale = 0.001f;
        public float Sensitivity = 0.2f;
        public bool isActive = false;
        //Projection variables Set defaults
        public float fov = 45.0f; //Angle in degrees
        public float zNear = 0.02f;
        public float zFar = 15000.0f;
        public float aspect = 1.0f;
        
        //Matrices
        public NbMatrix4 projMat;
        public NbMatrix4 projMatInv;
        public NbMatrix4 lookMat;
        public NbMatrix4 lookMatInv;
        public NbMatrix4 viewMat = NbMatrix4.Identity();
        public int type;
        public bool culling;

        //Camera Frustum Planes
        private readonly Frustum extFrustum = new();
        public NbVector4[] frPlanes = new NbVector4[6];

        //Rendering Stuff
        public GLVao vao;
        public int program;
        
        public Camera(int angle, int program, int mode, bool cull) : base(EntityType.Camera)
        {
            //Set fov on init
            fov = angle;
            Primitives.Box _box = new Primitives.Box(1.0f, 1.0f, 1.0f, new NbVector3(1.0f), true);
            vao = _box.getVAO();
            _box.Dispose();
            this.program = program;
            type = mode;
            culling = cull;

            //calcCameraOrientation(ref Front, ref Right, ref Up, 0, 0);

            //Set Orientation to the basis
            Right = BaseRight;
            Up = BaseUp;
            Front = BaseFront;

            //Initialize the viewmat
            this.updateViewMatrix();

        }
        
        public void updateViewMatrix()
        {
            lookMat = NbMatrix4.LookAt(Position, Position + Front, BaseUp);
            float fov_rad = MathUtils.radians(fov);
            
            if (type == 0) {
                //projMat = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);
                //Call Custom
                //projMat = this.ComputeFOVProjection();
                float w, h;
                float tangent = (float) System.Math.Tan(fov_rad / 2.0f);   // tangent of half fovY
                h = zNear * tangent;  // half height of near plane
                w = h * aspect;       // half width of near plane

                //projMat = Matrix4.CreatePerspectiveOffCenter(-w, w, -h, h, zNear, zFar);
                projMat = NbMatrix4.CreatePerspectiveFieldOfView(fov_rad, aspect, zNear, zFar);
                viewMat = lookMat * projMat;
            }
            else
            {
                //Create orthographic projection
                projMat = NbMatrix4.CreateOrthographic(aspect * 2.0f, 2.0f, zNear, zFar);
                //projMat.Transpose();
                //Create scale matrix based on the fov
                NbMatrix4 scaleMat = NbMatrix4.CreateScale(0.8f * fov_rad);
                viewMat = scaleMat * lookMat * projMat;
            }
            
            //Calculate invert Matrices
            lookMatInv = lookMat.Inverted();
            projMatInv = projMat.Inverted();
            
            updateFrustumPlanes();
        }

        public static void UpdateCameraDirectionalVectors(Camera cam)
        {
            //Update Camera Vectors
            TransformComponent tc = cam.GetComponent<TransformComponent>() as TransformComponent;

            //Apply Current Position to the Camera
            cam.Position = tc.Data.localTranslation;

            //Console.WriteLine(string.Format("Camera Position {0} {1} {2}",
            //                    cam.Position.X, cam.Position.Y, cam.Position.Z));

            cam.Front.X = (float) System.Math.Cos(cam.yaw) * (float) System.Math.Cos(cam.pitch);
            cam.Front.Y = (float) System.Math.Sin(cam.pitch);
            cam.Front.Z = (float) System.Math.Sin(cam.yaw) * (float) System.Math.Cos(cam.pitch);
            cam.Front.Normalize();

            cam.Up.X = (float) System.Math.Cos(cam.yaw) * (float)System.Math.Cos(cam.pitch + System.Math.PI / 2.0f);
            cam.Up.Y = (float) System.Math.Sin(cam.pitch + System.Math.PI / 2.0f);
            cam.Up.Z = (float) System.Math.Sin(cam.yaw) * (float)System.Math.Cos(cam.pitch + System.Math.PI / 2.0f);
            cam.Up.Normalize();
            
            cam.Right = cam.Front.Cross(cam.Up).Normalized();
            //cam.Up = Vector3.Cross(cam.Right, cam.Front);
        }

        public static void CalculateNextCameraState(Camera cam, CameraPos target)
        {
            TransformController t_controller = Common.RenderState.engineRef.transformSys.GetEntityTransformController(cam);

            //Calculate actual camera speed
            cam.yaw += -cam.Sensitivity * MathUtils.radians(target.Rotation.X);
            cam.pitch += cam.Sensitivity * MathUtils.radians(target.Rotation.Y);
            cam.pitch = MathUtils.clamp(cam.pitch, -89.0f, 89.0f);

            //Console.WriteLine("Mouse Displacement {0} {1}",
            //                target.Rotation.X, target.Rotation.Y);

            //Console.WriteLine(string.Format("Camera Rotation {0} {1} {2} {3}",
            //                    rx.X, rx.Y, rx.Z, rx.W),
            //                    Common.LogVerbosityLevel.INFO);

            //Move Camera based on the impulse

            //Calculate Next State 
            NbVector3 currentPosition = t_controller.LastPosition;
            NbQuaternion currentRotation = t_controller.LastRotation;
            NbVector3 currentScale = new(1.0f);

            NbVector3 offset = new();
            offset += SpeedScale * cam.Speed * target.PosImpulse.X * cam.Right;
            offset += SpeedScale * cam.Speed * target.PosImpulse.Y * cam.Front;
            offset += SpeedScale * cam.Speed * target.PosImpulse.Z * cam.Up;


            //Console.WriteLine(string.Format("Camera offset {0} {1} {2}",
            //                    offset.X, offset.Y, offset.Z));
             
            currentPosition += offset;
            //There is no need to update rotation for the camera. Pitch/Yaw is all we need
            //Quaternion rall = Quaternion.FromEulerAngles(cam.pitch, cam.yaw, 0.0f);
            //currentRotation = rall;

            t_controller.AddFutureState(currentPosition, currentRotation, currentScale);

        }

        /*
        public void updateTarget(CameraPos target, float interval)
        {
            //Interval is the update interval of the movement defined in the control camera timer
            
            //Cache current Position + Orientation
            PrevPosition = Position;
            PrevDirection = Direction;

            //Rotate Direction
            Quaternion rx = Quaternion.FromAxisAngle(Up, -target.Rotation.X * Sensitivity);
            Quaternion ry = Quaternion.FromAxisAngle(Right, -target.Rotation.Y * Sensitivity); //Looks OK
            //Quaternion rz = Quaternion.FromAxisAngle(Front, 0.0f); //Looks OK

            TargetDirection = Direction * rx * ry;

            float actual_speed = (float) Math.Pow(Speed, SpeedPower);
            
            float step = 0.00001f;
            Vector3 offset = new();
            offset += step * actual_speed * target.PosImpulse.X * Right;
            offset += step * actual_speed * target.PosImpulse.Y * Front;
            offset += step * actual_speed * target.PosImpulse.Z * Up;

            //Update final vector
            TargetPosition += offset;

            //Calculate Time for movement
            
            //Console.WriteLine("TargetPos {0} {1} {2}",
            //    TargetPosition.X, TargetPosition.Y, TargetPosition.Z);
            //Console.WriteLine("PrevPos {0} {1} {2}",
            //    PrevPosition.X, PrevPosition.Y, PrevPosition.Z);
            //Console.WriteLine("TargetRotation {0} {1} {2} {3}",
            //    TargetDirection.X, TargetDirection.Y, TargetDirection.Z, TargetDirection.W);
            //Console.WriteLine("PrevRotation {0} {1} {2} {3}",
            //    PrevDirection.X, PrevDirection.Y, PrevDirection.Z, PrevDirection.W);
            

            float eff_speed = interval * actual_speed / 1000.0f;
            t_pos_move = (TargetPosition - PrevPosition).Length / eff_speed;
            t_rot_move = (TargetDirection - PrevDirection).Length / eff_speed;
            t_start = 0.0f; //Reset time_counter

            //Console.WriteLine("t_pos {0}, t_rot {1}", t_pos_move, t_rot_move);

        }

        */

        /*
        public void Move(double dt)
        {
            
            //calculate interpolation coeff
            t_start += (float) dt;
            float pos_lerp_coeff, rot_lerp_coeff;

            pos_lerp_coeff = t_start / (float) Math.Max(t_pos_move, 1e-4);
            pos_lerp_coeff = MathUtils.clamp(pos_lerp_coeff, 0.0f, 1.0f);
            
            rot_lerp_coeff = t_start / (float)Math.Max(t_rot_move, 1e-4);
            rot_lerp_coeff = MathUtils.clamp(rot_lerp_coeff, 0.0f, 1.0f);
            
            
            //Interpolate Quaternions/Vectors
            Direction = PrevDirection * (1.0f - rot_lerp_coeff) +
                        TargetDirection * rot_lerp_coeff;
            Position = PrevPosition * (1.0f - pos_lerp_coeff) +
                    TargetPosition * pos_lerp_coeff;

            //Update Base Axis
            Quaternion newFront = MathUtils.conjugate(Direction) * new Quaternion(BaseFront, 0.0f) * Direction;
            Front = newFront.Xyz.Normalized();
            Right = Vector3.Cross(Front, BaseUp).Normalized();
            Up = Vector3.Cross(Right, Front).Normalized();
        }
        */

        public void updateFrustumPlanes()
        {
            //projMat.Transpose();
            //extFrustum.CalculateFrustum(projMat, lookMat); //Old Method
            extFrustum.CalculateFrustum(viewMat); // New Method
            return;
            /*
            Matrix4 mat = viewMat;
            mat.Transpose();
            //Matrix4 mat = proj;
            //Left
            frPlanes[0] = mat.Row0 + mat.Row3;
            //Right
            frPlanes[1] = mat.Row3 - mat.Row0;
            //Bottom
            frPlanes[2] = mat.Row3 + mat.Row1;
            //Top
            frPlanes[3] = mat.Row3 - mat.Row1;
            //Near
            frPlanes[4] = mat.Row3 + mat.Row2;
            //Far
            frPlanes[5] = mat.Row3 - mat.Row2;
            //Normalize them
            for (int i = 0; i < 6; i++)
            { 
                float l = frPlanes[i].Xyz.Length;
                //Normalize
                frPlanes[i].X /= l;
                frPlanes[i].Y /= l;
                frPlanes[i].Z /= l;
                frPlanes[i].W /= l;
            }
            */
        }

        public bool frustum_occlude(NbVector3 AABBMIN, NbVector3 AABBMAX, NbMatrix4 transform)
        {
            if (!Common.RenderState.settings.renderSettings.UseFrustumCulling)
                return true;

            float radius = 0.5f * (AABBMIN - AABBMAX).Length;
            NbVector3 bsh_center = AABBMIN + 0.5f * (AABBMAX - AABBMIN);

            //Move sphere to object's root position
            bsh_center = (new NbVector4(bsh_center, 1.0f) * transform).Xyz;

            //This is not accurate for some fucking reason
            //return extFrustum.AABBVsFrustum(cand.Bbox, cand.worldMat * transform);

            //In the future I should add the original AABB as well, spheres look to work like a charm for now   
            return extFrustum.SphereVsFrustum(bsh_center, radius);
        }


        public bool frustum_occlude(NbMesh mesh, NbMatrix4 transform)
        {
            if (!culling) return true;

            NbVector4 v1, v2;

            v1 = new NbVector4(mesh.MetaData.AABBMIN, 1.0f);
            v2 = new NbVector4(mesh.MetaData.AABBMAX, 1.0f);
            
            return frustum_occlude(v1.Xyz, v2.Xyz, transform);
        }

        public void render()
        {
            GL.UseProgram(program);

            //Keep manual rendering for the camera because it needs vertex updates
            //Init Arrays

            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5 };

            int b_size = extFrustum._frustum_points.Length * sizeof(float);
            byte[] verts_b = new byte[b_size];

            int i_size = indices.Length * sizeof(Int32);
            byte[] indices_b = new byte[i_size];

            System.Buffer.BlockCopy(extFrustum._frustum_points, 0, verts_b, 0, b_size);
            System.Buffer.BlockCopy(indices, 0, indices_b, 0, i_size);

            //Generate OpenGL buffers
            int vertex_buffer_object;
            int element_buffer_object;

            GL.GenBuffers(1, out vertex_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) b_size, verts_b, BufferUsageHint.StaticDraw);
            
            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) i_size, indices_b, BufferUsageHint.StaticDraw);

            //Render Elements
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            int vpos = GL.GetAttribLocation(program, "vPosition");
            GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(vpos);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.PointSize(10.0f);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedShort, indices_b);
            GL.DrawRangeElements(PrimitiveType.Triangles, 0, 36,
                36, DrawElementsType.UnsignedInt, (IntPtr) 0);
            //GL.DrawArrays(PrimitiveType.Points, 0, 8); //Works - renders points
            
            //Debug.WriteLine("Locator Object {2} vpos {0} cpos {1} prog {3}", vpos, cpos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vertex_buffer_object,this.color_buffer_object);

            GL.DisableVertexAttribArray(vpos);
            indices = null;
            verts_b = null;
            indices_b = null;
        }
    }

    public class Frustum
    {
        private readonly NbVector4[] _frustum = new NbVector4[6];
        public float[,] _frustum_points = new float[8, 3];
        
        public const int A = 0;
        public const int B = 1;
        public const int C = 2;
        public const int D = 3;

        public enum ClippingPlane : int
        {
            Right = 0,
            Left = 1,
            Bottom = 2,
            Top = 3,
            Back = 4,
            Front = 5
        }

        private static void NormalizePlane(float[,] frustum, int side)
        {
            float magnitude = 1.0f / (float)System.Math.Sqrt((frustum[side, 0] * frustum[side, 0]) + (frustum[side, 1] * frustum[side, 1])
                                                + (frustum[side, 2] * frustum[side, 2]));
            frustum[side, 0] *= magnitude;
            frustum[side, 1] *= magnitude;
            frustum[side, 2] *= magnitude;
            frustum[side, 3] *= magnitude;
        }

        public bool PointVsFrustum(NbVector4 point)
        {
            for (int i = 0; i < 6; i++)
            {
                if (NbVector4.Dot(_frustum[i],point) <= 0.0f)
                {
                    //Console.WriteLine("Point vs Frustum, Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p, x, y, z);
                    return false;
                }
                    
            }
            return true;
        }

        public bool PointVsFrustum(NbVector3 location)
        {
            return PointVsFrustum(new NbVector4(location, 1.0f));
        }


        public bool AABBVsFrustum(NbVector3[] AABB)
        {
            //Transform points from local to model space
            NbVector4[] tr_AABB = new NbVector4[2];

            tr_AABB[0] = new NbVector4(AABB[0], 1.0f);
            tr_AABB[1] = new NbVector4(AABB[1], 1.0f);


            NbVector4[] verts = new NbVector4[8];
            verts[0] = new NbVector4(tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z, 1.0f);
            verts[1] = new NbVector4(tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z, 1.0f);
            verts[2] = new NbVector4(tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z, 1.0f);
            verts[3] = new NbVector4(tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z, 1.0f);
            verts[4] = new NbVector4(tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z, 1.0f);
            verts[5] = new NbVector4(tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z, 1.0f);
            verts[6] = new NbVector4(tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z, 1.0f);
            verts[7] = new NbVector4(tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z, 1.0f);

            
            //Check if all points are outside one of the planes
            for (int p = 0; p < 6; p++)
            {
                //Check all 8 points
                int i;
                for (i = 0; i < 8; i++)
                {
                    if (NbVector4.Dot(_frustum[p], verts[i]) > 0.0f)
                        return true;
                }

            }

            return false;
        }


        public bool SphereVsFrustum(NbVector4 center, float radius)
        {
            float d = 0;
            for (int p = 0; p < 6; p++)
            {
                d = NbVector4.Dot(_frustum[p], center);
                if (d <= -radius)
                {
                    //Console.WriteLine("Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p,
                    //    x, y, z);
                    return false;
                }
            }
            return true;
        }

        public bool SphereVsFrustum(NbVector3 location, float radius)
        {
            return SphereVsFrustum(new NbVector4(location, 1.0f), radius);
        }

        public static bool VolumeVsFrustum(float x, float y, float z, float width, float height, float length)
        {
            /* TO BE REPAIRED
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            */
            return true;
        }

        public bool VolumeVsFrustum(NbVector3 location, float width, float height, float length)
        {
            return VolumeVsFrustum(location.X, location.Y, location.Z, width, height, length);
        }

        public bool CubeVsFrustum(float x, float y, float z, float size)
        {
            /*
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            */
            return true;
        }

        public float distanceFromPlane(int id, NbVector4 point)
        {
            return NbVector4.Dot(_frustum[id], point) / _frustum[id].Length;
        }


        public void CalculateFrustum(NbMatrix4 mvp)
        {
            //Front Plane
            _frustum[(int)ClippingPlane.Front] = new NbVector4(-mvp.M13, -mvp.M23, -mvp.M33, -mvp.M43);

            //Back Plane
            _frustum[(int)ClippingPlane.Back] = new NbVector4(mvp.M13 - mvp.M14, mvp.M23 - mvp.M24, mvp.M33 - mvp.M34,
                mvp.M43 - mvp.M44);

            //Left Plane
            _frustum[(int)ClippingPlane.Left] = new NbVector4(-mvp.M14 - mvp.M11, -mvp.M24 - mvp.M21,
                                                            -mvp.M34 - mvp.M31,
                                                            -mvp.M44 - mvp.M41);

            //Right Plane
            _frustum[(int)ClippingPlane.Right] = new NbVector4(mvp.M11 - mvp.M14, mvp.M21 - mvp.M24,
                                                             mvp.M31 - mvp.M34,
                                                             mvp.M41 - mvp.M44);

            //Top Plane
            _frustum[(int)ClippingPlane.Top] = new NbVector4(mvp.M12 - mvp.M14, mvp.M22 - mvp.M24,
                                                             mvp.M32 - mvp.M34,
                                                             mvp.M42 - mvp.M44);

            //Bottom Plane
            _frustum[(int)ClippingPlane.Bottom] = new NbVector4(  -mvp.M14 - mvp.M12,
                                                                -mvp.M24 - mvp.M22,
                                                                -mvp.M34 - mvp.M32,
                                                                -mvp.M44 - mvp.M42);

            //Invert everything to bring it to the original values
            for (int i = 0; i < 6; i++)
                _frustum[i] *= -1.0f;

            //Normalize planes (NOT SURE IF I NEED THAT)
            for (int i = 0; i < 6; i++)
                _frustum[i].Normalize();

            /*

            //Find Frustum Points by solving all the systems
            float[] p;
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Left, (int)ClippingPlane.Bottom);
            _frustum_points[0, 0] = p[0]; _frustum_points[0, 1] = p[1]; _frustum_points[0, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Left, (int)ClippingPlane.Top);
            _frustum_points[1, 0] = p[0]; _frustum_points[1, 1] = p[1]; _frustum_points[1, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Right, (int)ClippingPlane.Bottom);
            _frustum_points[2, 0] = p[0]; _frustum_points[2, 1] = p[1]; _frustum_points[2, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Right, (int)ClippingPlane.Top);
            _frustum_points[3, 0] = p[0]; _frustum_points[3, 1] = p[1]; _frustum_points[3, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Left, (int)ClippingPlane.Bottom);
            _frustum_points[4, 0] = p[0]; _frustum_points[4, 1] = p[1]; _frustum_points[4, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Left, (int)ClippingPlane.Top);
            _frustum_points[5, 0] = p[0]; _frustum_points[5, 1] = p[1]; _frustum_points[5, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Right, (int)ClippingPlane.Bottom);
            _frustum_points[6, 0] = p[0]; _frustum_points[6, 1] = p[1]; _frustum_points[6, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Right, (int)ClippingPlane.Top);
            _frustum_points[7, 0] = p[0]; _frustum_points[7, 1] = p[1]; _frustum_points[7, 2] = p[2];

            */
            
        }


        float[] solvePlaneSystem(int p1, int p2, int p3)
        {
            //Setup Matrix
            var A = MathNet.Numerics.LinearAlgebra.Matrix<float>.Build.DenseOfArray(new float[,]
            {
                { _frustum[p1].X, _frustum[p1].Y, _frustum[p1].Z },
                { _frustum[p2].X, _frustum[p2].Y, _frustum[p2].Z },
                { _frustum[p3].X, _frustum[p3].Y, _frustum[p3].Z }
            });

            //Setup Right Hand Side
            var b = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.Dense(new float[]
            { _frustum[p1].W, _frustum[p2].W, _frustum[p3].W });

            var x = A.Solve(b);

            float[] ret_x = new float[3];
            ret_x[0] = x[0];
            ret_x[1] = x[1];
            ret_x[2] = x[2];

            return ret_x;

        }

    }



}
