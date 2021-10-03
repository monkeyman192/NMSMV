using MVCore.Plugins;
using Newtonsoft.Json;
using ImGuiNET;
using ImGuiHelper;

namespace NibbleAssimpPlugin
{
    public class NMSPluginSettings : PluginSettings
    {
        [JsonIgnore]
        public static string GameVersion = "";

        public string GameDir;
        public string UnpackDir;
        public int ProcGenWinNum;
        public bool ForceProcGen;
        private FilePicker activePicker;
        private string current_file_path = "";
        private bool show_gamedir_folder_select = false;
        private bool show_unpackdir_folder_select = false;
        

        public NMSPluginSettings()
        {
            PluginName = "NMS";
            GameVersion = "Something";
        }

        public override void Draw()
        {

            ImGui.Columns(2);
            //Game Directory
            ImGui.Text("NMS Installation Folder");
            ImGui.NextColumn();
            ImGui.Text(GameDir);
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
            ImGui.Text(UnpackDir);
            ImGui.SameLine();

            if (ImGui.Button("Select"))
            {
                activePicker = FilePicker.GetFilePicker(this, current_file_path, null, true);
                show_unpackdir_folder_select = true;
            }

            ImGui.Columns(1);
            ImGui.SliderInt("ProcGen Window Number", ref ProcGenWinNum, 1, 10);
            ImGui.Checkbox("Force ProcGen", ref ForceProcGen);


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
        }

        public override void DrawModals()
        {
            var winsize = new System.Numerics.Vector2(500, 250);

            if (ImGui.BeginPopupModal("Select Game Directory"))
            {
                if (activePicker.Draw(new System.Numerics.Vector2(winsize.X - 15, winsize.Y - 60)))
                {
                    GameDir = activePicker.SelectedFile;
                    FilePicker.RemoveFilePicker(this);
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupModal("Select Unpacked File Directory"))
            {
                if (activePicker.Draw(new System.Numerics.Vector2(winsize.X - 15, winsize.Y - 60)))
                {
                    GameDir = activePicker.SelectedFile;
                    FilePicker.RemoveFilePicker(this);
                }
                ImGui.EndPopup();
            }
        }

        public override PluginSettings GenerateDefaultSettings()
        {
            NMSPluginSettings settings = new();

            //Game is available only on windows :(
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                settings.GameDir = FileUtils.getGameInstallationDir();
            else
                settings.GameDir = "";
            
            settings.UnpackDir = settings.GameDir;
        }
    }
}
