using System;
using System.Collections.Generic;
using System.IO;
using libMBIN;
using OpenTK;
using OpenTK.Mathematics;
using libMBIN.NMS.Toolkit;
using System.Security.Permissions;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using Microsoft.Win32;
using Newtonsoft.Json;
using Path = System.IO.Path;
using MVCore.Common;
using System.Windows;
using System.Reflection;
using libMBIN.NMS;

namespace MVCore.Utils
{
    public static class NMSUtils
    {
        public static NMSTemplate LoadNMSFileOLD(string filepath)
        {
            int load_mode = 0;
            NMSTemplate template;

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case

            if (File.Exists(exmlpath))
                load_mode = 0;
            else
                load_mode = 1;


            //Load Exml
            try
            {
                if (load_mode == 0)
                {
                    string xml = File.ReadAllText(exmlpath);
                    template = EXmlFile.ReadTemplateFromString(xml);
                }
                else
                {
                    if (!File.Exists(filepath))
                        throw new FileNotFoundException("File not found\n " + filepath);
                    libMBIN.MBINFile mbinf = new libMBIN.MBINFile(filepath);
                    mbinf.Load();
                    template = mbinf.GetData();
                    mbinf.Dispose();
                }
            } catch (Exception ex)
            {
                if (ex is System.IO.DirectoryNotFoundException || ex is System.IO.FileNotFoundException)
                {
                    Callbacks.showError("File " + filepath + " Not Found...", "Error");


                } else if (ex is System.Reflection.TargetInvocationException)
                {
                    Callbacks.showError("libMBIN failed to decompile the file. Try to update the libMBIN.dll (File->updateLibMBIN). If the issue persists contact the developer", "Error");
                }
                return null;

            }

            return template;
        }


