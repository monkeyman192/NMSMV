using System;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System.IO;
using MVCore.Common;
using System.Drawing;
using System.Drawing.Imaging;

namespace MVCore
{
    public class Texture : Entity
    {
        public int texID = -1;
        private bool disposed = false;
        public TextureTarget target;
        public string Name;
        public int Width;
        public int Height;
        public int Depth;
        public int MipMapCount;
        public InternalFormat pif;
        public PaletteOpt palOpt;
        public Vector4 procColor;
        public Vector3 avgColor;

        //Empty Initializer
        public Texture() :base(EntityType.Texture) { }
        //Path Initializer

        public Texture(byte[] data, bool isDDS, string name) : base(EntityType.Texture)
        {
            if (isDDS)
            {
                textureInitDDS(data, name);
            } else
            {
                textureInit(data, "temp");
            }
        }

        public Texture(string path, bool isCustom = false) : base(EntityType.Texture)
        {
            Stream fs;
            byte[] image_data;
            int data_length;

            if (!isCustom)
            {
                try
                {
                    fs = Import.NMS.FileUtils.LoadNMSFileStream(path);
                }
                catch (FileNotFoundException)
                {
                    //FileNotFoundExceptions during texture loading, are caught so that default textures are loaded
                    fs = null;
                }
            }
            else
                fs = new FileStream(path, FileMode.Open);


            if (fs == null)
            {
                //throw new System.IO.FileNotFoundException();
                Console.WriteLine("Texture {0} Missing. Using default.dds", path);

                //Load default.dds from resources
                image_data = File.ReadAllBytes("default.dds");
                data_length = image_data.Length;
            }
            else
            {
                data_length = (int) fs.Length;
                image_data = new byte[data_length];
            }

            fs.Read(image_data, 0, data_length);

            
            textureInit(image_data, path);
        }

        private void textureInitPNG(byte[] imageData, string _name)
        {
            MemoryStream ms = new MemoryStream(imageData);
            
            //Load the image from file
            Bitmap bmpTexture = new Bitmap(ms);
            bmpTexture.Save("test_image.bmp");
            
            //Convert the image to a form compatible with openGL
            Rectangle rctImageBounds = new Rectangle(0, 0, bmpTexture.Width, bmpTexture.Height);

            BitmapData oTextureData = bmpTexture.LockBits(rctImageBounds, ImageLockMode.ReadOnly,
                                                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            //Console.WriteLine(GL.GetError());
            //Generate PBO
            //pboID = GL.GenBuffer();
            //GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboID);
            //GL.BufferData(BufferTarget.PixelUnpackBuffer, 
            //        oTextureData.Stride * oTextureData.Height, oTextureData.Scan0, 
            //        BufferUsageHint.StaticDraw);
            
            //Upload to GPU
            texID = GL.GenTexture();
            target = TextureTarget.Texture2D;

            //Copy the image data into the texture
            GL.BindTexture(target, texID);
            GL.TexImage2D(target, 0, PixelInternalFormat.Rgba, bmpTexture.Width, bmpTexture.Height, 0, 
                OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, oTextureData.Scan0);
            
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, (float) TextureMinFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, (float) TextureMagFilter.Linear);

            
            bmpTexture.UnlockBits(oTextureData);

            //Cleanup
            bmpTexture = null;
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); //Unbind texture PBO

        }

