using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using NbCore.Math;


namespace NbCore
{
    public class AnimationData: Entity
    {
        public string Name;
        public string FileName;
        public AnimationType AnimType;
        public int FrameStart;
        public int FrameEnd;
        public string StartNode;
        public int Priority;
        public float Speed;
        public float ActionStartFrame;
        public float ActionFrame;
        public bool Additive;
        public bool Mirrored;
        public bool Active;
        
        public List<string> Nodes;
        public int FrameCount;

        //Fully unpacked scheme for now. Optimize later
        public Dictionary<string, List<NbVector3>> Translations;
        public Dictionary<string, List<NbQuaternion>> Rotations;
        public Dictionary<string, List<NbVector3>> Scales;
        
        public AnimationData() : base(EntityType.Animation)
        {
            Nodes = new();
            Translations = new();
            Rotations = new();
            Scales = new();
        }

        public void Init(string name, int fc)
        {
            Name = name;
            FrameCount = fc;
        }

        public void Load(string filename)
        {
            throw new NotImplementedException();
        }
        
        public void AddNode(string node)
        {
            Common.Callbacks.Assert(FrameCount != 0, "Zero framecount not allowed");
            
            if (!Nodes.Contains(node))
            {
                Nodes.Add(node);
                Translations[node] = new List<NbVector3>(FrameCount);
                Rotations[node] = new List<NbQuaternion>(FrameCount);
                Scales[node] = new List<NbVector3>(FrameCount);
            }
        }

        public void SetNodeRotation(string node, int frameId, NbQuaternion q)
        {
            Common.Callbacks.Assert(Nodes.Contains(node), "Node id out of bounds");
            Common.Callbacks.Assert(frameId < FrameCount, "Frame id out of bounds");

            Rotations[node][frameId] = q;
        }

        public NbQuaternion GetNodeRotation(string node, int frameId)
        {
            Common.Callbacks.Assert(Nodes.Contains(node), "Node id out of bounds");
            Common.Callbacks.Assert(frameId < FrameCount, "Frame id out of bounds");

            return Rotations[node][frameId];
        }

        public void SetNodeScale(string node, int frameId, NbVector3 t)
        {
            Common.Callbacks.Assert(Nodes.Contains(node), "Node id out of bounds");
            Common.Callbacks.Assert(frameId < FrameCount, "Frame id out of bounds");

            Scales[node][frameId] = t;
        }
        
        public NbVector3 GetNodeScale(string node, int frameId)
        {
            Common.Callbacks.Assert(Nodes.Contains(node), "Node id out of bounds");
            Common.Callbacks.Assert(frameId < FrameCount, "Frame id out of bounds");

            return Scales[node][frameId];
        }
        
        public void SetNodeTranslation(string node, int frameId, NbVector3 t)
        {
            Common.Callbacks.Assert(Nodes.Contains(node), "Node id out of bounds");
            Common.Callbacks.Assert(frameId < FrameCount, "Frame id out of bounds");

            Translations[node][frameId] = t;
        }
        
        public NbVector3 GetNodeTranslation(string node, int frameId)
        {
            Common.Callbacks.Assert(Nodes.Contains(node), "Node id out of bounds");
            Common.Callbacks.Assert(frameId < FrameCount, "Frame id out of bounds");

            return Translations[node][frameId];
        }

        private bool disposedValue = false;
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Translations = null;
                    Rotations = null;
                    Scales = null;
                }

                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
