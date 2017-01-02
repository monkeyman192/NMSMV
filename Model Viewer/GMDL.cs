﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using KUtility;
using Model_Viewer;
using System.Xml;

namespace GMDL
{
    public abstract class model: IDisposable
    {
        public abstract bool render(int pass);
        public abstract GMDL.model Clone(GMDL.scene scn);
        public GMDL.scene scene;
        public GLControl pcontrol;
        public bool renderable = true;
        public bool debuggable = false;
        public int selected = 0;
        public int[] shader_programs;
        public int ID;
        public TYPES type;
        public string name = "";
        public GMDL.Material material;
        public List<model> children = new List<model>();
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag = false; //This is used to define procgen usage

        //Animation Stuff
        public float[] JMArray = (float[]) Util.JMarray.Clone();
        public List<GMDL.Joint> jointModel = new List<GMDL.Joint>();
        public Dictionary<string, model> jointDict = new Dictionary<string, model>();
        public AnimeMetaData animMeta = null;
        public int frameCounter = 0;
        
        //Transformation Parameters
        public Vector3 worldPosition {
            get
            {
                if (parent != null)
                {
                    //Original working
                    //return parent.worldPosition + Vector3.Transform(this.localPosition, parent.worldMat);
                    
                    //Add Translation as well
                    return (Vector4.Transform(new Vector4(0.0f,0.0f,0.0f,1.0f), this.worldMat)).Xyz;
                }
                    
                else
                    return this.localPosition;
            }
        }
        public Matrix4 worldMat
        {
            get
            {
                if (parent != null)
                {
                    //Original working
                    return this.localMat * parent.worldMat;
                    //return this.localMat;
                }

                else
                    return this.localMat;
            }
        }
        public Matrix4 localMat
        {
            get
            {
                //Combine localRotation and Position to return the localMatrix
                Matrix4 rot = Matrix4.Identity;
                rot.M11 = localRotation.M11;
                rot.M12 = localRotation.M12;
                rot.M13 = localRotation.M13;
                rot.M21 = localRotation.M21;
                rot.M22 = localRotation.M22;
                rot.M23 = localRotation.M23;
                rot.M31 = localRotation.M31;
                rot.M32 = localRotation.M32;
                rot.M33 = localRotation.M33;
                //Create scaling matrix
                Matrix4 scale = Matrix4.Identity;
                scale[0, 0] = localScale[0];
                scale[1, 1] = localScale[1];
                scale[2, 2] = localScale[2];

                return scale * rot * Matrix4.CreateTranslation(localPosition);
            }
        }
        public Vector3 localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 localScale = new Vector3(1.0f, 1.0f, 1.0f);
        //public Vector3 localRotation = new Vector3(0.0f, 0.0f, 0.0f);
        public Matrix3 localRotation = Matrix3.Identity;
        public Vector3[] Bbox = new Vector3[2];

        public model parent;
        public int cIndex = 0;
        //Disposable Stuff
        public bool disposed = false;
        Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        public static void vectofloatArray(float[] flist, List<Vector3> veclist)
        {
            int count = veclist.Count;
            for (int i = 0; i < count; i++)
            {
                flist[3 * i] = veclist[i].X;
                flist[3 * i+1] = veclist[i].Y;
                flist[3 * i+2] = veclist[i].Z;
            }   
        }

