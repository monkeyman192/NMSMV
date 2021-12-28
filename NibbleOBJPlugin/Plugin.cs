using System;
using System.Collections.Generic;
using System.IO;
using NbCore;
using NbCore.Common;
using NbCore.Math;
using NbCore.Plugins;
using NbCore.Systems;
using NbCore.UI.ImGui;

using ImGuiCore = ImGuiNET.ImGui;


namespace NibbleOBJPlugin
{
    public class Plugin : PluginBase
    {
        public static string PluginName = "OBJPlugin";
        public static string PluginVersion = "1.0.0";
        public static string PluginDescription = "OBJ Plugin for Nibble Engine. Created by gregkwaste";
        public static string PluginCreator = "gregkwaste";

        private OpenFileDialog openFileDialog;

        
        public Plugin(Engine e) : base(e)
        {
            Name = PluginName;
            Version = PluginVersion;
            Description = PluginDescription;
            Creator = PluginCreator;
        }

        public override void OnLoad()
        {
            openFileDialog = new("", ".obj", false); //Initialize OpenFileDialog
            Log("Loaded OBJ Plugin", LogVerbosityLevel.INFO);
        }

        public override void Import(string filepath)
        {
            
        }

        private NbMesh GenerateMesh(List<NbVector3> lverts, List<NbVector3i> ltris)
        {

            NbMeshData data = GenerateGeometryData(lverts, ltris);
            NbMeshMetaData metadata = GenerateGeometryMetaData(data);

            //Generate NbMesh
            NbMesh mesh = new()
            {
                Hash = (ulong)"obj_mesh".GetHashCode(),
                Data = data,
                MetaData = metadata
            };

            return mesh;
        }

        private NbMeshMetaData GenerateGeometryMetaData(NbMeshData data)
        {
            NbMeshMetaData metadata = new()
            {
                IndicesLength = NbPrimitiveDataType.UnsignedInt,
                BatchCount = data.IndexBuffer.Length / 0x4,
                FirstSkinMat = 0,
                LastSkinMat = 0,
                VertrEndGraphics = data.VertexBuffer.Length / (0x3 * 0x4) - 1,
                VertrEndPhysics = data.VertexBuffer.Length / (0x3 * 0x4)
            };

            return metadata;
        }

        private NbMeshData GenerateGeometryData(List<NbVector3> lverts, List<NbVector3i> ltris)
        {
            NbMeshData data = NbMeshData.Create();
            data.Hash = (ulong)"obj_mesh".GetHashCode();

            //Save vertices
            int vxbytecount = lverts.Count * 3 * 4;
            int ixbytecount = ltris.Count * 3 * 4;
            data.VertexBuffer = new byte[vxbytecount];
            data.IndexBuffer = new byte[ixbytecount];

            MemoryStream ms = new MemoryStream(data.VertexBuffer);
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Seek(0, SeekOrigin.Begin);

            for (int i = 0; i < lverts.Count; i++)
            {
                bw.Write(lverts[i].X);
                bw.Write(lverts[i].Y);
                bw.Write(lverts[i].Z);
            }
            bw.Close();


            ms = new MemoryStream(data.IndexBuffer);
            bw = new BinaryWriter(ms);
            bw.Seek(0, SeekOrigin.Begin);

            for (int i = 0; i < ltris.Count; i++)
            {
                bw.Write(ltris[i].X);
                bw.Write(ltris[i].Y);
                bw.Write(ltris[i].Z);
            }

            //Create Buffers
            data.buffers = new bufInfo[1];
            
            bufInfo buf = new bufInfo()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = 12,
                type = NbPrimitiveDataType.Float
            };

            data.buffers[0] = buf;

            return data;
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

                NbMesh mesh = GenerateMesh(Vertices, Tris);

                //Generate Material
                MeshMaterial mat = new();

                mat.Name = "objMat";
                mat.add_flag(MaterialFlagEnum._F07_UNLIT);
                Uniform uf = new()
                {
                    Name = "mpCustomPerMaterial.gMaterialColourVec4",
                    Values = new(0.0f, 1.0f, 1.0f, 1.0f)
                };
                mat.Uniforms.Add(uf);
                EngineRef.renderSys.Renderer.CompileMaterialShader(mat);
                

                //Generate Scene
                Scene scn = new Scene();
                //Generate Scene Root
                SceneGraphNode root = EngineRef.CreateLocatorNode("OBJ_Root");
                
                //Generate Mesh Node
                SceneGraphNode mesh_node = EngineRef.CreateMeshNode("obj_mesh", mesh, mat);


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
            if (ImGuiCore.MenuItem("Import from obj", "", false, openFileDialog.IsOpen))
            {
                openFileDialog.Open();
            }
        }

        public override void DrawExporters(Scene scn)
        {
            return;
        }

        public override void Draw()
        {
            openFileDialog.Draw(new System.Numerics.Vector2(600,400));
        }
    }
}