using UnityEngine;

namespace PicoImageViewer.Data
{
    /// <summary>
    /// Represents a single image file discovered during folder scanning.
    /// </summary>
    [System.Serializable]
    public class ImageData
    {
        public string FileName;
        public string FullPath;
        public string RelativePath; // relative to root folder, used as persistence key
        public string FolderName;
        public int RowIndex;
        public int ColumnIndex;

        public ImageData(string fullPath, string rootPath, string folderName, int row, int col)
        {
            FullPath = fullPath;
            FileName = System.IO.Path.GetFileName(fullPath);
            FolderName = folderName;
            RowIndex = row;
            ColumnIndex = col;

            // Build relative path for persistence matching
            if (!string.IsNullOrEmpty(rootPath) && fullPath.StartsWith(rootPath))
            {
                RelativePath = fullPath.Substring(rootPath.Length).TrimStart('/', '\\');
            }
            else
            {
                RelativePath = FileName;
            }
        }
    }
}
