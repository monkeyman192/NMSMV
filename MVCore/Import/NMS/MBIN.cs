using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Globalization;
using OpenTK;
using OpenTK.Mathematics;
using Model_Viewer;
using libMBIN;
using libMBIN.NMS.Toolkit;
using System.Linq;
using MVCore;
using Console = System.Console;
using MVCore.Utils;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS;
using System.CodeDom;
using MVCore.Common;


namespace MVCore.Import.NMS
{
    public static class NMSMaterialUtils
    {
        public static MeshMaterial Parse(string path, TextureManager input_texMgr)
        {
            //Load template
            //Try to use libMBIN to load the Material files
            TkMaterialData template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkMaterialData;
#if DEBUG
            //Save NMSTemplate to exml
            template.WriteToExml("Temp\\" + template.Name + ".exml");
#endif

            //Make new material based on the template
            MeshMaterial mat = CreateMaterialFromStruct(template, input_texMgr);
            
            mat.texMgr = input_texMgr;
            mat.init();
            return mat;
        }

        public static Sampler CreateSamplerFromStruct(TkMaterialSampler ms, TextureManager texMgr)
        {
            Sampler sam = new()
            {
                
            };

            sam.init(texMgr);
            return sam;
        }
        
        public static MeshMaterial CreateMaterialFromStruct(TkMaterialData md, TextureManager texMgr)
        {
            MeshMaterial mat = new()
            {
                Name = md.Name,
                Class = md.Class
            };
            
            //Copy flags and uniforms

            for (int i = 0; i < md.Flags.Count; i++)
                mat.add_flag((MaterialFlagEnum) md.Flags[i].MaterialFlag);

            
            //Get Samplers
            for (int i = 0; i < md.Samplers.Count; i++)
            {
                TkMaterialSampler ms = md.Samplers[i];
                Sampler s = CreateSamplerFromStruct(md.Samplers[i], texMgr);
                s.Name = md.Samplers[i].Name;
                s.Map = md.Samplers[i].Map;
                
                mat.Samplers.Add(s);
            }
            
            //Get Uniforms
            for (int i = 0; i < md.Uniforms.Count; i++)
            {
                TkMaterialUniform mu = md.Uniforms[i];
                Uniform uf = new("mpCustomPerMaterial." + mu.Name);
                uf.Name = mu.Name;
                uf.Values = new(mu.Values.x,
                                        mu.Values.y,
                                        mu.Values.z,
                                        mu.Values.t);
                mat.Uniforms.Add(uf);
            }

                
            return mat;
        }
    
    }
    
    
    
    
}

