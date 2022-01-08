using System;
using System.Collections.Generic;
using NbCore.Primitives;
using NbCore;
using NbCore.Math;
using OpenTK.Graphics.OpenGL4;


namespace NbCore.Text
{
    public class GLText : Primitive
    {
        public NbVector2 pos;
        public NbVector2 size;
        public NbVector3 color;
        public float lineHeight;
        public string text;
        public GLInstancedMesh meshVao;
        public Font font; //Keep reference of the Font used
        public GLText()
        {
            pos = new NbVector2(0.0f);
            lineHeight = 10; //10 pixels text height by default
            color = new NbVector3(1.0f);
        }

        public GLText(Font f, NbVector2 pos, float h, NbVector3 c, string text)
        {
            font = f;
            lineHeight = h;
            this.pos = pos;
            color = c;
            generate(text);
        }

        public void generateMeshVao()
        {
            throw new NotImplementedException();
        }

        public void update(string new_text)
        {

            if (text.Length != new_text.Length)
            {
                //Resize geometry arrays
            }
            else
            {
                //Console.WriteLine("New Text {0}", new_text);
                //Recalculate geometry
                parseTextToGeom(new_text);
                updateGeomVertexBuffer();

                //Replace data
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, geom.vbuffer.Length, geom.vbuffer);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
        }

        private void parseTextToGeom(string text)
        {
            //Generate points
            int caret_pos = 0;
            for (int i = 0; i < text.Length; i++)
            {
                string c = text[i].ToString();
                if (!font.symbols.ContainsKey(c))
                    continue;

                Symbol s = font.symbols[c];

                int vertexOffset = 12 * i;
                int uvOffset = 8 * i;
                int indicesOffset = 6 * i;

                //Normalized data
                float s_x_origin_norm = s.x_origin / 1.0f;
                float s_y_origin_norm = s.y_origin / 1.0f;
                float s_width_norm = s.width / 1.0f;
                float s_height_norm = s.height / 1.0f;

                //Left Down ( 0 )
                verts[vertexOffset + 0] = caret_pos + s_x_origin_norm;
                verts[vertexOffset + 1] = font.baseHeight - s_y_origin_norm;
                verts[vertexOffset + 2] = 0.0f;
                uvs[uvOffset + 0] = (s.x_pos) / (float)font.texWidth;
                uvs[uvOffset + 1] = ((s.y_pos)) / (float)font.texHeight;

                //Left Up ( 1 )
                verts[vertexOffset + 3] = caret_pos + s_x_origin_norm;
                verts[vertexOffset + 4] = font.baseHeight - s_y_origin_norm - s_height_norm;
                verts[vertexOffset + 5] = 0.0f;
                uvs[uvOffset + 2] = (s.x_pos) / (float)font.texWidth;
                uvs[uvOffset + 3] = (s.y_pos + s.height) / (float)font.texHeight;


                //Right Down ( 2 )
                verts[vertexOffset + 6] = caret_pos + s_x_origin_norm + s_width_norm;
                verts[vertexOffset + 7] = font.baseHeight - s_y_origin_norm;
                verts[vertexOffset + 8] = 0.0f;
                uvs[uvOffset + 4] = (s.x_pos + s.width) / (float)font.texWidth;
                uvs[uvOffset + 5] = ((s.y_pos)) / (float)font.texHeight;


                //Right Up ( 3 )
                verts[vertexOffset + 9] = caret_pos + s_x_origin_norm + s_width_norm;
                verts[vertexOffset + 10] = font.baseHeight - s_y_origin_norm - s_height_norm;
                verts[vertexOffset + 11] = 0.0f;
                uvs[uvOffset + 6] = (s.x_pos + s.width) / (float)font.texWidth;
                uvs[uvOffset + 7] = (s.y_pos + s.height) / (float)font.texHeight;


                //Indices
                indices[indicesOffset + 0] = 4 * i;
                indices[indicesOffset + 1] = 4 * i + 2;
                indices[indicesOffset + 2] = 4 * i + 1;
                indices[indicesOffset + 3] = 4 * i + 2;
                indices[indicesOffset + 4] = 4 * i + 3;
                indices[indicesOffset + 5] = 4 * i + 1;

                //Αdvance
                caret_pos += s.advance;

            }

            //Calculate text height
            float max_text_height = 0;
            float min_text_height = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int vertexOffset = 12 * i;

                max_text_height = System.Math.Max(max_text_height, verts[vertexOffset + 1]);
                max_text_height = System.Math.Max(max_text_height, verts[vertexOffset + 4]);
                max_text_height = System.Math.Max(max_text_height, verts[vertexOffset + 7]);
                max_text_height = System.Math.Max(max_text_height, verts[vertexOffset + 10]);

                min_text_height = System.Math.Min(min_text_height, verts[vertexOffset + 1]);
                min_text_height = System.Math.Min(min_text_height, verts[vertexOffset + 4]);
                min_text_height = System.Math.Min(min_text_height, verts[vertexOffset + 7]);
                min_text_height = System.Math.Min(min_text_height, verts[vertexOffset + 10]);

            }

