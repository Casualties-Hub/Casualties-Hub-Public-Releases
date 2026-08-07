namespace Casualties_Hub.Models;
public class ProtectedFile
{
    public string RelativePath { get; set; } = "";
    public string SavedPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public string DisplayLabel => $"{(IsDirectory ? "Folder" : "File")}  —  {RelativePath}";
}
