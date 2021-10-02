using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using System;
using MVCore;
using ImGuiHelper;

namespace ImGUI_SDL_ModelViewer
{
    class AppImGuiManager : ImGuiManager
    {
        //ImGui Variables
        private readonly ImGuiObjectViewer ObjectViewer = new();
        private readonly ImGuiSceneGraphViewer SceneGraphViewer;
        private readonly ImGuiPakBrowser PakBrowser  = new();
        private readonly ImGuiMaterialEditor MaterialEditor = new();
        private readonly ImGuiShaderEditor ShaderEditor = new();
        private readonly ImGuiAboutWindow AboutWindow = new();
        private readonly ImGuiSettingsWindow SettingsWindow = new();
        private bool show_open_file_dialog = false;
        private bool show_open_file_dialog_pak = false;
        private bool show_update_libmbin_dialog = false;
        private bool show_settings_window = false;
        private bool show_about_window = false;
        private bool show_test_components = false;
        private string libMbinOnlineVersion = null;
        private string libMbinLocalVersion = null;
        
        public AppImGuiManager(Window win) : base(win)
        {
            SceneGraphViewer = new(this);
        }

        public void ShowSettingsWindow()
        {
            show_settings_window = true;
        }

        public void ShowAboutWindow()
        {
            show_about_window = true;
        }

        public void ShowTestComponents()
        {
            show_test_components = true;
        }

        public void ShowOpenFileDialog()
        {
            show_open_file_dialog = true;
        }

        public void ShowOpenFileDialogPak()
        {
            show_open_file_dialog_pak = true;
        }

        public void ShowUpdateLibMBINDialog()
        {
            show_update_libmbin_dialog = true;
        }
        
        //SceneGraph Related Methods

        public override void ProcessModals(GameWindow win, ref string current_file_path, ref bool OpenFileDialogFinished)
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
                    
                    (WindowRef as Window).SendRequest(ref req);
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
