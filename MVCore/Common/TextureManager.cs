using System;
using System.Collections.Generic;
using System.Text;
using MVCore.Common;

namespace MVCore
{
    public class TextureManager : IBaseResourceManager
    {
        public Dictionary<string, Texture> GLtextures = new();
        private TextureManager masterTexManager;

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
            //Search on the masterTextureManager first
            if (masterTexManager != null && masterTexManager.HasTexture(name))
                return true;
            else
                return GLtextures.ContainsKey(name);
        }

        public void AddTexture(Texture t)
        {
            GLtextures[t.name] = t;
        }

        public Texture GetTexture(string name)
        {
            //Fetches the textures from the masterTexture Manager if it exists
            if (masterTexManager != null && masterTexManager.HasTexture(name))
                return masterTexManager.GetTexture(name);
            else
                return GLtextures[name];
        }

        public void SetMasterTexManager(TextureManager mtMgr)
        {
            masterTexManager = mtMgr;
        }

    }
}
