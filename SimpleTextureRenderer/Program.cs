using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MVCore;
using OpenTK;
using GLSLHelper;
using MVCore.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using ImGuiNET;
using ImGuiHelper;

namespace SimpleTextureRenderer
{
    public class TextureRenderer : OpenTK.Windowing.Desktop.GameWindow
    {
        private Texture _texture;
        private Engine _engine;
        private DDSImage _ddsImage;
        private int mipmap_id = 0;
        private int depth_id = 0;
        private GLSLShaderConfig shader_conf;
        private int quad_vao_id;
        AppImGuiManager _ImGuiManager;
        private float scroll_delta_y = 0f;

        //Imgui stuff
        private bool IsOpenFileDialogOpen = false;
        
        public TextureRenderer(): base(OpenTK.Windowing.Desktop.GameWindowSettings.Default,
            OpenTK.Windowing.Desktop.NativeWindowSettings.Default)
        {
            Title = "DDS Texture Viewer v1.0";
            VSync = VSyncMode.On;
            RenderFrequency = 60;
            _ImGuiManager = new(this);
        }
        
        public void compileShader(GLSLShaderConfig config)
        {
            if (config.ProgramID != -1)
                GL.DeleteProgram(config.ProgramID);

            GLShaderHelper.CreateShaders(config);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            _ImGuiManager.Resize(ClientSize.X, ClientSize.Y);
            base.OnResize(e);
        }

        private void OpenFile(string filepath)
        {

            if (_texture != null)
                _texture.Dispose();
            
            _texture = new Texture(filepath, true);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            Callbacks.SetDefaultCallbacks();

            //Initialize Engine
            _engine = new Engine(this);
            RenderState.engineRef = _engine;
            _engine.init(ClientSize.X, ClientSize.Y);

            GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);
            //GL.Enable(EnableCap.DepthTest);

            //Setup Texture
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\BEAMGRADIENT.DDS";
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\SCROLLINGCLOUD.DDS";
            //string texturepath = "E:\\SSD_SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\COMMON\\ROBOTS\\QUADRUPED.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.HSV.DDS";
            //string texturepath = "D:\\Downloads\\TILEMAP.NORMAL.DDS";

            _texture = new Texture(Callbacks.getResource("default.dds"), 
                                   true, "default");
            
            //Compile Necessary Shaders

            string vs_path = "Shaders/Gbuffer_VS.glsl";
            vs_path = Path.GetFullPath(vs_path);
            vs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);

