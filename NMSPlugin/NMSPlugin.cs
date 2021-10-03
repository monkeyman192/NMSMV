using System;

using MVCore;
using MVCore.Plugins;
using ImGuiHelper;
using ImGuiNET;


namespace NMSPlugin
{
    public class NMSPlugin : PluginBase
    {
        private readonly ImGuiPakBrowser PakBrowser = new();
        private bool show_open_file_dialog_pak = false;
        private bool open_file_enabled = false;
        private bool show_update_libmbin_dialog = false;
        private string libMbinOnlineVersion = null;
        private string libMbinLocalVersion = null;

        public void ShowOpenFileDialogPak()
        {
            show_open_file_dialog_pak = true;
        }

        public void ShowUpdateLibMBINDialog()
        {
            show_update_libmbin_dialog = true;
        }

        private void ProcessModals()
        {
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

            bool isOpen = true;
            if (ImGui.BeginPopupModal("open-file-pak", ref isOpen))
            {
                if (PakBrowser.isFinished())
                {
                    string filename = PakBrowser.SelectedItem;
                    PakBrowser.Clear();
                    ImGui.CloseCurrentPopup();

                    //Issue File Open Request to the Window
                    //Fetch filepath and load Scene
                    //Somehow I should return the scene via the import function to the caller
                
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



        }


        public override void OnLoad()
        {
            Callbacks.Log("* Issuing NMS Archive Preload Request", LogVerbosityLevel.INFO);

            //Issue work request 
            ThreadRequest rq = new();
            rq.arguments.Add("NMSmanifest");
            rq.arguments.Add(Path.Combine(RenderState.settings.GameDir, "PCBANKS"));
            rq.type = THREAD_REQUEST_TYPE.WINDOW_LOAD_NMS_ARCHIVES;
            requestHandler.AddRequest(ref rq);
            workDispatcher.sendRequest(ref rq); //Generate worker
        }

        public override void Import(string filepath)
        {
            throw new NotImplementedException();
        }

        public override void Export(string filepath)
        {
            throw new NotImplementedException();
        }

        public override void OnUnload()
        {
            throw new NotImplementedException();
        }

        public override void DrawImporters(ref Scene scn)
        {
            if (ImGui.BeginMenu("#NMS"))
            {
                if (ImGui.MenuItem("Import from PAK", "", false, open_file_enabled))
                {
                    ShowOpenFileDialogPak();
                }

                if (ImGui.MenuItem("Update LibMBIN"))
                {
                    ShowUpdateLibMBINDialog();
                }

                ImGui.EndMenu();
            }
            
        }

        public override void DrawExporters(Scene scn)
        {
            throw new NotImplementedException();
        }

        public override void Draw()
        {
            throw new NotImplementedException();
        }
    }
}
