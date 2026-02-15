using System.Collections.Generic;

namespace PicoImageViewer.Data
{
    /// <summary>
    /// Represents a subfolder (row) containing images (columns).
    /// </summary>
    [System.Serializable]
    public class FolderData
    {
        public string FolderName;
        public string FolderPath;
        public int RowIndex;
        public List<ImageData> Images = new List<ImageData>();

        public FolderData(string folderName, string folderPath, int rowIndex)
        {
            FolderName = folderName;
            FolderPath = folderPath;
            RowIndex = rowIndex;
        }
    }
}
