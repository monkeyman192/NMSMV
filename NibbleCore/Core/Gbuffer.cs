using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using NbCore.Math;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace NbCore
{

    public struct FBOAttachment
    {
        public TextureTarget target;
        public PixelInternalFormat format;
    }

    [Flags]
    public enum FBOOptions
    {
        None = 0,
        MultiSample = 1
    }

    public class FBO : IDisposable
    {
        public int fbo = -1;
        private List<FBOAttachment> ColorAttachments;
        private FBOAttachment DepthAttachment;
        private FBOOptions options = FBOOptions.None;
        private List<int> channels;
        public int depth_channel = -1;
        
        //Buffer Specs
        public NbVector2i Size;
        public int msaa_samples = 8;

        public FBO(int x, int y, FBOOptions opts = FBOOptions.None)
        {
            //Setup properties
            Size = new(x, y);
            ColorAttachments = new();
            channels = new();
            options = opts;

            setup();
        }

        public int GetChannel(int i)
        {
            return channels[i];
        }

        public void setup()
        {
            //Init the main FBO
            fbo = GL.GenFramebuffer();

            //Main flags
            if (options.HasFlag(FBOOptions.MultiSample))
                GL.Enable(EnableCap.Multisample);
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        public void AddAttachment(TextureTarget target, PixelInternalFormat format, bool isDepth)
        {
            FBOAttachment attachment = new()
            {
                target = target,
                format = format
            };

            //TODO: Add check so that depth attachment is not overriden

            if (!isDepth)
            {
                int channel_id = setup_texture(target, fbo, FramebufferAttachment.ColorAttachment0 + channels.Count, format);
                ColorAttachments.Add(attachment);
                channels.Add(channel_id);
            } else
            {
                depth_channel = setup_texture(target, fbo, FramebufferAttachment.DepthAttachment, PixelInternalFormat.DepthComponent);
                channels.Add(depth_channel);
                DepthAttachment = attachment;
            }
        }

        public int setup_texture(TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id, PixelInternalFormat format)
        {
            int handle = GL.GenTexture();

            if (textarget == TextureTarget.Texture2DMultisample)
            {
                GL.BindTexture(TextureTarget.Texture2DMultisample, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, Size.X, Size.Y, true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.BindFramebuffer(FramebufferTarget.FramebufferExt, attach_to_fbo);
                GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, attachment_id, TextureTarget.Texture2DMultisample, handle, 0);

                
            }
            else if (textarget == TextureTarget.Texture2D)
            {
                GL.BindTexture(TextureTarget.Texture2D, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                //GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);

                switch (format)
                {
                    case PixelInternalFormat.DepthComponent:
                        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, Size.X, Size.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                        break;
                    default:
                        GL.TexImage2D(TextureTarget.Texture2D, 0, format, Size.X, Size.Y, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
                        break;
                }

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);


                GL.BindFramebuffer(FramebufferTarget.FramebufferExt, attach_to_fbo);
                GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, attachment_id, TextureTarget.Texture2D, handle, 0);

            }
            else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            return handle;
        }


        public void resize(int w, int h)
        {
            Size = new(w, h);
            Cleanup();
            channels.Clear();

            setup();
            //Add color attachments
            for (int i = 0; i < ColorAttachments.Count; i++)
            {
                channels.Add(setup_texture(ColorAttachments[i].target,
                                           fbo,
                                           FramebufferAttachment.ColorAttachment0 + channels.Count,
                                           ColorAttachments[i].format));
            }

            channels.Add(setup_texture(DepthAttachment.target,
                                           fbo, FramebufferAttachment.DepthAttachment,
                                           DepthAttachment.format));
        }

        public void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            for (int i = 0; i < channels.Count; i++)
                GL.DeleteTexture(channels[i]);

        }

        //STATIC HELPER METHODS

        public static void copyChannel(int from_fbo, int to_fbo, int sourceSizeX, int sourceSizeY, int destSizeX, int destSizeY,
            ReadBufferMode from_channel, DrawBufferMode to_channel)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from_fbo);
            GL.ReadBuffer(from_channel); //Read color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel); //Write to blur1

            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, destSizeX, destSizeY,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
        }

        public static void copyDepthChannel(int from_fbo, int to_fbo, int sourceSizeX, int sourceSizeY, int destSizeX, int destSizeY)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from_fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            
            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, destSizeX, destSizeY,
            ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        public static void copyChannel(int fbo, int sourceSizeX, int sourceSizeY, ReadBufferMode from_channel, DrawBufferMode to_channel)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.ReadBuffer(from_channel); //Read color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.DrawBuffer(to_channel); //Write to blur1

            //Method 1: Use Blitbuffer
            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, sourceSizeX, sourceSizeY,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }

        public static void dumpChannelToImage(int fbo_id, ReadBufferMode from_channel, string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo_id);
            GL.ReadBuffer(from_channel);
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
            for (int j = 0; j < width; j++)
                bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                    (int)pixels[4 * (width * i + j) + 0],
                    (int)pixels[4 * (width * i + j) + 1],
                    (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", System.Drawing.Imaging.ImageFormat.Png);
        }

        //Disposable Stuff
        private bool disposedValue;
        
        private void Dispose(bool disposing)
        {
            if (disposedValue)
                return;

            if (disposing)
            {
                ColorAttachments.Clear();
            }
            
            //Free unmanaged resources
            GL.DeleteFramebuffer(fbo);
            foreach (int tex_id in channels)
                GL.DeleteTexture(tex_id);
            
            
            disposedValue = true;
        }
        
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        
    }

