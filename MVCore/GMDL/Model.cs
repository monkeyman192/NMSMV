using System;
using System.Collections.Generic;
using System.ComponentModel;
using OpenTK;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;
using System.Collections.ObjectModel;
using MVCore.Utils;
using MVCore.Systems;
using System.Linq;
using System.Windows.Input;

namespace MVCore
{
    public abstract class Model : INotifyPropertyChanged
    {
        public int ID;
        public bool renderable; //Used to toggle visibility from the UI
        public bool active; //Used internally
        public bool occluded; //Used by the occluder
        public bool debuggable;
        public int selected;
        //public GLSLHelper.GLSLShaderConfig[] shader_programs;
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag; //This is used to define procgen usage
        public TkSceneNodeData nms_template;
        //public GLMeshVao meshVao;
        public int instanceId = -1;
        public int VolumeinstanceId = -1;
        
        //Components
        public SceneGraphNode parentScene;
        
        //LOD
        public float[] _LODDistances = new float[5];
        public int _LODNum = 1; //Default value of 1 LOD per model

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }


        //Properties
        public List<float> LODDistances
        {
            get
            {
                List<float> l = new();
                for (int i = 0; i < _LODDistances.Length; i++)
                {
                    if (_LODDistances[i] > 0)
                        l.Add(_LODDistances[i]);
                }
                return l;
            }
        }

        

        //Methods
        public virtual void recalculateAABB()
        {

        }

        /*
        public virtual void recalculateAABB()
        {
            //Revert back to the original values
            _AABBMIN = __AABBMIN;
            _AABBMAX = __AABBMAX;

            //Generate all 8 points from the AABB
            List<Vector4> vecs = new List<Vector4>
            {
                new Vector4(_AABBMIN.X, _AABBMIN.Y, _AABBMIN.Z, 1.0f),
                new Vector4(_AABBMAX.X, _AABBMIN.Y, _AABBMIN.Z, 1.0f),
                new Vector4(_AABBMIN.X, _AABBMAX.Y, _AABBMIN.Z, 1.0f),
                new Vector4(_AABBMAX.X, _AABBMAX.Y, _AABBMIN.Z, 1.0f),

                new Vector4(_AABBMIN.X, _AABBMIN.Y, _AABBMAX.Z, 1.0f),
                new Vector4(_AABBMAX.X, _AABBMIN.Y, _AABBMAX.Z, 1.0f),
                new Vector4(_AABBMIN.X, _AABBMAX.Y, _AABBMAX.Z, 1.0f),
                new Vector4(_AABBMAX.X, _AABBMAX.Y, _AABBMAX.Z, 1.0f)
            };

            //Transform all Vectors using the worldMat
            for (int i = 0; i < 8; i++)
                vecs[i] = vecs[i] * TransformationSystem.GetEntityWorldMat(this);

            //Init vectors to max
            _AABBMIN = new Vector3(float.MaxValue);
            _AABBMAX = new Vector3(float.MinValue);

            //Align values
            for (int i = 0; i < 8; i++)
            {
                _AABBMIN.X = Math.Min(_AABBMIN.X, vecs[i].X);
                _AABBMIN.Y = Math.Min(_AABBMIN.Y, vecs[i].Y);
                _AABBMIN.Z = Math.Min(_AABBMIN.Z, vecs[i].Z);

                _AABBMAX.X = Math.Max(_AABBMAX.X, vecs[i].X);
                _AABBMAX.Y = Math.Max(_AABBMAX.Y, vecs[i].Y);
                _AABBMAX.Z = Math.Max(_AABBMAX.Z, vecs[i].Z);
            }
        }
        */

        /*
        public bool intersects(Vector3 ray_start, Vector3 ray, ref float distance)
        {
            //Calculate bound box center
            float radius = 0.5f * (AABBMIN - AABBMAX).Length;
            Vector3 bsh_center = AABBMIN + 0.5f * (AABBMAX - AABBMIN);

            //Move sphere to object's root position
            bsh_center = (new Vector4(bsh_center, 1.0f)).Xyz;

            //Calculate factors of the point equation
            float a = ray.LengthSquared;
            float b = 2.0f * Vector3.Dot(ray, ray_start - bsh_center);
            float c = (ray_start - bsh_center).LengthSquared - radius * radius;

            float D = b * b - 4 * a * c;

            if (D >= 0.0f)
            {
                //Make sure that the calculated l is positive so that intersections are
                //checked only forward
                float l2 = (-b + (float)Math.Sqrt(D)) / (2.0f * a);
                float l1 = (-b - (float)Math.Sqrt(D)) / (2.0f * a);

                if (l2 > 0.0f || l1 > 0.0f)
                {
                    float d = (float)Math.Min((ray * l1).Length, (ray * l2).Length);

                    if (d < distance)
                    {
                        distance = d;
                        return true;
                    }
                }
            }

            return false;
        }

        */

        
        public virtual void setupSkinMatrixArrays()
        {
            //REWRITE
            //foreach (Model s in Children)
            //    s.setupSkinMatrixArrays();
        }

        public virtual void updateMeshInfo(bool lod_filter = false)
        {
            //All Mesh Updates should happen from the Mesh Management System
        }

        public virtual void update()
        {
            //All transform updates should happen from the transformation system
        }

        //TODO: Consider converting all such attributes using properties
        
        //Default Constructor
        protected Model() : base()
        {
            renderable = true;
            active = true;
            debuggable = false;
            occluded = false;
            selected = 0;
            //TODO: DIRTYFIX, When model will be completely replaced by the Entity class
            //and objects will be registered by the entity registration system, this should be removed
            ID = Common.RenderState.itemCounter;
            Common.RenderState.itemCounter++;
            procFlag = false;    //This is used to define procgen usage

            
        }

        private void catchPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            update();
        }


        public virtual void copyFrom(Model input)
        {
            renderable = input.renderable; //Override Renderability
            debuggable = input.debuggable;
            selected = 0;
            //MESHVAO AND INSTANCE IDS SHOULD BE HANDLED EXPLICITLY

            //Clone LOD Info
            _LODNum = input._LODNum;
            for (int i = 0; i < 5; i++)
                this._LODDistances[i] = input._LODDistances[i];
        }

        //Export to MBIN
        
        
        //Export to EXML
        private void exportToEXML()
        {
            if (nms_template != null)
            {
                //Fetch scene name
                string[] split = nms_template.Name.Value.Split('\\');
                string scnName = split[^1];
                
                //Todo Repair Export to EXML
                TkSceneNodeData temp = new();
                
                temp.WriteToExml(scnName + ".SCENE.EXML");
                Common.Callbacks.showInfo("Scene successfully exported to " + scnName + ".exml", "Info");
            }
        }

        


    }

}
