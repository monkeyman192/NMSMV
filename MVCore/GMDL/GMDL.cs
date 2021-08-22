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
using Model_Viewer;
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

    public class SimpleSampler
    {
        public string PName { get; set; }
        SimpleSampler()
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class gizmo: Model
    {
        public GLInstancedMeshVao meshVao;
        public gizmo()
        {
            type = TYPES.GIZMO;
            
            //Assemble geometry in the constructor
            meshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_translation_gizmo"];
            instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);
        }

        public override Model Clone()
        {
            return new gizmo();
        }
    }

    

    
    [StructLayout(LayoutKind.Explicit)]
    struct CustomPerMaterialUniforms
    {
        [FieldOffset(0)] //256 Bytes
        public unsafe fixed int matflags[64];
        [FieldOffset(256)] //64 Bytes
        public int diffuseTex;
        [FieldOffset(260)] //4 bytes
        public int maskTex;
        [FieldOffset(264)] //4 bytes
        public int normalTex;
        [FieldOffset(276)] //16 bytes
        public Vector4 gMaterialColourVec4;
        [FieldOffset(292)] //16 bytes
        public Vector4 gMaterialParamsVec4;
        [FieldOffset(308)] //16 bytes
        public Vector4 gMaterialSFXVec4;
        [FieldOffset(324)] //16 bytes
        public Vector4 gMaterialSFXColVec4;
        [FieldOffset(340)] //16 bytes
        public Vector4 gDissolveDataVec4;
        [FieldOffset(356)] //16 bytes
        public Vector4 gCustomParams01Vec4;
        
        public static readonly int SizeInBytes = 360;
    };

    public class Collision : Model
    {
        public COLLISIONTYPES collisionType;
        public GeomObject gobject;
        public MeshMetaData metaData = new MeshMetaData();
        public GLInstancedMeshVao meshVao;

        //Custom constructor
        public Collision()
        {
            
        }

        public override Model Clone()
        {
            Collision new_m = new Collision();
            new_m.collisionType = collisionType;
            new_m.copyFrom(this);

            new_m.meshVao = this.meshVao;
            new_m.instanceId = GLMeshBufferManager.AddInstance(ref new_m.meshVao, new_m);
            
            //Clone children
            foreach (Model child in children)
            {
                Model new_child = child.Clone();
                new_child.parent = new_m;
                new_m.children.Add(new_child);
            }

            return new_m;
        }
        
        
        protected Collision(Collision input) : base(input)
        {
            collisionType = input.collisionType;
        }

        public override void update()
        {
            base.update();

        }

        public override void updateMeshInfo(bool lod_filter=false)
        {
            if (renderable)
            {
                instanceId = GLMeshBufferManager.AddInstance(ref meshVao, this);
                base.updateMeshInfo(lod_filter);
                return;
            }

            base.updateMeshInfo(lod_filter);
        }

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
        public List<int[]> bIndices = new List<int[]>();
        public List<float[]> bWeights = new List<float[]>();
        public List<bufInfo> bufInfo = new();
        public int[] offsets; //List to save strides according to meshdescr
        public int[] small_offsets; //Same thing for the small description
        public short[] boneRemap;
        public List<Vector3[]> bboxes = new List<Vector3[]>();
        public List<Vector3> bhullverts = new List<Vector3>();
        public List<int> bhullstarts = new List<int>();
        public List<int> bhullends = new List<int>();
        public List<int[]> bhullindices = new List<int[]>();
        public List<int> vstarts = new List<int>();
        public Dictionary<ulong, geomMeshMetaData> meshMetaDataDict = new Dictionary<ulong, geomMeshMetaData>();
        public Dictionary<ulong, geomMeshData> meshDataDict = new Dictionary<ulong, geomMeshData>();

        //Joint info
        public int jointCount;
        public List<JointBindingData> jointData = new();
        public float[] invBMats = new float[256 * 16];

        //Dictionary with the compiled VAOs belonging on this gobject
        private Dictionary<ulong, GLVao> GLVaos = new();
        //Dictionary to index 
        private Dictionary<ulong, Dictionary<string, GLInstancedMeshVao>> GLMeshVaos = new();



        public Vector3 get_vec3_half(BinaryReader br)
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

        public Vector2 get_vec2_half(BinaryReader br)
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
        public GLInstancedMeshVao findGLMeshVao(string material_name, ulong hash)
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
        public bool saveGLMeshVAO(ulong hash, string matname, GLInstancedMeshVao meshVao)
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
                GLMeshVaos[hash] = new Dictionary<string, GLInstancedMeshVao>();
                
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
        public GLVao generateVAO(Mesh so)
        {
            //Generate VAO
            GLVao vao = new GLVao();
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
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) meshMetaDataDict[so.metaData.Hash].vs_size,
                meshDataDict[so.metaData.Hash].vs_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vx_size * (so.metaData.vertrend_graphics + 1))
            {
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
                Callbacks.showError("Mesh metadata does not match the vertex buffer size from the geometry file",
                    "Error");
            }
                
            RenderStats.vertNum += so.metaData.vertrend_graphics + 1; //Accumulate settings

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
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) meshMetaDataDict[so.metaData.Hash].is_size, 
                meshDataDict[so.metaData.Hash].is_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != meshMetaDataDict[so.metaData.Hash].is_size)
            {
                Callbacks.showError("Mesh metadata does not match the index buffer size from the geometry file", "Error");
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
            }

            RenderStats.trisNum += (int) (so.metaData.batchcount / 3); //Accumulate settings

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

            float[] vx_buffer_float = new float[(metaData.boundhullend - metaData.boundhullstart) * 3];

            for (int i = 0; i < metaData.boundhullend - metaData.boundhullstart; i++)
            {
                Vector3 v = bhullverts[i + metaData.boundhullstart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GeomObject temp_geom = new GeomObject();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = metaData.batchcount;
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
            temp_geom.ibuffer = new byte[temp_geom.indicesLength * metaData.batchcount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

            System.Buffer.BlockCopy(ibuffer, metaData.batchstart_physics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


            return temp_geom.generateVAO();
        }

        public GLVao generateVAO()
        {

            GLVao vao = new GLVao();

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
                    foreach (Dictionary<string, GLInstancedMeshVao> p in GLMeshVaos.Values)
                    {
                        foreach (GLInstancedMeshVao m in p.Values)
                            m.Dispose(); 
                        p.Clear();
                        //Materials are stored globally
                    }
                
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
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


    public class Sampler : TkMaterialSampler, IDisposable
    {
        public MyTextureUnit texUnit;
        public Texture tex;
        public TextureManager texMgr; //For now it should be inherited from the scene. In the future I can use a delegate
        public bool isProcGen = false;

        //Override Properties
        public string PName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }

        public string PMap
        {
            get
            {
                return Map;
            }
            set
            {
                Map = value;
            }
        }

        public Sampler()
        {

        }

        public Sampler(TkMaterialSampler ms)
        {
            //Pass everything here because there is no base copy constructor in the NMS template
            this.Name = "mpCustomPerMaterial." + ms.Name;
            this.Map = ms.Map;
            this.IsCube = ms.IsCube;
            this.IsSRGB = ms.IsSRGB;
            this.UseCompression = ms.UseCompression;
            this.UseMipMaps = ms.UseMipMaps;
        }

        public Sampler Clone()
        {
            Sampler newsampler = new Sampler();

            newsampler.PName = PName;
            newsampler.PMap = PMap;
            newsampler.texMgr = texMgr;
            newsampler.tex = tex;
            newsampler.texUnit = texUnit;
            newsampler.TextureAddressMode = TextureAddressMode;
            newsampler.TextureFilterMode = TextureFilterMode;

            return newsampler;
        }


        public void init(TextureManager input_texMgr)
        {
            texMgr = input_texMgr;
            texUnit = new MyTextureUnit(Name);

            //Save texture to material
            switch (Name)
            {
                case "mpCustomPerMaterial.gDiffuseMap":
                case "mpCustomPerMaterial.gDiffuse2Map":
                case "mpCustomPerMaterial.gMasksMap":
                case "mpCustomPerMaterial.gNormalMap":
                    prepTextures();
                    break;
                default:
                    Callbacks.Log("Not sure how to handle Sampler " + Name, LogVerbosityLevel.WARNING);
                    break;
            }
        }


        public void prepTextures()
        {
            string[] split = Map.Value.Split('.');

            string temp = "";
            if (Name == "mpCustomPerMaterial.gDiffuseMap")
            {
                //Check if the sampler describes a proc gen texture
                temp = split[0] + ".";
                //Construct main filename

                string texMbin = temp + "TEXTURE.MBIN";
                
                //Detect Procedural Texture
                if (RenderState.activeResMgr.NMSFileToArchiveMap.Keys.Contains(texMbin))
                {
                    TextureMixer.combineTextures(Map, Palettes.paletteSel, ref texMgr);
                    //Override Map
                    Map = temp + "DDS";
                    isProcGen = true;
                }
            }

            //Load the texture to the sampler
            loadTexture();
        }


        private void loadTexture()
        {
            if (Map == "")
                return;

            //Try to load the texture
            if (texMgr.HasTexture(Map))
            {
                tex = texMgr.GetTexture(Map);
            }
            else
            {
                tex = new Texture(Map);
                tex.palOpt = new PaletteOpt(false);
                tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                //At this point this should be a common texture. Store it to the master texture manager
                RenderState.activeResMgr.texMgr.AddTexture(tex);
            }
        }
        
        public static void dump_texture(string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.GetTexImage(TextureTarget.Texture2DArray, 0, PixelFormat.Rgba, PixelType.Byte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                        (int)pixels[4 * (width * i + j) + 0],
                        (int)pixels[4 * (width * i + j) + 1],
                        (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }

        public static void dump_texture_fb(string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                        (int)pixels[4 * (width * i + j) + 0],
                        (int)pixels[4 * (width * i + j) + 1],
                        (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }


        public static int generate2DTexture(PixelInternalFormat fmt, int w, int h, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex_id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, fmt, w, h, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static int generateTexture2DArray(PixelInternalFormat fmt, int w, int h, int d, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, tex_id);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, fmt, w, h, d, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static void generateTexture2DMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public static void generateTexture2DArrayMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
        }

        public static void setupTextureParameters(TextureTarget texTarget, int texture, int wrapMode, int magFilter, int minFilter, float af_amount)
        {

            GL.BindTexture(texTarget, texture);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapS, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapT, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureMagFilter, magFilter);
            GL.TexParameter(texTarget, TextureParameterName.TextureMinFilter, minFilter);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);

            //Use anisotropic filtering
            af_amount = Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(texTarget, (TextureParameterName)0x84FE, af_amount);
        }



#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls



        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //Texture lists should have been disposed from the dictionary
                    //Free other resources here
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
#endregion


    }

    
    public class Uniform: TkMaterialUniform
    {
        public MVector4 vec;
        private string prefix;
        
        public Uniform()
        {
            prefix = "";
            vec = new MVector4(0.0f);
        }

        public Uniform(string name)
        {
            prefix = "";
            PName = name;
            vec = new MVector4(0.0f);
        }

        public Uniform(TkMaterialUniform un)
        {
            //Copy Attributes
            Name = un.Name;
            vec = new MVector4(un.Values.x, un.Values.y, un.Values.z, un.Values.t);
        }

        public Uniform(string pref, TkMaterialUniform un) : this(un)
        {
            prefix = pref;
            Name = prefix + un.Name;
        }

        public void setPrefix(string pref)
        {
            prefix = pref;
        }

        public string PName
        {
            get { return Name; }
            set { Name = value; }
        }

        public MVector4 Vec
        {
            get {
                return vec;
            }

            set
            {
                vec = value;
            }
        }

    }

    public class MVector4: INotifyPropertyChanged
    {
        public Vector4 vec4;

        public MVector4(Vector4 v)
        {
            vec4 = v;
        }

        public MVector4(float x , float y, float z, float w)
        {
            vec4 = new Vector4(x, y, z, w);
        }

        public MVector4(float x)
        {
            vec4 = new Vector4(x);
        }

        //Properties
        public Vector4 Vec
        {
            get { return vec4; }
            set { 
                vec4 = value; 
                RaisePropertyChanged("Vec"); 
            }
        }
        public float X
        {
            get { return vec4.X; }
            set { 
                vec4.X = value; 
                RaisePropertyChanged("X"); 
            }
        }
        public float Y
        {
            get { return vec4.Y; }
            set { vec4.Y = value; RaisePropertyChanged("Y"); }
        }

        public float Z
        {
            get { return vec4.Z; }
            set { vec4.Z = value; RaisePropertyChanged("Z"); }
        }

        public float W
        {
            get { return vec4.W; }
            set { vec4.W = value; RaisePropertyChanged("W"); }
        }

        public static MVector4 operator -(MVector4 a, MVector4 b)
        {
            return new MVector4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
        }


        //Property Change Callbacks
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        
    }

    public class MatOpts
    {
        public int transparency;
        public bool castshadow;
        public bool disableTestz;
        public string link;
        public string shadername;
    }

    public class MyTextureUnit
    {
        public OpenTK.Graphics.OpenGL4.TextureUnit texUnit;

        public static Dictionary<string, TextureUnit> MapTextureUnit = new Dictionary<string, TextureUnit> {
            { "mpCustomPerMaterial.gDiffuseMap" , TextureUnit.Texture0 },
            { "mpCustomPerMaterial.gMasksMap" ,   TextureUnit.Texture1 },
            { "mpCustomPerMaterial.gNormalMap" ,  TextureUnit.Texture2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , TextureUnit.Texture3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", TextureUnit.Texture4},
            { "mpCustomPerMaterial.gDetailNormalMap", TextureUnit.Texture5}
        };

        public static Dictionary<string, int> MapTexUnitToSampler = new Dictionary<string, int> {
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
                    NMSUtils.fetchRotQuaternion(node, this, i, ref anim_rotations[node.Node][i]); //use Ref
                    NMSUtils.fetchTransVector(node, this, i, ref anim_positions[node.Node][i]); //use Ref
                    NMSUtils.fetchScaleVector(node, this, i, ref anim_scales[node.Node][i]); //use Ref
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

            //Calculate Binding Matrix
            Vector3 BindTranslate, BindScale;
            Quaternion BindRotation = new Quaternion();

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

        
        public float[] convertVec(Vector3 vec)
        {
            float[] fmat = new float[3];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            
            return fmat;
        }

        public float[] convertVec(Vector4 vec)
        {
            float[] fmat = new float[4];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            fmat[3] = vec.W;

            return fmat;
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