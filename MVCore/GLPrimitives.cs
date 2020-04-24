﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace MVCore.Primitives
{
    public class Primitive
    {
        internal float[] verts;
        internal float[] normals;
        internal int[] indices;

        internal GMDL.GeomObject geom;

        public GMDL.GeomObject getGeom()
        {
            GMDL.GeomObject geom = new GMDL.GeomObject();

            //Set main Geometry Info
            geom.vertCount = verts.Length / 3;
            geom.indicesCount = indices.Length;
            geom.indicesLength = 0x4;

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each
            
            //Set Buffer Offsets
            geom.offsets = new int[7];
            geom.bufInfo = new List<GMDL.bufInfo>();
            
            for (int i = 0; i< 7; i++)
            {
                geom.bufInfo.Add(null);
                geom.offsets[i] = -1;
            }

            geom.mesh_descr = "vn";
            geom.offsets[0] = 0;
            geom.offsets[2] = 0;
            geom.bufInfo[0] = new GMDL.bufInfo(0, VertexAttribPointerType.Float, 3, 0, "vPosition", false);
            geom.bufInfo[2] = new GMDL.bufInfo(2, VertexAttribPointerType.Float, 3, 0, "nPosition", false);


            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            geom.vbuffer = new byte[4 * verts.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, geom.vbuffer.Length);

            return geom;
        }

        public GMDL.GLVao getVAO()
        {
            return geom?.generateVAO();
        }
    }

    public class Sphere : Primitive {
        
        //Constructor
        public Sphere(Vector3 center, float radius)
        {
            int latBands = 10;
            int longBands = 10;

            //Init Arrays
            int arraysize = (latBands + 1) * (longBands + 1) * 3;
            int indarraysize = latBands * longBands * 3;
            verts = new float[arraysize];
            normals = new float[arraysize];
            indices = new int[2 * indarraysize];
            

            for (int lat = 0; lat <= latBands; lat++)
            {
                float theta = lat * (float)Math.PI / latBands;
                float sintheta = (float)Math.Sin(theta);
                float costheta = (float)Math.Cos(theta);

                for (int lng = 0; lng <= longBands; lng++)
                {
                    float phi = lng * 2 * (float) Math.PI / longBands;
                    float sinphi = (float) Math.Sin(phi);
                    float cosphi = (float) Math.Cos(phi);

                    float x = cosphi * sintheta;
                    float y = costheta;
                    float z = sinphi * sintheta;

                    verts[lat * (longBands + 1) * 3 + 3 * lng + 0] = center.X + radius * x;
                    verts[lat * (longBands + 1) * 3 + 3 * lng + 1] = center.Y + radius * y;
                    verts[lat * (longBands + 1) * 3 + 3 * lng + 2] = center.Z + radius * z;
                    
                    normals[lat * latBands * 3 + 3 * lng + 0] = x;
                    normals[lat * latBands * 3 + 3 * lng + 1] = y;
                    normals[lat * latBands * 3 + 3 * lng + 2] = z;
                }
            }


            //Indices
            for (int lat = 0; lat < latBands; lat++)
            {
                for (int lng = 0; lng < longBands; lng++)
                {
                    int first = lat * (longBands + 1) + lng;
                    int second = first + longBands + 1;

                    indices[lat * longBands * 6 + 6 * lng + 0] = second;
                    indices[lat * longBands * 6 + 6 * lng + 1] = first;
                    indices[lat * longBands * 6 + 6 * lng + 2] = first + 1;
                    

                    indices[lat * longBands * 6 + 6 * lng + 3] = second + 1;
                    indices[lat * longBands * 6 + 6 * lng + 4] = second;
                    indices[lat * longBands * 6 + 6 * lng + 5] = first + 1;
                }
            }
            

            
            geom = getGeom();
        }

    }

    class Capsule : Primitive
    {
        //Constructor
        public Capsule(Vector3 center, float height, float radius)
        {
            int latBands = 11;
            int longBands = 11;

            //Init Arrays
            int arraysize = (latBands + 1) * (longBands + 1) * 3;
            int indarraysize = latBands * longBands * 3;
            verts = new float[arraysize];
            normals = new float[arraysize];
            indices = new int[2 * indarraysize];

            List<float> vlist = new List<float>();
            List<int> ilist = new List<int>();


            for (int lat = 0; lat <= latBands; lat++)
            {
                float theta = lat * (float)Math.PI / latBands;
                float sintheta = (float)Math.Sin(theta);
                float costheta = (float)Math.Cos(theta);

                for (int lng = 0; lng <= longBands; lng++)
                {
                    float phi = lng * 2 * (float)Math.PI / longBands;
                    float sinphi = (float)Math.Sin(phi);
                    float cosphi = (float)Math.Cos(phi);

                    float x = cosphi * sintheta;
                    float y = costheta;
                    float z = sinphi * sintheta;

                    verts[lat * (longBands + 1) * 3 + 3 * lng + 0] = center.X + radius * x;
                    if (lat <= latBands / 2)
                        verts[lat * (longBands + 1) * 3 + 3 * lng + 1] = center.Y + (0.5f * height - radius) + radius * y;
                    else
                        verts[lat * (longBands + 1) * 3 + 3 * lng + 1] = center.Y - (0.5f * height - radius) + radius * y;
                    verts[lat * (longBands + 1) * 3 + 3 * lng + 2] = center.Z + radius * z;

                    normals[lat * latBands * 3 + 3 * lng + 0] = x;
                    normals[lat * latBands * 3 + 3 * lng + 1] = y;
                    normals[lat * latBands * 3 + 3 * lng + 2] = z;
                }
            }


            //Indices
            for (int lat = 0; lat < latBands; lat++)
            {
                for (int lng = 0; lng < longBands; lng++)
                {
                    int first = lat * (longBands + 1) + lng;
                    int second = first + longBands + 1;

                    indices[lat * longBands * 6 + 6 * lng + 0] = second;
                    indices[lat * longBands * 6 + 6 * lng + 1] = first;
                    indices[lat * longBands * 6 + 6 * lng + 2] = first + 1;


                    indices[lat * longBands * 6 + 6 * lng + 3] = second + 1;
                    indices[lat * longBands * 6 + 6 * lng + 4] = second;
                    indices[lat * longBands * 6 + 6 * lng + 5] = first + 1;
                }
            }
            geom = getGeom();
        }

    }

    class Cylinder : Primitive
    {
        //Constructor
        public Cylinder(float radius, float height)
        {
            int latBands = 10;
            
            //Init Arrays
            int arraysize = latBands;
            verts = new float[2* (1 + arraysize) * 3];
            normals = new float[2* (1 + arraysize) * 3];
            indices = new int[3 * latBands + 3*latBands + latBands * 2 * 3];

            //Add Top Cap Verts
            float y = height / 2.0f;
            //Add center vertex
            verts[0] = 0.0f;
            verts[1] = y;
            verts[2] = 0.0f;
            
            for (int lat = 0; lat < latBands; lat++)
            {
                float theta = lat * (2 * (float) Math.PI / latBands);
                verts[3 + 3 * lat + 0] = radius * (float) Math.Cos(theta);
                verts[3 + 3 * lat + 1] = y;
                verts[3 + 3 * lat + 2] = radius * (float) Math.Sin(theta);
            }

            //Top Cap Indices
            for (int lat = 1; lat < latBands; lat++)
            {
                indices[3 * (lat - 1) + 0] = 0;
                indices[3 * (lat - 1) + 1] = lat;
                indices[3 * (lat - 1) + 2] = lat+1;
            }
            //Close the circle
            indices[3 * (latBands - 1) + 0] = 0;
            indices[3 * (latBands - 1) + 1] = latBands;
            indices[3 * (latBands - 1) + 2] = 1;


            //Add Bottom Cap Verts
            int voff = (latBands + 1) * 3;
            //Add center vertex
            verts[voff + 0] = 0.0f;
            verts[voff + 1] = -y;
            verts[voff + 2] = 0.0f;

            
            for (int lat = 0; lat < latBands; lat++)
            {
                float theta = lat * (2 * (float)Math.PI / latBands);
                verts[voff + 3 + 3 * lat + 0] = radius * (float)Math.Cos(theta);
                verts[voff + 3 + 3 * lat + 1] = -y;
                verts[voff + 3 + 3 * lat + 2] = radius * (float)Math.Sin(theta);
            }

            //Bottom Cap Indices
            int ioff = latBands + 1;
            int array_ioff = 3 * latBands;
            for (int lat = 1; lat < latBands; lat++)
            {
                indices[array_ioff + 3 * (lat - 1) + 0] = ioff + 0;
                indices[array_ioff + 3 * (lat - 1) + 1] = ioff + lat;
                indices[array_ioff + 3 * (lat - 1) + 2] = ioff + lat + 1;
            }
            //Close the circle
            indices[array_ioff + 3 * (latBands - 1) + 0] = ioff + 0;
            indices[array_ioff + 3 * (latBands - 1) + 1] = ioff + latBands;
            indices[array_ioff + 3 * (latBands - 1) + 2] = ioff + 1;


            //Fix Side Indices
            //No need to add other vertices all are there
            array_ioff = 2 * 3 * latBands;
            for (int lat = 1; lat < latBands; lat++)
            {
                //First Tri
                indices[array_ioff + 6 * (lat - 1) + 0] = lat;
                indices[array_ioff + 6 * (lat - 1) + 1] = lat + latBands + 1;
                indices[array_ioff + 6 * (lat - 1) + 2] = lat + latBands + 2;
                //Second Tri
                indices[array_ioff + 6 * (lat - 1) + 3] = lat;
                indices[array_ioff + 6 * (lat - 1) + 4] = lat + latBands + 2;
                indices[array_ioff + 6 * (lat - 1) + 5] = lat + 1;
            }
            //Last quad
            indices[array_ioff + 6 * (latBands - 1) + 0] = latBands;
            indices[array_ioff + 6 * (latBands - 1) + 1] = 2*latBands + 1;
            indices[array_ioff + 6 * (latBands - 1) + 2] = latBands + 2;
            //Second Tri
            indices[array_ioff + 6 * (latBands - 1) + 3] = 1;
            indices[array_ioff + 6 * (latBands - 1) + 4] = latBands;
            indices[array_ioff + 6 * (latBands - 1) + 5] = latBands +2;

            geom = getGeom();
        }
        
    }

    class Box : Primitive
    {
        //Constructor
        public Box(float width, float height, float depth)
        {
            //Init Arrays
            verts = new float[8*3];
            normals = new float[8*3];
            indices = new int[12*3];

            //Verts
            //0
            verts[0] = width / 2.0f;
            verts[1] = height / 2.0f;
            verts[2] = depth / 2.0f;
            //1
            verts[3] = -width / 2.0f;
            verts[4] = height / 2.0f;
            verts[5] = depth / 2.0f;
            //2
            verts[6] = -width / 2.0f;
            verts[7] = height / 2.0f;
            verts[8] = -depth / 2.0f;
            //3
            verts[9] = width / 2.0f;
            verts[10] = height / 2.0f;
            verts[11] = -depth / 2.0f;
            //4
            verts[12] = width / 2.0f;
            verts[13] = -height / 2.0f;
            verts[14] = depth / 2.0f;
            //5
            verts[15] = -width / 2.0f;
            verts[16] = -height / 2.0f;
            verts[17] = depth / 2.0f;
            //6
            verts[18] = -width / 2.0f;
            verts[19] = -height / 2.0f;
            verts[20] = -depth / 2.0f;
            //7
            verts[21] = width / 2.0f;
            verts[22] = -height / 2.0f;
            verts[23] = -depth / 2.0f;

            indices = new int[]{0, 1, 2,
                1, 3, 2,
                4, 5, 6,
                4, 6, 7,
                1, 2, 5,
                2, 6, 5,
                0, 4, 3,
                4, 7, 3,
                2, 3, 7,
                2, 7, 6,
                0, 1, 4,
                1, 5, 4 };

            geom = getGeom();
        }

    }

    class Quad :Primitive
    {
        
        //Constructor
        public Quad(float width, float height)
        {
            //Init Arrays

            //Define Quad
            verts = new float[6 * 3] {
               -1.0f*width/2, 0.0f, -1.0f*height/2,
                1.0f*width/2, 0.0f, -1.0f*height/2,
               -1.0f*width/2, 0.0f,  1.0f*height/2,
               -1.0f*width/2, 0.0f,  1.0f*height/2,
                1.0f*width/2, 0.0f, -1.0f*height/2,
                1.0f*width/2, 0.0f,  1.0f*height/2};

            normals = new float[6 * 3] {
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f };

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
            indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            geom = getGeom();
        }

        //RenderQuad Constructor
        public Quad()
        {
            //Init Arrays
            //Define Quad
            verts = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

            normals = new float[6 * 3] {
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f,
                0.0f, 0.0f, 1.0f };

            //Indices
            indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            geom = getGeom();
        }
    
    }

    class Cross : Primitive
    {
        //Constructor
        public Cross(float scale)
        {
            //Set type
            //this.type = "LOCATOR";
            //Assemble geometry in the constructor
            //X
            verts = new float[6 * 3] { 1.0f, 0.0f, 0.0f,
                   -1.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, -1.0f, 0.0f,
                    0.0f, 0.0f, 1.0f,
                    0.0f, 0.0f, -1.0f};

            //Apply Scane to verts
            for (int i = 0; i < 3 * 6; i++)
                verts[i] *= scale;

            int arraysize = 6 * 3;
            int b_size = 2 * arraysize;
            float[] verts_combined = new float[b_size];

            Array.Copy(verts, 0, verts_combined, 0, arraysize);
            //Colors
            float[] colors = new float[6 * 3] { 1.0f, 0.0f, 0.0f,
                    1.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 1.0f,
                    0.0f, 0.0f, 1.0f};

            Array.Copy(colors, 0, verts_combined, arraysize, arraysize);

            //Indices
            indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            //Replace verts
            verts = verts_combined;

            geom = getGeom();
        }

        public new GMDL.GeomObject getGeom()
        {
            GMDL.GeomObject geom = new GMDL.GeomObject();

            //Set main Geometry Info
            geom.vertCount = 6;
            geom.indicesCount = indices.Length;
            geom.indicesLength = 0x4;

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            geom.offsets = new int[7];
            geom.bufInfo = new List<GMDL.bufInfo>();

            for (int i = 0; i < 7; i++)
            {
                geom.bufInfo.Add(null);
                geom.offsets[i] = -1;
            }

            geom.mesh_descr = "vn";
            geom.offsets[0] = 0;
            geom.offsets[2] = 0;
            geom.offsets[4] = 0;
            geom.bufInfo[0] = new GMDL.bufInfo(0, VertexAttribPointerType.Float, 3, 0, "vPosition", false);
            geom.bufInfo[2] = new GMDL.bufInfo(2, VertexAttribPointerType.Float, 3, 72, "nPosition", false);
            geom.bufInfo[4] = new GMDL.bufInfo(4, VertexAttribPointerType.Float, 3, 72, "bPosition", false);

            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            geom.vbuffer = new byte[4 * verts.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, geom.vbuffer.Length);

            return geom;
        }
    }

    class LineSegment : Primitive
    {
        //Constructor
        public LineSegment(int instance_num, Vector3 color)
        {
            instance_num = Math.Max(instance_num, 1); //Should be always >=1
            verts = new float[instance_num * 2 * 3];
            Array.Clear(verts, 0, instance_num * 2 * 3);
            
            int arraysize = instance_num * 2 * 3;
            int b_size = 2 * arraysize;
            float[] verts_combined = new float[b_size];

            Array.Copy(verts, 0, verts_combined, 0, arraysize);
            //Colors
            float[] colors = new float[instance_num * 2 * 3];

            for (int i=0; i < instance_num; i++)
            {
                colors[6 * i + 0] = color.X;
                colors[6 * i + 1] = color.Y;
                colors[6 * i + 2] = color.Z;
                colors[6 * i + 3] = color.X;
                colors[6 * i + 4] = color.Y;
                colors[6 * i + 5] = color.Z;
            }

            Array.Copy(colors, 0, verts_combined, arraysize, arraysize);

            //Indices
            indices = new Int32[instance_num * 2];
            for (int i = 0; i <instance_num * 2; i++)
                indices[i] = i;
            
            //Replace verts
            verts = verts_combined;

            geom = getGeom();
        }

        public new GMDL.GeomObject getGeom()
        {
            GMDL.GeomObject geom = new GMDL.GeomObject();

            //Set main Geometry Info
            geom.vertCount = 2;
            geom.indicesCount = indices.Length;
            geom.indicesLength = 0x4;

            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            geom.offsets = new int[7];
            geom.bufInfo = new List<GMDL.bufInfo>();

            for (int i = 0; i < 7; i++)
            {
                geom.bufInfo.Add(null);
                geom.offsets[i] = -1;
            }

            geom.mesh_descr = "vn";
            geom.offsets[0] = 0;
            geom.offsets[2] = 0;
            geom.bufInfo[0] = new GMDL.bufInfo(0, VertexAttribPointerType.Float, 3, 0, "vPosition", false);
            geom.bufInfo[2] = new GMDL.bufInfo(2, VertexAttribPointerType.Float, 3, 24, "nPosition", false);


            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            geom.vbuffer = new byte[4 * verts.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, geom.vbuffer.Length);

            return geom;
        }
    }

}
