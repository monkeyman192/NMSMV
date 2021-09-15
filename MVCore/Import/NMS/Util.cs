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

namespace MVCore.Import.NMS
{
    public static class Util
    {
        

        public static Dictionary<string, TextureUnit> MapTextureUnit = new()
        {
            { "mpCustomPerMaterial.gDiffuseMap", TextureUnit.Texture0 },
            { "mpCustomPerMaterial.gMasksMap", TextureUnit.Texture1 },
            { "mpCustomPerMaterial.gNormalMap", TextureUnit.Texture2 },
            { "mpCustomPerMaterial.gDiffuse2Map", TextureUnit.Texture3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", TextureUnit.Texture4 },
            { "mpCustomPerMaterial.gDetailNormalMap", TextureUnit.Texture5 }
        };

        public static Dictionary<string, int> MapTexUnitToSampler = new()
        {
            { "mpCustomPerMaterial.gDiffuseMap", 0 },
            { "mpCustomPerMaterial.gMasksMap", 1 },
            { "mpCustomPerMaterial.gNormalMap", 2 },
            { "mpCustomPerMaterial.gDiffuse2Map", 3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", 4 },
            { "mpCustomPerMaterial.gDetailNormalMap", 5 }
        };


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


        //Texture Utilities
        
        private static void loadSamplerTexture(Sampler sampler, TextureManager texMgr)
        {
            if (sampler.Map == "")
                return;

            //Try to load the texture
            if (texMgr.HasTexture(sampler.Map))
            {
                sampler.Tex = texMgr.GetTexture(sampler.Map);
            }
            else
            {
                Texture tex = new Texture(sampler.Map);
                tex.palOpt = new PaletteOpt(false);
                tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                //At this point this should be a common texture. Store it to the master texture manager
                RenderState.engineRef.resourceMgmtSys.texMgr.AddTexture(tex);
                sampler.Tex = tex;
            }
        }

        public static void PrepareProcGenSamplers(MeshMaterial mat, TextureManager texMgr)
        {
            //Workaround for Procedurally Generated Samplers
            //I need to check if the diffuse sampler is procgen and then force the maps
            //on the other samplers with the appropriate names
            //TODO: Go through the process of loading procedural textures again. I don't like this at all

            foreach (Sampler s in mat.Samplers)
            {
                //Check if the first sampler is procgen
                if (s.isProcGen)
                {
                    string name = s.Map;

                    //Properly assemble the mask and the normal map names

                    string[] split = name.Split('.');
                    string pre_ext_name = "";
                    for (int i = 0; i < split.Length - 1; i++)
                        pre_ext_name += split[i] + '.';

                    if (mat.SamplerMap.ContainsKey("mpCustomPerMaterial.gMasksMap"))
                    {
                        string new_name = pre_ext_name + "MASKS.DDS";
                        mat.SamplerMap["mpCustomPerMaterial.gMasksMap"].Map = new_name;
                        mat.SamplerMap["mpCustomPerMaterial.gMasksMap"].Tex = texMgr.GetTexture(new_name);
                    }
                    else if (mat.SamplerMap.ContainsKey("mpCustomPerMaterial.gNormalMap"))
                    {
                        string new_name = pre_ext_name + "NORMAL.DDS";
                        mat.SamplerMap["mpCustomPerMaterial.gNormalMap"].Map = new_name;
                        mat.SamplerMap["mpCustomPerMaterial.gNormalMap"].Tex = texMgr.GetTexture(new_name); ;
                    }
                    break;
                }
            }
        }

        public static void PrepareSamplerTextures(Sampler sampler, TextureManager texMgr)
        {
            //Save texture to material
            switch (sampler.Name)
            {
                case "mpCustomPerMaterial.gDiffuseMap":
                case "mpCustomPerMaterial.gDiffuse2Map":
                case "mpCustomPerMaterial.gMasksMap":
                case "mpCustomPerMaterial.gNormalMap":
                    sampler.texUnit = MapTextureUnit[sampler.Name];
                    sampler.SamplerID = MapTexUnitToSampler[sampler.Name];
                    break;
                default:
                    Callbacks.Log("Not sure how to handle Sampler " + sampler.Name, LogVerbosityLevel.WARNING);
                    return;
            }
            
            string[] split = sampler.Map.Split('.');
            
            string temp = "";
            if (sampler.Name == "mpCustomPerMaterial.gDiffuseMap")
            {
                //Check if the sampler describes a proc gen texture
                temp = split[0];
                //Construct main filename
                
                string texMbin = temp + ".TEXTURE.MBIN";
                
                //Detect Procedural Texture
                if (FileUtils.NMSFileToArchiveMap.Keys.Contains(texMbin))
                {
                    TextureMixer.combineTextures(sampler.Map, Palettes.paletteSel, ref texMgr);
                    //Override Map
                    sampler.Map = temp + "DDS";
                    sampler.isProcGen = true;
                }
            }

            //Load the texture to the sampler
            loadSamplerTexture(sampler, texMgr);
        }
        
    }
}