            //Save text size in pixels
            size = new NbVector2(caret_pos, System.Math.Abs(max_text_height - min_text_height));
        }

        private void generate(string text)
        {
            //Init text
            int charNum = text.Length;
            verts = new float[charNum * 4 * 3]; //4 Vector3 per character
            uvs = new float[charNum * 4 * 2]; //4 Vector2 per character
            indices = new int[charNum * 6]; //6 indices per character
            this.text = text;

            parseTextToGeom(text);

            //Generate geometry GL buffers
            geom = getGeom();
            generateMeshVao();
        }

        private void updateGeomVertexBuffer()
        {
            geom.vbuffer = new byte[4 * verts.Length + 4 * uvs.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, 4 * verts.Length); //Verts
            System.Buffer.BlockCopy(uvs, 0, geom.vbuffer, 4 * verts.Length, 4 * uvs.Length); //UVs
        }

        public override GeomObject getGeom(string name="")
        {
            GeomObject geom = new();

            //Set main Geometry Info
            geom.Name = name;
            geom.vertCount = verts.Length / 3;
            geom.indicesCount = indices.Length;
            geom.indicesType = NbPrimitiveDataType.Int;

            //Set Strides
            uint vx_size = 3 * 4;
            uint uv_size = 2 * 4;
            geom.vx_size = vx_size + uv_size;

            //Set Buffer Offsets
            geom.mesh_descr = "vu";
            
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
            geom.bufInfo.Add(buf);
            
            buf = new bufInfo()
            {
                count = 2,
                normalize = false,
                offset = geom.vertCount * 12,
                sem_text = "uvPosition",
                semantic = 1,
                stride = 8,
                type = NbPrimitiveDataType.Float
            };
            geom.bufInfo.Add(buf);
            
            //geom.bufInfo[0] = new bufInfo(0, VertexAttribPointerType.Float, 3, 12, 0, "vPosition", false);
            //geom.bufInfo[1] = new bufInfo(1, VertexAttribPointerType.Float, 2, 8, geom.vertCount * 12, "uvPosition", false);

            //Set Buffers
            geom.ibuffer = new byte[4 * indices.Length];
            System.Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, 4 * indices.Length);
            //Only Verticies
            //geom.vbuffer = new byte[4 * verts.Length];
            //System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, 4 * verts.Length); //Verts

            geom.vbuffer = new byte[4 * verts.Length + 4 * uvs.Length];
            System.Buffer.BlockCopy(verts, 0, geom.vbuffer, 0, 4 * verts.Length); //Verts
            System.Buffer.BlockCopy(uvs, 0, geom.vbuffer, 4 * verts.Length, 4 * uvs.Length); //UVs

            return geom;

        }

        public bool disposedValue = false;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ((IDisposable)meshVao).Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