        public void SubTextureData(byte[] data, int mipmap_id, int depth_id)
        {


        }

        
        private void textureInitDDS(byte[] imageData, string _name)
        {
            DDSImage ddsImage;
            Name = _name;

            ddsImage = new DDSImage(imageData);
            RenderStats.texturesNum += 1; //Accumulate settings

            Console.WriteLine("Sampler Name Path " + Name + " Width {0} Height {1}", 
                ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            Width = ddsImage.header.dwWidth;
            Height = ddsImage.header.dwHeight;
            int blocksize = 16;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext;
                    blocksize = 8;
                    break;
                //DXT5
                case (0x35545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext;
                    break;
                //ATI2A2XY
                case (0x32495441):
                    pif = InternalFormat.CompressedRgRgtc2; //Normal maps are probably never srgb
                    break;
                //DXT10 HEADER
                case (0x30315844):
                    {
                        switch (ddsImage.header10.dxgiFormat)
                        {
                            case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
                                pif = InternalFormat.CompressedSrgbAlphaBptcUnorm;
                                break;
                            default:
                                throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
                        }
                        break;
                    }
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }

            //Temp Variables
            int w = Width;
            int h = Height;
            int mm_count = Math.Max(1, ddsImage.header.dwMipMapCount); //Fix the counter to 1 to handle textures with single mipmaps
            int depth_count = Math.Max(1, ddsImage.header.dwDepth); //Fix the counter to 1 to fit the texture in a 3D container
            int temp_size = ddsImage.header.dwPitchOrLinearSize;

            MipMapCount = mm_count;
            Depth = depth_count;
            
            //Generate PBO
            GL.BufferData(BufferTarget.PixelUnpackBuffer, ddsImage.Data.Length, ddsImage.Data, BufferUsageHint.StaticDraw);
            //GL.BufferSubData(BufferTarget.PixelUnpackBuffer, IntPtr.Zero, ddsImage.bdata.Length, ddsImage.bdata);

            //Upload to GPU
            texID = GL.GenTexture();
            target = TextureTarget.Texture2DArray;

            GL.BindTexture(target, texID);
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0);
            //GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mm_count - 1);

            int offset = 0;
            for (int i = 0; i < mm_count; i++)
            {
                byte[] temp_data = new byte[temp_size * depth_count];
                System.Buffer.BlockCopy(ddsImage.Data, offset, temp_data, 0, temp_size * depth_count);
                GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, temp_data);
                offset += temp_size * depth_count;

                w = Math.Max(w >> 1, 1);
                h = Math.Max(h >> 1, 1);

                temp_size = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * blocksize;
                //This works only for square textures
                //temp_size = Math.Max(temp_size/4, blocksize);
            }

            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            //Console.WriteLine(GL.GetError());

            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float)Math.Max(af_amount, 4.0f);
            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            GL.GetTexParameter(target, GetTextureParameter.TextureMaxLevel, out int max_level);
            GL.GetTexParameter(target, GetTextureParameter.TextureBaseLevel, out int base_level);

            int maxsize = Math.Max(Height, Width);
            int p = (int)Math.Floor(Math.Log(maxsize, 2)) + base_level;
            int q = Math.Min(p, max_level);

#if (DEBUGNONO)
            //Get all mipmaps
            temp_size = ddsImage.header.dwPitchOrLinearSize;
            for (int i = 0; i < q; i++)
            {
                //Get lowest calculated mipmap
                byte[] pixels = new byte[temp_size];
                
                //Save to disk
                GL.GetCompressedTexImage(TextureTarget.Texture2D, i, pixels);
                File.WriteAllBytes("Temp\\level" + i.ToString(), pixels);
                temp_size = Math.Max(temp_size / 4, 16);
            }
#endif

#if (DUMP_TEXTURESNONO)
            Sampler.dump_texture(name.Split('\\').Last().Split('/').Last(), width, height);
#endif
            //avgColor = getAvgColor(pixels);
            ddsImage = null;
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); //Unbind texture PBO
        }

        public void textureInit(byte[] imageData, string _name)
        {
            string ext = Path.GetExtension(_name).ToUpper();

            switch (ext)
            {
                case ".DDS":
                    {
                        textureInitDDS(imageData, _name);
                        break;
                    }
                case ".PNG":
                    {
                        textureInitPNG(imageData, _name);
                        break;
                    }
                default:
                    {
                        Console.WriteLine("Unsupported Texture Extension");
                        break;
                    }
            }

        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                if (texID != -1) GL.DeleteTexture(texID);
            }

            //Free unmanaged resources
            disposed = true;
        }

        private Vector3 getAvgColor(byte[] pixels)
        {
            //Assume that I have the 4x4 mipmap
            //I need to fetch the first 2 colors and calculate the Average

            MemoryStream ms = new MemoryStream(pixels);
            BinaryReader br = new BinaryReader(ms);

            int color0 = br.ReadUInt16();
            int color1 = br.ReadUInt16();

            br.Close();

            //int rmask = 0x1F << 11;
            //int gmask = 0x3F << 5;
            //int bmask = 0x1F;
            uint temp;

            temp = (uint)(color0 >> 11) * 255 + 16;
            char r0 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color0 & 0x07E0) >> 5) * 255 + 32;
            char g0 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color0 & 0x001F) * 255 + 16;
            char b0 = (char)((temp / 32 + temp) / 32);

            temp = (uint)(color1 >> 11) * 255 + 16;
            char r1 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color1 & 0x07E0) >> 5) * 255 + 32;
            char g1 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color1 & 0x001F) * 255 + 16;
            char b1 = (char)((temp / 32 + temp) / 32);

            char red = (char)(((int)(r0 + r1)) / 2);
            char green = (char)(((int)(g0 + g1)) / 2);
            char blue = (char)(((int)(b0 + b1)) / 2);


            return new Vector3(red / 256.0f, green / 256.0f, blue / 256.0f);

        }

        private ulong PackRGBA(char r, char g, char b, char a)
        {
            return (ulong)((r << 24) | (g << 16) | (b << 8) | a);
        }

        public static void dump_texture(int texid, TextureTarget target, string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.BindTexture(target, texid);
            GL.GetTexImage(target, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.Byte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
                bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                    (int)pixels[4 * (width * i + j) + 0],
                    (int)pixels[4 * (width * i + j) + 1],
                    (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }

    }

}
