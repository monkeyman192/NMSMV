using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Mathematics;
using MVCore.Utils;
using MVCore.Common;
using OpenTK.Graphics.OpenGL4;

namespace MVCore.GMDL
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
        //Underlying struct
        public GLLight _strct;

        //Light Mesh
        public GLInstancedMeshVao meshVao;
        //Light Volume Mesh
        public GLInstancedLightMeshVao VolumeMeshVao;

        //Exposed Light Properties
        public MVector3 _color;
        public MVector3 _direction;
        public float _fov = 360.0f;
        public float _intensity = 1.0f;
        public ATTENUATION_TYPE _falloff;
        public LIGHT_TYPE _lightType;
        
        public bool updated = false; //Used to prevent unecessary uploads to the UBO

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;

        //Light Falloff Radius
        public float radius;

        //Properties
        public MVector3 Color
        {
            get
            {
                return _color;
            }
            set
            {
                
                _color = value;
                //TODO: Check if this is called and make sure to properly add the
                //property changed method
            }
        }

        public float FOV
        {
            get
            {
                return _fov;
            }

            set
            {
                _fov = value;
                _strct.fov = MathUtils.radians(_fov);
            }
        }

        public float Intensity
        {
            get
            {
                return _intensity;
            }

            set
            {
                _intensity = value;
                _strct.intensity = value;
            }
        }

        public string Attenuation
        {
            get
            {
                return _falloff.ToString();
            }

            set
            {
                bool parse_status = Enum.TryParse<ATTENUATION_TYPE>(value, out _falloff);
                if (!parse_status)
                    throw new Exception("Unsupported attenuation type");
                _strct.falloff = (int) _falloff;
            }
        }

        public override bool IsRenderable
        {
            get
            {
                return renderable;
            }

            set
            {
                _strct.isRenderable = value ? 1.0f : 0.0f;
                base.IsRenderable = value;
            }
        }

        
        public Light()
        {
            type = TYPES.LIGHT;
            _color = new MVector3(1.0f, 1.0f, 1.0f);
            _direction = new MVector3(0.0f, 0.0f, -1.0f);
            _fov = 360;
            _intensity = 1.0f;
            _falloff = ATTENUATION_TYPE.CONSTANT;
            _lightType = LIGHT_TYPE.POINT;

            //Initialize new MeshVao
            meshVao = new GLInstancedMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this); // Add instance

            //Register volume meshvao
            VolumeMeshVao = RenderState.activeResMgr.GLPrimitiveMeshVaos["default_light_sphere"] as GLInstancedLightMeshVao;
            VolumeinstanceId = RenderState.activeResMgr.lightBufferMgr.addInstance(ref VolumeMeshVao, this);
            
            //Init projection Matrix
            lightProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathUtils.radians(90), 1.0f, 1.0f, 300f);

            //Init lightSpace Matrices
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
            {
                lightSpaceMatrices[i] = Matrix4.Identity * lightProjectionMatrix;
            }

            //Catch changes to MVector from the UI
            Color = new MVector3(1.0f);
            Color.PropertyChanged += catchPropertyChanged;
            localPosition.PropertyChanged += catchPropertyChanged;
        }

        private void catchPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            update();
            
            //Save Color to struct
            _strct.color.X = Color.X;
            _strct.color.Y = Color.Y;
            _strct.color.Z = Color.Z;

            //Save Position to struct
            _strct.position.X = worldPosition.X;
            _strct.position.Y = worldPosition.Y;
            _strct.position.Z = worldPosition.Z;

        }

        protected Light(Light input) : base(input)
        {
            Color = input.Color;
            _intensity = input._intensity;
            _falloff = input._falloff;
            _direction = input._direction;
            _fov = input._fov;
            _lightType = input._lightType;
            _strct = input._strct;

            //Initialize new MeshVao
            meshVao = new GLInstancedMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this); //Add instance

            //Register volume meshvao
            VolumeMeshVao = RenderState.activeResMgr.GLPrimitiveMeshVaos["default_light_sphere"] as GLInstancedLightMeshVao;
            VolumeinstanceId = RenderState.activeResMgr.lightBufferMgr.addInstance(ref VolumeMeshVao, this);

            //Copy Matrices
            lightProjectionMatrix = input.lightProjectionMatrix;
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
                lightSpaceMatrices[i] = input.lightSpaceMatrices[i];

            update_struct();
            RenderState.activeResMgr.GLlights.Add(this);
        }

        public override void updateMeshInfo(bool lod_filter = false)
        {
            if (lod_filter)
            {
                _strct.isRenderable = 0.0f; //Force not renderable
                base.updateMeshInfo();
                RenderStats.occludedNum += 1;
                return;
            } 
            
            if (renderable)
            {
                //Generate matrices for the volume mesh
                Matrix4 scaleMat = Matrix4.Identity;
                Matrix4 tMat = Matrix4.CreateTranslation(worldPosition);
                Matrix4 sMat = Matrix4.CreateScale(radius);
                VolumeinstanceId = RenderState.activeResMgr.lightBufferMgr.addInstance(ref VolumeMeshVao, this, sMat * tMat); //Add instance
                
                if (RenderState.renderViewSettings.RenderLights)
                {
                    //End Point
                    Vector4 ep;
                    //Lights with 360 FOV are points
                    if (Math.Abs(FOV - 360.0f) <= 1e-4)
                    {
                        ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                        _lightType = LIGHT_TYPE.POINT;
                    }
                    else
                    {
                        ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                        _lightType = LIGHT_TYPE.SPOT;
                    }

                    ep = ep * _localRotation;
                    _direction.vec = ep.Xyz; //Set spotlight direction
                    update_struct();

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
                    instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity); //Add instance
                    
                }
            }

            base.updateMeshInfo();

        }

        public override void update()
        {
            base.update();

            //End Point
            Vector4 ep;
            //Lights with 360 FOV are points
            if (Math.Abs(FOV - 360.0f) <= 1e-4)
            {
                ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                ep = ep * _localRotation;
                _lightType = LIGHT_TYPE.POINT;
            }
            else
            {
                ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                ep = ep * _localRotation;
                _lightType = LIGHT_TYPE.SPOT;
            }

            ep.Normalize();

            _direction.vec = ep.Xyz; //Set spotlight direction
            update_struct();

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
            float eff_intensity = _intensity / lm_per_watt;
            float cutoff = 0.95f;
            
            switch (_falloff)
            {
                case ATTENUATION_TYPE.QUADRATIC:
                    {
                        //radius = (float) Math.Sqrt(_intensity * (1.0f - cutoff) / cutoff);
                        radius = (float) 3.3f * (float) Math.Pow(_intensity, 0.35f);
                        //3.30\cdot x^{ 0.33}\ 
                        break;
                    }
                case ATTENUATION_TYPE.LINEAR:
                    {
                        radius = _intensity * (1.0f - cutoff) / cutoff;
                        break;
                    }
                case ATTENUATION_TYPE.CONSTANT:
                    {
                        radius = 1000.0f;
                        break;
                    }
                    
            }
            

        }

        public void update_struct()
        {
            _strct.position = (new Vector4(worldPosition, 1.0f) * RenderState.rotMat).Xyz;
            _strct.isRenderable = renderable ? 1.0f : 0.0f;
            _strct.color = Color.Vec;
            _strct.intensity = _intensity; //Assume LED lamp
            _strct.direction = _direction.vec;
            _strct.fov = (float) Math.Cos(MathUtils.radians(_fov));
            _strct.falloff = (int) _falloff;
            _strct.type = (_lightType == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f;
            _strct.radius = radius;
        }

        public override Model Clone()
        {
            return new Light(this);
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
