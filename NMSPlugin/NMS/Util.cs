using System;
using System.Collections.Generic;
using System.IO;
using libMBIN;
using NbCore.Math;
using libMBIN.NMS.Toolkit;
using System.Security.Permissions;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using Microsoft.Win32;
using Newtonsoft.Json;
using Path = System.IO.Path;
using NbCore;
using System.Windows;
using System.Reflection;
using libMBIN.NMS;

namespace NMSPlugin
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
        public static NbQuaternion fetchRotQuaternion(TkAnimNodeData node, TkAnimMetadata animMeta, 
            int frameCounter)
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

            NbQuaternion q = new();
            q.X = activeFrame.Rotations[rotIndex].x;
            q.Y = activeFrame.Rotations[rotIndex].y;
            q.Z = activeFrame.Rotations[rotIndex].z;
            q.W = activeFrame.Rotations[rotIndex].w;

            return q;
        }


        public static NbVector3 fetchTransVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
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

            NbVector3 v = new();
            v.X = activeFrame.Translations[transIndex].x;
            v.Y = activeFrame.Translations[transIndex].y;
            v.Z = activeFrame.Translations[transIndex].z;

            return v;
        }

        public static NbVector3 fetchScaleVector(TkAnimNodeData node, TkAnimMetadata animMeta, int frameCounter)
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

            NbVector3 s = new NbVector3();
            s.X = activeFrame.Scales[scaleIndex].x;
            s.Y = activeFrame.Scales[scaleIndex].y;
            s.Z = activeFrame.Scales[scaleIndex].z;

            return s;
        }


        //Texture Utilities
        
        public static void loadSamplerTexture(Sampler sampler, TextureManager texMgr)
        {
            if (sampler.Map == "")
                return;

            //Try to load the texture
            if (texMgr.HasTexture(sampler.Map))
            {
                sampler.Tex = texMgr.Get(sampler.Map);
            }
            else
            {
                Texture tex = new Texture(sampler.Map);
                tex.palOpt = new PaletteOpt(false);
                tex.procColor = new NbVector4(1.0f, 1.0f, 1.0f, 0.0f);
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
                        mat.SamplerMap["mpCustomPerMaterial.gMasksMap"].Tex = texMgr.Get(new_name);
                    }
                    else if (mat.SamplerMap.ContainsKey("mpCustomPerMaterial.gNormalMap"))
                    {
                        string new_name = pre_ext_name + "NORMAL.DDS";
                        mat.SamplerMap["mpCustomPerMaterial.gNormalMap"].Map = new_name;
                        mat.SamplerMap["mpCustomPerMaterial.gNormalMap"].Tex = texMgr.Get(new_name); ;
                    }
                    break;
                }
            }
        }

        
        
    }
}
