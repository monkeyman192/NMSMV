using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Systems;
using MVCore.Utils;
using MVCore.Common;
using OpenTK.Graphics.OpenGL4;

namespace MVCore
{
    public enum ATTENUATION_TYPE
    {
        QUADRATIC = 0x0,
        CONSTANT,
        LINEAR,
        COUNT
    }

    public enum LIGHT_TYPE
    {
        POINT = 0x0,
        SPOT,
        COUNT
    }


       
    public class Light : Model
    {
        //Light Mesh
        public GLInstancedMeshVao meshVao;
        //Light Volume Mesh
        public GLInstancedLightMeshVao VolumeMeshVao;

        //Exposed Light Properties
        public Vector3 Color;
        public Vector3 Direction;
        public float FOV = 360.0f;
        public float Intensity = 1.0f;
        public bool IsRenderable = true;
        public ATTENUATION_TYPE Falloff;
        public LIGHT_TYPE LightType;
        
        public bool updated = false; //Used to prevent unecessary uploads to the UBO

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;

        //Light Falloff Radius
        public float radius;

        //Properties
        
        public Light()
        {
            Type = TYPES.LIGHT;
            Color = new Vector3(1.0f, 1.0f, 1.0f);
            Direction = new Vector3(0.0f, 0.0f, -1.0f);
            FOV = 360;
            Intensity = 1.0f;
            Falloff = ATTENUATION_TYPE.CONSTANT;
            LightType = LIGHT_TYPE.POINT;

            //Initialize new MeshVao
            meshVao = new GLInstancedMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this); // Add instance

            //Register volume meshvao
            VolumeMeshVao = RenderState.activeResMgr.GLPrimitiveMeshVaos["default_light_sphere"] as GLInstancedLightMeshVao;
            VolumeinstanceId = RenderState.activeResMgr.lightBufferMgr.AddInstance(ref VolumeMeshVao, this);
            
            //Init projection Matrix
            lightProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathUtils.radians(90), 1.0f, 1.0f, 300f);

            //Init lightSpace Matrices
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
            {
                lightSpaceMatrices[i] = Matrix4.Identity * lightProjectionMatrix;
            }