//    public class PBuffer
//    {
//        public int fbo = -1;
        
//        //Pixel Bufffer Textures
//        public int color = -1;
//        public int blur1 = -1;
//        public int blur2 = -1;
//        public int composite = -1;
//        public int depth = -1;

//        //Buffer Specs
//        public int[] size;
//        public int msaa_samples = 8;


//        public PBuffer(int x, int y)
//        {
//            size = new int[] { x, y };
//            setup();
//        }

//        public void setup()
//        {
//            //Init the main FBO
//            fbo = GL.GenFramebuffer();

//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Setup color texture
//            setup_texture(ref color, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(color, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment0);
//            //Setup blur1 texture
//            setup_texture(ref blur1, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(blur1, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment1);
//            //Setup blur2 texture
//            setup_texture(ref blur2, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(blur2, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment2);
//            //Setup composite texture
//            setup_texture(ref composite, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
//            bindTextureToFBO(composite, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment3);
            
//            //Setup depth texture
//            setup_texture(ref depth, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true);
//            bindTextureToFBO(depth, TextureTarget.Texture2D, fbo, FramebufferAttachment.DepthAttachment);

//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Revert Back the default fbo
//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//        }


//        public void setup_texture(ref int handle, TextureTarget textarget, PixelInternalFormat format, bool isDepth)
//        {
//            handle = GL.GenTexture();
//            GL.BindTexture(textarget, handle);

//            if (textarget == TextureTarget.Texture2DMultisample)
//            {
//                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

//            }
//            else if (textarget == TextureTarget.Texture2D)
//            {
//                if (isDepth)
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                else
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);


//                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
//                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
//                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

//            }
//            else
//            {
//                throw new Exception("Unsupported texture target " + textarget);
//            }
//        }

//        //TODO: Organize this function a bit
//        public void bindTextureToFBO(int texHandle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id)
//        {
//            GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
//            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, textarget, texHandle, 0);
//        }

//        public void Cleanup()
//        {

//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//            //Delete Buffer
//            GL.DeleteFramebuffer(fbo);

//            //Delete textures
//            GL.DeleteTexture(color);
//            GL.DeleteTexture(blur1);
//            GL.DeleteTexture(blur2);
//            GL.DeleteTexture(composite);
//            GL.DeleteTexture(depth);

//        }

//        public void resize(int w, int h)
//        {
//            size[0] = w;
//            size[1] = h;

//            Cleanup();
//            setup();
//        }


        

//    }

//    public class GBuffer : IDisposable
//    {
//        public int fbo = -1;

//        //Textures
//        public int albedo = -1;
//        public int normals = -1;
//        public int info = -1;
//        public int depth = -1;
//        public int depth_dump = -1;

//        //Buffer Specs
//        public int[] size;
//        public int msaa_samples = 4;

//        public GBuffer(int x, int y)
//        {
//            //Setup all stuff
//            //Init size to the current GLcontrol size
//            size = new int[] { x, y };

//            init();
//            setup();
//        }

//        public void setup()
//        {
//            //Init the main FBO
//            fbo = GL.GenFramebuffer();
            
//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Setup diffuse texture
//            setup_texture(ref albedo, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
//            bindTextureToFBO(albedo, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment0);
//            //Setup normals texture
//            setup_texture(ref normals, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
//            bindTextureToFBO(normals, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment1);
//            //Setup info texture 
//            setup_texture(ref info, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
//            bindTextureToFBO(info, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment2);
//            //Setup Depth texture
//            setup_texture(ref depth, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true, TextureMagFilter.Nearest, TextureMinFilter.Nearest);
//            bindTextureToFBO(depth, TextureTarget.Texture2D, fbo, FramebufferAttachment.DepthAttachment);
            
//            //Setup depth backup  texture
//            setup_texture(ref depth_dump, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true, TextureMagFilter.Nearest, TextureMinFilter.Nearest);

//            //Check
//            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
//                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

//            //Revert Back the default fbo
//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//        }

//        public void setup_texture(ref int handle, TextureTarget textarget, PixelInternalFormat format, bool isDepth, TextureMagFilter texMagFilter, TextureMinFilter texMinFilter)
//        {
//            handle = GL.GenTexture();
//            GL.BindTexture(textarget, handle);

