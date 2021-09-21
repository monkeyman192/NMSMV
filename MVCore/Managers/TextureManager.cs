using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore
{
    public class TextureManager
    {
        public Dictionary<string, Texture> GLtextures = new();
        


        public TextureManager()
        {

        }

        public void Cleanup()
        {
            DeleteTextures();
            RemoveTextures();
        }

        public void DeleteTextures()
        {
            foreach (Texture p in GLtextures.Values)
                p.Dispose();
        }

        public void RemoveTextures()
        {
            //Warning does not free the textures. Use wisely
            GLtextures.Clear();
        }

        public bool HasTexture(string name)
        {
            return GLtextures.ContainsKey(name);
        }

        public void AddTexture(Texture t)
        {
            GLtextures[t.name] = t;
        }

        public Texture GetTexture(string name)
        {
            return GLtextures[name];
        }


    }
}
