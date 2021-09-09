//#define DUMP_TEXTURES

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using KUtility;
using System.Linq;
using libMBIN.NMS.Toolkit;
using System.ComponentModel;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Runtime.InteropServices;
using GLSLHelper;
using MVCore.Common;
using MVCore.Utils;

namespace MVCore
{   
    public enum COLLISIONTYPES
    {
        MESH = 0x0,
        SPHERE,
        CYLINDER,
        BOX,
        CAPSULE    
    }

    public enum RENDERPASS
    {
        DEFERRED = 0x0,
        FORWARD,
        DECAL,
        BHULL,
        BBOX,
        DEBUG,
        PICK,
        COUNT
    }

    public class geomMeshMetaData
    {
        public string name;
        public ulong hash;
        public uint vs_size;
        public uint vs_abs_offset;
        public uint is_size;
        public uint is_abs_offset;
    }

    public class geomMeshData
    {
        public ulong hash;
        public byte[] vs_buffer;
        public byte[] is_buffer;
    }

    public class GeomObject : IDisposable
    {
        public string mesh_descr;
        public string small_mesh_descr;

        public bool interleaved;
        public int vx_size;
        public int small_vx_size;

        //Counters
        public int indicesCount=0;
        public int indicesLength = 0;
        public DrawElementsType indicesLengthType;
        public int vertCount = 0;

        //make sure there are enough buffers for non interleaved formats
        public byte[] ibuffer;
        public int[] ibuffer_int;
        public byte[] vbuffer;
        public byte[] small_vbuffer;
        public byte[] cbuffer;
        public byte[] nbuffer;
        public byte[] ubuffer;
        public byte[] tbuffer;
        public List<int[]> bIndices = new();
        public List<float[]> bWeights = new();
        public List<bufInfo> bufInfo = new();
        public int[] offsets; //List to save strides according to meshdescr
        public int[] small_offsets; //Same thing for the small description
        public short[] boneRemap;
        public List<Vector3[]> bboxes = new();
        public List<Vector3> bhullverts = new();
        public List<int> bhullstarts = new();
        public List<int> bhullends = new();
        public List<int[]> bhullindices = new();
        public List<int> vstarts = new();
        public Dictionary<ulong, geomMeshMetaData> meshMetaDataDict = new();
        public Dictionary<ulong, geomMeshData> meshDataDict = new();

        //Joint info
        public int jointCount;
        public List<JointBindingData> jointData = new();
        public float[] invBMats = new float[256 * 16];

        //Dictionary with the compiled VAOs belonging on this gobject
        private readonly Dictionary<ulong, GLVao> GLVaos = new();
        //Dictionary to index 
        private readonly Dictionary<ulong, Dictionary<string, GLInstancedMesh>> GLMeshVaos = new();



        public static Vector3 get_vec3_half(BinaryReader br)
        {
            Vector3 temp;
            //Get Values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            uint val3 = br.ReadUInt16();
            //Convert Values
            temp.X = Utils.Half.decompress(val1);
            temp.Y = Utils.Half.decompress(val2);
            temp.Z = Utils.Half.decompress(val3);
            //Console.WriteLine("half {0} {1} {2}", temp[0],temp[1],temp[2]);
            return temp;
        }

        public static Vector2 get_vec2_half(BinaryReader br)
        {
            Vector2 temp;
            //Get values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            //Convert Values
            temp.X = Utils.Half.decompress(val1);
            temp.Y = Utils.Half.decompress(val2);
            return temp;
        }





        //Fetch Meshvao from dictionary
        public GLInstancedMesh findGLMeshVao(string material_name, ulong hash)
        {
            if (GLMeshVaos.ContainsKey(hash))
                if (GLMeshVaos[hash].ContainsKey(material_name))
                    return GLMeshVaos[hash][material_name];
                
            return null;
        }

        //Fetch Meshvao from dictionary
        public GLVao findVao(ulong hash)
        {
            if (GLVaos.ContainsKey(hash))
                return GLVaos[hash];
            return null;
        }

        //Save GLMeshVAO to gobject
        public bool saveGLMeshVAO(ulong hash, string matname, GLInstancedMesh meshVao)
        {
            if (GLMeshVaos.ContainsKey(hash))
            {
                if (GLMeshVaos[hash].ContainsKey(matname))
                {
                    Console.WriteLine("MeshVao already in the dictionary, nothing to do...");
                    return false;
                }
            }
            else
                GLMeshVaos[hash] = new Dictionary<string, GLInstancedMesh>();
                
            GLMeshVaos[hash][matname] = meshVao;

            return true;

        }

        //Save VAO to gobject
        public bool saveVAO(ulong hash, GLVao vao)
        {
            //Double check tha the VAO is not already in the dictinary
            if (GLVaos.ContainsKey(hash))
            {
                Console.WriteLine("Vao already in the dictinary, nothing to do...");
                return false;
            }
                
            //Save to dictionary
            GLVaos[hash] = vao;
            return true;
        }

        //Fetch main VAO
        public GLVao generateVAO(MeshMetaData md)
        {
            //Generate VAO
            GLVao vao = new();
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];
            
