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
            var assemblypath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            openFileDialog = new("obj-open-file", assemblypath, ".obj", false); //Initialize OpenFileDialog
            Log("Loaded OBJ Plugin", LogVerbosityLevel.INFO);
        }

        public override void Import(string filepath)
        {
            SceneGraphNode node = ParseObj(filepath);
            EngineRef.RegisterSceneGraphNode(node);
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
            bw.Flush();
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
            //Use buffer information to calculate the per vertex stride
            data.VertexBufferStride = 0x4 * 0x3; 

            return data;
        }

        public SceneGraphNode ParseObj(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            List<NbVector3> VertexPositions = new();
            List<NbVector3> VertexNormals = new();
            List<NbVector2> VertexUVs = new();

            //Final Data
            List<NbVector3> Vertices = new();
            List<NbVector3i> Tris = new();
            int indexCount = 0;
            
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();

                if (line.StartsWith("#"))
                    continue;

                if (line.StartsWith("vt"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    VertexUVs.Add(new NbVector2(x, y));
                } else if (line.StartsWith("vn"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    float.TryParse(split[3], out var z);
                    VertexNormals.Add(new NbVector3(x, y, z));
                }
                else if (line.StartsWith("v"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    float.TryParse(split[3], out var z);
                    VertexPositions.Add(new NbVector3(x, y, z));
                }
                else if (line.StartsWith("f"))
                {
                    //Parse Face Data
                    string[] split = line.Split(' ');
                    
                    //Quad
                    if (split.Length == 5)
                    {
                        //TODO
                    } else //Triangle
                    {
                        //Assume triangles
                        for (int i = 1; i < 4; i++)
                        {
                            string[] vsplit = split[i].Split('/');
                            
                            for (int j = 0; j < vsplit.Length; j++)
                            {
                                switch (j)
                                {
                                    //VertexPositions
                                    case 0:
                                        {
                                            NbVector3 v1 = VertexPositions[int.Parse(vsplit[0]) - 1];
                                            Vertices.Add(v1);
                                            break;
                                        };
                                    default:
                                        {
                                            //Normals and Uns not yet supported
                                            break;
                                        }
                                }
                            }
                        }
                        //Save triangle
                        Tris.Add(new NbVector3i(indexCount, indexCount + 1, indexCount + 2));
                        indexCount += 3;
                    }
                }
                else
                {
                    Log($"Unknown obj directive {line}. Skipping...", LogVerbosityLevel.WARNING);
                }

            }
            sr.Close();

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
            mat.Shader = EngineRef.renderSys.GetMaterialShader(mat, 
                NbCore.Platform.Graphics.OpenGL.SHADER_MODE.DEFFERED);
            
            //Generate Mesh Node
            SceneGraphNode mesh_node = EngineRef.CreateMeshNode("obj_mesh", mesh, mat);
            

            return mesh_node;
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
            if (ImGuiCore.MenuItem("Import from obj", "", false, true))
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
            if (openFileDialog.Draw(new System.Numerics.Vector2(600, 400)))
            {
                Import(openFileDialog.GetSelectedFile());
            }
        }
    }
}