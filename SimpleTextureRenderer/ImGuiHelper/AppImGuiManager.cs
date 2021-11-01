using OpenTK.Windowing.Desktop;
using System;
using ImGuiHelper;
using ImGuiNET;
using NbCore;


namespace SimpleTextureRenderer
{
    public class AppImGuiManager : ImGuiManager
    {
        private bool show_open_file_dialog = false;
        private string current_file_path = "";

        public AppImGuiManager(GameWindow win, Engine engine) : base(win, engine)
        {

        }

        public void ShowOpenFileDialog()
        {
            show_open_file_dialog = true;
        }

        public override void ProcessModals(GameWindow win, ref string current_file_path, ref bool isDialogOpen)
        {
            //Functionality

            if (show_open_file_dialog)
            {
                ImGui.OpenPopup("open-file");
                show_open_file_dialog = false;
            }

            var winsize = new System.Numerics.Vector2(500, 250);
            ImGui.SetNextWindowSize(winsize);
            if (ImGui.BeginPopupModal("open-file", ref isDialogOpen, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize))
            {
                var picker = FilePicker.GetFilePicker(win, current_file_path, ".DDS");
                if (picker.Draw(new System.Numerics.Vector2(winsize.X - 15, winsize.Y - 60)))
                {
                    Console.WriteLine(picker.SelectedFile);
                    current_file_path = picker.SelectedFile;
                    FilePicker.RemoveFilePicker(win);
                    isDialogOpen = false;
                } 
                ImGui.EndPopup();
            }

        }
    }
}