            //Bind vertex buffer
            int size;
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            //Upload Vertex Buffer
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) meshMetaDataDict[md.Hash].vs_size,
                meshDataDict[md.Hash].vs_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vx_size * (md.VertrEndGraphics + 1))
            {
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
                Callbacks.showError("Mesh metadata does not match the vertex buffer size from the geometry file",
                    "Error");
            }
                
            RenderStats.vertNum += md.VertrEndGraphics + 1; //Accumulate settings

            //Assign VertexAttribPointers
            for (int i = 0; i < 7; i++)
            {
                if (bufInfo[i] == null) continue;
                bufInfo buf = bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, buf.normalize, vx_size, buf.offset);
                GL.EnableVertexAttribArray(i);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) meshMetaDataDict[md.Hash].is_size, 
                meshDataDict[md.Hash].is_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != meshMetaDataDict[md.Hash].is_size)
            {
                Callbacks.showError("Mesh metadata does not match the index buffer size from the geometry file", "Error");
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
            }

            RenderStats.trisNum += (int) (md.BatchCount / 3); //Accumulate settings

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
        }

        public GLVao getCollisionMeshVao(MeshMetaData metaData)
        {
            //Collision Mesh isn't used anywhere else.
            //No need to check for hashes and shit

            float[] vx_buffer_float = new float[(metaData.BoundHullEnd - metaData.BoundHullStart) * 3];

            for (int i = 0; i < metaData.BoundHullEnd - metaData.BoundHullStart; i++)
            {
                Vector3 v = bhullverts[i + metaData.BoundHullStart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GeomObject temp_geom = new();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = metaData.BatchCount;
            temp_geom.indicesLength = indicesLength; 

            //Set Strides
            temp_geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            temp_geom.offsets = new int[7];
            temp_geom.bufInfo = new();

            for (int i = 0; i < 7; i++)
            {
                temp_geom.bufInfo.Add(null);
                temp_geom.offsets[i] = -1;
            }

            temp_geom.mesh_descr = "vn";
            temp_geom.offsets[0] = 0;
            temp_geom.offsets[2] = 0;
            temp_geom.bufInfo[0] = new bufInfo(0, VertexAttribPointerType.Float, 3, 0, 0, "vPosition", false);
            temp_geom.bufInfo[2] = new bufInfo(2, VertexAttribPointerType.Float, 3, 0, 0, "nPosition", false);

            //Set Buffers
            temp_geom.ibuffer = new byte[temp_geom.indicesLength * metaData.BatchCount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

            System.Buffer.BlockCopy(ibuffer, metaData.BatchStartPhysics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


            return temp_geom.generateVAO();
        }

        public GLVao generateVAO()
        {

            GLVao vao = new();

            //Generate VAO
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            ErrorCode err = GL.GetError();
            if (err != ErrorCode.NoError)
                Console.WriteLine(GL.GetError());
            
            //Bind vertex buffer
            int size;
            //Upload Vertex Buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, vbuffer.Length,
                vbuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vbuffer.Length)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, ibuffer.Length,
                ibuffer, BufferUsageHint.StaticDraw);

            //Assign VertexAttribPointers
            for (int i = 0; i < 7; i++)
            {
                if (this.bufInfo[i] == null) continue;
                bufInfo buf = this.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, buf.stride, buf.offset);
                GL.EnableVertexAttribArray(i);
            }

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
        }


#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    ibuffer = null;
                    vbuffer = null;
                    small_vbuffer = null;
                    offsets = null;
                    small_offsets = null;
                    boneRemap = null;
                    invBMats = null;
                    
                    bIndices.Clear();
                    bWeights.Clear();
                    bufInfo.Clear();
                    bboxes.Clear();
                    bhullverts.Clear();
                    vstarts.Clear();
                    jointData.Clear();

                    //Clear buffers
                    foreach (KeyValuePair<ulong, geomMeshMetaData> pair in meshMetaDataDict)
                        meshDataDict[pair.Key] = null;

                    meshDataDict.Clear();
                    meshMetaDataDict.Clear();

                    //Clear Vaos
                    foreach (GLVao p in GLVaos.Values)
                        p.Dispose();
                    GLVaos.Clear();

                    //Dispose GLmeshes
                    foreach (Dictionary<string, GLInstancedMesh> p in GLMeshVaos.Values)
                    {
                        foreach (GLInstancedMesh m in p.Values)
                            m.Dispose(); 
                        p.Clear();
                        //Materials are stored globally
                    }
                
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() => Dispose(true);
        #endregion


    }

    public class bufInfo
    {
        public int semantic;
        public VertexAttribPointerType type;
        public int count;
        public int stride;
        public int offset;
        public string sem_text;
        public bool normalize;

        public bufInfo(int sem,VertexAttribPointerType typ, int c, int s, int off, string t, bool n)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
            normalize = n;
            offset = off;
        }
    }


    

    
    
    public class MyTextureUnit
    {
        public TextureUnit texUnit;

        public static Dictionary<string, TextureUnit> MapTextureUnit = new() {
            { "mpCustomPerMaterial.gDiffuseMap" , TextureUnit.Texture0 },
            { "mpCustomPerMaterial.gMasksMap" ,   TextureUnit.Texture1 },
            { "mpCustomPerMaterial.gNormalMap" ,  TextureUnit.Texture2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , TextureUnit.Texture3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", TextureUnit.Texture4},
            { "mpCustomPerMaterial.gDetailNormalMap", TextureUnit.Texture5}
        };

        public static Dictionary<string, int> MapTexUnitToSampler = new() {
            { "mpCustomPerMaterial.gDiffuseMap" , 0 },
            { "mpCustomPerMaterial.gMasksMap" ,   1 },
            { "mpCustomPerMaterial.gNormalMap" ,  2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , 3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", 4},
            { "mpCustomPerMaterial.gDetailNormalMap", 5}
        };

        public MyTextureUnit(string sampler_name)
        {
            texUnit = MapTextureUnit[sampler_name];
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

    
    
    //Animation Classes

    
    public class AnimNodeFrameData
    {
        public List<Quaternion> rotations = new();
        public List<Vector3> translations = new();
        public List<Vector3> scales = new();

        public void LoadRotations(FileStream fs,int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                Quaternion q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                    W = br.ReadSingle()
                };
                
                rotations.Add(q);
            }
        }

        public void LoadTranslations(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                };
                br.ReadSingle();
                translations.Add(q);
            }
        }

        public void LoadScales(FileStream fs, int count)
        {
            BinaryReader br = new(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new()
                {
                    X = br.ReadSingle(),
                    Y = br.ReadSingle(),
                    Z = br.ReadSingle(),
                };
                br.ReadSingle();
                this.scales.Add(q);
            }
        }

    }
    

    public class AnimPoseData: TkAnimPoseData
    {

        public AnimPoseData(TkAnimPoseData apd)
        {
            Anim = apd.Anim;
            FrameStart = apd.FrameStart;
            FrameEnd = apd.FrameEnd;
            PActivePoseFrame = (int) ((apd.FrameEnd - apd.FrameStart) / 2 + apd.FrameStart);
        }

        public string PAnim
        {
            get
            {
                return Anim;
            }
        }

        public int PFrameStart
        {
            get
            {
                return FrameStart;
            }
            set
            {
                FrameStart = value;
            }
        }

        public int PFrameEnd
        {
            get
            {
                return FrameEnd;
            }
            set
            {
                FrameEnd = value;
            }
        }

        public int PActivePoseFrame
        {
            get; set;
        }



    }

    
    public class AnimMetadata: TkAnimMetadata
    {
        public float duration;
        public Dictionary<string, Quaternion[]> anim_rotations;
        public Dictionary<string, Vector3[]> anim_positions;
        public Dictionary<string, Vector3[]> anim_scales;

        public AnimMetadata(TkAnimMetadata amd)
        {
            //Copy struct info
            FrameCount = amd.FrameCount;
            NodeCount = amd.NodeCount;
            NodeData = amd.NodeData;
            AnimFrameData = amd.AnimFrameData;
            StillFrameData = amd.StillFrameData;

            duration = FrameCount * 1000.0f;
        }

        public AnimMetadata()
        {
            duration = 0.0f;
        }

        public void load()
        {
            //Init dictionaries
            anim_rotations = new Dictionary<string, Quaternion[]>();
            anim_positions = new Dictionary<string, Vector3[]>();
            anim_scales = new Dictionary<string, Vector3[]>();

            loadData();
        }

        private void loadData()
        {
            for (int j = 0; j < NodeCount; j++)
            {
                TkAnimNodeData node = NodeData[j];
                //Init dictionary entries

                anim_rotations[node.Node] = new Quaternion[FrameCount];
                anim_positions[node.Node] = new Vector3[FrameCount];
                anim_scales[node.Node] = new Vector3[FrameCount];

                for (int i = 0; i < FrameCount; i++)
                {
                    Import.NMS.Util.fetchRotQuaternion(node, this, i, ref anim_rotations[node.Node][i]); //use Ref
                    Import.NMS.Util.fetchTransVector(node, this, i, ref anim_positions[node.Node][i]); //use Ref
                    Import.NMS.Util.fetchScaleVector(node, this, i, ref anim_scales[node.Node][i]); //use Ref
                }
            }
        }
    }

    
    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
        public Matrix4 BindMatrix = Matrix4.Identity;

        public void Load(Stream fs)
        {
            //Binary Reader
            BinaryReader br = new(fs);
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

            //Calculate Binding Matrix
            Vector3 BindTranslate, BindScale;
            Quaternion BindRotation = new();

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
            BindScale.X = br.ReadSingle();
            BindScale.Y = br.ReadSingle();
            BindScale.Z = br.ReadSingle();

            //Generate Matrix
            BindMatrix = Matrix4.CreateScale(BindScale) * Matrix4.CreateFromQuaternion(BindRotation) * Matrix4.CreateTranslation(BindTranslate);

            //Check Results [Except from Joint 0, the determinant of the multiplication is always 1,
            // transforms should be good]
            //Console.WriteLine((BindMatrix * invBindMatrix).Determinant);
        }

        
        

    }
}