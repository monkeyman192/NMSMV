using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NbCore;
using NbCore.Common;
using NbCore.Plugins;
using ImGuiNET;
using Newtonsoft.Json;
using System.Linq;

namespace NMSPlugin
{
    public class NMSPluginSettings : PluginSettings
    {
        [JsonIgnore]
        public static string DefaultSettingsFileName = "NbPlugin_NMS.ini";
        public string GameDir;
        public string UnpackDir;
        
        public new static PluginSettings GenerateDefaultSettings()
        {
            NMSPluginSettings settings = new()
            {
                GameDir = "",
                UnpackDir = ""
            };
            return settings;
        }

        public override void Draw()
        {
            ImGui.InputText("Game Directory", ref GameDir, 200);
            ImGui.InputText("Unpack Directory", ref GameDir, 200);
        }

        public override void DrawModals()
        {
            
        }

        public override void SaveToFile()
        {
            string jsondata = JsonConvert.SerializeObject(this);
            //Get Plugin Directory
            string plugindirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            File.WriteAllText(Path.Join(plugindirectory, DefaultSettingsFileName), jsondata);
        }
    }

    //Shared state across the NMSPlugin domain just to make import procedures easier 
    //(Avoid passing plugin settings everywhere)
    public static class PluginState
    {
        public static NMSPluginSettings Settings;
        public static Random Randgen = new Random();
    }

    public class NMSPlugin : PluginBase
    {
        public static string PluginName = "NMSPlugin";
        public static string PluginVersion = "1.0.0";
        public static string PluginDescription = "NMS Plugin for Nibble Engine.";
        public static string PluginCreator = "gregkwaste";

        private readonly ImGuiPakBrowser PakBrowser = new();
        private bool show_open_file_dialog_pak = false;
        private bool open_file_enabled = false;
        private bool show_update_libmbin_dialog = false;
        private string libMbinOnlineVersion = null;
        private string libMbinLocalVersion = null;

        public NMSPlugin(Engine e) : base(e)
        {
            base.Name = NMSPlugin.PluginName;
            base.Version = NMSPlugin.PluginVersion;
            base.Description = NMSPlugin.PluginDescription;
            base.Creator = NMSPlugin.PluginCreator;
        }
        
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
                    
                    show_open_file_dialog_pak = false;
                    ImGui.CloseCurrentPopup();
                    Import(filename);
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
                    libMbinLocalVersion = NbCore.Utils.HTMLUtils.queryLibMBINDLLLocalVersion();

                if (libMbinOnlineVersion == null)
                {
                    libMbinOnlineVersion = NbCore.Utils.HTMLUtils.queryLibMBINDLLOnlineVersion();
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
                    NbCore.Utils.HTMLUtils.updateLibMBIN();
                    libMbinLocalVersion = null;
                    libMbinOnlineVersion = null;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();

            }
        }


        public override void OnLoad()
        {
            Log(" Loading NMS Plugin...", LogVerbosityLevel.INFO);

            string plugindirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string settingsfilepath = Path.Join(plugindirectory, NMSPluginSettings.DefaultSettingsFileName);

            //Load Plugin Settings
            if (File.Exists(settingsfilepath))
            {
                string filedata = File.ReadAllText(settingsfilepath);
                Settings = JsonConvert.DeserializeObject<NMSPluginSettings>(filedata);
            }
            else
            {
                Log(" Settings file not found.", LogVerbosityLevel.INFO);
                Settings = NMSPluginSettings.GenerateDefaultSettings() as NMSPluginSettings;
                Settings.SaveToFile();
            }
            //Set State
             PluginState.Settings = Settings as NMSPluginSettings;

            //Create a separate thread to try and load PAK archives
            //Issue work request 

            Thread t = new Thread(() => {
                FileUtils.loadNMSArchives(Path.Combine(PluginState.Settings.GameDir, "PCBANKS"), 
                    ref open_file_enabled);
            });
            t.Start();
            
            //Add Defaults
            AddDefaultTextures();
        }

        private void AddDefaultTextures()
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            EngineRef.AddTexture(Callbacks.getResourceFromAssembly(currentAssembly, "default.dds"), "default.dds");
            EngineRef.AddTexture(Callbacks.getResourceFromAssembly(currentAssembly, "default_mask.dds"), "default_mask.dds");
        }
        
        public override void Import(string filepath)
        {
            Log(string.Format("Importing {0}", filepath), LogVerbosityLevel.INFO);

            //Re-init pallete
            Palettes.set_palleteColors();

            Importer.SetEngineReference(EngineRef);
            SceneGraphNode root = Importer.ImportScene(filepath);

            EngineRef.GetActiveScene().Clear();
            EngineRef.RegisterSceneGraphNode(root);
            
            //root.Dispose(); //Dispose since I don't use it for now
            //TODO: Register Scene to Engine
        }

        public override void Export(string filepath)
        {
            
        }

        public override void OnUnload()
        {
            FileUtils.unloadNMSArchives();
            //TODO: Add possibly other cleanups
        }

        public override void DrawImporters()
        {
            if (ImGui.BeginMenu("NMS"))
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
            
        }

        public override void Draw()
        {
            ProcessModals();
        }
    }
}
