using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using MVCore.Common;

namespace ImGuiHelper
{
    unsafe public class ImGuiPakBrowser
    {
        ImGuiTextFilterPtr _filter;
        string selected_item = "";
        bool DialogFinished;

        public ImGuiPakBrowser()
        {
            var filterPtr = ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null);
            _filter = new ImGuiTextFilterPtr(filterPtr);
            DialogFinished = false;
        }

        public void Draw()
        {
            _filter.Draw("Filter");
            //Draw listbox
            if (ImGui.BeginChild("ListBox",
                            new System.Numerics.Vector2(ImGui.GetWindowSize().X, 250),
                            true))
            {
                foreach (var line in RenderState.activeResMgr.NMSSceneFilesList)
                {
                    if (_filter.PassFilter(line))
                    {
                        if (ImGui.Selectable(line, line == selected_item))
                        {
                            selected_item = line;
                        }
                    }

                }
            }

            ImGui.EndChild();
            ImGui.Text(string.Format("Selected Item: {0}", selected_item));
            ImGui.SameLine();
            if (ImGui.Button("Open"))
                DialogFinished = true;
        }

        public void Clear()
        {
            _filter.Clear();
            selected_item = "";
            DialogFinished = false;
        }

        public bool isFinished()
        {
            return DialogFinished;
        }

        public void Destroy()
        {
            ImGuiNative.ImGuiTextFilter_destroy(_filter.NativePtr);
        }
    }

}
