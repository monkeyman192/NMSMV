using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore
{
    public class LODModelComponent : Component
    {
        private List<LODModelResource> _resources;

        //Properties
        public List<LODModelResource> Resources => _resources;

        public LODModelComponent()
        {
            _resources = new List<LODModelResource>();
        }

        public override Component Clone()
        {
            LODModelComponent lmc = new LODModelComponent();
            lmc.CopyFrom(this);
            return lmc;
        }

        public override void CopyFrom(Component c)
        {
            throw new NotImplementedException();
        }
    }

    public class LODModelResource
    {
        public string FileName;
        public Scene SceneRef = null;
        public float CrossFadeTime;
        public float CrossFadeoverlap;
    }
}