        public void init(string trans)
        {
            //Get Local Position
            string[] split = trans.Split(',');
            Vector3 rotation;
            this.localPosition.X = float.Parse(split[0], System.Globalization.CultureInfo.InvariantCulture);
            this.localPosition.Y = float.Parse(split[1], System.Globalization.CultureInfo.InvariantCulture);
            this.localPosition.Z = float.Parse(split[2], System.Globalization.CultureInfo.InvariantCulture);


            //using (System.IO.StreamWriter file =
            //new System.IO.StreamWriter(@"readtransformsGMDL.txt", true))
            //{
            //    file.WriteLine(String.Join(" ", new string[] { this.localPosition.X.ToString(),this.localPosition.Y.ToString(),this.localPosition.Z.ToString(),"INPUTS:",split[0],split[1],split[2]}));
            //}

            //Get Local Rotation
            //Quaternion qx = Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f),
            //    (float)Math.PI * float.Parse(split[3]) / 180.0f);
            //Quaternion qy = Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f),
            //    (float)Math.PI * float.Parse(split[4]) / 180.0f);
            //Quaternion qz = Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f),
            //    (float)Math.PI * float.Parse(split[5]) / 180.0f);

            Quaternion qx = Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f),
                float.Parse(split[3], System.Globalization.CultureInfo.InvariantCulture));
            Quaternion qy = Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f),
                float.Parse(split[4], System.Globalization.CultureInfo.InvariantCulture));
            Quaternion qz = Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f),
                float.Parse(split[5], System.Globalization.CultureInfo.InvariantCulture));

            //this.localRotation = qz * qx * qy;
            rotation.X = float.Parse(split[3], System.Globalization.CultureInfo.InvariantCulture);
            rotation.Y = float.Parse(split[4], System.Globalization.CultureInfo.InvariantCulture);
            rotation.Z = float.Parse(split[5], System.Globalization.CultureInfo.InvariantCulture);

            //Get Local Scale
            this.localScale.X = float.Parse(split[6], System.Globalization.CultureInfo.InvariantCulture);
            this.localScale.Y = float.Parse(split[7], System.Globalization.CultureInfo.InvariantCulture);
            this.localScale.Z = float.Parse(split[8], System.Globalization.CultureInfo.InvariantCulture);

            //Now Calculate the joint matrix;

            Matrix3 rotx, roty, rotz;
            Matrix3.CreateRotationX((float)Math.PI * rotation.X / 180.0f, out rotx);
            Matrix3.CreateRotationY((float)Math.PI * rotation.Y / 180.0f, out roty);
            Matrix3.CreateRotationZ((float)Math.PI * rotation.Z / 180.0f, out rotz);
            //Matrix4.CreateTranslation(ref this.localPosition, out transM);
            //Calculate local matrix
            this.localRotation = rotz*rotx*roty;

            //this.localMat = rotz * rotx * roty * transM;

            //Set paths
            if (parent!=null)
                this.cIndex = this.parent.children.Count;
        }

        public List<int> hpath()
        {
            List<int> list = new List<int>();

            list.Insert(0,cIndex); //Add current index
            GMDL.model recparent = parent;

            while (recparent != null)
            {
                list.Insert(0, recparent.cIndex);
                recparent = recparent.parent;
            }
                
            return list;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
                JMArray = null;
                jointDict = null;
                
                
                //Free other resources here
                if (children!=null)
                    foreach (model c in children) c.Dispose();
                children.Clear();
            }

            //Free unmanaged resources

            disposed = true;
        }

        //~model()
        //{
        //    Dispose(false);
        //}

        
        public void delete()
        {
            if (parent != null)
                parent.children.Remove(this);
        }

        //Animation Methods
        public void traverse_joints(List<GMDL.Joint> jlist)
        {
            foreach (GMDL.Joint j in jlist)
                traverse_joint(j);
        }

        private void traverse_joint(GMDL.Joint j)
        {
            jointDict[j.name] = j;
            Util.insertMatToArray(JMArray, j.jointIndex * 16, j.worldMat);
            foreach (model c in j.children)
                if (c.type == TYPES.JOINT)
                    traverse_joint((GMDL.Joint)c);
        }

        public void animate()
        {
            //Debug.WriteLine("Setting Frame Index {0}", frameIndex);
            GMDL.AnimNodeFrameData frame = new GMDL.AnimNodeFrameData();
            frame = animMeta.frameData.frames[frameCounter];

            foreach (GMDL.AnimeNode node in animMeta.nodeData.nodeList)
            {
                if (jointDict.ContainsKey(node.name))
                {
                    //Check if there is a rotation for that node
                    if (node.rotIndex < frame.rotations.Count - 1)
                        ((GMDL.model)jointDict[node.name]).localRotation = Matrix3.CreateFromQuaternion(frame.rotations[node.rotIndex]);

                    //Matrix4 newrot = Matrix4.CreateFromQuaternion(frame.rotations[node.rotIndex]);
                    if (node.transIndex < frame.translations.Count - 1)
                        ((GMDL.model)jointDict[node.name]).localPosition = frame.translations[node.transIndex];
                }
                //Debug.WriteLine("Node " + node.name+ " {0} {1} {2}",node.rotIndex,node.transIndex,node.scaleIndex);
            }

            //Update JMArrays
            foreach (GMDL.model joint in jointDict.Values)
            {
                GMDL.Joint j = (GMDL.Joint) joint;
                Util.insertMatToArray(JMArray, j.jointIndex * 16, j.worldMat);
            }
                
            
            frameCounter += 1;
            if (frameCounter >= animMeta.frameCount - 1)
                frameCounter = 0;
        }

        public void cloneJointDict(ref Dictionary<string, GMDL.model> jointdict, List<GMDL.Joint> jointlist)
        {
            foreach (GMDL.Joint j in jointlist)
                cloneJointPart(ref jointdict, j);
        }

        private void cloneJointPart(ref Dictionary<string,model> jointdict, GMDL.model joint)
        {
            if (joint.type==TYPES.JOINT) jointdict[joint.name] = joint;
            foreach (GMDL.model child in joint.children)
                cloneJointPart(ref jointdict, child);
        }

    }

    //public interface model{
    //    bool render();
    //    GMDL.model Clone();
    //    bool Renderable { get; set; }
    //    int ShaderProgram { set; get; }
    //    int Index { set; get; }
    //    string Type { set; get; }
    //    string Name { set; get; }
    //    GMDL.Material material { set; get; }
    //    List<model> Children { set; get; }
    //};
    
    public class scene : locator
    {
        public override model Clone(scene scn)
        {
            GMDL.scene copy = new GMDL.scene();
            copy.renderable = true; //Override Renderability
            copy.shader_programs = this.shader_programs;
            copy.type = this.type;
            copy.name = this.name;
            copy.ID = this.ID;
            copy.cIndex = this.cIndex;
            //Clone transformation
            copy.localPosition = this.localPosition;
            copy.localRotation = this.localRotation;
            copy.localScale = this.localScale;
            copy.scene = scn;

            //ANIMATION DATA
            copy.jointDict = new Dictionary<string, model>();
            copy.jointModel = new List<Joint>();
            copy.JMArray = (float[]) this.JMArray.Clone();
            foreach (GMDL.Joint j in this.jointModel)
                copy.jointModel.Add((GMDL.Joint) j.Clone(copy));

            copy.cloneJointDict(ref copy.jointDict, copy.jointModel);
            //When cloning scene objects the scene has the scn arguments as its parent scene
            //BUT children will have this copy as their scene
            //Clone Children as well
            foreach (GMDL.model child in this.children)
            {
                GMDL.model nChild = child.Clone(copy);
                nChild.parent = copy;
                copy.children.Add(nChild);
            }


            return (GMDL.model) copy;
        }

        //public override bool render()
        //{
        //    //Don't render shit here
        //    return true;
        //}
    }

    public class locator: model
    {
        //private Vector3[] verts;
        private float[] verts = new float[6*3];
        private float[] colors = new float[6 * 3];
        private Int32[] indices;
        //public bool renderable = true;
        int vertex_buffer_object;
        //int color_buffer_object;
        int element_buffer_object;
        //public int shader_program = -1;
        //this.type = "";
        //string name = "";
        //public int index;
        

        //Default Constructor
        public locator()
        {
            //Set type
            //this.type = "LOCATOR";
            //Assemble geometry in the constructor
            //X
            float vlen = 0.1f;
            verts = new float[6 * 3] { vlen, 0.0f, 0.0f,
                   -vlen, 0.0f, 0.0f,
                    0.0f, vlen, 0.0f,
                    0.0f, -vlen, 0.0f,
                    0.0f, 0.0f, vlen,
                    0.0f, 0.0f, -vlen};
            int b_size = verts.Length * sizeof(float) / 3;
            byte[] verts_b = new byte[b_size];
            
            Buffer.BlockCopy(verts, 0, verts_b, 0, b_size);
            //verts = new Vector3[6];
            //verts[0] = new Vector3(vlen, 0.0f, 0.0f);
            //verts[1] = new Vector3(-vlen, 0.0f, 0.0f);
            //verts[2] = new Vector3(0.0f, vlen, 0.0f);
            //verts[3] = new Vector3(0.0f, -vlen, 0.0f);
            //verts[4] = new Vector3(0.0f, 0.0f, vlen);
            //verts[5] = new Vector3(0.0f, 0.0f, -vlen);

            //Colors
            colors = new float[6 * 3] { 1.0f, 0.0f, 0.0f,
                    1.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 1.0f,
                    0.0f, 0.0f, 1.0f};

            //Indices
            indices = new Int32[2 * 3] {0, 1, 2, 3, 4, 5};
            
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * 6 * 3;
            GL.GenBuffers(1, out vertex_buffer_object);
            //GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
            //Add vert color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, colors);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) (sizeof(int) * 6), indices, BufferUsageHint.StaticDraw);
        }

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            verts = null;
            indices = null;
            colors = null;
            GL.DeleteBuffer(vertex_buffer_object);
            GL.DeleteBuffer(element_buffer_object);
            base.Dispose(disposing);
        }
        

        private void renderMain(int pass)
        {
            //Debug.WriteLine("Rendering Locator {0}", this.name);
            //Debug.WriteLine("Rendering VBO Object here");
            //VBO RENDERING
            GL.UseProgram(pass);

            int arraysize = sizeof(float) * 6 * 3;
            //Vertex attribute
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Color Attribute
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(1);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.PointSize(10.0f);
            
            //GL.DrawElements(PrimitiveType.Points, 6, DrawElementsType.UnsignedInt, this.indices);
            GL.DrawArrays(PrimitiveType.Lines, 0, 6);
            //Debug.WriteLine("Locator Object {2} vpos {0} cpos {1} prog {3}", vpos, cpos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vertex_buffer_object,this.color_buffer_object);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            
        }


        public override bool render(int pass)
        {

            if (this.renderable == false)
            {
                //Debug.WriteLine("Not Renderable Locator");
                return false;
            }

            int program = this.shader_programs[pass];

            switch (pass)
            {
                case 0:
                    renderMain(program);
                    break;
                default:
                    break;
            }
            

            return true;
        }

        public override GMDL.model Clone(GMDL.scene scn)
        {
            GMDL.locator copy = new GMDL.locator();
            copy.renderable = true; //Override Renderability
            copy.shader_programs = this.shader_programs;
            copy.type = this.type;
            copy.name = this.name;
            copy.ID = this.ID;
            copy.cIndex = this.cIndex;
            //Clone transformation
            copy.localPosition = this.localPosition;
            copy.localRotation = this.localRotation;
            copy.localScale = this.localScale;
            copy.scene = scn;
            //Simple Locator cloning is handled exactly the same way with sharedvbo
            //Clone Children as well
            foreach (GMDL.model child in this.children)
            {
                GMDL.model nChild = child.Clone(scn);
                nChild.parent = copy;
                copy.children.Add(nChild);
            }
                
            
            return (GMDL.model) copy;
        }
    }

    public class sharedVBO : model
    {
        public int vertrstart = 0;
        public int vertrend = 0;
        public int batchstart = 0;
        public int batchcount = 0;
        public int firstskinmat = 0;
        public int lastskinmat = 0;
        public int skinned = 1;
        //Accurate boneRemap
        public int[] BoneRemap;
        public customVBO vbo;
        public customVBO sph_vbo;

        public Vector3 color = new Vector3();
        //public bool renderable = true;
        //public int shader_program = -1;
        //public int index;
        
        //BSphere calculator
        public void setupBSphere()
        {
            //For now just setup the Bounding Sphere VBO
            Vector4 bsh_center = (new Vector4((Bbox[0] + Bbox[1])));
            bsh_center = 0.5f * bsh_center;
            bsh_center.W = 1.0f;

            float radius = (0.5f * (Bbox[1] - Bbox[0])).Length;

            //Create Sphere vbo
            sph_vbo = new Sphere(bsh_center.Xyz, radius).getVBO();
        }

        public float[] getBindRotMats
        {
            get
            {
                float[] jMats = new float[60 * 16];

                for (int i = 0; i < this.vbo.jointData.Count; i++)
                {
                    Matrix4 temp = Matrix4.CreateFromQuaternion(vbo.jointData[i].BindRotation);
                    jMats[i * 16] = temp.M11;
                    jMats[i * 16 + 1] = temp.M12;
                    jMats[i * 16 + 2] = temp.M13;
                    jMats[i * 16 + 3] = temp.M14;
                    jMats[i * 16 + 4] = temp.M21;
                    jMats[i * 16 + 5] = temp.M22;
                    jMats[i * 16 + 6] = temp.M23;
                    jMats[i * 16 + 7] = temp.M24;
                    jMats[i * 16 + 8] = temp.M31;
                    jMats[i * 16 + 9] = temp.M32;
                    jMats[i * 16 + 10] = temp.M33;
                    jMats[i * 16 + 11] = temp.M34;
                    //jMats[i * 16 + 12] = temp.M41;
                    //jMats[i * 16 + 13] = temp.M42;
                    //jMats[i * 16 + 14] = temp.M43;
                    //jMats[i * 16 + 15] = temp.M44;
                    jMats[i * 16 + 12] = 0.0f;
                    jMats[i * 16 + 13] = 0.0f;
                    jMats[i * 16 + 14] = 0.0f;
                    jMats[i * 16 + 15] = 1.0f;
                }

                return jMats;
            }
        }
        public float[] getBindTransMats
        {
            get
            {
                float[] trans = new float[60 * 3];

                for (int i = 0; i < this.vbo.jointData.Count; i++)
                {
                    Vector3 temp = vbo.jointData[i].BindTranslate;
                    trans[3 * i + 0] = temp.X;
                    trans[3 * i + 1] = temp.Y;
                    trans[3 * i + 2] = temp.Z;
                }

                return trans;
            }
        }

        public void renderBsphere(int pass)
        {
            GL.UseProgram(pass);

            //Render the sphere
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, sph_vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (sph_vbo.bufInfo[i] == null) continue;
                bufInfo buf = sph_vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, sph_vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Reset
            int loc;
            for (int i = 0; i < 64; i++)
            {
                loc = GL.GetUniformLocation(pass, "matflags[" + i.ToString() + "]");
                GL.Uniform1(loc, 0.0f);
            }

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, this.color);

            //Upload Light Flag
            loc = GL.GetUniformLocation(pass, "useLighting");
            GL.Uniform1(loc, 0.0f);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, sph_vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawRangeElements(PrimitiveType.Triangles, 0, sph_vbo.vCount,
            sph_vbo.iCount, sph_vbo.iType, IntPtr.Zero);

            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

        }

        public void renderBbox(int pass)
        {
            GL.UseProgram(pass);

            float [] verts = new float[] { Bbox[0].X, Bbox[0].Y, Bbox[0].Z,
                                           Bbox[1].X, Bbox[0].Y, Bbox[0].Z,
                                           Bbox[0].X, Bbox[1].Y, Bbox[0].Z,
                                           Bbox[1].X, Bbox[1].Y, Bbox[0].Z,

                                           Bbox[0].X, Bbox[0].Y, Bbox[1].Z,
                                           Bbox[1].X, Bbox[0].Y, Bbox[1].Z,
                                           Bbox[0].X, Bbox[1].Y, Bbox[1].Z,
                                           Bbox[1].X, Bbox[1].Y, Bbox[1].Z };

            float[] colors = new float[] { color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z};

            //Indices
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
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts.Length;
            int vb_bbox, eb_bbox;
            GL.GenBuffers(1, out vb_bbox);
            GL.GenBuffers(1, out eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2*arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
            //Add vert color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, colors);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            int loc;
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Reset
            for (int i = 0; i < 64; i++)
            {
                loc = GL.GetUniformLocation(pass, "matflags[" + i.ToString() + "]");
                GL.Uniform1(loc, 0.0f);
            }

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, this.color);
            

            //Upload Light Flag
            loc = GL.GetUniformLocation(pass, "useLighting");
            GL.Uniform1(loc, 0.0f);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts.Length,
                indices.Length, DrawElementsType.UnsignedInt , IntPtr.Zero);

            GL.DisableVertexAttribArray(0);


            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);
            
    }

        public virtual void renderMain(int pass)
        {
            GL.UseProgram(pass);
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (vbo.bufInfo[i] == null) continue;
                bufInfo buf = vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);
            
            for (int i = 0; i < material.materialflags.Count; i++)
                GL.Uniform1(loc + material.materialflags[i], 1.0f);
            
            //Upload BoneRemap Information
            loc = GL.GetUniformLocation(pass, "boneRemap");
            GL.Uniform1(loc, BoneRemap.Length, BoneRemap);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            float[] skinmats = Util.mulMatArrays(vbo.invBMats, scene.JMArray, 128);

            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 128, false, skinmats);

            //Upload Light Flag
            loc = GL.GetUniformLocation(pass, "useLighting");
            GL.Uniform1(loc, 1.0f);

            //Upload Selected Flag
            loc = GL.GetUniformLocation(pass, "selected");
            GL.Uniform1(loc, selected);

            //BIND TEXTURES
            int tex0Id = (int)TextureUnit.Texture0;
            //Diffuse Texture
            string test = "diffuseTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 0); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 0));
            GL.BindTexture(TextureTarget.Texture2D, material.fDiffuseMap.bufferID);

            //Mask Texture
            test = "maskTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 1); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 1));
            GL.BindTexture(TextureTarget.Texture2D, material.fMaskMap.bufferID);

            //Normal Texture
            test = "normalTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 2); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 2));
            GL.BindTexture(TextureTarget.Texture2D, material.fNormalMap.bufferID);


            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            //NEW WAY OF TEXTURE BINDING
            //If there are samples defined, there are diffuse textures for sure

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, this.color);
            
            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.FrontAndBack, RenderOptions.RENDERMODE);
            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            for (int i=0;i<7;i++)
                GL.DisableVertexAttribArray(i);
        }

        public  virtual void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (vbo.bufInfo[i] == null) continue;
                bufInfo buf = vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            for (int i = 0; i < material.materialflags.Count; i++)
                GL.Uniform1(loc + material.materialflags[i], 1.0f);

            //Upload BoneRemap Information
            loc = GL.GetUniformLocation(pass, "boneRemap");
            GL.Uniform1(loc, BoneRemap.Length, BoneRemap);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            float[] skinmats = Util.mulMatArrays(vbo.invBMats, scene.JMArray, 128);

            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 128, false, skinmats);

            //Render Elements
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));

            for (int i = 0; i< 7; i++)
                GL.DisableVertexAttribArray(i);

        }

        public override bool render(int pass)
        {
            if (this.renderable == false)
            {
                //Debug.WriteLine("Not Renderable");
                return false;
            }

            int program = this.shader_programs[pass];

            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    //renderBsphere(program);
                    //renderBbox(program);
                    renderMain(program);
                    break;
                //Render Debug
                case 1:
                    renderDebug(program);
                    break;
                //Render for Picking
                case 2:
                    renderDebug(program);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }
            
            return true;
        }

        private void rendersmall()
        {
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.small_vertex_buffer_object);

            int vpos, bI, bW;
            //Vertex attribute
            vpos = GL.GetAttribLocation(this.shader_programs[0], "vPosition");
            int vstride = vbo.vx_size * vertrstart;
            GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.HalfFloat, false, this.vbo.small_vx_size, vbo.small_vx_stride);
            GL.EnableVertexAttribArray(vpos);

            //If there are BlendIndices there are obviously blendWeights as well
            //Max Indices count found so far is 4. I'm hardcoding it unless i find something else in the files.
            bI = GL.GetAttribLocation(this.shader_programs[0], "blendIndices");
            GL.VertexAttribPointer(bI, 4, VertexAttribPointerType.UnsignedByte, false, vbo.small_vx_size, vbo.small_blendI_stride);
            GL.EnableVertexAttribArray(bI);

            bW = GL.GetAttribLocation(this.shader_programs[0], "blendWeights");
            GL.VertexAttribPointer(bW, 4, VertexAttribPointerType.HalfFloat, false, vbo.small_vx_size, vbo.small_blendW_stride);
            GL.EnableVertexAttribArray(bW);

            //Testing Upload full bIndices array
            //GL.BindBuffer(BufferTarget.ArrayBuffer, vbo.bIndices_buffer_object);
            //bI = GL.GetAttribLocation(this.shader_program, "blendIndices");
            //GL.VertexAttribPointer(bI, 4, VertexAttribPointerType.Int, false, 0, 0);
            //GL.EnableVertexAttribArray(bI);

            //InverseBind Matrices
            int loc;
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Upload BoneRemap Information
            loc = GL.GetUniformLocation(shader_programs[0], "boneRemap");
            GL.Uniform1(loc, BoneRemap.Length, BoneRemap);

            //Upload skinned status
            loc = GL.GetUniformLocation(shader_programs[0], "skinned");
            GL.Uniform1(loc, skinned);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            float[] skinmats = Util.mulMatArrays(vbo.invBMats, scene.JMArray, 128);

            loc = GL.GetUniformLocation(shader_programs[0], "skinMats");
            GL.UniformMatrix4(loc, 128, false, skinmats);

            //Upload procedural sampler flag
            loc = GL.GetUniformLocation(shader_programs[0], "procFlag");
            GL.Uniform1(loc, 0);

            //Disable Diffuse Maps
            loc = GL.GetUniformLocation(shader_programs[0], "diffTexCount");
            GL.Uniform1(loc, 0);

            loc = GL.GetUniformLocation(shader_programs[0], "diffuseFlag");
            GL.Uniform1(loc, 0.0f);

            //Upload Default Color
            loc = GL.GetUniformLocation(this.shader_programs[0], "color");
            GL.Uniform3(loc, 1.0, 0.0, 0.0); //Render Small Red

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.Front, RenderOptions.RENDERMODE);
            GL.PolygonMode(MaterialFace.Back, RenderOptions.RENDERMODE);
            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));

            //Debug.WriteLine("Normal Object {2} vpos {0} cpos {1} prog {3}", vpos, npos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vbo.vertex_buffer_object, this.vbo.color_buffer_object);

            GL.DisableVertexAttribArray(vpos);
            GL.DisableVertexAttribArray(bI);
            GL.DisableVertexAttribArray(bW);

        }

        public override GMDL.model Clone(GMDL.scene scn)
        {
            GMDL.sharedVBO copy = new GMDL.sharedVBO();
            copy.vertrend = this.vertrend;
            copy.vertrstart = this.vertrstart;
            copy.renderable = true; //Override Renderability
            copy.shader_programs = this.shader_programs;
            copy.type = this.type;
            copy.vbo = this.vbo;
            copy.name = this.name;
            copy.ID = this.ID;
            //Clone transformation
            copy.localPosition = this.localPosition;
            copy.localRotation = this.localRotation;
            copy.localScale = this.localScale;
            //Skinning Stuff
            copy.firstskinmat = this.firstskinmat;
            copy.lastskinmat = this.lastskinmat;
            copy.batchcount = this.batchcount;
            copy.batchstart = this.batchstart;
            copy.color = this.color;
            if (this.material != null)
                copy.material = this.material.Clone();
            copy.BoneRemap = this.BoneRemap;
            copy.skinned = this.skinned;
            copy.palette = this.palette;
            copy.cIndex = this.cIndex;
            //animation data
            copy.jointDict = new Dictionary<string, model>();
            copy.cloneJointDict(ref copy.jointDict, this.jointModel);
            copy.jointModel = new List<GMDL.Joint>();
            //In sharedVBO objects, both this and all the children have the same scene
            copy.scene = scn;
            foreach (GMDL.model child in this.jointModel)
            {
                GMDL.model nChild = child.Clone(scn);
                nChild.parent = copy;
                copy.jointModel.Add((GMDL.Joint) nChild);
            }
                
            //Clone Children as well
            foreach (GMDL.model child in this.children)
            {
                GMDL.model nChild = child.Clone(scn);
                nChild.parent = copy;
                copy.children.Add(nChild);
            }

            return (GMDL.model)copy;
        }

        public void writeGeomToStream(StreamWriter s, ref uint index)
        {
            int vertcount = this.vertrend - this.vertrstart + 1;
            MemoryStream vms = new MemoryStream(this.vbo.geomVbuf);
            MemoryStream ims = new MemoryStream(this.vbo.geomIbuf);
            BinaryReader vbr = new BinaryReader(vms);
            BinaryReader ibr = new BinaryReader(ims);
            //Start Writing
            //Object name
            s.WriteLine("o " + this.name);
            //Get Verts

            vbr.BaseStream.Seek(vbo.vx_size * vertrstart, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                uint v1 = vbr.ReadUInt16();
                uint v2 = vbr.ReadUInt16();
                uint v3 = vbr.ReadUInt16();
                //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());

                //Transform vector with worldMatrix
                Vector4 v = new Vector4(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3),1.0f);
                v = Vector4.Transform(v, this.worldMat);


                //s.WriteLine("v " + Half.decompress(v1).ToString() + " "+ Half.decompress(v2).ToString() + " " + Half.decompress(v3).ToString());
                s.WriteLine("v " + v.X.ToString() + " " + v.Y.ToString() + " " + v.Z.ToString());
                vbr.BaseStream.Seek(this.vbo.vx_size - 0x6, SeekOrigin.Current);
            }
            //Get Normals

            vbr.BaseStream.Seek(vbo.n_stride + vbo.vx_size * vertrstart, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                uint v1 = vbr.ReadUInt16();
                uint v2 = vbr.ReadUInt16();
                uint v3 = vbr.ReadUInt16();
                //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());

                s.WriteLine("vn " + Half.decompress(v1).ToString() + " " + Half.decompress(v2).ToString() + " " + Half.decompress(v3).ToString());
                vbr.BaseStream.Seek(this.vbo.vx_size - 0x6, SeekOrigin.Current);
            }
            //Get UVs

            vbr.BaseStream.Seek(vbo.uv0_stride + vbo.vx_size * vertrstart, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                uint v1 = vbr.ReadUInt16();
                uint v2 = vbr.ReadUInt16();
                uint v3 = vbr.ReadUInt16();
                //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());

                s.WriteLine("vt " + Half.decompress(v1).ToString() + " " + Half.decompress(v2).ToString());
                vbr.BaseStream.Seek(this.vbo.vx_size - 0x6, SeekOrigin.Current);
            }

            //Some Options
            s.WriteLine("usemtl(null)");
            s.WriteLine("s off");

            //Get indices
            ibr.BaseStream.Seek(this.vbo.iLength * this.batchstart, SeekOrigin.Begin);
            bool start = false;
            uint fstart = 0;
            for (int i = 0; i < batchcount/3; i++)
            {
                uint f1, f2, f3;
                if (this.vbo.iLength == 2)
                {
                    f1 = ibr.ReadUInt16();
                    f2 = ibr.ReadUInt16();
                    f3 = ibr.ReadUInt16();
                }
                else
                {
                    f1 = ibr.ReadUInt32();
                    f2 = ibr.ReadUInt32();
                    f3 = ibr.ReadUInt32();
                }

                if (!start)
                    fstart = f1; start = true;

                uint f11, f22, f33;
                f11 = f1 - fstart + index;
                f22 = f2 - fstart + index;
                f33 = f3 - fstart + index;

                s.WriteLine("f " + f11.ToString() + "/" + f11.ToString() + "/" + f11.ToString() + " "
                                 + f22.ToString() + "/" + f22.ToString() + "/" + f22.ToString() + " "
                                 + f33.ToString() + "/" + f33.ToString() + "/" + f33.ToString() + " ");
            }
            index += (uint) vertcount;
        }

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (material !=null) material.Dispose();
            //vbo.Dispose(); I assume the the vbo's will be cleared with Resourcegmt cleanup
            BoneRemap = null;
            base.Dispose(disposing);
        }

    }

    public class Collision : sharedVBO
    {
        public int collisionType = -1;

        //Custom constructor
        public Collision()
        {
            this.skinned = 0; //Collision objects are not skinned (at least for now)
            this.color = new Vector3(1.0f, 1.0f, 0.0f); //Set Yellow Color for collision objects
        }

        public override bool render(int pass)
        {
            if (this.renderable == false || this.vbo == null || RenderOptions.RenderCollisions == false)
            {
                //Debug.WriteLine("Not Renderable");
                return false;
            }

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    program = this.shader_programs[pass];
                    renderMain(program);
                    break;
                //Render Debug
                case 1:
                    program = this.shader_programs[pass];
                    renderDebug(program);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        public override void renderMain(int pass)
        {
            //Debug.WriteLine(this.name + this);
            GL.UseProgram(pass);

            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (vbo.bufInfo[i] == null) continue;
                bufInfo buf = vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            //GL.Uniform3(loc, this.color);
            GL.Uniform3(loc, this.color);

            //Upload Light Flag
            loc = GL.GetUniformLocation(pass, "useLighting");
            GL.Uniform1(loc, 0.0f);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            
            if (collisionType == (int)COLLISIONTYPES.MESH)
            {
                GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));
            }
            else if (collisionType == (int)COLLISIONTYPES.BOX)
            {
                GL.DrawRangeElements(PrimitiveType.Triangles, 0, vbo.vCount,
                vbo.iCount, vbo.iType, IntPtr.Zero);
            }
            else
            {
                GL.DrawRangeElements(PrimitiveType.Triangles, 0, vbo.vCount,
                vbo.iCount, vbo.iType, IntPtr.Zero);
            }

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            //Debug.WriteLine("Normal Object {2} vpos {0} cpos {1} prog {3}", vpos, npos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vbo.vertex_buffer_object, this.vbo.color_buffer_object);

            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

        }

        public override void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (vbo.bufInfo[i] == null) continue;
                bufInfo buf = vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Render Elements
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));

            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

        }

    }

    public class Decal : sharedVBO
    {
        public int collisionType = -1;

        //Custom constructor
        public Decal() { }

        public Decal(GMDL.sharedVBO root) {
            //Copy attributes from root object
            this.vertrend = root.vertrend;
            this.vertrstart = root.vertrstart;
            this.renderable = true; //Override Renderability
            this.shader_programs = root.shader_programs;
            this.type = root.type;
            this.vbo = root.vbo;
            this.name = root.name;
            this.ID = root.ID;
            //Clone transformation
            this.localPosition = root.localPosition;
            this.localRotation = root.localRotation;
            this.localScale = root.localScale;
            //Skinning Stuff
            this.firstskinmat = root.firstskinmat;
            this.lastskinmat = root.lastskinmat;
            this.batchcount = root.batchcount;
            this.batchstart = root.batchstart;
            this.color = root.color;
            this.material = root.material;
            this.BoneRemap = root.BoneRemap;
            this.skinned = root.skinned;
            this.palette = root.palette;
            this.cIndex = root.cIndex;
            //animation data
            this.jointDict = new Dictionary<string, model>();
            this.cloneJointDict(ref this.jointDict, root.jointModel);
            this.jointModel = new List<GMDL.Joint>();
            //In sharedVBO objects, both root and all the children have the same scene
            this.scene = root.scene;
            this.children = root.children; //Just assign them by ref
            
        }

        public override bool render(int pass)
        {
            if (this.renderable == false || this.vbo == null)
            {
                //Debug.WriteLine("Not Renderable");
                return false;
            }

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    program = this.shader_programs[pass];
                    renderMain(program);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        public override void renderMain(int pass)
        {
            //Debug.WriteLine(this.name + this);
            GL.UseProgram(pass);

            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (vbo.bufInfo[i] == null) continue;
                bufInfo buf = vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            if (!(loc < 0))
            {
                for (int i = 0; i < 64; i++)
                    GL.Uniform1(loc + i, 0.0f);

                for (int i = 0; i < material.materialflags.Count; i++)
                    GL.Uniform1(loc + material.materialflags[i], 1.0f);
            }
            

            //Upload decalTexture

            //BIND TEXTURES
            int tex0Id = (int)TextureUnit.Texture0;
            //Diffuse Texture
            string test = "decalTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 0); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 0));
            GL.BindTexture(TextureTarget.Texture2D, material.fDiffuseMap.bufferID);

            //Depth Texture
            test = "depthTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 1); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 1));
            GL.BindTexture(TextureTarget.Texture2D, Util.gbuf.positions);

            //Util.gbuf.dump();

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));


            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

        }

        public override void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            for (int i = 0; i < 7; i++)
            {
                if (vbo.bufInfo[i] == null) continue;
                bufInfo buf = vbo.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vbo.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Render Elements
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr)(batchstart * vbo.iLength));

            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

        }

    }

    public class customVBO: IDisposable
    {
        private bool disposed = false;
        public int vertex_buffer_object;
        public int small_vertex_buffer_object;
        public int normal_buffer_object;
        public int element_buffer_object;
        public int color_buffer_object;
        //Testing
        public int bIndices_buffer_object;

        public List<JointBindingData> jointData;
        public List<GMDL.bufInfo> bufInfo;
        public float[] invBMats;
        public int vx_size;
        public int vx_stride;
        public int n_stride;
        public int t_stride;
        public int b_stride;
        public int uv0_stride;
        public int blendI_stride;
        public int blendW_stride;

        //Small Stuff
        public int small_vx_size;
        public int small_vx_stride;
        public int small_blendI_stride;
        public int small_blendW_stride;

        public int trisCount;
        public int iCount;
        public int vCount;
        public int iLength;
        public int[] boneRemap = new int[512];
        public DrawElementsType iType;
        public byte[] geomVbuf;
        public byte[] geomIbuf;



        public customVBO()
        {
        }

        public customVBO(GeomObject geom)
        {
            this.LoadFromGeom(geom);
        }

        public void LoadFromGeom(GeomObject geom)
        {
            //Set essential parameters
            this.vx_size = geom.vx_size;
            this.small_vx_size = geom.small_vx_size;
            this.vx_stride = geom.offsets[0];
            this.bufInfo = geom.bufInfo;
            this.small_vx_stride = geom.small_offsets[0];
            this.uv0_stride = geom.offsets[1];
            this.n_stride = geom.offsets[2];
            this.t_stride = geom.offsets[3];
            this.b_stride = geom.offsets[4];
            this.blendI_stride = geom.offsets[5];
            this.small_blendI_stride = geom.small_offsets[5];
            this.blendW_stride = geom.offsets[6];
            this.small_blendW_stride = geom.small_offsets[6];
            this.vCount = (int)geom.vertCount;
            this.iCount = (int) geom.indicesCount;
            this.trisCount = (int) geom.indicesCount / 3;
            this.iLength = (int)geom.indicesLength;
            this.boneRemap = geom.boneRemap;
            this.geomVbuf = geom.vbuffer;
            this.geomIbuf = geom.ibuffer;
            
            if (geom.indicesLength == 0x2)
                this.iType = DrawElementsType.UnsignedShort;
            else
                this.iType = DrawElementsType.UnsignedInt;
            //Set Joint Data
            this.jointData = geom.jointData;
            invBMats = new float[128 * 16];
            //Copy inverted Matrix to local variable
            for (int i = 0; i < jointData.Count; i++)
                Array.Copy(jointData[i].convertMat(), 0, invBMats, 16 * i, 16);

            int[] vbo_buffers = new int[5];
            GL.GenBuffers(5, vbo_buffers);

            vertex_buffer_object = vbo_buffers[0];
            small_vertex_buffer_object = vbo_buffers[1];
            color_buffer_object = vbo_buffers[2];
            element_buffer_object = vbo_buffers[3];
            bIndices_buffer_object = vbo_buffers[4];

            //GL.GenBuffers(1, out vertex_buffer_object);
            //GL.GenBuffers(1, out small_vertex_buffer_object);
            //GL.GenBuffers(1, out color_buffer_object);
            //GL.GenBuffers(1, out element_buffer_object);
            //GL.GenBuffers(1, out bIndices_buffer_object);
            Debug.WriteLine(GL.GetError());

            int size;
            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (geom.vx_size * geom.vertCount),
                geom.vbuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != geom.vx_size * geom.vertCount)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));

            //Upload small vertex buffer
            if (geom.small_vx_size != -1)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, small_vertex_buffer_object);
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(geom.small_vx_size * geom.vertCount),
                    geom.small_vbuffer, BufferUsageHint.StaticDraw);
                GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                    out size);
                if (size != geom.small_vx_size * geom.vertCount)
                    throw new ApplicationException(String.Format("Problem with small vertex buffer"));
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(geom.indicesLength * geom.indicesCount), geom.ibuffer, BufferUsageHint.StaticDraw);

            ////Explicitly Parse BlendIndices
            //int offset = blendI_stride;
            //int[] bIndices = new int[geom.vertCount * 4];
            //float[] bWeights = new float[geom.vertCount * 4];

            ////Binary Reader
            //MemoryStream ms = new MemoryStream();
            //ms.Write(geom.vbuffer, 0, geom.vbuffer.Length);
            //BinaryReader br = new BinaryReader(ms);
            //ms.Position = blendI_stride;
            //for (int i = 0; i < geom.vertCount; i++)
            //{
            //    //bIndices[4 * i] = br.ReadByte();
            //    //bIndices[4 * i + 1] = br.ReadByte();
            //    //bIndices[4 * i + 2] = br.ReadByte();
            //    //bIndices[4 * i + 3] = br.ReadByte();

            //    Debug.WriteLine("Indices {0} {1} {2} {3}", br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            //    ms.Position += geom.vx_size - 4;
            //}

            //GL.BindBuffer(BufferTarget.ArrayBuffer, bIndices_buffer_object);
            //GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (sizeof(int) * 4 * geom.vertCount),
            //    bIndices, BufferUsageHint.StaticDraw);

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                bufInfo.Clear();
                jointData.Clear();
                //Clear gl arrays
                GL.DeleteBuffer(vertex_buffer_object);
                GL.DeleteBuffer(small_vertex_buffer_object);
                GL.DeleteBuffer(element_buffer_object);
                GL.DeleteBuffer(color_buffer_object);
                
                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~customVBO()
        {
            Dispose(false);
        }

        
    }

    public class GeomObject : IDisposable
    {
        private bool disposed = false;
        //public List<Vector3> verts = new List<Vector3>();
        //public List<Vector3> normals = new List<Vector3>();
        //public List<Vector3> tangents = new List<Vector3>();
        //public List<List<Vector2>> uvs = new List<List<Vector2>>();
        public string mesh_descr;
        public string small_mesh_descr;

        public bool interleaved;
        public int vx_size;
        public int small_vx_size;

        //Counters
        public int indicesCount=0;
        public int indicesLength = 0;
        public int vertCount = 0;

        //make sure there are enough buffers for non interleaved formats
        public byte[] ibuffer;
        public byte[] vbuffer;
        public byte[] small_vbuffer;
        public byte[] cbuffer;
        public byte[] nbuffer;
        public byte[] ubuffer;
        public byte[] tbuffer;
        public List<int[]> bIndices = new List<int[]>();
        public List<float[]> bWeights = new List<float[]>();
        public List<bufInfo> bufInfo = new List<GMDL.bufInfo>();
        public int[] offsets; //List to save strides according to meshdescr
        public int[] small_offsets; //Same thing for the small description
        public int[] boneRemap;
        public List<Vector3[]> bboxes = new List<Vector3[]>();
        public List<int> vstarts = new List<int>();

        public customVBO vbo;

        //Joint info
        public List<JointBindingData> jointData = new List<JointBindingData>();
        
        public Vector3 get_vec3_half(BinaryReader br)
        {
            Vector3 temp;
            //Get Values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            uint val3 = br.ReadUInt16();
            //Convert Values
            temp.X = Half.decompress(val1);
            temp.Y = Half.decompress(val2);
            temp.Z = Half.decompress(val3);
            //Debug.WriteLine("half {0} {1} {2}", temp[0],temp[1],temp[2]);
            return temp;
        }

        public Vector2 get_vec2_half(BinaryReader br)
        {
            Vector2 temp;
            //Get values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            //Convert Values
            temp.X = Half.decompress(val1);
            temp.Y = Half.decompress(val2);
            return temp;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                bIndices.Clear();
                bWeights.Clear();
                bufInfo.Clear();
                bboxes.Clear();
                vstarts.Clear();
                //Clear vbo
                if (vbo != null) vbo.Dispose();

                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~GeomObject()
        {
            Dispose(false);
        }

        
    }
    
    public class bufInfo
    {
        public int semantic;
        public VertexAttribPointerType type;
        public int count;
        public int stride;
        public string sem_text;

        public bufInfo(int sem,VertexAttribPointerType typ, int c, int s, string t)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
        }
    }

    public class Material: IDisposable
    {
        private bool disposed = false;
        public string name;
        public string type;
        public MatOpts opts;
        public List<int> materialflags = new List<int>();
        public Dictionary<string, Dictionary<string, Vector4>> palette = new Dictionary<string, Dictionary<string, Vector4>>();
        public List<Uniform> uniforms = new List<Uniform>();
        public List<Sampler> samplers = new List<Sampler>();
        public List<PaletteOpt> palOpts = new List<PaletteOpt>();
        public List<Texture> difftextures = new List<Texture>(8);
        public List<Texture> masktextures = new List<Texture>(8);
        public List<Texture> normaltextures = new List<Texture>(8);
        public float[] baseLayersUsed = new float[8];
        public float[] alphaLayersUsed = new float[8];
        public List<float[]> reColourings = new List<float[]>(8);
        public Texture fDiffuseMap = new Texture();
        public Texture fMaskMap = new Texture();
        public Texture fNormalMap = new Texture();

        public Material()
        {
            //Init texture buffers
            for (int i = 0; i < 8; i++)
            {
                difftextures.Add(null);
                masktextures.Add(null);
                normaltextures.Add(null);
                reColourings.Add(new float[] { 1.0f, 1.0f, 1.0f, 0.0f });
                palOpts.Add(null);
            }

        }

        public GMDL.Material Clone()
        {
            GMDL.Material newmat = new GMDL.Material();

            //Clone Samplers
            for (int i = 0; i < samplers.Count; i++)
                newmat.samplers.Add(samplers[i].Clone());

            //Copy materialflags
            for (int i=0;i<materialflags.Count;i++)
                newmat.materialflags.Add(materialflags[i]);

            //Copy arrays
            for (int i = 0; i < 8; i++)
            {
                //newmat.alphaLayersUsed = this.alphaLayersUsed;
                //newmat.baseLayersUsed = this.baseLayersUsed;
                //newmat.difftextures[i] = this.difftextures[i];
                //newmat.masktextures[i] = this.masktextures[i];
                //newmat.normaltextures[i] = this.normaltextures[i];
                //newmat.reColourings[i] = this.reColourings[i];
                
                //Create palOpts
                if (this.palOpts[i] != null)
                {
                    PaletteOpt palOpt = new PaletteOpt();
                    palOpt.ColorName = this.palOpts[i].ColorName;
                    palOpt.PaletteName = this.palOpts[i].PaletteName;
                    newmat.palOpts[i] = palOpt;
                }
            }

            //Remix textures

            return newmat;
        }

        public void prepTextures()
        {
            foreach (Sampler sam in samplers){

                if(this.name == "EagleHead_Mat")
                {
                    Debug.WriteLine("b");
                }

                string[] split = sam.pathDiff.Split('.');
                //Construct main filename
                string temp = "";
                for (int i = 0; i < split.Length - 1; i++)
                    temp += split[i] + ".";
                string texMbin = temp + "TEXTURE.MBIN";
                string texMbinexml = temp + "TEXTURE.exml";
                texMbin = Path.GetFullPath(Path.Combine(Util.dirpath, texMbin));
                //texMbinexml = Path.Combine(Util.dirpath, texMbinexml);
                texMbinexml = Util.getExmlPath(texMbin);
                
                //Force procgen if there is a sub procgen texture defined in the sampler
                if (Util.forceProcGen)
                {
                    texMbin = split[0] + ".TEXTURE.MBIN";
                    texMbin = Path.GetFullPath(Path.Combine(Util.dirpath, texMbin));
                    texMbinexml = Util.getExmlPath(texMbin);
                }
                 
                //Detect Procedural Texture
                if (File.Exists(texMbin))
                {
                    Debug.WriteLine("Procedural Texture Detected: " + texMbin);
                    sam.proc = true;
                    //Convert to exml
                    if (!File.Exists(texMbinexml))
                        Util.MbinToExml(texMbin, texMbinexml);
                    
                    //Parse exml now
                    XmlDocument descrXml = new XmlDocument();
                    descrXml.Load(texMbinexml);
                    XmlElement root = (XmlElement)descrXml.ChildNodes[1];

                    List<XmlElement> texList = new List<XmlElement>(8);
                    for (int i = 0; i < 8; i++) texList.Add(null);
                    ModelProcGen.parse_procTexture(ref texList, root);

#if DEBUG
                    Debug.WriteLine("Proc Texture Selection");
                    for (int i = 0; i < 8; i++) {
                        if (texList[i] != null)
                        {
                            string partNameDiff = ((XmlElement)texList[i].SelectSingleNode(".//Property[@name='Diffuse']")).GetAttribute("value");
                            Debug.WriteLine(partNameDiff);
                        }
                    }
                        
#endif
                    Debug.WriteLine("Proc Textures");

                    for (int i = 7; i > 0; i--)
                    {

                        XmlElement node = texList[i];
                        //Add defaults
                        if (node == null)
                        {
                            baseLayersUsed[i] = 0.0f;
                            alphaLayersUsed[i] = 0.0f;
                            continue;
                        }

                        string partNameDiff = ((XmlElement)node.SelectSingleNode(".//Property[@name='Diffuse']")).GetAttribute("value");
                        string partNameMask = ((XmlElement)node.SelectSingleNode(".//Property[@name='Mask']")).GetAttribute("value");
                        string partNameNormal = ((XmlElement)node.SelectSingleNode(".//Property[@name='Normal']")).GetAttribute("value");

                        XmlElement paletteNode = (XmlElement) node.SelectSingleNode(".//Property[@name='Palette']");
                        //XmlElement innerPalNode = (XmlElement)paletteNode.SelectSingleNode(".//Property[@name='Palette']");
                        string paletteName = ((XmlElement) paletteNode.SelectSingleNode(".//Property[@name='Palette']")).GetAttribute("value");
                        //Get ColourAlt node
                        string colorName = ((XmlElement)paletteNode.SelectSingleNode(".//Property[@name='ColourAlt']")).GetAttribute("value");

                        //Select a pallete color
                        Vector4 palColor = palette[paletteName][colorName];
                        //Randomize palette Color every single time
                        //Vector3 palColor = Model_Viewer.Palettes.get_color(paletteName, colorName);
                        
                        //Store pallete color to Recolouring List
                        reColourings[i] = new float[] { palColor[0], palColor[1], palColor[2], palColor[3] };
                        //Create Palette Option
                        PaletteOpt palOpt = new PaletteOpt();
                        palOpt.PaletteName = paletteName;
                        palOpt.ColorName = colorName;
                        palOpts[i] = palOpt;

                        //DIFFUSE
                        if (partNameDiff == "")
                        {
                            //Add White
                            baseLayersUsed[i] = 0.0f;
                        } else if (!Util.resMgmt.GLtextures.ContainsKey(partNameDiff))
                        {
                            //Construct Texture paths
                            string pathDiff = Path.Combine(Util.dirpath, partNameDiff);

                            //Configure the Diffuse Texture
                            try
                            {
                                Texture tex = new Texture(pathDiff);
                                tex.palOpt = palOpt;
                                tex.procColor = palColor;
                                //store to global dict
                                Util.resMgmt.GLtextures[partNameDiff] = tex;

                                //Save Texture to material
                                this.difftextures[i] = tex;
                                baseLayersUsed[i] = 1.0f;
                                alphaLayersUsed[i] = 1.0f;
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                //Texture Not Found Continue
                                Debug.WriteLine("Diffuse Texture " + pathDiff + " Not Found, Appending White Tex");
                                baseLayersUsed[i] = 0.0f;
                            }
                        } else
                        //Load texture from dict
                        {
                            Texture tex = Util.resMgmt.GLtextures[partNameDiff];
                            //Save Texture to material
                            this.difftextures[i] = tex;
                            baseLayersUsed[i] = 1.0f;
                        }


                        //MASK
                        if (partNameMask == "")
                        {
                            //Skip
                            alphaLayersUsed[i] = 0.0f;
                        } else if (!Util.resMgmt.GLtextures.ContainsKey(partNameMask))
                        {
                            string pathMask = Path.Combine(Util.dirpath, partNameMask);
                            //Configure Mask
                            try
                            {
                                Texture texmask = new Texture(pathMask);
                                //store to global dict
                                Util.resMgmt.GLtextures[partNameMask] = texmask;
                                //Store Texture to material
                                this.masktextures[i] = texmask;
                                alphaLayersUsed[i] = 0.0f;
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                //Mask Texture not found
                                Debug.WriteLine("Mask Texture " + pathMask + " Not Found");
                                alphaLayersUsed[i] = 0.0f;
                            }
                        }
                        else
                        //Load texture from dict
                        {
                            Texture tex = Util.resMgmt.GLtextures[partNameMask];
                            //Store Texture to material
                            this.masktextures[i] = tex;
                            alphaLayersUsed[i] = 1.0f;
                        }


                        //NORMALS
                        if (partNameNormal == "")
                        {
                            //Skip

                        } else if (!Util.resMgmt.GLtextures.ContainsKey(partNameNormal))
                        {
                            string pathNormal = Path.Combine(Util.dirpath, partNameNormal);
                            
                            try
                            {
                                Texture texnormal = new Texture(pathNormal);
                                //store to global dict
                                Util.resMgmt.GLtextures[partNameNormal] = texnormal;
                                //Store Texture to material
                                this.normaltextures[i] = texnormal;
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                //Normal Texture not found
                                Debug.WriteLine("Normal Texture " + pathNormal + " Not Found");
                            }

                        }
                        else
                        //Load texture from dict
                        {
                            Texture tex = Util.resMgmt.GLtextures[partNameNormal];
                            //Store Texture to material
                            this.normaltextures[i] = tex;
                        }

                    }

                }
                //Store Non Proc Texture
                else
                {
                    int active_id = 0;
                    Debug.WriteLine("Proper Texture, Bullshiting for now");
                    //Handle Diffuse
                    if (sam.pathDiff != "")
                        if (Util.resMgmt.GLtextures.ContainsKey(sam.pathDiff))
                        {
                            Texture tex = Util.resMgmt.GLtextures[sam.pathDiff];
                            difftextures[active_id] = tex;
                            baseLayersUsed[active_id] = 1.0f;
                        }
                        else
                        {
                            Texture tex = new Texture(Path.Combine(Model_Viewer.Util.dirpath, sam.pathDiff));
                            tex.palOpt = new PaletteOpt(false);
                            tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                            difftextures[active_id] = tex;
                            baseLayersUsed[active_id] = 1.0f;
                            //Store to resource
                            Util.resMgmt.GLtextures[sam.pathDiff] = tex;
                        }


                    //Handle Mask
                    if (sam.pathMask != "" && sam.pathMask != null)
                        if (Util.resMgmt.GLtextures.ContainsKey(sam.pathMask))
                        {
                            Texture tex = Util.resMgmt.GLtextures[sam.pathMask];
                            masktextures[active_id] = tex;
                            alphaLayersUsed[active_id] = 1.0f;
                        }
                        //else if (!File.Exists(Path.Combine(Util.dirpath, sam.pathMask)))
                        //{
                        //    Texture tex = Util.resMgmt.GLtextures["default_mask.dds"];
                        //    masktextures[active_id] = tex;
                        //    alphaLayersUsed[active_id] = 1.0f;
                        //}
                        else
                        {
                            Texture tex = new Texture(Path.Combine(Model_Viewer.Util.dirpath, sam.pathMask));
                            tex.palOpt = new PaletteOpt(false);
                            tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                            masktextures[active_id] = tex;
                            alphaLayersUsed[active_id] = 1.0f;
                            //Store to resource
                            Util.resMgmt.GLtextures[sam.pathMask] = tex;
                        }

                    //Handle Normal
                    if (sam.pathNormal != "" && sam.pathNormal != null)
                        if (Util.resMgmt.GLtextures.ContainsKey(sam.pathNormal))
                        {
                            Texture tex = Util.resMgmt.GLtextures[sam.pathNormal];
                            normaltextures[active_id] = tex;
                        }
                        else
                        {
                            try
                            {
                                Texture tex = new Texture(Path.Combine(Model_Viewer.Util.dirpath, sam.pathNormal));
                                tex.palOpt = new PaletteOpt(false);
                                tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                                normaltextures[active_id] = tex;
                                //Store to resource
                                Util.resMgmt.GLtextures[sam.pathNormal] = tex;
                            }
                            catch (System.IO.FileNotFoundException)
                            { 
                                //File doesn't exist, to nothing
                            }
                            
                            
                        }
                    
                }
                    
            }

            //Reverse Lists



        }

        public void mixTextures() {
            //SETUP QUAD
            GL.UseProgram(Util.resMgmt.shader_programs[3]);
            int quad_vbo;
            int quad_ebo;

            //Define Quad
            float[] quad = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

            float[] quadcolors = new float[6 * 3]
            {
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f,  0.0f, 1.0f
            };

            //Indices
            int[] indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            //Generate OpenGL buffers
            int arraysize = sizeof(float) * 6 * 3;
            GL.GenBuffers(1, out quad_vbo);
            GL.GenBuffers(1, out quad_ebo);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, quad);
            //Add color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, quadcolors);

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * 6), indices, BufferUsageHint.StaticDraw);

            //Vertex attribute
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            //vPosition #0
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //vColor #1
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(1);

            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);


            //Create Textures to save to
            int texsize = 1024;
            //Diffuse Output
            int out_tex_diffuse = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex_diffuse);
            //GL.TexImage2DMultisample(TextureTargetMultisample.Texture2D, 4, PixelInternalFormat.Rgba, texsize, texsize, false);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //NULL means reserve texture memory, but texels are undefined
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texsize, texsize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            //Create New RenderBuffer for the diffuse
            int fb_diffuse = GL.GenFramebuffer();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_diffuse);
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, out_tex_diffuse, 0);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Debug.WriteLine("MALAKIES STO FRAMEBUFFER");

            //Mask Output
            int out_tex_mask = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex_mask);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //NULL means reserve texture memory, but texels are undefined
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texsize, texsize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            //Create New RenderBuffer for the diffuse
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, out_tex_mask, 0);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Debug.WriteLine("MALAKIES STO FRAMEBUFFER");

            //Normal Output
            int out_tex_normal = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex_normal);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //NULL means reserve texture memory, but texels are undefined
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texsize, texsize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            //Create New RenderBuffer for the diffuse
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, out_tex_normal, 0);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Debug.WriteLine("MALAKIES STO FRAMEBUFFER");


            //Upload Textures
            int pass_program = Util.resMgmt.shader_programs[3];

            //BIND TEXTURES
            GMDL.Texture tex;
            int loc;

            Debug.WriteLine("Rendering Textures of : " + name);
            //If there are samples defined, there are diffuse textures for sure

            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            //NEW WAY OF TEXTURE BINDING

            //DIFFUSE TEXTURES
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.ConstantAlpha, BlendingFactorDest.OneMinusConstantAlpha);

            /*
             * 
             * TEXTURES SHOULD BE PUSHED TO THE SHADERS BOTTOM TO TOP
             * 
             * 
             * 
             * 
             */

            Texture dMask = Util.resMgmt.GLtextures["default_mask.dds"];
            Texture dDiff = Util.resMgmt.GLtextures["default.dds"];

            //Upload base Layers Used
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;
                loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed[" + active_id.ToString() + "]");
                GL.Uniform1(loc, baseLayersUsed[active_id]);
            }

            //Upload Base Layer Index
            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            for (int i = 0; i < 8; i++)
            {
                //int active_id = 7 - i;
                if (difftextures[i] != null)
                { 
                    GL.Uniform1(loc, i);
                    break;
                }
                
            }

            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                if (difftextures[active_id] != null)
                    tex = difftextures[active_id];
                else
                    tex = dMask;

                //Upload diffuse Texture
                string sem = "diffuseTex[" + active_id.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, active_id); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                GL.ActiveTexture((TextureUnit) (tex0Id + active_id));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);

            }

            //Seems like alphaChannel variable is set from the _F24_AOMAP flag
            //^^^ AO Map flag probably does not fix the alpha situation.
            //Decals don't have the flag but their textures contain alpha. For now I'm adding the Transparent flag on the check as well
            loc = GL.GetUniformLocation(pass_program, "hasAlphaChannel");
            if (materialflags.Contains(23) || materialflags.Contains(8))
                GL.Uniform1(loc, 1.0f);
            else
                GL.Uniform1(loc, 0.0f);


            //MASKS
            //Upload alpha Layers Used
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;
                loc = GL.GetUniformLocation(pass_program, "lalphaLayersUsed[" + active_id.ToString() + "]");
                GL.Uniform1(loc, alphaLayersUsed[active_id]);
            }

            //Upload Mask Textures -- Alpha Masks???
            loc = GL.GetUniformLocation(pass_program, "m_lbaseLayersUsed");
            for (int i = 0; i < 8; i++)
            {
                if (masktextures[i] != null) GL.Uniform1(loc + i, 1.0f);
                else GL.Uniform1(loc + i, 0.0f);
            }
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                if (masktextures[active_id] != null)
                    tex = masktextures[active_id];
                else
                    tex = dDiff;


                //Upload diffuse Texture
                string sem = "maskTex[" + active_id.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, 8 + active_id); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                //Upload PaletteColor
                //loc = GL.GetUniformLocation(pass_program, "palColors[" + i.ToString() + "]");
                //Use Texture paletteOpt and object palette to load the palette color
                //GL.Uniform3(loc, palette[tex.palOpt.PaletteName][tex.palOpt.ColorName]);

                GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 8 + active_id));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);

            }

            //Upload Normal Textures
            loc = GL.GetUniformLocation(pass_program, "n_lbaseLayersUsed");
            for (int i = 0; i < 8; i++)
            {
                if (normaltextures[i] != null) GL.Uniform1(loc + i, 1.0f);
                else GL.Uniform1(loc + i, 0.0f);
            }
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                if (normaltextures[active_id] != null)
                    tex = normaltextures[active_id];
                else
                    tex = dMask;

                //Upload diffuse Texture
                string sem = "normalTex[" + active_id.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, 16 + active_id); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                GL.ActiveTexture((TextureUnit)(tex0Id + 16 + active_id));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
            }

            //Upload Recolouring Information
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                loc = GL.GetUniformLocation(pass_program, "lRecolours[" + active_id.ToString() + "]");
                GL.Uniform4(loc, reColourings[active_id][0], reColourings[active_id][1], reColourings[active_id][2], reColourings[active_id][3]);
            }


            //RENDERING PHASE
            //Render to the FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_diffuse);
            GL.DrawBuffers(3, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 });

            //Set Viewport
            GL.Viewport(0, 0, 1024, 1024);
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Multisample);
            //GL.Disable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            //Store Framebuffer to Disk
            byte[] pixels = new byte[4 * texsize * texsize];
            //GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            
            //Diffuse
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
#if TEST
            GL.ReadPixels(0, 0, texsize, texsize, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            FileStream fs = new FileStream("framebuffer_raw_diffuse_" + name, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(pixels);
            fs.Flush();
            fs.Close();
#endif
            //Store Texture to material
            fDiffuseMap.bufferID = out_tex_diffuse;
            
            
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
#if TEST
            GL.ReadPixels(0, 0, texsize, texsize, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            fs = new FileStream("framebuffer_raw_mask_" + name, FileMode.Create);
            bw = new BinaryWriter(fs);
            bw.Write(pixels);
            fs.Flush();
            fs.Close();
#endif
            //Store Texture to material
            fMaskMap.bufferID = out_tex_mask;

            GL.ReadBuffer(ReadBufferMode.ColorAttachment2);
#if DEBUG
            GL.ReadPixels(0, 0, texsize, texsize, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            FileStream fs = new FileStream("framebuffer_raw_normal_" + name, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(pixels);
            fs.Flush();
            fs.Close();
#endif
            //Store Texture to material
            fNormalMap.bufferID = out_tex_normal;

            //Bring Back screen
            GL.Disable(EnableCap.Multisample);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DeleteBuffer(quad_vbo);
            GL.DeleteBuffer(quad_ebo);
            GL.DeleteFramebuffer(fb_diffuse);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                materialflags.Clear();
                palette.Clear();
                uniforms.Clear();
                samplers.Clear();
                reColourings.Clear();
                //Texture lists should have been disposed from the dictionary
                foreach (Texture t in difftextures)
                    if (t!=null) t.Dispose();
                difftextures.Clear();
                foreach (Texture t in masktextures)
                    if (t != null) t.Dispose();
                masktextures.Clear();
                foreach (Texture t in normaltextures)
                    if (t != null) t.Dispose();
                normaltextures.Clear();

                if (fDiffuseMap != null) fDiffuseMap.Dispose();
                if (fMaskMap != null) fMaskMap.Dispose();
                if (fNormalMap != null) fNormalMap.Dispose();
                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~Material()
        {
            Dispose(false);
        }

        
    }
    
    public class Uniform
    {
        public string name;
        public Vector4 value;
    }

    public class MatOpts
    {
        public int transparency;
        public bool castshadow;
        public bool disableTestz;
        public string link;
        public string shadername;
    }

    public class Sampler
    {
        public string name;
        public string pathDiff;
        public string pathMask = null;
        public string pathNormal = null;
        public bool proc = false;
        public List<Texture> procTextures = new List<Texture>();

        public Sampler Clone()
        {
            Sampler newsampler = new Sampler();
            newsampler.name = name;
            newsampler.pathDiff = pathDiff;
            newsampler.pathMask = pathMask;
            newsampler.pathNormal = pathNormal;
            newsampler.proc = proc;
            return newsampler;

        }
    }

    public class PaletteOpt
    {
        public string PaletteName;
        public string ColorName;

        //Default Empty Constructor
        public PaletteOpt() { }
        //Empty Palette Constructor
        public PaletteOpt(bool flag)
        {
            if (!flag)
            {
                PaletteName = "Fur";
                ColorName = "None";
            }
        }
    }

    public class Texture : IDisposable
    {
        private bool disposed = false;
        public int bufferID = -1;
        public string name;
        public int width;
        public int height;
        public PixelInternalFormat pif;
        public bool containsAlphaMap;
        public PaletteOpt palOpt;
        public Vector4 procColor;
        public PixelFormat pf;
        //public DDSImage ddsImage;
        //Attach mask and normal textures to the diffuse
        public Texture mask;
        public Texture normal;

        //Empty Initializer
        public Texture() {}
        //Path Initializer
        public Texture(string path)
        {
            if (!File.Exists(path))
                throw new System.IO.FileNotFoundException();
            
            DDSImage ddsImage = new DDSImage(File.ReadAllBytes(Path.Combine(Model_Viewer.Util.dirpath, path)));
            name = path;
            Debug.WriteLine("Sampler Name Path " + path + " Width {0} Height {1}", ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            width = ddsImage.header.dwWidth;
            height = ddsImage.header.dwHeight;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = PixelInternalFormat.CompressedRgbaS3tcDxt1Ext;
                    containsAlphaMap = false;
                    break;
                case (0x35545844):
                    pif = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
                    containsAlphaMap = true;
                    break;
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }
            //Force RGBA for now
            pf = PixelFormat.Rgba;
            //Upload to GPU
            bufferID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, bufferID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

            GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, this.pif,
                this.width, this.height, 0, ddsImage.header.dwPitchOrLinearSize, ddsImage.bdata);

            ddsImage = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (bufferID != -1) GL.DeleteTexture(bufferID);
                if (mask != null) mask.Dispose();
                if (normal != null) normal.Dispose();
                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~Texture()
        {
            Dispose(false);
        }

    }

    public class Joint : model
    {
        private int vertex_buffer_object;
        private int element_buffer_object;
        public int jointIndex;
        public Vector3 color;

        public Joint()
        {
            //Create Buffers
            GL.GenBuffers(1, out vertex_buffer_object);
            //GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);
        }

        
        
        //Empty stuff
        public override model Clone(scene scn)
        {
            GMDL.Joint copy = new GMDL.Joint();
            copy.renderable = true; //Override Renderability
            copy.shader_programs = this.shader_programs;
            copy.type = this.type;
            copy.name = this.name;
            copy.ID = this.ID;
            copy.vertex_buffer_object = this.vertex_buffer_object;
            copy.element_buffer_object = this.element_buffer_object;
            copy.jointIndex = this.jointIndex;
            copy.color = this.color;
            
            //Copy Transformations
            copy.localPosition = this.localPosition;
            copy.localScale = this.localScale;
            copy.localRotation = this.localRotation;
            copy.scene = scn;

            //Clone Children as well
            foreach (GMDL.model child in this.children)
            {
                GMDL.model nChild = child.Clone(scn);
                nChild.parent = copy;
                copy.children.Add(nChild);
            }

            return copy;
        }

        //Render should render Bones from joint to children
        private void renderMain(int pass)
        {
            GL.UseProgram(pass);
            
            //Draw Lines to children joints
            List<Vector3> verts = new List<Vector3>();
            //List<int> indices = new List<int>();
            List<Vector3> colors = new List<Vector3>();
            int arraysize = this.children.Count * 2 * 3 * sizeof(float);
            int[] indices = new int[this.children.Count * 2];
            for (int i = 0; i < this.children.Count; i++)
            {
                verts.Add(this.worldPosition);
                verts.Add(children[i].worldPosition);
                ////Choosing red color for the skeleton
                colors.Add(new Vector3(1.0f, 0.0f, 0.0f));
                colors.Add(new Vector3(1.0f, 0.0f, 0.0f));
                //Use Random Color for Testing
                //colors.Add(color);
                //colors.Add(color);

                //Add line indices
                indices[2 * i] = 2 * i;
                indices[2 * i + 1] = 2 * i + 1;
            }

            float[] vertsf = new float[verts.Count * 3];
            float[] colorf = new float[colors.Count * 3];
            vectofloatArray(vertsf, verts);
            vectofloatArray(colorf, colors);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, vertsf);
            //Add vert color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, colorf);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * indices.Length), indices, BufferUsageHint.StaticDraw);

            //Render Immediately
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Color Attribute
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(1);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.PointSize(5.0f);

            GL.DrawArrays(PrimitiveType.Lines, 0, indices.Length);
            GL.DrawArrays(PrimitiveType.Points, 0, indices.Length);
            
            //Draw only Joint Point
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

        public override bool render(int pass)
        {
            if (this.renderable == false)
            {
                //Debug.WriteLine("Not Renderable");
                return false;
            }
            if (this.children.Count == 0)
                return false;

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    program = this.shader_programs[pass];
                    renderMain(program);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        //DIsposal
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                GL.DeleteBuffer(vertex_buffer_object);
                GL.DeleteBuffer(element_buffer_object);
                //Free other resources here
                base.Dispose(true);
            }

            //Free unmanaged resources
            disposed = true;
        }

    }

    public class Light : model
    {
        //I should expand the light properties here
        public float intensity = 1.0f;

        private int vertex_buffer_object;
        private int element_buffer_object;


        public Light() {
            GL.GenBuffers(1, out vertex_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);
        }

        private void renderMain(int pass)
        {
            GL.UseProgram(pass);

            //Draw Single Points
            float[] vertsf = new float[9];
            int[] indices = new int[1];
            indices[0] = 0;
            
            vertsf[0] = this.worldPosition.X;
            vertsf[1] = this.worldPosition.Y;
            vertsf[2] = this.worldPosition.Z;

            int arraysize = 3 * sizeof(float);
            
            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, vertsf);
            
            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * indices.Length), indices, BufferUsageHint.StaticDraw);

            //Render Immediately
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.PointSize(10.0f);

            GL.DrawArrays(PrimitiveType.Points, 0, indices.Length);

            //Draw only Joint Point
            GL.DisableVertexAttribArray(0);
        }

        public override bool render(int pass)
        {
            int program = this.shader_programs[pass];

            switch (pass)
            {
                case 0:
                    renderMain(program);
                    break;
                default:
                    break;      
            }

            return true;
        }

        public override GMDL.model Clone(GMDL.scene scene)
        {
            throw new NotImplementedException();
        }

        public void updatePosition(Vector3 newPosition)
        {
            this.localPosition = newPosition;
        }
    }


    //Animation Classes
    public class AnimNodeFrameData
    {
        public List<Quaternion> rotations = new List<Quaternion>();
        public List<Vector3> translations = new List<Vector3>();
        public List<Vector3> scales = new List<Vector3>();

        public void LoadRotations(FileStream fs,int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Quaternion q = new Quaternion();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                q.W = br.ReadSingle();

                this.rotations.Add(q);
            }
        }

        public void LoadTranslations(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new Vector3();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                br.ReadSingle();
                this.translations.Add(q);
            }
        }

        public void LoadScales(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new Vector3();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                br.ReadSingle();
                this.scales.Add(q);
            }
        }

    }


    public class AnimFrameData
    {
        public List<AnimNodeFrameData> frames = new List<AnimNodeFrameData>();
        public int frameCount;

        public void Load(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            this.frameCount = count;
            for (int i = 0; i < count; i++)
            {
                uint rotOff = (uint)fs.Position + br.ReadUInt32();
                fs.Seek(0x4, SeekOrigin.Current);
                int rotCount = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                uint transOff = (uint)fs.Position + br.ReadUInt32();
                fs.Seek(0x4, SeekOrigin.Current);
                int transCount = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                uint scaleOff = (uint)fs.Position + br.ReadUInt32();
                fs.Seek(0x4, SeekOrigin.Current);
                int scaleCount = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                long back = fs.Position;

                AnimNodeFrameData frame = new AnimNodeFrameData();
                fs.Seek(rotOff, SeekOrigin.Begin);
                frame.LoadRotations(fs, rotCount);
                fs.Seek(transOff, SeekOrigin.Begin);
                frame.LoadTranslations(fs, transCount);
                fs.Seek(scaleOff, SeekOrigin.Begin);
                frame.LoadScales(fs, scaleCount);

                fs.Seek(back, SeekOrigin.Begin);

                this.frames.Add(frame);

            }
        }
    }
    public class AnimeNode
    {
        public int index;
        public string name = "";
        public bool canCompress = false;
        public int rotIndex = 0;
        public int transIndex = 0;
        public int scaleIndex = 0;


        public AnimeNode(int fIndex)
        {
            this.index = fIndex;
        }
        public void Load(FileStream fs)
        {
            //Binary reader
            BinaryReader br = new BinaryReader(fs);
            char[] charbuffer = new char[0x100];

            charbuffer = br.ReadChars(0x10);
            name = (new string(charbuffer)).Trim('\0');
            canCompress = (br.ReadInt32()==0) ? false : true;
            rotIndex = br.ReadInt32();
            transIndex = br.ReadInt32();
            scaleIndex = br.ReadInt32();
        }
    }
    public class NodeData
    {
        public List<AnimeNode> nodeList = new List<AnimeNode>();
        public int nodeCount = 0;

        public void parseNodes(FileStream fs, int count)
        {
            nodeCount = count;

            for (int i = 0; i < count; i++)
            {
                AnimeNode node = new AnimeNode(i);
                node.Load(fs);
                nodeList.Add(node);
            }
        }

    }

    public class AnimeMetaData
    {
        public int nodeCount = 0;
        public int frameCount = 0;
        public NodeData nodeData = new NodeData();
        public AnimFrameData frameData = new AnimFrameData();

        public void Load(FileStream fs)
        {
            //Binary Reader
            BinaryReader br = new BinaryReader(fs);
            fs.Seek(0x60, SeekOrigin.Begin);
            frameCount = br.ReadInt32();
            nodeCount = br.ReadInt32();

            //Get Offsets
            uint nodeOffset = (uint)fs.Position + br.ReadUInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            uint animeFrameDataOff = (uint)fs.Position + br.ReadUInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            uint staticFrameOff = (uint)fs.Position;

            Debug.WriteLine("Animation File");
            Debug.WriteLine("Frames {0} Nodes {1}", frameCount, nodeCount);
            Debug.WriteLine("Parsing Nodes NodeOffset {0}", nodeOffset);

            fs.Seek(nodeOffset, SeekOrigin.Begin);
            NodeData nodedata = new NodeData();
            nodedata.parseNodes(fs, nodeCount);
            nodeData = nodedata;

            Debug.WriteLine("Parsing Animation Frame Data Offset {0}", animeFrameDataOff);
            fs.Seek(animeFrameDataOff, SeekOrigin.Begin);
            AnimFrameData framedata = new AnimFrameData();
            framedata.Load(fs, frameCount);
            this.frameData = framedata;

        }

    }

    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
        public Vector3 BindTranslate;
        public Quaternion BindRotation;
        public Vector3 Bindscale;

        
        public void Load(FileStream fs)
        {
            //Binary Reader
            BinaryReader br = new BinaryReader(fs);
            //Lamest way to read a matrix
            invBindMatrix.M11 = br.ReadSingle();
            invBindMatrix.M12 = br.ReadSingle();
            invBindMatrix.M13 = br.ReadSingle();
            invBindMatrix.M14 = br.ReadSingle();
            invBindMatrix.M21 = br.ReadSingle();
            invBindMatrix.M22 = br.ReadSingle();
            invBindMatrix.M23 = br.ReadSingle();
            invBindMatrix.M24 = br.ReadSingle();
            invBindMatrix.M31 = br.ReadSingle();
            invBindMatrix.M32 = br.ReadSingle();
            invBindMatrix.M33 = br.ReadSingle();
            invBindMatrix.M34 = br.ReadSingle();
            invBindMatrix.M41 = br.ReadSingle();
            invBindMatrix.M42 = br.ReadSingle();
            invBindMatrix.M43 = br.ReadSingle();
            invBindMatrix.M44 = br.ReadSingle();
            //transpose the matrix
            //invBindMatrix.Transpose();
            //invBindMatrix.Invert();
            
            //Get Translate
            BindTranslate.X = br.ReadSingle();
            BindTranslate.Y = br.ReadSingle();
            BindTranslate.Z = br.ReadSingle();
            //Get Quaternion
            BindRotation.X = br.ReadSingle();
            BindRotation.Y = br.ReadSingle();
            BindRotation.Z = br.ReadSingle();
            BindRotation.W = br.ReadSingle();
            //Get Scale
            Bindscale.X = br.ReadSingle();
            Bindscale.Y = br.ReadSingle();
            Bindscale.Z = br.ReadSingle();

        }

        public float[] convertMat()
        {
            float[] fmat = new float[16];

            fmat[0] = this.invBindMatrix.M11;
            fmat[1] = this.invBindMatrix.M12;
            fmat[2] = this.invBindMatrix.M13;
            fmat[3] = this.invBindMatrix.M14;
            fmat[4] = this.invBindMatrix.M21;
            fmat[5] = this.invBindMatrix.M22;
            fmat[6] = this.invBindMatrix.M23;
            fmat[7] = this.invBindMatrix.M24;
            fmat[8] = this.invBindMatrix.M31;
            fmat[9] = this.invBindMatrix.M32;
            fmat[10] = this.invBindMatrix.M33;
            fmat[11] = this.invBindMatrix.M34;
            fmat[12] = this.invBindMatrix.M41;
            fmat[13] = this.invBindMatrix.M42;
            fmat[14] = this.invBindMatrix.M43;
            fmat[15] = this.invBindMatrix.M44;

            return fmat;
        }

    }

}



