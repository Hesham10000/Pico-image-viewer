using System;
using System.Collections;
using UnityEngine;

namespace PicoImageViewer.Android
{
    /// <summary>
    /// Handles Android runtime permission requests for file access on Pico devices.
    /// Supports both legacy WRITE_EXTERNAL_STORAGE and API 30+ MANAGE_EXTERNAL_STORAGE.
    /// </summary>
    public class AndroidPermissions : MonoBehaviour
    {
        public static AndroidPermissions Instance { get; private set; }

        public Action<bool> OnPermissionResult;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Check and request storage permissions. Calls onResult(true) if granted.
        /// </summary>
        public void RequestStoragePermissions(Action<bool> onResult)
        {
            OnPermissionResult = onResult;

#if UNITY_ANDROID && !UNITY_EDITOR
            StartCoroutine(RequestPermissionsCoroutine());
#else
            onResult?.Invoke(true);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator RequestPermissionsCoroutine()
        {
            // Check if we already have permissions
            if (HasStoragePermission())
            {
                OnPermissionResult?.Invoke(true);
                yield break;
            }

            // API 30+: need MANAGE_EXTERNAL_STORAGE
            int apiLevel = GetApiLevel();
            if (apiLevel >= 30)
            {
                RequestManageExternalStorage();
                // Wait a frame for the settings activity
                yield return new WaitForSeconds(0.5f);

                // We can't truly wait for the user to return from settings,
                // so we check periodically
                float timeout = 30f;
                float elapsed = 0f;
                while (elapsed < timeout && !HasStoragePermission())
                {
                    yield return new WaitForSeconds(1f);
                    elapsed += 1f;
                }

                OnPermissionResult?.Invoke(HasStoragePermission());
            }
            else
            {
                // API < 30: request READ/WRITE_EXTERNAL_STORAGE
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                        "android.permission.READ_EXTERNAL_STORAGE"))
                {
                    UnityEngine.Android.Permission.RequestUserPermission(
                        "android.permission.READ_EXTERNAL_STORAGE");
                    yield return new WaitForSeconds(0.5f);
                }

                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                        "android.permission.WRITE_EXTERNAL_STORAGE"))
                {
                    UnityEngine.Android.Permission.RequestUserPermission(
                        "android.permission.WRITE_EXTERNAL_STORAGE");
                    yield return new WaitForSeconds(0.5f);
                }

                // Check result
                bool granted = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    "android.permission.READ_EXTERNAL_STORAGE");
                OnPermissionResult?.Invoke(granted);
            }
        }

        private bool HasStoragePermission()
        {
            int apiLevel = GetApiLevel();
            if (apiLevel >= 30)
            {
                using (var env = new AndroidJavaClass("android.os.Environment"))
                {
                    return env.CallStatic<bool>("isExternalStorageManager");
                }
            }
            else
            {
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    "android.permission.READ_EXTERNAL_STORAGE");
            }
        }

        private void RequestManageExternalStorage()
        {
            try
            {
                using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                           .GetStatic<AndroidJavaObject>("currentActivity"))
                using (var settings = new AndroidJavaClass("android.provider.Settings"))
                {
                    string action = "android.settings.MANAGE_ALL_FILES_ACCESS_PERMISSION";
                    using (var intent = new AndroidJavaObject("android.content.Intent", action))
                    {
                        activity.Call("startActivity", intent);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidPermissions] Failed to request MANAGE_EXTERNAL_STORAGE: {ex.Message}");
            }
        }

        private int GetApiLevel()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return version.GetStatic<int>("SDK_INT");
            }
        }
#endif
    }
}
