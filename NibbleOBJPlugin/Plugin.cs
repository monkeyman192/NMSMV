using System;
using System.Collections.Generic;
using System.IO;
using NbCore;
using NbCore.Common;
using NbCore.Math;
using NbCore.Plugins;
using NbCore.Systems;

namespace NibbleOBJPlugin
{
    public class Plugin : PluginBase
    {
        public static string PluginName = "OBJPlugin";
        public static string PluginVersion = "1.0.0";
        public static string PluginDescription = "OBJ Plugin for Nibble Engine. Created by gregkwaste";
        public static string PluginCreator = "gregkwaste";

        private bool show_open_file_dialog = false;
        
        public Plugin(Engine e) : base(e)
        {
            Name = PluginName;
            Version = PluginVersion;
            Description = PluginDescription;
            Creator = PluginCreator;
        }

        public override void OnLoad()
        {
            Log("Loaded OBJ Plugin", LogVerbosityLevel.INFO);
        }

        public override void Import(string filepath)
        {
            
            
            
            
        }

        private GeomObject GenerateGeometry(List<NbVector3> lverts, List<NbVector3i> ltris)
        {
            float[] verts = new float[lverts.Count * 3];
            float[] indices = new float[ltris.Count * 3];

            for (int i = 0; i < lverts.Count; i++)
            {
                verts[3 * i + 0] = lverts[i].X;
                verts[3 * i + 1] = lverts[i].Y;
                verts[3 * i + 2] = lverts[i].Z;
            }
            
            for (int i = 0; i < ltris.Count; i++)
            {
                indices[3 * i + 0] = ltris[i].X;
                indices[3 * i + 1] = ltris[i].Y;
                indices[3 * i + 2] = ltris[i].Z;
            }
            
            GeomObject geom = new();
            geom.Name = "obj_mesh";

            //Set main Geometry Info
            geom.vertCount = verts.Length / 3;
            geom.indicesCount = indices.Length;
            geom.indicesLength = 0x4;

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each
            
            //Set Buffer Offsets
            geom.mesh_descr = "vn";
            
            bufInfo buf = new bufInfo()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = 0,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            //TODO: I can calculate normals if needed
            
            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            geom.vbuffer = new byte[4 * verts.Length];
            Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, geom.vbuffer.Length);

            return geom;
        }

        public Scene ParseObj(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            List<NbVector3> Vertices = new();
            List<NbVector3i> Tris = new();
            
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();

                if (line.StartsWith("#"))
                    continue;
                
                if (line.StartsWith("v"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    float.TryParse(split[3], out var z);
                    Vertices.Add(new NbVector3(x, y, z));
                }
                else if (line.StartsWith("f"))
                {
                    //Parse Triangle
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    int.TryParse(split[1], out var x);
                    int.TryParse(split[2], out var y);
                    int.TryParse(split[3], out var z);
                    Tris.Add(new NbVector3i(x, y, z));
                }
                else
                {
                    Log("Unknown obj directive. Skipping...", LogVerbosityLevel.WARNING);
                }
                
                //Convert data to mesh
                GeomObject geom = GenerateGeometry(Vertices, Tris);
                
                GLInstancedMesh mesh = new()
                {
                    Name = "obj_mesh",
                    type = SceneNodeType.GIZMO,
                    vao = geom.generateVAO(),
                    MetaData = new()
                    {
                        BatchCount = geom.indicesCount,
                        AABBMIN = new NbVector3(-0.1f),
                        AABBMAX = new NbVector3(0.1f),
                        IndicesLength = NbPrimitiveDataType.UnsignedInt,
                    }
                };
                
                //Generate Scene
                Scene scn = new Scene();
                //Generate Scene Root
                SceneGraphNode root = EngineRef.CreateLocatorNode("OBJ_Root");
                
                //Generate Mesh Node
                //SceneGraphNode mesh = EngineRef.CreateMeshNode("obj_mesh", geom.);


            } 
            
            
            
            
            sr.Close();
            return null;
        }

        public override void Export(string filepath)
        {
            Log("Not supported yet", LogVerbosityLevel.INFO);
        }

        public override void OnUnload()
        {
            throw new NotImplementedException();
        }

        public override void DrawImporters()
        {
            return;
        }

        public override void DrawExporters(Scene scn)
        {
            return;
        }

        public override void Draw()
        {
            throw new NotImplementedException();
        }
    }
}