        public static Stream LoadNMSFileStream(string filepath, ref ResourceManager resMgr)
        {
            int load_mode = 0;
            
            string conv_filepath = filepath.TrimStart('/');
            filepath = filepath.Replace('\\', '/');
            string effective_filepath = filepath;

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case

            if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, exmlpath)))
                load_mode = 0; //Load Exml
            else if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, filepath)))
                load_mode = 1; //Load MBIN from file
            else if (resMgr.NMSFileToArchiveMap.ContainsKey(filepath))
                load_mode = 2; //Extract file from archive
            else if (resMgr.NMSFileToArchiveMap.ContainsKey("/" + filepath))
            {
                effective_filepath = "/" + filepath;
                load_mode = 2; //Extract file from archive
            } else
            {
                Callbacks.Log("File: " + filepath + " Not found in PAKs or local folders. ", LogVerbosityLevel.ERROR);
                Callbacks.showError("File: " + filepath + " Not found in PAKs or local folders. ", "Error");
                throw new FileNotFoundException("File not found\n " + filepath);
            }
            switch (load_mode)
            {
                case 0: //Load EXML
                    return new FileStream(Path.Combine(RenderState.settings.UnpackDir, exmlpath), FileMode.Open);
                case 1: //Load MBIN
                    return new FileStream(Path.Combine(RenderState.settings.UnpackDir, filepath), FileMode.Open);
                case 2: //Load File from Archive
                    {
                        Callbacks.Log("Trying to export File" + effective_filepath, LogVerbosityLevel.INFO);
                        if (resMgr.NMSFileToArchiveMap.ContainsKey(effective_filepath))
                        {
                            Callbacks.Log("File was found in archives. File Index: " + resMgr.NMSFileToArchiveMap[effective_filepath].GetFileIndex(effective_filepath), 
                                LogVerbosityLevel.INFO);
                        }

                        int fileIndex = resMgr.NMSFileToArchiveMap[effective_filepath].GetFileIndex(effective_filepath);
                        return resMgr.NMSFileToArchiveMap[effective_filepath].ExtractFile(fileIndex);
                    }
                    
            }

            return null;
        }

        public static NMSTemplate LoadNMSTemplate(string filepath, ref ResourceManager resMgr)
        {
            int load_mode = 0;
            NMSTemplate template = null;
            filepath = filepath.Replace('\\', '/');
            string effective_filepath = filepath;

            //Checks to prevent malformed paths from further processing
            //TODO: Why do we need this?????
            //if (filepath.Contains(' '))
            //    return null;

            string exmlpath = Path.ChangeExtension(filepath, "exml");
            exmlpath = exmlpath.ToUpper(); //Make upper case
            
            if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, exmlpath)))
                load_mode = 0; //Load Exml
            else if (File.Exists(Path.Combine(RenderState.settings.UnpackDir, filepath)))
                load_mode = 1; //Load MBIN from file
            else if (resMgr.NMSFileToArchiveMap.ContainsKey(filepath))
                load_mode = 2; //Extract file from archive
            else if (resMgr.NMSFileToArchiveMap.ContainsKey("/" + filepath)) //AMUMSS BULLSHIT
            {
                effective_filepath = "/" + filepath;
                load_mode = 2; //Extract file from archive
            }
            else
            {
                Callbacks.Log("File: " + filepath + " Not found in PAKs or local folders. ", LogVerbosityLevel.ERROR);
                Callbacks.showError("File: " + filepath + " Not found in PAKs or local folders. ", "Error");
                return null;
            }

            try
            {
                switch (load_mode)
                {
                    case 0: //Load EXML
                        {
                            string xml = File.ReadAllText(Path.Combine(RenderState.settings.UnpackDir, exmlpath));
                            template = EXmlFile.ReadTemplateFromString(xml);
                            break;
                        }
                    case 1: //Load MBIN
                        {
                            string eff_path = Path.Combine(RenderState.settings.UnpackDir, filepath);
                            MBINFile mbinf = new MBINFile(eff_path);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            break;
                        }
                    case 2: //Load File from Archive
                        {
                            Stream file = resMgr.NMSFileToArchiveMap[effective_filepath].ExtractFile(effective_filepath);
                            MBINFile mbinf = new MBINFile(file);
                            mbinf.Load();
                            template = mbinf.GetData();
                            mbinf.Dispose();
                            break;
                        }
                }
            } catch (Exception ex)
            {
                if (ex is System.IO.DirectoryNotFoundException || ex is System.IO.FileNotFoundException)
                    Callbacks.showError("File " + effective_filepath + " Not Found...", "Error");
                else if (ex is System.IO.IOException)
                    Callbacks.showError("File " + effective_filepath + " problem...", "Error");
                else if (ex is System.Reflection.TargetInvocationException)
                {
                    Callbacks.showError("libMBIN failed to decompile file. If this is a vanilla file, contact the MbinCompiler developer",
                    "Error");
                }
                else
                {
                    Callbacks.showError("Unhandled Exception " + ex.Message, "Error");
                }
                return null;

            }

