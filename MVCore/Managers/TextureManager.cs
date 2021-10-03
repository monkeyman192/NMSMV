using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Managers;

namespace MVCore
{
    public class TextureManager : EntityManager<Texture>
    {
        Dictionary<string, Texture> TextureMap = new();
        
        public TextureManager()
        {

        }

        public override void CleanUp()
        {
            DeleteTextures();
            base.CleanUp();
        }

        public void DeleteTextures()
        {
            foreach (Texture p in Entities)
                p.Dispose();
        }

        public bool HasTexture(string name)
        {
            return TextureMap.ContainsKey(name);
        }

        public bool AddTexture(Texture t)
        {
            if (!HasTexture(t.Name))
            {
                TextureMap[t.Name] = t;
                return Add(t);
            }
            else
                return false;
            
        }

        public Texture Get(string name)
        {
            return TextureMap[name];
        }


    }
}
