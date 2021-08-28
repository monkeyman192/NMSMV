using System;
using System.Collections.Generic;
using Assimp;
using MVCore;
using MVCore.Systems;
using MVCore.Utils;

namespace MVCore.Export
{

    /* Bring that shit back when we are done with the transition to the ECS system
     

    public override Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {
            Assimp.Mesh amesh = new Assimp.Mesh();
            Assimp.Node node;
            amesh.Name = Name;

            int meshHash = meshVao.GetHashCode();

            //TESTING
            if (scn.MeshCount > 20)
            {
                node = base.assimpExport(ref scn, ref meshImportStatus);
                return node;
            }

            if (!meshImportStatus.ContainsKey(meshHash))
            //if (false)
            {
                meshImportStatus[meshHash] = scn.MeshCount;

                int vertcount = metaData.vertrend_graphics - metaData.vertrstart_graphics + 1;
                MemoryStream vms = new MemoryStream(gobject.meshDataDict[metaData.Hash].vs_buffer);
                MemoryStream ims = new MemoryStream(gobject.meshDataDict[metaData.Hash].is_buffer);
                BinaryReader vbr = new BinaryReader(vms);
                BinaryReader ibr = new BinaryReader(ims);


                //Initialize Texture Component Channels
                if (gobject.bufInfo[1] != null)
                {
                    List<Assimp.Vector3D> textureChannel = new List<Assimp.Vector3D>();
                    amesh.TextureCoordinateChannels.Append(textureChannel);
                    amesh.UVComponentCount[0] = 2;
                }

                //Generate bones only for the joints related to the mesh
                Dictionary<int, Assimp.Bone> localJointDict = new Dictionary<int, Assimp.Bone>();

                //Export Bone Structure
                if (Skinned)
                //if (false)
                {
                    for (int i = 0; i < meshVao.BoneRemapIndicesCount; i++)
                    {
                        int joint_id = meshVao.BoneRemapIndices[i];
                        //Fetch name
                        Joint relJoint = null;

                        foreach (Joint jnt in parentScene.jointDict.Values)
                        {
                            if (jnt.jointIndex == joint_id)
                            {
                                relJoint = jnt;
                                break;
                            }

                        }

                        //Generate bone
                        Assimp.Bone b = new Assimp.Bone();
                        if (relJoint != null)
                        {
                            b.Name = relJoint.Name;
                            b.OffsetMatrix = MathUtils.convertMatrix(relJoint.invBMat);
                        }


                        localJointDict[i] = b;
                        amesh.Bones.Add(b);
                    }
                }



                //Write geometry info

                vbr.BaseStream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < vertcount; i++)
                {
                    Assimp.Vector3D v, vN;

                    for (int j = 0; j < gobject.bufInfo.Count; j++)
                    {
                        bufInfo buf = gobject.bufInfo[j];
                        if (buf is null)
                            continue;

                        switch (buf.semantic)
                        {
                            case 0: //vPosition
                                {
                                    switch (buf.type)
                                    {
                                        case VertexAttribPointerType.HalfFloat:
                                            uint v1 = vbr.ReadUInt16();
                                            uint v2 = vbr.ReadUInt16();
                                            uint v3 = vbr.ReadUInt16();
                                            uint v4 = vbr.ReadUInt16();

                                            //Transform vector with worldMatrix
                                            v = new Assimp.Vector3D(Utils.Half.decompress(v1), Utils.Half.decompress(v2), Utils.Half.decompress(v3));
                                            break;
                                        case VertexAttribPointerType.Float: //This is used in my custom vbos
                                            float f1 = vbr.ReadSingle();
                                            float f2 = vbr.ReadSingle();
                                            float f3 = vbr.ReadSingle();
                                            //Transform vector with worldMatrix
                                            v = new Assimp.Vector3D(f1, f2, f3);
                                            break;
                                        default:
                                            throw new Exception("Unimplemented Vertex Type");
                                    }
                                    amesh.Vertices.Add(v);
                                    break;
                                }

                            case 1: //uvPosition
                                {
                                    Assimp.Vector3D uv;
                                    uint v1 = vbr.ReadUInt16();
                                    uint v2 = vbr.ReadUInt16();
                                    uint v3 = vbr.ReadUInt16();
                                    uint v4 = vbr.ReadUInt16();
                                    //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                                    uv = new Assimp.Vector3D(Utils.Half.decompress(v1), Utils.Half.decompress(v2), 0.0f);

                                    amesh.TextureCoordinateChannels[0].Add(uv); //Add directly to the first channel
                                    break;
                                }
                            case 2: //nPosition
                            case 3: //tPosition
                                {
                                    switch (buf.type)
                                    {
                                        case (VertexAttribPointerType.Float):
                                            float f1, f2, f3;
                                            f1 = vbr.ReadSingle();
                                            f2 = vbr.ReadSingle();
                                            f3 = vbr.ReadSingle();
                                            vN = new Assimp.Vector3D(f1, f2, f3);
                                            break;
                                        case (VertexAttribPointerType.HalfFloat):
                                            uint v1, v2, v3;
                                            v1 = vbr.ReadUInt16();
                                            v2 = vbr.ReadUInt16();
                                            v3 = vbr.ReadUInt16();
                                            vN = new Assimp.Vector3D(Utils.Half.decompress(v1), Utils.Half.decompress(v2), Utils.Half.decompress(v3));
                                            break;
                                        case (VertexAttribPointerType.Int2101010Rev):
                                            int i1, i2, i3;
                                            uint value;
                                            byte[] a32 = new byte[4];
                                            a32 = vbr.ReadBytes(4);

                                            value = BitConverter.ToUInt32(a32, 0);
                                            //Convert Values
                                            i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                                            i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                                            i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                                            //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
                                            float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

                                            vN = new Assimp.Vector3D(Convert.ToSingle(i1) / norm,
                                                             Convert.ToSingle(i2) / norm,
                                                             Convert.ToSingle(i3) / norm);

                                            //Debug.WriteLine(vN);
                                            break;
                                        default:
                                            throw new Exception("UNIMPLEMENTED NORMAL TYPE. PLEASE REPORT");
                                    }

                                    if (j == 2)
                                        amesh.Normals.Add(vN);
                                    else if (j == 3)
                                    {
                                        amesh.Tangents.Add(vN);
                                        amesh.BiTangents.Add(new Assimp.Vector3D(0.0f, 0.0f, 1.0f));
                                    }
                                    break;
                                }
                            case 4: //bPosition
                                vbr.ReadBytes(4); // skip
                                break;
                            case 5: //BlendIndices + BlendWeights
                                {
                                    int[] joint_ids = new int[4];
                                    float[] weights = new float[4];

                                    for (int k = 0; k < 4; k++)
                                    {
                                        joint_ids[k] = vbr.ReadByte();
                                    }


                                    for (int k = 0; k < 4; k++)
                                        weights[k] = Utils.Half.decompress(vbr.ReadUInt16());

                                    if (Skinned)
                                    //if (false)
                                    {
                                        for (int k = 0; k < 4; k++)
                                        {
                                            int joint_id = joint_ids[k];

                                            Assimp.VertexWeight vw = new Assimp.VertexWeight();
                                            vw.VertexID = i;
                                            vw.Weight = weights[k];
                                            localJointDict[joint_id].VertexWeights.Add(vw);

                                        }


                                    }


                                    break;
                                }
                            case 6:
                                break; //Handled by 5
                            default:
                                {
                                    throw new Exception("UNIMPLEMENTED BUF Info. PLEASE REPORT");
                                    break;
                                }

                        }
                    }

                }

                //Export Faces
                //Get indices
                ibr.BaseStream.Seek(0, SeekOrigin.Begin);
                bool start = false;
                int fstart = 0;
                for (int i = 0; i < metaData.batchcount / 3; i++)
                {
                    int f1, f2, f3;
                    //NEXT models assume that all gstream meshes have uint16 indices
                    f1 = ibr.ReadUInt16();
                    f2 = ibr.ReadUInt16();
                    f3 = ibr.ReadUInt16();

                    if (!start && Type != TYPES.COLLISION)
                    { fstart = f1; start = true; }
                    else if (!start && Type == TYPES.COLLISION)
                    {
                        fstart = 0; start = true;
                    }

                    int f11, f22, f33;
                    f11 = f1 - fstart;
                    f22 = f2 - fstart;
                    f33 = f3 - fstart;


                    Assimp.Face face = new Assimp.Face();
                    face.Indices.Add(f11);
                    face.Indices.Add(f22);
                    face.Indices.Add(f33);


                    amesh.Faces.Add(face);
                }

                scn.Meshes.Add(amesh);

            }

            node = base.assimpExport(ref scn, ref meshImportStatus);
            node.MeshIndices.Add(meshImportStatus[meshHash]);

            return node;
        }
    





     
     
     
     
     
     
     
     
     * 
     */

    public class AssimpExporter
    {
        public static Node assimpExport(SceneGraphNode m, ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {

            //Default shit
            //Create assimp node
            Node node = new(m.Name);
            node.Transform = MathUtils.convertMatrix(TransformationSystem.GetEntityLocalMat(m));

            //Handle animations maybe?
            if (m.HasComponent<AnimComponent>())
            {
                AnimComponent cmp = m.GetComponent<AnimComponent>() as AnimComponent;
                cmp.AssimpExport(ref scn);
            }
            
            foreach (SceneGraphNode child in m.Children)
            {
                Node c = assimpExport(child, ref scn, ref meshImportStatus);
                node.Children.Add(c);
            }

            return node;
        }


    }
}
