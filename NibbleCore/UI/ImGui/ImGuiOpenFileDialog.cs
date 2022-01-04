using ImGuiCore = ImGuiNET.ImGui;
using System;
using System.Collections.Generic;
using System.IO;
using Num = System.Numerics;


namespace NbCore.UI.ImGui
{
	public class OpenFileDialog
	{
		
		static readonly Num.Vector4 YELLOW_TEXT_COLOR = new Num.Vector4(1.0f, 1.0f, 0.0f, 1.0f);

		private string _uid;
		private FilePicker filePicker = null;
		public bool IsOpen = false;
		private bool show_open_file_dialog = false;

		public OpenFileDialog(string uid, string startingPath, string searchFilter = null, bool onlyAllowFolders = false)
        {
			_uid = uid;
			filePicker = new();
			filePicker.SelectedFile = "";
			filePicker.RootFolder = startingPath;
			filePicker.CurrentFolder = startingPath;
			filePicker.OnlyAllowFolders = onlyAllowFolders;
			
			if (searchFilter != null)
			{
				if (filePicker.AllowedExtensions != null)
					filePicker.AllowedExtensions.Clear();
				else
					filePicker.AllowedExtensions = new List<string>();

				filePicker.AllowedExtensions.AddRange(searchFilter.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
			}
		}

		private void DrawDirectoryFiles(DirectoryInfo di)
        {
			var fileSystemEntries = GetFileSystemEntries(di.FullName);
			foreach (var fse in fileSystemEntries)
			{
				if (Directory.Exists(fse))
				{
					var name = Path.GetFileName(fse);
					ImGuiCore.PushStyleColor(ImGuiNET.ImGuiCol.Text, YELLOW_TEXT_COLOR);
					if (ImGuiCore.Selectable(name + "/", false, ImGuiNET.ImGuiSelectableFlags.DontClosePopups))
						filePicker.CurrentFolder = fse;
					ImGuiCore.PopStyleColor();
				}
				else
				{
					var name = Path.GetFileName(fse);
					bool isSelected = filePicker.SelectedFile == fse;
					if (ImGuiCore.Selectable(name, isSelected, ImGuiNET.ImGuiSelectableFlags.DontClosePopups) || ImGuiCore.IsMouseDoubleClicked(0))
						filePicker.SelectedFile = fse;
				}
			}

		}

		public void Open()
        {
			filePicker.SelectedFile = "";
			show_open_file_dialog = true;
		}

		private void Close()
        {
			ImGuiCore.CloseCurrentPopup();
		}

		public bool isFileSelected()
        {
			return filePicker.SelectedFile != "";
        }

		public string GetSelectedFile()
        {
			return filePicker.SelectedFile;
        }
		
		public bool Draw(System.Numerics.Vector2 winsize)
		{
			if (show_open_file_dialog)
			{
				ImGuiCore.OpenPopup(_uid);
				show_open_file_dialog = false;
			}

			bool isopen = true;
			if (ImGuiCore.BeginPopupModal(_uid, ref isopen, ImGuiNET.ImGuiWindowFlags.None))
			{
				if (filePicker.CurrentFolder == null)
				{
					ImGuiCore.Text("My Computer");
					if (ImGuiCore.BeginChildFrame(1, winsize))
					{
						//Draw Drives
						var driveList = DriveInfo.GetDrives();
						foreach (var de in driveList)
						{
							if (Directory.Exists(de.RootDirectory.FullName))
							{
								var name = de.RootDirectory.FullName;
								ImGuiCore.PushStyleColor(ImGuiNET.ImGuiCol.Text, YELLOW_TEXT_COLOR);
								if (ImGuiCore.Selectable(name, false, ImGuiNET.ImGuiSelectableFlags.DontClosePopups))
									filePicker.CurrentFolder = de.RootDirectory.FullName;
								ImGuiCore.PopStyleColor();
							}
						}
						ImGuiCore.EndChildFrame();
					}

					if (ImGuiCore.Button("Cancel"))
					{
						Close();
					}
				}

				ImGuiCore.Text("Current Folder: " + filePicker.CurrentFolder);

				if (ImGuiCore.BeginChildFrame(1, winsize))
				{
					var di = new DirectoryInfo(filePicker.CurrentFolder);
					if (di.Exists)
					{
						if (ImGuiCore.Selectable("../", false, ImGuiNET.ImGuiSelectableFlags.DontClosePopups))
						{
							if (di.Parent != null)
							{
								filePicker.CurrentFolder = di.Parent.FullName;
								DrawDirectoryFiles(di.Parent);
							}
							else
							{
								filePicker.CurrentFolder = null;
							}
						}
						else
						{
							DrawDirectoryFiles(di);
						}
					}
				}
				ImGuiCore.EndChildFrame();


				if (ImGuiCore.Button("Cancel"))
				{
					Close();
				}

				if (filePicker.OnlyAllowFolders)
				{
					ImGuiCore.SameLine();
					if (ImGuiCore.Button("Open"))
					{
						filePicker.SelectedFile = filePicker.CurrentFolder;
						Close();
					}
				}
				else if (filePicker.SelectedFile != null)
				{
					ImGuiCore.SameLine();
					if (ImGuiCore.Button("Open"))
					{
						Close();
						return true;
					}
				}

				ImGuiCore.EndPopup();
			}

			return false;

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
				else if (!filePicker.OnlyAllowFolders)
				{
					if (filePicker.AllowedExtensions != null)
					{
						foreach (string ext in filePicker.AllowedExtensions)
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