//            if (textarget == TextureTarget.Texture2DMultisample)
//            {
//                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
//            }
//            else if (textarget == TextureTarget.Texture2D)
//            {
//                if (isDepth)
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
//                else
//                    GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

//                //Setup Texture Parameters
//                GL.TexParameter(textarget, TextureParameterName.TextureMagFilter, (int)texMagFilter);
//                GL.TexParameter(textarget, TextureParameterName.TextureMinFilter, (int)texMinFilter);
//                //GL.TexParameter(textarget, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
//                //GL.TexParameter(textarget, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
//            }
//            else
//            {
//                throw new Exception("Unsupported texture target " + textarget);
//            }
//        }

//        //TODO: Organize this function a bit
//        public void bindTextureToFBO(int texHandle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id)
//        {
//            GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
//            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, textarget, texHandle, 0);
//        }

        
//        public void init()
//        {
//            GL.Viewport(0, 0, size[0], size[1]);
//            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
//            //Main flags
//            GL.Enable(EnableCap.Multisample);
            
//            //Geometry Shader Parameters
//            GL.PatchParameter(PatchParameterFloat.PatchDefaultInnerLevel, new float[] { 2.0f });
//            GL.PatchParameter(PatchParameterFloat.PatchDefaultOuterLevel, new float[] { 4.0f, 4.0f, 4.0f });
//            GL.PatchParameter(PatchParameterInt.PatchVertices, 3);
//        }
        
//        public void bind()
//        {
//            //Bind Gbuffer fbo
//            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
//            GL.Viewport(0, 0, size[0], size[1]);
//            GL.DrawBuffers(3, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0,
//                                                      DrawBuffersEnum.ColorAttachment1,
//                                                      DrawBuffersEnum.ColorAttachment2} );
//        }

//        public void clearColor(Vector4 col)
//        {
//            GL.ClearColor(col.X, col.Y, col.Z, col.W);
//        }

//        public void clear(ClearBufferMask mask)
//        {
//            GL.Clear(mask);
//        }

//        public void stop()
//        {
//            //Blit can replace the render & stop funtions
//            //Simply resolves and copies the ms offscreen fbo to the default framebuffer without any need to render the textures and to any other post proc effects
//            //I guess that I don't need the textures as well, when I'm rendering like this
//            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
//            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
//            //Blit
//            GL.BlitFramebuffer(0, 0, size[0], size[1],
//                                   0, 0, size[0], size[1],
//                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
//                                   BlitFramebufferFilter.Nearest);
//        }

//        public void dump()
//        {
//            //Bind Buffers
//            //Resolving Buffers
//            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
//            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

//            byte[] pixels = new byte[16 * size[0] * size[1]];
//            //pixels = new byte[4 * size[0] * size[1]];
//            //Console.WriteLine("Dumping Framebuffer textures " + size[0] + " " + size[1]);

//#if false
//            //Save Depth Texture
//            GL.BindTexture(TextureTarget.Texture2D, dump_depth);
//            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, pixels);

//            File.WriteAllBytes("dump.depth", pixels);
//#endif

//#if false
//            //Read Color0
//            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
//            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
//            GL.BlitFramebuffer(0, 0, size[0], size[1],
//                                   0, 0, size[0], size[1],
//                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
//                                   BlitFramebufferFilter.Nearest);


//            //Save Diffuse Color
//            GL.BindTexture(TextureTarget.Texture2D, dump_diff);
//            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

//            //File.WriteAllBytes("dump.color0", pixels);
//#endif


//            //Rebind Gbuffer fbo
//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
//            //GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
//            //GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

//        }

//        public void Cleanup()
//        {

//            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
//            //Delete Buffer
//            GL.DeleteFramebuffer(fbo);

//            //Delete textures
//            GL.DeleteTexture(albedo);
//            GL.DeleteTexture(normals);
//            GL.DeleteTexture(info);
//            GL.DeleteTexture(depth);
//            GL.DeleteTexture(depth_dump);
//        }

//        public void resize(int w, int h)
//        {
//            size[0] = w;
//            size[1] = h;
            
//            Cleanup();
//            setup();
//        }


        

//        #region IDisposable Support
//        private bool disposedValue = false; // To detect redundant calls

//        protected virtual void Dispose(bool disposing)
//        {
//            if (!disposedValue)
//            {
//                if (disposing)
//                {
//                    // TODO: dispose managed state (managed objects).
//                    Cleanup();
//                }

//                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
//                // TODO: set large fields to null.

//                disposedValue = true;
//            }
//        }

//        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
//        //~GBuffer()
//        //{
//        //    Cleanup();
//        //    GL.DeleteBuffer(quad_vbo);
//        //    GL.DeleteBuffer(quad_ebo);
//        //}

//        // This code added to correctly implement the disposable pattern.
//        public void Dispose()
//        {
//            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
//            Dispose(true);
//            // TODO: uncomment the following line if the finalizer is overridden above.
//            // GC.SuppressFinalize(this);
//        }
//        #endregion


//    }

}
