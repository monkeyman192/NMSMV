using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using Num = System.Numerics;

namespace ImGuiHelper
{
	public class FilePicker
	{
		static readonly Dictionary<object, FilePicker> _filePickers = new Dictionary<object, FilePicker>();
		static readonly Num.Vector4 YELLOW_TEXT_COLOR = new Num.Vector4(1.0f, 1.0f, 0.0f, 1.0f);

		public string RootFolder;
		public string CurrentFolder;
		public string SelectedFile;
		public List<string> AllowedExtensions;
		public bool OnlyAllowFolders;

		public static FilePicker GetFolderPicker(object o, string startingPath)
			=> GetFilePicker(o, startingPath, null, true);

		public static FilePicker GetFilePicker(object o, string startingPath, string searchFilter = null, bool onlyAllowFolders = false)
		{
			if (File.Exists(startingPath))
			{
				startingPath = new FileInfo(startingPath).DirectoryName;
			}
			else if (string.IsNullOrEmpty(startingPath) || !Directory.Exists(startingPath))
			{
				startingPath = Environment.CurrentDirectory;
				if (string.IsNullOrEmpty(startingPath))
					startingPath = AppContext.BaseDirectory;
			}

			if (!_filePickers.TryGetValue(o, out FilePicker fp))
			{
				fp = new FilePicker();
				fp.RootFolder = startingPath;
				fp.CurrentFolder = startingPath;
				fp.OnlyAllowFolders = onlyAllowFolders;

				if (searchFilter != null)
				{
					if (fp.AllowedExtensions != null)
						fp.AllowedExtensions.Clear();
					else
						fp.AllowedExtensions = new List<string>();

					fp.AllowedExtensions.AddRange(searchFilter.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
				}

				_filePickers.Add(o, fp);
			}

			return fp;
		}

		public static void RemoveFilePicker(object o) => _filePickers.Remove(o);

		private bool DrawDirectoryFiles(DirectoryInfo di)
        {
			bool result = false;
			var fileSystemEntries = GetFileSystemEntries(di.FullName);
			foreach (var fse in fileSystemEntries)
			{
				if (Directory.Exists(fse))
				{
					var name = Path.GetFileName(fse);
					ImGui.PushStyleColor(ImGuiCol.Text, YELLOW_TEXT_COLOR);
					if (ImGui.Selectable(name + "/", false, ImGuiSelectableFlags.DontClosePopups))
						CurrentFolder = fse;
					ImGui.PopStyleColor();
				}
				else
				{
					var name = Path.GetFileName(fse);
					bool isSelected = SelectedFile == fse;
					if (ImGui.Selectable(name, isSelected, ImGuiSelectableFlags.DontClosePopups))
						SelectedFile = fse;

					if (ImGui.IsMouseDoubleClicked(0))
					{
						result = true;
						ImGui.CloseCurrentPopup();
					}
				}
			}

			return result;
		}

		public bool Draw(System.Numerics.Vector2 winsize)
		{
			if (CurrentFolder == null)
            {
				ImGui.Text("My Computer");
				if (ImGui.BeginChildFrame(1, winsize))
				{
					//Draw Drives
					var driveList = DriveInfo.GetDrives();
					foreach (var de in driveList)
					{
						if (Directory.Exists(de.RootDirectory.FullName))
						{
							var name = de.RootDirectory.FullName;
							ImGui.PushStyleColor(ImGuiCol.Text, YELLOW_TEXT_COLOR);
							if (ImGui.Selectable(name, false, ImGuiSelectableFlags.DontClosePopups))
								CurrentFolder = de.RootDirectory.FullName;
							ImGui.PopStyleColor();
						}
					}
					ImGui.EndChildFrame();
				}

				if (ImGui.Button("Cancel"))
				{
					ImGui.CloseCurrentPopup();
				}

				return false;
			}

			ImGui.Text("Current Folder: " + CurrentFolder);
			bool result = false;

			if (ImGui.BeginChildFrame(1, winsize))
			{
				var di = new DirectoryInfo(CurrentFolder);
				if (di.Exists)
				{
					if (ImGui.Selectable("../", false, ImGuiSelectableFlags.DontClosePopups))
					{
						if (di.Parent != null)
                        {
							CurrentFolder = di.Parent.FullName;
							result = DrawDirectoryFiles(di.Parent);
						}
                        else
                        {
							CurrentFolder = null;
						}
					} else
                    {
						result = DrawDirectoryFiles(di);
					}
				}
			}
			ImGui.EndChildFrame();


			if (ImGui.Button("Cancel"))
			{
				result = false;
				ImGui.CloseCurrentPopup();
			}

			if (OnlyAllowFolders)
			{
				ImGui.SameLine();
				if (ImGui.Button("Open"))
				{
					result = true;
					SelectedFile = CurrentFolder;
					ImGui.CloseCurrentPopup();
				}
			}
			else if (SelectedFile != null)
			{
				ImGui.SameLine();
				if (ImGui.Button("Open"))
				{
					result = true;
					ImGui.CloseCurrentPopup();
				}
			}

			return result;
		}

		bool TryGetFileInfo(string fileName, out FileInfo realFile)
		{
			try
			{
				realFile = new FileInfo(fileName);
				return true;
			}
			catch
			{
				realFile = null;
				return false;
			}
		}

		List<string> GetFileSystemEntries(string fullName)
		{
			var files = new List<string>();
			var dirs = new List<string>();

			foreach (var fse in Directory.GetFileSystemEntries(fullName, ""))
			{
				if (Directory.Exists(fse))
				{
					dirs.Add(fse);
				}
				else if (!OnlyAllowFolders)
				{
					if (AllowedExtensions != null)
					{
						foreach (string ext in AllowedExtensions)
                        {
							if (fse.ToLower().EndsWith(ext.ToLower()))
								files.Add(fse);
						}
					}
					else
					{
						files.Add(fse);
					}
				}
			}

			var ret = new List<string>(dirs);
			ret.AddRange(files);

			return ret;
		}

	}
}