#if DEBUG
            //Save NMSTemplate to exml
            string data = EXmlFile.WriteTemplate(template);
            string path =  Path.Combine("Temp", filepath + ".exml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, data);
#endif
            return template;
        }

        //Animation frame data collection methods
        public static OpenTK.Mathematics.Quaternion fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            OpenTK.Mathematics.Quaternion q;
            //Check if there is a rotation for that node
            if (node.RotIndex < frame.Rotations.Count)
            {
                int rotindex = node.RotIndex;
                q = new OpenTK.Mathematics.Quaternion(frame.Rotations[rotindex].x,
                                frame.Rotations[rotindex].y,
                                frame.Rotations[rotindex].z,
                                frame.Rotations[rotindex].w);
            }
            else //Load stillframedata
            {
                int rotindex = node.RotIndex - frame.Rotations.Count;
                q = new OpenTK.Mathematics.Quaternion(stillframe.Rotations[rotindex].x,
                                stillframe.Rotations[rotindex].y,
                                stillframe.Rotations[rotindex].z,
                                stillframe.Rotations[rotindex].w);
            }

            return q;
        }

        public static void fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, 
            int frameCounter, ref OpenTK.Mathematics.Quaternion q)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;
            TkAnimNodeFrameData activeFrame = null;
            int rotIndex = -1;
            //Check if there is a rotation for that node
            
            if (node.RotIndex < frame.Rotations.Count)
            {
                activeFrame = frame;
                rotIndex = node.RotIndex;
}
            else //Load stillframedata
            {
                activeFrame = stillframe;
                rotIndex = node.RotIndex - frame.Rotations.Count;
            }

            q.X = activeFrame.Rotations[rotIndex].x;
            q.Y = activeFrame.Rotations[rotIndex].y;
            q.Z = activeFrame.Rotations[rotIndex].z;
            q.W = activeFrame.Rotations[rotIndex].w;

        }


        public static void fetchTransVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Vector3 v)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;
            TkAnimNodeFrameData activeFrame;
            int transIndex = -1;

            //Load Translations
            if (node.TransIndex < frame.Translations.Count)
            {
                transIndex = node.TransIndex;
                activeFrame = frame;
                
            }
            else //Load stillframedata
            {
                transIndex = node.TransIndex - frame.Translations.Count;
                activeFrame = stillframe;
            }


            v.X = activeFrame.Translations[transIndex].x;
            v.Y = activeFrame.Translations[transIndex].y;
            v.Z = activeFrame.Translations[transIndex].z;
        }

        public static Vector3 fetchTransVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            Vector3 v;
            //Load Translations
            if (node.TransIndex < frame.Translations.Count)
            {
                v = new Vector3(frame.Translations[node.TransIndex].x,
                                                    frame.Translations[node.TransIndex].y,
                                                    frame.Translations[node.TransIndex].z);
            }
            else //Load stillframedata
            {
                int transindex = node.TransIndex - frame.Translations.Count;
                v = new Vector3(stillframe.Translations[transindex].x,
                                                    stillframe.Translations[transindex].y,
                                                    stillframe.Translations[transindex].z);
            }

            return v;
        }


        public static Vector3 fetchScaleVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            Vector3 v;

            if (node.ScaleIndex < frame.Scales.Count)
            {
                v = new Vector3(
                    frame.Scales[node.ScaleIndex].x,
                    frame.Scales[node.ScaleIndex].y, 
                    frame.Scales[node.ScaleIndex].z);
            }
            else //Load stillframedata
            {
                int scaleindex = node.ScaleIndex - frame.Scales.Count;
                v = new Vector3(
                    stillframe.Scales[scaleindex].x,
                    stillframe.Scales[scaleindex].y,
                    stillframe.Scales[scaleindex].z);
            }

            return v;
        }

        public static void fetchScaleVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter, ref Vector3 s)
        {
            //Load Frames
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;
            TkAnimNodeFrameData activeFrame = null;
            int scaleIndex = -1;

            if (node.ScaleIndex < frame.Scales.Count)
            {
                scaleIndex = node.ScaleIndex;
                activeFrame = frame;
            }
            else //Load stillframedata
            {
                scaleIndex = node.ScaleIndex - frame.Scales.Count;
                activeFrame = stillframe;
            }

            s.X = activeFrame.Scales[scaleIndex].x;
            s.Y = activeFrame.Scales[scaleIndex].y;
            s.Z = activeFrame.Scales[scaleIndex].z;
            
        }


        //Load Game Archive Handles
        
        public static void loadNMSArchives(string filepath, string gameDir, ref ResourceManager resMgr, ref int status)
        {
            Callbacks.Log("Trying to load PAK files from " + gameDir, LogVerbosityLevel.INFO);
            if (!Directory.Exists(gameDir))
            {
                Callbacks.showError("Unable to locate game Directory. PAK files (Vanilla + Mods) not loaded. You can still work using unpacked files", "Info");
                status = - 1;
                return;
            }
            
            //Load the handles to the resource manager
            
            //Fetch .pak files
            string[] pak_files = Directory.GetFiles(gameDir);
            resMgr.NMSArchiveMap.Clear();

            Callbacks.updateStatus("Loading Vanilla NMS Archives...");

            foreach (string pak_path in pak_files)
            {
                if (!pak_path.EndsWith(".pak"))
                    continue;
                
                try
                {
                    FileStream arc_stream = new FileStream(pak_path, FileMode.Open);
                    libPSARC.PSARC.Archive psarc = new libPSARC.PSARC.Archive(arc_stream, true);
                    Callbacks.Log("Loaded :" + pak_path, LogVerbosityLevel.INFO);
                    resMgr.NMSArchiveMap[pak_path] = psarc;
                }
                catch (Exception ex)
                {
                    Callbacks.showError("An Error Occured : " + ex.Message, "Error");
                    Callbacks.Log("Pak file " + pak_path + " failed to load", LogVerbosityLevel.ERROR);
                    Callbacks.Log("Error : " + ex.GetType().Name + " " + ex.Message, LogVerbosityLevel.ERROR);
                }
            }
            
            if (Directory.Exists(Path.Combine(gameDir, "MODS")))
            {
                pak_files = Directory.GetFiles(Path.Combine(gameDir, "MODS"));
                Callbacks.updateStatus("Loading Modded NMS Archives...");
                foreach (string pak_path in pak_files)
                {
                    if (pak_path.Contains("CUSTOMMODELS"))
                        Console.WriteLine(pak_path);

                    if (!pak_path.EndsWith(".pak"))
                        continue;
                    
                    try
                    {
                        FileStream arc_stream = new FileStream(pak_path, FileMode.Open);
                        libPSARC.PSARC.Archive psarc = new libPSARC.PSARC.Archive(arc_stream, true);
                        resMgr.NMSArchiveMap[pak_path] = psarc;
                    }
                    catch (Exception ex)
                    {
                        Callbacks.showError("An Error Occured : " + ex.Message, "Error");
                        Callbacks.Log("Pak file " + pak_path + " failed to load", LogVerbosityLevel.ERROR);
                        Callbacks.Log("Error : " + ex.GetType().Name + " " + ex.Message, LogVerbosityLevel.ERROR);
                    }
                }
            }

            if (resMgr.NMSArchiveMap.Keys.Count == 0)
            {
                Callbacks.Log("No pak files found. Not creating/reading manifest file.", LogVerbosityLevel.WARNING);
                return;
            }

            //Populate resource manager with the files
            Callbacks.updateStatus("Populating Resource Manager...");
            foreach (string arc_path in resMgr.NMSArchiveMap.Keys.Reverse())
            {
                libPSARC.PSARC.Archive arc = resMgr.NMSArchiveMap[arc_path];

                foreach (string f in arc.filePaths)
                {
                    resMgr.NMSFileToArchiveMap[f] = resMgr.NMSArchiveMap[arc_path];
                }
            }


            //Detect and save list of scene files
            foreach (string path in RenderState.activeResMgr.NMSFileToArchiveMap.Keys)
            {
                if (path.EndsWith(".SCENE.MBIN"))
                    resMgr.NMSSceneFilesList.Add(path);
            }
            resMgr.NMSSceneFilesList.Sort();


            status = 0; // All good
            Callbacks.updateStatus("Ready");
        }

        public static void unloadNMSArchives(ResourceManager resMgr)
        {
            foreach (libPSARC.PSARC.Archive arc in resMgr.NMSArchiveMap.Values)
            {
                arc.Dispose();
            }
            resMgr.NMSArchiveMap.Clear();
            resMgr.NMSFileToArchiveMap.Clear();
            resMgr.NMSSceneFilesList.Clear();
        }

        public static string getGameInstallationDir()
        {
            //Registry keys
            string gog32_keyname = @"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games\1446213994";
            string gog32_keyval = "PATH";

            string gog64_keyname = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\GOG.com\Games\1446213994";
            string gog64_keyval = "PATH";

            //Check Steam
            string val = null;


            val = fetchSteamGameInstallationDir() as string;
            if (val != null)
            {
                Callbacks.Log("Found Steam Version: " + val, LogVerbosityLevel.INFO);
                return val;
            } else
                Callbacks.Log("Unable to find Steam Version: ", LogVerbosityLevel.INFO);
                
            //Check GOG32
            val = Registry.GetValue(gog32_keyname, gog32_keyval, "") as string;
            if (val != null)
            {
                Callbacks.Log("Found GOG32 Version: " + val, LogVerbosityLevel.INFO);
                return val;
            }
            else
                Callbacks.Log("Unable to find GOG32 Version: " + val, LogVerbosityLevel.INFO);

            
            //Check GOG64
            val = Registry.GetValue(gog64_keyname, gog64_keyval, "") as string;
            if (val != null)
            {
                Callbacks.Log("Found GOG64 Version: " + val, LogVerbosityLevel.INFO);
                return val;
            }
            else
                Callbacks.Log("Unable to find GOG64 Version: " + val, LogVerbosityLevel.INFO);

            return "";
        }

        private static string fetchSteamGameInstallationDir()
        {
            //At first try to find the steam installation folder

            //Try to fetch the installation dir
            string steam_keyname = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam";
            string steam_keyval = "InstallPath";
            string nms_id = "275850";

            //Fetch Steam Installation Folder
            
            string steam_path = Registry.GetValue(steam_keyname, steam_keyval, null) as string;

            if (steam_path is null)
            {
                Callbacks.Log("Failed to find Steam Installation: ", LogVerbosityLevel.INFO);
                return null;
            }
                
            Callbacks.Log("Found Steam Installation: " + steam_path, LogVerbosityLevel.INFO);
            Callbacks.Log("Searching for NMS in the default steam directory...", LogVerbosityLevel.INFO);

            //At first try to find acf entries in steam installation dir
            foreach (string path in Directory.GetFiles(Path.Combine(steam_path, "steamapps")))
            {
                if (!path.EndsWith(".acf"))
                    continue;

                if (path.Contains(nms_id))
                    return Path.Combine(steam_path, @"steamapps\common\No Man's Sky\GAMEDATA");
            }

            Callbacks.Log("NMS not found in default folders. Searching Steam Libraries...", LogVerbosityLevel.INFO);

            //If that did't work try to load the libraryfolders.vdf
            StreamReader sr = new StreamReader(Path.Combine(steam_path, @"steamapps\libraryfolders.vdf"));
            List<string> libraryPaths = new List<string>();

            int line_count = 0;
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                if (line_count < 4)
                {
                    line_count++;
                    continue;
                }

                if (!line.StartsWith("\t"))
                    continue;
                    
                string[] split = line.Split('\t');
                string path = split[split.Length - 1];
                path = path.Trim('\"');
                path = path.Replace("\\\\", "\\");
                libraryPaths.Add(Path.Combine(path, "steamapps"));
            }
            
            //Check all library paths for the acf file

            foreach (string path in libraryPaths)
            {
                foreach (string filepath in Directory.GetFiles(path))
                {
                    if (!filepath.EndsWith(".acf"))
                        continue;

                    if (filepath.Contains(nms_id))
                        return Path.Combine(path, @"common\No Man's Sky\GAMEDATA");
                }
            }

            Callbacks.Log("Unable to locate Steam Installation...", LogVerbosityLevel.INFO);
            return null;
        }

        private static FieldInfo getField<T>(T item, string fieldName)
        {
            return item.GetType().GetField(fieldName);
        }

        private static object getFieldValue(object item, FieldInfo temp)
        {
            return temp.GetValue(item);
        }

        public static string parseNMSTemplateAttrib(List<TkSceneNodeAttributeData> temp, string attrib)
        {

            foreach (TkSceneNodeAttributeData elem in temp)
            {
                //Get Name field
                FieldInfo nameField = getField(elem, "Name");
                FieldInfo valueField = getField(elem, "Value");

                NMSString0x10 iitem = getFieldValue(elem, nameField) as NMSString0x10;
                if (iitem.Value == attrib)
                {
                    //Fetch and return the value
                    NMSString0x100 ival = getFieldValue(elem, valueField) as NMSString0x100;
                    return ival.Value;

                }
            }

            return "";
        }

    }
}
