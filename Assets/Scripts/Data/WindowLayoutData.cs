using System.Collections.Generic;
using UnityEngine;

namespace PicoImageViewer.Data
{
    /// <summary>
    /// Per-window layout override saved to disk.
    /// </summary>
    [System.Serializable]
    public class WindowLayoutEntry
    {
        public string RelativePath; // key to match images across re-scans
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Width;
        public float Height;
        public bool IsHidden;
    }

    /// <summary>
    /// Per-folder layout data containing all window overrides.
    /// </summary>
    [System.Serializable]
    public class FolderLayoutData
    {
        public string RootFolder;
        public List<WindowLayoutEntry> Entries = new List<WindowLayoutEntry>();

        private static string GetPrefsKey(string rootFolder)
        {
            return "PicoImageViewer_Layout_" + rootFolder.GetHashCode();
        }

        public void Save()
        {
            string json = JsonUtility.ToJson(this, true);
            PlayerPrefs.SetString(GetPrefsKey(RootFolder), json);
            PlayerPrefs.Save();
        }

        public static FolderLayoutData Load(string rootFolder)
        {
            string key = GetPrefsKey(rootFolder);
            if (PlayerPrefs.HasKey(key))
            {
                string json = PlayerPrefs.GetString(key);
                var data = JsonUtility.FromJson<FolderLayoutData>(json);
                if (data != null) return data;
            }
            return new FolderLayoutData { RootFolder = rootFolder };
        }

        public WindowLayoutEntry FindEntry(string relativePath)
        {
            return Entries.Find(e => e.RelativePath == relativePath);
        }
    }
}
