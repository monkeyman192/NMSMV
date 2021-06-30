using System;
using System.Collections.Generic;
using System.Windows;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common.Input;
using OpenTK.Input;

namespace MVCore.Input
{
    public class KeyboardHandler
    {
        //Designed for maintaining KeyStroke Status
        public Dictionary<Keys, bool> KeyDown = new Dictionary<Keys, bool>();

        //Constructor
        public KeyboardHandler()
        {
            KeyDown[Keys.W] = false;
            KeyDown[Keys.A] = false;
            KeyDown[Keys.S] = false;
            KeyDown[Keys.D] = false;
            KeyDown[Keys.R] = false;
            KeyDown[Keys.F] = false;
            KeyDown[Keys.Q] = false;
            KeyDown[Keys.E] = false;
            KeyDown[Keys.Z] = false;
            KeyDown[Keys.C] = false;
        }

        //Update Position
        public void updateState()
        {
            //TODO: At some point get rid of this shit
        
        }

        public void setKeyState(Keys k, bool state)
        {
            KeyDown[k] = state;
        }

        public int getKeyStatus(Keys k)
        {
            return KeyDown[k] ? 1 : 0;
        }
    }
}
