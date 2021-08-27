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
    public abstract class Model : Entity, IDisposable, INotifyPropertyChanged
    {
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
        public Scene parentScene;
        public List<Component> _components = new List<Component>();
        public int animComponentID;
        public int animPoseComponentID;
        public int actionComponentID;

        //LOD
        public float[] _LODDistances = new float[5];
        public int _LODNum = 1; //Default value of 1 LOD per model

        //Disposable Stuff
        public bool disposed = false;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

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
                List<float> l = new List<float>();
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

        public abstract Model Clone();

        public virtual void updateLODDistances()
        {
            foreach (Entity s in Children)
                s.updateLODDistances();
        }

        public virtual void setupSkinMatrixArrays()
        {
            foreach (Entity s in Children)
                s.setupSkinMatrixArrays();
        }

        public virtual void updateMeshInfo(bool lod_filter = false)
        {
            foreach (Entity child in Children)
            {
                child.updateMeshInfo(lod_filter);
            }
        }

        public virtual void update()
        {
            //All transform updates should happen from the transformation system
        }

        public void AddChild(Model m)
        {
            Children.Add(m);
            m.parent = this;
        }

        //TODO: Consider converting all such attributes using properties
        
        public void init(float[] trans)
        {
            TransformData td = (GetComponent<TransformComponent>() as TransformComponent).Data;

            td.TransX = trans[0];
            td.TransY = trans[1];
            td.TransZ = trans[2];
            td.RotX = trans[3];
            td.RotY = trans[4];
            td.RotZ = trans[5];
            td.ScaleX = trans[6];
            td.ScaleY = trans[7];
            td.ScaleZ = trans[8];


            //Set Original positions
            td.StoreAsOldTransform();

        }

        //Default Constructor
        protected Model()
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


            //Component Init

            //Add TransformComponent
            TransformationSystem.AddTransformComponentToEntity(this);
            
            _components = new List<Component>();
            animComponentID = -1;
            animPoseComponentID = -1;
            actionComponentID = -1;
        }

        private void catchPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            update();
        }


        public virtual void copyFrom(Model input)
        {
            CopyFrom(input);
            
            renderable = input.renderable; //Override Renderability
            debuggable = input.debuggable;
            selected = 0;
            //MESHVAO AND INSTANCE IDS SHOULD BE HANDLED EXPLICITLY

            //Clone LOD Info
            _LODNum = input._LODNum;
            for (int i = 0; i < 5; i++)
                this._LODDistances[i] = input._LODDistances[i];

            //Component Stuff
            animComponentID = input.animComponentID;
            animPoseComponentID = input.animPoseComponentID;

        }

        //Copy Constructor
        public Model(Model input)
        {
            this.copyFrom(input);
            foreach (Entity child in input.Children)
            {
                Entity nChild = child.Clone();
                nChild.Parent = this;
                Children.Add(nChild);
            }
        }

        //NMSTEmplate Export
        
        public virtual TkSceneNodeData ExportTemplate(bool keepRenderable)
        {
            //Copy main info
            TkSceneNodeData cpy = new TkSceneNodeData();

            cpy.Transform = nms_template.Transform;
            cpy.Attributes = nms_template.Attributes;
            cpy.Type = nms_template.Type;
            cpy.Name = nms_template.Name;
            cpy.NameHash = nms_template.NameHash;

            if (Children.Count > 0)
                cpy.Children = new List<TkSceneNodeData>();

            foreach (Entity child in Children)
            {
                if (!child.renderable && keepRenderable)
                    continue;
                else if (child.nms_template != null)
                    cpy.Children.Add(child.ExportTemplate(keepRenderable));
            }

            return cpy;
        }



        #region ComponentQueries
        public int hasComponent(Type ComponentType)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                Component temp = _components[i];
                if (temp.GetType() == ComponentType)
                    return i;
            }

            return -1;
        }

        #endregion


        #region AnimationComponent

        public virtual void setParentScene(Scene scene)
        {
            parentScene = scene;
            foreach (Model child in children)
            {
                child.setParentScene(scene);
            }
        }

        #endregion

        #region AnimPoseComponent
        //TODO: It would be nice if I didn't have to do make the method public, but it needs a lot of work on the 
        //AnimPoseComponent class to temporarily store the selected pose frames, while also in the model.update method

        //Locator Animation Stuff

        public Dictionary<string, Matrix4> loadPose()
        {

            if (animPoseComponentID < 0)
                return new Dictionary<string, Matrix4>();

            AnimPoseComponent apc = _components[animPoseComponentID] as AnimPoseComponent;
            Dictionary<string, Matrix4> posematrices = new Dictionary<string, Matrix4>();

            foreach (TkAnimNodeData node in apc._poseFrameData.NodeData)
            {
                List<Quaternion> quats = new List<Quaternion>();
                List<Vector3> translations = new List<Vector3>();
                List<Vector3> scales = new List<Vector3>();

                //We should interpolate frame shit over all the selected Pose Data

                //Gather all the transformation data for all the pose factors
                for (int i = 0; i < apc._poseData.Count; i++)
                //for (int i = 0; i < 1; i++)
                {
                    //Get Pose Frame
                    int poseFrameIndex = apc._poseData[i].PActivePoseFrame;

                    Vector3 v_t, v_s;
                    Quaternion lq;
                    //Fetch Rotation Quaternion
                    lq = NMSUtils.fetchRotQuaternion(node, apc._poseFrameData, poseFrameIndex);
                    v_t = NMSUtils.fetchTransVector(node, apc._poseFrameData, poseFrameIndex);
                    v_s = NMSUtils.fetchScaleVector(node, apc._poseFrameData, poseFrameIndex);

                    quats.Add(lq);
                    translations.Add(v_t);
                    scales.Add(v_s);
                }

                float fact = 1.0f / quats.Count;
                Quaternion fq = new Quaternion();
                Vector3 f_vt = new Vector3();
                Vector3 f_vs = new Vector3();


                fq = quats[0];
                f_vt = translations[0];
                f_vs = scales[0];

                //Interpolate all data
                for (int i = 1; i < quats.Count; i++)
                {
                    //Method A: Interpolate
                    //Quaternion.Slerp(fq, quats[i], 0.5f);
                    //Vector3.Lerp(f_vt, translations[i], 0.5f);
                    //Vector3.Lerp(f_vs, scales[i], 0.5f);

                    //Addup
                    f_vs *= scales[i];
                }

                //Generate Transformation Matrix
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq) * Matrix4.CreateTranslation(f_vt);
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq);
                Matrix4 poseMat = Matrix4.CreateScale(f_vs);
                posematrices[node.Node] = poseMat;

            }

            return posematrices;

        }

        public virtual void applyPoses(Dictionary<string, Matrix4> poseMatrices)
        {

        }


        #endregion

        public ICommand ResetTransform
        {
            get { return new ResetTransformCommand(); }
        }

        private void resetTransform()
        {
            TransformData td = TransformationSystem.GetEntityTransformData(this);
            td.ResetTransform();
            
            //Save values to underlying SceneNode
            if (nms_template != null)
            {
                nms_template.Transform.ScaleX = td.ScaleX;
                nms_template.Transform.ScaleY = td.ScaleY;
                nms_template.Transform.ScaleZ = td.ScaleZ;
                nms_template.Transform.TransX = td.TransX;
                nms_template.Transform.TransY = td.TransY;
                nms_template.Transform.TransZ = td.TransZ;
                //Convert rotation from matrix to angles
                //Vector3 q_euler = quaternionToEuler(oldrotation);
                Matrix4 tempMat = Matrix4.CreateFromQuaternion(td.localRotation);
                tempMat.Transpose();
                Vector3 q_euler = MathUtils.matrixToEuler(tempMat, "ZXY");

                nms_template.Transform.RotX = q_euler.X;
                nms_template.Transform.RotY = q_euler.Y;
                nms_template.Transform.RotZ = q_euler.Z;

            }
        }

        public ICommand ExportToMBIN
        {
            get { return new ExportToMBINCommand(); }
        }

        //Export to MBIN
        private void exportToMBIN()
        {
            if (nms_template != null)
            {
                //Fetch scene name
                string[] split = nms_template.Name.Value.Split('\\');
                string scnName = split[split.Length - 1];

                TkSceneNodeData temp = ExportTemplate(true);
                temp.WriteToMbin(scnName.ToUpper() + ".SCENE.MBIN");
                Common.Callbacks.showInfo("Scene successfully exported to " + scnName.ToUpper() + ".MBIN", "Info");
            }
        }

        
        public ICommand ExportToEXML
        {
            get { return new ExportToEXMLCommand(); }
        }


        //Export to EXML
        private void exportToEXML()
        {
            if (nms_template != null)
            {
                //Fetch scene name
                string[] split = nms_template.Name.Value.Split('\\');
                string scnName = split[split.Length - 1];

                TkSceneNodeData temp = ExportTemplate(true);

                temp.WriteToExml(scnName + ".SCENE.EXML");
                Common.Callbacks.showInfo("Scene successfully exported to " + scnName + ".exml", "Info");
            }
        }

        //ICommands

        private class ResetTransformCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                Model m = (Model) parameter;
                m.resetTransform();
            }
        }

        private class ExportToMBINCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                Model m = (Model)parameter;
                m.exportToMBIN();
            }
        }

        private class ExportToEXMLCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                Model m = (Model)parameter;
                m.exportToEXML();
            }
        }



        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                if (children != null)
                    foreach (Model c in children) c.Dispose();
                children.Clear();

                //Free textureManager
            }

            //Free unmanaged resources

            disposed = true;
        }

#if DEBUG
        ~Model()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + Type.ToString());
        }
#endif


    }

}
