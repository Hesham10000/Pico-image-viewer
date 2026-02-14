using UnityEngine;

namespace PicoImageViewer.Data
{
    /// <summary>
    /// Serializable app settings persisted to JSON via PlayerPrefs.
    /// </summary>
    [System.Serializable]
    public class AppSettings
    {
        // Folder
        public string LastRootFolder = "/sdcard/Download/paradox";

        // Grid origin
        public float GridForwardOffset = 2.0f;   // meters in front of user
        public float GridUpOffset = 0.0f;         // meters above/below eye level
        public float GridLeftOffset = 0.0f;       // meters left of center

        // Layout
        public float RowSpacing = 0.8f;           // vertical gap between rows (meters)
        public float ColumnSpacing = 0.6f;        // horizontal gap between columns (meters)
        public float DefaultWindowWidth = 0.5f;   // meters
        public float DefaultWindowHeight = 0.4f;  // meters
        public float WindowScaleMultiplier = 1.0f;
        public bool AutoFitAspect = true;

        // Interaction
        public float DragSensitivity = 1.0f;
        public float ResizeSensitivity = 1.0f;
        public bool SnapToGrid = false;

        // Texture
        public int MaxTextureSize = 2048;

        private const string PrefsKey = "PicoImageViewer_Settings";

        public void Save()
        {
            string json = JsonUtility.ToJson(this, true);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }

        public static AppSettings Load()
        {
            if (PlayerPrefs.HasKey(PrefsKey))
            {
                string json = PlayerPrefs.GetString(PrefsKey);
                return JsonUtility.FromJson<AppSettings>(json);
            }
            return new AppSettings();
        }
    }
}