//TODO move to MVCore.Import.NMS
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

    public static class GEOMMBIN {

        public static GeomObject Parse(ref Stream fs, ref Stream gfs)
        {
            //FileStream testfs = new FileStream("test.geom", FileMode.CreateNew);
            //byte[] fs_data = new byte[fs.Length];
            //fs.Read(fs_data, 0, (int) fs.Length);
            //testfs.Write(fs_data, 0, (int) fs.Length);
            //testfs.Close();

            BinaryReader br = new(fs);
            Console.WriteLine("Parsing Geometry MBIN");

            fs.Seek(0x60, SeekOrigin.Begin);

            var vert_num = br.ReadInt32();
            var indices_num = br.ReadInt32();
            var indices_flag = br.ReadInt32();
            var collision_index_count = br.ReadInt32();

            Console.WriteLine("Model Vertices: {0}", vert_num);
            Console.WriteLine("Model Indices: {0}", indices_num);
            Console.WriteLine("Indices Flag: {0}", indices_flag);
            Console.WriteLine("Collision Index Count: {0}", collision_index_count);

            //Joint Bindings
            var jointbindingOffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var jointCount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            //Skip Unknown yet offset sections
            //Joint Bindings
            //Joint Extensions
            //Joint Mirror Pairs
            //Joint Mirror Axes
            fs.Seek(3 * 0x10, SeekOrigin.Current);

            //Usefull Bone Remapping information

            var skinmatoffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var bc = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);

            //Vertstarts
            var vsoffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var partcount = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            
            //VertEnds
            fs.Seek(0x10, SeekOrigin.Current);

            //Bound Hull Vert start
            var boundhull_vertstart_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Vert end
            var boundhull_vertend_offset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //MatrixLayouts
            fs.Seek(0x10, SeekOrigin.Current);

            //BoundBoxes
            var bboxminoffset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            var bboxmaxoffset = fs.Position + br.ReadInt32();
            fs.Seek(0xC, SeekOrigin.Current);

            //Bound Hull Verts
            var bhulloffset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var bhull_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);


            var lod_count = br.ReadInt32();
            var vx_type = br.ReadInt32();
            Console.WriteLine("Buffer Count: {0} VxType {1}", lod_count, vx_type);
            fs.Seek(0x8, SeekOrigin.Current);
            var mesh_descr_offset = fs.Position + br.ReadInt64();
            var buf_count = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //Parse Small Vertex Layout Info
            var small_bufcount = br.ReadInt32();
            var small_vx_type = br.ReadInt32();
            Console.WriteLine("Small Buffer Count: {0} VxType {1}", small_bufcount, small_vx_type);
            fs.Seek(0x8, SeekOrigin.Current);
            var small_mesh_descr_offset = fs.Position + br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            br.ReadInt32(); //Skip second buf count
            fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

            //fs.Seek(0x20, SeekOrigin.Current); //Second lod offsets

            //Get primary geom offsets
            var indices_offset = fs.Position + br.ReadInt64();
            fs.Seek(0x8, SeekOrigin.Current); //Skip Section Sizes and a 1

            var meshMetaData_offset = fs.Position + br.ReadInt64();
            var meshMetaData_counter = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current); //Skip Section Sizes and a 1

            //fs.Seek(0x10, SeekOrigin.Current);

            //Initialize geometry object
            var geom = new GeomObject();
            
            //Store Counts
            geom.indicesCount = indices_num;
            if (indices_flag == 0x1)
            {
                geom.indicesLength = 0x2;
                geom.indicesLengthType = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedShort;
            }
            else
            {
                geom.indicesLength = 0x4;
                geom.indicesLengthType = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
            }
                
            geom.vertCount = vert_num;
            geom.vx_size = vx_type;
            geom.small_vx_size = small_vx_type;

            //Get Bone Remapping Information
            //I'm 99% sure that boneRemap is not a case in NEXT models
            //it is still there though...
            fs.Seek(skinmatoffset, SeekOrigin.Begin);
            geom.boneRemap = new short[bc];
            for (int i = 0; i < bc; i++)
                geom.boneRemap[i] = (short) br.ReadInt32();

            //Store Joint Data
            fs.Seek(jointbindingOffset, SeekOrigin.Begin);
            geom.jointCount = jointCount;
            for (int i = 0; i < jointCount; i++)
            {
                JointBindingData jdata = new();
                jdata.Load(fs);
                //Copy Matrix
                Array.Copy(MathUtils.convertMat(jdata.invBindMatrix), 0, geom.invBMats, 16 * i, 16);
                //Store the struct
                geom.jointData.Add(jdata);
            }

            //Get Vertex Starts
            //I'm fetching that just for getting the object id within the geometry file
            fs.Seek(vsoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
                geom.vstarts.Add(br.ReadInt32());
        
            //Get BBoxes
            //Init first
            for (int i = 0; i < partcount; i++)
            {
                Vector3[] bb = new Vector3[2];
                geom.bboxes.Add(bb);
            }

            fs.Seek(bboxminoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++) {
                geom.bboxes[i][0] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadBytes(4);
            }

            fs.Seek(bboxmaxoffset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bboxes[i][1] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadBytes(4);
            }

            //Get BoundHullStarts
            fs.Seek(boundhull_vertstart_offset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bhullstarts.Add(br.ReadInt32());
            }

            //Get BoundHullEnds
            fs.Seek(boundhull_vertend_offset, SeekOrigin.Begin);
            for (int i = 0; i < partcount; i++)
            {
                geom.bhullends.Add(br.ReadInt32());
            }

            //TODO : Recheck and fix that shit
            fs.Seek(bhulloffset, SeekOrigin.Begin);
            for (int i = 0; i < bhull_count; i++)
            {
                geom.bhullverts.Add(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                br.ReadBytes(4);
            }

            //Get indices buffer
            fs.Seek(indices_offset, SeekOrigin.Begin);
            geom.ibuffer = new byte[indices_num * geom.indicesLength];
            fs.Read(geom.ibuffer, 0, indices_num * geom.indicesLength);

            //Get MeshMetaData
            fs.Seek(meshMetaData_offset, SeekOrigin.Begin);
            for (int i = 0; i < meshMetaData_counter; i++)
            {
                geomMeshMetaData mmd = new()
                {
                    name = StringUtils.read_string(br, 0x80),
                    hash = br.ReadUInt64(),
                    vs_size = br.ReadUInt32(),
                    vs_abs_offset = br.ReadUInt32(),
                    is_size = br.ReadUInt32(),
                    is_abs_offset = br.ReadUInt32()
                };
                geom.meshMetaDataDict[mmd.hash] = mmd;
                Console.WriteLine(mmd.name);
            }
        
            //Get main mesh description
            fs.Seek(mesh_descr_offset, SeekOrigin.Begin);
            var mesh_desc = "";
            //int[] mesh_offsets = new int[buf_count];
            //Set size excplicitly to 7
            int[] mesh_offsets = new int[7];
            geom.bufInfo = new List<bufInfo>();
            //Set all offsets to -1
            for (int i = 0; i < 7; i++)
            {
                mesh_offsets[i] = -1;
                geom.bufInfo.Add(null);
            }

            for (int i = 0; i < buf_count; i++)
            {
                var buf_id = br.ReadInt32();
                var buf_elem_count = br.ReadInt32();
                var buf_type = br.ReadInt32();
                var buf_localoffset = br.ReadInt32();
                //var buf_test1 = br.ReadInt32();
                //var buf_test2 = br.ReadInt32();
                //var buf_test3 = br.ReadInt32();
                //var buf_test4 = br.ReadInt32();
                
                geom.bufInfo[buf_id]= get_bufInfo_item(buf_id, buf_localoffset, buf_elem_count, buf_type);
                mesh_offsets[buf_id] = buf_localoffset;
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Descr
            mesh_desc = getDescr(ref mesh_offsets, buf_count);
            Console.WriteLine("Mesh Description: " + mesh_desc);

            //Store description
            geom.mesh_descr = mesh_desc;
            geom.offsets = mesh_offsets;
            //Get small description
            fs.Seek(small_mesh_descr_offset, SeekOrigin.Begin);
            var small_mesh_desc = "";
            //int[] mesh_offsets = new int[buf_count];
            //Set size excplicitly to 7
            int[] small_mesh_offsets = new int[7];
            //Set all offsets to -1
            for (int i = 0; i < 7; i++)
                small_mesh_offsets[i] = -1;

            for (int i = 0; i < small_bufcount; i++)
            {
                var buf_id = br.ReadInt32();
                var buf_elem_count = br.ReadInt32();
                var buf_type = br.ReadInt32();
                var buf_localoffset = br.ReadInt32();
                small_mesh_offsets[buf_id] = buf_localoffset;
                fs.Seek(0x10, SeekOrigin.Current);
            }

            //Get Small Descr
            small_mesh_desc = getDescr(ref small_mesh_offsets, small_bufcount);
            Console.WriteLine("Small Mesh Description: " + small_mesh_desc);

            //Store description
            geom.small_mesh_descr = small_mesh_desc;
            geom.small_offsets = small_mesh_offsets;
            //Set geom interleaved
            geom.interleaved = true;


            //Load streams from the geometry stream file
            
            foreach (KeyValuePair<ulong, geomMeshMetaData> pair in geom.meshMetaDataDict)
            {
                geomMeshMetaData mmd = pair.Value;
                geomMeshData md = new()
                {
                    vs_buffer = new byte[mmd.vs_size],
                    is_buffer = new byte[mmd.is_size]
                };

                //Fetch Buffers
                gfs.Seek((int) mmd.vs_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.vs_buffer, 0, (int) mmd.vs_size);

                gfs.Seek((int) mmd.is_abs_offset, SeekOrigin.Begin);
                gfs.Read(md.is_buffer, 0, (int) mmd.is_size);
            
                geom.meshDataDict[mmd.hash] = md;
            }

            return geom;

        }


        private static bufInfo get_bufInfo_item(int buf_id, int offset, int count, int buf_type)
        {
            int sem = buf_id;
            int off = offset;
            OpenTK.Graphics.OpenGL4.VertexAttribPointerType typ = get_type(buf_type);
            string text = get_shader_sem(buf_id);
            bool normalize = false;
            if (text == "bPosition")
                normalize = true;
            return new bufInfo(sem, typ, count, 0, off, text, normalize);
        }


        private static string get_shader_sem(int buf_id)
        {
            switch (buf_id)
            {
                case 0:
                    return "vPosition"; //Verts
                case 1:
                    return "uvPosition0"; //Verts
                case 2:
                    return "nPosition"; //Verts
                case 3:
                    return "tPosition"; //Verts
                case 4:
                    return "bPosition"; //Verts
                case 5:
                    return "blendIndices"; //Verts
                case 6:
                    return "blendWeights"; //Verts
                default:
                    return "shit"; //Default
            }
        }

        private static OpenTK.Graphics.OpenGL4.VertexAttribPointerType get_type(int val){

            switch (val)
            {
                case (0x140B):
                    return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.HalfFloat;
                case (0x1401):
                    return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedByte;
                case (0x8D9F):
                    return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Int2101010Rev;
                default:
                    Console.WriteLine("Unknown VERTEX SECTION TYPE-----------------------------------");
                    throw new ApplicationException("NEW VERTEX SECTION TYPE. FIX IT ASSHOLE...");
                    //return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedByte;
            }
        }

        private static int get_type_count(int val)
        {

            switch (val)
            {
                case (0x140B):
                    return 4;
                case (0x1401):
                    return 1;
                default:
                    Console.WriteLine("Unknown VERTEX SECTION TYPE-----------------------------------");
                    return 1;
            }
        }

        private static string getDescr(ref int[] offsets, int count)
        {
            string mesh_desc = "";


            for (int i = 0; i < count; i++)
            {
                if (offsets[i] != -1)
                {
                    switch (i)
                    {
                        case 0:
                            mesh_desc += "v"; //Verts
                            break;
                        case 1:
                            mesh_desc += "u"; //UVs
                            break;
                        case 2:
                            mesh_desc += "n"; //Normals
                            break;
                        case 3:
                            mesh_desc += "t"; //Tangents
                            break;
                        case 4:
                            mesh_desc += "p"; //Vertex Color
                            break;
                        case 5:
                            mesh_desc += "b"; //BlendIndices
                            break;
                        case 6:
                            mesh_desc += "w"; //BlendWeights
                            break;
                        default:
                            mesh_desc += "x"; //Default
                            break;
                    }
                }
            }

            return mesh_desc;
        }

        private static TextureManager localTexMgr;
        private static readonly List<SceneGraphNode> localAnimScenes = new();

        private static readonly Dictionary<Type, int> SupportedComponents = new()
        {
            {typeof(TkAnimPoseComponentData), 0},
            {typeof(TkAnimationComponentData), 1},
            {typeof(TkLODComponentData), 2},
            {typeof(TkPhysicsComponentData), 3},
            {typeof(GcTriggerActionComponentData), 4},
            {typeof(EmptyNode), 5}
        };


        public static SceneGraphNode LoadObjects(string path)
        {
            TkSceneNodeData template = (TkSceneNodeData) NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr);
            
            Console.WriteLine("Loading Objects from MBINFile");

            string sceneName = template.Name;
            Common.Callbacks.Log(string.Format("Trying to load Scene {0}", sceneName), Common.LogVerbosityLevel.INFO);
            string[] split = sceneName.Split('\\');
            string scnName = split[^1];
            Common.Callbacks.updateStatus("Importing Scene: " + scnName);
            Common.Callbacks.Log(string.Format("Importing Scene: {0}", scnName), Common.LogVerbosityLevel.INFO);
            
            //Get Geometry File
            //Parse geometry once
            string geomfile = NMSUtils.parseNMSTemplateAttrib(template.Attributes, "GEOMETRY");
            int num_lods = int.Parse(NMSUtils.parseNMSTemplateAttrib(template.Attributes, "NUMLODS"));

            GeomObject gobject;
            if (Common.RenderState.activeResMgr.GLgeoms.ContainsKey(geomfile))
            {
                //Load from dict
                gobject = Common.RenderState.activeResMgr.GLgeoms[geomfile];

            } else
            {

#if DEBUG
                //Use libMBIN to decompile the file
                TkGeometryData geomdata = (TkGeometryData)NMSUtils.LoadNMSTemplate(geomfile + ".PC", ref Common.RenderState.activeResMgr);
                //Save NMSTemplate to exml
                string xmlstring = EXmlFile.WriteTemplate(geomdata);
                File.WriteAllText("Temp\\temp_geom.exml", xmlstring);
#endif
                //Load Gstream and Create gobject

                Stream fs, gfs;
                
                fs = NMSUtils.LoadNMSFileStream(geomfile + ".PC", ref Common.RenderState.activeResMgr);

                //Try to fetch the geometry.data.mbin file in order to fetch the geometry streams
                string gstreamfile = "";
                split = geomfile.Split('.');
                for (int i = 0; i < split.Length - 1; i++)
                    gstreamfile += split[i] + ".";
                gstreamfile += "DATA.MBIN.PC";

                gfs = NMSUtils.LoadNMSFileStream(gstreamfile, ref Common.RenderState.activeResMgr);

                //FileStream gffs = new FileStream("testfilegeom.mbin", FileMode.Create);
                //fs.CopyTo(gffs);
                //gffs.Close();

                if (fs is null)
                {
                    Common.Callbacks.showError("Could not find geometry file " + geomfile + ".PC", "Error");
                    Common.Callbacks.Log(string.Format("Could not find geometry file {0} ", geomfile + ".PC"), Common.LogVerbosityLevel.ERROR);

                    //Create Dummy Scene
                    SceneGraphNode dummy = new()
                    {
                        Name = "DUMMY_SCENE",
                        Type = TYPES.MODEL
                    };
                    return dummy;
                }

                gobject = Parse(ref fs, ref gfs);
                Common.RenderState.activeResMgr.GLgeoms[geomfile] = gobject;
                Common.Callbacks.Log(string.Format("Geometry file {0} successfully parsed",
                    geomfile + ".PC"), Common.LogVerbosityLevel.INFO);
                
                fs.Close();
                gfs.Close();
            }

            //Random Generetor for colors
            Random randgen = new();

            //Parse root scene
            SceneGraphNode root = parseNode(template, gobject, null, null);
            
            //Save scene path to resourcemanager
            RenderState.activeResMgr.GLScenes[path] = root; //Use input path
            
            return root;
        }

        private static void ProcessAnimPoseComponent(SceneGraphNode node, TkAnimPoseComponentData component)
        {
            //Load PoseFile
            AnimPoseComponent apc = new(component);
            apc.ref_object = node; //Set referenced animScene
            node.AddComponent<AnimPoseComponent>(apc);
        }

        private static void ProcessAnimationComponent(SceneGraphNode node, TkAnimationComponentData component)
        {
            AnimComponent ac = new(component);
            node.AddComponent<AnimComponent>(ac);
        }

        private static void ProcessPhysicsComponent(SceneGraphNode node, TkPhysicsComponentData component)
        {
            PhysicsComponent pc = new(component);
            node.AddComponent<PhysicsComponent>(pc);
        }

        private static void ProcessTriggerActionComponent(SceneGraphNode node, GcTriggerActionComponentData component)
        {
            TriggerActionComponent tac = new(component);
            node.AddComponent<TriggerActionComponent>(tac);
        }

        private static void ProcessLODComponent(SceneGraphNode node, TkLODComponentData component)
        {
            //Load all LOD models as children to the node
            LODModelComponent lodmdlcomp = new();
            
            for (int i = 0; i < component.LODModel.Count; i++)
            {
                string filepath = component.LODModel[i].LODModel.Filename;
                Console.WriteLine("Loading LOD " + filepath);
                SceneGraphNode so = LoadObjects(filepath);
                so.SetParent(node);
                //Create LOD Resource
                LODModelResource lodres = new(component.LODModel[i]);
                lodmdlcomp.Resources.Add(lodres);
            }
            
            node.AddComponent<LODModelComponent>(lodmdlcomp);
        }

        private static void ProcessComponents(SceneGraphNode node, TkAttachmentData attachment)
        {
            if (attachment == null)
                return;

            for (int i = 0; i < attachment.Components.Count; i++)
            {
                NMSTemplate comp = attachment.Components[i];
                Type comp_type = comp.GetType();
                
                if (!SupportedComponents.ContainsKey(comp_type))
                {
                    Console.WriteLine("Unsupported Component Type " + comp_type);
                    continue;
                }
                    
                switch (SupportedComponents[comp_type])
                {
                    case 0:
                        ProcessAnimPoseComponent(node, comp as TkAnimPoseComponentData);
                        break;
                    case 1:
                        ProcessAnimationComponent(node, comp as TkAnimationComponentData);
                        break;
                    case 2:
                        ProcessLODComponent(node, comp as TkLODComponentData);
                        break;
                    case 3:
                        ProcessPhysicsComponent(node, comp as TkPhysicsComponentData);
                        break;
                    case 4:
                        ProcessTriggerActionComponent(node, comp as GcTriggerActionComponentData);
                        break;
                    case 5: //Empty Node do nothing
                        break;
                }   
            
            }
            
            //Add default LOD distances
            for (int i = 0; i < attachment.LodDistances.Length; i++)
                node.LODDistances.Add(attachment.LodDistances[i]);
        }


        private static MeshMaterial parseMaterial(string matname, TextureManager texMgr)
        {
            MeshMaterial mat;

            Common.Callbacks.Log(string.Format("Trying to load Material {0}", matname), Common.LogVerbosityLevel.INFO);
            string matkey = matname; //Use the entire path

            TkMaterialData template =
                NMSUtils.LoadNMSTemplate(matname, ref RenderState.activeResMgr) as TkMaterialData;
            
            //Material mat = MATERIALMBIN.Parse(newXml);
            mat = Import.NMS.NMSMaterialUtils.CreateMaterialFromStruct(template, texMgr);
            
            //File probably not found not even in the PAKS, 
            if (mat == null)
            {
                Common.Callbacks.Log(string.Format("Warning Material Missing!!!"), Common.LogVerbosityLevel.WARNING);
                //Generate empty material
                mat = new MeshMaterial();
            }
            
            //Load default form palette on init
            //mat.palette = Model_Viewer.Palettes.paletteSel;
            mat.name_key = matkey; //Store the material key to the resource manager
                                   //Store the material to the Resources

            return mat;
        }


        /*
        private static Locator parseLocator(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;

            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};

            Locator so = new Locator();
            //Fetch attributes

            //For now fetch only one attachment
            string attachment = NMSUtils.parseNMSTemplateAttrib(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                try
                {
                    attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
                }
                catch (Exception)
                {
                    attachment_data = null;
                }
            }

            if (node.Attributes.Count > 1)
                Common.Callbacks.showError("DM THE IDIOT TO ADD SUPPORT FOR FUCKING MULTIPLE ATTACHMENTS...", "DM THE IDIOT");

            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.Name = node.Name;
            so.NameHash = node.NameHash;
            so.nms_template = node;

            //Get Transformation
            so.Parent = parent;
            so.parentScene = scene;
            so.init(transforms);

            //Process Locator Attachments
            ProcessComponents(so, attachment_data);
            
            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.Children.Add(part);
            }

            //Finally Order children by name
            so.Children.OrderBy(i => i.Name);

            //Do not restore the old AnimScene let them flow
            //localAnimScene = old_localAnimScene; //Restore old_localAnimScene
            return so;

        }


        private static Joint parseJoint(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;

            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};

            Joint so = new Joint();
            //Set properties
            so.Name = node.Name;
            so.NameHash = node.NameHash;
            so.nms_template = node;
            //Get Transformation
            so.Parent = parent;
            so.parentScene = scene;
            so.init(transforms);

            //Get JointIndex
            so.jointIndex = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "JOINTINDEX").Value);
            //Get InvBMatrix from gobject
            if (so.jointIndex < gobject.jointData.Count)
            {
                so.invBMat = gobject.jointData[so.jointIndex].invBindMatrix;
                so.BindMat = gobject.jointData[so.jointIndex].BindMatrix;
            }

            //Set Random Color
            so.color[0] = Common.RenderState.randgen.Next(255) / 255.0f;
            so.color[1] = Common.RenderState.randgen.Next(255) / 255.0f;
            so.color[2] = Common.RenderState.randgen.Next(255) / 255.0f;


            so.meshVao = new GLInstancedMesh();
            so.instanceId = GLMeshBufferManager.AddInstance(ref so.meshVao, so); //Add instance
            so.meshVao.type = TYPES.JOINT;
            so.meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs

            so.meshVao.vao = new Primitives.LineSegment(children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            so.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];

            //Add joint to scene
            scene.jointDict[so.Name] = so;

            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.Children.Add(part);
            }

            //Finally Order children by name
            so.Children.OrderBy(i => i.Name);
            return so;

        }

        private static Collision parseCollision(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;

            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};

            //Create model
            Collision so = new Collision();

            so.debuggable = true;
            so.Name = node.Name + "_COLLISION";
            so.NameHash = node.NameHash;
            so.Type = TYPES.COLLISION;
            so.nms_template = node;

            //Get Options
            //In collision objects first child is probably the type
            //string collisionType = ((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value").ToUpper();
            string collisionType = node.Attributes.FirstOrDefault(item => item.Name == "TYPE").Value.Value.ToUpper();

            Common.Callbacks.Log(string.Format("Collision Detected {0} {1}", node.Name, collisionType), 
                Common.LogVerbosityLevel.INFO);

            //Get Material for all types
            string matkey = node.Name; //I will index the collision materials by their name, it shouldn't hurt anywhere
                                  // + cleaning up will be up to the resource manager

            MeshMetaData metaData = new MeshMetaData();
            if (collisionType == "MESH")
            {
                so.collisionType = (int)COLLISIONTYPES.MESH;
                metaData.batchstart_physics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHSTART"));
                metaData.batchcount = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHCOUNT"));
                metaData.vertrstart_physics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRSTART"));
                metaData.vertrend_physics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "VERTREND"));
                metaData.firstskinmat = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "FIRSTSKINMAT"));
                metaData.lastskinmat = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "LASTSKINMAT"));
                metaData.boundhullstart = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLST"));
                metaData.boundhullend = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLED"));

                so.gobject = gobject;
                so.metaData = metaData; //Set metadata

                //Find id within the vbo
                int iid = -1;
                for (int i = 0; i < gobject.vstarts.Count; i++)
                    if (gobject.vstarts[i] == metaData.vertrstart_physics)
                    {
                        iid = i;
                        break;
                    }

                if (metaData.lastskinmat - metaData.firstskinmat > 0)
                {
                    throw new Exception("SKINNED COLLISION. CHECK YOUR SHIT!");
                }

                //Set vao
                try
                {
                    so.meshVao = new GLInstancedMesh(so.metaData);
                    so.instanceId = GLMeshBufferManager.AddInstance(ref so.meshVao, so); //Add instance
                    so.meshVao.vao = gobject.getCollisionMeshVao(so.metaData);
                    //Use indiceslength from the gobject
                    so.meshVao.metaData.indicesLength = so.gobject.indicesLengthType;
                }
                catch (KeyNotFoundException e)
                {
                    Common.Callbacks.Log("Missing Collision Mesh " + so.Name, Common.LogVerbosityLevel.WARNING);
                    so.meshVao = null;
                }

            }
            else if (collisionType == "CYLINDER")
            {
                //Console.WriteLine("CYLINDER NODE PARSING NOT IMPLEMENTED");
                //Set cvbo

                float radius = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "RADIUS"));
                float height = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "HEIGHT"));
                Common.Callbacks.Log(string.Format("Cylinder Collision r:{0} h:{1}", radius, height), Common.LogVerbosityLevel.INFO);

                metaData.batchstart_graphics = 0;
                metaData.batchcount = 120;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 22 - 1;
                so.metaData = metaData;
                so.meshVao = new GLInstancedMesh(so.metaData);
                so.meshVao.vao = (new Primitives.Cylinder(radius, height, new Vector3(0.0f, 0.0f, 0.0f), true)).getVAO();
                so.instanceId = GLMeshBufferManager.AddInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.CYLINDER;

            }
            else if (collisionType == "BOX")
            {
                //Console.WriteLine("BOX NODE PARSING NOT IMPLEMENTED");
                //Set cvbo
                float width = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "WIDTH").Value);
                float height = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "HEIGHT").Value);
                float depth = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "DEPTH").Value);
                Common.Callbacks.Log(string.Format("Sphere Collision w:{0} h:{0} d:{0}", width, height, depth), 
                    Common.LogVerbosityLevel.INFO);
                //Set general vao properties
                metaData.batchstart_graphics = 0;
                metaData.batchcount = 36;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 8 - 1;
                so.metaData = metaData;

                so.meshVao = new GLInstancedMesh(so.metaData);
                so.meshVao.vao = (new Primitives.Box(width, height, depth, new Vector3(1.0f), true)).getVAO();
                so.instanceId = GLMeshBufferManager.AddInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.BOX;


            }
            else if (collisionType == "CAPSULE")
            {
                //Set cvbo
                float radius = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "RADIUS"));
                float height = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "HEIGHT"));
                Common.Callbacks.Log(string.Format("Capsule Collision r:{0} h:{1}", radius, height), 
                    Common.LogVerbosityLevel.INFO);
                metaData.batchstart_graphics = 0;
                metaData.batchcount = 726;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 144 - 1;
                so.metaData = metaData;
                so.meshVao = new GLInstancedMesh(so.metaData);
                so.meshVao.vao = (new Primitives.Capsule(new Vector3(), height, radius)).getVAO();
                so.instanceId = GLMeshBufferManager.AddInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.CAPSULE;

            }
            else if (collisionType == "SPHERE")
            {
                //Set cvbo
                float radius = MathUtils.FloatParse(node.Attributes.FirstOrDefault(item => item.Name == "RADIUS").Value);
                Common.Callbacks.Log(string.Format("Sphere Collision r:{0}", radius), 
                    Common.LogVerbosityLevel.INFO);
                metaData.batchstart_graphics = 0;
                metaData.batchcount = 600;
                metaData.vertrstart_graphics = 0;
                metaData.vertrend_graphics = 121 - 1;
                so.metaData = metaData;
                so.meshVao = new GLInstancedMesh(so.metaData);
                so.meshVao.vao = (new Primitives.Sphere(new Vector3(), radius)).getVAO();
                so.instanceId = GLMeshBufferManager.AddInstance(ref so.meshVao, so); //Add instance
                so.collisionType = COLLISIONTYPES.SPHERE;
            }
            else
            {
                Common.Callbacks.Log("NEW COLLISION TYPE: " + collisionType, Common.LogVerbosityLevel.INFO);
            }

            //Set metaData and material to the collision Mesh
            so.meshVao.metaData = new MeshMetaData(so.metaData);
            so.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["collisionMat"];
            so.meshVao.type = TYPES.COLLISION;
            so.meshVao.collisionType = so.collisionType;

            Common.Callbacks.Log(string.Format("Batch Start {0} Count {1} ",
                metaData.batchstart_physics, metaData.batchcount), Common.LogVerbosityLevel.INFO);

            so.Parent = parent;
            so.init(transforms);

            //Collision probably has no children biut I'm leaving that code here
            foreach (TkSceneNodeData child in children)
                so.Children.Add(parseNode(child, gobject, so, scene));

            return so;

        }


        private static Light parseLight(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;

            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};


            Light so = new Light();
            //Set Properties
            so.Name = node.Name;
            so.NameHash = node.NameHash;
            so.Type = TYPES.LIGHT;
            so.nms_template = node;

            so.Parent = parent;
            so.init(transforms);

            //Parse Light Attributes
            so.Color.X = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "COL_R"));
            so.Color.Y = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "COL_G"));
            so.Color.Z = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "COL_B"));
            so.FOV = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "FOV"));
            so.Intensity = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "INTENSITY"));
            so.Falloff = (ATTENUATION_TYPE) Enum.Parse(typeof(ATTENUATION_TYPE), NMSUtils.parseNMSTemplateAttrib(node.Attributes, "FALLOFF").ToUpper());
            
            //Add Light to the resource Manager
            Common.RenderState.activeResMgr.GLlights.Add(so);

            return so;

        }

        private static Reference parseReference(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;

            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};

            //Another Scene file referenced
            Console.WriteLine("Reference Detected");

            string scene_ref = node.Attributes.FirstOrDefault(item => item.Name == "SCENEGRAPH").Value;
            Common.Callbacks.Log(string.Format("Loading Reference {0}", scene_ref), Common.LogVerbosityLevel.INFO);

            //Getting Scene MBIN file
            //string exmlPath = Path.GetFullPath(Util.getFullExmlPath(path));
            //Console.WriteLine("Loading Scene " + path);
            //Parse MBIN to xml

            //Generate Reference object
            Reference so = new Reference();
            so.Name = node.Name;
            so.NameHash = node.NameHash;

            //Get Transformation
            so.Parent = parent;
            so.nms_template = node;
            so.init(transforms);

            SceneGraphNode new_so;
            //Check if scene has been parsed
            if (!Common.RenderState.activeResMgr.GLScenes.ContainsKey(scene_ref))
            {
                //Read new Scene
                new_so = LoadObjects(scene_ref);
            }
            else
            {
                //Make a shallow copy of the scene
                new_so = (SceneGraphNode) Common.RenderState.activeResMgr.GLScenes[scene_ref].Clone();
            }

            so.ref_scene = new_so;
            new_so.Parent = so;
            so.Children.Add(new_so); //Keep it also as a child so the rest of pipeline is not affected

            //Handle Children
            //Console.WriteLine("Children Count {0}", childs.ChildNodes.Count);
            foreach (TkSceneNodeData child in children)
            {
                SceneGraphNode part = parseNode(child, gobject, so, scene);
                so.Children.Add(part);
            }

            return so;

        }

        private static Locator parseEmitter(TkSceneNodeData node,
            GeomObject gobject, Model parent, Scene scene)
        {
            TkTransformData transform = node.Transform;
            List<TkSceneNodeAttributeData> attribs = node.Attributes;
            List<TkSceneNodeData> children = node.Children;

            //Load Transforms
            //Get Transformation
            var transforms = new float[] { transform.TransX,
                transform.TransY,
                transform.TransZ,
                transform.RotX,
                transform.RotY,
                transform.RotZ,
                transform.ScaleX,
                transform.ScaleY,
                transform.ScaleZ};
            Locator so = new Locator();
            so.Type = TYPES.EMITTER;
            //Fetch attributes

            //For now fetch only one attachment
            string attachment = NMSUtils.parseNMSTemplateAttrib(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
            }

            //TODO: Parse Emitter material and Emission data. from the node attributes

            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.Name = node.Name;
            so.NameHash = node.NameHash;
            so.nms_template = node;

            //Get Transformation
            so.Parent = parent;
            so.init(transforms);

            //Process Locator Attachments
            ProcessComponents(so, attachment_data);

            //Handle Children
            foreach (TkSceneNodeData child in children)
            {
                Model part = parseNode(child, gobject, so, scene);
                so.Children.Add(part);
            }

            //Do not restore the old AnimScene let them flow
            //localAnimScene = old_localAnimScene; //Restore old_localAnimScene
            return so;
        }

        */

        private static SceneGraphNode parseNode(TkSceneNodeData node, 
            GeomObject gobject, SceneGraphNode parent, SceneGraphNode parentScene)
        {
            Common.Callbacks.Log(string.Format("Importing Node {0}", node.Name), 
                Common.LogVerbosityLevel.INFO);
            Common.Callbacks.updateStatus("Importing Part: " + node.Name);

            if (!Enum.TryParse(node.Type, out TYPES typeEnum))
                throw new Exception("Node Type " + node.Type + "Not supported");

            SceneGraphNode so = new()
            {
                Name = node.Name,
                NameHash = node.NameHash,
                Type = typeEnum,
                ID = Common.RenderState.itemCounter++
            };
            
            //Add Transform Component
            TransformData td = new(node.Transform.TransX,
                                   node.Transform.TransY,
                                   node.Transform.TransZ,
                                   node.Transform.RotX,
                                   node.Transform.RotY,
                                   node.Transform.RotZ,
                                   node.Transform.ScaleX,
                                   node.Transform.ScaleY,
                                   node.Transform.ScaleY);
            TransformComponent tc = new(td);
            so.AddComponent<TransformComponent>(tc);

            //Set Parent after the transform component has been initialized
            if (parent != null)
                so.SetParent(parent);
            so.ParentScene = parentScene;
            
            //For now fetch only one attachment
            string attachment = NMSUtils.parseNMSTemplateAttrib(node.Attributes, "ATTACHMENT");
            TkAttachmentData attachment_data = null;
            if (attachment != "")
            {
                attachment_data = NMSUtils.LoadNMSTemplate(attachment, ref Common.RenderState.activeResMgr) as TkAttachmentData;
            }

            //Process Attachments
            ProcessComponents(so, attachment_data);

            if (typeEnum == TYPES.MESH)
            {
                Common.Callbacks.Log(string.Format("Parsing Mesh {0}", node.Name), 
                    Common.LogVerbosityLevel.INFO);

                //Add MeshComponent
                MeshComponent mc = new()
                {
                    MetaData = new()
                    {
                        BatchStartPhysics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHSTARTPHYSI")),
                        VertrStartPhysics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRSTARTPHYSI")),
                        VertrEndPhysics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRENDPHYSICS")),
                        BatchStartGraphics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHSTARTGRAPH")),
                        BatchCount = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BATCHCOUNT")),
                        VertrStartGraphics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRSTARTGRAPH")),
                        VertrEndGraphics = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "VERTRENDGRAPHIC")),
                        FirstSkinMat = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "FIRSTSKINMAT")),
                        LastSkinMat = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "LASTSKINMAT")),
                        LODLevel = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "LODLEVEL")),
                        BoundHullStart = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLST")),
                        BoundHullEnd = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "BOUNDHULLED")),
                        AABBMIN = new Vector3(MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMINX")),
                                          MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMINY")),
                                          MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMINZ"))),
                        AABBMAX = new Vector3(MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMAXX")),
                                          MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMAXY")),
                                          MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "AABBMAXZ"))),
                        Hash = ulong.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "HASH"))
                    }
    
                };

                //Common.Callbacks.Log(string.Format("Randomized Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]), Common.LogVerbosityLevel.INFO);
                Common.Callbacks.Log(string.Format("Batch Physics Start {0} Count {1} Vertex Physics {2} - {3} Vertex Graphics {4} - {5} SkinMats {6}-{7}",
                    mc.MetaData.BatchStartPhysics, mc.MetaData.BatchCount, mc.MetaData.VertrStartPhysics,
                    mc.MetaData.VertrEndPhysics, mc.MetaData.VertrStartGraphics, mc.MetaData.VertrEndGraphics,
                    mc.MetaData.FirstSkinMat, mc.MetaData.LastSkinMat), Common.LogVerbosityLevel.INFO);

                Console.WriteLine("Object {0}, Number of skinmatrices required: {1}", so.Name, 
                    mc.MetaData.LastSkinMat - mc.MetaData.FirstSkinMat);
                
                //TODO Process the corresponding mesh if needed
                so.AddComponent<MeshComponent>(mc);

                //Search for the vao
                GLVao vao = gobject.findVao(mc.MetaData.Hash);

                if (vao == null)
                {
                    //Generate VAO and Save vao
                    vao = gobject.generateVAO(mc.MetaData);
                    gobject.saveVAO(mc.MetaData.Hash, vao);
                }

                //Get Material Name
                string matname = NMSUtils.parseNMSTemplateAttrib(node.Attributes, "MATERIAL");

                //Search for the material
                MeshMaterial mat;
                if (RenderState.activeResMgr.GLmaterials.ContainsKey(matname))
                    mat = RenderState.activeResMgr.GLmaterials[matname];
                else
                {
                    //Parse material
                    mat = parseMaterial(matname, localTexMgr);
                    //Save Material to the resource manager
                    Common.RenderState.activeResMgr.AddMaterial(mat);
                }

                //Search for the meshVao in the gobject
                GLInstancedMesh meshVao = gobject.findGLMeshVao(matname, mc.MetaData.Hash);
                
                if (meshVao == null)
                {
                    //Generate new meshVao
                    meshVao = new GLInstancedMesh(mc.MetaData);
                    meshVao.type = TYPES.MESH;
                    meshVao.vao = vao;
                    mc.Material = mat; //Set meshVao Material

                    //Set indicesLength
                    //Calculate indiceslength per index buffer
                    if (mc.MetaData.BatchCount > 0)
                    {
                        int indicesLength = (int) gobject.meshMetaDataDict[mc.MetaData.Hash].is_size / mc.MetaData.BatchCount;
                        
                        switch (indicesLength)
                        {
                            case 1:
                                meshVao.MetaData.IndicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedByte;
                                mc.MetaData.IndicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedByte;
                                break;
                            case 2:
                                meshVao.MetaData.IndicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedShort;
                                mc.MetaData.IndicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedShort;
                                break;
                            case 4:
                                meshVao.MetaData.IndicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
                                mc.MetaData.IndicesLength = OpenTK.Graphics.OpenGL4.DrawElementsType.UnsignedInt;
                                break;
                        }

                    }

                    //Configure boneRemap properly
                    meshVao.BoneRemapIndicesCount = mc.MetaData.LastSkinMat - mc.MetaData.FirstSkinMat;
                    meshVao.BoneRemapIndices = new int[meshVao.BoneRemapIndicesCount];
                    for (int i = 0; i < mc.MetaData.LastSkinMat - mc.MetaData.FirstSkinMat; i++)
                        meshVao.BoneRemapIndices[i] = gobject.boneRemap[mc.MetaData.FirstSkinMat + i];

                    //Set skinned flag
                    if (meshVao.BoneRemapIndicesCount > 0)
                        meshVao.skinned = true;

                    //Set skinned flag if its set as a metarial flag
                    if (mat.has_flag(MaterialFlagEnum._F02_SKINNED))
                        meshVao.skinned = true;

                    //Generate collision mesh vao
                    try
                    {
                        meshVao.bHullVao = gobject.getCollisionMeshVao(mc.MetaData); //Missing data
                    }
                    catch (Exception)
                    {
                        Common.Callbacks.Log("Error while fetching bHull Collision Mesh", Common.LogVerbosityLevel.ERROR);
                        meshVao.bHullVao = null;
                    }

                    //so.setupBSphere(); //Setup Bounding Sphere Mesh

                    //Save meshvao to the gobject
                    gobject.saveGLMeshVAO(mc.MetaData.Hash, matname, meshVao);
                }
            }
            else if (typeEnum == TYPES.MODEL)
            {
                //Create MeshComponent
                MeshComponent mc = new()
                {
                    MeshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshes["default_cross"],
                    Material = Common.RenderState.activeResMgr.GLmaterials["crossMat"]
                };
                
                so.AddComponent<MeshComponent>(mc);

                //Create SceneComponent
                SceneComponent sc = new()
                {
                    NumLods = int.Parse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "LODLEVEL")),
                };
                
                sc.TexManager.SetMasterTexManager(Common.RenderState.activeResMgr.texMgr);
                localTexMgr = sc.TexManager;

                so.AddComponent<SceneComponent>(sc);

                //Fetch extra LOD attributes
                for (int i = 1; i < sc.NumLods; i++)
                {
                    float attr_val = MathUtils.FloatParse(NMSUtils.parseNMSTemplateAttrib(node.Attributes, "LODDIST" + i));
                    sc.LODDistances.Add(attr_val);
                }

            }
            else if (typeEnum == TYPES.LOCATOR)
            {
                throw new Exception("Not Implemented Yet!");
            }
            else if (typeEnum == TYPES.JOINT)
            {
                throw new Exception("Not Implemented Yet!");
            }
            else if (typeEnum == TYPES.REFERENCE)
            {
                throw new Exception("Not Implemented Yet!");
            }
            else if (typeEnum == TYPES.COLLISION)
            {
                throw new Exception("Not Implemented Yet!");
            }
            else if (typeEnum == TYPES.LIGHT)
            {
                throw new Exception("Not Implemented Yet!");
            }
            else if (typeEnum == TYPES.EMITTER)
            {
                throw new Exception("Not Implemented Yet!");
            } else
            {
                Common.Callbacks.Log("Unknown scenenode type. Please contant the developer", Common.LogVerbosityLevel.WARNING);
            }

            //Console.WriteLine("Children Count {0}", childs.ChildNodes.Count);
            foreach (TkSceneNodeData child in node.Children)
            {
                SceneGraphNode part = parseNode(child, gobject, so, parentScene);
                so.Children.Add(part);
            }

            //Finally Order children by name
            so.Children.Sort((a, b) => string.Compare(a.Name, b.Name));

            return so;
        }
 

    }

}
