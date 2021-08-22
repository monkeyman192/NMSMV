using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using MVCore.Common;


namespace ImGuiHelper
{
    public class ImGuiSettingsWindow
    {
        private bool show_gamedir_folder_select = false;
        private bool show_unpackdir_folder_select = false;
        private bool show_save_confirm_dialog = false;
        private string current_file_path = "";
        private FilePicker activePicker;

        public ImGuiSettingsWindow()
        {

        }

        public void Draw()
        {

            //Assume that a Popup has begun
            ImGui.BeginChild("SettingsWindow", ImGui.GetContentRegionAvail(),
                true, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);


            ImGui.Columns(2);
            //Game Directory
            ImGui.Text("NMS Installation Folder");
            ImGui.NextColumn();
            ImGui.Text(RenderState.settings.GameDir);
            ImGui.SameLine();

            if (ImGui.Button("Select"))
            {
                activePicker = FilePicker.GetFilePicker(this, current_file_path, null, true);
                show_gamedir_folder_select = true;
            }

            ImGui.NextColumn();
            //Unpacked Files Directory
            ImGui.Text("NMS Unpacked Folder");
            ImGui.NextColumn();
            ImGui.Text(RenderState.settings.UnpackDir);
            ImGui.SameLine();

            if (ImGui.Button("Select"))
            {
                activePicker = FilePicker.GetFilePicker(this, current_file_path, null, true);
                show_unpackdir_folder_select = true;
            }

            ImGui.Columns(1);
            ImGui.SliderInt("ProcGen Window Number", ref RenderState.settings.ProcGenWinNum, 1, 10);
            ImGui.Checkbox("Force ProcGen", ref RenderState.settings.ForceProcGen);

            //Render Settings
            ImGui.BeginGroup();
            ImGui.TextColored(ImGuiManager.DarkBlue, "Rendering Settings");
            ImGui.SliderFloat("HDR Exposure", ref RenderState.settings.renderSettings.HDRExposure, 0.001f, 0.5f);
            ImGui.InputInt("FPS", ref RenderState.settings.renderSettings.FPS);
            ImGui.Checkbox("Vsync", ref RenderState.settings.renderSettings.UseVSync);
            ImGui.EndGroup();

            if (ImGui.Button("Save Settings"))
            {
                Settings.saveToDisk(RenderState.settings);
                show_save_confirm_dialog = true;
            }


            ImGui.EndChild();

            //Popup Init

            if (show_gamedir_folder_select)
            {
                ImGui.OpenPopup("Select Game Directory");
                show_gamedir_folder_select = false;
            }

            if (show_unpackdir_folder_select)
            {
                ImGui.OpenPopup("Select Unpacked File Directory");
                show_unpackdir_folder_select = false;
            }

            if (show_save_confirm_dialog)
            {
                ImGui.OpenPopup("Info");
                show_save_confirm_dialog = false;
            }


            //Popup Actions

            if (ImGui.BeginPopupModal("Select Game Directory"))
            {
                if (activePicker.Draw())
                {
                    RenderState.settings.GameDir = activePicker.SelectedFile;
                    FilePicker.RemoveFilePicker(this);
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("Select Unpacked File Directory"))
            {
                if (activePicker.Draw())
                {
                    RenderState.settings.GameDir = activePicker.SelectedFile;
                    FilePicker.RemoveFilePicker(this);
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("Info"))
            {
                ImGui.Text("Settings Saved Successfully!");
                ImGui.EndPopup();
            }



        }

        ~ImGuiSettingsWindow()
        {

        }
    }
}
