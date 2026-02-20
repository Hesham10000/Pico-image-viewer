using System;
using UnityEngine;

namespace PicoImageViewer.Android
{
    /// <summary>
    /// Android file/folder picker using Storage Access Framework (SAF) on API 29+.
    /// Falls back to a manual path input approach when SAF is unavailable.
    /// </summary>
    public static class AndroidFilePicker
    {
        private static Action<string> _onFolderPicked;

        /// <summary>
        /// Opens the Android folder picker via SAF. On non-Android platforms,
        /// invokes the callback with null (UI should use manual path input).
        /// </summary>
        public static void PickFolder(Action<string> callback)
        {
            _onFolderPicked = callback;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var activity = GetCurrentActivity())
                using (var intent = new AndroidJavaObject("android.content.Intent",
                    "android.intent.action.OPEN_DOCUMENT_TREE"))
                {
                    intent.Call<AndroidJavaObject>("addFlags", 1); // FLAG_GRANT_READ_URI_PERMISSION
                    var chooser = intent.CallStatic<AndroidJavaObject>(
                        "createChooser", intent, "Select image folder");

                    activity.Call("startActivityForResult", intent, RequestCode);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidFilePicker] SAF picker failed: {ex.Message}. " +
                                 "User should enter path manually.");
                _onFolderPicked?.Invoke(null);
            }
#else
            Debug.Log("[AndroidFilePicker] Not on Android. Use manual path input.");
            _onFolderPicked?.Invoke(null);
#endif
        }

        private const int RequestCode = 42001;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject GetCurrentActivity()
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        /// <summary>
        /// Called from the Android activity result callback plugin.
        /// Receives the selected folder URI and converts it to a usable file path.
        /// </summary>
        public static void OnActivityResult(int requestCode, int resultCode, AndroidJavaObject data)
        {
            if (requestCode != RequestCode || data == null)
            {
                _onFolderPicked?.Invoke(null);
                return;
            }

            try
            {
                var uri = data.Call<AndroidJavaObject>("getData");
                if (uri == null)
                {
                    _onFolderPicked?.Invoke(null);
                    return;
                }

                string path = ConvertTreeUriToPath(uri);
                _onFolderPicked?.Invoke(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidFilePicker] Failed to process result: {ex.Message}");
                _onFolderPicked?.Invoke(null);
            }
        }

        /// <summary>
        /// Attempts to convert a SAF tree URI to a filesystem path.
        /// Falls back to common known paths on Pico devices.
        /// </summary>
        private static string ConvertTreeUriToPath(AndroidJavaObject uri)
        {
            string uriString = uri.Call<string>("toString");
            Debug.Log($"[AndroidFilePicker] Selected URI: {uriString}");

            // Try to extract path from content URI
            // Format: content://com.android.externalstorage.documents/tree/primary%3ADownload%2Fparadox
            if (uriString.Contains("externalstorage.documents"))
            {
                string encoded = uriString;
                int treeIdx = encoded.IndexOf("/tree/");
                if (treeIdx >= 0)
                {
                    string treePart = encoded.Substring(treeIdx + 6);
                    treePart = Uri.UnescapeDataString(treePart);

                    // "primary:Download/paradox" → "/sdcard/Paradox"
                    if (treePart.StartsWith("primary:"))
                    {
                        string relativePath = treePart.Substring(8);
                        return "/sdcard/" + relativePath;
                    }
                }
            }

            // Fallback: return the URI string and let the caller handle it
            return uriString;
        }
#endif
    }
}
