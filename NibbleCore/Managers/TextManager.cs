using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore.Text
{
    public class TextManager
    {
        public enum Semantic
        {
            FPS = 0x0,
            OCCLUDED_COUNT,
            VERT_COUNT,
            TRIS_COUNT,
            TEXTURE_COUNT,
            CTRL_ID
        }

        public List<GLText> texts = new List<GLText>();
        public Dictionary<Semantic, GLText> textMap = new Dictionary<Semantic, GLText>();
        public TextManager()
        {

        }

        public GLText getText(Semantic sem)
        {
            if (!textMap.ContainsKey(sem))
                return null;
            return textMap[sem];
        }

        public void addText(GLText t, Semantic sem)
        {
            if (t != null)
            {
                texts.Add(t);
                if (textMap.ContainsKey(sem))
                    textMap[sem].Dispose();
                textMap[sem] = t;
            }

        }

        ~TextManager()
        {
            texts.Clear();
        }

        public void cleanup()
        {
            foreach (GLText t in texts)
                t.Dispose();
            texts.Clear();
        }

    }
}
