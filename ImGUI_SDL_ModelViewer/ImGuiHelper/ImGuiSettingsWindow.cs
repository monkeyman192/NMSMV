﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using MVCore.Common;
using MVCore.Plugins;


namespace ImGuiHelper
{
    public class ImGuiSettingsWindow
    {
        private bool show_save_confirm_dialog = false;
        private FilePicker activePicker;

        public ImGuiSettingsWindow()
        {

        }

        public void Draw()
        {

            //Assume that a Popup has begun
            ImGui.BeginChild("SettingsWindow", ImGui.GetContentRegionAvail(),
                true, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            foreach (PluginBase plugin in RenderState.engineRef.Plugins)
            {
                plugin.Settings.Draw();
            }

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

            

            if (show_save_confirm_dialog)
            {
                ImGui.OpenPopup("Info");
                show_save_confirm_dialog = false;
            }


            //Popup Actions
            
            
            //Draw Plugin Modals
            foreach (PluginBase plugin in RenderState.engineRef.Plugins)
            {
                plugin.Settings.DrawModals();
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