            string fs_path = "Shaders/texture_shader_fs.glsl";
            fs_path = Path.GetFullPath(fs_path);
            fs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);

            GLSLShaderSource vs = new GLSLShaderSource(vs_path, true);
            GLSLShaderSource fs = new GLSLShaderSource(fs_path, true);

            //Pass Shader
            shader_conf = GLShaderHelper.compileShader(vs, fs, null, null, null,
                new(), new(), SHADER_TYPE.MATERIAL_SHADER, SHADER_MODE.DEFAULT);

            compileShader(shader_conf);

            //Generate Geometry

            //Default render quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad();
            quad_vao_id = q.getVAO().vao_id;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            scroll_delta_y += e.OffsetY;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            
            base.OnRenderFrame(e);
            //Update Imgui
            //TODO: maybe group mouse data to a struct and pass that one instead
            _ImGuiManager.Update(e.Time, scroll_delta_y); 
            scroll_delta_y = 0.0f; //Reset Scroll delta from frame to frame
            
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);
            GL.ClearColor(1.0f, 1.0f, 0.0f, 0.0f);
            
            GL.UseProgram(shader_conf.ProgramID);
            //Console.WriteLine("1" + GL.GetError());

            GL.BindVertexArray(quad_vao_id);
            //Console.WriteLine("2" + GL.GetError());

            //Upload texture
            GL.Uniform1(shader_conf.uniformLocations["InTex"], 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, _texture.texID);

            //Upload Uniforms
            GL.Uniform1(shader_conf.uniformLocations["texture_depth"], (float)depth_id);
            GL.Uniform1(shader_conf.uniformLocations["mipmap"], (float) mipmap_id);

            //Console.WriteLine("3" + GL.GetError());

            //Render quad
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);


            //Draw UI
            //_ImGuiManager.Render();
            DrawUI();
            //ImGui.ShowDemoWindow();
            _ImGuiManager.Render();

            //Console.WriteLine("4" + GL.GetError());

            SwapBuffers();
        }

        private void DrawUI()
        {
            //Draw Main MenuBar
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open"))
                    {
                        _ImGuiManager.ShowOpenFileDialog();
                        IsOpenFileDialogOpen = true;
                    }

                    if (ImGui.MenuItem("Close"))
                    {
                        //Dispose stuff and close
                        _texture.Dispose();
                        _engine.CleanUp();
                        Close();
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }


            if (_texture != null) {

                if (ImGui.Begin("Texture Properties"))
                {
                    ImGui.Text("Info:");
                    ImGui.Columns(2);
                    ImGui.Text("Width");
                    ImGui.Text("Height");
                    ImGui.Text("Depth");
                    ImGui.Text("MipMapCount");
                    ImGui.Text("Format");
                    ImGui.NextColumn();
                    ImGui.Text(_texture.Width.ToString());
                    ImGui.Text(_texture.Height.ToString());
                    ImGui.Text(_texture.Depth.ToString());
                    ImGui.Text(_texture.MipMapCount.ToString());

                    //Make format output a bit friendlier
                    switch (_texture.pif)
                    {
                        case InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext:
                            ImGui.Text("DXT5");
                            break;
                        case InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext:
                            ImGui.Text("DXT1");
                            break;
                        case InternalFormat.CompressedRgRgtc2:
                            ImGui.Text("ATI2A2XY");
                            break;
                        case InternalFormat.CompressedSrgbAlphaBptcUnorm:
                            ImGui.Text("BC7 (DX10 Header)");
                            break;
                        default:
                            ImGui.Text("UNKNOWN");
                            break;
                    }

                    ImGui.NextColumn();
                    ImGui.Separator();
                    //Prepare depth options
                    ImGui.Text("Active Depth:");
                    ImGui.NextColumn();


                    string[] opts = new string[_texture.Depth];
                    for (int i = 0; i < opts.Length; i++)
                        opts[i] = i.ToString();
                    ImGui.Combo("##0", ref depth_id, opts, _texture.Depth, 12);

                    ImGui.NextColumn();
                    ImGui.Text("Active Mipmap:");

                    opts = new string[_texture.MipMapCount];
                    for (int i = 0; i < opts.Length; i++)
                        opts[i] = i.ToString();

                    ImGui.NextColumn();
                    ImGui.Combo("##1", ref mipmap_id, opts, _texture.MipMapCount, 12);

                    ImGui.Columns(1);
                    ImGui.End();
                }

            }

            //Main StatusBar
            float textHeight = ImGui.GetTextLineHeight();
            ImGuiViewportPtr vp = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, vp.Size.Y - 1.4f * textHeight));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(vp.Size.X, 1.6f * textHeight));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new System.Numerics.Vector2(0f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.0f);

            ImGuiWindowFlags sbarFlags = ImGuiWindowFlags.NoResize |
                                         ImGuiWindowFlags.NoScrollbar |     
                                         ImGuiWindowFlags.NoTitleBar;
            bool sbar_open = true;

            if (ImGui.Begin("##TestWindow", ref sbar_open, sbarFlags))
            {
                //StatusBar Texts
                string statusText = "Ready";
                string copyrightText = "Created by gregkwaste©";
                ImGui.Columns(2, "#statusbar", false);
                ImGui.SetCursorPosY(2.0f);
                ImGui.Text(statusText);
                ImGui.NextColumn();
                
                ImGui.SetColumnOffset(ImGui.GetColumnIndex(), vp.Size.X - ImGui.CalcTextSize(copyrightText).X);
                ImGui.SetCursorPosY(2.0f);
                ImGui.Text("Made by gregkwaste");
                ImGui.Columns(1);
                ImGui.End();
            }

            ImGui.PopStyleVar(4);


            //Process Modals
            bool oldOpenDialogStatus = IsOpenFileDialogOpen;
            string filePath = "";
            _ImGuiManager.ProcessModals(this, ref filePath, ref IsOpenFileDialogOpen);

            if (oldOpenDialogStatus == true && IsOpenFileDialogOpen == false)
            {
                //Open File
                OpenFile(filePath);
            }



        }

        [STAThread]
        public static void Main()
        {
            using (TextureRenderer tx = new TextureRenderer())
            {
                tx.Run();
            }
            
            Console.WriteLine("All Good");
        }
    }
}
