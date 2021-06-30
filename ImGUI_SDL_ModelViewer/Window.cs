using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using ImGuiNET;
using ImGuiHelper;
using OpenTK.Windowing.Common;
using MVCore.Engine;

namespace ImGUI_SDL_ModelViewer
{
    public class Window : GameWindow
    {
        ImGuiController _controller;

        //Parameters
        private string current_file_path = Environment.CurrentDirectory;
        private string status_string = "Ready";

        //Engine
        private Engine engine;

        //ImGui Variables
        private bool show_open_file_dialog = false;
        private bool show_open_file_dialog_pak = false;
        private bool show_update_libmbin_dialog = false;
        private string libMbinOnlineVersion = null;
        private string libMbinLocalVersion = null;

        public Window() : base(GameWindowSettings.Default, 
            new NativeWindowSettings() { Size = new Vector2i(800, 600), APIVersion = new Version(4, 5) })
        { }

        protected override void OnLoad()
        {
            base.OnLoad();
            Title = "NMSMV " + Util.getVersion();
            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);


            //Initialize Engine backend
            //engine = new Engine(this);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            // Tell ImGui of the new size
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            _controller.Update(this, (float) e.Time);

            GL.ClearColor(new Color4(0, 32, 48, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            //Render Shit

            
            //TODO Draw UI
            DrawUI();
            //ImGui.ShowDemoWindow();
            
            _controller.Render();

            //ImGuiUtil.CheckGLError("End of frame");

            SwapBuffers();
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            _controller.PressChar((char)e.Unicode);
        }
        

        private void DrawUI()
        {
            //if (show_open_file_dialog) OpenFileDialog(ref show_open_file_dialog);

            //Main Menu
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open", "Ctrl + O"))
                    {
                        show_open_file_dialog = true;
                    }

                    if (ImGui.MenuItem("Open from PAK"))
                    {
                        show_open_file_dialog_pak = true;
                    }

                    if (ImGui.MenuItem("Update LibMBIN"))
                    {
                        //TODO
                        show_update_libmbin_dialog = true;
                    }

                    if (ImGui.MenuItem("Close", "Ctrl + Q"))
                    {
                        //TODO, properly cleanup and close the window
                        Close();
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }


            ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, Size.Y - 2.0f * ImGui.CalcTextSize("test").Y));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(Size.X, 1.75f * ImGui.CalcTextSize("test").Y));

            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            
            if (ImGui.Begin("StatusBar", ImGuiWindowFlags.NoMove |
                                         ImGuiWindowFlags.NoDecoration))
            {
                ImGui.Columns(2, "statusbarColumns", false);
                ImGui.Text(status_string);
                ImGui.NextColumn();
                string text = "Created by gregkwaste";
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(text).X
                    - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X);
                
                ImGui.Text("Created by gregkwaste");
                ImGui.End();
            }

            
            //Functionality
                

            if (show_open_file_dialog)
            {
                ImGui.OpenPopup("open-file");
                show_open_file_dialog = false;
            }

            if (show_update_libmbin_dialog)
            {
                ImGui.OpenPopup("update-libmbin");
                show_update_libmbin_dialog = false;
            }

                
            var isOpen = true;
            if (ImGui.BeginPopupModal("open-file", ref isOpen, ImGuiWindowFlags.NoTitleBar))
            {
                var picker = ImGuiHelper.FilePicker.GetFilePicker(this, current_file_path, ".SCENE.MBIN|.SCENE.EXML");
                if (picker.Draw())
                {
                    Console.WriteLine(picker.SelectedFile);
                    current_file_path = picker.CurrentFolder;
                    FilePicker.RemoveFilePicker(this);
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("update-libmbin", ref isOpen, ImGuiWindowFlags.None))
            {
                if (libMbinLocalVersion == null)
                    libMbinLocalVersion = MVCore.Utils.HTMLUtils.queryLibMBINDLLLocalVersion();
                
                if (libMbinOnlineVersion == null)
                {
                    libMbinOnlineVersion = MVCore.Utils.HTMLUtils.queryLibMBINDLLOnlineVersion();
                }
                    
                ImGui.Text("Old Version: " + libMbinLocalVersion);
                ImGui.Text("Online Version: " + libMbinOnlineVersion);
                ImGui.Text("Do you want to update?");

                bool updatelibmbin = false;
                if (ImGui.Button("YES"))
                {
                    updatelibmbin = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("NO"))
                {
                    libMbinLocalVersion = null;
                    libMbinOnlineVersion = null;
                    ImGui.CloseCurrentPopup();
                }

                if (updatelibmbin)
                {
                    //MVCore.Utils.HTMLUtils.fetchLibMBINDLL();
                    libMbinLocalVersion = null;
                    libMbinOnlineVersion = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();

            }


        }

    }
}
