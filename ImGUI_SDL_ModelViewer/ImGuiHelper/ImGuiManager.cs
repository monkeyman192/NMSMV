using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using System;
using MVCore;
using ImGUI_SDL_ModelViewer;

namespace ImGuiHelper
{
    static class ImGuiManager
    {
        //ImGui Variables
        static private ImGuiObjectViewer ObjectViewer = new ImGuiObjectViewer();
        static private ImGuiSceneGraphViewer SceneGraphViewer = new();
        static private ImGuiPakBrowser PakBrowser  = new ImGuiPakBrowser();
        static private ImGuiAboutWindow AboutWindow = new ImGuiAboutWindow();
        static private ImGuiSettingsWindow SettingsWindow = new ImGuiSettingsWindow();
        static private bool show_open_file_dialog = false;
        static private bool show_open_file_dialog_pak = false;
        static private bool show_update_libmbin_dialog = false;
        static private bool show_settings_window = false;
        static private bool show_about_window = false;
        static private bool show_test_components = false;
        static private string libMbinOnlineVersion = null;
        static private string libMbinLocalVersion = null;
        static private Window windowRef = null;

        //ImguiPalette Colors
        //Blue
        public static System.Numerics.Vector4 DarkBlue = new(0.04f, 0.2f, 0.96f, 1.0f);

        
        public static void InitImGUI()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; //Enable Docking
        }

        public static void SetWindowRef(Window win)
        {
            windowRef = win;
        }

        public static void ShowSettingsWindow()
        {
            show_settings_window = true;
        }

        public static void ShowAboutWindow()
        {
            show_about_window = true;
        }

        public static void ShowTestComponents()
        {
            show_test_components = true;
        }

        public static void ShowOpenFileDialog()
        {
            show_open_file_dialog = true;
        }

        public static void ShowOpenFileDialogPak()
        {
            show_open_file_dialog_pak = true;
        }

        public static void ShowUpdateLibMBINDialog()
        {
            show_update_libmbin_dialog = true;
        }

        //Object Viewer Related Methods

        public static void DrawObjectInfoViewer()
        {
            ObjectViewer?.Draw();
        }

        public static void SetObjectReference(Entity m)
        {
            ObjectViewer.SetModel(m);
        }

        //SceneGraph Related Methods

        public static void DrawSceneGraph()
        {
            SceneGraphViewer?.Draw();
        }

        public static void PopulateSceneGraph(SceneGraphNode m)
        {
            SceneGraphViewer.Init(m);
        }

        public static void ClearSceneGraph()
        {
            SceneGraphViewer.Clear();
        }

        public static void ProcessModals(GameWindow win, string current_file_path)
        {
            //Functionality

            if (show_open_file_dialog)
            {
                ImGui.OpenPopup("open-file");
                show_open_file_dialog = false;
            }

            if (show_open_file_dialog_pak)
            {
                ImGui.OpenPopup("open-file-pak");
                show_open_file_dialog_pak = false;
            }

            if (show_update_libmbin_dialog)
            {
                ImGui.OpenPopup("update-libmbin");
                show_update_libmbin_dialog = false;
            }

            if (show_about_window)
            {
                ImGui.OpenPopup("show-about");
                show_about_window = false;
            }

            if (show_settings_window)
            {
                ImGui.OpenPopup("show-settings");
                show_settings_window = false;
            }
            
            var isOpen = true;
            if (ImGui.BeginPopupModal("open-file", ref isOpen, ImGuiWindowFlags.NoTitleBar))
            {
                var picker = FilePicker.GetFilePicker(win, current_file_path, ".SCENE.MBIN|.SCENE.EXML");
                if (picker.Draw())
                {
                    Console.WriteLine(picker.SelectedFile);
                    current_file_path = picker.CurrentFolder;
                    FilePicker.RemoveFilePicker(win);
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("open-file-pak", ref isOpen))
            {
                if (PakBrowser.isFinished())
                {
                    string filename = PakBrowser.SelectedItem;
                    PakBrowser.Clear();
                    ImGui.CloseCurrentPopup();
                    
                    //Issue File Open Request to the Window
                    ThreadRequest req = new ThreadRequest();
                    req.type = THREAD_REQUEST_TYPE.WINDOW_OPEN_FILE;
                    req.arguments.Add(filename);
                    
                    windowRef.SendRequest(ref req);
                }
                else
                {
                    PakBrowser.Draw();
                }

                if (ImGui.IsKeyPressed(ImGui.GetKeyIndex(ImGuiKey.Escape)))
                {
                    PakBrowser.Clear();
                    ImGui.CloseCurrentPopup();
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


            if (ImGui.BeginPopupModal("show-about", ref isOpen, ImGuiWindowFlags.NoResize))
            {

                ImGuiNative.igSetNextWindowSize(new System.Numerics.Vector2(256 + 36, 256 + 60), ImGuiCond.Appearing);
                if (ImGui.IsKeyPressed(ImGui.GetKeyIndex(ImGuiKey.Escape)))
                {
                    ImGui.CloseCurrentPopup();
                }

                AboutWindow.Draw();

                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("show-settings", ref isOpen))
            {

                ImGuiNative.igSetNextWindowSize(new System.Numerics.Vector2(800, 256 + 60), ImGuiCond.Always);
                if (ImGui.IsKeyPressed(ImGui.GetKeyIndex(ImGuiKey.Escape)))
                {
                    ImGui.CloseCurrentPopup();
                }

                SettingsWindow.Draw();

                ImGui.EndPopup();
            }

        }



    }

    

    
    
}