            //Catch changes to MVector from the UI
            Color = new Vector3(1.0f);
        }

        protected Light(Light input) : base(input)
        {
            Color = input.Color;
            Direction = input.Direction;
            Intensity = input.Intensity;
            Falloff = input.Falloff;
            FOV = input.FOV;
            LightType = input.LightType;
            
            //Initialize new MeshVao
            meshVao = new GLInstancedMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this); //Add instance

            //Register volume meshvao
            VolumeMeshVao = RenderState.activeResMgr.GLPrimitiveMeshVaos["default_light_sphere"] as GLInstancedLightMeshVao;
            VolumeinstanceId = RenderState.activeResMgr.lightBufferMgr.AddInstance(ref VolumeMeshVao, this);

            //Copy Matrices
            lightProjectionMatrix = input.lightProjectionMatrix;
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
                lightSpaceMatrices[i] = input.lightSpaceMatrices[i];

            RenderState.activeResMgr.GLlights.Add(this);
        }

        public override void updateMeshInfo(bool lod_filter = false)
        {
            if (lod_filter)
            {
                base.updateMeshInfo();
                RenderStats.occludedNum += 1;
                return;
            } 
            
            if (renderable)
            {
                Vector4 worldPosition = TransformationSystem.GetEntityWorldPosition(this);
                //Generate matrices for the volume mesh
                Matrix4 scaleMat = Matrix4.Identity;
                Matrix4 tMat = Matrix4.CreateTranslation(worldPosition.Xyz);
                Matrix4 sMat = Matrix4.CreateScale(radius);
                VolumeinstanceId = RenderState.activeResMgr.lightBufferMgr.AddInstance(ref VolumeMeshVao, this, sMat * tMat); //Add instance
                
                if (RenderState.settings.viewSettings.ViewLights)
                {
                    //End Point
                    Vector4 ep;
                    //Lights with 360 FOV are points
                    if (Math.Abs(FOV - 360.0f) <= 1e-4)
                    {
                        ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                        LightType = LIGHT_TYPE.POINT;
                    }
                    else
                    {
                        ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                        LightType = LIGHT_TYPE.SPOT;
                    }

                    ep = ep * Matrix4.CreateFromQuaternion(TransformationSystem.GetEntityLocalRotation(this));
                    Direction = ep.Xyz; //Set spotlight direction
                
                    //Update Vertex Buffer based on the new data
                    float[] verts = new float[6];
                    int arraysize = 6 * sizeof(float);

                    //Origin Point
                    verts[0] = worldPosition.X;
                    verts[1] = worldPosition.Y;
                    verts[2] = worldPosition.Z;

                    ep.X += worldPosition.X;
                    ep.Y += worldPosition.Y;
                    ep.Z += worldPosition.Z;

                    verts[3] = ep.X;
                    verts[4] = ep.Y;
                    verts[5] = ep.Z;

                    GL.BindVertexArray(meshVao.vao.vao_id);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                    //Add verts data, color data should stay the same
                    GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);

                    //Uplod worldMat to the meshVao
                    instanceId = GLMeshBufferManager.AddInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity); //Add instance
                    
                }
            }

            base.updateMeshInfo();

        }

        public override void update()
        {
            base.update();

            Matrix4 localRotation = Matrix4.CreateFromQuaternion(TransformationSystem.GetEntityLocalRotation(this));
            Vector3 worldPosition = TransformationSystem.GetEntityWorldPosition(this).Xyz;

            //End Point
            Vector4 ep;
            //Lights with 360 FOV are points
            if (Math.Abs(FOV - 360.0f) <= 1e-4)
            {
                ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                ep = ep * localRotation;
                LightType = LIGHT_TYPE.POINT;
            }
            else
            {
                ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                ep = ep * localRotation;
                LightType = LIGHT_TYPE.SPOT;
            }

            ep.Normalize();

            Direction = ep.Xyz; //Set spotlight direction
            
            //Assume that this is a point light for now
            //Right
            lightSpaceMatrices[0] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Left
            lightSpaceMatrices[1] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(-1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Up
            lightSpaceMatrices[2] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, -1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Down
            lightSpaceMatrices[3] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Near
            lightSpaceMatrices[4] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, 1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Far
            lightSpaceMatrices[5] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, -1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));

            //Calculate Light radius
            float lm_per_watt = 683; //Assume LED lamp
            float eff_intensity = Intensity / lm_per_watt;
            float cutoff = 0.95f;
            
            switch (Falloff)
            {
                case ATTENUATION_TYPE.QUADRATIC:
                    {
                        //radius = (float) Math.Sqrt(_intensity * (1.0f - cutoff) / cutoff);
                        radius = (float) 3.3f * (float) Math.Pow(Intensity, 0.35f);
                        //3.30\cdot x^{ 0.33}\ 
                        break;
                    }
                case ATTENUATION_TYPE.LINEAR:
                    {
                        radius = Intensity * (1.0f - cutoff) / cutoff;
                        break;
                    }
                case ATTENUATION_TYPE.CONSTANT:
                    {
                        radius = 1000.0f;
                        break;
                    }
                    
            }
            
        }

        
        //Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                VolumeMeshVao = null; //VAO will be deleted from the resource manager since it is a common mesh
                base.Dispose(true);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }


    public class LightGLMeshVao : GLInstancedMeshVao
    {
        //Class static properties
        public new const int MAX_INSTANCES = 1024;
        
        //Constructor
        public LightGLMeshVao() : base()
        {
            
        }

        public LightGLMeshVao(MeshMetaData data) :base(data)
        {
            
        }

        //Rendering Methods

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    base.Dispose(disposing);
                    //TODO: dispose extra stuff here
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~mainGLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public new void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }



}
